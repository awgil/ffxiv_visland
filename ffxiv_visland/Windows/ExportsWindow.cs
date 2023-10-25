using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Interface.Windowing;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Numerics;
using ECommons.DalamudServices;

namespace visland.Windows;

unsafe class ExportsWindow : Window, IDisposable
{
    public ExportsWindow(Plugin plugin) : base("Exports Automation")
    {
        RespectCloseHotkey = false; // don't steal esc focus
        ShowCloseButton = false; // opened/closed automatically
        Size = new Vector2(150, 100);
        SizeCondition = ImGuiCond.FirstUseEver;
        PositionCondition = ImGuiCond.Always; // updated every frame
    }
    //AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MJIDisposeShop", AutoExport);
    //AddonLifecycle.UnregisterListener(AutoCollectFarm);

    public void Dispose()
    {
    }

    public override void PreOpenCheck()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("MJIDisposeShop");
        IsOpen = addon != null && addon->IsVisible;
        if (IsOpen)
        {
            Position = new Vector2(addon->X + addon->GetScaledWidth(true), addon->Y);
        }
    }

    public override void Draw()
    {
        ImGui.Checkbox("Auto Export", ref Plugin.P.Config.AutoSell);
        if (Plugin.P.Config.AutoSell)
        {
            ImGui.PushItemWidth(150);
            ImGui.SliderInt("Auto Sell Amount", ref Plugin.P.Config.AutoSellAmount, 0, 999);
            ImGui.PopItemWidth();
        }
    }

    private static void AutoExport(AddonEvent eventType, AddonArgs addonInfo)
    {
        var addon = (AtkUnitBase*)addonInfo.Addon;
        if (addonInfo.AddonName != "MJIDisposeShop" || addon is null) return;
        if (!Plugin.P.Config.AutoSell) return;

        Callback.Fire(addon, false, 13, Plugin.P.Config.AutoSellAmount);
        var subAddon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("MJIDisposeShopShippingBulk");
        if (subAddon != null)
            Callback.Fire(subAddon, true, 0);
    }
}
