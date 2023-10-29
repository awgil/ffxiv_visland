using Dalamud.Common;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Reflection;
using ImGuiNET;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using visland.Farm;
using visland.Gathering;
using visland.Granary;
using visland.Pasture;
using visland.Windows;
using visland.Workshop;

namespace visland;

class RepoMigrateWindow : Window
{
    public static string OldURL = "https://raw.githubusercontent.com/awgil/ffxiv_plugin_distribution/master/pluginmaster.json";
    public static string NewURL = "https://puni.sh/api/repository/veyn";

    public RepoMigrateWindow() : base("Warning! Plugin home repository was changed")
    {
        IsOpen = true;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("The home repository of Island Sanctuary Automation (visland) plugin was recently changed.");
        ImGui.TextUnformatted("Please update your dalamud settings to point to the new repository:");
        if (ImGui.Button("Click here to copy new url into clipboard"))
            ImGui.SetClipboardText(NewURL);
        ImGui.TextUnformatted("1. Go to repo settings (esc -> dalamud settings -> experimental).");
        ImGui.TextUnformatted($"2. Replace '{OldURL}' with '{NewURL}' (use button above and just ctrl-V).");
        ImGui.TextUnformatted("3. Press save-and-close button.");
        ImGui.TextUnformatted("4. Go to dalamud plugins (esc -> dalamud plugins -> installed plugins).");
        ImGui.TextUnformatted("5. Uninstall and reinstall this plugin (you might need to restart the game before dalamud allows you to reinstall).");
        ImGui.TextUnformatted("Don't worry, you won't lose any settings. Sorry for bother and enjoy the plugin!");
    }
}

public sealed class Plugin : IDalamudPlugin
{
    public string Name => "Island sanctuary automation";

    public DalamudPluginInterface Dalamud { get; init; }

    public WindowSystem WindowSystem = new("visland");
    private GatherWindow _wndGather;
    private WorkshopWindow _wndWorkshop;
    private GranaryWindow _wndGranary;
    private PastureWindow _wndPasture;
    private FarmWindow _wndFarm;
    private ExportsWindow _wndExports;

    public unsafe Plugin(DalamudPluginInterface dalamud)
    {
        var dir = dalamud.ConfigDirectory;
        if (!dir.Exists)
            dir.Create();
        var dalamudRoot = dalamud.GetType().Assembly.
                GetType("Dalamud.Service`1", true)!.MakeGenericType(dalamud.GetType().Assembly.GetType("Dalamud.Dalamud", true)!).
                GetMethod("Get")!.Invoke(null, BindingFlags.Default, null, Array.Empty<object>(), null);
        var dalamudStartInfo = dalamudRoot.GetFoP<DalamudStartInfo>("StartInfo");
        FFXIVClientStructs.Interop.Resolver.GetInstance.SetupSearchSpace(0, new(Path.Combine(dalamud.ConfigDirectory.FullName, $"{dalamudStartInfo.GameVersion}_cs.json")));
        FFXIVClientStructs.Interop.Resolver.GetInstance.Resolve();

        ECommonsMain.Init(dalamud, this);

        dalamud.Create<Service>();
        dalamud.UiBuilder.Draw += WindowSystem.Draw;

        Service.Config.Initialize();
        if (dalamud.ConfigFile.Exists)
            Service.Config.LoadFromFile(dalamud.ConfigFile);
        Service.Config.Modified += (_, _) => Service.Config.SaveToFile(dalamud.ConfigFile);

        Dalamud = dalamud;

        _wndGather = new GatherWindow();
        _wndWorkshop = new WorkshopWindow();
        _wndGranary = new GranaryWindow();
        _wndPasture = new PastureWindow();
        _wndFarm = new FarmWindow();
        _wndExports = new ExportsWindow();

        if (dalamud.SourceRepository == RepoMigrateWindow.OldURL)
        {
            WindowSystem.AddWindow(new RepoMigrateWindow());
        }
        else
        {
            WindowSystem.AddWindow(_wndGather);
            WindowSystem.AddWindow(_wndWorkshop);
            WindowSystem.AddWindow(_wndGranary);
            WindowSystem.AddWindow(_wndPasture);
            WindowSystem.AddWindow(_wndFarm);
            //WindowSystem.AddWindow(_wndExports);
            Service.CommandManager.AddHandler("/visland", new CommandInfo(OnCommand) { HelpMessage = "Show plugin gathering UI" });
            Dalamud.UiBuilder.OpenConfigUi += () => _wndGather.IsOpen = true;
        }

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "MJIDisposeShop", _wndExports.AutoExport);
    }

    public void Dispose()
    {
        Service.AddonLifecycle.UnregisterListener(_wndExports.AutoExport);

        WindowSystem.RemoveAllWindows();
        Service.CommandManager.RemoveHandler("/visland");
        _wndGather.Dispose();
        _wndWorkshop.Dispose();
        _wndGranary.Dispose();
        _wndPasture.Dispose();
        _wndFarm.Dispose();
        _wndExports.Dispose();
    }

    private void OnCommand(string command, string arguments)
    {
        Service.Log.Debug($"cmd: '{command}', args: '{arguments}'");
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
                case "exec":
                    ExecuteCommand(string.Join(" ", args.Skip(1)), false);
                    break;
                case "execonce":
                    ExecuteCommand(string.Join(" ", args.Skip(1)), true);
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
        route.Waypoints.Add(new() { Position = origin + offset, Radius = 0.5f, InteractWithName = "", InteractWithOID = 0 });
        _wndGather.Exec.Start(route, 0, false, false);
    }

    private void ExecuteCommand(string name, bool once)
    {
        var route = _wndGather.RouteDB.Routes.Find(r => r.Name == name);
        if (route != null)
            _wndGather.Exec.Start(route, 0, true, !once);
    }
}
