using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.Logging;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static visland.Plugin;

namespace visland.Questing;
internal unsafe class ExecQuestJournalEvent
{
    internal static bool IsEnabled = false;
    internal static readonly uint[] CofferIcons = [26557, 26509, 26558, 26559, 26560, 26561, 26562, 25916, 26564, 26565, 26566, 26567,];
    internal static readonly uint[] GilIcons = [26001];
    internal static Random Random = new();

    internal static string[] CompleteStr = ["Complete", "完成", "Abschließen", "Accepter", "コンプリート"];

    internal static void Init()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "JournalAccept", Accept);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "JournalResult", OnJournalResultSetup);
        Svc.Framework.Update += Tick;
        IsEnabled = true;
    }

    internal static void Shutdown()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "JournalAccept", Accept);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "JournalResult", OnJournalResultSetup);
        Svc.Framework.Update -= Tick;
        IsEnabled = false;
    }

    internal static void Toggle() => (IsEnabled ? new System.Action(Shutdown) : Init)();

    private static void Accept(AddonEvent type, AddonArgs args)
    {
        if (IsEnabled)
        {
            if (args.AddonName == "JournalAccept")
                Callback.Fire((AtkUnitBase*)args.Addon, true, 3);
        }
    }

    internal static void Tick(IFramework framework)
    {
        var addon = Svc.GameGui.GetAddonByName("JournalResult", 1);
        if (addon == IntPtr.Zero)
        {
            return;
        }
        var questAddon = (AtkUnitBase*)addon;
        if (!GenericHelpers.IsAddonReady(questAddon)) return;
        if (questAddon->UldManager.NodeListCount <= 4) return;
        var buttonNode = (AtkComponentNode*)questAddon->UldManager.NodeList[4];
        if (buttonNode->Component->UldManager.NodeListCount <= 2) return;
        var textComponent = (AtkTextNode*)buttonNode->Component->UldManager.NodeList[2];
        if (!CompleteStr.Contains(Marshal.PtrToStringUTF8((IntPtr)textComponent->NodeText.StringPtr))) return;
        if (textComponent->AtkResNode.Color.A != 255) return;
        if (!((AddonJournalResult*)addon)->CompleteButton->IsEnabled) return;
        if (EzThrottler.Throttle("Complete"))
        {
            Svc.Log.Debug("Completing quest");
            Callback.Fire((AtkUnitBase*)addon, true, 1);
        }
    }

    private static void OnJournalResultSetup(AddonEvent type, AddonArgs args)
    {
        if (IsEnabled)
        {
            var canvas = (nint)((AtkUnitBase*)args.Addon)->UldManager.NodeList[7]->GetComponent();
            var r = new ReaderJournalResult((AtkUnitBase*)args.Addon);
            if (r.OptionalRewards.Count > 0)
            {
                PluginLog.Debug($"Preparing to select optional reward item. Candidates: ({r.OptionalRewards.Count})\n{r.OptionalRewards.Select(x => $"ID:{x.ItemID} / Icon:{x.IconID} / Amount:{x.Amount} / Name:{x.Name} ").Print("\n")}");
                foreach (var x in r.OptionalRewards)
                {
                    if (Svc.Data.GetExcelSheet<Item>()?.GetRow(x.ItemID) == null)
                    {
                        DuoLog.Warning($"Encountered unknown item id: {x.ItemID}. Selecting cancelled. Please report this error with logs and screenshot.");
                        return;
                    }
                }
                foreach (PickRewardMethod x in Enum.GetValues(typeof(PickRewardMethod)))
                {
                    {
                        if (x == PickRewardMethod.Gil_sacks && TrySelectGil(r.OptionalRewards, out var index))
                        {
                            PluginLog.Debug($"Selecting {index} = {r.OptionalRewards[index].Name} because it's gil sack");
                            Svc.Log.Debug($"[{nameof(visland)}] Auto-selected optional reward {index + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[index].Name} (gil)");
                            P.Memory.PickRewardItemUnsafe(canvas, index);
                            return;
                        }
                    }
                    {
                        if (x == PickRewardMethod.Highest_vendor_value && TrySelectHighestVendorValue(r.OptionalRewards, out var index))
                        {
                            PluginLog.Debug($"Selecting {index} = {r.OptionalRewards[index].Name} because it's highest vendor value");
                            Svc.Log.Debug($"[{nameof(visland)}] Auto-selected optional reward {index + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[index].Name} (highest value)");
                            P.Memory.PickRewardItemUnsafe(canvas, index);
                            return;
                        }
                    }
                    {
                        if (x == PickRewardMethod.Gear_coffer && TrySelectCoffer(r.OptionalRewards, out var index))
                        {
                            PluginLog.Debug($"Selecting {index} = {r.OptionalRewards[index].Name} because it's coffer");
                            Svc.Log.Debug($"[{nameof(visland)}] Auto-selected optional reward {index + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[index].Name} (coffer)");
                            P.Memory.PickRewardItemUnsafe(canvas, index);
                            return;
                        }
                    }
                    {
                        if (x == PickRewardMethod.Equipable_item_for_current_job && TrySelectCurrentJobItem(r.OptionalRewards, out var index))
                        {
                            PluginLog.Debug($"Selecting {index} = {r.OptionalRewards[index].Name} because it's current job item");
                            Svc.Log.Debug($"[{nameof(visland)}] Auto-selected optional reward {index + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[index].Name} (equipable)");
                            P.Memory.PickRewardItemUnsafe(canvas, index);
                            return;
                        }
                    }
                    {
                        if (x == PickRewardMethod.High_quality_gear && TrySelectHighQualityGear(r.OptionalRewards, out var index))
                        {
                            PluginLog.Debug($"Selecting {index} = {r.OptionalRewards[index].Name} because it's high quality gear item");
                            Svc.Log.Debug($"[{nameof(visland)}] Auto-selected optional reward {index + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[index].Name} (HQ gear item)");
                            P.Memory.PickRewardItemUnsafe(canvas, index);
                            return;
                        }
                    }
                }
                var rand = Random.Next(r.OptionalRewards.Count);
                PluginLog.Debug($"Selecting random reward: {rand} - {r.OptionalRewards[rand].Name}");
                Svc.Log.Debug($"[{nameof(visland)}] Auto-selected optional reward {rand + 1}/{r.OptionalRewards.Count}: {r.OptionalRewards[rand].Name} (random)");
                P.Memory.PickRewardItemUnsafe(canvas, rand);
                return;
            }
        }
    }

    internal static bool TrySelectCoffer(List<ReaderJournalResult.OptionalReward> data, out int index)
    {
        List<int> possible = [];
        for (var i = 0; i < data.Count; i++)
        {
            var d = data[i];
            if (CofferIcons.Contains(d.IconID))
            {
                possible.Add(i);
            }
        }
        if (possible.Count > 0)
        {
            index = possible[Random.Next(possible.Count)];
            return true;
        }
        index = default;
        return false;
    }

    internal static bool TrySelectGil(List<ReaderJournalResult.OptionalReward> data, out int index)
    {
        for (var i = 0; i < data.Count; i++)
        {
            var d = data[i];
            if (GilIcons.Contains(d.IconID))
            {
                index = i;
                return true;
            }
        }
        index = default;
        return false;
    }

    internal static bool TrySelectHighestVendorValue(List<ReaderJournalResult.OptionalReward> data, out int index)
    {
        var value = 0u;
        index = 0;
        for (var i = 0; i < data.Count; i++)
        {
            var d = data[i];
            var item = Svc.Data.GetExcelSheet<Item>()?.GetRow(d.ItemID);
            if (item != null && item.PriceLow * d.Amount > value)
            {
                index = i;
                value = item.PriceLow * d.Amount;
            }
        }
        return value > 0;
    }

    internal static bool TrySelectCurrentJobItem(List<ReaderJournalResult.OptionalReward> data, out int index)
    {
        List<int> possible = [];
        if (Player.Available)
        {
            for (var i = 0; i < data.Count; i++)
            {
                var d = data[i];
                var item = Svc.Data.GetExcelSheet<Item>()?.GetRow(d.ItemID);
                if (item != null && item.ClassJobCategory.Value != null && item.ClassJobCategory.Value.IsJobInCategory((Job)Player.Object.ClassJob.Id))
                {
                    possible.Add(i);
                }
            }
        }
        if (possible.Count > 0)
        {
            index = possible[Random.Next(possible.Count)];
            return true;
        }
        index = default;
        return false;
    }

    internal static readonly uint[] GearCats = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 40, 41, 42, 43, 84, 87, 88, 89, 96, 97, 98, 99, 105, 106, 107, 108, 109];
    internal static bool TrySelectHighQualityGear(List<ReaderJournalResult.OptionalReward> data, out int index)
    {
        List<int> possible = [];
        for (var i = 0; i < data.Count; i++)
        {
            var d = data[i];
            var item = Svc.Data.GetExcelSheet<Item>()?.GetRow(d.ItemID);
            if (d.IsHQ && item != null && item.ItemUICategory?.Value?.RowId.EqualsAny(GearCats) == true)
            {
                possible.Add(i);
            }
        }
        if (possible.Count > 0)
        {
            index = possible[Random.Next(possible.Count)];
            return true;
        }
        index = default;
        return false;
    }
}