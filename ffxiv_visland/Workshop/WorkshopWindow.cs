using Dalamud.Interface.Utility.Raii;
using visland.Helpers;

namespace visland.Workshop;

unsafe class WorkshopWindow : UIAttachedWindow
{
    private WorkshopManual _manual = new();
    private WorkshopOCImport _oc = new();
    private WorkshopDebug _debug = new();

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
        }
    }
}
