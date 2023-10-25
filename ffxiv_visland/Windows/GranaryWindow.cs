using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using static ECommons.GenericHelpers;
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
    //AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MJIGatheringHouse", AutoCollectGranary);
    //AddonLifecycle.UnregisterListener(AutoCollectGranary);

    public override void Draw()
    {
        if (ImGui.Checkbox("Auto Collect", ref _config.AutoCollect))
            _config.NotifyModified();
        if (ImGui.Checkbox("Auto Max", ref _config.AutoMax))
            _config.NotifyModified();
    }

    private void AutoCollectGranary(AddonEvent eventType, AddonArgs addonInfo)
    {
        var addon = (AtkUnitBase*)addonInfo.Addon;
        if (addonInfo.AddonName != "MJIGatheringHouse") return;
        if (!_config.AutoCollect) return;

        if (addon->AtkValues[73].Byte != 0)
        {
            _taskManager.Enqueue(() => { TryGetAddonByName<AtkUnitBase>("MJIGatheringHouse", out var granary1); Callback.Fire(granary1, false, 13, 0); }, "CollectGranary1");
            _taskManager.DelayNext(200);
            _taskManager.Enqueue(() => AutoYesNo());
        }
        if (addon->AtkValues[147].Byte != 0)
        {
            _taskManager.Enqueue(() => { TryGetAddonByName<AtkUnitBase>("MJIGatheringHouse", out var granary2); Callback.Fire(granary2, false, 13, 1); }, "CollectGranary2");
            _taskManager.DelayNext(200);
            _taskManager.Enqueue(() => AutoYesNo());
        }
    }

    private void AutoYesNo()
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectYesno");
        if (addon != null && addon->IsVisible && addon->UldManager.NodeList[15]->IsVisible)
            Callback.Fire(addon, true, 0);
    }
}
