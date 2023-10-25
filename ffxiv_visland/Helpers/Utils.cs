using Dalamud;
using Dalamud.Interface;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace visland.Helpers;

public unsafe class Utils
{
    public static bool IconButton(FontAwesomeIcon icon, string text, string tooltip, int width = -1)
    {
        ImGui.PushFont(UiBuilder.IconFont);

        if (width > 0)
            ImGui.SetNextItemWidth(32);

        var result = ImGui.Button($"{icon.ToIconString()}##{icon.ToIconString()}-{tooltip}");
        ImGui.PopFont();

        if (tooltip != null)
            TextTooltip(tooltip);

        return result;
    }

    public static void TextTooltip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(text);
            ImGui.EndTooltip();
        }
    }

    // item (button, menu item, etc.) that is disabled unless shift is held, useful for 'dangerous' operations like deletion
    public static bool DangerousItem(Func<bool> item)
    {
        bool disabled = !ImGui.IsKeyDown(ImGuiKey.ModShift);
        ImGui.BeginDisabled(disabled);
        bool res = item();
        ImGui.EndDisabled();
        if (disabled && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hold shift");
        return res;
    }
    public static bool DangerousButton(string label) => DangerousItem(() => ImGui.Button(label));
    public static bool DangerousMenuItem(string label) => DangerousItem(() => ImGui.MenuItem(label));

    public static unsafe int GetMaxWorkshops()
    {
        try
        {
            var currentRank = MJIManager.Instance()->IslandState.CurrentRank;
            return currentRank switch
            {
                1 when currentRank < 3 => 0,
                2 when currentRank < 6 => 1,
                3 when currentRank < 8 => 2,
                4 when currentRank < 14 => 3,
                _ => 4,
            };
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex.Message);
            return 4;
        }
    }

    public static void TextV(string s)
    {
        var cur = ImGui.GetCursorPos();
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 0);
        ImGui.Button("");
        ImGui.PopStyleVar();
        ImGui.SameLine();
        ImGui.SetCursorPos(cur);
        ImGui.TextUnformatted(s);
    }

    public static void AutoYesNo()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectYesno");
        if (addon != null && addon->IsVisible && addon->UldManager.NodeList[15]->IsVisible)
            Callback.Fire(addon, true, 0);
    }

    // get all types defined in specified assembly
    public static IEnumerable<Type?> GetAllTypes(Assembly asm)
    {
        try
        {
            return asm.DefinedTypes;
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types;
        }
    }

    // get all types derived from specified type in specified assembly
    public static IEnumerable<Type> GetDerivedTypes<Base>(Assembly asm)
    {
        var b = typeof(Base);
        return GetAllTypes(asm).Where(t => t?.IsSubclassOf(b) ?? false).Select(t => t!);
    }
}

public static class LazyRowExtensions
{
    public static LazyRow<T> GetDifferentLanguage<T>(this LazyRow<T> row, ClientLanguage language) where T : ExcelRow
    {
        return new LazyRow<T>(Service.DataManager.GameData, row.Row, language.ToLumina());
    }
}