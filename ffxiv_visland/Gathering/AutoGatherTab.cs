//using Dalamud.Interface.Components;
//using Dalamud.Interface;
//using Dalamud.Interface.Utility.Raii;
//using ECommons.ImGuiMethods;
//using ImGuiNET;
//using System.Numerics;
//using visland.Helpers;
//using System.Collections.Generic;
//using static visland.Gathering.GatherRouteDB;
//using System.Linq;

//namespace visland.Gathering;
//public unsafe class AutoGatherTab(GatherRouteExec exec)
//{
//    private readonly UITree _tree = new();
//    private GatherRouteExec exec = exec;
//    private ShoppingListDB ShoppingListDB = new();

//    private string? import;
//    public void Draw()
//    {
//        DrawHeader();
//        ImGui.Separator();
//        ImGui.Spacing();

//        var cra = ImGui.GetContentRegionAvail();
//        var sidebar = cra with { X = cra.X * 0.40f };
//        var editor = cra with { X = cra.X * 0.60f };

//        DrawSidebar(sidebar);
//        ImGui.SameLine();
//        DrawEditor(editor);
//    }

//    private void DrawHeader()
//    {

//    }

//    private string searchString = string.Empty;
//    private readonly List<ShoppingList> FilteredLists = [];
//    private int selectedListIndex = -1;
//    private void DrawSidebar(Vector2 size)
//    {
//        using var _ = ImRaii.Child("Sidebar", size, false);

//        if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
//            ShoppingListDB.ShoppingLists.Add(new() { Name = "Unnamed List" });

//        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Create a New Shopping List");
//        ImGui.SameLine();

//        if (ImGuiComponents.IconButton(FontAwesomeIcon.FileImport))
//            ShoppingListDB.TryImport(ShoppingListDB);
//        if (ImGui.IsItemHovered())
//            ImGui.SetTooltip("Import Shopping List from Clipboard");

//        ImGuiEx.TextV("Search: ");
//        ImGui.SameLine();
//        ImGuiEx.SetNextItemFullWidth();
//        if (ImGui.InputText("###ListSearch", ref searchString, 500))
//        {
//            FilteredLists.Clear();
//            if (searchString.Length > 0)
//            {
//                foreach (var list in ShoppingListDB.ShoppingLists)
//                {
//                    if (list.Name.Contains(searchString, System.StringComparison.CurrentCultureIgnoreCase) || list.Group.Contains(searchString, System.StringComparison.CurrentCultureIgnoreCase))
//                        FilteredLists.Add(list);
//                }
//            }
//        }

//        ImGui.Separator();

//        using var lists = ImRaii.Child("ShoppingLists");
//        var groups = ShoppingListDB.GetGroups(ShoppingListDB, true);
//        foreach (var group in groups)
//        {
//            foreach (var __ in _tree.Node($"{group}###{groups.IndexOf(group)}", contextMenu: () => ContextMenuGroup(group)))
//            {
//                var source = FilteredLists.Count > 0 ? FilteredLists : [.. ShoppingListDB.ShoppingLists];
//                for (var i = 0; i < source.Count; i++)
//                {
//                    var shoppinglist = source[i];
//                    var routeGroup = string.IsNullOrEmpty(shoppinglist.Group) ? "None" : shoppinglist.Group;
//                    if (routeGroup == group)
//                    {
//                        if (ImGui.Selectable($"{shoppinglist.Name} ({shoppinglist.Orders.Count} steps)###{i}", i == selectedListIndex))
//                            selectedListIndex = i;
//                    }
//                }
//            }
//        }
//    }

//    private void ContextMenuGroup(string group)
//    {
//        var old = group;
//        ImGuiEx.TextV("Name: ");
//        ImGui.SameLine();
//        if (ImGui.InputText("##groupname", ref group, 256))
//            ShoppingListDB.ShoppingLists.Where(r => r.Group == old).ToList().ForEach(r => r.Group = group);
//    }

//    private void DrawEditor(Vector2 size)
//    {
//        using var _ = ImRaii.Child("Editor", size);
//    }
//}
