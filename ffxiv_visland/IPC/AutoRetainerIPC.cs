using ECommons;
using ECommons.EzIpcManager;
using System;
using visland.Helpers;

namespace visland.IPC;

#nullable disable
public class AutoRetainerIPC
{
    public AutoRetainerIPC() => EzIPC.Init(this, "AutoRetainer");

    [EzIPC("PluginState.%m")] public readonly Func<bool> IsBusy;
    [EzIPC("PluginState.%m")] public readonly Func<int> GetInventoryFreeSlotCount;
    [EzIPC] public readonly Func<bool> GetMultiModeEnabled;
    [EzIPC] public readonly Action<bool> SetMultiModeEnabled;

    public bool GetMultiEnabled()
    {
        if (Utils.HasPlugin("AutoRetainer"))
        {
            GenericHelpers.TryExecute(GetMultiModeEnabled.Invoke, out var result);
            return result;
        }
        return false;
    }

    public void SetMultiEnabled(bool s)
    {
        if (Utils.HasPlugin("AutoRetainer"))
            GenericHelpers.TryExecute(() => SetMultiModeEnabled.Invoke(s));
    }
}
