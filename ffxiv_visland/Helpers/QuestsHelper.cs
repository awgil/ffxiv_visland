using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static ECommons.GenericHelpers;

namespace visland.Helpers;

public class QuestsHelper
{
    // functions needed
    // PickupQuest: questID, npcID, pos
    // TurnInQuest: questID, itemID, npcID, allowHQ, pos, rewardSlot
    // EquipSpecificItem or EquipRecommended?
    // Wait
    // IfCondition
    // WaitForCondition
    // HandOver: itemID, npcID, requiresHQ, pos, questID, stepID
    // TalkTo: npcID, pos
    // EmoteAt: npcID, pos
    // ArtisanMakeList
    // ArtisanExecuteList
    // ArtisanDeleteList
    // if we handle Talk skipping and quest pickup natively, we need to not conflict with YesAlready or TextAdvance
    private static readonly Dictionary<uint, Quest>? QuestSheet = Svc.Data?.GetExcelSheet<Quest>()?.Where(x => x.Id.RawString.Length > 0).ToDictionary(i => i.RowId, i => i);

    public static nint itemContextMenuAgent = nint.Zero;
    public delegate void UseItemDelegate(nint itemContextMenuAgent, uint itemID, uint inventoryPage, uint inventorySlot, short a5);
    public static UseItemDelegate UseItem;

    public static nint emoteAgent = nint.Zero;
    public delegate void DoEmoteDelegate(nint agent, uint emoteID, long a3, bool a4, bool a5);
    public static DoEmoteDelegate DoEmote;

    protected ECommons.Automation.TaskManager tm;

    public QuestsHelper()
    {
        tm = new();
        unsafe
        {
            try
            {
                var agentModule = Framework.Instance()->GetUiModule()->GetAgentModule();
                try
                {
                    DoEmote = Marshal.GetDelegateForFunctionPointer<DoEmoteDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? B8 0A 00 00 00"));
                    emoteAgent = (nint)agentModule->GetAgentByInternalId(AgentId.Emote);
                }
                catch { Service.Log.Error($"Failed to load {nameof(emoteAgent)}"); }

                try
                {
                    UseItem = Marshal.GetDelegateForFunctionPointer<UseItemDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 89 7C 24 38"));
                    itemContextMenuAgent = (nint)agentModule->GetAgentByInternalId(AgentId.InventoryContext);
                }
                catch { Service.Log.Error($"Failed to load {nameof(itemContextMenuAgent)}"); }
            }
            catch { Service.Log.Error($"Failed to load agentModule"); }
        }
    }

    public static bool IsQuestAccepted(ushort questID) => new QuestManager().IsQuestAccepted(questID);
    public static bool IsQuestComplete(ushort questID) => QuestManager.IsQuestComplete(questID);
    public static byte GetCurrentQuestSequence(ushort questID) => QuestManager.GetQuestSequence(questID);

    private static unsafe GameObject* GetObjectToInteractWith(uint objID)
    {
        if (Service.ObjectTable.TryGetFirst(x => x.DataId == objID, out var obj) && obj != null)
            return obj.IsTargetable ? (GameObject*)obj.Address : null;
        return null;
    }

    public static void PickUpQuest(ushort questID, uint npcOID)
    {
        unsafe
        {
            var obj = GetObjectToInteractWith(npcOID);
            if (obj != null)
                TargetSystem.Instance()->InteractWithObject(obj, false);
        }
    }

    public static void TurnInQuest(ushort questID, uint npcOID, uint itemID = 0, bool allowHQ = false, int rewardSlot = -1)
    {
        unsafe
        {
            var obj = GetObjectToInteractWith(npcOID);
            if (obj != null)
                TargetSystem.Instance()->InteractWithObject(obj, false);
        }
    }

    public static void UseItemOn(uint itemID, uint targetOID = 0)
    {
        if (targetOID != 0)
        {
            Service.ObjectTable.TryGetFirst(x => x.TargetObjectId == targetOID, out var obj);
            if (obj != null)
            {
                Service.TargetManager.Target = obj;
            }
        }

        UseItem(itemContextMenuAgent, itemID, 9999, 0, 0);
    }

    public static void EmoteAt(uint emoteID, uint targetOID = 0)
    {
        if (targetOID != 0)
        {
            unsafe
            {
                var obj = GetObjectToInteractWith(targetOID);
                if (obj != null)
                    Service.TargetManager.Target = Service.ObjectTable.CreateObjectReference((nint)obj);
            }
        }

        DoEmote(emoteAgent, emoteID, 0, true, true);
    }

    public static string GetNameOfQuest(ushort questID)
    {
        if (questID > 0)
        {
            var digits = questID.ToString().Length;
            if (QuestSheet!.Any(x => Convert.ToInt16(x.Value.Id.RawString.GetLast(digits)) == questID))
            {
                return QuestSheet!.First(x => Convert.ToInt16(x.Value.Id.RawString.GetLast(digits)) == questID).Value.Name.RawString.Replace("", "").Trim();
            }
        }
        return "";
    }

    public static bool SwitchJobGearset(uint cjID)
    {
        if (Svc.ClientState.LocalPlayer!.ClassJob.Id == cjID) return true;
        var gs = GetGearsetForClassJob(cjID);
        if (gs is null) return true;

        Chat chat = new();
        chat.SendMessage($"/gearset change {gs.Value + 1}");

        return true;
    }

    private static unsafe byte? GetGearsetForClassJob(uint cjId)
    {
        var gearsetModule = RaptureGearsetModule.Instance();
        for (var i = 0; i < 100; i++)
        {
            var gearset = gearsetModule->GetGearset(i);
            if (gearset == null) continue;
            if (!gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            if (gearset->ID != i) continue;
            if (gearset->ClassJob == cjId) return gearset->ID;
        }
        return null;
    }

    public unsafe void AutoEquip(uint? jobId, bool updateGearset = false)
    {
        if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat]) return;
        if (Svc.Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]) return;
        var mod = RecommendEquipModule.Instance();
        tm.DelayNext("EquipMod", 500);
        tm.Enqueue(() => mod->SetupRecommendedGear(), 500);
        tm.Enqueue(mod->EquipRecommendedGear, 500);

        if (updateGearset)
        {
            var id = RaptureGearsetModule.Instance()->CurrentGearsetIndex;
            tm.DelayNext("UpdatingGS", 1000);
            tm.Enqueue(() => RaptureGearsetModule.Instance()->UpdateGearset(id));
        }
    }
}

public static class StringExtensions
{
    public static string GetLast(this string source, int tail_length) => tail_length >= source.Length ? source : source[^tail_length..];
}