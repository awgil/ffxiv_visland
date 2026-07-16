using AutoRetainerAPI.Configuration;
using Dalamud.Plugin.Ipc;
using System.Collections.Generic;

namespace visland.IPC;

public class AutoRetainerIPC {
    private readonly ICallGateSubscriber<bool> _isBusy;
    private readonly ICallGateSubscriber<bool> _getMultiModeEnabled;
    private readonly ICallGateSubscriber<bool, object> _setMultiModeEnabled;
    private readonly ICallGateSubscriber<List<ulong>> _getRegisteredCIDs;
    private readonly ICallGateSubscriber<ulong, OfflineCharacterData> _getOfflineCharacterData;

    public AutoRetainerIPC() {
        _isBusy = Service.Interface.GetIpcSubscriber<bool>("AutoRetainer.PluginState.IsBusy");
        _getMultiModeEnabled = Service.Interface.GetIpcSubscriber<bool>("AutoRetainer.GetMultiModeEnabled");
        _setMultiModeEnabled = Service.Interface.GetIpcSubscriber<bool, object>("AutoRetainer.SetMultiModeEnabled");
        _getRegisteredCIDs = Service.Interface.GetIpcSubscriber<List<ulong>>("AutoRetainer.GetRegisteredCIDs");
        _getOfflineCharacterData = Service.Interface.GetIpcSubscriber<ulong, OfflineCharacterData>("AutoRetainer.GetOfflineCharacterData");
    }

    public bool IsBusy() => _isBusy.HasFunction && _isBusy.InvokeFunc();
    public bool GetMultiEnabled() => _getMultiModeEnabled.HasFunction && _getMultiModeEnabled.InvokeFunc();
    public void SetMultiEnabled(bool enabled) {
        if (_setMultiModeEnabled.HasAction)
            _setMultiModeEnabled.InvokeAction(enabled);
    }
    public List<ulong> GetRegisteredCIDs() => _getRegisteredCIDs.HasFunction ? _getRegisteredCIDs.InvokeFunc() : [];
    public OfflineCharacterData? GetOfflineCharacterData(ulong cid) => _getOfflineCharacterData.HasFunction ? _getOfflineCharacterData.InvokeFunc(cid) : null;
}
