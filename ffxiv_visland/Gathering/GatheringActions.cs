using ECommons;
using ECommons.ExcelServices;
using ECommons.Logging;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using System.Linq;
using visland.Helpers;
using static visland.Plugin;

namespace visland.Gathering;

internal abstract class Actions
{
    // collectables
    public abstract (uint id, ushort buff) Scour { get; set; }
    public abstract uint Collect { get; set; }
    public abstract uint Meticulous { get; set; }
    public abstract uint Scrutiny { get; set; }
    public abstract uint Brazen { get; set; }
    public abstract uint PrimingTouch { get; set; }

    public abstract (uint id, ushort buff) GatheringRateUpI { get; set; } // sharp vision / field mastery
    public abstract (uint id, ushort buff) GatheringRateUpII { get; set; }
    public abstract (uint id, ushort buff) GatheringRateUpIII { get; set; }
    public abstract (uint id, ushort buff) GiftOfTheLandI { get; set; } // mountaineer's gift / pioneer's gift
    public abstract (uint id, ushort buff) GiftOfTheLandII { get; set; }
    public abstract (uint id, ushort buff) GatheringRateUpLimited { get; set; } // Clear Vision / Flora Mastery
    public abstract (uint id, ushort buff) GatheringYieldUpI { get; set; } // King's Yield / Blessed Harvest
    public abstract (uint id, ushort buff) GatheringYieldUpII { get; set; }
    public abstract (uint id, ushort buff) GatheringYieldUpLimited { get; set; } // 1 upgrades to 2 via trait, they're not separate
    public abstract (uint id, ushort buff) Luck { get; set; }
    public abstract (uint id, ushort buff) TwelvesBounty { get; set; }
    public abstract (uint id, ushort buff) GivingLand { get; set; }
    public abstract (uint id, ushort buff) RestoreIntegrity { get; set; } // Solid Reasoning / Ageless Words
    public abstract (uint id, ushort buff) WiseToTheWorld { get; set; }
    public abstract (uint id, ushort buff) Tidings { get; set; }
    public abstract (uint id, ushort buff) SurveyI { get; set; } // Lay of the Land / Arbor Call
    public abstract (uint id, ushort buff) SurveyII { get; set; }
}

internal class GatheringActions
{
    internal class BTNActions : Actions
    {
        public override (uint id, ushort buff) Scour { get; set; } = (22186, Buffs.Scrutiny);
        public override uint Collect { get; set; } = 815;
        public override uint Meticulous { get; set; } = 22188;
        public override uint Scrutiny { get; set; } = 22189;
        public override uint Brazen { get; set; } = 22187;
        public override uint PrimingTouch { get; set; } = 34872;

        public override (uint id, ushort buff) GatheringRateUpI { get; set; } = (218, Buffs.GatheringRateUp);
        public override (uint id, ushort buff) GatheringRateUpII { get; set; } = (220, Buffs.GatheringRateUp);
        public override (uint id, ushort buff) GatheringRateUpIII { get; set; } = (294, Buffs.GatheringRateUp);
        public override (uint id, ushort buff) GiftOfTheLandI { get; set; } = (21178, Buffs.GiftOfTheLand);
        public override (uint id, ushort buff) GiftOfTheLandII { get; set; } = (25590, Buffs.GiftOfTheLandII);
        public override (uint id, ushort buff) GatheringRateUpLimited { get; set; } = (4086, Buffs.GatheringRateUpLimited);
        public override (uint id, ushort buff) GatheringYieldUpI { get; set; } = (222, Buffs.GatheringYieldUp);
        public override (uint id, ushort buff) GatheringYieldUpII { get; set; } = (224, Buffs.GatheringYieldUp);
        public override (uint id, ushort buff) GatheringYieldUpLimited { get; set; } = (273, Buffs.GatheringYieldUpII);
        public override (uint id, ushort buff) Luck { get; set; } = (4095, Buffs.None);
        public override (uint id, ushort buff) TwelvesBounty { get; set; } = (282, Buffs.TwelvesBounty);
        public override (uint id, ushort buff) GivingLand { get; set; } = (4590, Buffs.GivingLand);
        public override (uint id, ushort buff) RestoreIntegrity { get; set; } = (215, Buffs.None);
        public override (uint id, ushort buff) WiseToTheWorld { get; set; } = (26522, Buffs.EurekaMoment);
        public override (uint id, ushort buff) Tidings { get; set; } = (21204, Buffs.GatherersBounty);
        public override (uint id, ushort buff) SurveyI { get; set; } = (211, Buffs.ArborCall);
        public override (uint id, ushort buff) SurveyII { get; set; } = (290, Buffs.ArborCallII);
    }

