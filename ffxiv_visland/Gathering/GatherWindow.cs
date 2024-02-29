using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Numerics;
using visland.Helpers;
using static visland.Gathering.GatherRouteDB;

namespace visland.Gathering;

public class GatherWindow : Window, IDisposable
{
    private readonly UITree _tree = new();
    private readonly List<System.Action> _postDraw = [];

    public GatherRouteDB RouteDB;
    public GatherRouteExec Exec = new();

    private int selectedRouteIndex = -1;
    public static bool loop;

    private readonly List<uint> Colours = Svc.Data.GetExcelSheet<UIColor>()!.Select(x => x.UIForeground).ToList();
    private Vector4 greenColor = new Vector4(0x5C, 0xB8, 0x5C, 0xFF) / 0xFF;
    private Vector4 redColor = new Vector4(0xD9, 0x53, 0x4F, 0xFF) / 0xFF;
    private Vector4 yellowColor = new Vector4(0xD9, 0xD9, 0x53, 0xFF) / 0xFF;

    private readonly List<int> Emotes = Svc.Data.GetExcelSheet<Emote>()?.Select(x => (int)x.RowId).ToList()!;
    private readonly List<int> Items = Svc.Data.GetExcelSheet<Item>()?.Select(x => (int)x.RowId).ToList()!;
    private readonly List<int> Zones = Svc.Data.GetExcelSheet<TerritoryType>()?.Where(x => Coordinates.HasAetheryteInZone(x.RowId)).Select(x => (int)x.RowId).ToList()!;
    private readonly ExcelSheet<Emote> _emotes;
    private readonly ExcelSheet<Item> _items;
    private readonly ExcelSheet<TerritoryType> _zones;

    private string searchString = string.Empty;
    private readonly List<Route> FilteredRoutes = [];

    public GatherWindow() : base("Gathering Automation", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(800, 800);
        SizeCondition = ImGuiCond.FirstUseEver;
        RouteDB = Service.Config.Get<GatherRouteDB>();

        _emotes = Svc.Data.GetExcelSheet<Emote>()!;
        _items = Svc.Data.GetExcelSheet<Item>()!;
        _zones = Svc.Data.GetExcelSheet<TerritoryType>()!;
    }

    public void Dispose() => Exec.Dispose();

    public override void PreOpenCheck() => Exec.Update();

    public override void Draw()
    {
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

    private void DrawExecution()
    {
        ImGui.Text("Status: ");
        ImGui.SameLine();

        if (Exec.CurrentRoute != null)
            Utils.FlashText($"{(Exec.Paused ? "PAUSED" : Exec.Waiting ? "WAITING" : "RUNNING")}", new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Exec.Paused ? new Vector4(1.0f, 0.0f, 0.0f, 1.0f) : new Vector4(0.0f, 1.0f, 0.0f, 1.0f), 2);
        ImGui.SameLine();
        
        if (Exec.CurrentRoute == null || Exec.CurrentWaypoint >= Exec.CurrentRoute.Waypoints.Count)
        {
            ImGui.Text("Route not running");
            return;
        }

        if (Exec.CurrentRoute != null) // Finish() call could've reset it
        {
            ImGui.SameLine();
            ImGui.Text($"{Exec.CurrentRoute.Name}: Step #{Exec.CurrentWaypoint + 1}");

            if (Exec.Waiting)
            {
                ImGui.SameLine();
                ImGui.Text($"waiting {Exec.WaitUntil - Environment.TickCount64}ms");
            }
        }
    }

    private unsafe void DrawSidebar(Vector2 size)
    {
        using (ImRaii.Child("Sidebar", size, false))
        {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
            {
                RouteDB.Routes.Add(new() { Name = "Unnamed Route" });
                RouteDB.NotifyModified();
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Create a New Route");
            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport))
            {
                try
                {
                    var import = JsonConvert.DeserializeObject<Route>(ImGui.GetClipboardText());
                    RouteDB.Routes.Add(new() { Name = import!.Name, Waypoints = import.Waypoints });
                    RouteDB.NotifyModified();
                }
                catch (JsonReaderException ex)
                {
                    Service.ChatGui.PrintError($"Failed to import route: {ex.Message}");
                    Service.Log.Error(ex, "Failed to import route");
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Import Route from Clipboard (\uE052 Base64)");
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    try
                    {
                        var import = JsonConvert.DeserializeObject<Route>(Utils.FromCompressedBase64(ImGui.GetClipboardText()));
                        RouteDB.Routes.Add(new() { Name = import!.Name, Waypoints = import.Waypoints });
                        RouteDB.NotifyModified();
                    }
                    catch (JsonReaderException ex)
                    {
                        Service.ChatGui.PrintError($"Failed to import route: {ex.Message}");
                        Service.Log.Error(ex, "Failed to import route");
                    }
                }
            }

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Cog))
            {
                ImGui.OpenPopup("Advanced Options");
            }
            DrawRouteSettingsPopup();
            ImGui.SameLine();
            if (ImGui.Checkbox("Stop Route on Error", ref RouteDB.DisableOnErrors))
                RouteDB.NotifyModified();
            ImGuiComponents.HelpMarker("Stops executing a route when you encounter a node you can't gather from due to full inventory.");

