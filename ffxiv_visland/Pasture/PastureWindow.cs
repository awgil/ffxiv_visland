using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using visland.Helpers;

namespace visland.Pasture;

unsafe class PastureWindow : UIAttachedWindow
{
    public class Config : Configuration.Node
    {
        public bool AutoCollect = false;
    }

    private Config _config;
    private PastureDebug _debug = new();

    public PastureWindow() : base("Pasture Automation", "MJIAnimalManagement", new(400, 600))
    {
        _config = Service.Config.Get<Config>();
    }

    public override void Draw()
    {
        if (ImGui.Checkbox("Auto Collect", ref _config.AutoCollect))
            _config.NotifyModified();
        ImGui.Separator();
        _debug.Draw();
    }

    public void AutoCollectPasture(AddonEvent eventType, AddonArgs addonInfo)
    {
        var addon = (AtkUnitBase*)addonInfo.Addon;
        if (addonInfo.AddonName != "MJIAnimalManagement") return;
        if (!_config.AutoCollect) return;
        if (addon->AtkValues[219].Byte != 0) return;

        Callback.Fire(addon, false, 5);
        Utils.AutoYesNo();
    }
}
