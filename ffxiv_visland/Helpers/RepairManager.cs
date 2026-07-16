using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;

namespace visland.Helpers;

internal unsafe class RepairManager {
    private static readonly Throttle _throttle = new();
    public static bool UseRepair() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6);

    internal static void Repair() {
        if (!AddonUtils.TryGetAddonByName("Repair", out AddonRepair* addon) || !addon->AtkUnitBase.IsVisible)
            return;
        var btn = addon->RepairAllButton;
        if (btn == null || !btn->IsEnabled)
            return;
        _throttle.Exec(() => AtkCallback.Fire(&addon->AtkUnitBase, false, 0));
    }

    public static unsafe void OpenRepair() {
        if (Service.GameGui.GetAddonByName("Repair", 1) == IntPtr.Zero)
            UseRepair();
    }

    public static unsafe void CloseRepair() {
        if (Service.GameGui.GetAddonByName("Repair", 1) != IntPtr.Zero)
            UseRepair();
    }

    private static readonly string[] _texts = ["Repair as many of the displayed items as possible using the following materials?", "修理可能なアイテムをまとめて修理しますか？", "Folgendes Material verbrauchen, um möglichst viele Gegenstände der Liste zu reparieren?", "Réparer tous les objets affichés pouvant l'être"];
    public static bool ListenersActive;
    public static void ToggleListeners(bool enable) {
        if (enable) {
            Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", ConfirmYesNo);
            ListenersActive = true;
        }
        else {
            Service.AddonLifecycle.UnregisterListener(ConfirmYesNo);
            ListenersActive = false;
        }
    }

    internal static void ConfirmYesNo(AddonEvent type, AddonArgs args) {
        var addon = (AtkUnitBase*)args.Addon.Address;
        var textNode = addon->GetTextNodeById(2);
        var text = textNode != null ? textNode->NodeText.ToString() : "";
        foreach (var expected in _texts) {
            if (text.Contains(expected, StringComparison.Ordinal)) {
                Game.SelectYes();
                return;
            }
        }
    }

    internal static bool HasDarkMatterOrBetter(uint darkMatterID) => ItemRepairResource.Any(r => r.Item.RowId >= darkMatterID && InventoryManager.Instance()->GetInventoryItemCount(r.Item.RowId) > 0);

    internal static int GetMinEquippedPercent() {
        var ret = ushort.MaxValue;
        var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < equipment->Size; i++) {
            var item = equipment->GetInventorySlot(i);
            if (item != null && item->ItemId > 0)
                if (item->Condition < ret) ret = item->Condition;
        }
        return (int)Math.Ceiling((double)ret / 300);
    }

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

    private static DateTime _nextRetry;
    internal static bool ProcessRepair() {
        if (DateTime.Now < _nextRetry) return false;

        if (RepairWindowOpen() && !CanRepairAny()) {
            if (DateTime.Now < _nextRetry) return false;
            CloseRepair();
            _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
            return false;
        }

        if (CanRepairAny()) {
            if (DateTime.Now < _nextRetry) return false;
            if (!RepairWindowOpen()) {
                OpenRepair();
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                return false;
            }

            if (RepairWindowOpen() && !AddonUtils.IsOccupied()) {
                Repair();
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                return false;
            }

            _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
            return false;
        }

        return true;
    }
}
