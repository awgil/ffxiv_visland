using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using ECommons;
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
using visland.IPC;

namespace visland.Gathering;

public class GatherRouteExec : IDisposable
{
    public GatherRouteDB RouteDB;
    public GatherRouteDB.Route? CurrentRoute;
    public int CurrentWaypoint;
    public bool ContinueToNext;
    public bool Paused;
    public bool Loop;
    public bool Waiting;
    public long WaitUntil;
    public bool Pathfind;

    private OverrideCamera _camera = new();
    private OverrideMovement _movement = new();
    private QuestsHelper _qh = new();

    private Throttle _interact = new();
    private Throttle _action = new();
    private CircularBuffer<long> Errors = new(5);

    public GatherRouteExec()
    {
        RouteDB = Service.Config.Get<GatherRouteDB>();
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
        
        bool aboutToBeMounted = Service.Condition[ConditionFlag.Unknown57]; // condition 57 is set while mount up animation is playing
        if (player == null || player.IsCasting || GenericHelpers.IsOccupied() || aboutToBeMounted || Paused || CurrentRoute == null || Plugin.P.TaskManager.IsBusy || CurrentWaypoint >= CurrentRoute.Waypoints.Count)
            return;

        CompatModule.EnsureCompatibility(RouteDB);

        var wp = CurrentRoute.Waypoints[CurrentWaypoint];
        var toWaypoint = wp.Position - player.Position;
        var toWaypointXZ = new Vector3(toWaypoint.X, 0, toWaypoint.Z);
        bool needToGetCloser = toWaypoint.LengthSquared() > wp.Radius * wp.Radius;

        //if (wp.ZoneID != default && Player.Territory != wp.ZoneID)
        //{
        //    Plugin.P.TaskManager.Enqueue(() => Telepo.Instance()->Teleport(Coordinates.GetNearestAetheryte(wp.ZoneID, wp.Position), 0));
        //    return;
        //}

        if (needToGetCloser)
        {
            if (NavmeshIPC.PathIsRunning()) return;
            bool mounted = Service.Condition[ConditionFlag.Mounted];
            if (wp.Movement != GatherRouteDB.Movement.Normal && !mounted)
            {
                ExecuteMount();
                return;
            }

            var sprint = player.StatusList.FirstOrDefault(s => s.StatusId == 50);
            var sprintRemaining = sprint?.RemainingTime ?? 0;
            if (sprintRemaining < 5 && !mounted)
            {
                if (MJIManager.Instance()->IsPlayerInSanctuary == 1)
                    ExecuteIslandSprint();
                else
                    if (ActionManager.Instance()->GetRecastTime(ActionType.GeneralAction, 4) == 0)
                        ExecuteSprint();
            }

            bool flying = Service.Condition[ConditionFlag.InFlight] || Service.Condition[ConditionFlag.Diving];
            if (wp.Movement == GatherRouteDB.Movement.MountFly && mounted && !flying && !Service.Condition[ConditionFlag.Jumping])
            {
                // TODO: improve, jump is not the best really...
                ExecuteJump();
            }

            if (Pathfind && Utils.HasPlugin(NavmeshIPC.Name))
            {
                if (!NavmeshIPC.NavIsReady()) return;
                if (wp.Movement == GatherRouteDB.Movement.MountFly || flying)
                    NavmeshIPC.PathFlyTo(wp.Position);
                else
                    NavmeshIPC.PathMoveTo(wp.Position);
            }
            else
            {
                _movement.DesiredPosition = wp.Position;
                _camera.SpeedH = _camera.SpeedV = 360.Degrees();
                _camera.DesiredAzimuth = Angle.FromDirection(toWaypoint.X, toWaypoint.Z) + 180.Degrees();
            }

            return;
        }

        switch (wp.Interaction)
        {
            case GatherRouteDB.InteractionType.Standard:
                var interactObj = !GenericHelpers.IsOccupied() ? FindObjectToInteractWith(wp) : null;
                if (interactObj != null) { _interact.Exec(() => { Service.Log.Debug("Interacting..."); TargetSystem.Instance()->OpenObjectInteraction(interactObj); }); return; }
                break;
            case GatherRouteDB.InteractionType.Emote:
                QuestsHelper.EmoteAt((uint)wp.EmoteID, wp.InteractWithOID);
                break;
            case GatherRouteDB.InteractionType.UseItem:
                QuestsHelper.UseItemOn((uint)wp.ItemID, wp.InteractWithOID);
                break;
            case GatherRouteDB.InteractionType.UseAction:
                QuestsHelper.UseAction((uint)wp.ActionID, wp.InteractWithOID);
                break;
            case GatherRouteDB.InteractionType.QuestTalk:
                QuestsHelper.TalkTo(wp.InteractWithOID);
                break;
            case GatherRouteDB.InteractionType.Grind:
                if (Utils.HasPlugin(BossModIPC.Name))
                    QuestsHelper.Grind(QuestsHelper.GetMobName((uint)wp.MobID));
                break;
        }

        if (Plugin.P.TaskManager.IsBusy) return; // let any interactions play out first

        if (!ContinueToNext)
        {
            Finish();
            return;
        }

        Errors.Clear(); //Resets errors between points in case gathering is still valid but just unable to gather all items from a node (e.g maxed out on stone, but not quartz)

        if (!Waiting && wp.WaitTimeMs != default)
        {
            WaitUntil = Environment.TickCount64 + wp.WaitTimeMs;
            Waiting = true;
        }

        if (Waiting && Environment.TickCount64 <= WaitUntil) return;

        if (wp.WaitForCondition != default && !Svc.Condition[wp.WaitForCondition]) return;

        Waiting = false;

        if (++CurrentWaypoint >= CurrentRoute!.Waypoints.Count)
        {
            if (Loop)
                CurrentWaypoint = 0;
            else
                Finish();
        }
    }

    public void Start(GatherRouteDB.Route route, int waypoint, bool continueToNext, bool loopAtEnd, bool pathfind)
    {
        CurrentRoute = route;
        CurrentWaypoint = waypoint;
        ContinueToNext = continueToNext;
        Loop = loopAtEnd;
        Pathfind = pathfind;
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
        Waiting = false;
        _camera.Enabled = false;
        _movement.Enabled = false;
        NavmeshIPC.PathStop();
        CompatModule.RestoreChanges();
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
    private void ExecuteSprint() => ExecuteActionSafe(ActionType.GeneralAction, 4);

    private void CheckToDisable(ref SeString message, ref bool isHandled)
    {
        if (Service.Config.Get<GatherRouteDB>().DisableOnErrors)
        {
            Errors.PushBack(Environment.TickCount64);
            if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 30 * 1000)) // 5 errors within 30 seconds stops the route, can adjust this as necessary
            {
                Finish();
            }
        }
    }
}
