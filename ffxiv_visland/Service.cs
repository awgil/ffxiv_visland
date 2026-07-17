using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Linq;
using visland.Gathering;
using visland.Helpers;
using visland.IPC;

namespace visland;

public class Service {
    [PluginService] public static IDalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider Hook { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IAetheryteList AetheryteList { get; private set; } = null!;
    [PluginService] public static IPlayerState PlayerState { get; private set; } = null!;

    public static Configuration Config { get; private set; } = null!;
    public static Retainers Retainers { get; private set; } = null!;
    public static TaskManager TaskManager { get; private set; } = null!;
    public static NavmeshIPC Navmesh { get; private set; } = null!;
    public static AutoRetainerIPC AutoRetainer { get; private set; } = null!;
    public static VislandIPC Visland { get; private set; } = null!;
    public static GatherRouteExec RouteExec { get; private set; } = null!;

    public static void Init(IDalamudPluginInterface pi) {
        try {
            pi.Create<Service>();
            Config = new();
            Config.Initialize(pi.ConfigFile);
            TaskManager = new TaskManager { AbortOnTimeout = true, TimeLimitMS = 20000 };
            Navmesh = new NavmeshIPC();
            AutoRetainer = new AutoRetainerIPC();
            Retainers = new Retainers();
            RouteExec = new GatherRouteExec();
            Visland = new VislandIPC();
        }
        catch (Exception ex) {
            Log.Error(ex, $"Error initalising {nameof(Service)}");
        }
    }

    public static void Dispose() {
        RouteExec.Dispose();
        TaskManager.Dispose();
        Config.Dispose();
    }
}

public class Retainers {
    public ulong StartingCharacter;
    public bool Finished => Service.AutoRetainer.GetMultiEnabled() && !Service.AutoRetainer.IsBusy() && Player.CID == StartingCharacter && !HasRetainersReady && !HasSubsReady;

    public bool HasRetainersReady {
        get {
            foreach (var c in Service.AutoRetainer.GetRegisteredCIDs()) {
                var data = Service.AutoRetainer.GetOfflineCharacterData(c);
                if (data is not { Enabled: true }) continue;
                if (data.RetainerData.Any(x => x.HasVenture && x.VentureEndsAt <= DateTime.Now.ToUnixTimestamp()))
                    return true;
            }
            return false;
        }
    }

    public bool HasSubsReady {
        get {
            foreach (var c in Service.AutoRetainer.GetRegisteredCIDs()) {
                var data = Service.AutoRetainer.GetOfflineCharacterData(c);
                if (data is not { Enabled: true }) continue;
                if (data.OfflineSubmarineData.Any(x => data.EnabledSubs.Contains(x.Name) && x.ReturnTime <= DateTime.Now.ToUnixTimestamp()))
                    return true;
            }
            return false;
        }
    }

    public ulong GetPreferredCharacter() => Service.AutoRetainer.GetRegisteredCIDs().FirstOrDefault(c => Service.AutoRetainer.GetOfflineCharacterData(c) is { Preferred: true });
}
