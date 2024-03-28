using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using ECommons;
using ECommons.Automation;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using System.Linq;
using System.Numerics;
using visland.Helpers;
using visland.IPC;
using visland.Questing;

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
        Svc.Toasts.ErrorToast -= CheckToDisable;
    }

    public unsafe void Update()
    {
        _camera.SpeedH = _camera.SpeedV = default;
        _movement.DesiredPosition = Player.Object?.Position ?? new();

        if (Paused && NavmeshIPC.PathIsRunning())
            NavmeshIPC.PathStop();

        if (!Player.Available || Player.Object!.IsCasting || GenericHelpers.IsOccupied() || Player.Mounting || Paused || CurrentRoute == null || Plugin.P.TaskManager.IsBusy || CurrentWaypoint >= CurrentRoute.Waypoints.Count)
            return;

        CompatModule.EnsureCompatibility(RouteDB);

        var wp = CurrentRoute.Waypoints[CurrentWaypoint];
        var toWaypoint = wp.Position - Player.Object.Position;
        bool needToGetCloser = toWaypoint.LengthSquared() > wp.Radius * wp.Radius;
        Pathfind = wp.Pathfind;

        //if (wp.ZoneID != default && Player.Territory != wp.ZoneID)
        //{
        //    Plugin.P.TaskManager.Enqueue(() => Telepo.Instance()->Teleport(Coordinates.GetNearestAetheryte(wp.ZoneID, wp.Position), 0));
        //    return;
        //}

        if (needToGetCloser)
        {
            if (NavmeshIPC.PathIsRunning()) return;
            if (wp.Movement != GatherRouteDB.Movement.Normal && !Player.Mounted)
            {
                ExecuteMount();
                return;
            }

            if (Player.OnIsland && !Player.Mounted && Player.SprintCD > 5)
                ExecuteIslandSprint();
            
            if (!Player.OnIsland && !Player.Mounted && Player.SprintCD == 0)
                ExecuteSprint();

            if (wp.Movement == GatherRouteDB.Movement.MountFly && Player.Mounted && !Player.InclusiveFlying && !Service.Condition[ConditionFlag.Jumping])
            {
                // TODO: improve, jump is not the best really...
                ExecuteJump();
            }

            if (Pathfind && Utils.HasPlugin(NavmeshIPC.Name))
            {
                if (!NavmeshIPC.NavIsReady()) return;
                NavmeshIPC.PathfindAndMoveTo(wp.Position, wp.Movement == GatherRouteDB.Movement.MountFly || Player.InclusiveFlying);
            }
            else
            {
                _movement.DesiredPosition = wp.Position;
                _camera.SpeedH = _camera.SpeedV = 360.Degrees();
                _camera.DesiredAzimuth = Angle.FromDirection(toWaypoint.X, toWaypoint.Z) + 180.Degrees();
            }

            return;
        }

        // force stop at destination to avoid a bug wherein you interact with the object and keep moving for a period of time
        if (Pathfind && NavmeshIPC.PathIsRunning())
            NavmeshIPC.PathStop();

        if (!Player.Normal && wp.Movement == GatherRouteDB.Movement.Normal)
        {
            ExecuteDismount();
            return;
        }

        if (Player.ExclusiveFlying && wp.Movement == GatherRouteDB.Movement.MountNoFly)
        {
            _movement.DesiredPosition = new Vector3(Player.Object.Position.X, wp.Position.Y, Player.Object.Position.Z);
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
            //case GatherRouteDB.InteractionType.PickupQuest:
            //    QuestsHelper.PickUpQuest(wp.QuestID, wp.InteractWithOID);
            //    break;
            //case GatherRouteDB.InteractionType.TurninQuest:
            //    QuestsHelper.TurnInQuest(wp.QuestID, wp.InteractWithOID);
            //    break;
            case GatherRouteDB.InteractionType.Grind:
                if (Utils.HasPlugin(BossModIPC.Name))
                    switch (wp.StopCondition)
                    {
                        case GatherRouteDB.GrindStopConditions.Kills:
                            QuestsHelper.Grind(QuestsHelper.GetMobName((uint)wp.MobID), () => ExecKillCounter.Tally.FindIndex(i => i.Name == QuestsHelper.GetMobName((uint)wp.MobID)) >= wp.KillCount);
                            break;
                        case GatherRouteDB.GrindStopConditions.QuestSequence:
                            QuestsHelper.Grind(QuestsHelper.GetMobName((uint)wp.MobID), () => QuestsHelper.GetQuestStep(wp.QuestID) == wp.QuestSeq);
                            break;
                        case GatherRouteDB.GrindStopConditions.QuestComplete:
                            QuestsHelper.Grind(QuestsHelper.GetMobName((uint)wp.MobID), () => QuestsHelper.IsQuestCompleted(wp.QuestID));
                            break;
                    }
                break;
            case GatherRouteDB.InteractionType.EquipRecommendedGear:
                QuestsHelper.EquipRecommendedGear();
                break;
            case GatherRouteDB.InteractionType.StartRoute:
                var route = RouteDB.Routes.Find(r => r.Name == wp.RouteName);
                if (route != null)
                {
                    Finish();
                    Start(route, 0, true, false, route.Waypoints[0].Pathfind);
                    return;
                }
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

    public void Start(GatherRouteDB.Route route, int waypoint, bool continueToNext, bool loopAtEnd, bool pathfind = false)
    {
        CurrentRoute = route;
        CurrentWaypoint = waypoint;
        ContinueToNext = continueToNext;
        Loop = loopAtEnd;
        route.Waypoints[waypoint].Pathfind = pathfind;
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
        CompatModule.RestoreChanges();
        if (Pathfind && NavmeshIPC.PathIsRunning())
            NavmeshIPC.PathStop();
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
    private void ExecuteDismount() => ExecuteActionSafe(ActionType.GeneralAction, 23);
    private void ExecuteJump() => ExecuteActionSafe(ActionType.GeneralAction, 2);
    private void ExecuteSprint() => ExecuteActionSafe(ActionType.GeneralAction, 4);

    private static void ToggleExecutors(bool state, GatherRouteDB.Waypoint? wp = default)
    {
        if (state)
        {
            ExecKillHowTos.Init();
            ExecSkipTalk.Init();
            ExecSelectYes.Init();
            ExecQuestJournalEvent.Init();
            AutoCutsceneSkipper.Enable();
            if (wp!.Interaction == GatherRouteDB.InteractionType.Grind)
                ExecKillCounter.Init([QuestsHelper.GetMobName((uint)wp!.MobID)]);
        }
        else
        {
            ExecKillHowTos.Shutdown();
            ExecSkipTalk.Shutdown();
            ExecSelectYes.Shutdown();
            ExecQuestJournalEvent.Shutdown();
            AutoCutsceneSkipper.Disable();
            ExecKillCounter.Dispose();
        }
    }

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
