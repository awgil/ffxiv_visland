using Dalamud.Plugin.Ipc;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace visland.IPC;

public class NavmeshIPC {
    public const string Name = "vnavmesh";

    private readonly ICallGateSubscriber<bool> _navIsReady;
    private readonly ICallGateSubscriber<float> _navBuildProgress;
    private readonly ICallGateSubscriber<bool> _navReload;
    private readonly ICallGateSubscriber<bool> _navRebuild;
    private readonly ICallGateSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>> _navPathfind;
    private readonly ICallGateSubscriber<bool> _navIsAutoLoad;
    private readonly ICallGateSubscriber<bool, object> _navSetAutoLoad;
    private readonly ICallGateSubscriber<Vector3, float, float, Vector3?> _queryMeshNearestPoint;
    private readonly ICallGateSubscriber<Vector3, bool, float, Vector3?> _queryMeshPointOnFloor;
    private readonly ICallGateSubscriber<List<Vector3>, bool, object> _pathMoveTo;
    private readonly ICallGateSubscriber<object> _pathStop;
    private readonly ICallGateSubscriber<bool> _pathIsRunning;
    private readonly ICallGateSubscriber<int> _pathNumWaypoints;
    private readonly ICallGateSubscriber<bool> _pathGetMovementAllowed;
    private readonly ICallGateSubscriber<bool, object> _pathSetMovementAllowed;
    private readonly ICallGateSubscriber<bool> _pathGetAlignCamera;
    private readonly ICallGateSubscriber<bool, object> _pathSetAlignCamera;
    private readonly ICallGateSubscriber<float> _pathGetTolerance;
    private readonly ICallGateSubscriber<float, object> _pathSetTolerance;
    private readonly ICallGateSubscriber<Vector3, bool, bool> _pathfindAndMoveTo;
    private readonly ICallGateSubscriber<bool> _pathfindInProgress;

    public NavmeshIPC() {
        _navIsReady = Service.Interface.GetIpcSubscriber<bool>($"{Name}.Nav.IsReady");
        _navBuildProgress = Service.Interface.GetIpcSubscriber<float>($"{Name}.Nav.BuildProgress");
        _navReload = Service.Interface.GetIpcSubscriber<bool>($"{Name}.Nav.Reload");
        _navRebuild = Service.Interface.GetIpcSubscriber<bool>($"{Name}.Nav.Rebuild");
        _navPathfind = Service.Interface.GetIpcSubscriber<Vector3, Vector3, bool, Task<List<Vector3>>>($"{Name}.Nav.Pathfind");
        _navIsAutoLoad = Service.Interface.GetIpcSubscriber<bool>($"{Name}.Nav.IsAutoLoad");
        _navSetAutoLoad = Service.Interface.GetIpcSubscriber<bool, object>($"{Name}.Nav.SetAutoLoad");
        _queryMeshNearestPoint = Service.Interface.GetIpcSubscriber<Vector3, float, float, Vector3?>($"{Name}.Query.Mesh.NearestPoint");
        _queryMeshPointOnFloor = Service.Interface.GetIpcSubscriber<Vector3, bool, float, Vector3?>($"{Name}.Query.Mesh.PointOnFloor");
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

    public bool IsEnabled => _navIsReady.HasFunction;

    public bool IsReady() => _navIsReady.HasFunction && _navIsReady.InvokeFunc();
    public float BuildProgress() => _navBuildProgress.HasFunction ? _navBuildProgress.InvokeFunc() : 0;
    public void Reload() { if (_navReload.HasFunction) _navReload.InvokeFunc(); }
    public void Rebuild() { if (_navRebuild.HasFunction) _navRebuild.InvokeFunc(); }
    public Task<List<Vector3>>? Pathfind(Vector3 from, Vector3 to, bool fly = false)
        => _navPathfind.HasFunction ? _navPathfind.InvokeFunc(from, to, fly) : null;
    public bool IsAutoLoad() => _navIsAutoLoad.HasFunction && _navIsAutoLoad.InvokeFunc();
    public void SetAutoLoad(bool value) { if (_navSetAutoLoad.HasAction) _navSetAutoLoad.InvokeAction(value); }

    public Vector3? QueryMeshNearestPoint(Vector3 pos, float halfExtentXZ, float halfExtentY)
        => _queryMeshNearestPoint.HasFunction ? _queryMeshNearestPoint.InvokeFunc(pos, halfExtentXZ, halfExtentY) : null;
    public Vector3? QueryMeshPointOnFloor(Vector3 pos, bool allowUnlandable, float halfExtentXZ)
        => _queryMeshPointOnFloor.HasFunction ? _queryMeshPointOnFloor.InvokeFunc(pos, allowUnlandable, halfExtentXZ) : null;

    public void MoveTo(List<Vector3> waypoints, bool fly) { if (_pathMoveTo.HasAction) _pathMoveTo.InvokeAction(waypoints, fly); }
    public void Stop() { if (_pathStop.HasAction) _pathStop.InvokeAction(); }
    public bool IsRunning() => _pathIsRunning.HasFunction && _pathIsRunning.InvokeFunc();
    public int NumWaypoints() => _pathNumWaypoints.HasFunction ? _pathNumWaypoints.InvokeFunc() : 0;
    public bool GetMovementAllowed() => _pathGetMovementAllowed.HasFunction && _pathGetMovementAllowed.InvokeFunc();
    public void SetMovementAllowed(bool value) { if (_pathSetMovementAllowed.HasAction) _pathSetMovementAllowed.InvokeAction(value); }
    public bool GetAlignCamera() => _pathGetAlignCamera.HasFunction && _pathGetAlignCamera.InvokeFunc();
    public void SetAlignCamera(bool value) { if (_pathSetAlignCamera.HasAction) _pathSetAlignCamera.InvokeAction(value); }
    public float GetTolerance() => _pathGetTolerance.HasFunction ? _pathGetTolerance.InvokeFunc() : 0;
    public void SetTolerance(float tolerance) { if (_pathSetTolerance.HasAction) _pathSetTolerance.InvokeAction(tolerance); }

    public bool PathfindAndMoveTo(Vector3 pos, bool fly) => _pathfindAndMoveTo.HasFunction && _pathfindAndMoveTo.InvokeFunc(pos, fly);
    public bool PathfindInProgress() => _pathfindInProgress.HasFunction && _pathfindInProgress.InvokeFunc();
}
