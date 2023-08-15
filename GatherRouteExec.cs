using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ImGuiNET;
using SharpDX;
using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace visland;

public class GatherRouteExec : IDisposable
{
    public GatherRouteDB.Route? CurrentRoute;
    public int CurrentWaypoint;
    public bool ContinueToNext;
    public bool LoopAtEnd;
    public bool Paused;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetMatrixSingletonDelegate();
    private GetMatrixSingletonDelegate _getMatrixSingleton { get; init; }

    private delegate int GetGamepadAxisDelegate(ulong self, int axisID);
    [Signature("E8 ?? ?? ?? ?? 0F BE 0D ?? ?? ?? ?? BA 04 00 00 00 66 0F 6E F8 66 0F 6E C1 48 8B CE 0F 5B C0 0F 5B FF F3 0F 5E F8", DetourName = nameof(GetGamepadAxisDetour))]
    private Hook<GetGamepadAxisDelegate> _getGamepadAxisHook = null!;

    private int[] _gamepadOverrides = new int[7];
    private bool _gamepadOverridesEnabled;

    private float _cameraAzimuth;
    private float _cameraAltitude;
    private Throttle _interact = new();
    private Throttle _sprint = new();

    public GatherRouteExec()
    {
        SignatureHelper.Initialise(this);
        PluginLog.Information($"GetGamepadAxis address: 0x{_getGamepadAxisHook.Address:X}");

        var getMatrixSingletonAddress = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8D 4C 24 ?? 48 89 4c 24 ?? 4C 8D 4D ?? 4C 8D 44 24 ??");
        _getMatrixSingleton = Marshal.GetDelegateForFunctionPointer<GetMatrixSingletonDelegate>(getMatrixSingletonAddress);
        PluginLog.Information($"GetMatrixSingleton address: 0x{getMatrixSingletonAddress:X}");
    }

    public void Dispose()
    {
        _getGamepadAxisHook.Dispose();
    }

    public unsafe void Update()
    {
        var matrixSingleton = _getMatrixSingleton();
        var viewProj = ReadMatrix(matrixSingleton + 0x1B4);
        var proj = ReadMatrix(matrixSingleton + 0x174);
        var view = viewProj * Matrix.Invert(proj);
        _cameraAzimuth = MathF.Atan2(view.Column3.X, view.Column3.Z);
        _cameraAltitude = MathF.Asin(view.Column3.Y);

        var player = Service.ClientState.LocalPlayer;
        var gathering = Service.Condition[ConditionFlag.OccupiedInQuestEvent];
        var wp = gathering || Paused || CurrentRoute == null || CurrentWaypoint >= CurrentRoute.Waypoints.Count ? null : CurrentRoute.Waypoints[CurrentWaypoint];
        var toWaypoint = wp != null && player != null ? wp.Position - player.Position : new();
        var toWaypointXZ = new Vector3(toWaypoint.X, 0, toWaypoint.Z);
        var radius = wp?.Radius ?? 0;
        bool needToGetCloser = toWaypoint.LengthSquared() > radius * radius;

        _gamepadOverridesEnabled = needToGetCloser;
        if (needToGetCloser)
        {
            var distance = player != null ? (wp.Position - player.Position).Length() : 0;
            Mount(distance);

            var cameraFacing = _cameraAzimuth + MathF.PI;
            var dirToDist = MathF.Atan2(toWaypoint.X, toWaypoint.Z);
            var relDir = cameraFacing - dirToDist;
            _gamepadOverrides[3] = (int)(100 * MathF.Sin(relDir));
            _gamepadOverrides[4] = (int)(100 * MathF.Cos(relDir));
            _gamepadOverrides[5] = Math.Clamp((int)(NormalizeAngle(relDir) * 500), -100, 100);
            _gamepadOverrides[6] = 0;
            if (Service.Condition[ConditionFlag.InFlight] || Service.Condition[ConditionFlag.Diving])
            {
                var dy = _gamepadOverrides[4] < 0 ? toWaypoint.Y : -toWaypoint.Y;
                var angle = _cameraAltitude - MathF.Atan2(dy, toWaypointXZ.Length());
                _gamepadOverrides[6] = Math.Clamp((int)(NormalizeAngle(angle) * 500), -100, 100);
            }

            var sprintStatus = player?.StatusList.FirstOrDefault(s => s.StatusId == 50);
            var sprintRemaining = sprintStatus?.RemainingTime ?? 0;
            if (sprintRemaining < 5 && !Service.Condition[ConditionFlag.Mounted] && MJIManager.Instance()->IsPlayerInSanctuary == 1)
            {
                _sprint.Exec(() => ActionManager.Instance()->UseAction(ActionType.Spell, 31314));
            }

            return;
        }

        var interactObj = !gathering ? FindObjectToInteractWith(wp) : null;
        if (interactObj != null)
        {
            _interact.Exec(() =>
            {
                PluginLog.Debug("Interacting...");
                TargetSystem.Instance()->InteractWithObject(interactObj);
            });
            return;
        }

        if (wp == null)
            return;

        if (!ContinueToNext)
        {
            Finish();
            return;
        }

        if (++CurrentWaypoint >= CurrentRoute!.Waypoints.Count)
        {
            if (LoopAtEnd)
            {
                CurrentWaypoint = 0;
            }
            else
            {
                Finish();
            }
        }
    }

