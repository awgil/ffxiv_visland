using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using visland.Helpers;
using visland.IPC;
using static visland.Gathering.GatherRouteDB;

namespace visland.Gathering;

public class GatherWindow : Window {
    private readonly UITree _tree = new();
    private readonly List<System.Action> _postDraw = [];

    public GatherRouteDB RouteDB = null!;
    public GatherRouteExec Exec => Service.RouteExec;
    public GatherDebug _debug = null!;

    private int selectedRouteIndex = -1;
    private static bool loop;

    private Vector4 greenColor = new Vector4(0x5C, 0xB8, 0x5C, 0xFF) / 0xFF;
    private Vector4 redColor = new Vector4(0xD9, 0x53, 0x4F, 0xFF) / 0xFF;

    private string searchString = string.Empty;
    private readonly List<Route> FilteredRoutes = [];
    private FontAwesomeIcon PlayIcon => Exec.CurrentRoute != null && !Exec.Paused ? FontAwesomeIcon.Pause : FontAwesomeIcon.Play;
    private string PlayTooltip => Exec.CurrentRoute == null ? "Start Route" : Exec.Paused ? "Resume Route" : "Pause Route";

    public GatherWindow() : base("Gathering Automation", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse) {
        Size = new Vector2(800, 800);
        SizeCondition = ImGuiCond.FirstUseEver;
        RouteDB = Service.Config.Get<GatherRouteDB>();

        _debug = new(Exec);
    }

    public override void Draw() {
        using var tabs = ImRaii.TabBar("Tabs");
        if (tabs) {
            using (var tab = ImRaii.TabItem("Routes"))
                if (tab) {
                    DrawExecution();
                    ImGui.Separator();
                    ImGui.Spacing();

                    var cra = ImGui.GetContentRegionAvail();
                    var sidebar = cra with { X = cra.X * 0.40f };
                    var editor = cra with { X = cra.X * 0.60f };

                    DrawSidebar(sidebar);
                    ImGui.SameLine();
                    DrawEditor(editor);

                    foreach (var a in _postDraw)
                        a();
                    _postDraw.Clear();
                }
            using (var tab = ImRaii.TabItem("Log"))
                if (tab)
                    ImGui.TextUnformatted("Plugin log is available via /xllog or Dalamud log window.");
            using (var tab = ImRaii.TabItem("Debug"))
                if (tab)
                    _debug.Draw();
        }
    }

    private void DrawExecution() {
        ImGui.Text("Status: ");
        ImGui.SameLine();

        if (Exec.CurrentRoute != null)
            Utils.FlashText($"{(Exec.Paused ? "PAUSED" : Exec.Waiting ? "WAITING" : "RUNNING")}", new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Exec.Paused ? new Vector4(1.0f, 0.0f, 0.0f, 1.0f) : new Vector4(0.0f, 1.0f, 0.0f, 1.0f), 2);
        ImGui.SameLine();

        if (Exec.CurrentRoute == null || Exec.CurrentWaypoint >= Exec.CurrentRoute.Waypoints.Count) {
            ImGui.Text("Route not running");
            return;
        }

        if (Exec.CurrentRoute != null) // Finish() call could've reset it
        {
            ImGui.SameLine();
            ImGui.Text($"{Exec.CurrentRoute.Name}: Step #{Exec.CurrentWaypoint + 1} {Exec.CurrentRoute.Waypoints[Exec.CurrentWaypoint].Position}");

            if (Exec.Waiting) {
                ImGui.SameLine();
                ImGui.Text($"waiting {Exec.WaitUntil - Environment.TickCount64}ms");
            }
        }

        ImGui.SameLine();
        ImGui.Text($"State: {Exec.CurrentState}");
    }

    private unsafe void DrawSidebar(Vector2 size) {
        using (ImRaii.Child("Sidebar", size, false)) {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
                RouteDB.Routes.Add(new() { Name = "Unnamed Route" });
                RouteDB.NotifyModified();
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Create a New Route");
            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport))
                TryImport(RouteDB);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Import Route from Clipboard");

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
                ImGui.OpenPopup("Advanced Options");
            DrawRouteSettingsPopup();

            ImGui.SameLine();
            RapidImport();

