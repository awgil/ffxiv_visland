using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Bindings.ImGui;
using Lumina.Data;
using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;
using visland.Helpers;
using System;

namespace visland.Workshop;

public unsafe class WorkshopDebug {
    private readonly UITree _tree = new();
    private WorkshopSolver.FavourState _favourState = new();
    private WorkshopSolverFavourSheet? _favourSolution;
    private readonly string[] _itemNames;

    public WorkshopDebug() {
        _itemNames = [.. MJICraftworksObject.Get().Select(o => o.Item.Value.Name.ToString())];
    }

    public void Draw() {
        if (ImGui.Button("Clear current cycle"))
            WorkshopUtils.ClearCurrentCycleSchedule();
        ImGui.SameLine();
        if (ImGui.Button("Refresh favours/demand"))
            WorkshopUtils.RequestDemandFavours();

        ImGui.SameLine();
        if (ImGui.Button("Void 2nd rest (this week)"))
            WorkshopUtils.VoidSecondRestThisWeek();

        var curWeek = WorkshopUtils.CurrentWeek();
        _tree.LeafNode($"Current week: #{curWeek.index}, started at {curWeek.startTime}");

        var ad = AgentMJICraftSchedule.Instance()->Data;
        var sheet = MJICraftworksObject.Get(Dalamud.Game.ClientLanguage.English);
        foreach (var na in _tree.Node($"Agent data: {(nint)ad:X}", ad == null)) {
            _tree.LeafNode($"updatestate={ad->UpdateState}, level={ad->IslandLevel}");
            _tree.LeafNode($"addons: modal={ad->OpenedModalAddonHandle} ({ad->OpenedModalAddonId}, review={ad->ReviewMaterialsAddonHandle}, confirm={ad->ConfirmAddonHandle}");
            _tree.LeafNode($"setting: ws={ad->CurScheduleSettingWorkshop}, slot={ad->CurScheduleSettingStartingSlot}, item=#{ad->CurScheduleSettingCraftIndex}, numMats={ad->CurScheduleSettingNumMaterials}, init={ad->CurScheduleSettingMaterialsInitializedMask:X2}");
            _tree.LeafNode($"s/d: sort={ad->CurSupplyDemandSort:X}, time={ad->CurSupplyDemandFilterTime:X}, cat={ad->CurSupplyDemandFilterCategory:X}, cpop={ad->CurSupplyDemandFilterThisWeekPopularity:X}, npop={ad->CurSupplyDemandFilterNextWeekPopularity:X}, s={ad->CurSupplyDemandFilterSupply:X}, d={ad->CurSupplyDemandFilterDemandShift:X}, f={ad->CurSupplyDemandFilterFavors:X}");
            _tree.LeafNode($"ctx: sched={ad->CurContextMenuScheduleEntryWorkshop}/{ad->CurContextMenuScheduleEntrySlot}, sd={ad->CurContextMenuSupplyDemandRow}, preset={ad->CurContextMenuPresetIndex}");
            _tree.LeafNode($"groove={ad->Groove}, cur-cycle={ad->CycleDisplayed}, cur-hour={ad->HourSinceCycleStart}, in-progress={ad->CycleInProgress}");
            _tree.LeafNode($"rest mask={ad->RestCycles:X}, proposed={ad->NewRestCycles:X}, prompt={ad->ConfirmPrompt}");
            _tree.LeafNode($"flags1={ad->Flags1}");
            _tree.LeafNode($"flags2={ad->Flags2}");
            _tree.LeafNode($"CraftworksRestDays: [{string.Join(", ", WorkshopUtils.GetCurrentRestCycles())}]");

            var i = 0;
            foreach (ref var w in ad->WorkshopSchedules)
                DrawWorkshopSchedule(ref w, $"Workshop {i++}");
            DrawWorkshopSchedule(ref ad->CopiedSchedule, "Workshop in clipboard");

            foreach (var n in _tree.Node("Raw crafts", ad->Crafts.LongCount == 0)) {
                i = 0;
                foreach (ref readonly var item in ad->Crafts.AsSpan()) {
                    foreach (var nn in _tree.Node($"Item {i++}: id={item.CraftObjectId} ({item.Name})")) {
                        _tree.LeafNode($"Sheet data: itemid={item.ItemId}, level={item.LevelReq}, time={item.CraftingTime}, value={item.Value}");
                        _tree.LeafNode($"Indices: main={item.CraftIndex}, sorted={item.SortedByNameIndex}");
                        _tree.LeafNode($"Themes: num={item.NumThemes} [{item.ThemeIds[0]}, {item.ThemeIds[1]}, {item.ThemeIds[2]}]");
                        _tree.LeafNode($"Props: fav={item.Favor}, pop-cur={item.ThisWeekPopularity}, pop-next={item.NextWeekPopularity}, supply={item.Supply}, demand-shift={item.DemandShift}");
                    }
                }
            }
            foreach (var n in _tree.Node("Crafts per theme", ad->ThemeNames.LongCount == 0)) {
                for (var j = 0; j < (int)ad->ThemeNames.LongCount; ++j) {
                    foreach (var nn in _tree.Node(ad->ThemeNames.AsSpan()[j].ToString(), ad->UnlockedObjectsPerTheme[j].LongCount == 0)) {
                        foreach (ref readonly var item in ad->UnlockedObjectsPerTheme[j].AsSpan()) {
                            _tree.LeafNode($"id={item.Value->CraftObjectId} ({sheet.GetRow(item.Value->CraftObjectId).Item.Value.Name})");
                        }
                    }
                }
            }
            foreach (var n in _tree.Node("Crafts sorted by name", ad->CraftsSortedByName.LongCount == 0)) {
                foreach (ref readonly var item in ad->CraftsSortedByName.AsSpan()) {
                    _tree.LeafNode($"id={item.Value->CraftObjectId} ({sheet.GetRow(item.Value->CraftObjectId).Item.Value.Name})");
                }
            }

            foreach (var n in _tree.Node($"Material allocation for cycle {ad->MaterialUse.Cycle}###matalloc")) {
                foreach (var nn in _tree.Node("Cycle"))
                    DrawMaterialAlloc(ref ad->MaterialUse.Entries[0]);
                foreach (var nn in _tree.Node("Week"))
                    DrawMaterialAlloc(ref ad->MaterialUse.Entries[1]);
                foreach (var nn in _tree.Node("Week + next"))
                    DrawMaterialAlloc(ref ad->MaterialUse.Entries[2]);
                foreach (var nn in _tree.Node("Workshop 1"))
                    for (var j = 0; j < 6; ++j)
                        _tree.LeafNode($"{ad->MaterialUse.StartingHours[j]} == {ad->MaterialUse.CraftIds[j]} '{sheet.GetRow(ad->MaterialUse.CraftIds[j]).Item.Value.Name}'");
                foreach (var nn in _tree.Node("Workshop 2"))
                    for (var j = 0; j < 6; ++j)
                        _tree.LeafNode($"{ad->MaterialUse.StartingHours[j + 6]} == {ad->MaterialUse.CraftIds[j + 6]} '{sheet.GetRow(ad->MaterialUse.CraftIds[j + 6]).Item.Value.Name}'");
                foreach (var nn in _tree.Node("Workshop 3"))
                    for (var j = 0; j < 6; ++j)
                        _tree.LeafNode($"{ad->MaterialUse.StartingHours[j + 12]} == {ad->MaterialUse.CraftIds[j + 12]} '{sheet.GetRow(ad->MaterialUse.CraftIds[j + 12]).Item.Value.Name}'");
                foreach (var nn in _tree.Node("Workshop 4"))
                    for (var j = 0; j < 6; ++j)
                        _tree.LeafNode($"{ad->MaterialUse.StartingHours[j + 18]} == {ad->MaterialUse.CraftIds[j + 18]} '{sheet.GetRow(ad->MaterialUse.CraftIds[j + 18]).Item.Value.Name}'");
            }
        }

        var mji = MJIManager.Instance();
        _tree.LeafNode($"Popularity: dirty={mji->DemandDirty}, req={mji->RequestDemandType} obj={mji->RequestDemandCraftId}");
        if (!mji->DemandDirty) {
            DrawPopularity("Curr", mji->CurrentPopularity);
            DrawPopularity("Next", mji->NextPopularity);
        }

        var favoursData = mji->FavorState;
        var dataAvail = favoursData != null ? favoursData->UpdateState : -1;
        foreach (var nf in _tree.Node($"Favours: avail={dataAvail}", dataAvail != 2)) {
            DrawFavourState(0, "Prev");
            DrawFavourState(3, "This");
            DrawFavourState(6, "Next");
            DrawFavourSetup(0, 4, 8);
            DrawFavourSetup(1, 6, 6);
            DrawFavourSetup(2, 8, 8);
            ImGui.TextV("Init from game week:");
            ImGui.SameLine();
            if (ImGui.Button("Fetch demand"))
                WorkshopUtils.RequestDemandFavours();
            ImGui.SameLine();
            if (ImGui.Button("Prev"))
                InitFavoursFromGame(0, -1);
            using (ImRaii.Disabled(mji->DemandDirty)) {
                ImGui.SameLine();
                if (ImGui.Button("This"))
                    InitFavoursFromGame(3, mji->CurrentPopularity);
                ImGui.SameLine();
                if (ImGui.Button("Next"))
                    InitFavoursFromGame(6, mji->NextPopularity);
            }

            if (ImGui.Button("Solve!"))
                _favourSolution = new(_favourState);

            if (_favourSolution != null) {
                _tree.LeafNode($"Plan: {_favourSolution.Plan}");
                foreach (var n in _tree.Node("Links")) {
                    DrawLinked(_favourSolution.Favours[0], 4, _favourSolution.Links[0][0]);
                    DrawLinked(_favourSolution.Favours[0], 6, _favourSolution.Links[0][1]);
                    DrawLinked(_favourSolution.Favours[0], 8, _favourSolution.Links[0][2]);
                    DrawLinked(_favourSolution.Favours[1], 4, _favourSolution.Links[1][0]);
                    DrawLinked(_favourSolution.Favours[1], 6, _favourSolution.Links[1][1]);
                    DrawLinked(_favourSolution.Favours[1], 8, _favourSolution.Links[1][2]);
                    DrawLinked(_favourSolution.Favours[2], 4, _favourSolution.Links[2][0]);
                    DrawLinked(_favourSolution.Favours[2], 6, _favourSolution.Links[2][1]);
                    DrawLinked(_favourSolution.Favours[2], 8, _favourSolution.Links[2][2]);
                }
                foreach (var n in _tree.Node($"Solution ({_favourSolution.Recs.Count} cycles)", _favourSolution.Recs.Count == 0)) {
                    var i = 0;
                    foreach (var r in _tree.Nodes(_favourSolution.Recs, r => new($"Schedule {i++}"))) {
                        _tree.LeafNodes(r.Slots, s => $"{s.Slot}: {s.CraftObjectId} '{sheet.GetRow(s.CraftObjectId).Item.Value.Name}'");
                    }
                }
            }
        }
    }

