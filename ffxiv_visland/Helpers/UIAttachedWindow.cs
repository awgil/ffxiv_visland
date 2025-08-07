using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace visland.Helpers;

// window attached to the addon
public abstract class UIAttachedWindow : Window, IDisposable
{
    private string _addon;

    public UIAttachedWindow(string name, string addon, Vector2 initialSize) : base(name)
    {
        _addon = addon;
        RespectCloseHotkey = false; // don't steal esc focus
        ShowCloseButton = false; // opened/closed automatically
        Size = initialSize;
        SizeCondition = ImGuiCond.FirstUseEver;
        PositionCondition = ImGuiCond.Always; // updated every frame
        ForceMainWindow = true;
    }

    public virtual void Dispose() { }

    public unsafe override void PreOpenCheck()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName(_addon).Address;
        IsOpen = addon != null && addon->IsVisible;
        if (IsOpen)
        {
            Position = new Vector2(addon->X + addon->GetScaledWidth(true), addon->Y);
        }
    }
}
