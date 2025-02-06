using Dalamud.Plugin.Ipc;
using ECommons;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using visland.Helpers;

namespace visland.IPC;
internal class NavmeshIPC
{
    internal static readonly string Name = "vnavmesh";
    internal static bool IsEnabled => Utils.HasPlugin(Name);
    // nav
    private static ICallGateSubscriber<bool>? _navIsReady;
    private static ICallGateSubscriber<float>? _navBuildProgress;
    private static ICallGateSubscriber<bool>? _navReload;
    private static ICallGateSubscriber<bool>? _navRebuild;
    private static ICallGateSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>>? _navPathfind;
    private static ICallGateSubscriber<bool>? _navIsAutoLoad;
    private static ICallGateSubscriber<bool, object>? _navSetAutoLoad;

    // query
    private static ICallGateSubscriber<Vector3, float, float, Vector3?>? _queryMeshNearestPoint;
    private static ICallGateSubscriber<Vector3, bool, float, Vector3?>? _queryMeshPointOnFloor;
    private static ICallGateSubscriber<Vector3, float, float, float, Vector3?>? _queryMeshFurthestPoint;

    // path
    private static ICallGateSubscriber<List<Vector3>, bool, object>? _pathMoveTo;
    private static ICallGateSubscriber<object>? _pathStop;
    private static ICallGateSubscriber<bool>? _pathIsRunning;
    private static ICallGateSubscriber<int>? _pathNumWaypoints;
    private static ICallGateSubscriber<bool>? _pathGetMovementAllowed;
    private static ICallGateSubscriber<bool, object>? _pathSetMovementAllowed;
    private static ICallGateSubscriber<bool>? _pathGetAlignCamera;
    private static ICallGateSubscriber<bool, object>? _pathSetAlignCamera;
    private static ICallGateSubscriber<float>? _pathGetTolerance;
    private static ICallGateSubscriber<float, object>? _pathSetTolerance;

    // simplemove
    private static ICallGateSubscriber<Vector3, bool, bool>? _pathfindAndMoveTo;
    private static ICallGateSubscriber<bool>? _pathfindInProgress;

    internal static void Init()
    {
        if (Utils.HasPlugin(Name))
        {
            try
            {
                _navIsReady = Service.Interface.GetIpcSubscriber<bool>($"{Name}.Nav.IsReady");
                _navBuildProgress = Service.Interface.GetIpcSubscriber<float>($"{Name}.Nav.BuildProgress");
                _navReload = Service.Interface.GetIpcSubscriber<bool>($"{Name}.Nav.Reload");
                _navRebuild = Service.Interface.GetIpcSubscriber<bool>($"{Name}.Nav.Rebuild");
                _navPathfind = Service.Interface.GetIpcSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>>($"{Name}.Nav.Pathfind");
                _navIsAutoLoad = Service.Interface.GetIpcSubscriber<bool>($"{Name}.Nav.IsAutoLoad");
                _navSetAutoLoad = Service.Interface.GetIpcSubscriber<bool, object>($"{Name}.Nav.SetAutoLoad");

                _queryMeshNearestPoint = Service.Interface.GetIpcSubscriber<Vector3, float, float, Vector3?>($"{Name}.Query.Mesh.NearestPoint");
                _queryMeshPointOnFloor = Service.Interface.GetIpcSubscriber<Vector3, bool, float, Vector3?>($"{Name}.Query.Mesh.PointOnFloor");
                _queryMeshFurthestPoint = Service.Interface.GetIpcSubscriber<Vector3, float, float, float, Vector3?>($"{Name}.Query.Mesh.FurthestPoint");

                _pathMoveTo = Service.Interface.GetIpcSubscriber<List<Vector3>, bool, object>($"{Name}.Path.MoveTo");
                _pathStop = Service.Interface.GetIpcSubscriber<object>($"{Name}.Path.Stop");
                _pathIsRunning = Service.Interface.GetIpcSubscriber<bool>($"{Name}.Path.IsRunning");
                _pathNumWaypoints = Service.Interface.GetIpcSubscriber<int>($"{Name}.Path.NumWaypoints");
                _pathGetMovementAllowed = Service.Interface.GetIpcSubscriber<bool>($"{Name}.Path.GetMovementAllowed");
                _pathSetMovementAllowed = Service.Interface.GetIpcSubscriber<bool, object>($"{Name}.Path.SetMovementAllowed");
                _pathGetAlignCamera = Service.Interface.GetIpcSubscriber<bool>($"{Name}.Path.GetAlignCamera");
                _pathSetAlignCamera = Service.Interface.GetIpcSubscriber<bool, object>($"{Name}.Path.SetAlignCamera");
                _pathGetTolerance = Service.Interface.GetIpcSubscriber<float>($"{Name}.Path.GetTolerance");
                _pathSetTolerance = Service.Interface.GetIpcSubscriber<float, object>($"{Name}.Path.SetTolerance");

                _pathfindAndMoveTo = Service.Interface.GetIpcSubscriber<Vector3, bool, bool>($"{Name}.SimpleMove.PathfindAndMoveTo");
                _pathfindInProgress = Service.Interface.GetIpcSubscriber<bool>($"{Name}.SimpleMove.PathfindInProgress");
            }
            catch (Exception ex) { ex.Log(); }
        }
    }

