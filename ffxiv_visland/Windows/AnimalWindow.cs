using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Interface.Windowing;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Numerics;

namespace visland.Windows;

unsafe class AnimalWindow : Window, IDisposable
{
    public AnimalWindow(Plugin plugin) : base("Pasture Automation")
    {
        RespectCloseHotkey = false; // don't steal esc focus
        ShowCloseButton = false; // opened/closed automatically
        Size = new Vector2(100, 50);
        SizeCondition = ImGuiCond.FirstUseEver;
        PositionCondition = ImGuiCond.Always; // updated every frame
    }
    //AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MJIAnimalManagement", AutoCollectPasture);
    //AddonLifecycle.UnregisterListener(AutoCollectPasture);
    public void Dispose()
    {
    }

    public override void PreOpenCheck()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("MJIAnimalManagement");
        IsOpen = addon != null && addon->IsVisible;
        if (IsOpen)
        {
            Position = new Vector2(addon->X + addon->GetScaledWidth(true), addon->Y);
        }
    }

    public override void Draw()
    {
        ImGui.Checkbox("Auto Collect", ref Plugin.P.Config.AutoCollectPasture);
    }

    private void AutoCollectPasture(AddonEvent eventType, AddonArgs addonInfo)
    {
        var addon = (AtkUnitBase*)addonInfo.Addon;
        if (addonInfo.AddonName != "MJIAnimalManagement") return;
        if (!Plugin.P.Config.AutoCollectPasture) return;
        if (addon->AtkValues[219].Byte != 0) return;

        Callback.Fire(addon, false, 5);
        Helpers.Utils.AutoYesNo();
    }
}
