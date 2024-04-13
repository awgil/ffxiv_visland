using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Statuses;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Linq;
#nullable disable

namespace visland.Helpers;
public unsafe static class Player
{
    public static PlayerCharacter Object => Service.ClientState.LocalPlayer;
    public static bool Available => Service.ClientState.LocalPlayer != null;
    public static bool Interactable => Available && Object.IsTargetable;
    public static ulong CID => Service.ClientState.LocalContentId;
    public static StatusList Status => Service.ClientState.LocalPlayer.StatusList;
    public static string Name => Service.ClientState.LocalPlayer?.Name.ToString();
    public static int Level => Service.ClientState.LocalPlayer?.Level ?? 0;
    public static bool IsInHomeWorld => Service.ClientState.LocalPlayer.HomeWorld.Id == Service.ClientState.LocalPlayer.CurrentWorld.Id;
    public static string HomeWorld => Service.ClientState.LocalPlayer?.HomeWorld.GameData.Name.ToString();
    public static string CurrentWorld => Service.ClientState.LocalPlayer?.CurrentWorld.GameData.Name.ToString();
    public static Character* Character => (Character*)Service.ClientState.LocalPlayer.Address;
    public static BattleChara* BattleChara => (BattleChara*)Service.ClientState.LocalPlayer.Address;
    public static GameObject* GameObject => (GameObject*)Service.ClientState.LocalPlayer.Address;
    public static uint Territory => Service.ClientState.TerritoryType;
    public static bool Mounted => Service.Condition[ConditionFlag.Mounted];
    public static bool Mounting => Service.Condition[ConditionFlag.Unknown57]; // condition 57 is set while mount up animation is playing
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
}