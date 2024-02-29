using Dalamud.Plugin.Ipc;
using System.Numerics;
using visland.Helpers;

namespace visland.IPC;
internal class NavmeshIPC
{
    internal static readonly string Name = "vnavmesh";
    internal static ICallGateSubscriber<bool>? NavIsReady;
    internal static ICallGateSubscriber<float>? NavBuildProgress;
    internal static ICallGateSubscriber<object>? NavReload;
    internal static ICallGateSubscriber<object>? NavRebuild;
    internal static ICallGateSubscriber<bool>? NavIsAutoLoad;
    internal static ICallGateSubscriber<bool, object>? NavSetAutoLoad;

    internal static ICallGateSubscriber<Vector3, float, Vector3?>? QueryMeshNearestPoint;

    internal static ICallGateSubscriber<Vector3, object>? PathMoveTo;
    internal static ICallGateSubscriber<Vector3, object>? PathFlyTo;
    internal static ICallGateSubscriber<object>? PathStop;
    internal static ICallGateSubscriber<bool>? PathIsRunning;
    internal static ICallGateSubscriber<int>? PathNumWaypoints;
    internal static ICallGateSubscriber<bool>? PathGetMovementAllowed;
    internal static ICallGateSubscriber<bool, object>? PathSetMovementAllowed;
    internal static ICallGateSubscriber<bool>? PathGetAlignCamera;
    internal static ICallGateSubscriber<bool, object>? PathSetAlignCamera;
    internal static ICallGateSubscriber<float>? PathGetTolerance;
    internal static ICallGateSubscriber<bool, object>? PathSetTolerance;

    internal static void Init()
    {
        if (Utils.HasPlugin($"{Name}"))
        {
            NavIsReady = Service.PluginInterface.GetIpcSubscriber<bool>($"{Name}.Nav.IsReady");
            NavBuildProgress = Service.PluginInterface.GetIpcSubscriber<float>($"{Name}.Nav.BuildProgress");
            NavReload = Service.PluginInterface.GetIpcSubscriber<object>($"{Name}.Nav.Reload");
            NavRebuild = Service.PluginInterface.GetIpcSubscriber<object>($"{Name}.Nav.Rebuild");
            NavIsAutoLoad = Service.PluginInterface.GetIpcSubscriber<bool>($"{Name}.Nav.IsAutoLoad");
            NavSetAutoLoad = Service.PluginInterface.GetIpcSubscriber<bool, object>($"{Name}.Nav.SetAutoLoad");

            QueryMeshNearestPoint = Service.PluginInterface.GetIpcSubscriber<Vector3, float, Vector3?>($"{Name}.Query.Mesh.NearestPoint");

            PathMoveTo = Service.PluginInterface.GetIpcSubscriber<Vector3, object>($"{Name}.Path.MoveTo");
            PathFlyTo = Service.PluginInterface.GetIpcSubscriber<Vector3, object>($"{Name}.Path.FlyTo");
            PathStop = Service.PluginInterface.GetIpcSubscriber<object>($"{Name}.Path.Stop");
            PathIsRunning = Service.PluginInterface.GetIpcSubscriber<bool>($"{Name}.Path.IsRunning");
            PathNumWaypoints = Service.PluginInterface.GetIpcSubscriber<int>($"{Name}.Path.NumWaypoints");
            PathGetMovementAllowed = Service.PluginInterface.GetIpcSubscriber<bool>($"{Name}.Path.GetMovementAllowed");
            PathSetMovementAllowed = Service.PluginInterface.GetIpcSubscriber<bool, object>($"{Name}.Path.SetMovementAllowed");
            PathGetAlignCamera = Service.PluginInterface.GetIpcSubscriber<bool>($"{Name}.Path.GetAlignCamera");
            PathSetAlignCamera = Service.PluginInterface.GetIpcSubscriber<bool, object>($"{Name}.Path.SetAlignCamera");
            PathGetTolerance = Service.PluginInterface.GetIpcSubscriber<float>($"{Name}.Path.GetTolerance");
            PathSetTolerance = Service.PluginInterface.GetIpcSubscriber<bool, object>($"{Name}.Path.SetTolerance");
        }
    }

    internal static void Dispose()
    {
        if (Utils.HasPlugin($"{Name}"))
        {
            NavIsReady = null;
            NavBuildProgress = null;
            NavReload = null;
            NavRebuild = null;
            NavIsAutoLoad = null;
            NavSetAutoLoad = null;

            QueryMeshNearestPoint = null;

            PathMoveTo = null;
            PathFlyTo = null;
            PathStop = null;
            PathIsRunning = null;
            PathNumWaypoints = null;
            PathGetMovementAllowed = null;
            PathSetMovementAllowed = null;
            PathGetAlignCamera = null;
            PathSetAlignCamera = null;
            PathGetTolerance = null;
            PathSetTolerance = null;
        }
    }
}
