using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using System.Globalization;
using System.Linq;
using System.Numerics;
using visland.Export;
using visland.Farm;
using visland.Gathering;
using visland.Gathering.AutoGather;
using visland.Granary;
using visland.Helpers;
using visland.Pasture;
using visland.Workshop;

namespace visland;

public sealed class Plugin : IDalamudPlugin {
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

    private readonly AutoGatherController _autoGather;
    private readonly WindowSystem _windowSystem = new("visland");

    public unsafe Plugin(IDalamudPluginInterface dalamud) {
        var dir = dalamud.ConfigDirectory;
        if (!dir.Exists)
            dir.Create();

        Service.Init(dalamud);

        P = this;
        _windowSystem.Add(new GatherWindow(), new WorkshopWindow(), new GranaryWindow(), new PastureWindow(), new FarmWindow(), new ExportWindow());
        _autoGather = new AutoGatherController();

        Service.Interface.UiBuilder.Draw += OnDraw;
        Service.CommandManager.AddHandler("/visland", new CommandInfo(OnCommand) { HelpMessage = HelpMessage });
        Service.Interface.UiBuilder.OpenConfigUi += () => _windowSystem.Get<GatherWindow>()!.IsOpen = true;
    }

    public void Dispose() {
        Service.CommandManager.RemoveHandler("/visland");
        Service.Interface.UiBuilder.Draw -= OnDraw;
        _autoGather.Dispose();
        _windowSystem.Dispose();
        Service.Dispose();
    }

    private void OnDraw() => _windowSystem.Draw();

    private void OnCommand(string command, string arguments) {
        Service.Log.Debug($"cmd: '{command}', args: '{arguments}'");
        if (arguments.Length == 0)
            _windowSystem.Get<GatherWindow>()!.IsOpen ^= true;
        else {
            var args = arguments.Split(' ');
            switch (args[0]) {
                case "moveto":
                    if (args.Length > 3)
                        MoveToCommand(args, false);
                    break;
                case "movedir":
                    if (args.Length > 3)
                        MoveToCommand(args, true);
                    break;
                case "stop":
                    Service.RouteExec.Finish();
                    break;
                case "pause":
                    Service.RouteExec.Paused = true;
                    break;
                case "resume":
                    Service.RouteExec.Paused = false;
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
            }
        }
    }

    internal void ExecuteTempRoute(string base64, bool once) {
        (var _, var json) = Utils.FromCompressedBase64(base64);
        var route = Newtonsoft.Json.JsonConvert.DeserializeObject<GatherRouteDB.Route>(json);
        if (route != null)
            Service.RouteExec.Start(route, 0, true, !once);
        else
            Service.Log.Warning($"Failed to deserialize route from clipboard: {base64}");
    }

    internal void MoveToCommand(string[] args, bool relativeToPlayer) {
        var originActor = relativeToPlayer ? Service.ObjectTable.LocalPlayer : null;
        var origin = originActor?.Position ?? new();
        var offset = new Vector3(float.Parse(args[1], CultureInfo.InvariantCulture), float.Parse(args[2], CultureInfo.InvariantCulture), float.Parse(args[3], CultureInfo.InvariantCulture));
        var route = new GatherRouteDB.Route { Name = "Temporary", Waypoints = [] };
        route.Waypoints.Add(new() { Position = origin + offset, Radius = 0.5f, InteractWithName = "", InteractWithOID = 0 });
        Service.RouteExec.Start(route, 0, false, false);
    }

    internal void ExecuteCommand(string name, bool once) {
        var route = Service.RouteExec.RouteDB.Routes.Find(r => r.Name == name);
        if (route != null)
            Service.RouteExec.Start(route, 0, true, !once, route.Waypoints.ElementAt(0).Pathfind);
    }
}
