using Dalamud;
using Dalamud.Interface;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel;
using System;

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
}

public static class LazyRowExtensions
{
    public static LazyRow<T> GetDifferentLanguage<T>(this LazyRow<T> row, ClientLanguage language) where T : ExcelRow
    {
        return new LazyRow<T>(Service.DataManager.GameData, row.Row, language.ToLumina());
    }
}