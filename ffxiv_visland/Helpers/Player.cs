using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;

namespace visland.Helpers;

public static unsafe class Player {
    [MemberNotNullWhen(true, nameof(Object))]
    public static bool Available => Service.ObjectTable.LocalPlayer is { IsDead: false };
    public static IGameObject? Object => Service.ObjectTable.LocalPlayer;
    public static Vector3 Position => Object?.Position ?? default;
    public static ulong CID => Service.PlayerState.ContentId;
    public static uint Job => Service.PlayerState.ClassJob.RowId;
    public static uint Territory => Service.ClientState.TerritoryType;
    public static bool Mounted => Service.Condition[ConditionFlag.Mounted];
    public static bool Mounting => Service.Condition[ConditionFlag.Mounting];
    public static bool IsJumping => Service.Condition[ConditionFlag.Jumping];
    public static bool IsCasting => Service.Condition[ConditionFlag.Casting];
    public static IEnumerable<IStatus> Status => Service.ObjectTable.LocalPlayer?.StatusList is { } list ? list : [];
    public static bool Normal => Service.Condition[ConditionFlag.NormalConditions];
    public static bool ExclusiveFlying => Service.Condition[ConditionFlag.InFlight];
    public static bool InclusiveFlying => Service.Condition[ConditionFlag.InFlight] || Service.Condition[ConditionFlag.Diving];
    public static bool InWater => Service.Condition[ConditionFlag.Swimming] || Service.Condition[ConditionFlag.Diving];
    public static float SprintCD => Status.FirstOrDefault(s => s.StatusId == 50)?.RemainingTime ?? 0;
    public static bool StellarSprinting => Status.Any(x => x.StatusId == 4398);
    public static bool HasFoodBuff => Status.Any(x => x.StatusId == 48);
    public static float FoodCD => Status.FirstOrDefault(s => s.StatusId == 48)?.RemainingTime ?? 0;
    public static float AnimationLock => ActionManager.Instance()->AnimationLock;
    public static bool InGatheringAnimation => Service.Condition[ConditionFlag.ExecutingGatheringAction];
    public static uint Gp => Service.ObjectTable.LocalPlayer?.CurrentGp ?? 0;
    public static uint MaxGp => Service.ObjectTable.LocalPlayer?.MaxGp ?? 0;
    public static int Gathering => PlayerState.Instance()->Attributes[72];
    public static int Perception => PlayerState.Instance()->Attributes[73];
    public static bool IsOnIsland => MJIManager.Instance() != null && MJIManager.Instance()->IsPlayerInSanctuary;

    public static float DistanceTo(Vector3 pos) => Object == null ? float.MaxValue : Vector3.Distance(Object.Position, pos);

    public static void EatFood(int id) {
        if (InventoryManager.Instance()->GetInventoryItemCount((uint)id) > 0)
            _action.Exec(() => AgentInventoryContext.Instance()->UseItem((uint)id));
        else if (InventoryManager.Instance()->GetInventoryItemCount((uint)id, true) > 0)
            _action.Exec(() => AgentInventoryContext.Instance()->UseItem((uint)id + 1_000_000));
    }

    public static void Mount() => ExecuteActionSafe(ActionType.GeneralAction, 24);
    public static void Dismount() => ExecuteActionSafe(ActionType.GeneralAction, 23);
    public static void Jump() => ExecuteActionSafe(ActionType.GeneralAction, 2);

    public static void Sprint() {
        if (Mounted || StellarSprinting) return;
        if (IsOnIsland && SprintCD < 5)
            ExecuteActionSafe(ActionType.Action, 31314);
        if (!IsOnIsland && SprintCD == 0)
            ExecuteActionSafe(ActionType.GeneralAction, 4);
    }

    public static void RevealNode() => ExecuteActionSafe(ActionType.Action, visland.Gathering.GatheringActions.GetCurrentSurveyAbility());

    public static bool SwitchJob(uint classJobId) {
        if (Job == classJobId) return true;
        var gearsets = RaptureGearsetModule.Instance();
        foreach (ref var gs in gearsets->Entries) {
            if (!gearsets->IsValidGearset(gs.Id)) continue;
            if (gs.ClassJob == classJobId) {
                Service.Log.Debug($"Switching from {Job} to {classJobId} (gs: {gs.Id}/{gs.NameString})");
                return gearsets->EquipGearset(gs.Id) == 0;
            }
        }
        return false;
    }

    public static int JobLevel(uint classJobId) => Service.PlayerState.GetClassJobLevel(ClassJob.GetRow(classJobId)!.Value);

    public static bool HasFood(uint foodId)
        => InventoryManager.Instance()->GetInventoryItemCount(foodId) > 0 || InventoryManager.Instance()->GetInventoryItemCount(foodId, true) > 0;

    private static void ExecuteActionSafe(ActionType type, uint id, uint extraParam = 0)
        => _action.Exec(() => ActionManager.Instance()->UseAction(type, id, extraParam: extraParam));

    private static readonly Throttle _action = new();
}
