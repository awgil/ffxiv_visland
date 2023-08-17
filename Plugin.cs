using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using System.Numerics;

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
        Service.CommandManager.AddHandler("/visland", new CommandInfo(OnCommand) { HelpMessage = "Show plugin gathering UI" });

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

    private void OnCommand(string command, string arguments)
    {
        PluginLog.Debug($"cmd: '{command}', args: '{arguments}'");
        if (arguments.Length == 0)
        {
            _wndGather.IsOpen = true;
        }
        else
        {
            var args = arguments.Split(' ');
            switch (args[0])
            {
                case "moveto":
                    if (args.Length > 3)
                        MoveToCommand(args, false);
                    break;
                case "movedir":
                    if (args.Length > 3)
                        MoveToCommand(args, true);
                    break;
                case "stop":
                    _wndGather.Exec.Finish();
                    break;
                case "pause":
                    _wndGather.Exec.Paused = true;
                    break;
                case "resume":
                    _wndGather.Exec.Paused = false;
                    break;
            }
        }
    }

    private void MoveToCommand(string[] args, bool relativeToPlayer)
    {
        var originActor = relativeToPlayer ? Service.ClientState.LocalPlayer : null;
        var origin = originActor?.Position ?? new();
        var offset = new Vector3(float.Parse(args[1]), float.Parse(args[2]), float.Parse(args[3]));
        var route = new GatherRouteDB.Route { Name = "Temporary", Waypoints = new() };
        route.Waypoints.Add(new() { Position = origin + offset, Radius = 0.5f, Mount = false, InteractWithName = "", InteractWithOID = 0 });
        _wndGather.Exec.Start(route, 0, false, false);
    }
}
