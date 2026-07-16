using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;
using System.Runtime.InteropServices;
using visland.Helpers;

namespace visland.Gathering;

[StructLayout(LayoutKind.Explicit, Size = 0x2B0)]
public unsafe struct CameraEx {
    [FieldOffset(0x140)] public float DirH;
    [FieldOffset(0x144)] public float DirV;
    [FieldOffset(0x148)] public float InputDeltaHAdjusted;
    [FieldOffset(0x14C)] public float InputDeltaVAdjusted;
    [FieldOffset(0x150)] public float InputDeltaH;
    [FieldOffset(0x154)] public float InputDeltaV;
    [FieldOffset(0x158)] public float DirVMin;
    [FieldOffset(0x15C)] public float DirVMax;
}

public unsafe class OverrideCamera : IDisposable {
    public bool Enabled {
        get => _rmiCameraHook.IsEnabled;
        set {
            if (value)
                _rmiCameraHook.Enable();
            else
                _rmiCameraHook.Disable();
        }
    }

    public bool IgnoreUserInput;
    public Angle DesiredAzimuth;
    public Angle DesiredAltitude;
    public Angle SpeedH = 360.Degrees();
    public Angle SpeedV = 360.Degrees();

    private delegate void RMICameraDelegate(CameraEx* self, int inputMode, float speedH, float speedV);
    private readonly Hook<RMICameraDelegate> _rmiCameraHook;

    public OverrideCamera() {
        var rmiCameraAddr = Service.SigScanner.ScanText("48 8B C4 53 48 81 EC ?? ?? ?? ?? 44 0F 29 50 ??");
        _rmiCameraHook = Service.Hook.HookFromAddress<RMICameraDelegate>(rmiCameraAddr, RMICameraDetour);
        Service.Log.Information($"RMICamera address: 0x{rmiCameraAddr:X}");
    }

    public void Dispose() => _rmiCameraHook.Dispose();

    private void RMICameraDetour(CameraEx* self, int inputMode, float speedH, float speedV) {
        _rmiCameraHook.Original(self, inputMode, speedH, speedV);
        if (IgnoreUserInput || inputMode == 0) {
            var dt = Framework.Instance()->FrameDeltaTime;
            var deltaH = (DesiredAzimuth - self->DirH.Radians()).Normalized();
            var deltaV = (DesiredAltitude - self->DirV.Radians()).Normalized();
            var maxH = SpeedH.Rad * dt;
            var maxV = SpeedV.Rad * dt;
            self->InputDeltaH = Math.Clamp(deltaH.Rad, -maxH, maxH);
            self->InputDeltaV = Math.Clamp(deltaV.Rad, -maxV, maxV);
        }
    }
}
