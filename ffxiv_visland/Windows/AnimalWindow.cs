using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using visland.Helpers;

namespace visland.Windows;

unsafe class AnimalWindow : UIAttachedWindow
{
    public class Config : Configuration.Node
    {
        public bool AutoCollect = false;
    }

    private Config _config;

    public AnimalWindow() : base("Pasture Automation", "MJIAnimalManagement", new(100, 50))
    {
        _config = Service.Config.Get<Config>();
    }
    //AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MJIAnimalManagement", AutoCollectPasture);
    //AddonLifecycle.UnregisterListener(AutoCollectPasture);

    public override void Draw()
    {
        if (ImGui.Checkbox("Auto Collect", ref _config.AutoCollect))
            _config.NotifyModified();
    }

    private void AutoCollectPasture(AddonEvent eventType, AddonArgs addonInfo)
    {
        var addon = (AtkUnitBase*)addonInfo.Addon;
        if (addonInfo.AddonName != "MJIAnimalManagement") return;
        if (!_config.AutoCollect) return;
        if (addon->AtkValues[219].Byte != 0) return;

        Callback.Fire(addon, false, 5);
        Utils.AutoYesNo();
    }
}
