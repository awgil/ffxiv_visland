using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using System;
using System.Linq;

namespace visland.Gathering.AutoGather;

// TODO: remove entirely? I don't think anyone uses this and it's totally scope creep
public sealed class AutoGatherController : IDisposable {
    private static readonly string[] _addonNames = ["Gathering", "GatheringMasterpiece"];

    public AutoGatherController() {
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, _addonNames, OnAddonSetup);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, _addonNames, OnAddonFinalize);
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
                    Service.TaskManager.Enqueue(() => exec.GatheringAM.Items.Any(x => x.ItemID != 0));
                    Service.TaskManager.Enqueue(() => {
                        exec.GatheredItem = exec.GatheringAM!.Items.FirstOrDefault(x => x.ItemID != 0 && x.ItemID == (uint)exec.CurrentRoute!.TargetGatherItem);
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
