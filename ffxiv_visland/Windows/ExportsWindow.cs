using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
//using ECommons.Automation;
//using ECommons.DalamudServices;
using visland.Helpers;

namespace visland.Windows;

unsafe class ExportsWindow : UIAttachedWindow
{
    public class Config : Configuration.Node
    {
        public bool AutoSell = false;
        public int AutoSellAmount = 900;
    }

    private Config _config;

    public ExportsWindow() : base("Exports Automation", "MJIDisposeShop", new(150, 100))
    {
        _config = Service.Config.Get<Config>();
    }
    //AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MJIDisposeShop", AutoExport);
    //AddonLifecycle.UnregisterListener(AutoCollectFarm);

    public override void Draw()
    {
        if (ImGui.Checkbox("Auto Export", ref _config.AutoSell))
            _config.NotifyModified();
        if (_config.AutoSell)
        {
            ImGui.PushItemWidth(150);
            if (ImGui.SliderInt("Auto Sell Amount", ref _config.AutoSellAmount, 0, 999))
                _config.NotifyModified();
            ImGui.PopItemWidth();
        }
    }

    private void AutoExport(AddonEvent eventType, AddonArgs addonInfo)
    {
        var addon = (AtkUnitBase*)addonInfo.Addon;
        if (addonInfo.AddonName != "MJIDisposeShop" || addon is null) return;
        if (!_config.AutoSell) return;

        //Callback.Fire(addon, false, 13, _config.AutoSellAmount);
        //var subAddon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("MJIDisposeShopShippingBulk");
        //if (subAddon != null)
        //    Callback.Fire(subAddon, true, 0);
    }
}
