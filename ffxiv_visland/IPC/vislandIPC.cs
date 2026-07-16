using System.Linq;
using Newtonsoft.Json;
using visland.Gathering;
using visland.Helpers;

namespace visland.IPC;

public class VislandIPC {
    public VislandIPC() {
        Service.Interface.GetIpcProvider<bool>("visland.IsRouteRunning")
            .RegisterFunc(() => Service.RouteExec.CurrentRoute != null && !Service.RouteExec.Paused);
        Service.Interface.GetIpcProvider<bool>("visland.IsRoutePaused")
            .RegisterFunc(() => Service.RouteExec.Paused);
        Service.Interface.GetIpcProvider<bool, object>("visland.SetRoutePaused")
            .RegisterAction(state => Service.RouteExec.Paused = state);
        Service.Interface.GetIpcProvider<object>("visland.StopRoute")
            .RegisterAction(Service.RouteExec.Finish);
        Service.Interface.GetIpcProvider<string, bool, object>("visland.StartRoute")
            .RegisterAction((route, once) => {
                var (_, json) = Utils.FromCompressedBase64(route);
                var parsed = JsonConvert.DeserializeObject<GatherRouteDB.Route>(json);
                if (parsed != null)
                    Service.RouteExec.Start(parsed, 0, true, !once);
            });
        Service.Interface.GetIpcProvider<uint, object>("visland.GatherItem")
            .RegisterAction(itemId => {
                var item = Service.RouteExec.GatheringAM?.Items.FirstOrDefault(x => x.ItemID == itemId);
                item?.Gather();
            });
    }
}
