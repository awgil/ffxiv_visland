using ImGuiNET;
using System;
using System.ComponentModel;
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
}