    private void DrawWorkshopSchedule(ref AgentMJICraftSchedule.WorkshopData w, string tag) {
        foreach (var n in _tree.Node($"{tag}: {w.NumScheduleEntries} entries, {w.NumEfficientCrafts} eff, {w.UsedTimeSlots:X} used", w.NumScheduleEntries == 0)) {
            for (var j = 0; j < w.NumScheduleEntries; ++j) {
                ref var e = ref w.EntryData[j];
                _tree.LeafNode($"Item {j}: {e.CraftObjectId} ({MJICraftworksObject.GetRow(e.CraftObjectId)?.Item.Value.Name}), flags={e.Flags} startslot={e.StartingSlot}, dur={e.Duration}, started={e.Started}, efficient={e.Efficient}");
            }
        }
    }

    private void DrawMaterialAlloc(ref AgentMJICraftSchedule.MaterialAllocationEntry entry) {
        _tree.LeafNode($"index={entry.EntryIndex} unk={entry.uDC}");
        for (var i = 0; i < 109; ++i)
            if (entry.UsedAmounts[i] != 0)
                _tree.LeafNode($"{MJIItemPouch.GetRow((uint)i)?.Item.Value.Name} = {entry.UsedAmounts[i]}");
    }

    private void DrawPopularity(string tag, byte index) {
        var pop = MJICraftworksPopularity.GetRow(index)!;
        foreach (var np in _tree.Node($"{tag} popularity={index}")) {
            _tree.LeafNodes(MJICraftworksObject.Get().Where(o => o.RowId > 0), o => {
                if (o.RowId < pop.Value.Popularity.Count)
                    return $"{o.RowId} '{o.Item.Value.Name}' = {pop.Value.Popularity[(int)o.RowId].Value.Ratio}";
                else
                    return $"{o.RowId} '{o.Item.Value.Name}' = (idx={o.RowId} out of range={pop.Value.Popularity.Count - 1}. Please report)";
            });
        }
    }

