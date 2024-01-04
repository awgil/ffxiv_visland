using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using ECommons;
using ECommons.Automation;
using ECommons.CircularBuffers;
using ECommons.Configuration;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SharpDX;
using System;
using System.Diagnostics;
using System.Linq;
using visland.Helpers;

namespace visland.Gathering;

public class GatherRouteExec : IDisposable
{
    public GatherRouteDB.Route? CurrentRoute;
    public int CurrentWaypoint;
    public bool ContinueToNext;
    public bool Paused;

    private OverrideCamera _camera = new();
    private OverrideMovement _movement = new();
    private OverrideAFK _afk = new();
    private QuestsHelper _qh = new();

    private Throttle _interact = new();
    private Throttle _action = new();
    private CircularBuffer<long> Errors = new(5);

    private long ThrottleTime { get; set; } = Environment.TickCount64;
    private Stopwatch waypointTimer = new();
    private bool Waiting = false;

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
        
        var gathering = Service.Condition[ConditionFlag.OccupiedInQuestEvent] || Service.Condition[ConditionFlag.OccupiedInEvent] || Service.Condition[ConditionFlag.OccupiedSummoningBell] || Service.Condition[ConditionFlag.Gathering];
        bool aboutToBeMounted = Service.Condition[ConditionFlag.Unknown57]; // condition 57 is set while mount up animation is playing
        if (player == null || player.IsCasting || gathering || aboutToBeMounted || Paused || CurrentRoute == null || CurrentWaypoint >= CurrentRoute.Waypoints.Count)
            return;

        if (Svc.GameConfig.UiControl.GetUInt("FlyingControlType") == 1)
        {
            Service.Config.Get<GatherRouteDB>().WasFlyingInManual = true;
            Svc.GameConfig.Set(Dalamud.Game.Config.UiControlOption.FlyingControlType, 0);
        }

        if (MJIManager.Instance()->IsPlayerInSanctuary == 1 && MJIManager.Instance()->CurrentMode != 1)
        {
            // you can't just change the CurrentMode in MJIManager
            Callback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("MJIHud"), false, 11, 0);
            Callback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("ContextIconMenu"), true, 0, 1, 82042, 0, 0);   
        }

        // the context menu doesn't respect the updateState for some reason
        if (MJIManager.Instance()->IsPlayerInSanctuary == 1 && GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContextIconMenu", out var cim) && cim->IsVisible)
            Callback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("ContextIconMenu"), true, -1);

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

        //var interactObj = !gathering ? FindObjectToInteractWith(wp) : null;
        //if (interactObj != null)
        //{
        //    _interact.Exec(() =>
        //    {
        //        Service.Log.Debug("Interacting...");
        //        TargetSystem.Instance()->InteractWithObject(interactObj);
        //    });
        //    return;
        //}

        switch (wp.Interaction)
        {
            case GatherRouteDB.InteractionType.Standard:
                var interactObj = !gathering ? FindObjectToInteractWith(wp) : null;
                if (interactObj != null) { _interact.Exec(() => { Service.Log.Debug("Interacting..."); TargetSystem.Instance()->InteractWithObject(interactObj); }); return; }
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
        }

        if (!ContinueToNext)
        {
            Finish();
            return;
        }

        Errors.Clear(); //Resets errors between points in case gathering is still valid but just unable to gather all items from a node (e.g maxed out on stone, but not quartz)

        // this is technically a little off because of when the throttle is set, but it's good enough :tm:
        if (Environment.TickCount64 - ThrottleTime >= wp.WaitTimeMs)
        {
            if (wp.WaitForCondition == ConditionFlag.None || Svc.Condition[wp.WaitForCondition])
            {
                if (++CurrentWaypoint >= CurrentRoute!.Waypoints.Count)
                {
                    ThrottleTime = Environment.TickCount64;
                    if (GatherWindow.loop)
                    {
                        CurrentWaypoint = 0;
                    }
                    else
                    {
                        Finish();
                    }
                }
            }
        }
    }

    public void Start(GatherRouteDB.Route route, int waypoint, bool continueToNext, bool loopAtEnd)
    {
        CurrentRoute = route;
        CurrentWaypoint = waypoint;
        ContinueToNext = continueToNext;
        _camera.Enabled = true;
        _movement.Enabled = true;
        ThrottleTime = Environment.TickCount64;
    }

    public void Finish()
    {
        if (CurrentRoute == null)
            return;
        CurrentRoute = null;
        CurrentWaypoint = 0;
        ContinueToNext = false;
        _camera.Enabled = false;
        _movement.Enabled = false;
        if (Service.Config.Get<GatherRouteDB>().WasFlyingInManual)
            Svc.GameConfig.Set(Dalamud.Game.Config.UiControlOption.FlyingControlType, 1);
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
