using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Numerics;
using visland.Helpers;

namespace visland.Gathering;

public class GatherWindow : Window, IDisposable
{
    private readonly UITree _tree = new();
    private readonly List<System.Action> _postDraw = new();

    public GatherRouteDB RouteDB;
    public GatherRouteExec Exec = new();

    private int selectedRouteIndex = -1;
    private static bool loop;

    private Vector4 greenColor = new Vector4(0x5C, 0xB8, 0x5C, 0xFF) / 0xFF;
    private Vector4 redColor = new Vector4(0xD9, 0x53, 0x4F, 0xFF) / 0xFF;

    private string searchString = string.Empty;
    private readonly List<GatherRouteDB.Route> FilteredRoutes = new();
    private bool hornybonk;

    public GatherWindow() : base("Gathering Automation", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size = new Vector2(800, 800);
        SizeCondition = ImGuiCond.FirstUseEver;
        RouteDB = Service.Config.Get<GatherRouteDB>();
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
            Utils.FlashText($"{(Exec.Paused ? "PAUSED" : "RUNNING")}", new Vector4(1.0f, 1.0f, 1.0f, 1.0f), Exec.Paused ? new Vector4(1.0f, 0.0f, 0.0f, 1.0f) : new Vector4(0.0f, 1.0f, 0.0f, 1.0f), 2);
        ImGui.SameLine();

        if (Exec.CurrentRoute == null || Exec.CurrentWaypoint >= Exec.CurrentRoute.Waypoints.Count)
        {
            ImGui.Text("Route not running");
            return;
        }

        var curPos = Service.ClientState.LocalPlayer?.Position ?? new();
        var wp = Exec.CurrentRoute.Waypoints[Exec.CurrentWaypoint];
        if (Exec.CurrentRoute != null) // Finish() call could've reset it
        {
            ImGui.SameLine();
            ImGui.Text($"{Exec.CurrentRoute.Name}: Step #{Exec.CurrentWaypoint + 1}");
        }
    }

    private unsafe void DrawSidebar(Vector2 size)
    {
        using (ImRaii.Child("Sidebar", size, false))
        {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
            {
                RouteDB.Routes.Add(new() { Name = "Unnamed Route"});
                RouteDB.NotifyModified();
            }

            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Create a New Route");
            ImGui.SameLine();

            if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport))
            {
                try
                {
                    var import = JsonConvert.DeserializeObject<GatherRouteDB.Route>(ImGui.GetClipboardText());
                    RouteDB.Routes.Add(new() { Name = import!.Name, Waypoints = import.Waypoints });
                    RouteDB.NotifyModified();
                }
                catch (JsonReaderException ex)
                {
                    Service.ChatGui.PrintError($"Failed to import route: {ex.Message}");
                    Service.Log.Error(ex, "Failed to import route");
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Import Route from Clipboard");

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
                if (searchString.Equals("ERP", StringComparison.CurrentCultureIgnoreCase) && !hornybonk)
                {
                    hornybonk = true;
                    Util.OpenLink("https://duckduckgo.com/?t=h_&q=grass&iax=images&ia=images");
                }
                else
                {
                    hornybonk = false;
                }
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
                for (int i = 0; i < (FilteredRoutes.Count > 0 ? FilteredRoutes.Count : RouteDB.Routes.Count); i++)
                {
                    var route = RouteDB.Routes[i];
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
        }
    }

    private void DrawEditor(Vector2 size)
    {
        if (selectedRouteIndex == -1) return;
        var route = RouteDB.Routes[selectedRouteIndex];

        using (ImRaii.Child("Editor", size))
        {
            using (ImRaii.Disabled(Exec.CurrentRoute != null))
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Play))
                    Exec.Start(route, 0, true, loop);
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
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Export Route");

            var name = route.Name;
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
                    route.Waypoints.Add(new() { Position = player.Position, Radius = RouteDB.DefaultWaypointRadius, ZoneID = Service.ClientState.TerritoryType, Movement = Service.Condition[ConditionFlag.Mounted] ? GatherRouteDB.Movement.MountFly : GatherRouteDB.Movement.Normal });
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
                    route.Waypoints.Add(new() { Position = target.Position, Radius = RouteDB.DefaultInteractionRadius, ZoneID = Service.ClientState.TerritoryType, Movement = Service.Condition[ConditionFlag.Mounted] ? GatherRouteDB.Movement.MountFly : GatherRouteDB.Movement.Normal, InteractWithOID = target.DataId, InteractWithName = target.Name.ToString().ToLower() });
                    RouteDB.NotifyModified();
                    Exec.Start(route, route.Waypoints.Count - 1, false, false);
                }
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add Waypoint: Interact with Target");