            ImGui.TextV("Search: ");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputText("###RouteSearch", ref searchString, 500)) {
                FilteredRoutes.Clear();
                if (searchString.Length > 0) {
                    foreach (var route in RouteDB.Routes) {
                        if (route.Name.Contains(searchString, StringComparison.CurrentCultureIgnoreCase) || route.Group.Contains(searchString, StringComparison.CurrentCultureIgnoreCase))
                            FilteredRoutes.Add(route);
                    }
                }
            }

            ImGui.Separator();

            using (ImRaii.Child("routes")) {
                var groups = GetGroups(RouteDB, true);
                foreach (var group in groups) {
                    foreach (var _ in _tree.Node($"{group}###{groups.IndexOf(group)}", contextMenu: () => ContextMenuGroup(group))) {
                        var routeSource = FilteredRoutes.Count > 0 ? FilteredRoutes : RouteDB.Routes;
                        for (var i = 0; i < routeSource.Count; i++) {
                            var route = routeSource[i];
                            var routeGroup = string.IsNullOrEmpty(route.Group) ? "None" : route.Group;
                            if (routeGroup == group) {
                                if (ImGui.Selectable($"{route.Name} ({route.Waypoints.Count} steps)###{i}", i == selectedRouteIndex))
                                    selectedRouteIndex = i;
                                //if (ImRaii.ContextPopup($"{route.Name}{i}"))
                                //{
                                //    selectedRouteIndex = i;
                                //    ContextMenuRoute(routeSource[i]);
                                //}
                            }
                        }
                    }
                }
            }
        }
    }

    internal static bool RapidImportEnabled = false;
    private void RapidImport() {
        if (ImGui.Checkbox("Enable Rapid Import", ref RapidImportEnabled))
            ImGui.SetClipboardText("");

        ImGuiComponents.HelpMarker("Import multiple presets with ease by simply copying them. Visland will read your clipboard and attempt to import whatever you copy. Your clipboard will be cleared upon enabling.");
        if (RapidImportEnabled) {
            try {
                var text = ImGui.GetClipboardText();
                if (text != "") {
                    TryImport(RouteDB);
                    ImGui.SetClipboardText("");
                }
            }
            catch (Exception e) {
                Service.Log.Error(e, "Rapid import failed");
            }
        }
    }

    private void DrawRouteSettingsPopup() {
        using var popup = ImRaii.Popup("Advanced Options");
        if (popup.Success) {
            Utils.DrawSection("Global Route Editing Options", ImGuiColors.ParsedGold);
            if (ImGui.SliderFloat("Default Waypoint Radius", ref RouteDB.DefaultWaypointRadius, 0, 100))
                RouteDB.NotifyModified();
            if (ImGui.SliderFloat("Default Interaction Radius", ref RouteDB.DefaultInteractionRadius, 0, 100))
                RouteDB.NotifyModified();

            Utils.DrawSection("Global Route Operation Options", ImGuiColors.ParsedGold);

            if (ImGui.Checkbox("Auto Enable Island Sanctuary Gather Mode", ref RouteDB.GatherModeOnStart))
                RouteDB.NotifyModified();
            ImGuiComponents.HelpMarker("Enables \"Gather Mode\" when on your Island Sanctuary automatically when commencing a route.");

            using (ImRaii.Disabled()) {
                if (ImGui.Checkbox("Stop Route on Error", ref RouteDB.DisableOnErrors))
                    RouteDB.NotifyModified();
            }
            ImGuiComponents.HelpMarker("Stops executing a route when you encounter a node you can't gather from due to full inventory.");

            if (ImGui.Checkbox("Teleport between zones", ref RouteDB.TeleportBetweenZones))
                RouteDB.NotifyModified();

            Utils.WorkInProgressIcon();
            ImGui.SameLine();
            if (ImGui.Checkbox("Auto Gather", ref RouteDB.AutoGather))
                RouteDB.NotifyModified();
            ImGuiComponents.HelpMarker($"Applies to non-island routes only. Will auto gather the item in the \"Item Target\" field and use the best actions available.");

            Utils.DrawSection("Global Route Extras", ImGuiColors.ParsedGold);

            if (ImGui.Checkbox("Extract materia during routes", ref RouteDB.ExtractMateria))
                RouteDB.NotifyModified();
            if (ImGui.Checkbox("Repair gear during routes", ref RouteDB.RepairGear))
                RouteDB.NotifyModified();
            if (ImGui.SliderFloat("Repair percentage threshold", ref RouteDB.RepairPercent, 0, 100))
                RouteDB.NotifyModified();
            if (ImGui.Checkbox("Purify collectables during routes", ref RouteDB.PurifyCollectables))
                RouteDB.NotifyModified();
            ImGuiComponents.HelpMarker($"Also known as {Addon.GetRow(2160)!.Value.Text}");
            if (ImGui.Checkbox("Check AutoRetainer during routes", ref RouteDB.AutoRetainerIntegration))
                RouteDB.NotifyModified();
            ImGuiComponents.HelpMarker($"Will enable multi mode when you have any retainers or submarines returned across any enabled characters. Requires the current character to be set as the Preferred Character and the Teleport to FC config enabled in AutoRetainer.");
            if (UICombo.ExcelSheetCombo("##Foods", out Item i, _ => $"[{RouteDB.GlobalFood}] {Item.GetRow((uint)RouteDB.GlobalFood)?.Name}", x => $"[{x.RowId}] {x.Name}", x => x.ItemUICategory.RowId == 46)) {
                RouteDB.GlobalFood = (int)i.RowId;
                RouteDB.NotifyModified();
            }
            if (RouteDB.GlobalFood != 0) {
                ImGui.SameLine();
                if (ImGui.IconButton(FontAwesomeIcon.Undo, "ClearGlobalFood")) {
                    RouteDB.GlobalFood = 0;
                    RouteDB.NotifyModified();
                }
            }
            ImGuiComponents.HelpMarker("Food set here will apply to all routes unless overwritten in the route itself.");
        }
    }

    private void DrawEditor(Vector2 size) {
        if (selectedRouteIndex == -1) return;

        var routeSource = FilteredRoutes.Count > 0 ? FilteredRoutes : RouteDB.Routes;
        if (routeSource.Count == 0) return;
        var route = selectedRouteIndex >= routeSource.Count ? routeSource.Last() : routeSource[selectedRouteIndex];

        using (ImRaii.Child("Editor", size)) {
            if (ImGuiComponents.IconButton(PlayIcon)) {
                if (Exec.CurrentRoute != null)
                    Exec.Paused = !Exec.Paused;
                if (Exec.CurrentRoute == null && route.Waypoints.Count > 0)
                    Exec.Start(route, 0, true, loop, route.Waypoints[0].Pathfind);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(PlayTooltip);
            ImGui.SameLine();

            ImGui.PushStyleColor(ImGuiCol.Button, loop ? greenColor : redColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, loop ? greenColor : redColor);
            if (ImGuiComponents.IconButton(FontAwesomeIcon.SyncAlt))
                loop ^= true;
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Loop Route");
            ImGui.SameLine();

            if (Exec.CurrentRoute != null) {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Stop))
                    Exec.Finish();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Stop Route");
                ImGui.SameLine();
            }

            var canDelete = !ImGui.GetIO().KeyCtrl;
            using (ImRaii.Disabled(canDelete)) {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) {
                    if (Exec.CurrentRoute == route)
                        Exec.Finish();
                    RouteDB.Routes.Remove(route);
                    RouteDB.NotifyModified();
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Delete Route (Hold CTRL)");
            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileExport)) {
                ImGui.SetClipboardText(JsonConvert.SerializeObject(route));
            }
            if (ImGui.IsItemHovered()) {
                ImGui.SetTooltip("Export Route (\uE052 Base64)");
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    ImGui.SetClipboardText(Utils.ToCompressedBase64(route));
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.EllipsisH))
                ImGui.OpenPopup("##MassEditing");
            DrawMassEditContextMenu(route);

            var name = route.Name;
            var group = route.Group;
            var movementType = Service.Condition[ConditionFlag.InFlight] ? Movement.MountFly : Service.Condition[ConditionFlag.Mounted] ? Movement.MountNoFly : Movement.Normal;
            ImGui.TextV("Name: ");
            ImGui.SameLine();
            if (ImGui.InputText("##name", ref name, 256)) {
                route.Name = name;
                RouteDB.NotifyModified();
            }
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus)) {
                Exec.Finish();
                var player = Service.ObjectTable.LocalPlayer;
                if (player != null) {
                    route.Waypoints.Add(new() { Position = player.Position, Radius = RouteDB.DefaultWaypointRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType });
                    RouteDB.NotifyModified();
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add Waypoint: Current Position");
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.UserPlus)) {
                var target = Service.TargetManager.Target;
                if (target != null) {
                    route.Waypoints.Add(new() { Position = target.Position, Radius = RouteDB.DefaultInteractionRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType, InteractWithOID = target.BaseId, InteractWithName = target.Name.ToString().ToLower() });
                    RouteDB.NotifyModified();
                    Exec.Start(route, route.Waypoints.Count - 1, false, false);
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add Waypoint: Interact with Target");

            ImGui.TextV("Group: ");
            ImGui.SameLine();
            if (ImGui.InputText("##group", ref group, 256)) {
                route.Group = group;
                RouteDB.NotifyModified();
            }

            if (RouteDB.AutoGather) {
                ImGui.TextV("Item Target: ");
                ImGui.SameLine();
                if (UICombo.ExcelSheetCombo("##Gatherables", out GatheringItem gatherable, _ => $"[{route.TargetGatherItem}] {Item.GetRow((uint)route.TargetGatherItem)?.Name.ToString()}", x => $"[{x.RowId}] {Item.GetRow(x.Item.RowId)?.Name.ToString()}", x => x.Item.RowId != 0)) {
                    route.TargetGatherItem = (int)gatherable.Item.RowId;
                    RouteDB.NotifyModified();
                }
                if (route.TargetGatherItem != 0) {
                    ImGui.SameLine();
                    if (ImGui.IconButton(FontAwesomeIcon.Undo, "ClearItemTarget")) {
                        route.TargetGatherItem = 0;
                        RouteDB.NotifyModified();
                    }
                }
            }

            using (ImRaii.Child("waypoints")) {
                for (var i = 0; i < route.Waypoints.Count; ++i) {
                    var wp = route.Waypoints[i];
                    foreach (var wn in _tree.Node($"#{i + 1}: [x: {wp.Position.X:f0}, y: {wp.Position.Y:f0}, z: {wp.Position.Z:f0}] ({wp.Movement}) {(wp.InteractWithOID != 0 ? $" @ {wp.InteractWithName} ({wp.InteractWithOID:X})" : "")}###{i}", color: wp.IsPhantom ? ImGuiColors.HealerGreen.ToHex() : 0xffffffff, contextMenu: () => ContextMenuWaypoint(route, i)))
                        DrawWaypoint(wp);
                }
            }
        }
    }

    private bool pathfind;
    private uint zoneID;
    private float radius;
    private InteractionType interaction;
    private void DrawMassEditContextMenu(Route route) {
        using var popup = ImRaii.Popup("##MassEditing");
        if (!popup) return;

        Utils.DrawSection("Route Settings", ImGuiColors.ParsedGold);
        if (UICombo.ExcelSheetCombo("##Foods", out Item i, _ => $"[{route.Food}] {Item.GetRow((uint)route.Food)?.Name}", x => $"[{x.RowId}] {x.Name}", x => x.ItemUICategory.RowId == 46)) {
            route.Food = (int)i.RowId;
            RouteDB.NotifyModified();
        }
        if (RouteDB.GlobalFood != 0) {
            ImGui.SameLine();
            if (ImGui.IconButton(FontAwesomeIcon.Undo, "ClearLocalFood")) {
                route.Food = 0;
                RouteDB.NotifyModified();
            }
        }
        ImGuiComponents.HelpMarker("Food set here will apply to this route only and overrides the global food setting.");

        Utils.DrawSection("Mass Editing", ImGuiColors.ParsedGold);
        ImGui.Checkbox("Pathfind", ref pathfind);
        ImGui.SameLine();
        if (ImGui.Button("Apply All###Pathfind")) {
            route?.Waypoints.ForEach(x => x.Pathfind = pathfind);
            RouteDB.NotifyModified();
        }

        ImGui.InputUInt("Zone", ref zoneID);
        ImGui.SameLine();
        if (ImGui.Button("Apply All###Zone")) {
            route?.Waypoints.ForEach(x => x.ZoneID = zoneID);
            RouteDB.NotifyModified();
        }

        ImGui.InputFloat("Radius", ref radius);
        ImGui.SameLine();
        if (ImGui.Button("Apply All###Radius")) {
            route?.Waypoints.ForEach(x => x.Radius = radius);
            RouteDB.NotifyModified();
        }

        UICombo.Enum("Interaction type", ref interaction);
        ImGui.SameLine();
        if (ImGui.Button("Apply All###Interaction")) {
            route?.Waypoints.ForEach(x => x.Interaction = interaction);
            RouteDB.NotifyModified();
        }
    }

    private void DrawWaypoint(Waypoint wp) {
        if (ImGui.IconButton(FontAwesomeIcon.MapMarker) && Player.Available) {
            wp.Position = Player.Position;
            wp.ZoneID = Service.ClientState.TerritoryType;
            RouteDB.NotifyModified();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Set Position to Current");
        ImGui.SameLine();
        if (ImGui.InputFloat3("Position", ref wp.Position))
            RouteDB.NotifyModified();

        if (ImGui.InputUInt("Zone ID", ref wp.ZoneID))
            RouteDB.NotifyModified();

        if (ImGui.InputFloat("Radius (yalms)", ref wp.Radius))
            RouteDB.NotifyModified();

        if (UICombo.Enum("Movement mode", ref wp.Movement))
            RouteDB.NotifyModified();

        ImGui.SameLine();
        using (var noNav = ImRaii.Disabled(!Service.Navmesh.IsEnabled)) {
            if (ImGui.Checkbox("Pathfind?", ref wp.Pathfind))
                RouteDB.NotifyModified();
        }
        if (!Service.Navmesh.IsEnabled)
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip($"This features requires {NavmeshIPC.Name} to be installed.");

        if (ImGuiComponents.IconButton(FontAwesomeIcon.UserPlus)) {
            if (wp.InteractWithOID == default) {
                var target = Service.TargetManager.Target;
                if (target != null) {
                    wp.Position = target.Position;
                    wp.Radius = RouteDB.DefaultInteractionRadius;
                    wp.InteractWithName = target.Name.ToString().ToLower();
                    wp.InteractWithOID = target.BaseId;
                    RouteDB.NotifyModified();
                }
            }
            else
                wp.InteractWithOID = default;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add/Remove target from waypoint");
        ImGui.SameLine();
        if (ImGui.IconButton(FontAwesomeIcon.CommentDots)) {
            wp.showInteractions ^= true;
            RouteDB.NotifyModified();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle Interactions");
        ImGui.SameLine();
        if (ImGui.IconButton(FontAwesomeIcon.Clock)) {
            wp.showWaits ^= true;
            RouteDB.NotifyModified();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle Waits");

        if (wp.showInteractions) {
            if (UICombo.Enum("Interaction Type", ref wp.Interaction))
                RouteDB.NotifyModified();
            switch (wp.Interaction) {
                case InteractionType.None: break;
                case InteractionType.Standard: break;
                case InteractionType.StartRoute:
                    if (UICombo.String("Route Name", [.. RouteDB.Routes.Select(r => r.Name)], ref wp.RouteName))
                        RouteDB.NotifyModified();
                    break;
                case InteractionType.NodeScan:
                    ImGui.SameLine();
                    Utils.WorkInProgressIcon();
                    ImGuiComponents.HelpMarker("Node scanning will check the object table for nearby targetable gathering points, failing that will use your gatherer's reveal node ability and navigate to that. It will create a new phantom waypoint with the aforementioned information and navigate to it. Every phantom waypoint will also node scan. These special waypoints do not get saved to the route.");
                    ImGui.TextUnformatted("This feature will have trouble with land nodes at the moment.");
                    break;
            }
        }

        if (wp.showWaits) {
            if (ImGui.InputFloat2("Eorzean Time Wait", ref wp.WaitTimeET))
                RouteDB.NotifyModified();
            if (ImGui.SliderInt("Wait (ms)", ref wp.WaitTimeMs, 0, 60000))
                RouteDB.NotifyModified();
            if (UICombo.Enum("Wait for Condition", ref wp.WaitForCondition))
                RouteDB.NotifyModified();
        }
    }

    private void ContextMenuGroup(string group) {
        var old = group;
        ImGui.TextV("Name: ");
        ImGui.SameLine();
        if (ImGui.InputText("##groupname", ref group, 256)) {
            RouteDB.Routes.Where(r => r.Group == old).ToList().ForEach(r => r.Group = group);
            RouteDB.NotifyModified();
        }
    }

    private void ContextMenuWaypoint(Route r, int i) {
        if (ImGui.MenuItem("Execute this step only"))
            Exec.Start(r, i, false, false, r.Waypoints[i].Pathfind);

        if (ImGui.MenuItem("Execute route once starting from this step"))
            Exec.Start(r, i, true, false, r.Waypoints[i].Pathfind);

        if (ImGui.MenuItem("Execute route starting from this step and then loop"))
            Exec.Start(r, i, true, true, r.Waypoints[i].Pathfind);

        var movementType = Service.Condition[ConditionFlag.InFlight] ? Movement.MountFly : Service.Condition[ConditionFlag.Mounted] ? Movement.MountNoFly : Movement.Normal;
        var target = Service.TargetManager.Target;

        if (ImGui.MenuItem($"Swap to {(r.Waypoints[i].InteractWithOID != default ? "normal waypoint" : "interact waypoint")}")) {
            _postDraw.Add(() => {
                r.Waypoints[i].InteractWithOID = r.Waypoints[i].InteractWithOID != default ? default : target?.BaseId ?? default;
                RouteDB.NotifyModified();
            });
        }

        if (ImGui.MenuItem("Insert step above")) {
            _postDraw.Add(() => {
                if (i > 0 && i < r.Waypoints.Count) {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    if (Service.ObjectTable.LocalPlayer != null) {
                        r.Waypoints.Insert(i, new() { Position = Player.Position, Radius = RouteDB.DefaultWaypointRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType });
                        RouteDB.NotifyModified();
                    }
                }
            });
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            _postDraw.Add(() => {
                if (i > 0 && i < r.Waypoints.Count) {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    if (target != null) {
                        r.Waypoints.Insert(i, new() { Position = target.Position, Radius = RouteDB.DefaultInteractionRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType, InteractWithOID = target.BaseId, InteractWithName = target.Name.ToString().ToLower() });
                        RouteDB.NotifyModified();
                    }
                }
            });
        }

        if (ImGui.MenuItem("Insert step below")) {
            _postDraw.Add(() => {
                if (i > 0 && i < r.Waypoints.Count) {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    if (Service.ObjectTable.LocalPlayer != null) {
                        r.Waypoints.Insert(i + 1, new() { Position = Player.Position, Radius = RouteDB.DefaultWaypointRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType });
                        RouteDB.NotifyModified();
                    }
                }
            });
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right)) {
            _postDraw.Add(() => {
                if (i > 0 && i < r.Waypoints.Count) {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    if (target != null) {
                        r.Waypoints.Insert(i + 1, new() { Position = target.Position, Radius = RouteDB.DefaultInteractionRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType, InteractWithOID = target.BaseId, InteractWithName = target.Name.ToString().ToLower() });
                        RouteDB.NotifyModified();
                    }
                }
            });
        }

        if (ImGui.MenuItem("Move up")) {
            _postDraw.Add(() => {
                if (i > 0 && i < r.Waypoints.Count) {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    var wp = r.Waypoints[i];
                    r.Waypoints.RemoveAt(i);
                    r.Waypoints.Insert(i - 1, wp);
                    RouteDB.NotifyModified();
                }
            });
        }

        if (ImGui.MenuItem("Move down")) {
            _postDraw.Add(() => {
                if (i + 1 < r.Waypoints.Count) {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    var wp = r.Waypoints[i];
                    r.Waypoints.RemoveAt(i);
                    r.Waypoints.Insert(i + 1, wp);
                    RouteDB.NotifyModified();
                }
            });
        }

        if (ImGui.MenuItem("Delete")) {
            _postDraw.Add(() => {
                if (i < r.Waypoints.Count) {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    r.Waypoints.RemoveAt(i);
                    RouteDB.NotifyModified();
                }
            });
        }
    }
}
