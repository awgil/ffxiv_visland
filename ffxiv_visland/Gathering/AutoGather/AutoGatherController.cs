using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using System;
using System.Linq;

namespace visland.Gathering.AutoGather;

public sealed class AutoGatherController : IDisposable {
    private static readonly string[] AddonNames = ["Gathering", "GatheringMasterpiece"];

    public AutoGatherController() {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, AddonNames, OnAddonSetup);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, AddonNames, OnAddonFinalize);
    }

    public void Dispose() {
        Service.AddonLifecycle.UnregisterListener(OnAddonSetup);
        Service.AddonLifecycle.UnregisterListener(OnAddonFinalize);
    }

    private void OnAddonSetup(AddonEvent type, AddonArgs args) {
        var exec = Service.RouteExec;
        switch (args.AddonName) {
            case "Gathering":
                exec.GatheringAM = new GatheringAddon.Gathering(args.Addon);
                if (exec.CurrentRoute != null) {
                    Service.TaskManager.Enqueue(() => exec.GatheringAM.GatheredItems.Any(x => x.ItemID != 0));
                    Service.TaskManager.Enqueue(() => {
                        exec.GatheredItem = exec.GatheringAM!.GatheredItems.FirstOrDefault(x => x.ItemID != 0 && x.ItemID == (uint)exec.CurrentRoute!.TargetGatherItem);
                        return exec.GatheredItem != null;
                    });
                }
                break;
            case "GatheringMasterpiece":
                exec.GatheringCollectableAM = new GatheringAddon.GatheringMasterpiece(args.Addon);
                break;
        }
    }

    private void OnAddonFinalize(AddonEvent type, AddonArgs args) {
        switch (args.AddonName) {
            case "Gathering":
                Service.RouteExec.GatheringAM = null;
                Service.RouteExec.GatheredItem = null;
                break;
            case "GatheringMasterpiece":
                Service.RouteExec.GatheringCollectableAM = null;
                break;
        }
    }
}
