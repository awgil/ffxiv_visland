using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.Sheets;
using System;

namespace visland.Helpers;

internal unsafe class RepairAssistantManager {
    public static bool UseRepair() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6);

    internal static bool HasDarkMatterOrBetter(uint darkMatterID) => ItemRepairResource.Any(r => r.Item.RowId >= darkMatterID && InventoryManager.Instance()->GetInventoryItemCount(r.Item.RowId) > 0);

    internal static bool CanRepairAny(float repairPercent = 0) {
        var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < equipment->Size; i++) {
            var item = equipment->GetInventorySlot(i);
            if (item != null && item->ItemId > 0)
                if (CanRepairItem(item->ItemId) && item->Condition / 300 < (repairPercent > 0 ? repairPercent : 100))
                    return true;
        }
        return false;
    }

    internal static bool CanRepairItem(uint ItemId) {
        if (Item.GetRow(ItemId) is { ClassJobCategory.RowId: > 0, ClassJobRepair.RowId: > 0 } row) {
            var repairItem = row.ItemRepair.Value!.Item;

            if (!HasDarkMatterOrBetter(repairItem.RowId))
                return false;

            var jobLevel = Player.JobLevel(row.ClassJobRepair.RowId);
            if (Math.Max(row.LevelEquip - 10, 1) <= jobLevel)
                return true;
        }

        return false;
    }

    internal static bool RepairWindowOpen() => AddonUtils.TryGetAddonByName<AddonRepair>("Repair", out _);
    internal static bool ProcessRepair() {
        Service.TaskManager.Enqueue(UseRepair);
        Service.TaskManager.Enqueue(RepairWindowOpen);
        Service.TaskManager.Enqueue(() => RepairManager.Instance()->RepairEquipped(false));
        Service.TaskManager.Enqueue(() => !CanRepairAny());
        Service.TaskManager.Enqueue(UseRepair);
        return true;
    }
}
