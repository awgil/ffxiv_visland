using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Statuses;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Linq;
using visland.Gathering;
#nullable disable

namespace visland.Helpers;
public unsafe static class PlayerEx
{
    public static IPlayerCharacter Object => Service.ClientState.LocalPlayer;
    public static bool Available => Service.ClientState.LocalPlayer != null;
    public static bool Interactable => Available && Object.IsTargetable;
    public static ulong CID => Service.ClientState.LocalContentId;
    public static StatusList Status => Service.ClientState.LocalPlayer.StatusList;
    public static string Name => Service.ClientState.LocalPlayer?.Name.ToString();
    public static int Level => Service.ClientState.LocalPlayer?.Level ?? 0;
    public static bool IsInHomeWorld => Service.ClientState.LocalPlayer.HomeWorld.Value.RowId == Service.ClientState.LocalPlayer.CurrentWorld.Value.RowId;
    public static string HomeWorld => Service.ClientState.LocalPlayer?.HomeWorld.Value.Name.ToString();
    public static string CurrentWorld => Service.ClientState.LocalPlayer?.CurrentWorld.Value.Name.ToString();
    public static Character* Character => (Character*)Service.ClientState.LocalPlayer.Address;
    public static BattleChara* BattleChara => (BattleChara*)Service.ClientState.LocalPlayer.Address;
    public static GameObject* GameObject => (GameObject*)Service.ClientState.LocalPlayer.Address;
    public static uint Territory => Service.ClientState.TerritoryType;
    public static bool Mounted => Service.Condition[ConditionFlag.Mounted];
    public static bool Mounting => Service.Condition[ConditionFlag.Unknown57]; // condition 57 is set while mount up animation is playing

    public static unsafe bool Dismounting => **(byte**)(Service.ClientState.LocalPlayer.Address + 1400) == 1;
    public static bool Jumping => Service.Condition[ConditionFlag.Jumping] || Service.Condition[ConditionFlag.Jumping61];
    public static bool OnIsland => MJIManager.Instance()->IsPlayerInSanctuary == 1;
    public static bool Normal => Service.Condition[ConditionFlag.NormalConditions];
    public static bool ExclusiveFlying => Service.Condition[ConditionFlag.InFlight];
    public static bool InclusiveFlying => Service.Condition[ConditionFlag.InFlight] || Service.Condition[ConditionFlag.Diving];
    public static bool InWater => Service.Condition[ConditionFlag.Swimming] || Service.Condition[ConditionFlag.Diving];
    public static float SprintCD => Status.FirstOrDefault(s => s.StatusId == 50)?.RemainingTime ?? 0;
    public static uint FlyingControlType
    {
        get => Svc.GameConfig.UiControl.GetUInt("FlyingControlType");
        set => Svc.GameConfig.Set(Dalamud.Game.Config.UiControlOption.FlyingControlType, value);
    }
    public static bool HasFoodBuff => Status.Any(x => x.StatusId == 48); // Well Fed buff
    public static float FoodCD => Status.FirstOrDefault(s => s.StatusId == 48)?.RemainingTime ?? 0;
    public static bool HasManual => Status.Any(x => x.StatusId == 49);
    public static float ManualCD => Status.FirstOrDefault(s => s.StatusId == 49)?.RemainingTime ?? 0;
    //public static float CordialCD => ActionManager.Instance()->GetActionStatus(ActionType.Item, cordial.Id) == 0;
    public static float AnimationLock => ActionManager.Instance()->AnimationLock;
    public static bool InGatheringAnimation => Svc.Condition[ConditionFlag.Gathering42];

    public static uint Gp => Object.CurrentGp;
    public static uint MaxGp => Object.MaxGp;
    public static int Gathering => PlayerState.Instance()->Attributes[72];
    public static int Perception => PlayerState.Instance()->Attributes[73];
    public static (string Name, uint Id, ushort GP) BestCordial
    {
        get
        {
            var im = InventoryManager.Instance();
            for (var cont = InventoryType.Inventory1; cont <= InventoryType.Inventory4; cont++)
            {
                foreach (var cordial in DataStore.Cordials)
                {
                    if (im->GetItemCountInContainer(cordial.Id + 1_000_000, cont) > 0)
                        return DataStore.Cordials.First(x => x.Id == cordial.Id + 1_000_000);
                    else if (im->GetItemCountInContainer(cordial.Id, cont) > 0)
                        return DataStore.Cordials.First(x => x.Id == cordial.Id);
                }
            }
            return (string.Empty, 0, 0);
        }
    }

    public static Job Job => GetJob(Svc.ClientState.LocalPlayer);
    public static Job GetJob(this IPlayerCharacter pc) => (Job)pc.ClassJob.RowId;

    public static unsafe void EatFood(int id)
    {
        if (InventoryManager.Instance()->GetInventoryItemCount((uint)id) > 0)
            _action.Exec(() => AgentInventoryContext.Instance()->UseItem((uint)id));
        else if (InventoryManager.Instance()->GetInventoryItemCount((uint)id, true) > 0)
            _action.Exec(() => AgentInventoryContext.Instance()->UseItem((uint)id + 1_000_000));
    }

    public static void Mount() => ExecuteActionSafe(ActionType.GeneralAction, 24); // flying mount roulette
    public static void Dismount() => ExecuteActionSafe(ActionType.GeneralAction, 23);
    public static void Jump() => ExecuteActionSafe(ActionType.GeneralAction, 2);
    public static void Sprint()
    {
        if (Mounted) return;

        if (OnIsland && SprintCD < 5)
            ExecuteActionSafe(ActionType.Action, 31314);

        if (!OnIsland && SprintCD == 0)
            ExecuteActionSafe(ActionType.GeneralAction, 4);
    }
    public static void RevealNode() => ExecuteActionSafe(ActionType.Action, GatheringActions.GetCurrentSurveyAbility());
    public static void DrinkCordial() => ExecuteActionSafe(ActionType.Item, BestCordial.Id, 65535);
    public static bool SwitchJob(Job job)
    {
        if (Job == job) return true;
        var gearsets = RaptureGearsetModule.Instance();
        foreach (ref var gs in gearsets->Entries)
        {
            if (!RaptureGearsetModule.Instance()->IsValidGearset(gs.Id)) continue;
            if ((Job)gs.ClassJob == job)
            {
                PluginLog.Debug($"Switching from {Job} to {job} (gs: {gs.Id}/{gs.NameString})");
                return gearsets->EquipGearset(gs.Id) == 0;
            }
        }
        return false;
    }
    public static bool HasFood(uint foodId) => InventoryManager.Instance()->GetInventoryItemCount(foodId) > 0 || InventoryManager.Instance()->GetInventoryItemCount(foodId, true) > 0;

    private static readonly Throttle _action = new();
    private static unsafe void ExecuteActionSafe(ActionType type, uint id, uint extraParam = 0) => _action.Exec(() => ActionManager.Instance()->UseAction(type, id, extraParam: extraParam));
}