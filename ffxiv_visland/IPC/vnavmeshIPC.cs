using ECommons.EzIpcManager;
using System;
using System.Numerics;
using System.Threading.Channels;
using System.Threading;
using visland.Helpers;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace visland.IPC;

#nullable disable
public class NavmeshIPC
{
    public static string Name = "vnavmesh";
    public NavmeshIPC() => EzIPC.Init(this, Name);
    public static bool IsEnabled => Utils.HasPlugin(Name);
    public static TimeSpan PathfindingTime { get; set; }

    [EzIPC("Nav.%m")] public readonly Func<bool> IsReady;
    [EzIPC("Nav.%m")] public readonly Func<float> BuildProgress;
    [EzIPC("Nav.%m")] public readonly Func<bool> Reload;
    [EzIPC("Nav.%m")] public readonly Func<bool> Rebuild;
    [EzIPC("Nav.%m")] public readonly Func<Vector3, Vector3, bool, Task<List<Vector3>>> Pathfind;
    [EzIPC("Nav.%m")] public readonly Func<Vector3, Vector3, bool, CancellationToken, Task<List<Vector3>>> PathfindCancelable;
    [EzIPC("Nav.%m")] public readonly Action PathfindCancelAll;
    [EzIPC("Nav.%m")] public readonly Func<bool> PathfindInProgress;
    [EzIPC("Nav.%m")] public readonly Func<int> PathfindNumQueued;
    [EzIPC("Nav.%m")] public readonly Func<bool> IsAutoLoad;
    [EzIPC("Nav.%m")] public readonly Action<bool> SetAutoLoad;

    [EzIPC("SimpleMove.%m")] public readonly Func<Vector3, bool, bool> PathfindAndMoveTo;
    [EzIPC("SimpleMove.PathfindInProgress")] public readonly Func<bool> SimpleMoveInProgress;

    [EzIPC("Path.%m")] public readonly Action<List<Vector3>, bool> MoveTo;
    [EzIPC("Path.%m")] public readonly Action Stop;
    [EzIPC("Path.%m")] public readonly Func<bool> IsRunning;
    [EzIPC("Path.%m")] public readonly Func<int> NumWaypoints;
    [EzIPC("Path.%m")] public readonly Func<bool> GetMovementAllowed;
    [EzIPC("Path.%m")] public readonly Action<bool> SetMovementAllowed;
    [EzIPC("Path.%m")] public readonly Func<bool> GetAlignCamera;
    [EzIPC("Path.%m")] public readonly Action<bool> SetAlignCamera;
    [EzIPC("Path.%m")] public readonly Func<float> GetTolerance;

    [EzIPC("Query.Mesh.%m")] public readonly Func<Vector3, float, float, Vector3?> NearestPoint;
    [EzIPC("Query.Mesh.%m")] public readonly Func<Vector3, float, bool, Vector3?> PointOnFloor;


}
