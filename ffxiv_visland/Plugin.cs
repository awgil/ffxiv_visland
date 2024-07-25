using Dalamud.Common;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation.LegacyTaskManager;
using ECommons.Configuration;
using ECommons.Reflection;
using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using visland.Export;
using visland.Farm;
using visland.Gathering;
using visland.Granary;
using visland.Helpers;
using visland.IPC;
using visland.Pasture;
using visland.Workshop;

namespace visland;

public sealed class Plugin : IDalamudPlugin
{
    public static string Name => "visland";
    public static string Repo => "https://puni.sh/api/repository/veyn";
    internal static string HelpMessage => "Opens the Gathering Menu\n" +
        $"/{Name} moveto <X> <Y> <Z> → move to raw coordinates\n" +
        $"/{Name} movedir <X> <Y> <Z> → move this many units over (relative to player facing)\n" +
        $"/{Name} stop → stop current route\n" +
        $"/{Name} pause → pause current route\n" +
        $"/{Name} resume → resume current route\n" +
        $"/{Name} exec <name> → run route by name continuously\n" +
        $"/{Name} execonce <name> → run route by name once\n" +
        $"/{Name} exectemp <base64 route> → run unsaved route continuously\n" +
        $"/{Name} exectemponce <base64 route> → run unsaved route once";

    internal static Plugin P = null!;
    internal TaskManager TaskManager;
    internal ShoppingListDB ShoppingListsFile;

    private VislandIPC _vislandIPC;

    public WindowSystem WindowSystem = new("visland");
    private GatherWindow _wndGather;
    private WorkshopWindow _wndWorkshop;
    private GranaryWindow _wndGranary;
    private PastureWindow _wndPasture;
    private FarmWindow _wndFarm;
    private ExportWindow _wndExports;

    public unsafe Plugin(IDalamudPluginInterface dalamud)
    {
        var dir = dalamud.ConfigDirectory;
        if (!dir.Exists)
            dir.Create();
        var dalamudRoot = dalamud.GetType().Assembly.
                GetType("Dalamud.Service`1", true)!.MakeGenericType(dalamud.GetType().Assembly.GetType("Dalamud.Dalamud", true)!).
                GetMethod("Get")!.Invoke(null, BindingFlags.Default, null, Array.Empty<object>(), null);
        var dalamudStartInfo = dalamudRoot.GetFoP<DalamudStartInfo>("StartInfo");

        ECommonsMain.Init(dalamud, this, ECommons.Module.DalamudReflector);
        DalamudReflector.RegisterOnInstalledPluginsChangedEvents(CheckIPC);
        Service.Init(dalamud);

        dalamud.Create<Service>();

        Service.Config.Initialize();
        if (dalamud.ConfigFile.Exists)
            Service.Config.LoadFromFile(dalamud.ConfigFile);
        Service.Config.Modified += (_, _) => Service.Config.SaveToFile(dalamud.ConfigFile);

        P = this;
        TaskManager = new() { AbortOnTimeout = true, TimeLimitMS = 20000 };

        //EzConfig.DefaultSerializationFactory = new YamlFactory();
        //ShoppingListsFile = EzConfig.Init<ShoppingListDB>();
        //ShoppingListsFile.ShoppingLists.CollectionChanged += OnChange;

        _wndGather = new GatherWindow();
        //EzConfigGui.Init(_wndGather.Draw);
        //_wndGather.Setup();

        //EzConfigGui.WindowSystem.AddWindow(new WorkshopWindow());
        //EzConfigGui.WindowSystem.AddWindow(new GranaryWindow());
        //EzConfigGui.WindowSystem.AddWindow(new PastureWindow());
        //EzConfigGui.WindowSystem.AddWindow(new FarmWindow());
        //EzConfigGui.WindowSystem.AddWindow(new ExportWindow());
        _wndWorkshop = new WorkshopWindow();
        _wndGranary = new GranaryWindow();
        _wndPasture = new PastureWindow();
        _wndFarm = new FarmWindow();
        _wndExports = new ExportWindow();

        _vislandIPC = new(_wndGather);
        NavmeshIPC.Init();

        WindowSystem.AddWindow(_wndGather);
        WindowSystem.AddWindow(_wndWorkshop);
        WindowSystem.AddWindow(_wndGranary);
        WindowSystem.AddWindow(_wndPasture);
        WindowSystem.AddWindow(_wndFarm);
        WindowSystem.AddWindow(_wndExports);
        EzCmd.Add("/visland", OnCommand, HelpMessage);
        Service.Interface.UiBuilder.Draw += WindowSystem.Draw;
        Service.Interface.UiBuilder.OpenConfigUi += () => _wndGather.IsOpen = true;
    }

    public void Dispose()
    {
        _vislandIPC.Dispose();
        WindowSystem.RemoveAllWindows();
        _wndGather.Dispose();
        _wndWorkshop.Dispose();
        _wndGranary.Dispose();
        _wndPasture.Dispose();
        _wndFarm.Dispose();
        _wndExports.Dispose();
        //ShoppingListsFile.ShoppingLists.CollectionChanged -= OnChange;
        ECommonsMain.Dispose();
    }

    private void OnCommand(string command, string arguments)
    {
        Service.Log.Debug($"cmd: '{command}', args: '{arguments}'");
        if (arguments.Length == 0)
            _wndGather.IsOpen ^= true;
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
                case "exectemp":
                    ExecuteTempRoute(args[1], false);
                    break;
                case "exectemponce":
                    ExecuteTempRoute(args[1], true);
                    break;
                case "gather":
                    if (args.Length > 2)
                        TryGather(args);
                    break;
            }
        }
    }

    private void TryGather(string[] args)
    {
        throw new NotImplementedException();
    }

    private void ExecuteTempRoute(string base64, bool once)
    {
        var (IsBase64, Json) = Utils.FromCompressedBase64(base64);
        var route = Newtonsoft.Json.JsonConvert.DeserializeObject<GatherRouteDB.Route>(Json);
        if (route != null)
            _wndGather.Exec.Start(route, 0, true, !once);
    }

    private void MoveToCommand(string[] args, bool relativeToPlayer)
    {
        var originActor = relativeToPlayer ? Service.ClientState.LocalPlayer : null;
        var origin = originActor?.Position ?? new();
        var offset = new Vector3(float.Parse(args[1], CultureInfo.InvariantCulture), float.Parse(args[2], CultureInfo.InvariantCulture), float.Parse(args[3], CultureInfo.InvariantCulture));
        var route = new GatherRouteDB.Route { Name = "Temporary", Waypoints = [] };
        route.Waypoints.Add(new() { Position = origin + offset, Radius = 0.5f, InteractWithName = "", InteractWithOID = 0 });
        _wndGather.Exec.Start(route, 0, false, false);
    }

    private void ExecuteCommand(string name, bool once)
    {
        var route = _wndGather.RouteDB.Routes.Find(r => r.Name == name);
        if (route != null)
            _wndGather.Exec.Start(route, 0, true, !once, route.Waypoints.ElementAt(0).Pathfind);
    }

    private void CheckIPC()
    {
        if (Utils.HasPlugin(BossModIPC.Name))
            BossModIPC.Init();
        if (Utils.HasPlugin(NavmeshIPC.Name))
            NavmeshIPC.Init();
    }

    public static void OnChange(object? sender, NotifyCollectionChangedEventArgs e) => EzConfig.Save();
}