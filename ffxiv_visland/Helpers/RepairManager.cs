using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;

namespace visland.Helpers;
internal unsafe class RepairManager
{
    private static Throttle _throttle = new();
    public static bool UseRepair() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6);

    internal static void Repair()
    {
        if (GenericHelpers.TryGetAddonByName<AddonRepair>("Repair", out var addon) && addon->AtkUnitBase.IsVisible && addon->RepairAllButton->IsEnabled)
            _throttle.Exec(() => new AddonMaster.Repair((IntPtr)addon).RepairAll());
    }

    public unsafe static void OpenRepair()
    {
        if (Svc.GameGui.GetAddonByName("Repair", 1) == IntPtr.Zero)
            UseRepair();
    }

    public unsafe static void CloseRepair()
    {
        if (Svc.GameGui.GetAddonByName("Repair", 1) != IntPtr.Zero)
            UseRepair();
    }

    private static readonly string[] _texts = ["Repair as many of the displayed items as possible using the following materials?", "修理可能なアイテムをまとめて修理しますか？", "Folgendes Material verbrauchen, um möglichst viele Gegenstände der Liste zu reparieren?", "Réparer tous les objets affichés pouvant l'être"];
    public static bool ListenersActive;
    public static void ToggleListeners(bool enable)
    {
        if (enable)
        {
            Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", ConfirmYesNo);
            ListenersActive = true;
        }
        else
        {
            Svc.AddonLifecycle.UnregisterListener(ConfirmYesNo);
            ListenersActive = false;
        }
    }


    internal static void ConfirmYesNo(AddonEvent type, AddonArgs args)
    {
        var addon = new AddonMaster.SelectYesno((AtkUnitBase*)args.Addon);
        if (addon.Text.ContainsAny(_texts))
            addon.Yes();
    }

    internal static bool HasDarkMatterOrBetter(uint darkMatterID)
    {
        var repairResources = GenericHelpers.GetSheet<ItemRepairResource>()!;
        foreach (var dm in repairResources)
        {
            if (dm.Item.RowId < darkMatterID)
                continue;

            if (InventoryManager.Instance()->GetInventoryItemCount(dm.Item.RowId) > 0)
                return true;
        }
        return false;
    }

    internal static int GetMinEquippedPercent()
    {
        var ret = ushort.MaxValue;
        var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < equipment->Size; i++)
        {
            var item = equipment->GetInventorySlot(i);
            if (item != null && item->ItemId > 0)
                if (item->Condition < ret) ret = item->Condition;
        }
        return (int)Math.Ceiling((double)ret / 300);
    }

    internal static bool CanRepairAny(float repairPercent = 0)
    {
        var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < equipment->Size; i++)
        {
            var item = equipment->GetInventorySlot(i);
            if (item != null && item->ItemId > 0)
                if (CanRepairItem(item->ItemId) && item->Condition / 300 < (repairPercent > 0 ? repairPercent : 100))
                    return true;
        }
        return false;
    }

    internal static bool CanRepairItem(uint ItemId)
    {
        var row = GenericHelpers.GetRow<Item>(ItemId)!;

        if (row.Value.ClassJobRepair.RowId > 0)
        {
            var actualJob = (Job)row.Value.ClassJobRepair.RowId;
            var repairItem = row.Value.ItemRepair.Value!.Item;

            if (!HasDarkMatterOrBetter(repairItem.RowId))
                return false;

            var jobLevel = JobLevel(actualJob);
            if (Math.Max(row.Value.LevelEquip - 10, 1) <= jobLevel)
                return true;
        }

        return false;
    }

    public static unsafe int JobLevel(Job job) => PlayerState.Instance()->ClassJobLevels[GenericHelpers.GetRow<ClassJob>((uint)job)?.ExpArrayIndex ?? 0];

    internal static bool RepairWindowOpen() => GenericHelpers.TryGetAddonByName<AddonRepair>("Repair", out _);

    private static DateTime _nextRetry;
    internal static bool ProcessRepair()
    {
        if (DateTime.Now < _nextRetry) return false;

        if (RepairWindowOpen() && !CanRepairAny())
        {
            if (DateTime.Now < _nextRetry) return false;
            CloseRepair();
            _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
            return false;
        }

        if (CanRepairAny())
        {
            if (DateTime.Now < _nextRetry) return false;
            if (!RepairWindowOpen())
            {
                OpenRepair();
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                return false;
            }

            if (RepairWindowOpen() && !GenericHelpers.IsOccupied())
            {
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
