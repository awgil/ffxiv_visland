using Dalamud.Interface.Utility;
using Dalamud.Utility;
using ECommons.DalamudServices;
using ImGuiNET;
using Lumina.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace visland.Helpers;

public static class UICombo
{
    public static string EnumString(Enum v)
    {
        var name = v.ToString();
        return v.GetType().GetField(name)?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? name;
    }

    public static bool Enum<T>(string label, ref T v) where T : Enum
    {
        bool res = false;
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo(label, EnumString(v)))
        {
            foreach (var opt in System.Enum.GetValues(v.GetType()))
            {
                if (ImGui.Selectable(EnumString((Enum)opt), opt.Equals(v)))
                {
                    v = (T)opt;
                    res = true;
                }
            }
            ImGui.EndCombo();
        }
        return res;
    }

    public static bool Int(string label, string[] values, ref int v, Func<int, bool> filter)
    {
        bool res = false;
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo(label, v < values.Length ? values[v] : v.ToString()))
        {
            for (int i = 0; i < values.Length; ++i)
            {
                if (filter(i) && ImGui.Selectable(values[i], v == i))
                {
                    v = i;
                    res = true;
                }
            }
            ImGui.EndCombo();
        }
        return res;
    }

    public static bool Int(string label, string[] values, ref int v) => Int(label, values, ref v, _ => true);

    public static bool UInt(string label, string[] values, ref uint v, Func<uint, bool> filter)
    {
        var cast = (int)v;
        var res = Int(label, values, ref cast, x => filter((uint)x));
        v = (uint)cast;
        return res;
    }

    public static bool UInt(string label, string[] values, ref uint v) => UInt(label, values, ref v, _ => true);

    public static bool Bool(string label, string[] values, ref bool v)
    {
        int val = v ? 1 : 0;
        if (!Int(label, values, ref val))
            return false;
        v = val != 0;
        return true;
    }

    public static bool String(string label, string[] values, ref string v)
    {
        bool res = false;
        ImGui.SetNextItemWidth(200);
        if (ImGui.BeginCombo(label, v.ToString()))
        {
            for (int i = 0; i < values.Length; ++i)
            {
                if (ImGui.Selectable(values[i], v == values[i]))
                {
                    v = values[i];
                    res = true;
                }
            }
            ImGui.EndCombo();
        }
        return res;
    }

    public record ExcelSheetOptions<T> where T : ExcelRow
    {
        public Func<T, string> FormatRow { get; init; } = row => row.ToString();
        public Func<T, string, bool>? SearchPredicate { get; init; } = null;
        public Func<T, bool, bool>? DrawSelectable { get; init; } = null;
        public IEnumerable<T>? FilteredSheet { get; init; } = null;
        public Vector2? Size { get; init; } = null;
    }

    public record ExcelSheetComboOptions<T> : ExcelSheetOptions<T> where T : ExcelRow
    {
        public Func<T, string>? GetPreview { get; init; } = null;
        public ImGuiComboFlags ComboFlags { get; init; } = ImGuiComboFlags.None;
    }

    private static string? sheetSearchText;
    private static ExcelRow[]? filteredSearchSheet;
    private static string? prevSearchID;
    private static Type? prevSearchType;

    private static void ExcelSheetSearchInput<T>(string id, IEnumerable<T> filteredSheet, Func<T, string, bool> searchPredicate) where T : ExcelRow
    {
        if (ImGui.IsWindowAppearing() && ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive())
        {
            if (id != prevSearchID)
            {
                if (typeof(T) != prevSearchType)
                {
                    sheetSearchText = string.Empty;
                    prevSearchType = typeof(T);
                }

                filteredSearchSheet = null;
                prevSearchID = id;
            }

            ImGui.SetKeyboardFocusHere(0);
        }

        if (ImGui.InputTextWithHint("##ExcelSheetSearch", "Search", ref sheetSearchText, 128, ImGuiInputTextFlags.AutoSelectAll))
            filteredSearchSheet = null;

        filteredSearchSheet ??= filteredSheet.Where(s => searchPredicate(s, sheetSearchText)).Cast<ExcelRow>().ToArray();
    }

    public static bool ExcelSheetCombo<T>(string id, ref int selectedRow, ExcelSheetComboOptions<T>? options = null) where T : ExcelRow
    {
        options ??= new ExcelSheetComboOptions<T>();
        var sheet = Svc.Data.GetExcelSheet<T>();
        if (sheet == null) return false;

        var getPreview = options.GetPreview ?? options.FormatRow;
        if (!ImGui.BeginCombo(id, sheet.GetRow((uint)selectedRow) is { } r ? getPreview(r) : selectedRow.ToString(), options.ComboFlags | ImGuiComboFlags.HeightLargest)) return false;

        ExcelSheetSearchInput(id, options.FilteredSheet ?? sheet, options.SearchPredicate ?? ((row, s) => options.FormatRow(row).Contains(s, StringComparison.CurrentCultureIgnoreCase)));

        ImGui.BeginChild("ExcelSheetSearchList", options.Size ?? new Vector2(0, 200 * ImGuiHelpers.GlobalScale), true);

        var ret = false;
        var drawSelectable = options.DrawSelectable ?? ((row, selected) => ImGui.Selectable(options.FormatRow(row), selected));
        for (var i = 0; i < filteredSearchSheet!.Length; i++)
        {
            var row = (T)filteredSearchSheet[i];
            ImGui.PushID(i);
            if (!drawSelectable(row, selectedRow == row.RowId)) continue;
            selectedRow = (int)row.RowId;
            ret = true;
            break;
        }

        // ImGui issue #273849, children keep popups from closing automatically
        if (ret)
            ImGui.CloseCurrentPopup();

        ImGui.EndChild();
        ImGui.EndCombo();
        return ret;
    }

    private static string FormatTerritoryRow(Lumina.Excel.GeneratedSheets.TerritoryType t) => t.RowId switch
    {
        _ => $"[#{t.RowId}] {t.PlaceName.Value?.Name}"
    };

    private readonly static Dictionary<uint, Lumina.Excel.GeneratedSheets.TerritoryType> TerritoryTypes = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.TerritoryType>()?.Where(i => i.RowId == 0 || Coordinates.HasAetheryteInZone(i.RowId)).ToDictionary(i => i.RowId, i => i)!;

    public static readonly ExcelSheetComboOptions<Lumina.Excel.GeneratedSheets.TerritoryType> territoryComboOptions = new()
    {
        FormatRow = FormatTerritoryRow,
        FilteredSheet = TerritoryTypes.Select(kv => kv.Value)
    };

    private static string FormatTerritoryRow(Lumina.Excel.GeneratedSheets.Quest q) => q.RowId switch
    {
        _ => $"[#{q.RowId}] {q.Name}"
    };

    private readonly static Dictionary<uint, Lumina.Excel.GeneratedSheets.Quest> Quests = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Quest>()?.Where(q => q.Id.RawString.Length > 0).ToDictionary(i => i.RowId, i => i)!;

    public static readonly ExcelSheetComboOptions<Lumina.Excel.GeneratedSheets.Quest> questComboOptions = new()
    {
        FormatRow = FormatTerritoryRow,
        FilteredSheet = Quests.Select(kv => kv.Value)
    };

    private static string FormatActionRow(Lumina.Excel.GeneratedSheets.Action a) => a.RowId switch
    {
        _ => $"[#{a.RowId} {a.ClassJob.Value?.Abbreviation}{(a.IsPvP ? " PVP" : string.Empty)}] {a.Name}"
    };

    private readonly static Dictionary<uint, Lumina.Excel.GeneratedSheets.Action> Actions = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.Action>()?.Where(i => i.ClassJobCategory.Row > 0 && i.ActionCategory.Row <= 4 && i.RowId > 8).ToDictionary(i => i.RowId, i => i)!;

    public static readonly ExcelSheetComboOptions<Lumina.Excel.GeneratedSheets.Action> actionComboOptions = new()
    {
        FormatRow = FormatActionRow,
        FilteredSheet = Actions.Select(kv => kv.Value)
    };

    private static string FormatActionRow(Lumina.Excel.GeneratedSheets.BNpcName m) => m.RowId switch
    {
        _ => $"[#{m.RowId}] {m.Singular}"
    };

    private readonly static Dictionary<uint, Lumina.Excel.GeneratedSheets.BNpcName> Mobs = Svc.Data.GetExcelSheet<Lumina.Excel.GeneratedSheets.BNpcName>()?.Where(i => !i.Singular.RawString.IsNullOrEmpty()).ToDictionary(i => i.RowId, i => i)!;

    public static readonly ExcelSheetComboOptions<Lumina.Excel.GeneratedSheets.BNpcName> mobComboOptions = new()
    {
        FormatRow = FormatActionRow,
        FilteredSheet = Mobs.Select(kv => kv.Value)
    };
}
