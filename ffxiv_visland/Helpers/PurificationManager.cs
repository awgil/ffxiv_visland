using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using ECommons;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using System.Collections.Generic;
using System.Linq;

namespace visland.Helpers;
internal class PurificationManager
{
    public static readonly InventoryType[] PlayerInventory =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    ];

    public static unsafe bool PurifyAllTask()
    {
        if (IsResultsOpen() || ListenersActive || Svc.Condition[ConditionFlag.Occupied39]) return false;
        return PurifyItemTask();
    }

    public static unsafe bool PurifyItemTask()
    {
        var items = GetPurifyableItems();
        var agent = AgentPuryfyItemSelector.Instance();
        if (items.Count == 0 || agent == null) return true;

        EnableListeners();
        agent->ReduceItem(items.First());
        return true;
    }

    public static unsafe bool IsResultsOpen() => GenericHelpers.TryGetAddonByName<AtkUnitBase>("PurifyResult", out var results) && results->IsVisible;

    private static bool ListenersActive;
    private static void EnableListeners()
    {
        Svc.Log.Verbose("Enabling PurifyResult listeners");
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "PurifyResult", ResultsSetup);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "PurifyResult", DisableListeners);
        ListenersActive = true;
    }

    private static void DisableListeners(AddonEvent type, AddonArgs args)
    {
        Svc.Log.Verbose("Disabling PurifyResult listeners");
        Svc.AddonLifecycle.UnregisterListener(ResultsSetup);
        Svc.AddonLifecycle.UnregisterListener(DisableListeners);
        ListenersActive = false;
    }

    private static unsafe void ResultsSetup(AddonEvent type, AddonArgs args)
    {
        if (!GenericHelpers.IsAddonReady((AtkUnitBase*)args.Addon)) return;
        new AddonMaster.PurifyResult((AtkUnitBase*)args.Addon).Close();
    }

    public static bool CanPurifyAny() => GetPurifyableItems().Count > 0;

    public static unsafe List<Pointer<InventoryItem>> GetPurifyableItems()
    {
        List<Pointer<InventoryItem>> items = [];
        foreach (var inv in PlayerInventory)
        {
            var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
            for (var i = 0; i < cont->Size; ++i)
                if (cont->GetInventorySlot(i)->Flags == InventoryItem.ItemFlags.Collectable)
                    items.Add(cont->GetInventorySlot(i));
        }
        return items;
    }
}
