using FFXIVClientStructs.FFXIV.Client.UI;
using System.Runtime.InteropServices;

namespace visland;

[StructLayout(LayoutKind.Explicit, Size = 0x4F8)]
public unsafe struct AfkModule
{
    [FieldOffset(0x10)] public float ElapsedForAfkMessage;
    [FieldOffset(0x14)] public float ElapsedForKick;
    [FieldOffset(0x18)] public float ElapsedUnk1;
    [FieldOffset(0x1C)] public float ElapsedUnk2;
}

public unsafe class OverrideAFK
{
    private AfkModule* _module;

    public OverrideAFK()
    {
        var uiModule = UIModule.Instance();
        var uiModuleVtbl = (void**)uiModule->VTable;
        var getAfkModule = (delegate* unmanaged[Stdcall]<UIModule*, AfkModule*>)uiModuleVtbl[55];
        _module = getAfkModule(uiModule);
        Service.Log.Info($"Module: {(nint)_module:X}");
    }

    public void ResetTimers()
    {
        _module->ElapsedForAfkMessage = 0;
        _module->ElapsedForKick = 0;
        _module->ElapsedUnk1 = 0;
        _module->ElapsedUnk2 = 0;
    }
}
