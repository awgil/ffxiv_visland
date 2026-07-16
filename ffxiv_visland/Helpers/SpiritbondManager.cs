using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace visland.Helpers;

public static unsafe class SpiritbondManager {
    public static bool UseMateriaExtraction() => ActionManager.Instance()->UseAction(ActionType.GeneralAction, 14);
    public static ushort Weapon => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[0].SpiritbondOrCollectability;
    public static ushort Offhand => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[1].SpiritbondOrCollectability;
    public static ushort Helm => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[2].SpiritbondOrCollectability;
    public static ushort Body => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[3].SpiritbondOrCollectability;
    public static ushort Hands => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[4].SpiritbondOrCollectability;
    public static ushort Legs => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[6].SpiritbondOrCollectability;
    public static ushort Feet => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[7].SpiritbondOrCollectability;
    public static ushort Earring => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[8].SpiritbondOrCollectability;
    public static ushort Neck => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[9].SpiritbondOrCollectability;
    public static ushort Wrist => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[10].SpiritbondOrCollectability;
    public static ushort Ring1 => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[11].SpiritbondOrCollectability;
    public static ushort Ring2 => InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems)->Items[12].SpiritbondOrCollectability;

    public static bool IsSpiritbondReadyAny() {
        if (!QuestManager.IsQuestComplete(66174)) return false;

        if (Weapon == 10000) return true;
        if (Offhand == 10000) return true;
        if (Helm == 10000) return true;
        if (Body == 10000) return true;
        if (Hands == 10000) return true;
        if (Legs == 10000) return true;
        if (Feet == 10000) return true;
        if (Earring == 10000) return true;
        if (Neck == 10000) return true;
        if (Wrist == 10000) return true;
        if (Ring1 == 10000) return true;
        if (Ring2 == 10000) return true;

        return false;
    }

    public static bool IsMateriaMenuOpen() => Service.GameGui.GetAddonByName("Materialize", 1) != IntPtr.Zero;

    public static bool IsMateriaMenuDialogOpen() => Service.GameGui.GetAddonByName("MaterializeDialog", 1) != IntPtr.Zero;
    public static unsafe void OpenMateriaMenu() {
        if (Service.GameGui.GetAddonByName("Materialize", 1) == IntPtr.Zero)
            UseMateriaExtraction();
    }

    public static unsafe void CloseMateriaMenu() {
        if (Service.GameGui.GetAddonByName("Materialize", 1) != IntPtr.Zero)
            UseMateriaExtraction();
    }

    public static unsafe void ConfirmMateriaDialog() {
        try {
            AtkCallback.Fire("MaterializeDialog", true, 0);
        }
        catch {
        }
    }

    private static DateTime _nextRetry;

    public static unsafe bool ExtractMateriaTask() {
        if (IsMateriaMenuOpen() && !IsSpiritbondReadyAny()) {
            if (DateTime.Now < _nextRetry) return false;
            CloseMateriaMenu();
            _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(500));
            return false;
        }

        if (IsSpiritbondReadyAny()) {
            if (DateTime.Now < _nextRetry) return false;
            if (!IsMateriaMenuOpen()) {
                OpenMateriaMenu();
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(500));
                return false;
            }

            if (IsMateriaMenuOpen() && !AddonUtils.IsOccupied()) {
                ExtractFirstMateria();
                _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(500));
                return false;
            }

            _nextRetry = DateTime.Now.Add(TimeSpan.FromMilliseconds(500));
            return false;
        }

        return true;
    }

    public static unsafe void ExtractFirstMateria() {
        try {
            if (IsSpiritbondReadyAny()) {
                if (IsMateriaMenuDialogOpen()) {
                    ConfirmMateriaDialog();
                }
                else {
                    var materializePTR = Service.GameGui.GetAddonByName("Materialize", 1);
                    if (materializePTR == IntPtr.Zero)
                        return;

                    var materalizeWindow = (AtkUnitBase*)materializePTR.Address;
                    if (materalizeWindow == null)
                        return;

                    var values = stackalloc AtkValue[2];
                    values[0] = new() {
                        Type = AtkValueType.Int,
                        Int = 2,
                    };
                    values[1] = new() {
                        Type = AtkValueType.UInt,
                        UInt = 0,
                    };

                    materalizeWindow->FireCallback(1, values);
                }
            }
        }
        catch (Exception e) {
            Service.Log.Error(e, "Failed to extract materia");
        }
    }
}
