using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using ExdSheets.Sheets;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using visland.Helpers;
using visland.IPC;
using static visland.Plugin;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

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
    public AddonMaster.Gathering? GatheringAM;
    public AddonMaster.Gathering.GatheredItem? GatheredItem;
    public AddonMaster.GatheringMasterpiece? GatheringCollectableAM;

    private OverrideCamera _camera = new();
    private OverrideMovement _movement = new();
    private QuestsHelper _qh = new();

    private Throttle _interact = new();
    private CircularBuffer<long> Errors = new(5);

    public GatherRouteExec()
    {
        RouteDB = Service.Config.Get<GatherRouteDB>();
        //Svc.Chat.CheckMessageHandled += CheckToDisable;
        //Svc.Toasts.ErrorToast += CheckToDisable;
    }

    public void Dispose()
    {
        //Svc.Chat.CheckMessageHandled -= CheckToDisable;
        //Svc.Toasts.ErrorToast -= CheckToDisable;
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
        if (CurrentRoute == null) return;

        CurrentRoute.Waypoints.RemoveAll(x => x.IsPhantom);
        CurrentRoute = null;
        CurrentWaypoint = 0;
        ContinueToNext = false;
        Waiting = false;
        Paused = false;
        _camera.Enabled = false;
        _movement.Enabled = false;
        CompatModule.RestoreChanges();
        if (Pathfind && NavmeshIPC.IsRunning())
            NavmeshIPC.Stop();
    }

    public unsafe void Update()
    {
        _camera.SpeedH = _camera.SpeedV = default;
        _movement.DesiredPosition = Player.Object?.Position ?? new();

        if (Paused && NavmeshIPC.IsRunning())
            NavmeshIPC.Stop();

        if (RouteDB.AutoRetainerIntegration && Service.Retainers.Finished && Service.Retainers.GetPreferredCharacter() == Player.CID)
        {
            Service.Retainers.IPC.SetMultiEnabled(false);
            Paused = false;
        }

        if (!Player.Available || Player.Object!.IsCasting || Player.Mounting || Player.Dismounting || Paused || CurrentRoute == null || P.TaskManager.IsBusy || CurrentWaypoint >= CurrentRoute.Waypoints.Count)
            return;

        CompatModule.EnsureCompatibility(RouteDB);

        if (RouteDB.AutoGather && GatheringAM != null && GatheredItem != null && !Player.InGatheringAnimation)
        {
            GatheringActions.UseNextBestAction(GatheringAM, GatheredItem);
            return;
        }

        if (RouteDB.AutoGather && GatheringCollectableAM != null && !Player.InGatheringAnimation)
        {
            GatheringActions.UseNextBestAction(GatheringCollectableAM);
            return;
        }

        if (GenericHelpers.IsOccupied()) return; // must check after auto gathering

        var wp = CurrentRoute.Waypoints[CurrentWaypoint];
        var toWaypoint = wp.Position - Player.Object.Position;
        var needToGetCloser = toWaypoint.LengthSquared() > wp.Radius * wp.Radius;
        Pathfind = wp.Pathfind;

        // check if phantom waypoints have invalid interact data (i.e. from being added outside the object table distance)
        if (wp.IsPhantom && wp.InteractWithOID == 0)
        {
            var obj = Svc.Objects.FirstOrDefault(o => o?.ObjectKind == ObjectKind.GatheringPoint && o.IsTargetable && o?.Position.X - CurrentRoute.Waypoints[CurrentWaypoint].InteractWithPosition.X < 5 && o?.Position.Z - CurrentRoute.Waypoints[CurrentWaypoint].InteractWithPosition.Z < 5, null);
            if (obj != null)
            {
                wp.InteractWithOID = obj.DataId;
                wp.InteractWithName = obj.Name.TextValue;
                wp.InteractWithPosition = obj.Position;
            }
        }

        var food = CurrentRoute.Food != 0 ? CurrentRoute.Food : RouteDB.GlobalFood != 0 ? RouteDB.GlobalFood : 0;
        if (food != 0 && Player.HasFood((uint)food) && !Player.HasFoodBuff && Player.AnimationLock == 0)
        {
            Svc.Log.Debug($"Eating {Utils.GetRow<Item>((uint)food)?.Name}");
            Player.EatFood(food);
            return;
        }

        if (RouteDB.AutoRetainerIntegration && (Service.Retainers.HasSubsReady || Service.Retainers.HasRetainersReady) && Service.Retainers.GetPreferredCharacter() == Player.CID)
        {
            Paused = true;
            Service.Retainers.StartingCharacter = Player.CID;
            Service.Retainers.IPC.SetMultiEnabled(true);
            return;
        }

        if (RouteDB.TeleportBetweenZones && wp.ZoneID != default && Coordinates.HasAetheryteInZone((uint)wp.ZoneID) && Player.Territory != wp.ZoneID)
        {
            Svc.Log.Info($"Teleporting from [{Player.Territory}] to [{wp.ZoneID}] {Coordinates.GetNearestAetheryte(wp.ZoneID, wp.Position)}");
            P.TaskManager.Enqueue(() => Telepo.Instance()->Teleport(Coordinates.GetNearestAetheryte(wp.ZoneID, wp.Position), 0));
            P.TaskManager.Enqueue(() => Player.Object.IsCasting);
            P.TaskManager.Enqueue(() => Player.Territory == wp.ZoneID);
            return;
        }

        if (wp.InteractWithOID != default && !Player.OnIsland && wp.IsNode && Player.Job != wp.NodeJob)
        {
            // must be done before movement or nodes will be skipped
            P.TaskManager.Enqueue(() => Player.SwitchJob(wp.NodeJob));
            return;
        }

        if (needToGetCloser)
        {
            // skip current waypoint if target isn't there
            if (wp.IsNode && Vector3.Distance(Player.Object.Position, wp.Position) < 50 && !Svc.Objects.Any(x => x.DataId == wp.InteractWithOID && x.IsTargetable))
            {
                Svc.Log.Debug("Current waypoint target is not targetable, moving to next waypoint");
                if (NavmeshIPC.IsRunning())
                    NavmeshIPC.Stop();
                goto next;
            }

            if (NavmeshIPC.IsRunning()) return;
            if (wp.Movement != GatherRouteDB.Movement.Normal && !Player.Mounted)
            {
                Player.Mount();
                return;
            }

            Player.Sprint();

            if (wp.Movement == GatherRouteDB.Movement.MountFly && Player.Mounted && !Player.InclusiveFlying)
            {
                // TODO: improve, jump is not the best really...
                Player.Jump();
                return;
            }

            if (Pathfind && NavmeshIPC.IsEnabled)
            {
                if (!NavmeshIPC.IsReady() || NavmeshIPC.PathfindInProgress()) return;
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
            Player.Dismount();
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
                if (interactObj != null)
                {
                    if (RouteDB.AutoGather && Player.Gp < 700) return;
                    _interact.Exec(() =>
                    {
                        Service.Log.Debug("Interacting...");
                        TargetSystem.Instance()->OpenObjectInteraction(interactObj);
                    });
                    return;
                }
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
                var objs = Svc.Objects.Where(o => o?.ObjectKind == ObjectKind.GatheringPoint && o.IsTargetable).OrderBy(x => x.DataId);
                if (objs.Any())
                {
                    Svc.Log.Debug($"Found {objs.Count()} GatheringPoints");
                    TryAddObjects(wp, objs);
                }
                else
                {
                    var markers = GetGatheringMarkers();
                    if (markers.Count == 0) { Player.RevealNode(); return; }
                    TryAddMarkers(wp, markers);
                }
                Svc.Log.Debug($"{!wp.IsLast(CurrentRoute)}");
                if (!wp.IsLast(CurrentRoute))
                {
                    ++CurrentWaypoint;
                    return;
                }
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

        //if (Player.BestCordial.Id != 0 && Player.BestCordial.GP + Player.Gp <= Player.MaxGp)
        //{
        //    Player.DrinkCordial();
        //    return;
        //}

        next:
        if (!ContinueToNext)
        {
            Finish();
            return;
        }

        Errors.Clear(); // Resets errors between points in case gathering is still valid but just unable to gather all items from a node (e.g maxed out on stone, but not quartz)

        if (wp.WaitTimeET != default && wp.WaitTimeET != (Utils.EorzeanHour(), Utils.EorzeanMinute()).ToVec2()) return;

        if (!Waiting && wp.WaitTimeMs != default)
        {
            WaitUntil = Environment.TickCount64 + wp.WaitTimeMs;
            Waiting = true;
        }

        if (Waiting && Environment.TickCount64 <= WaitUntil) return;

        if (wp.WaitForCondition != default && !Svc.Condition[wp.WaitForCondition]) return;

        Waiting = false;

        if (wp.IsPhantom && wp.IsLast(CurrentRoute)) // phantom nodes should have two interactions: standard and nodescan. Ideally find a better way than just duplicating the function here
        {
            var objs = Svc.Objects.Where(o => o?.ObjectKind == ObjectKind.GatheringPoint && o.IsTargetable).OrderBy(x => x.DataId);
            if (objs.Any())
            {
                Svc.Log.Debug($"Found {objs.Count()} GatheringPoints");
                TryAddObjects(wp, objs);
            }
            else
            {
                var markers = GetGatheringMarkers();
                if (markers.Count == 0) { Player.RevealNode(); return; }
                TryAddMarkers(wp, markers);
            }
            if (!wp.IsLast(CurrentRoute))
            {
                // we don't want phantom waypoints to continue as normal or else it would reset the route
                ++CurrentWaypoint;
                return;
            }
        }

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
        return false;
    }

    #region Interactions
    private unsafe GameObject* FindObjectToInteractWith(GatherRouteDB.Waypoint wp)
    {
        if (wp.InteractWithOID == 0)
            return null;

        foreach (var obj in Service.ObjectTable.Where(o => o.DataId == wp.InteractWithOID && (o.Position - wp.Position).LengthSquared() < 1))
            return obj.IsTargetable ? (GameObject*)obj.Address : null;
        return null;
    }

    private void TryAddObjects(GatherRouteDB.Waypoint wp, IEnumerable<IGameObject> nodes)
    {
        List<GatherRouteDB.Waypoint>? waypoints = [];
        waypoints = nodes.Select(obj => new GatherRouteDB.Waypoint
        {
            IsPhantom = true,
            Position = Svc.Condition[ConditionFlag.Diving] ? obj.Position : NavmeshIPC.QueryMeshPointOnFloor(obj.Position, false, 5) ?? obj.Position,
            ZoneID = Svc.ClientState.TerritoryType,
            Radius = RouteDB.DefaultWaypointRadius,
            InteractWithName = obj.Name.TextValue,
            InteractWithOID = obj.DataId,
            InteractWithPosition = obj.Position,
            Interaction = GatherRouteDB.InteractionType.Standard,
            Movement = Player.InclusiveFlying ? GatherRouteDB.Movement.MountFly : GatherRouteDB.Movement.Normal
        }).ToList();

        if (waypoints.Count > 0)
            wp.AddWaypointsAfter(CurrentRoute!, waypoints);
    }

    private void TryAddMarkers(GatherRouteDB.Waypoint wp, List<(MiniMapGatheringMarker Marker, Vector3 Position, float DistanceToLast, IGameObject? Node)> markers)
    {
        List<GatherRouteDB.Waypoint>? waypoints = [];
        waypoints = markers.Select(marker => new GatherRouteDB.Waypoint
        {
            IsPhantom = true,
            Position = Svc.Condition[ConditionFlag.Diving] ? marker.Position : NavmeshIPC.QueryMeshPointOnFloor(marker.Position, false, 5) ?? marker.Position,
            ZoneID = Svc.ClientState.TerritoryType,
            Radius = RouteDB.DefaultWaypointRadius,
            InteractWithName = marker.Node?.Name.TextValue ?? "",
            InteractWithOID = marker.Node?.DataId ?? 0,
            InteractWithPosition = marker.Node?.Position ?? marker.Position,
            Interaction = GatherRouteDB.InteractionType.Standard,
            Movement = Svc.Condition[ConditionFlag.Diving] || marker.DistanceToLast > 30 ? GatherRouteDB.Movement.MountFly : GatherRouteDB.Movement.Normal
        }).OrderBy(x => Vector3.Distance(Player.Object.Position, x.Position)).ToList();

        if (waypoints.Count > 0)
            wp.AddWaypointsAfter(CurrentRoute!, waypoints);
    }

    private unsafe List<(MiniMapGatheringMarker Marker, Vector3 Position, float DistanceToLast, IGameObject? Node)> GetGatheringMarkers()
        => AgentMap.Instance()->MiniMapGatheringMarkers.ToArray()
            .Where(x => x.MapMarker.IconId != 0)
            .Select((marker, index) =>
            {
                var pos = new Vector3(marker.MapMarker.X / 16, Svc.ClientState.LocalPlayer!.Position.Y, marker.MapMarker.Y / 16);
                var dist = index > 0 ? Vector3.Distance(Svc.Objects.ElementAt(index - 1).Position, pos) : 0;
                var obj = Svc.Objects.FirstOrDefault(o => o?.ObjectKind == ObjectKind.GatheringPoint && o.IsTargetable && o?.Position.X - CurrentRoute!.Waypoints[CurrentWaypoint].InteractWithPosition.X < 5 && o?.Position.Z - CurrentRoute.Waypoints[CurrentWaypoint].InteractWithPosition.Z < 5, null);
                Svc.Log.Debug($"Found {nameof(MiniMapGatheringMarker)} @ {pos} {(obj != null ? $"and matching object [{obj.DataId}] {obj.Name.TextValue} @ {obj.Position}" : string.Empty)}");
                return (Marker: marker, Position: obj != null ? obj.Position : pos, DistanceToLast: dist, Node: obj);
            }).ToList();
    #endregion

    #region Error Checking
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
        if (logErrors.Any(x => msg == Utils.GetRow<LogMessage>(x)!.Value.Text.ExtractText()))
            Errors.PushBack(Environment.TickCount64);
        if (Errors.Count() >= 5 && Errors.All(x => x > Environment.TickCount64 - 30 * 1000)) // 5 errors within 30 seconds stops the route, can adjust this as necessary
        {
            Svc.Log.Debug("Chat error threshold reached. Stopping route.");
            Finish();
        }
    }
    #endregion
}
