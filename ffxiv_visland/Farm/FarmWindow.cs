using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using visland.Helpers;

namespace visland.Farm;

unsafe class FarmWindow : UIAttachedWindow
{
    public class Config : Configuration.Node
    {
        public bool AutoCollect = false;
    }

    private Config _config;
    private FarmDebug _debug = new();

    public FarmWindow() : base("Farm Automation", "MJIFarmManagement", new(100, 50))
    {
        _config = Service.Config.Get<Config>();
    }
    //AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MJIFarmManagement", AutoCollectFarm);
    //AddonLifecycle.UnregisterListener(AutoCollectFarm);

    public override void Draw()
    {
        if (ImGui.Checkbox("Auto Collect", ref _config.AutoCollect))
            _config.NotifyModified();
        ImGui.Separator();
        _debug.Draw();
    }

    private void AutoCollectFarm(AddonEvent eventType, AddonArgs addonInfo)
    {
        var addon = (AtkUnitBase*)addonInfo.Addon;
        if (addonInfo.AddonName != "MJIFarmManagement") return;
        if (!_config.AutoCollect) return;
        if (addon->AtkValues[195].Byte != 0) return;

        Callback.Fire(addon, false, 3);
        Utils.AutoYesNo();
    }
}