            ImGuiEx.TextV("Search: ");
            ImGui.SameLine();
            ImGuiEx.SetNextItemFullWidth();
            if (ImGui.InputText("###RouteSearch", ref searchString, 500))
            {
                FilteredRoutes.Clear();
                if (searchString.Length > 0)
                {
                    foreach (var route in RouteDB.Routes)
                    {
                        if (route.Name.Contains(searchString, StringComparison.CurrentCultureIgnoreCase))
                            FilteredRoutes.Add(route);
                    }
                }
            }

            ImGui.Separator();

            using (ImRaii.Child("routes"))
            {
                for (var i = 0; i < (FilteredRoutes.Count > 0 ? FilteredRoutes.Count : RouteDB.Routes.Count); i++)
                {
                    var routeSource = FilteredRoutes.Count > 0 ? FilteredRoutes : RouteDB.Routes;
                    var route = routeSource[i];
                    var selectedRoute = ImGui.Selectable($"{route.Name} ({route.Waypoints.Count} steps)###{i}", i == selectedRouteIndex);
                    if (selectedRoute)
                        selectedRouteIndex = i;
                }
            }
        }
    }

    private void DrawRouteSettingsPopup()
    {
        using var popup = ImRaii.Popup("Advanced Options");
        if (popup.Success)
        {
            if (ImGui.SliderFloat("Default Waypoint Radius", ref RouteDB.DefaultWaypointRadius, 0, 100))
                RouteDB.NotifyModified();
            if (ImGui.SliderFloat("Default Interaction Radius", ref RouteDB.DefaultInteractionRadius, 0, 100))
                RouteDB.NotifyModified();
            if (ImGui.Checkbox("Auto Enable Gather Mode on Route Start", ref RouteDB.GatherModeOnStart))
                RouteDB.NotifyModified();
        }
    }

    private void DrawEditor(Vector2 size)
    {
        if (selectedRouteIndex == -1) return;

        var routeSource = FilteredRoutes.Count > 0 ? FilteredRoutes : RouteDB.Routes;
        if (routeSource.Count == 0) return;
        var route = selectedRouteIndex >= routeSource.Count ? routeSource.Last() : routeSource[selectedRouteIndex];

        using (ImRaii.Child("Editor", size))
        {
            using (ImRaii.Disabled(Exec.CurrentRoute != null))
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Play))
                    Exec.Start(route, 0, true, loop, route.Waypoints[0].Pathfind);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Execute Route");
            ImGui.SameLine();

            ImGui.PushStyleColor(ImGuiCol.Button, loop ? greenColor : redColor);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, loop ? greenColor : redColor);
            if (ImGuiComponents.IconButton(FontAwesomeIcon.SyncAlt))
                loop ^= true;
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Loop Route");
            ImGui.SameLine();

            if (Exec.CurrentRoute != null)
            {
                if (ImGuiEx.IconButton(Exec.Paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause))
                    Exec.Paused = !Exec.Paused;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(Exec.Paused ? "Resume" : "Pause");
                ImGui.SameLine();

                if (ImGuiComponents.IconButton(FontAwesomeIcon.Stop))
                    Exec.Finish();
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Stop Route");
                ImGui.SameLine();
            }

            var canDelete = !ImGui.GetIO().KeyCtrl;
            using (ImRaii.Disabled(canDelete))
            {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                {
                    if (Exec.CurrentRoute == route)
                        Exec.Finish();
                    RouteDB.Routes.Remove(route);
                    RouteDB.NotifyModified();
                }
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip("Delete Route (Hold CTRL)");
            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileExport))
            {
                ImGui.SetClipboardText(JsonConvert.SerializeObject(route));
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Export Route (\uE052 Base64)");
                if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    ImGui.SetClipboardText(Utils.ToCompressedBase64(route));
            }

            var name = route.Name;
            var movementType = Service.Condition[ConditionFlag.InFlight] ? Movement.MountFly : Service.Condition[ConditionFlag.Mounted] ? Movement.MountNoFly : Movement.Normal;
            ImGuiEx.TextV("Name: ");
            ImGui.SameLine();
            if (ImGui.InputText("", ref name, 256))
            {
                route.Name = name;
                RouteDB.NotifyModified();
            }
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
            {
                Exec.Finish();
                var player = Service.ClientState.LocalPlayer;
                if (player != null)
                {
                    route.Waypoints.Add(new() { Position = player.Position, Radius = RouteDB.DefaultWaypointRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType });
                    RouteDB.NotifyModified();
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add Waypoint: Current Position");
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.UserPlus))
            {
                var target = Service.TargetManager.Target;
                if (target != null)
                {
                    route.Waypoints.Add(new() { Position = target.Position, Radius = RouteDB.DefaultInteractionRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType, InteractWithOID = target.DataId, InteractWithName = target.Name.ToString().ToLower() });
                    RouteDB.NotifyModified();
                    Exec.Start(route, route.Waypoints.Count - 1, false, false, false);
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add Waypoint: Interact with Target");

            using (ImRaii.Child("waypoints"))
            {
                for (var i = 0; i < route.Waypoints.Count; ++i)
                {
                    var wp = route.Waypoints[i];
                    foreach (var wn in _tree.Node($"#{i + 1}: [x: {wp.Position.X:f0}, y: {wp.Position.Y:f0}, z: {wp.Position.Z:f0}] ({wp.Movement}){(wp.InteractWithOID != 0 ? $" @ {wp.InteractWithName} ({wp.InteractWithOID:X})" : "")}###{i}", contextMenu: () => ContextMenuWaypoint(route, i)))
                    {
                        DrawWaypoint(wp);
                    }
                }
            }
        }
    }

    private void DrawWaypoint(Waypoint wp)
    {
        if (ImGuiEx.IconButton(FontAwesomeIcon.MapMarker) && Service.ClientState.LocalPlayer is var player && player != null)
        {
            wp.Position = player.Position;
            wp.ZoneID = Service.ClientState.TerritoryType;
            RouteDB.NotifyModified();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Set Position to Current");
        ImGui.SameLine();
        if (ImGui.InputFloat3("Position", ref wp.Position))
            RouteDB.NotifyModified();
        //if (Utils.ExcelSheetCombo("##Territory", ref wp.ZoneID, Utils.territoryComboOptions))
        //    RouteDB.NotifyModified();
        if (ImGui.InputFloat("Radius (yalms)", ref wp.Radius))
            RouteDB.NotifyModified();
        if (UICombo.Enum("Movement mode", ref wp.Movement))
            RouteDB.NotifyModified();
        ImGui.SameLine();
        using (var noNav = ImRaii.Disabled(!Utils.HasPlugin("vnavmesh")))
        {
            if (ImGui.Checkbox("Pathfind?", ref wp.Pathfind))
                RouteDB.NotifyModified();
        }
        if (ImGuiComponents.IconButton(FontAwesomeIcon.UserPlus))
        {
            if (wp.InteractWithOID == default)
            {
                var target = Service.TargetManager.Target;
                if (target != null)
                {
                    wp.InteractWithOID = target.DataId;
                    RouteDB.NotifyModified();
                }
            }
            else
                wp.InteractWithOID = default;
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add/Remove target from waypoint");
        ImGui.SameLine();
        if (ImGuiEx.IconButton(FontAwesomeIcon.CommentDots))
        {
            wp.showInteractions ^= true;
            RouteDB.NotifyModified();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle Interactions");
        ImGui.SameLine();
        if (ImGuiEx.IconButton(FontAwesomeIcon.Clock))
        {
            wp.showWaits ^= true;
            RouteDB.NotifyModified();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle Waits");

        if (wp.showInteractions)
        {
            if (UICombo.Enum("Interaction Type", ref wp.Interaction))
                RouteDB.NotifyModified();
            switch (wp.Interaction)
            {
                case InteractionType.None: break;
                case InteractionType.Standard: break;
                case InteractionType.Emote:
                    ImGui.PushItemWidth(100);
                    if (ImGui.DragInt($"Use Emote {_emotes.GetRow((uint)wp.EmoteID)?.Name}###{nameof(InteractionType.Emote)}", ref wp.EmoteID, 1, Emotes.First(), Emotes.Last()))
                        RouteDB.NotifyModified();
                    break;
                case InteractionType.UseItem:
                    ImGui.PushItemWidth(100);
                    if (ImGui.DragInt($"Item {_items.GetRow((uint)wp.ItemID)?.Name}###{nameof(InteractionType.UseItem)}", ref wp.ItemID, 1, Items.First(), Items.Last()))
                        RouteDB.NotifyModified();
                    break;
                case InteractionType.UseAction:
                    if (Utils.ExcelSheetCombo("##Action", ref wp.ActionID, Utils.actionComboOptions))
                        RouteDB.NotifyModified();
                    break;
                case InteractionType.Grind:
                    using (var noVbm = ImRaii.Disabled(!Utils.HasPlugin("BossMod")))
                    {
                        if (Utils.ExcelSheetCombo("##Mob", ref wp.MobID, Utils.mobComboOptions))
                            RouteDB.NotifyModified();
                    }
                    break;
            }
        }

        if (wp.showWaits)
        {
            if (ImGui.SliderInt("Wait (ms)", ref wp.WaitTimeMs, 0, 60000))
                RouteDB.NotifyModified();
            if (UICombo.Enum("Wait for Condition", ref wp.WaitForCondition))
                RouteDB.NotifyModified();
        }
    }

    private void ContextMenuWaypoint(Route r, int i)
    {
        if (ImGui.MenuItem("Execute this step only"))
        {
            Exec.Start(r, i, false, false, r.Waypoints[i].Pathfind);
        }

        if (ImGui.MenuItem("Execute route once starting from this step"))
        {
            Exec.Start(r, i, true, false, r.Waypoints[i].Pathfind);
        }

        if (ImGui.MenuItem("Execute route starting from this step and then loop"))
        {
            Exec.Start(r, i, true, true, r.Waypoints[i].Pathfind);
        }

        var movementType = Service.Condition[ConditionFlag.InFlight] ? Movement.MountFly : Service.Condition[ConditionFlag.Mounted] ? Movement.MountNoFly : Movement.Normal;
        var target = Service.TargetManager.Target;

        if (ImGui.MenuItem($"Swap to {(r.Waypoints[i].InteractWithOID != default ? "normal waypoint" : "interact waypoint")}"))
        {
            _postDraw.Add(() =>
            {
                r.Waypoints[i].InteractWithOID = r.Waypoints[i].InteractWithOID != default ? default : target?.DataId ?? default;
                RouteDB.NotifyModified();
            });
        }

        if (ImGui.MenuItem("Insert step above"))
        {
            _postDraw.Add(() =>
            {
                if (i > 0 && i < r.Waypoints.Count)
                {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    if (Service.ClientState.LocalPlayer != null)
                    {
                        r.Waypoints.Insert(i, new() { Position = Service.ClientState.LocalPlayer.Position, Radius = RouteDB.DefaultWaypointRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType });
                        RouteDB.NotifyModified();
                    }
                }
            });
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            _postDraw.Add(() =>
            {
                if (i > 0 && i < r.Waypoints.Count)
                {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    if (target != null)
                    {
                        r.Waypoints.Insert(i, new() { Position = target.Position, Radius = RouteDB.DefaultInteractionRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType, InteractWithOID = target.DataId, InteractWithName = target.Name.ToString().ToLower() });
                        RouteDB.NotifyModified();
                    }
                }
            });
        }

        if (ImGui.MenuItem("Insert step below"))
        {
            _postDraw.Add(() =>
            {
                if (i > 0 && i < r.Waypoints.Count)
                {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    if (Service.ClientState.LocalPlayer != null)
                    {
                        r.Waypoints.Insert(i + 1, new() { Position = Service.ClientState.LocalPlayer.Position, Radius = RouteDB.DefaultWaypointRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType });
                        RouteDB.NotifyModified();
                    }
                }
            });
        }
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            _postDraw.Add(() =>
            {
                if (i > 0 && i < r.Waypoints.Count)
                {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    if (target != null)
                    {
                        r.Waypoints.Insert(i + 1, new() { Position = target.Position, Radius = RouteDB.DefaultInteractionRadius, ZoneID = Service.ClientState.TerritoryType, Movement = movementType, InteractWithOID = target.DataId, InteractWithName = target.Name.ToString().ToLower() });
                        RouteDB.NotifyModified();
                    }
                }
            });
        }

        if (ImGui.MenuItem("Move up"))
        {
            _postDraw.Add(() =>
            {
                if (i > 0 && i < r.Waypoints.Count)
                {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    var wp = r.Waypoints[i];
                    r.Waypoints.RemoveAt(i);
                    r.Waypoints.Insert(i - 1, wp);
                    RouteDB.NotifyModified();
                }
            });
        }

        if (ImGui.MenuItem("Move down"))
        {
            _postDraw.Add(() =>
            {
                if (i + 1 < r.Waypoints.Count)
                {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    var wp = r.Waypoints[i];
                    r.Waypoints.RemoveAt(i);
                    r.Waypoints.Insert(i + 1, wp);
                    RouteDB.NotifyModified();
                }
            });
        }

        if (ImGui.MenuItem("Delete"))
        {
            _postDraw.Add(() =>
            {
                if (i < r.Waypoints.Count)
                {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    r.Waypoints.RemoveAt(i);
                    RouteDB.NotifyModified();
                }
            });
        }
    }
}