    internal class MINActions : Actions
    {
        public override (uint id, ushort buff) Scour { get; set; } = (22182, Buffs.Scrutiny);
        public override uint Collect { get; set; } = 240;
        public override uint Meticulous { get; set; } = 22184;
        public override uint Scrutiny { get; set; } = 22185;
        public override uint Brazen { get; set; } = 22183;
        public override uint PrimingTouch { get; set; } = 34871;

        public override (uint id, ushort buff) GatheringRateUpI { get; set; } = (235, Buffs.GatheringRateUp);
        public override (uint id, ushort buff) GatheringRateUpII { get; set; } = (237, Buffs.GatheringRateUp);
        public override (uint id, ushort buff) GatheringRateUpIII { get; set; } = (295, Buffs.GatheringRateUp);
        public override (uint id, ushort buff) GiftOfTheLandI { get; set; } = (21177, Buffs.GiftOfTheLand);
        public override (uint id, ushort buff) GiftOfTheLandII { get; set; } = (25589, Buffs.GiftOfTheLandII);
        public override (uint id, ushort buff) GatheringRateUpLimited { get; set; } = (4072, Buffs.GatheringRateUpLimited);
        public override (uint id, ushort buff) GatheringYieldUpI { get; set; } = (239, Buffs.GatheringYieldUp);
        public override (uint id, ushort buff) GatheringYieldUpII { get; set; } = (241, Buffs.GatheringYieldUp);
        public override (uint id, ushort buff) GatheringYieldUpLimited { get; set; } = (272, Buffs.GatheringYieldUpII);
        public override (uint id, ushort buff) Luck { get; set; } = (4081, Buffs.None);
        public override (uint id, ushort buff) TwelvesBounty { get; set; } = (280, Buffs.TwelvesBounty);
        public override (uint id, ushort buff) GivingLand { get; set; } = (4589, Buffs.GivingLand);
        public override (uint id, ushort buff) RestoreIntegrity { get; set; } = (232, Buffs.None);
        public override (uint id, ushort buff) WiseToTheWorld { get; set; } = (26521, Buffs.EurekaMoment);
        public override (uint id, ushort buff) Tidings { get; set; } = (21203, Buffs.GatherersBounty);
        public override (uint id, ushort buff) SurveyI { get; set; } = (228, Buffs.LayOfTheLand);
        public override (uint id, ushort buff) SurveyII { get; set; } = (291, Buffs.LayOfTheLandII);
    }

    internal static class Buffs
    {
        public const ushort None = 0;
        public const ushort Scrutiny = 757;
        public const ushort GivingLand = 1802;
        public const ushort TwelvesBounty = 825;
        public const ushort GatheringRateUp = 218;
        public const ushort GatheringRateUpLimited = 754;
        public const ushort GiftOfTheLand = 2666;
        public const ushort GiftOfTheLandII = 759;
        public const ushort GatheringYieldUp = 219; // used for permanent yield 1 and 2 (how do you tell them apart?)
        public const ushort GatheringYieldUpII = 1286; // temporary yield
        public const ushort EurekaMoment = 2765;
        public const ushort GatherersBounty = 2667; // tidings
        public const ushort LayOfTheLand = 234;
        public const ushort LayOfTheLandII = 243;
        public const ushort ArborCall = 233;
        public const ushort ArborCallII = 242;
    }