            using (ImRaii.Child("waypoints"))
            {
                for (int i = 0; i < route.Waypoints.Count; ++i)
                {
                    var wp = route.Waypoints[i];
                    foreach (var wn in _tree.Node($"#{i + 1}: Goto: [{wp.Position.X:f0}, {wp.Position.Y:f0}, {wp.Position.Z:f0}] ({wp.Movement}){(wp.InteractWithOID != 0 ? $" @ {wp.InteractWithName} ({wp.InteractWithOID:X})" : "")}###{i}", contextMenu: () => ContextMenuWaypoint(route, i)))
                    {
                        DrawWaypoint(wp);
                    }
                }
            }
        }
    }

    private void DrawWaypoint(GatherRouteDB.Waypoint wp)
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
        if (ImGui.InputInt("ZoneID", ref wp.ZoneID))
            RouteDB.NotifyModified();
        //if (wp.ZoneID != 0)
        //    ImGui.Text($"{Service.DataManager.GetExcelSheet<TerritoryType>()!.GetRow((uint)wp.ZoneID)!.PlaceName.Value!.Name}");
        if (ImGui.InputFloat("Radius", ref wp.Radius))
            RouteDB.NotifyModified();
        if (UICombo.Enum("Movement mode", ref wp.Movement))
            RouteDB.NotifyModified();

        //if (wp.showInteractions)
        //{
        //    if (UICombo.Enum("Interaction Type", ref wp.Interaction))
        //        RouteDB.NotifyModified();
        //    switch (wp.Interaction)
        //    {
        //        case GatherRouteDB.InteractionType.None: break;
        //        case GatherRouteDB.InteractionType.Standard: break;
        //        case GatherRouteDB.InteractionType.Maim:
        //            if (ImGui.InputInt($"Bring health to x%###{nameof(GatherRouteDB.InteractionType.Maim)}", ref wp.MaimPercent))
        //                RouteDB.NotifyModified();
        //            break;
        //        case GatherRouteDB.InteractionType.Kill: break;
        //        case GatherRouteDB.InteractionType.Heal:
        //            if (ImGui.InputInt($"Heal to x%###{nameof(GatherRouteDB.InteractionType.Heal)}", ref wp.HealPercent))
        //                RouteDB.NotifyModified();
        //            break;
        //        case GatherRouteDB.InteractionType.Emote:
        //            if (ImGui.InputInt($"Use Emote###{nameof(GatherRouteDB.InteractionType.Emote)}", ref wp.EmoteID))
        //                RouteDB.NotifyModified();
        //            ImGui.SameLine();
        //            if (wp.EmoteID != 0)
        //            {
        //                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, 0))
        //                    ImGui.TextColored(Colours.UIForeground576, $"{(wp.EmoteID != 0 ? Svc.Data.GetExcelSheet<Emote>(Svc.ClientState.ClientLanguage)!.GetRow((uint)wp.EmoteID)!.Name : "")}");
        //            }
        //            break;
        //        case GatherRouteDB.InteractionType.TalkTo: break;
        //        case GatherRouteDB.InteractionType.PickupQuest:
        //            if (ImGui.InputInt($"Quest ID###{nameof(GatherRouteDB.InteractionType.PickupQuest)}", ref wp.QuestID))
        //                RouteDB.NotifyModified();
        //            ImGui.Text($"{QuestsHelper.GetNameOfQuest((ushort)wp.QuestID)}");
        //            break;
        //        case GatherRouteDB.InteractionType.TurnInQuest:
        //            if (ImGui.InputInt($"Quest ID###{nameof(GatherRouteDB.InteractionType.TurnInQuest)}", ref wp.QuestID))
        //                RouteDB.NotifyModified();
        //            ImGui.Text($"{QuestsHelper.GetNameOfQuest((ushort)wp.QuestID)}");
        //            break;
        //        case GatherRouteDB.InteractionType.HandOver:
        //            if (ImGui.InputInt($"Item ID###{nameof(GatherRouteDB.InteractionType.HandOver)}", ref wp.ItemID))
        //                RouteDB.NotifyModified();
        //            break;
        //        case GatherRouteDB.InteractionType.UseItem:
        //            if (ImGui.InputInt($"Item ID###{nameof(GatherRouteDB.InteractionType.UseItem)}", ref wp.ItemID))
        //                RouteDB.NotifyModified();
        //            break;
        //    }
        //}

        //if (wp.showWaits)
        //{
        //    if (ImGui.SliderInt("Wait (ms)", ref wp.WaitTimeMs, 0, 60000))
        //        RouteDB.NotifyModified();
        //    if (UICombo.Enum("Wait for Condition", ref wp.WaitForCondition))
        //        RouteDB.NotifyModified();
        //}

        //if (ImGuiEx.IconButton(FontAwesomeIcon.CommentDots))
        //{
        //    wp.showInteractions ^= true;
        //    RouteDB.NotifyModified();
        //}
        //if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle Interactions");
        //ImGui.SameLine();
        //if (ImGuiEx.IconButton(FontAwesomeIcon.Clock))
        //{
        //    wp.showWaits ^= true;
        //    RouteDB.NotifyModified();
        //}
        //if (ImGui.IsItemHovered()) ImGui.SetTooltip("Toggle Waits");
    }

    private void ContextMenuWaypoint(GatherRouteDB.Route r, int i)
    {
        if (ImGui.MenuItem("Execute this step only"))
        {
            Exec.Start(r, i, false, false);
        }

        if (ImGui.MenuItem("Execute route once starting from this step"))
        {
            Exec.Start(r, i, true, false);
        }

        if (ImGui.MenuItem("Execute route starting from this step and then loop"))
        {
            Exec.Start(r, i, true, true);
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
