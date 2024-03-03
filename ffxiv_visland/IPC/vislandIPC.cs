using System;
using System.Collections.Generic;
using visland.Gathering;

namespace visland.IPC;

internal class VislandIPC
{
    private readonly List<Action> _disposeActions = [];

    public VislandIPC(GatherWindow _wndGather)
    {
        Register("IsRouteRunning", () => _wndGather.Exec.CurrentRoute != null && !_wndGather.Exec.Paused);
        Register("IsRoutePaused", () => _wndGather.Exec.Paused);
        Register<bool>("SetRoutePaused", s => _wndGather.Exec.Paused = s);
        Register("StopRoute", _wndGather.Exec.Finish);
    }

    public void Dispose()
    {
        foreach (var a in _disposeActions)
            a();
    }

    private void Register<TRet>(string name, Func<TRet> func)
    {
        var p = Service.Interface.GetIpcProvider<TRet>($"{Plugin.Name}." + name);
        p.RegisterFunc(func);
        _disposeActions.Add(p.UnregisterFunc);
    }

    private void Register(string name, Action func)
    {
        var p = Service.Interface.GetIpcProvider<object>($"{Plugin.Name}." + name);
        p.RegisterAction(func);
        _disposeActions.Add(p.UnregisterAction);
    }

    private void Register<T1>(string name, Action<T1> func)
    {
        var p = Service.Interface.GetIpcProvider<T1, object>($"{Plugin.Name}." + name);
        p.RegisterAction(func);
        _disposeActions.Add(p.UnregisterAction);
    }
}
