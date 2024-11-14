using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ECommons.Reflection;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

    public static uint ToHex(this Vector4 color)
    {
        var r = (byte)(color.X * 255);
        var g = (byte)(color.Y * 255);
        var b = (byte)(color.Z * 255);
        var a = (byte)(color.W * 255);
        return (uint)(a << 24 | b << 16 | g << 8 | r);
    }

    public static Vector2 ToVec2(this (int, int) tuple) => new(tuple.Item1, tuple.Item2);

    public static bool HasPlugin(string name) => DalamudReflector.TryGetDalamudPlugin(name, out var _, false, true);

    // item (button, menu item, etc.) that is disabled unless shift is held, useful for 'dangerous' operations like deletion
    public static bool DangerousItem(Func<bool> item)
    {
        var disabled = !ImGui.IsKeyDown(ImGuiKey.ModShift);
        ImGui.BeginDisabled(disabled);
        var res = item();
        ImGui.EndDisabled();
        if (disabled && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("Hold shift");
        return res;
    }
    public static bool DangerousButton(string label) => DangerousItem(() => ImGui.Button(label));
    public static bool DangerousMenuItem(string label) => DangerousItem(() => ImGui.MenuItem(label));

    private static Vector2 size = new(24);
    public static void WorkInProgressIcon()
    {
        const uint iconID = 60073;
        var texture = Svc.Texture?.GetFromGameIcon(iconID).GetWrapOrEmpty();
        if (texture != null)
            ImGui.Image(texture.ImGuiHandle, size);
        else
            ImGui.Dummy(size);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Work in progress");
    }

    private static float startTime;
    public static void FlashText(string text, Vector4 colour1, Vector4 colour2, float duration)
    {
        var currentTime = (float)ImGui.GetTime();
        var elapsedTime = currentTime - startTime;

        var t = (float)Math.Sin(elapsedTime / duration * Math.PI * 2) * 0.5f + 0.5f;

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

    public static void DrawSection(string Label, Vector4 colour, bool PushDown = true, bool drawSeparator = true)
    {
        var style = ImGui.GetStyle();

        // push down a bit
        if (PushDown)
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + style.ItemSpacing.Y * 2);

        using (ImRaii.PushColor(ImGuiCol.Text, colour))
            ImGui.TextUnformatted(Label);

        if (drawSeparator)
        {
            // pull up the separator
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - style.ItemSpacing.Y + 3);
            ImGui.Separator();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + style.ItemSpacing.Y * 2 - 1);
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
    public static void Swap<T>(ref T l, ref T r) => (r, l) = (l, r);

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

    public static (bool IsBase64, string Json) FromCompressedBase64(this string compressedBase64)
    {
        try
        {
            var data = Convert.FromBase64String(compressedBase64);
            using var compressedStream = new MemoryStream(data);
            using var zipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var resultStream = new MemoryStream();
            zipStream.CopyTo(resultStream);
            return (true, Encoding.UTF8.GetString(resultStream.ToArray()));
        }
        catch (FormatException)
        {
            return (false, string.Empty);
        }
    }

    public static bool IsJson(string text)
    {
        try
        {
            JToken.Parse(text);
            return true;
        }
        catch (JsonReaderException)
        {
            return false;
        }
    }

    public static bool EditNumberField(string labelBefore, float fieldWidth, ref int refValue, string labelAfter = "", string helpText = "")
    {
        ImGuiEx.TextV(labelBefore);

        ImGui.SameLine();

        ImGui.PushItemWidth(fieldWidth * ImGuiHelpers.GlobalScale);
        var clicked = ImGui.DragInt($"##{labelBefore}###", ref refValue);
        ImGui.PopItemWidth();

        if (labelAfter != string.Empty)
        {
            ImGui.SameLine();
            ImGuiEx.TextV(labelAfter);
        }

        if (helpText != string.Empty)
            ImGuiComponents.HelpMarker(helpText);

        return clicked;
    }

    public static int EorzeanHour() => DateTimeOffset.FromUnixTimeSeconds(Framework.Instance()->ClientTime.EorzeaTime).Hour;
    public static int EorzeanMinute() => DateTimeOffset.FromUnixTimeSeconds(Framework.Instance()->ClientTime.EorzeaTime).Minute;
}

public static class Extensions
{
    //public static string GetItemName(this ILazyRow row)
    //{
    //    if (Utils.GetSheet<Item>()!.HasRow(row.Row))
    //        return (Utils.GetRow<Item>(row.Row)!.Name);
    //    return Utils.GetSheet<EventItem>()!.HasRow(row.Row) ? Utils.GetRow<EventItem>(row.Row)!.Name : "";
    //}

    //public static string GetGatheringItem(this ILazyRow row)
    //{
    //    if (Utils.GetSheet<GatheringItem>()!.HasRow(row.Row))
    //        return Utils.GetRow<Item>((uint)Utils.GetRow<GatheringItem>(row.Row)!.Item)!.Name;
    //    return Utils.GetSheet<SpearfishingItem>()!.HasRow(row.Row) ? Utils.GetRow<SpearfishingItem>(row.Row)!.Item.GetItemName() : row.Row.ToString();
    //}

    public static int ToUnixTimestamp(this DateTime value) => (int)Math.Truncate(value.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
}