    private void DrawFavourState(int offset, string tag) {
        var f = MJIManager.Instance()->FavorState;
        foreach (var n in _tree.Node($"{tag} favour state")) {
            for (var i = 0; i < 3; ++i) {
                var idx = f->CraftObjectIds[i + offset];
                _tree.LeafNode($"{idx} '{MJICraftworksObject.GetRow(idx)?.Item.Value.Name}': delivered={f->NumDelivered[i + offset]}, scheduled={f->NumScheduled[i + offset]}, bonus={f->Bonus(i + offset)}, shipped={f->Shipped(i + offset)}");
            }
        }
    }

    private void DrawFavourSetup(int idx, int duration, int req) {
        ImGui.TextV($"{duration}h:");
        ImGui.SameLine();
        UICombo.UInt($"###c{idx}", _itemNames, ref _favourState.CraftObjectIds[idx], i => i != 0 && MJICraftworksObject.GetRow(i)?.CraftingTime == duration);
        ImGui.SameLine();
        ImGui.DragInt($"###r{idx}", ref _favourState.CompletedCounts[idx], 0.03f, 0, req);
    }

    private void InitFavoursFromGame(int offset, int pop) {
        var state = MJIManager.Instance()->FavorState;
        for (var i = 0; i < 3; ++i) {
            _favourState.CraftObjectIds[i] = state->CraftObjectIds[i + offset];
            _favourState.CompletedCounts[i] = state->NumDelivered[i + offset] + state->NumScheduled[i + offset];
        }
        if (pop >= 0) {
            _favourState.Popularity.Set((uint)pop);
        }
    }

    private void DrawLinked(MJICraftworksObject obj, int duration, List<MJICraftworksObject> links) {
        foreach (var n in _tree.Node($"{duration}h linked to {obj.CraftingTime}h favour ({obj.Theme[0].Value.Name}/{obj.Theme[1].Value.Name})", links.Count == 0))
            _tree.Nodes(links, o => new($"{o.RowId} '{o.Item.Value.Name}' {o.Theme[0].Value.Name}/{o.Theme[1].Value.Name} == {o.Value * _favourState.Popularity.Multiplier(o.RowId):f1}", true, _favourSolution!.Favours.Contains(o) ? 0xff00ff00 : 0xffffffff)).Count();
    }
}
