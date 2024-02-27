using Dalamud.Plugin.Ipc;
using System.Numerics;

namespace visland.IPC;
internal class NavmeshIPC
{
    internal static readonly string PluginName = "vnavmesh";
    internal static ICallGateSubscriber<bool>? NavIsReady;
    internal static ICallGateSubscriber<float>? NavBuildProgress;
    internal static ICallGateSubscriber<object>? NavReload;
    internal static ICallGateSubscriber<object>? NavRebuild;
    internal static ICallGateSubscriber<bool>? NavIsAutoLoad;
    internal static ICallGateSubscriber<bool, object>? NavSetAutoLoad;
    internal static ICallGateSubscriber<Vector3, object>? PathMoveTo;
    internal static ICallGateSubscriber<Vector3, object>? PathFlyTo;
    internal static ICallGateSubscriber<object>? PathStop;
    internal static ICallGateSubscriber<bool>? PathIsRunning;
    internal static ICallGateSubscriber<int>? PathNumWaypoints;
    internal static ICallGateSubscriber<bool>? PathGetMovementAllowed;
    internal static ICallGateSubscriber<bool, object>? PathSetMovementAllowed;
    internal static ICallGateSubscriber<float>? PathGetTolerance;
    internal static ICallGateSubscriber<bool, object>? PathSetTolerance;

    internal static void Init()
    {
        NavIsReady = Service.PluginInterface.GetIpcSubscriber<bool>($"{PluginName}.Nav.IsReady");
        NavBuildProgress = Service.PluginInterface.GetIpcSubscriber<float>($"{PluginName}.Nav.BuildProgress");
        NavReload = Service.PluginInterface.GetIpcSubscriber<object>($"{PluginName}.Nav.Reload");
        NavRebuild = Service.PluginInterface.GetIpcSubscriber<object>($"{PluginName}.Nav.Rebuild");
        NavIsAutoLoad = Service.PluginInterface.GetIpcSubscriber<bool>($"{PluginName}.Nav.IsAutoLoad");
        NavSetAutoLoad = Service.PluginInterface.GetIpcSubscriber<bool, object>($"{PluginName}.Nav.SetAutoLoad");
        PathMoveTo = Service.PluginInterface.GetIpcSubscriber<Vector3, object>($"{PluginName}.Path.MoveTo");
        PathFlyTo = Service.PluginInterface.GetIpcSubscriber<Vector3, object>($"{PluginName}.Path.FlyTo");
        PathStop = Service.PluginInterface.GetIpcSubscriber<object>($"{PluginName}.Path.Stop");
        PathIsRunning = Service.PluginInterface.GetIpcSubscriber<bool>($"{PluginName}.Path.IsRunning");
        PathNumWaypoints = Service.PluginInterface.GetIpcSubscriber<int>($"{PluginName}.Path.NumWaypoints");
        PathGetMovementAllowed = Service.PluginInterface.GetIpcSubscriber<bool>($"{PluginName}.Path.GetMovementAllowed");
        PathSetMovementAllowed = Service.PluginInterface.GetIpcSubscriber<bool, object>($"{PluginName}.Path.SetMovementAllowed");
        PathGetTolerance = Service.PluginInterface.GetIpcSubscriber<float>($"{PluginName}.Path.GetTolerance");
        PathSetTolerance = Service.PluginInterface.GetIpcSubscriber<bool, object>($"{PluginName}.Path.SetTolerance");
    }

    internal static void Dispose()
    {
        NavIsReady = null;
        NavBuildProgress = null;
        NavReload = null;
        NavRebuild = null;
        NavIsAutoLoad = null;
        NavSetAutoLoad = null;
        PathMoveTo = null;
        PathFlyTo = null;
        PathStop = null;
        PathIsRunning = null;
        PathNumWaypoints = null;
        PathGetMovementAllowed = null;
        PathSetMovementAllowed = null;
        PathGetTolerance = null;
        PathSetTolerance = null;
    }
}