    public void Start(GatherRouteDB.Route route, int waypoint, bool continueToNext, bool loopAtEnd)
    {
        CurrentRoute = route;
        CurrentWaypoint = waypoint;
        ContinueToNext = continueToNext;
        LoopAtEnd = loopAtEnd;
        _getGamepadAxisHook.Enable();
    }

    public void Finish()
    {
        if (CurrentRoute == null)
            return;
        CurrentRoute = null;
        CurrentWaypoint = 0;
        ContinueToNext = false;
        LoopAtEnd = false;
        _getGamepadAxisHook.Disable();
    }

    public void Draw(UITree tree)
    {
        //ImGui.TextUnformatted($"Camera: {_cameraAzimuth * 180 / MathF.PI:f2} {_cameraAltitude * 180 / MathF.PI:f2}");
        //ImGui.TextUnformatted($"Gamepad: {_gamepadOverrides[3]} {_gamepadOverrides[4]} {_gamepadOverrides[5]} {_gamepadOverrides[6]}");
        //var player = Service.ClientState.LocalPlayer;
        //var target = Service.TargetManager.Target;
        //var cameraFacing = _cameraAzimuth + MathF.PI;
        //if (target != null)
        //{
        //    var toTarget = target.Position - player!.Position;
        //    var dirToTarget = MathF.Atan2(toTarget.X, toTarget.Z);
        //    var relDirTarget = cameraFacing - dirToTarget;
        //    ImGui.TextUnformatted($"Target reldir: {relDirTarget * 180 / MathF.PI:f2}");
        //}

        if (CurrentRoute == null || CurrentWaypoint >= CurrentRoute.Waypoints.Count)
        {
            ImGui.TextUnformatted("Route not running");
            return;
        }

        var curPos = Service.ClientState.LocalPlayer?.Position ?? new();
        var wp = CurrentRoute.Waypoints[CurrentWaypoint];
        ImGui.TextUnformatted($"Executing: {CurrentRoute.Name} #{CurrentWaypoint}: [{wp.Position.X:f3}, {wp.Position.Y:f3}, {wp.Position.Z:f3}] +- {wp.Radius:f3} (dist={(curPos - wp.Position).Length():f3}) @ {wp.InteractWith}");
        ImGui.SameLine();
        if (ImGui.Button(Paused ? "Resume" : "Pause"))
        {
            Paused = !Paused;
        }
        ImGui.SameLine();
        if (ImGui.Button("Stop"))
        {
            Finish();
        }

        //var toWaypoint = wp.Position - player!.Position;
        //var toWaypointXZ = new Vector3(toWaypoint.X, 0, toWaypoint.Z);
        //var dirToDist = MathF.Atan2(toWaypoint.X, toWaypoint.Z);
        //var relDir = cameraFacing - dirToDist;
        //var dy = _gamepadOverrides[4] < 0 ? toWaypoint.Y : -toWaypoint.Y;
        //var angle = _cameraAltitude - MathF.Atan2(dy, toWaypointXZ.Length());
        //ImGui.TextUnformatted($"RelDir={relDir * 180 / MathF.PI:f2}, dy={dy:f3}, angle={angle * 180 / MathF.PI:f2}");
    }

    private int GetGamepadAxisDetour(ulong self, int axisID) => _gamepadOverridesEnabled && axisID < _gamepadOverrides.Length ? _gamepadOverrides[axisID] : _getGamepadAxisHook.Original(self, axisID);

    private unsafe Matrix ReadMatrix(nint address)
    {
        var p = (float*)address;
        Matrix mtx = new();
        for (var i = 0; i < 16; i++)
            mtx[i] = *p++;
        return mtx;
    }

    private unsafe GameObject* FindObjectToInteractWith(GatherRouteDB.Waypoint? wp)
    {
        if (wp == null || wp.InteractWith.Length == 0)
            return null;

        foreach (var obj in Service.ObjectTable.Where(o => o.DataId == GatherNodeDB.GatherNodeDataId && (o.Position - wp.Position).LengthSquared() < 1 && o.Name.ToString().ToLower() == wp.InteractWith))
            return obj.IsTargetable ? (GameObject*)obj.Address : null;

        return null;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle < -MathF.PI)
            angle += 2 * MathF.PI;
        while (angle > MathF.PI)
            angle -= 2 * MathF.PI;
        return angle;
    }

    private unsafe void Mount(float distance)
    {
        if (Service.ClientState.LocalPlayer.IsCasting)
        {
            return;
        }

        // distance should be configurable I guess?
        if (distance > 30)
        {
            if (Service.Condition[ConditionFlag.Mounted])
            {
                _getGamepadAxisHook.Enable();
                ContinueToNext = true;
                return;
            }

            if (!Service.Condition[ConditionFlag.Mounted])
            {
                _getGamepadAxisHook.Disable();
                ContinueToNext = false;
                _sprint.Exec(() => ActionManager.Instance()->UseAction(ActionType.General, 24));
            }
        }
    }
}
