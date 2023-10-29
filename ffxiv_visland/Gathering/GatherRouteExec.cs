using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using SharpDX;
using System;
using System.Linq;
using visland.Helpers;

namespace visland.Gathering;

public class GatherRouteExec : IDisposable
{
    public GatherRouteDB.Route? CurrentRoute;
    public int CurrentWaypoint;
    public bool ContinueToNext;
    public bool LoopAtEnd;
    public bool Paused;

    private OverrideCamera _camera = new();
    private OverrideMovement _movement = new();
    private OverrideAFK _afk = new();

    private Throttle _interact = new();
    private Throttle _action = new();
    private CircularBuffer<long> Errors = new(5);

    public GatherRouteExec()
    {
        Svc.Toasts.ErrorToast += CheckToDisable;
    }

    public void Dispose()
    {
        _camera.Dispose();
        _movement.Dispose();
        Svc.Toasts.ErrorToast -= CheckToDisable;
    }

    public unsafe void Update()
    {
        var player = Service.ClientState.LocalPlayer;
        _camera.SpeedH = _camera.SpeedV = default;
        _movement.DesiredPosition = player?.Position ?? new();

        var gathering = Service.Condition[ConditionFlag.OccupiedInQuestEvent] || Service.Condition[ConditionFlag.OccupiedInEvent] || Service.Condition[ConditionFlag.OccupiedSummoningBell];
        bool aboutToBeMounted = Service.Condition[ConditionFlag.Unknown57]; // condition 57 is set while mount up animation is playing
        bool isCurrentlyGathering = Service.Condition[ConditionFlag.Gathering]; // condition 6 is true while player is interacting with a node
        if (player == null || player.IsCasting || gathering || aboutToBeMounted || Paused || CurrentRoute == null || CurrentWaypoint >= CurrentRoute.Waypoints.Count)
            return;

        // ensure we don't get afk-kicked while running the route
        _afk.ResetTimers();

        var wp = CurrentRoute.Waypoints[CurrentWaypoint];
        var toWaypoint = wp.Position - player.Position;
        var toWaypointXZ = new Vector3(toWaypoint.X, 0, toWaypoint.Z);
        bool needToGetCloser = toWaypoint.LengthSquared() > wp.Radius * wp.Radius;

        if (needToGetCloser)
        {
            bool mounted = Service.Condition[ConditionFlag.Mounted];
            if (wp.Movement != GatherRouteDB.Movement.Normal && !mounted)
            {
                ExecuteMount();
                return;
            }

            _movement.DesiredPosition = wp.Position;
            _camera.SpeedH = _camera.SpeedV = 360.Degrees();
            _camera.DesiredAzimuth = Angle.FromDirection(toWaypoint.X, toWaypoint.Z) + 180.Degrees();

            var sprintStatus = player.StatusList.FirstOrDefault(s => s.StatusId == 50);
            var sprintRemaining = sprintStatus?.RemainingTime ?? 0;
            if (sprintRemaining < 5 && !mounted && MJIManager.Instance()->IsPlayerInSanctuary == 1)
            {
                ExecuteIslandSprint();
            }

            bool flying = Service.Condition[ConditionFlag.InFlight] || Service.Condition[ConditionFlag.Diving];
            if (wp.Movement == GatherRouteDB.Movement.MountFly && mounted && !flying && !Service.Condition[ConditionFlag.Jumping])
            {
                // TODO: improve, jump is not the best really...
                ExecuteJump();
            }

            return;
        }

        var interactObj = !gathering ? FindObjectToInteractWith(wp) : null;
        if (interactObj != null && !isCurrentlyGathering)
        {
            _interact.Exec(() =>
            {
                Service.Log.Debug("Interacting...");
                TargetSystem.Instance()->InteractWithObject(interactObj);
            });
            return;
        }

        if (!ContinueToNext)
        {
            Finish();
            return;
        }

        Errors.Clear(); //Resets errors between points in case gathering is still valid but just unable to gather all items from a node (e.g maxed out on stone, but not quartz)

        if (++CurrentWaypoint >= CurrentRoute!.Waypoints.Count)
        {
            if (LoopAtEnd)
            {
                CurrentWaypoint = 0;
            }
            else
            {
                Finish();
            }
        }
    }

    public void Start(GatherRouteDB.Route route, int waypoint, bool continueToNext, bool loopAtEnd)
    {
        CurrentRoute = route;
        CurrentWaypoint = waypoint;
        ContinueToNext = continueToNext;
        LoopAtEnd = loopAtEnd;
        _camera.Enabled = true;
        _movement.Enabled = true;
    }

    public void Finish()
    {
        if (CurrentRoute == null)
            return;
        CurrentRoute = null;
        CurrentWaypoint = 0;
        ContinueToNext = false;
        LoopAtEnd = false;
        _camera.Enabled = false;
        _movement.Enabled = false;
    }

    private unsafe GameObject* FindObjectToInteractWith(GatherRouteDB.Waypoint wp)
    {
        if (wp.InteractWithOID == 0)
            return null;

        foreach (var obj in Service.ObjectTable.Where(o => o.DataId == wp.InteractWithOID && (o.Position - wp.Position).LengthSquared() < 1))
            return obj.IsTargetable ? (GameObject*)obj.Address : null;

        return null;
    }

    private unsafe void ExecuteActionSafe(ActionType type, uint id) => _action.Exec(() => ActionManager.Instance()->UseAction(type, id));
    private void ExecuteIslandSprint() => ExecuteActionSafe(ActionType.Action, 31314);
    private void ExecuteMount() => ExecuteActionSafe(ActionType.GeneralAction, 24); // flying mount roulette
    private void ExecuteJump() => ExecuteActionSafe(ActionType.GeneralAction, 2);

    private void CheckToDisable(ref SeString message, ref bool isHandled)
    {
        if (Service.Config.Get<GatherRouteDB>().DisableOnErrors)
        {
            Errors.PushBack(Environment.TickCount64);
            if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 30 * 1000)) //5 errors within 30 seconds stops the route, can adjust this as necessary
            {
                Finish();
            }
        }
    }
}
