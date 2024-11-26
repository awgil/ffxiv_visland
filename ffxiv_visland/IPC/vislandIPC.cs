using ECommons;
using ECommons.EzIpcManager;
using visland.Gathering;

namespace visland.IPC;

internal class VislandIPC
{
    private readonly GatherWindow wndGather;
    public VislandIPC(GatherWindow _wndGather)
    {
        wndGather = _wndGather;
        EzIPC.Init(this);
    }

    [EzIPC] public bool IsRouteRunning() => wndGather.Exec.CurrentRoute != null && !wndGather.Exec.Paused;
    [EzIPC] public bool IsRoutePaused() => wndGather.Exec.Paused;
    [EzIPC] public void SetRoutePaused(bool state) => wndGather.Exec.Paused = state;
    [EzIPC] public void StopRoute() => wndGather.Exec.Finish();
    [EzIPC] public void StartRoute(string route, bool once) => P.ExecuteTempRoute(route, once);

    [EzIPC]
    public void GatherItem(uint itemId)
    {
        if (wndGather.Exec.GatheringAM?.GatheredItems.TryGetFirst(x => x.ItemID == itemId, out var item) ?? false)
            item.Gather();
    }
}
