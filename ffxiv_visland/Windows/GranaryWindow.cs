using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using visland.Helpers;

namespace visland.Windows;

unsafe class GranaryWindow : UIAttachedWindow
{
    public class Config : Configuration.Node
    {
        public bool AutoCollect = false;
        public bool AutoMax = false;
    }

    private TaskManager _taskManager = new();
    private Config _config;

    public GranaryWindow() : base("Granary Automation", "MJIGatheringHouse", new(250, 50))
    {
        _config = Service.Config.Get<Config>();
    }

    public override void Draw()
    {
        if (ImGui.Checkbox("Auto Collect", ref _config.AutoCollect))
            _config.NotifyModified();
        if (ImGui.Checkbox("Auto Max", ref _config.AutoMax))
            _config.NotifyModified();
    }

    public void AutoCollectGranary(AddonEvent eventType, AddonArgs addonInfo)
    {
        var addon = (AtkUnitBase*)addonInfo.Addon;
        if (addonInfo.AddonName != "MJIGatheringHouse") return;
        if (!_config.AutoCollect) return;

        if (addon->AtkValues[73].Byte != 0)
        {
            _taskManager.Enqueue(() => { ECommons.GenericHelpers.TryGetAddonByName<AtkUnitBase>("MJIGatheringHouse", out var granary1); Callback.Fire(granary1, false, 13, 0); }, "CollectGranary1");
            _taskManager.DelayNext(200);
            _taskManager.Enqueue(() => Utils.AutoYesNo());
        }
        if (addon->AtkValues[147].Byte != 0)
        {
            _taskManager.Enqueue(() => { ECommons.GenericHelpers.TryGetAddonByName<AtkUnitBase>("MJIGatheringHouse", out var granary2); Callback.Fire(granary2, false, 13, 1); }, "CollectGranary2");
            _taskManager.DelayNext(200);
            _taskManager.Enqueue(() => Utils.AutoYesNo());
        }
    }
}
