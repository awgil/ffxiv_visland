using Dalamud;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using Lumina.Excel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;

namespace visland.Helpers;

public static unsafe class Utils
{
    public static Vector4 ConvertToVector4(uint color)
    {
        var r = (byte)(color >> 24);
        var g = (byte)(color >> 16);
        var b = (byte)(color >> 8);
        var a = (byte)color;

        return new Vector4(r / 255.0f, g / 255.0f, b / 255.0f, a / 255.0f);
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
        for (var i = 0; i < filteredSearchSheet.Length; i++)
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

    private static float startTime;
    public static void FlashText(string text, Vector4 colour1, Vector4 colour2, float duration)
    {
        float currentTime = (float)ImGui.GetTime();
        float elapsedTime = currentTime - startTime;

        float t = (float)Math.Sin(elapsedTime / duration * Math.PI * 2) * 0.5f + 0.5f;

        // Interpolate the color difference
        Vector4 interpolatedColor = new(
            colour1.X + t * (colour2.X - colour1.X),
            colour1.Y + t * (colour2.Y - colour1.Y),
            colour1.Z + t * (colour2.Z - colour1.Z),
            1.0f
        );

        ImGui.PushStyleColor(ImGuiCol.Text, interpolatedColor);
        ImGui.Text(text);
        ImGui.PopStyleColor();

        if (elapsedTime >= duration)
        {
            startTime = currentTime;
        }
    }

    // note: argument should really be any AtkEventInterface
    public static AtkValue SynthesizeEvent(AgentInterface* receiver, ulong eventKind, Span<AtkValue> args)
    {
        AtkValue res = new();
        receiver->ReceiveEvent(&res, args.GetPointer(0), (uint)args.Length, eventKind);
        return res;
    }

    // get number of owned items by id
    public static int NumItems(uint id) => InventoryManager.Instance()->GetInventoryItemCount(id);
    public static int NumCowries() => NumItems(37549);

    // sort elements of a list by key
    public static void SortBy<TValue, TKey>(this List<TValue> list, Func<TValue, TKey> proj) where TKey : notnull, IComparable => list.Sort((l, r) => proj(l).CompareTo(proj(r)));
    public static void SortByReverse<TValue, TKey>(this List<TValue> list, Func<TValue, TKey> proj) where TKey : notnull, IComparable => list.Sort((l, r) => proj(r).CompareTo(proj(l)));

    // swap two values
    public static void Swap<T>(ref T l, ref T r)
    {
        var t = l;
        l = r;
        r = t;
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

    public static unsafe string ToCompressedBase64<T>(T data)
    {
        try
        {
            var json = JsonConvert.SerializeObject(data, Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json);
            using var compressedStream = new MemoryStream();
            using (var zipStream = new GZipStream(compressedStream, CompressionMode.Compress))
            {
                zipStream.Write(bytes, 0, bytes.Length);
            }

            return Convert.ToBase64String(compressedStream.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string FromCompressedBase64(this string compressedBase64)
    {
        var data = Convert.FromBase64String(compressedBase64);
        using var compressedStream = new MemoryStream(data);
        using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var resultStream = new MemoryStream();
        zipStream.CopyTo(resultStream);
        return Encoding.UTF8.GetString(resultStream.ToArray());
    }
}

public static class LazyRowExtensions
{
    public static LazyRow<T> GetDifferentLanguage<T>(this LazyRow<T> row, ClientLanguage language) where T : ExcelRow
    {
        return new LazyRow<T>(Service.DataManager.GameData, row.Row, language.ToLumina());
    }
}