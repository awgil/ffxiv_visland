using Dalamud.Interface.Utility.Raii;
using visland.Helpers;
using ImGuiNET;

namespace visland.Workshop;

unsafe class WorkshopWindow : UIAttachedWindow
{
    private WorkshopConfig _config;
    private WorkshopFavors _favors = new();
    private WorkshopSchedule _sched = new();
    private WorkshopManual _manual;
    private WorkshopOCImport _oc;
    private WorkshopDebug _debug;

    public WorkshopWindow() : base("Workshop automation", "MJICraftSchedule", new(500, 650))
    {
        _config = Service.Config.Get<WorkshopConfig>();
        _manual = new(_sched);
        _oc = new(_favors, _sched);
        _debug = new(_sched);
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
            using (var tab = ImRaii.TabItem("Settings"))
                if (tab)
                    DrawSettings();
            using (var tab = ImRaii.TabItem("Debug"))
                if (tab)
                    _debug.Draw();
        }
    }

    public override void OnOpen()
    {
        if (_config.AutoOpenNextDay)
        {
            _sched.SetCurrentCycle(_sched.CycleInProgress + 1);
        }
        if (_config.AutoImport)
        {
            _oc.ImportRecsFromClipboard(true);
        }
    }

    private void DrawSettings()
    {
        if (ImGui.Checkbox("Automatically select next cycle on open", ref _config.AutoOpenNextDay))
            _config.NotifyModified();
        if (ImGui.Checkbox("Automatically import base recs on open", ref _config.AutoImport))
            _config.NotifyModified();
    }
}
