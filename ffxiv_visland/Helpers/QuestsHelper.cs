using Dalamud.Game.ClientState.Conditions;
using Dalamud.Memory;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using visland.IPC;
using static ECommons.GenericHelpers;
using static visland.Plugin;

namespace visland.Helpers;

public class QuestsHelper
{
    private static readonly Dictionary<uint, Quest>? QuestSheet = Svc.Data?.GetExcelSheet<Quest>()?.Where(x => x.Id.RawString.Length > 0).ToDictionary(i => i.RowId, i => i);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public static nint itemContextMenuAgent = nint.Zero;
    public delegate void UseItemDelegate(nint itemContextMenuAgent, uint itemID, uint inventoryPage, uint inventorySlot, short a5);
    public static UseItemDelegate UseItem;

    public static nint emoteAgent = nint.Zero;
    public delegate void DoEmoteDelegate(nint agent, uint emoteID, long a3, bool a4, bool a5);
    public static DoEmoteDelegate DoEmote;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    private static Throttle _interact = new();

    public QuestsHelper()
    {
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

    public static string GetMobName(uint npcID) => Svc.Data.GetExcelSheet<BNpcName>()?.GetRow(npcID)?.Singular.RawString ?? "";

    public static bool IsQuestAccepted(int questID) => new QuestManager().IsQuestAccepted(((uint)questID).ToInternalID());
    public static bool IsQuestCompleted(int questID) => QuestManager.IsQuestComplete(((uint)questID).ToInternalID());
    public static byte GetQuestStep(int questID) => QuestManager.GetQuestSequence(((uint)questID).ToInternalID());
    public static unsafe bool HasQuest(int questID) => QuestManager.Instance()->NormalQuestsSpan.ToArray().ToList().Any(q => (q.QuestId ^ 65536) == questID);

    public static bool IsTodoChecked(int questID, int questStep, int objectiveIndex) => true; // TODO: might need to reverse the agent or something. Doing this by addon does not seem like a good idea

    public static unsafe void GetTo(int zoneID, Vector3 pos, float radius = 0f)
    {
        if (Player.Territory != zoneID)
            P.TaskManager.Enqueue(() => Telepo.Instance()->Teleport(Coordinates.GetNearestAetheryte(zoneID, pos), 0));
        P.TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.Casting] && !IsOccupied());
        P.TaskManager.Enqueue(() => NavmeshIPC.PathfindAndMoveTo(pos, false));
        P.TaskManager.Enqueue(() => !NavmeshIPC.PathIsRunning());
    }

    private static unsafe GameObject* FindObjectToInteractWith(uint InteractWithOID)
    {
        foreach (var obj in Service.ObjectTable.Where(o => o.DataId == InteractWithOID))
            return obj.IsTargetable ? (GameObject*)obj.Address : null;
        return null;
    }

    public static unsafe void TalkTo(uint npcOID)
    {
        var interactObj = !IsOccupied() ? FindObjectToInteractWith(npcOID) : null;
        if (interactObj != null)
            _interact.Exec(() => { Service.Log.Debug($"Attempting to talk to {npcOID}"); TargetSystem.Instance()->OpenObjectInteraction(interactObj); });
        P.TaskManager.Enqueue(() => TargetSystem.Instance()->OpenObjectInteraction(interactObj), $"Targeting {npcOID}");
        P.TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.OccupiedInQuestEvent], $"Waiting for !{nameof(ConditionFlag.OccupiedInQuestEvent)} with {npcOID}");
    }

    public static unsafe void PickUpQuest(int questID, uint npcOID)
    {
        if (HasQuest(questID)) return;

        P.TaskManager.Enqueue(() => TalkTo(npcOID), $"{nameof(TalkTo)}: {npcOID}");
        P.TaskManager.Enqueue(() =>
        {
            if (TryGetAddonByName<AtkUnitBase>("SelectIconString", out var addon) && addon->IsVisible)
            {
                var quests = new List<string>();
                for (var i = 0; i < addon->AtkValuesCount; i++)
                {
                    if (!(addon->AtkValues[i].Type == FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String)) continue;
                    quests.Add(MemoryHelper.ReadSeStringNullTerminated(new nint(addon->AtkValues[i].String)).ToString());
                }
                var index = quests.IndexOf(GetNameOfQuest((ushort)questID)) - 1; // the first string element in SelectIconString is always an empty string
                Callback.Fire(addon, true, index);
                return true;
            }
            if (Svc.GameGui.GetAddonByName("JournalAccept") != IntPtr.Zero)
                return true;
            return false;
        }, "Waiting for SelectIconString or JournalAccept");
    }

    public static unsafe void TurnInQuest(int questID, uint npcOID, uint itemID = 0, bool allowHQ = false, int rewardSlot = -1)
    {
        Svc.Log.Info($"checking is quest complete on {questID} {IsQuestCompleted(questID)}");
        if (IsQuestCompleted(questID)) return;

        P.TaskManager.Enqueue(() => TalkTo(npcOID), $"{nameof(TalkTo)}: {npcOID}");
        P.TaskManager.Enqueue(() =>
        {
            if (TryGetAddonByName<AtkUnitBase>("SelectIconString", out var addon) && addon->IsVisible)
            {
                var quests = new List<string>();
                for (var i = 0; i < addon->AtkValuesCount; i++)
                {
                    if (!(addon->AtkValues[i].Type == FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String)) continue;
                    quests.Add(MemoryHelper.ReadSeStringNullTerminated(new nint(addon->AtkValues[i].String)).ToString());
                }
                var index = quests.IndexOf(GetNameOfQuest((ushort)questID)) - 1; // the first string element in SelectIconString is always an empty string
                Callback.Fire(addon, true, index);
                return true;
            }
            if (Svc.GameGui.GetAddonByName("JournalResult") != IntPtr.Zero)
                return true;
            return false;
        }, "Waiting for SelectIconString or JournalResult");
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

    public static unsafe void EmoteAt(uint emoteID, uint targetOID = 0)
    {
        if (targetOID != 0)
        {
            var obj = FindObjectToInteractWith(targetOID);
            if (obj != null)
                Service.TargetManager.Target = Service.ObjectTable.CreateObjectReference((nint)obj);
        }

        DoEmote(emoteAgent, emoteID, 0, true, true);
    }

    public static unsafe void UseAction(uint actionID, uint targetOID = 0)
    {
        if (targetOID != 0)
        {
            var obj = FindObjectToInteractWith(targetOID);
            if (obj != null)
                Service.TargetManager.Target = Service.ObjectTable.CreateObjectReference((nint)obj);
        }
        try
        {
            ActionManager.Instance()->UseAction(ActionType.Action, actionID, targetOID);
        }
        catch (Exception e) { e.Log(); }
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

    public static unsafe void AutoEquip(bool updateGearset = false)
    {
        if (Svc.Condition[ConditionFlag.InCombat]) return;
        if (Svc.Condition[ConditionFlag.BetweenAreas]) return;
        var mod = RecommendEquipModule.Instance();
        P.TaskManager.DelayNext("EquipMod", 500);
        P.TaskManager.Enqueue(() => mod->SetupRecommendedGear(), 500);
        P.TaskManager.Enqueue(mod->EquipRecommendedGear, 500);

        if (updateGearset)
        {
            var id = RaptureGearsetModule.Instance()->CurrentGearsetIndex;
            P.TaskManager.DelayNext("UpdatingGS", 1000);
            P.TaskManager.Enqueue(() => RaptureGearsetModule.Instance()->UpdateGearset(id));
        }
    }

    public static unsafe void Grind(string mobName, Func<bool> stopCondition)
    {
        if (stopCondition()) return;
        if (mobName.IsNullOrEmpty()) return;
        var mob = Svc.Objects.FirstOrDefault(o => o.IsTargetable && !o.IsDead && o.Name.TextValue.EqualsIgnoreCase(mobName));
        if (mob != null)
        {
            Svc.Log.Info($"found {mobName} @ {mob.Position}");
            GetTo(Svc.ClientState.TerritoryType, mob.Position, mob.HitboxRadius);
            P.TaskManager.Enqueue(() => Svc.Targets.Target = mob);
            P.TaskManager.Enqueue(() => BossModIPC.SetAutorotationState?.InvokeAction(true));
            P.TaskManager.Enqueue(() => BossModIPC.InitiateCombat?.InvokeAction());
            P.TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.InCombat]);
            P.TaskManager.Enqueue(() => BossModIPC.SetAutorotationState?.InvokeAction(false));
            P.TaskManager.Enqueue(() => Grind(mobName, stopCondition), $"{nameof(Grind)}: {mobName}");
        }
        else
            Svc.Log.Info($"Failed to find {mobName} nearby");
    }

    public static unsafe bool HasItem(int itemID, int quantity = 1) => InventoryManager.Instance()->GetInventoryItemCount((uint)itemID, true) >= quantity;

    public static unsafe void BuyItem(uint itemID, int quantity, uint npcID)
    {
        var locations = ItemVendorLocation.GetVendorLocations(itemID);
        foreach (var loc in locations)
        {
            if (!Coordinates.HasAetheryteInZone(loc.TerritoryType)) continue;
            P.TaskManager.Enqueue(() => GetTo((int)loc.TerritoryType, new Vector3(loc.X, 0, loc.Y)));
            break;
        }
        P.TaskManager.Enqueue(() => TalkTo(npcID), $"{nameof(TalkTo)}: {npcID}");
        // TODO: implement purchasing
    }
}

public static class StringExtensions
{
    public static string GetLast(this string source, int tail_length) => tail_length >= source.Length ? source : source[^tail_length..];
}

public static class QuestIDExtensions
{
    public static uint ToInternalID(this uint questid) => questid % 65536;

    public static uint ToSheetID(this uint questid) => questid ^ 65536;
}