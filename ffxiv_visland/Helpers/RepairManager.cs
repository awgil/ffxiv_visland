using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;
using visland.Gathering;

namespace visland.Helpers;
internal unsafe class RepairManager
{
    private static Throttle _throttle = new();
    public static bool UseRepair() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 6);

    internal static void Repair()
    {
        if (GenericHelpers.TryGetAddonByName<AddonRepair>("Repair", out var addon) && addon->AtkUnitBase.IsVisible && addon->RepairAllButton->IsEnabled)
        {
            _throttle.Exec(() => new AddonMaster.Repair((IntPtr)addon).RepairAll());
        }
    }

    internal static void ConfirmYesNo()
    {
        if (GenericHelpers.TryGetAddonByName<AddonRepair>("Repair", out var r) &&
            r->AtkUnitBase.IsVisible && GenericHelpers.TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) &&
            addon->AtkUnitBase.IsVisible &&
            addon->YesButton is not null &&
            addon->YesButton->IsEnabled &&
            addon->AtkUnitBase.UldManager.NodeList[15]->IsVisible())
        {
            new AddonMaster.SelectYesno((IntPtr)addon).Yes();
        }
    }

    internal static bool HasDarkMatterOrBetter(uint darkMatterID)
    {
        var repairResources = Svc.Data.Excel.GetSheet<ItemRepairResource>()!;
        foreach (var dm in repairResources)
        {
            if (dm.Item.Row < darkMatterID)
                continue;

            if (InventoryManager.Instance()->GetInventoryItemCount(dm.Item.Row) > 0)
                return true;
        }
        return false;
    }

    internal static int GetNPCRepairPrice()
    {
        var output = 0;
        var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < equipment->Size; i++)
        {
            var item = equipment->GetInventorySlot(i);
            if (item != null && item->ItemId > 0)
            {
                var actualCond = Math.Round(item->Condition / (float)300, 2);
                if (actualCond < 100)
                {
                    var lvl = Svc.Data.GetExcelSheet<Item>()!.GetRow(item->ItemId)!.LevelEquip;
                    var condDif = (100 - actualCond) / 100;
                    var price = Math.Round(Svc.Data.GetExcelSheet<ItemRepairPrice>()!.GetRow(lvl)!.Unknown0 * condDif, 0, MidpointRounding.ToPositiveInfinity);
                    output += (int)price;
                }
            }
        }

        return output;
    }

    internal static int GetMinEquippedPercent()
    {
        var ret = ushort.MaxValue;
        var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < equipment->Size; i++)
        {
            var item = equipment->GetInventorySlot(i);
            if (item != null && item->ItemId > 0)
            {
                if (item->Condition < ret) ret = item->Condition;
            }
        }
        return (int)Math.Ceiling((double)ret / 300);
    }

    internal static bool CanRepairAny(int repairPercent = 0)
    {
        var equipment = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        for (var i = 0; i < equipment->Size; i++)
        {
            var item = equipment->GetInventorySlot(i);
            if (item != null && item->ItemId > 0)
            {
                if (CanRepairItem(item->ItemId) && item->Condition / 300 < (repairPercent > 0 ? repairPercent : 100))
                {
                    return true;
                }
            }
        }
        return false;
    }

    internal static bool CanRepairItem(uint ItemId)
    {
        var row = Svc.Data.GetExcelSheet<Item>()!.GetRow(ItemId)!;

        if (row.ClassJobRepair.Row > 0)
        {
            var actualJob = (Job)row.ClassJobRepair.Row;
            var repairItem = row.ItemRepair.Value!.Item;

            if (!HasDarkMatterOrBetter(repairItem.Row))
                return false;

            var jobLevel = JobLevel(actualJob);
            if (Math.Max(row.LevelEquip - 10, 1) <= jobLevel)
                return true;
        }

        return false;
    }

    public static unsafe int JobLevel(Job job) => PlayerState.Instance()->ClassJobLevels[Svc.Data.GetExcelSheet<ClassJob>()?.GetRow((uint)job)?.ExpArrayIndex ?? 0];

    internal static bool RepairWindowOpen()
    {
        if (GenericHelpers.TryGetAddonByName<AddonRepair>("Repair", out var repairAddon))
            return true;

        return false;
    }

    private static DateTime _nextRetry;

    internal static bool ProcessRepair(bool option)
    {
        if (!option) return true;

        if (GetMinEquippedPercent() >= Service.Config.Get<GatherRouteDB>().RepairPercent)
        {
            if (GenericHelpers.TryGetAddonByName<AddonRepair>("Repair", out var r) && r->AtkUnitBase.IsVisible)
            {
                if (DateTime.Now < _nextRetry) return false;
                if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied39])
                {
                    Svc.Log.Verbose("Repair visible");
                    Svc.Log.Verbose("Closing repair window");
                    UseRepair();
                }
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                return false;
            }
            return true;
        }

        if (DateTime.Now < _nextRetry) return false;

        if (GenericHelpers.TryGetAddonByName<AddonRepair>("Repair", out var repairAddon) && repairAddon->AtkUnitBase.IsVisible && repairAddon->RepairAllButton != null)
        {
            if (!repairAddon->RepairAllButton->IsEnabled)
            {
                UseRepair();
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
                return false;
            }

            if (!Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Occupied39])
            {
                ConfirmYesNo();
                Repair();
            }
            _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
            return false;
        }

        if (CanRepairAny())
        {
            if (!RepairWindowOpen() && !GenericHelpers.IsOccupied())
                UseRepair();
            _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(1000));
            return false;
        }

        return true;
    }
}
