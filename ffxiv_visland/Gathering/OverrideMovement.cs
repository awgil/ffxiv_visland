using Dalamud.Game.Config;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using visland.Helpers;

namespace visland.Gathering;

[StructLayout(LayoutKind.Explicit, Size = 0x18)]
public unsafe struct PlayerMoveControllerFlyInput {
    [FieldOffset(0x0)] public float Forward;
    [FieldOffset(0x4)] public float Left;
    [FieldOffset(0x8)] public float Up;
    [FieldOffset(0xC)] public float Turn;
    [FieldOffset(0x10)] public float u10;
    [FieldOffset(0x14)] public byte DirMode;
    [FieldOffset(0x15)] public byte HaveBackwardOrStrafe;
}

public unsafe class OverrideMovement : IDisposable {
    public bool Enabled {
        get => _rmiWalkHook.IsEnabled;
        set {
            if (value) {
                _rmiWalkHook.Enable();
                _rmiFlyHook.Enable();
            }
            else {
                _rmiWalkHook.Disable();
                _rmiFlyHook.Disable();
            }
        }
    }

    public bool IgnoreUserInput;
    public Vector3 DesiredPosition;
    public float Precision = 0.01f;

    private bool _legacyMode;

    private delegate bool RMIWalkIsInputEnabled(void* self);
    private readonly RMIWalkIsInputEnabled _rmiWalkIsInputEnabled1;
    private readonly RMIWalkIsInputEnabled _rmiWalkIsInputEnabled2;

    private delegate void RMIWalkDelegate(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk);
    private readonly Hook<RMIWalkDelegate> _rmiWalkHook;

    private delegate void RMIFlyDelegate(void* self, PlayerMoveControllerFlyInput* result);
    private readonly Hook<RMIFlyDelegate> _rmiFlyHook;

    public OverrideMovement() {
        var rmiWalkAddr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 80 7B 3E 00 48 8D 3D");
        var rmiFlyAddr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 0F B6 0D ?? ?? ?? ?? B8");
        _rmiWalkHook = Service.Hook.HookFromAddress<RMIWalkDelegate>(rmiWalkAddr, RMIWalkDetour);
        _rmiFlyHook = Service.Hook.HookFromAddress<RMIFlyDelegate>(rmiFlyAddr, RMIFlyDetour);
        Service.Log.Information($"RMIWalk address: 0x{rmiWalkAddr:X}");
        Service.Log.Information($"RMIFly address: 0x{rmiFlyAddr:X}");

        var rmiWalkIsInputEnabled1Addr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 10 38 43 3C");
        var rmiWalkIsInputEnabled2Addr = Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 03 88 47 3F");
        Service.Log.Information($"RMIWalkIsInputEnabled1 address: 0x{rmiWalkIsInputEnabled1Addr:X}");
        Service.Log.Information($"RMIWalkIsInputEnabled2 address: 0x{rmiWalkIsInputEnabled2Addr:X}");
        _rmiWalkIsInputEnabled1 = Marshal.GetDelegateForFunctionPointer<RMIWalkIsInputEnabled>(rmiWalkIsInputEnabled1Addr);
        _rmiWalkIsInputEnabled2 = Marshal.GetDelegateForFunctionPointer<RMIWalkIsInputEnabled>(rmiWalkIsInputEnabled2Addr);

        Service.GameConfig.UiControlChanged += OnConfigChanged;
        UpdateLegacyMode();
    }

    public void Dispose() {
        _rmiWalkHook.Dispose();
        _rmiFlyHook.Dispose();
        Service.GameConfig.UiControlChanged -= OnConfigChanged;
    }

    private void RMIWalkDetour(void* self, float* sumLeft, float* sumForward, float* sumTurnLeft, byte* haveBackwardOrStrafe, byte* a6, byte bAdditiveUnk) {
        _rmiWalkHook.Original(self, sumLeft, sumForward, sumTurnLeft, haveBackwardOrStrafe, a6, bAdditiveUnk);
        var movementAllowed = bAdditiveUnk == 0 && _rmiWalkIsInputEnabled1(self) && _rmiWalkIsInputEnabled2(self);
        if (movementAllowed && (IgnoreUserInput || *sumLeft == 0 && *sumForward == 0) && DirectionToDestination(false) is var relDir && relDir != null) {
            var dir = relDir.Value.h.ToDirection();
            *sumLeft = dir.X;
            *sumForward = dir.Y;
        }
    }

    private void RMIFlyDetour(void* self, PlayerMoveControllerFlyInput* result) {
        _rmiFlyHook.Original(self, result);
        if ((IgnoreUserInput || result->Forward == 0 && result->Left == 0 && result->Up == 0) && DirectionToDestination(true) is var relDir && relDir != null) {
            var dir = relDir.Value.h.ToDirection();
            result->Forward = dir.Y;
            result->Left = dir.X;
            result->Up = relDir.Value.v.Rad;
        }
    }

    private (Angle h, Angle v)? DirectionToDestination(bool allowVertical) {
        var player = Service.ObjectTable.LocalPlayer;
        if (player == null)
            return null;

        var dist = DesiredPosition - player.Position;
        if (dist.LengthSquared() <= Precision * Precision)
            return null;

        var dirH = Angle.FromDirectionXZ(dist);
        var dirV = allowVertical ? Angle.FromDirection(new(dist.Y, new Vector2(dist.X, dist.Z).Length())) : default;

        var refDir = _legacyMode
            ? ((CameraEx*)CameraManager.Instance()->GetActiveCamera())->DirH.Radians() + 180.Degrees()
            : player.Rotation.Radians();
        return (dirH - refDir, dirV);
    }

    private void OnConfigChanged(object? sender, ConfigChangeEvent evt) => UpdateLegacyMode();
    private void UpdateLegacyMode() {
        _legacyMode = Service.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) && mode == 1;
        Service.Log.Debug($"Legacy mode is now {(_legacyMode ? "enabled" : "disabled")}");
    }
}
