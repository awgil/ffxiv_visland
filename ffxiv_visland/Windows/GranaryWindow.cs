using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Interface.Windowing;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Numerics;
using static ECommons.GenericHelpers;

namespace visland.Windows;

unsafe class GranaryWindow : Window, IDisposable
{
    public GranaryWindow(Plugin plugin) : base("Granary Automation")
    {
        RespectCloseHotkey = false; // don't steal esc focus
        ShowCloseButton = false; // opened/closed automatically
        Size = new Vector2(250, 50);
        SizeCondition = ImGuiCond.FirstUseEver;
        PositionCondition = ImGuiCond.Always; // updated every frame
    }
    //AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MJIGatheringHouse", AutoCollectGranary);
    //AddonLifecycle.UnregisterListener(AutoCollectGranary);

    public void Dispose()
    {
    }

    public override void PreOpenCheck()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("MJIGatheringHouse");
        IsOpen = addon != null && addon->IsVisible;
        if (IsOpen)
        {
            Position = new Vector2(addon->X + addon->GetScaledWidth(true), addon->Y);
        }
    }

    public override void Draw()
    {
        ImGui.Checkbox("Auto Collect", ref Plugin.P.Config.AutoCollectGranary);
        ImGui.Checkbox("Auto Max", ref Plugin.P.Config.AutoMaxGranary);
    }

    private void AutoCollectGranary(AddonEvent eventType, AddonArgs addonInfo)
    {
        var addon = (AtkUnitBase*)addonInfo.Addon;
        if (addonInfo.AddonName != "MJIGatheringHouse") return;
        if (!Plugin.P.Config.AutoCollectGranary) return;

        if (addon->AtkValues[73].Byte != 0)
        {
            Plugin.P.TaskManager.Enqueue(() => { TryGetAddonByName<AtkUnitBase>("MJIGatheringHouse", out var granary1); Callback.Fire(granary1, false, 13, 0); }, "CollectGranary1");
            Plugin.P.TaskManager.DelayNext(200);
            Plugin.P.TaskManager.Enqueue(() => AutoYesNo());
        }
        if (addon->AtkValues[147].Byte != 0)
        {
            Plugin.P.TaskManager.Enqueue(() => { TryGetAddonByName<AtkUnitBase>("MJIGatheringHouse", out var granary2); Callback.Fire(granary2, false, 13, 1); }, "CollectGranary2");
            Plugin.P.TaskManager.DelayNext(200);
            Plugin.P.TaskManager.Enqueue(() => AutoYesNo());
        }
    }

    private void AutoYesNo()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectYesno");
        if (addon != null && addon->IsVisible && addon->UldManager.NodeList[15]->IsVisible)
            Callback.Fire(addon, true, 0);
    }
}
