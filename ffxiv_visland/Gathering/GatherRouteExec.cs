using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using System.Numerics;
using visland.Helpers;
using visland.IPC;
using static visland.Plugin;

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
        Svc.Chat.CheckMessageHandled += CheckToDisable;
        Svc.Toasts.ErrorToast += CheckToDisable;
    }

    public void Dispose()
    {
        Svc.Chat.CheckMessageHandled -= CheckToDisable;
        Svc.Toasts.ErrorToast -= CheckToDisable;
    }

    public unsafe void Update()
    {
        _camera.SpeedH = _camera.SpeedV = default;
        _movement.DesiredPosition = Player.Object?.Position ?? new();

        if (Paused && NavmeshIPC.IsRunning())
            NavmeshIPC.Stop();

        if (!Player.Available || Player.Object!.IsCasting || GenericHelpers.IsOccupied() || Player.Mounting || Paused || CurrentRoute == null || P.TaskManager.IsBusy || CurrentWaypoint >= CurrentRoute.Waypoints.Count)
            return;

        CompatModule.EnsureCompatibility(RouteDB);

        var wp = CurrentRoute.Waypoints[CurrentWaypoint];
        var toWaypoint = wp.Position - Player.Object.Position;
        var needToGetCloser = toWaypoint.LengthSquared() > wp.Radius * wp.Radius;
        Pathfind = wp.Pathfind;

        //if (wp.ZoneID != default && Player.Territory != wp.ZoneID)
        //{
        //    Plugin.P.TaskManager.Enqueue(() => Telepo.Instance()->Teleport(Coordinates.GetNearestAetheryte(wp.ZoneID, wp.Position), 0));
        //    return;
        //}

        if (needToGetCloser)
        {
            if (NavmeshIPC.IsRunning()) return;
            if (wp.Movement != GatherRouteDB.Movement.Normal && !Player.Mounted)
            {
                ExecuteMount();
                return;
            }

            ExecuteSprint();

            if (wp.Movement == GatherRouteDB.Movement.MountFly && Player.Mounted && !Player.InclusiveFlying)
            {
                // TODO: improve, jump is not the best really...
                ExecuteJump();
                return;
            }

            if (Pathfind && NavmeshIPC.IsEnabled)
            {
                if (!NavmeshIPC.IsReady()) return;
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
        if (Pathfind && NavmeshIPC.IsRunning())
            NavmeshIPC.Stop();

        if (!Player.Normal && wp.Movement == GatherRouteDB.Movement.Normal)
        {
            ExecuteDismount();
            return;
        }

        if (Player.ExclusiveFlying && wp.Movement == GatherRouteDB.Movement.MountNoFly)
        {
            _movement.DesiredPosition = new Vector3(Player.Object.Position.X, wp.Position.Y, Player.Object.Position.Z);
            Svc.Log.Verbose($"Waypoint is MountNoFly, currently flying. Setting desired position lower.");
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
            case GatherRouteDB.InteractionType.ChatCommand:
                QuestsHelper.UseCommand(wp.ChatCommand);
                break;
        }

        if (P.TaskManager.IsBusy)
        {
            Svc.Log.Verbose("Waiting for previous interactions to finish.");
            return;
        }

        if (Service.Config.Get<GatherRouteDB>().ExtractMateria && SpiritbondManager.IsSpiritbondReadyAny() && !GenericHelpers.IsOccupied() && !Svc.Condition[ConditionFlag.Mounted])
        {
            Svc.Log.Debug("Extract materia task queued.");
            P.TaskManager.Enqueue(() => SpiritbondManager.ExtractMateriaTask(), "ExtractMateria");
            return;
        }

        if (Service.Config.Get<GatherRouteDB>().RepairGear && RepairManager.CanRepairAny(Service.Config.Get<GatherRouteDB>().RepairPercent) && !GenericHelpers.IsOccupied() && !Svc.Condition[ConditionFlag.Mounted])
        {
            Svc.Log.Debug("Repair gear task queued.");
            P.TaskManager.Enqueue(() => RepairManager.ProcessRepair(), "RepairGear");
            return;
        }

        if (DalamudReflector.IsOnStaging() && Service.Config.Get<GatherRouteDB>().PurifyCollectables && PurificationManager.CanPurifyAny() && !GenericHelpers.IsOccupied() && !Svc.Condition[ConditionFlag.Mounted])
        {
            Svc.Log.Debug("Purify collectables task queued.");
            P.TaskManager.Enqueue(() => PurificationManager.PurifyAllTask(), "PurifyCollectables");
            return;
        }

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
        if (Pathfind && NavmeshIPC.IsRunning())
            NavmeshIPC.Stop();
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
    private void ExecuteMount() => ExecuteActionSafe(ActionType.GeneralAction, 24); // flying mount roulette
    private void ExecuteDismount() => ExecuteActionSafe(ActionType.GeneralAction, 23);
    private void ExecuteJump() => ExecuteActionSafe(ActionType.GeneralAction, 2);
    private void ExecuteSprint()
    {
        if (Player.Mounted) return;

        if (Player.OnIsland && Player.SprintCD > 5)
            ExecuteActionSafe(ActionType.Action, 31314);

        if (!Player.OnIsland && Player.SprintCD == 0)
            ExecuteActionSafe(ActionType.GeneralAction, 4);
    }

    private void CheckToDisable(ref SeString message, ref bool isHandled)
    {
        if (!Service.Config.Get<GatherRouteDB>().DisableOnErrors) return;
        Errors.PushBack(Environment.TickCount64);
        if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 30 * 1000)) // 5 errors within 30 seconds stops the route, can adjust this as necessary
            Finish();
    }

    private static readonly uint[] logErrors = [3570, 3574, 3575, 3584, 3589]; // various unable to spearfish errors
    private void CheckToDisable(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!Service.Config.Get<GatherRouteDB>().DisableOnErrors) return;
        
        var msg = message.ExtractText();
        if (logErrors.Any(x => msg == Utils.GetRow<LogMessage>(x)!.Text.ExtractText()))
            Errors.PushBack(Environment.TickCount64);
        if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 30 * 1000)) // 5 errors within 30 seconds stops the route, can adjust this as necessary
            Finish();
    }
}