    public static unsafe void UseNextBestAction(AddonMaster.Gathering am, AddonMaster.Gathering.GatheredItem item)
    {
        if (P.TaskManager.IsBusy) return;
        var action = GetNextBestAction(am, item);
        if (action == 0)
        {
            PluginLog.Information($"Gathering {item.ItemName}");
            item.Gather();
        }
        else
        {
            PluginLog.Information($"Using {GenericHelpers.GetRow<Action>(action)?.Name}");
            P.TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.Action, action));
        }
    }

    public static unsafe uint GetNextBestAction(AddonMaster.Gathering am, AddonMaster.Gathering.GatheredItem item)
    {
        if (item.IsCollectable) return 0;

        Actions actions = PlayerEx.Job == Job.MIN ? new MINActions() : new BTNActions();

        if (!item.IsEnabled && CanUse(actions.Luck))
            return actions.Luck.id;

        if (ItemIsCrystal(item.ItemID))
        {
            if (CanUse(actions.GivingLand))
                return actions.GivingLand.id;
            if (CanUse(actions.TwelvesBounty))
                return actions.TwelvesBounty.id;
            return 0;
        }

        if (item.GatherChance <= 50 && CanUse(actions.GatheringRateUpIII))
            return actions.GatheringRateUpIII.id;
        if (item.GatherChance <= 85 && CanUse(actions.GatheringRateUpII))
            return actions.GatheringRateUpII.id;
        if (item.GatherChance <= 95 && CanUse(actions.GatheringRateUpI))
            return actions.GatheringRateUpI.id;

        if (item.BoonChance <= 70 && CanUse(actions.GiftOfTheLandII))
            return actions.GiftOfTheLandII.id;
        if (item.BoonChance <= 90 && CanUse(actions.GiftOfTheLandI))
            return actions.GiftOfTheLandI.id;

        if (CanUse(actions.GatheringYieldUpII))
            return actions.GatheringYieldUpII.id;
        if (CanUse(actions.GatheringYieldUpI))
            return actions.GatheringYieldUpI.id;

        if (CanUse(actions.GatheringYieldUpLimited))
            return actions.GatheringYieldUpLimited.id;

        if (CanUse(actions.Tidings))
            return actions.Tidings.id;

        if (am.CurrentIntegrity < am.TotalIntegrity && CanUse(actions.RestoreIntegrity))
            return actions.RestoreIntegrity.id;
        if (am.CurrentIntegrity < am.TotalIntegrity && CanUse(actions.WiseToTheWorld))
            return actions.WiseToTheWorld.id;

        return 0;
    }

    public static unsafe void UseNextBestAction(AddonMaster.GatheringMasterpiece am)
    {
        if (P.TaskManager.IsBusy) return;
        var action = GetNextBestAction(am);
        if (action == 0) return;

        PluginLog.Information($"Using {GenericHelpers.GetRow<Action>(action)?.Name}");
        P.TaskManager.Enqueue(() => ActionManager.Instance()->UseAction(ActionType.Action, action));
    }

    public static unsafe uint GetNextBestAction(AddonMaster.GatheringMasterpiece am)
    {
        Actions actions = PlayerEx.Job == Job.MIN ? new MINActions() : new BTNActions();

        if (ActionManager.Instance()->GetActionStatus(ActionType.Action, actions.Scour.id) == 0 ||
            ActionManager.Instance()->GetActionStatus(ActionType.Action, actions.Collect) == 0)
        {
            bool scrutiny = HasScrutiny();
            if (am.CurrentCollectability == 1000 || am.CurrentIntegrity == 1)
                return actions.Collect;

            if (am.CurrentCollectability + am.MeticulousPower >= am.MaxCollectability)
                return actions.Meticulous;

            if (am.CurrentCollectability + am.ScourPower >= am.MaxCollectability)
                return actions.Scour.id;

            if (PlayerEx.Gp >= 200 && !scrutiny)
                return actions.Scrutiny;

            if (scrutiny)
                return actions.Meticulous;

            return actions.Scour.id;
        }

        return 0;
    }

    public static uint GetCurrentSurveyAbility(bool highest = true)
    {
        Actions actions = PlayerEx.Job == Job.MIN ? new MINActions() : new BTNActions();
        return PlayerEx.Job switch
        {
            Job.MIN => highest ? actions.SurveyII.id : actions.SurveyI.id,
            Job.BTN => highest ? actions.SurveyII.id : actions.SurveyI.id,
            Job.FSH => highest ? 7905u : 7904u,
            _ => 0,
        };
    }

    private static unsafe bool CanUse((uint Id, ushort buff) action)
    {
        if (!UIState.Instance()->IsUnlockLinkUnlockedOrQuestCompleted(GenericHelpers.GetRow<Action>(action.Id)!.Value.UnlockLink.RowId)) return false;
        if (PlayerEx.Object.CurrentGp < ActionManager.GetActionCost(ActionType.Action, action.Id, 0, 0, 0, 0)) return false;
        if (action.buff != 0 && PlayerEx.Status.Any(x => x.StatusId == action.buff)) return false;
        return true;
    }

    private static bool HasScrutiny() => PlayerEx.Status.Any(x => x.StatusId == Buffs.Scrutiny);

    private static bool ItemIsCrystal(uint itemId) => itemId >= 2 && itemId <= 19;
}
