using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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
        // a target of exactly 0 means "do this without moving"
        var needToGetCloser = wp.Position != Vector3.Zero && toWaypoint.LengthSquared() > wp.Radius * wp.Radius;
        //needToGetCloser = 
        Pathfind = wp.Pathfind;

        var food = CurrentRoute.Food != 0 ? CurrentRoute.Food : RouteDB.GlobalFood != 0 ? RouteDB.GlobalFood : 0;
        if (food != 0 && !Player.HasFood && Player.AnimationLock == 0)
        {
            Svc.Log.Debug($"Eating {Utils.GetRow<Item>((uint)food)?.Name.RawString}");
            ExecuteEatFood(food);
            return;
        }

        if (RouteDB.TeleportBetweenZones && wp.ZoneID != default && Coordinates.HasAetheryteInZone((uint)wp.ZoneID) && Player.Territory != wp.ZoneID)
        {
            P.TaskManager.Enqueue(() => Telepo.Instance()->Teleport(Coordinates.GetNearestAetheryte(wp.ZoneID, wp.Position), 0));
            P.TaskManager.Enqueue(() => Player.Object.IsCasting);
            P.TaskManager.Enqueue(() => Player.Territory == wp.ZoneID);
            return;
        }

        if (needToGetCloser)
        {
            //if (wp.InteractWithOID != default && Vector3.Distance(Player.Object.Position, wp.Position) < 100 && !Svc.Objects.Any(x => x.DataId == wp.InteractWithOID && !x.IsTargetable))
            //{
            //    Svc.Log.Debug("Current waypoint target is not targetable, moving to next waypoint");
            //    ++CurrentWaypoint;
            //    if (NavmeshIPC.IsRunning())
            //        NavmeshIPC.Stop();
            //    return;
            //}

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
            case GatherRouteDB.InteractionType.NodeScan:
                if (NavmeshIPC.PathfindInProgress()) break;
                if (Waiting) break;
                var nodes = Svc.Objects.Where(x => x.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.GatheringPoint && x.IsTargetable)
                    .OrderBy(x => Vector3.DistanceSquared(x.Position, Player.Object.Position))
                    .Select((x, i) => new {Value = x, DistanceToLast = i > 0 ? Vector3.Distance(Svc.Objects.ElementAt(i - 1).Position, x.Position) : 0});
                if (wp.InteractWithName != String.Empty)
                    nodes = nodes.Where(x => x.Value.Name.TextValue.EqualsIgnoreCase(wp.InteractWithName)).Take(1);
                var waypoints = nodes.Select(node => new GatherRouteDB.Waypoint
                {
                    IsPhantom = true,
                    Position = node.Value.Position,
                    ZoneID = Service.ClientState.TerritoryType,
                    InteractWithName = node.Value.Name.TextValue.ToLower(),
                    InteractWithOID = node.Value.DataId,
                    Radius = RouteDB.DefaultInteractionRadius,
                    Movement = node.DistanceToLast < 30 ? GatherRouteDB.Movement.Normal
                    : Player.ExclusiveFlying ? GatherRouteDB.Movement.MountFly
                    : wp.Movement == GatherRouteDB.Movement.MountFly ? GatherRouteDB.Movement.MountFly
                    : GatherRouteDB.Movement.MountNoFly,
                    Pathfind = true
                }).ToList();
                CurrentRoute.Waypoints.InsertRange(CurrentRoute.Waypoints.IndexOf(wp) + 1, waypoints);
                break;
            case GatherRouteDB.InteractionType.SurveyNodeScan:
                if (NavmeshIPC.PathfindInProgress()) break;
                if (Waiting) break;
                AgentMap* map = AgentMap.Instance();
                if (map == null || map->CurrentMapId == 0)
                    break;
                if ((int)wp.SurveyNodeType > map->MiniMapGatheringMarkers.Length)
                    break;
                // these are the flashy gathering markers around the compass
                var marker = map->MiniMapGatheringMarkers[(int)wp.SurveyNodeType];
                if (marker.MapMarker.IconId == 0)
                    break;
                var targetPosition = new Vector3(marker.MapMarker.X / 16.0f, 1024, marker.MapMarker.Y / 16.0f);
                if (NavmeshIPC.QueryMeshPointOnFloor(targetPosition, false, 1) is Vector3 foundTarget)
                    targetPosition = foundTarget;
                else
                {
                    Svc.Log.Verbose($"Didn't find navigable position around {targetPosition}, defaulting to player position");
                    targetPosition = Player.Object.Position;
                }
                var waypoint = new GatherRouteDB.Waypoint
                {
                    IsPhantom = true,
                    Position = targetPosition,
                    showInteractions = true,
                    Interaction = GatherRouteDB.InteractionType.NodeScan,
                    InteractWithName = marker.TooltipText.ToString().Split(' ', 3).LastOrDefault(String.Empty),// breaks by space so "Lv. 60 Rocky Outcrop" gets split into {"Lv.", "60", "Rocky Outcrop"}
                    Radius = wp.Radius,
                    Movement = Vector3.Distance(Player.Object.Position, targetPosition) < 30 ? GatherRouteDB.Movement.Normal
                        : Player.ExclusiveFlying ? GatherRouteDB.Movement.MountFly
                        : wp.Movement == GatherRouteDB.Movement.MountFly ? GatherRouteDB.Movement.MountFly
                        : GatherRouteDB.Movement.MountNoFly,
                    Pathfind = true
                };
                CurrentRoute.Waypoints.Insert(CurrentRoute.Waypoints.IndexOf(wp) + 1, waypoint);
                break;
        }

        if (P.TaskManager.IsBusy) return; // let interactions play out

        if (RouteDB.ExtractMateria && SpiritbondManager.IsSpiritbondReadyAny() && !GenericHelpers.IsOccupied() && !Svc.Condition[ConditionFlag.Mounted])
        {
            Svc.Log.Debug("Extract materia task queued.");
            P.TaskManager.Enqueue(() => SpiritbondManager.ExtractMateriaTask(), "ExtractMateria");
            return;
        }

        if (RouteDB.RepairGear && RepairManager.CanRepairAny(RouteDB.RepairPercent) && !GenericHelpers.IsOccupied() && !Svc.Condition[ConditionFlag.Mounted])
        {
            Svc.Log.Debug("Repair gear task queued.");
            P.TaskManager.Enqueue(() => RepairManager.ProcessRepair(), "RepairGear");
            return;
        }

        if (RouteDB.PurifyCollectables && PurificationManager.CanPurifyAny() && !GenericHelpers.IsOccupied() && !Svc.Condition[ConditionFlag.Mounted])
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

        if (wp.WaitTimeET != default && wp.WaitTimeET != (Utils.EorzeanHour(), Utils.EorzeanMinute()).ToVec2()) return;

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
            {
                CurrentRoute.Waypoints.RemoveAll(x => x.IsPhantom);
                CurrentWaypoint = 0;
            }
            else
                Finish();
        }
    }

    private bool NodeExists(Vector3 nodePos)
    {
        if (Vector3.DistanceSquared(Player.Object.Position, nodePos) < 100)
            if (Svc.Objects.FirstOrDefault(x => x?.Position == nodePos, null) != null)
                return true;
        throw new NotImplementedException();
    }

    public void Start(GatherRouteDB.Route route, int waypoint, bool continueToNext, bool loopAtEnd, bool pathfind = false)
    {
        CurrentRoute = route;
        route.Waypoints.RemoveAll(x => x.IsPhantom);
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
        CurrentRoute.Waypoints.RemoveAll(x => x.IsPhantom);
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
    private unsafe void ExecuteEatFood(int id)
    {
        if (InventoryManager.Instance()->GetInventoryItemCount((uint)id) > 0 || InventoryManager.Instance()->GetInventoryItemCount((uint)id, true) > 0)
            _action.Exec(() => AgentInventoryContext.Instance()->UseItem((uint)id));
    }

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
        if (!RouteDB.DisableOnErrors) return;
        Svc.Log.Verbose($"ErrorToast fired with string: {message}");
        Errors.PushBack(Environment.TickCount64);
        if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 30 * 1000)) // 5 errors within 30 seconds stops the route, can adjust this as necessary
        {
            Svc.Log.Debug("Toast error threshold reached. Stopping route.");
            Finish();
        }
    }

    private static readonly uint[] logErrors = [3570, 3574, 3575, 3584, 3589]; // various unable to spearfish errors
    private void CheckToDisable(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if (!RouteDB.DisableOnErrors || type != XivChatType.ErrorMessage) return;

        Svc.Log.Verbose($"ErrorMessage fired with string: {message}");
        var msg = message.ExtractText();
        if (logErrors.Any(x => msg == Utils.GetRow<LogMessage>(x)!.Text.ExtractText()))
            Errors.PushBack(Environment.TickCount64);
        if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 30 * 1000)) // 5 errors within 30 seconds stops the route, can adjust this as necessary
        {
            Svc.Log.Debug("Chat error threshold reached. Stopping route.");
            Finish();
        }
    }
}
