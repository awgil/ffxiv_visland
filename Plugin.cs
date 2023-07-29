using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace visland;

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Island sanctuary automation";

    public DalamudPluginInterface Dalamud { get; init; }
    public Config Config { get; init; }

    public WindowSystem WindowSystem = new("visland");
    private GatherWindow _wndGather;
    private WorkshopWindow _wndWorkshop;

    public Plugin(DalamudPluginInterface dalamud)
    {
        dalamud.Create<Service>();

        Dalamud = dalamud;
        Config = new();
        Config.LoadFromFile(dalamud.ConfigFile);

        _wndGather = new GatherWindow(this);
        WindowSystem.AddWindow(_wndGather);
        Service.CommandManager.AddHandler("/visland", new CommandInfo((_, _) => _wndGather.IsOpen = true) { HelpMessage = "Show plugin gathering UI" });

        _wndWorkshop = new WorkshopWindow(this);
        WindowSystem.AddWindow(_wndWorkshop);

        Dalamud.UiBuilder.Draw += WindowSystem.Draw;
        Dalamud.UiBuilder.OpenConfigUi += () => _wndGather.IsOpen = true;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        Service.CommandManager.RemoveHandler("/visland");
        _wndGather.Dispose();
        _wndWorkshop.Dispose();
    }
}
