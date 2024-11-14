using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using ImGuiNET;
using Lumina.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
        var res = false;
        ImGui.SetNextItemWidth(200);
        using var combo = ImRaii.Combo(label, EnumString(v));
        if (!combo) return false;
        foreach (var opt in System.Enum.GetValues(v.GetType()))
        {
            if (ImGui.Selectable(EnumString((Enum)opt), opt.Equals(v)))
            {
                v = (T)opt;
                res = true;
            }
        }
        return res;
    }

    public static bool Int(string label, string[] values, ref int v, Func<int, bool> filter)
    {
        var res = false;
        ImGui.SetNextItemWidth(200);
        if (ImRaii.Combo(label, v < values.Length ? values[v] : v.ToString()))
        {
            for (var i = 0; i < values.Length; ++i)
            {
                if (filter(i) && ImGui.Selectable(values[i], v == i))
                {
                    v = i;
                    res = true;
                }
            }
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
        var val = v ? 1 : 0;
        if (!Int(label, values, ref val))
            return false;
        v = val != 0;
        return true;
    }

    public static bool String(string label, string[] values, ref string v)
    {
        var res = false;
        ImGui.SetNextItemWidth(200);
        using var combo = ImRaii.Combo(label, v.ToString());
        if (!combo) return false;
        for (var i = 0; i < values.Length; ++i)
        {
            if (ImGui.Selectable(values[i], v == values[i]))
            {
                v = values[i];
                res = true;
            }
        }
        return res;
    }

    // https://github.com/Koenari/HimbeertoniRaidTool/blob/b28313e6d62de940acc073f203e3032e846bfb13/HimbeertoniRaidTool/UI/ImGuiHelper.cs#L188
    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T selected,
        Func<ExcelSheet<T>, string> getPreview,
        ImGuiComboFlags flags = ImGuiComboFlags.None) where T : struct, IExcelRow<T>
            => ExcelSheetCombo(id, out selected, getPreview, t => t.ToString() ?? string.Empty, flags);
    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T selected,
        Func<ExcelSheet<T>, string> getPreview, Func<T, string, bool> searchPredicate,
        ImGuiComboFlags flags = ImGuiComboFlags.None) where T : struct, IExcelRow<T>
            => ExcelSheetCombo(id, out selected, getPreview, t => t.ToString() ?? string.Empty, searchPredicate, flags);
    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T selected,
        Func<ExcelSheet<T>, string> getPreview, Func<T, bool> preFilter,
        ImGuiComboFlags flags = ImGuiComboFlags.None) where T : struct, IExcelRow<T>
            => ExcelSheetCombo(id, out selected, getPreview, t => t.ToString() ?? string.Empty, preFilter, flags);
    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T selected,
        Func<ExcelSheet<T>, string> getPreview, Func<T, string, bool> searchPredicate,
        Func<T, bool> preFilter, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : struct, IExcelRow<T>
            => ExcelSheetCombo(id, out selected, getPreview, t => t.ToString() ?? string.Empty, searchPredicate, preFilter, flags);
    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T selected,
        Func<ExcelSheet<T>, string> getPreview, Func<T, string> toName,
        ImGuiComboFlags flags = ImGuiComboFlags.None) where T : struct, IExcelRow<T>
            => ExcelSheetCombo(id, out selected, getPreview, toName, (t, s) => toName(t).Contains(s, StringComparison.CurrentCultureIgnoreCase), flags);
    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T selected,
        Func<ExcelSheet<T>, string> getPreview, Func<T, string> toName,
        Func<T, string, bool> searchPredicate,
        ImGuiComboFlags flags = ImGuiComboFlags.None) where T : struct, IExcelRow<T>
            => ExcelSheetCombo(id, out selected, getPreview, toName, searchPredicate, _ => true, flags);
    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T selected,
        Func<ExcelSheet<T>, string> getPreview, Func<T, string> toName,
        Func<T, bool> preFilter, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : struct, IExcelRow<T>
            => ExcelSheetCombo(id, out selected, getPreview, toName, (t, s) => toName(t).Contains(s, StringComparison.CurrentCultureIgnoreCase), preFilter, flags);
    public static bool ExcelSheetCombo<T>(string id, [NotNullWhen(true)] out T selected,
        Func<ExcelSheet<T>, string> getPreview, Func<T, string> toName,
        Func<T, string, bool> searchPredicate,
        Func<T, bool> preFilter, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : struct, IExcelRow<T>
    {
        var sheet = Svc.Data.GetExcelSheet<T>();
        if (sheet is null)
        {
            selected = default;
            return false;
        }
        return SearchableCombo(id, out selected, getPreview(sheet), sheet, toName, searchPredicate, preFilter, flags);
    }

    private static string _search = string.Empty;
    private static HashSet<object>? _filtered;
    private static int _hoveredItem;
    private static readonly Dictionary<string, (bool toogle, bool wasEnterClickedLastTime)> _comboDic = new();

    public static bool SearchableCombo<T>(string id, [NotNullWhen(true)] out T? selected, string preview,
        IEnumerable<T> possibilities, Func<T, string> toName,
        ImGuiComboFlags flags = ImGuiComboFlags.None) where T : notnull
            => SearchableCombo(id, out selected, preview, possibilities, toName, (p, s) => toName.Invoke(p).Contains(s, StringComparison.InvariantCultureIgnoreCase), flags);
    public static bool SearchableCombo<T>(string id, [NotNullWhen(true)] out T? selected, string preview,
        IEnumerable<T> possibilities, Func<T, string> toName,
        Func<T, string, bool> searchPredicate,
        ImGuiComboFlags flags = ImGuiComboFlags.None) where T : notnull
            => SearchableCombo(id, out selected, preview, possibilities, toName, searchPredicate, _ => true, flags);
    public static bool SearchableCombo<T>(string id, [NotNullWhen(true)] out T? selected, string preview,
        IEnumerable<T> possibilities, Func<T, string> toName,
        Func<T, string, bool> searchPredicate,
        Func<T, bool> preFilter, ImGuiComboFlags flags = ImGuiComboFlags.None) where T : notnull
    {

        _comboDic.TryAdd(id, (false, false));
        (var toggle, var wasEnterClickedLastTime) = _comboDic[id];
        selected = default;
        using var combo = ImRaii.Combo(id + (toggle ? "##x" : ""), preview, flags);
        if (!combo) return false;
        if (wasEnterClickedLastTime || ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            toggle = !toggle;
            _search = string.Empty;
            _filtered = null;
        }
        var enterClicked = ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter);
        wasEnterClickedLastTime = enterClicked;
        _comboDic[id] = (toggle, wasEnterClickedLastTime);
        if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
            _hoveredItem--;
        if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
            _hoveredItem++;
        _hoveredItem = Math.Clamp(_hoveredItem, 0, Math.Max(_filtered?.Count - 1 ?? 0, 0));
        if (ImGui.IsWindowAppearing() && ImGui.IsWindowFocused() && !ImGui.IsAnyItemActive())
        {
            _search = string.Empty;
            _filtered = null;
            ImGui.SetKeyboardFocusHere(0);
        }

        if (ImGui.InputText("##ExcelSheetComboSearch", ref _search, 128))
            _filtered = null;
        if (_filtered == null)
        {
            _filtered = possibilities.Where(preFilter).Where(s => searchPredicate(s, _search)).Cast<object>().ToHashSet();
            _hoveredItem = 0;
        }
        var i = 0;
        foreach (var row in _filtered.Cast<T>())
        {
            var hovered = _hoveredItem == i;
            ImGui.PushID(i);

            if (ImGui.Selectable(toName(row), hovered) || enterClicked && hovered)
            {
                selected = row;
                ImGui.PopID();
                ImGui.EndCombo();
                return true;
            }
            ImGui.PopID();
            i++;
        }
        return false;
    }
}
