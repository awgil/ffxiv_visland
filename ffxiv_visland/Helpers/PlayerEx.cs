using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Linq;
using visland.Gathering;
#nullable disable

namespace visland.Helpers;
public unsafe static class PlayerEx
{
    extension(Player)
    {
        public static bool Normal => Service.Condition[ConditionFlag.NormalConditions];
		public static bool ExclusiveFlying => Service.Condition[ConditionFlag.InFlight];
		public static bool InclusiveFlying => Service.Condition[ConditionFlag.InFlight] || Service.Condition[ConditionFlag.Diving];
		public static bool InWater => Service.Condition[ConditionFlag.Swimming] || Service.Condition[ConditionFlag.Diving];
		public static float SprintCD => Player.Status.FirstOrDefault(s => s.StatusId == 50)?.RemainingTime ?? 0;
		public static bool StellarSprinting => Player.Status.Any(x => x.StatusId == 4398);
		public static uint FlyingControlType
		{
			get => Svc.GameConfig.UiControl.GetUInt("FlyingControlType");
			set => Svc.GameConfig.Set(Dalamud.Game.Config.UiControlOption.FlyingControlType, value);
		}
		public static bool HasFoodBuff => Player.Status.Any(x => x.StatusId == 48); // Well Fed buff
		public static float FoodCD => Player.Status.FirstOrDefault(s => s.StatusId == 48)?.RemainingTime ?? 0;
		public static bool HasManual => Player.Status.Any(x => x.StatusId == 49);
		public static float ManualCD => Player.Status.FirstOrDefault(s => s.StatusId == 49)?.RemainingTime ?? 0;
		//public static float CordialCD => ActionManager.Instance()->GetActionStatus(ActionType.Item, cordial.Id) == 0;
		public static float AnimationLock => ActionManager.Instance()->AnimationLock;
		public static bool InGatheringAnimation => Svc.Condition[ConditionFlag.ExecutingGatheringAction];

		public static uint Gp => Player.Object.CurrentGp;
		public static uint MaxGp => Player.Object.MaxGp;
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
			if (Player.Mounted || Player.StellarSprinting) return;

			if (Player.IsOnIsland && Player.SprintCD < 5)
				ExecuteActionSafe(ActionType.Action, 31314);

			if (!Player.IsOnIsland && Player.SprintCD == 0)
				ExecuteActionSafe(ActionType.GeneralAction, 4);
		}
		public static void RevealNode() => ExecuteActionSafe(ActionType.Action, GatheringActions.GetCurrentSurveyAbility());
		public static void DrinkCordial() => ExecuteActionSafe(ActionType.Item, Player.BestCordial.Id, 65535);
		public static bool SwitchJob(Job job)
		{
			if (Player.Job == job) return true;
			var gearsets = RaptureGearsetModule.Instance();
			foreach (ref var gs in gearsets->Entries)
			{
				if (!RaptureGearsetModule.Instance()->IsValidGearset(gs.Id)) continue;
				if ((Job)gs.ClassJob == job)
				{
					PluginLog.Debug($"Switching from {Player.Job} to {job} (gs: {gs.Id}/{gs.NameString})");
					return gearsets->EquipGearset(gs.Id) == 0;
				}
			}
			return false;
		}
		public static bool HasFood(uint foodId) => InventoryManager.Instance()->GetInventoryItemCount(foodId) > 0 || InventoryManager.Instance()->GetInventoryItemCount(foodId, true) > 0;

		
		private static unsafe void ExecuteActionSafe(ActionType type, uint id, uint extraParam = 0) => _action.Exec(() => ActionManager.Instance()->UseAction(type, id, extraParam: extraParam));
	}
	private static readonly Throttle _action = new();
}