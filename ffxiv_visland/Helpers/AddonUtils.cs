using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace visland.Helpers;

public static unsafe class AddonUtils {
    public static bool TryGetAddonByName<T>(string name, out T* addon) where T : unmanaged {
        addon = (T*)Service.GameGui.GetAddonByName(name).Address;
        return addon != null;
    }

    public static bool TryGetAddonByName(string name, out AtkUnitBase* addon) {
        addon = (AtkUnitBase*)Service.GameGui.GetAddonByName(name).Address;
        return addon != null;
    }

    public static bool IsOccupied()
        => Service.Condition[ConditionFlag.OccupiedInQuestEvent] ||
        Service.Condition[ConditionFlag.OccupiedInEvent] ||
        Service.Condition[ConditionFlag.OccupiedSummoningBell] ||
        Service.Condition[ConditionFlag.Occupied39] ||
        Service.Condition[ConditionFlag.Crafting] ||
        Service.Condition[ConditionFlag.PreparingToCraft] ||
        Service.Condition[ConditionFlag.ExecutingGatheringAction];

    public static bool IsAddonReady(AtkUnitBase* addon) => addon != null && addon->IsVisible && addon->IsReady;
}

public static unsafe class AtkCallback {
    public static void Fire(string addonName, bool checkVisibility, params int[] values) {
        if (AddonUtils.TryGetAddonByName<AtkUnitBase>(addonName, out var addon)) {
            Fire(addon, checkVisibility, values);
        }
    }

    public static void Fire(AtkUnitBase* addon, bool checkVisibility, params int[] values) {
        if (addon == null)
            return;
        if (checkVisibility && !addon->IsVisible)
            return;

        var atkValues = stackalloc AtkValue[values.Length];
        for (var i = 0; i < values.Length; ++i) {
            atkValues[i].Type = AtkValueType.Int;
            atkValues[i].Int = values[i];
        }

        addon->FireCallback((uint)values.Length, atkValues);
    }
}
