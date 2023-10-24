using Dalamud;
using Dalamud.Interface;
using ImGuiNET;
using Lumina.Excel;

namespace visland;

public unsafe class Helpers
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
}

public static class LazyRowExtensions
{
    public static LazyRow<T> GetDifferentLanguage<T>(this LazyRow<T> row, ClientLanguage language) where T : ExcelRow
    {
        return new LazyRow<T>(Service.DataManager.GameData, row.Row, language.ToLumina());
    }
}