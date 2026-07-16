using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace visland.Helpers;

internal class PurificationManager {
    public static readonly InventoryType[] PlayerInventory =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
    ];

    private static DateTime _nextRetry;
    public static unsafe bool PurifyAllTask() {
        if (!QuestManager.IsQuestComplete(67633)) return true;
        if (CanPurifyAny()) {
            if (DateTime.Now < _nextRetry) return false;
            if (!AddonUtils.IsOccupied() && !Service.Condition[ConditionFlag.Occupied39]) {
                PurifyItem(GetPurifyableItems().First());
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(500));
                return false;
            }

            _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(500));
            return false;
        }
        return true;
    }

    public static unsafe void PurifyItem(Pointer<InventoryItem> item) {
        var agent = AgentPurify.Instance();
        if (agent == null) { Service.Log.Debug("AgentPurify is null"); return; }

        agent->ReduceItem(item);
        Service.Log.Debug($"Reducing [{item.Value->ItemId}] {item.Value->Container}/{item.Value->Slot}");
    }

    private static unsafe bool IsResultsOpen() => AddonUtils.TryGetAddonByName("PurifyResult", out var results) && results->IsVisible;

    public static bool ListenersActive;
    public static void EnableListeners() {
        if (!ListenersActive) {
            Service.Log.Debug("Enabling PurifyResult listeners");
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "PurifyResult", ResultsSetup);
            ListenersActive = true;
        }
    }

    public static void DisableListeners() {
        if (ListenersActive) {
            Service.Log.Debug("Disabling PurifyResult listeners");
            Service.AddonLifecycle.UnregisterListener(ResultsSetup);
            ListenersActive = false;
        }
    }

    private static void ResultsSetup(AddonEvent type, AddonArgs args) {
        AtkCallback.Fire(args.AddonName, false, 0);
    }

    public static bool CanPurifyAny() => GetPurifyableItems().Count > 0;

    public static unsafe List<Pointer<InventoryItem>> GetPurifyableItems() {
        List<Pointer<InventoryItem>> items = [];
        foreach (var inv in PlayerInventory) {
            var cont = InventoryManager.Instance()->GetInventoryContainer(inv);
            for (var i = 0; i < cont->Size; ++i)
                if (cont->GetInventorySlot(i)->Flags == InventoryItem.ItemFlags.Collectable && Item.GetRow(cont->GetInventorySlot(i)->ItemId)?.AetherialReduce > 0)
                    items.Add(cont->GetInventorySlot(i));
        }
        return items;
    }
}
