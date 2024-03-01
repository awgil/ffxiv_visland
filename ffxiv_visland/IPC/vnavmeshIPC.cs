using Dalamud.Plugin.Ipc;
using ECommons;
using System;
using System.Numerics;
using visland.Helpers;

namespace visland.IPC;
internal class NavmeshIPC
{
    internal static readonly string Name = "vnavmesh";
    private static ICallGateSubscriber<bool>? _navIsReady;
    private static ICallGateSubscriber<float>? _navBuildProgress;
    private static ICallGateSubscriber<object>? _navReload;
    private static ICallGateSubscriber<object>? _navRebuild;
    private static ICallGateSubscriber<bool>? _navIsAutoLoad;
    private static ICallGateSubscriber<bool, object>? _navSetAutoLoad;

    private static ICallGateSubscriber<Vector3, float, Vector3?>? _queryMeshNearestPoint;

    private static ICallGateSubscriber<Vector3, object>? _pathMoveTo;
    private static ICallGateSubscriber<Vector3, object>? _pathFlyTo;
    private static ICallGateSubscriber<object>? _pathStop;
    private static ICallGateSubscriber<bool>? _pathIsRunning;
    private static ICallGateSubscriber<int>? _pathNumWaypoints;
    private static ICallGateSubscriber<bool>? _pathGetMovementAllowed;
    private static ICallGateSubscriber<bool, object>? _pathSetMovementAllowed;
    private static ICallGateSubscriber<bool>? _pathGetAlignCamera;
    private static ICallGateSubscriber<bool, object>? _pathSetAlignCamera;
    private static ICallGateSubscriber<float>? _pathGetTolerance;
    private static ICallGateSubscriber<float, object>? _pathSetTolerance;

    internal static void Init()
    {
        if (Utils.HasPlugin($"{Name}"))
        {
            _navIsReady = Service.PluginInterface.GetIpcSubscriber<bool>($"{Name}.Nav.IsReady");
            _navBuildProgress = Service.PluginInterface.GetIpcSubscriber<float>($"{Name}.Nav.BuildProgress");
            _navReload = Service.PluginInterface.GetIpcSubscriber<object>($"{Name}.Nav.Reload");
            _navRebuild = Service.PluginInterface.GetIpcSubscriber<object>($"{Name}.Nav.Rebuild");
            _navIsAutoLoad = Service.PluginInterface.GetIpcSubscriber<bool>($"{Name}.Nav.IsAutoLoad");
            _navSetAutoLoad = Service.PluginInterface.GetIpcSubscriber<bool, object>($"{Name}.Nav.SetAutoLoad");

            _queryMeshNearestPoint = Service.PluginInterface.GetIpcSubscriber<Vector3, float, Vector3?>($"{Name}.Query.Mesh.NearestPoint");

            _pathMoveTo = Service.PluginInterface.GetIpcSubscriber<Vector3, object>($"{Name}.Path.MoveTo");
            _pathFlyTo = Service.PluginInterface.GetIpcSubscriber<Vector3, object>($"{Name}.Path.FlyTo");
            _pathStop = Service.PluginInterface.GetIpcSubscriber<object>($"{Name}.Path.Stop");
            _pathIsRunning = Service.PluginInterface.GetIpcSubscriber<bool>($"{Name}.Path.IsRunning");
            _pathNumWaypoints = Service.PluginInterface.GetIpcSubscriber<int>($"{Name}.Path.NumWaypoints");
            _pathGetMovementAllowed = Service.PluginInterface.GetIpcSubscriber<bool>($"{Name}.Path.GetMovementAllowed");
            _pathSetMovementAllowed = Service.PluginInterface.GetIpcSubscriber<bool, object>($"{Name}.Path.SetMovementAllowed");
            _pathGetAlignCamera = Service.PluginInterface.GetIpcSubscriber<bool>($"{Name}.Path.GetAlignCamera");
            _pathSetAlignCamera = Service.PluginInterface.GetIpcSubscriber<bool, object>($"{Name}.Path.SetAlignCamera");
            _pathGetTolerance = Service.PluginInterface.GetIpcSubscriber<float>($"{Name}.Path.GetTolerance");
            _pathSetTolerance = Service.PluginInterface.GetIpcSubscriber<float, object>($"{Name}.Path.SetTolerance");
        }
    }

    internal static T? Execute<T>(Func<T> func)
    {
        if (Utils.HasPlugin($"{Name}"))
        {
            try
            {
                return func();
            }
            catch (Exception ex) { ex.Log(); }
        }
        return default;
    }

    internal static void Execute<T>(Action<T> action, T param)
    {
        if (Utils.HasPlugin($"{Name}"))
        {
            try
            {
                action(param);
            }
            catch (Exception ex) { ex.Log(); }
        }
    }

    internal static void Execute(Action action)
    {
        if (Utils.HasPlugin($"{Name}"))
        {
            try
            {
                action();
            }
            catch (Exception ex) { ex.Log(); }
        }
    }

    internal static bool NavIsReady() => Execute(() => _navIsReady!.InvokeFunc());
    internal static void PathMoveTo(Vector3 pos) => Execute(_pathMoveTo!.InvokeAction, pos);
    internal static void PathFlyTo(Vector3 pos) => Execute(_pathFlyTo!.InvokeAction, pos);
    internal static void PathStop() => Execute(_pathStop!.InvokeAction);
    internal static bool PathIsRunning() => Execute(() => _pathIsRunning!.InvokeFunc());
    internal static float PathGetTolerance() => Execute(() => _pathGetTolerance!.InvokeFunc());
    internal static void PathSetTolerance(float tolerance) => Execute(_pathSetTolerance!.InvokeAction, tolerance);
}
