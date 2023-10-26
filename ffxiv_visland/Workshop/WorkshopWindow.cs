using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Interface.Utility.Raii;
using visland.Helpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons.Automation;
using System;

namespace visland.Workshop;

unsafe class WorkshopWindow : UIAttachedWindow
{
    private WorkshopManual _manual = new();
    private WorkshopOCImport _oc = new();
    private WorkshopDebug _debug = new();
    private WorkshopSchedule _sched = new();
    private WorkshopSettings _settings = new();
    private readonly TaskManager _taskManager = new();

    public WorkshopWindow() : base("Workshop automation", "MJICraftSchedule", new(500, 650))
    {
    }

    public override void Draw()
    {
        using var tabs = ImRaii.TabBar("Tabs");
        if (tabs)
        {
            using (var tab = ImRaii.TabItem("OC import"))
                if (tab)
                    _oc.Draw();
            using (var tab = ImRaii.TabItem("Manual schedule"))
                if (tab)
                    _manual.Draw();
            using (var tab = ImRaii.TabItem("Debug"))
                if (tab)
                    _debug.Draw();
            //using (var tab = ImRaii.TabItem("Settings"))
            //    if (tab)
            //        _settings.Draw();
        }
    }

    public void OnWorkshopSetup(AddonEvent eventType, AddonArgs addonInfo)
    {
        if (addonInfo.AddonName != "MJICraftSchedule") return;
        var addon = (AtkUnitBase*)addonInfo.Addon;
        //TaskManager.Enqueue(() => IsWindowReady(addon), $"WaitingFor{addonInfo.AddonName}");
        //TaskManager.Enqueue(() =>
        //{
        //    if (_settings._config.AutoOpenNextDay)
        //    {
        //        _sched.SetCurrentCycle(_sched.CycleInProgress + 2);
        //    }
        //    if (_settings._config.AutoImport)
        //    {
        //        _sched.SetCurrentCycle(_sched.CycleInProgress + 2);
        //    }
        //}, $"{nameof(OnWorkshopSetup)}");
    }

    private static bool IsWindowReady(AtkUnitBase* addon) => addon->AtkValues[0].Type != 0;
}