    internal static T? Execute<T>(Func<T> func)
    {
        if (Utils.HasPlugin(Name))
        {
            try
            {
                if (func != null)
                    return func();
            }
            catch (Exception ex) { ex.Log(); }
        }

        return default;
    }

    internal static void Execute(Action action)
    {
        if (Utils.HasPlugin(Name))
            GenericHelpers.TryExecute(() => action?.Invoke());
    }

    internal static void Execute<T>(Action<T> action, T param)
    {
        if (Utils.HasPlugin(Name))
            GenericHelpers.TryExecute(() => action?.Invoke(param));
    }

    internal static void Execute<T1, T2>(Action<T1, T2> action, T1 p1, T2 p2)
    {
        if (Utils.HasPlugin(Name))
            GenericHelpers.TryExecute(() => action?.Invoke(p1, p2));
    }

    internal static bool IsReady() => Execute(() => _navIsReady!.InvokeFunc());
    internal static float BuildProgress() => Execute(() => _navBuildProgress!.InvokeFunc());
    internal static void Reload() => Execute(() => _navReload!.InvokeFunc());
    internal static void Rebuild() => Execute(() => _navRebuild!.InvokeFunc());
    internal static Task<List<Vector3>>? Pathfind(Vector3 from, Vector3 to, bool fly = false) => Execute(() => _navPathfind!.InvokeFunc(from, to, fly));
    internal static bool IsAutoLoad() => Execute(() => _navIsAutoLoad!.InvokeFunc());
    internal static void SetAutoLoad(bool value) => Execute(_navSetAutoLoad!.InvokeAction, value);

    internal static Vector3? QueryMeshNearestPoint(Vector3 pos, float halfExtentXZ, float halfExtentY) => Execute(() => _queryMeshNearestPoint!.InvokeFunc(pos, halfExtentXZ, halfExtentY));
    internal static Vector3? QueryMeshPointOnFloor(Vector3 pos, bool allowUnlandable, float halfExtentXZ) => Execute(() => _queryMeshPointOnFloor!.InvokeFunc(pos, allowUnlandable, halfExtentXZ));

    internal static void MoveTo(List<Vector3> waypoints, bool fly) => Execute(_pathMoveTo!.InvokeAction, waypoints, fly);
    internal static void Stop() => Execute(_pathStop!.InvokeAction);
    internal static bool IsRunning() => Execute(() => _pathIsRunning!.InvokeFunc());
    internal static int NumWaypoints() => Execute(() => _pathNumWaypoints!.InvokeFunc());
    internal static bool GetMovementAllowed() => Execute(() => _pathGetMovementAllowed!.InvokeFunc());
    internal static void SetMovementAllowed(bool value) => Execute(_pathSetMovementAllowed!.InvokeAction, value);
    internal static bool GetAlignCamera() => Execute(() => _pathGetAlignCamera!.InvokeFunc());
    internal static void SetAlignCamera(bool value) => Execute(_pathSetAlignCamera!.InvokeAction, value);
    internal static float GetTolerance() => Execute(() => _pathGetTolerance!.InvokeFunc());
    internal static void SetTolerance(float tolerance) => Execute(_pathSetTolerance!.InvokeAction, tolerance);

    internal static bool PathfindAndMoveTo(Vector3 pos, bool fly) => Execute(() => _pathfindAndMoveTo!.InvokeFunc(pos, fly));
    internal static bool PathfindInProgress() => Execute(() => _pathfindInProgress!.InvokeFunc());
}