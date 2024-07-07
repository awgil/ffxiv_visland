using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Data;
using Lumina.Excel.GeneratedSheets2;
using System.Collections.Generic;
using System.Linq;
using visland.Helpers;

namespace visland.Workshop;

public unsafe class WorkshopDebug
{
    private WorkshopSolver _solver = new();
    private UITree _tree = new();
    private WorkshopSolver.FavorState _favorState = new();
    private WorkshopSolverFavorSheet? _favorSolution;
    private string[] _itemNames;

    public WorkshopDebug()
    {
        _itemNames = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>()!.Select(o => o.Item.Value?.Name ?? "").ToArray();
    }

    public void Draw()
    {
        if (ImGui.Button("Clear current cycle"))
            WorkshopUtils.ClearCurrentCycleSchedule();
        ImGui.SameLine();
        if (ImGui.Button("Refresh favors/demand"))
            WorkshopUtils.RequestDemandFavors();

        var curWeek = WorkshopUtils.CurrentWeek();
        _tree.LeafNode($"Current week: #{curWeek.index}, started at {curWeek.startTime}");

        var ad = AgentMJICraftSchedule.Instance()->Data;
        var sheet = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>(Language.English)!;
        foreach (var na in _tree.Node($"Agent data: {(nint)ad:X}", ad == null))
        {
            _tree.LeafNode($"updatestate={ad->UpdateState}, level={ad->IslandLevel}");
            _tree.LeafNode($"addons: modal={ad->OpenedModalAddonHandle} ({ad->OpenedModalAddonId}, review={ad->ReviewMaterialsAddonHandle}, confirm={ad->ConfirmAddonHandle}");
            _tree.LeafNode($"setting: ws={ad->CurScheduleSettingWorkshop}, slot={ad->CurScheduleSettingStartingSlot}, item=#{ad->CurScheduleSettingCraftIndex}, numMats={ad->CurScheduleSettingNumMaterials}, init={ad->CurScheduleSettingMaterialsInitializedMask:X2}");
            _tree.LeafNode($"s/d: sort={ad->CurSupplyDemandSort:X}, time={ad->CurSupplyDemandFilterTime:X}, cat={ad->CurSupplyDemandFilterCategory:X}, cpop={ad->CurSupplyDemandFilterThisWeekPopularity:X}, npop={ad->CurSupplyDemandFilterNextWeekPopularity:X}, s={ad->CurSupplyDemandFilterSupply:X}, d={ad->CurSupplyDemandFilterDemandShift:X}, f={ad->CurSupplyDemandFilterFavors:X}");
            _tree.LeafNode($"ctx: sched={ad->CurContextMenuScheduleEntryWorkshop}/{ad->CurContextMenuScheduleEntrySlot}, sd={ad->CurContextMenuSupplyDemandRow}, preset={ad->CurContextMenuPresetIndex}");
            _tree.LeafNode($"groove={ad->Groove}, cur-cycle={ad->CycleDisplayed}, cur-hour={ad->HourSinceCycleStart}, in-progress={ad->CycleInProgress}");
            _tree.LeafNode($"rest mask={ad->RestCycles:X}, proposed={ad->NewRestCycles:X}, prompt={ad->ConfirmPrompt}");
            _tree.LeafNode($"flags1={ad->Flags1}");
            _tree.LeafNode($"flags2={ad->Flags2}");

            var i = 0;
            foreach (ref var w in ad->WorkshopSchedules)
                DrawWorkshopSchedule(ref w, $"Workshop {i++}");
            DrawWorkshopSchedule(ref ad->CopiedSchedule, "Workshop in clipboard");

            foreach (var n in _tree.Node("Raw crafts", ad->Crafts.LongCount == 0))
            {
                i = 0;
                foreach (ref readonly var item in ad->Crafts.AsSpan())
                {
                    foreach (var nn in _tree.Node($"Item {i++}: id={item.CraftObjectId} ({item.Name})"))
                    {
                        _tree.LeafNode($"Sheet data: itemid={item.ItemId}, level={item.LevelReq}, time={item.CraftingTime}, value={item.Value}");
                        _tree.LeafNode($"Indices: main={item.CraftIndex}, sorted={item.SortedByNameIndex}");
                        _tree.LeafNode($"Themes: num={item.NumThemes} [{item.ThemeIds[0]}, {item.ThemeIds[1]}, {item.ThemeIds[2]}]");
                        _tree.LeafNode($"Props: fav={item.Favor}, pop-cur={item.ThisWeekPopularity}, pop-next={item.NextWeekPopularity}, supply={item.Supply}, demand-shift={item.DemandShift}");
                    }
                }
            }
            foreach (var n in _tree.Node("Crafts per theme", ad->ThemeNames.LongCount == 0))
            {
                for (var j = 0; j < (int)ad->ThemeNames.LongCount; ++j)
                {
                    foreach (var nn in _tree.Node(ad->ThemeNames.AsSpan()[j].ToString(), ad->UnlockedObjectsPerTheme[j].LongCount == 0))
                    {
                        foreach (ref readonly var item in ad->UnlockedObjectsPerTheme[j].AsSpan())
                        {
                            _tree.LeafNode($"id={item.Value->CraftObjectId} ({sheet.GetRow(item.Value->CraftObjectId)?.Item.Value?.Name})");
                        }
                    }
                }
            }
            foreach (var n in _tree.Node("Crafts sorted by name", ad->CraftsSortedByName.LongCount == 0))
            {
                foreach (ref readonly var item in ad->CraftsSortedByName.AsSpan())
                {
                    _tree.LeafNode($"id={item.Value->CraftObjectId} ({sheet.GetRow(item.Value->CraftObjectId)?.Item.Value?.Name})");
                }
            }

            foreach (var n in _tree.Node($"Material allocation for cycle {ad->MaterialUse.Cycle}###matalloc"))
            {
                foreach (var nn in _tree.Node("Cycle"))
                    DrawMaterialAlloc(ref ad->MaterialUse.Entries[0]);
                foreach (var nn in _tree.Node("Week"))
                    DrawMaterialAlloc(ref ad->MaterialUse.Entries[1]);
                foreach (var nn in _tree.Node("Week + next"))
                    DrawMaterialAlloc(ref ad->MaterialUse.Entries[2]);
                foreach (var nn in _tree.Node("Workshop 1"))
                    for (var j = 0; j < 6; ++j)
                        _tree.LeafNode($"{ad->MaterialUse.StartingHours[j]} == {ad->MaterialUse.CraftIds[j]} '{sheet.GetRow(ad->MaterialUse.CraftIds[j])?.Item.Value?.Name}'");
                foreach (var nn in _tree.Node("Workshop 2"))
                    for (var j = 0; j < 6; ++j)
                        _tree.LeafNode($"{ad->MaterialUse.StartingHours[j + 6]} == {ad->MaterialUse.CraftIds[j + 6]} '{sheet.GetRow(ad->MaterialUse.CraftIds[j + 6])?.Item.Value?.Name}'");
                foreach (var nn in _tree.Node("Workshop 3"))
                    for (var j = 0; j < 6; ++j)
                        _tree.LeafNode($"{ad->MaterialUse.StartingHours[j + 12]} == {ad->MaterialUse.CraftIds[j + 12]} '{sheet.GetRow(ad->MaterialUse.CraftIds[j + 12])?.Item.Value?.Name}'");
                foreach (var nn in _tree.Node("Workshop 4"))
                    for (var j = 0; j < 6; ++j)
                        _tree.LeafNode($"{ad->MaterialUse.StartingHours[j + 18]} == {ad->MaterialUse.CraftIds[j + 18]} '{sheet.GetRow(ad->MaterialUse.CraftIds[j + 18])?.Item.Value?.Name}'");
            }
        }

        var mji = MJIManager.Instance();
        _tree.LeafNode($"Popularity: dirty={mji->DemandDirty}, req={mji->RequestDemandType} obj={mji->RequestDemandCraftId}");
        if (!mji->DemandDirty)
        {
            DrawPopularity("Curr", mji->CurrentPopularity);
            DrawPopularity("Next", mji->NextPopularity);
        }

        var favorsData = mji->FavorState;
        var dataAvail = favorsData != null ? favorsData->UpdateState : -1;
        foreach (var nf in _tree.Node($"Favors: avail={dataAvail}", dataAvail != 2))
        {
            DrawFavorState(0, "Prev");
            DrawFavorState(3, "This");
            DrawFavorState(6, "Next");
            DrawFavorSetup(0, 4, 8);
            DrawFavorSetup(1, 6, 6);
            DrawFavorSetup(2, 8, 8);
            ImGuiEx.TextV("Init from game week:");
            ImGui.SameLine();
            if (ImGui.Button("Fetch demand"))
                WorkshopUtils.RequestDemandFavors();
            ImGui.SameLine();
            if (ImGui.Button("Prev"))
                InitFavorsFromGame(0, -1);
            using (ImRaii.Disabled(mji->DemandDirty))
            {
                ImGui.SameLine();
                if (ImGui.Button("This"))
                    InitFavorsFromGame(3, mji->CurrentPopularity);
                ImGui.SameLine();
                if (ImGui.Button("Next"))
                    InitFavorsFromGame(6, mji->NextPopularity);
            }

            if (ImGui.Button("Solve!"))
                _favorSolution = new(_favorState);

            if (_favorSolution != null)
            {
                _tree.LeafNode($"Plan: {_favorSolution.Plan}");
                foreach (var n in _tree.Node("Links"))
                {
                    DrawLinked(_favorSolution.Favors[0], 4, _favorSolution.Links[0][0]);
                    DrawLinked(_favorSolution.Favors[0], 6, _favorSolution.Links[0][1]);
                    DrawLinked(_favorSolution.Favors[0], 8, _favorSolution.Links[0][2]);
                    DrawLinked(_favorSolution.Favors[1], 4, _favorSolution.Links[1][0]);
                    DrawLinked(_favorSolution.Favors[1], 6, _favorSolution.Links[1][1]);
                    DrawLinked(_favorSolution.Favors[1], 8, _favorSolution.Links[1][2]);
                    DrawLinked(_favorSolution.Favors[2], 4, _favorSolution.Links[2][0]);
                    DrawLinked(_favorSolution.Favors[2], 6, _favorSolution.Links[2][1]);
                    DrawLinked(_favorSolution.Favors[2], 8, _favorSolution.Links[2][2]);
                }
                foreach (var n in _tree.Node($"Solution ({_favorSolution.Recs.Count} cycles)", _favorSolution.Recs.Count == 0))
                {
                    var i = 0;
                    foreach (var r in _tree.Nodes(_favorSolution.Recs, r => new($"Schedule {i++}")))
                    {
                        _tree.LeafNodes(r.Slots, s => $"{s.Slot}: {s.CraftObjectId} '{sheet.GetRow(s.CraftObjectId)?.Item.Value?.Name}'");
                    }
                }
            }
        }
    }

    private void DrawWorkshopSchedule(ref AgentMJICraftSchedule.WorkshopData w, string tag)
    {
        foreach (var n in _tree.Node($"{tag}: {w.NumScheduleEntries} entries, {w.NumEfficientCrafts} eff, {w.UsedTimeSlots:X} used", w.NumScheduleEntries == 0))
        {
            for (var j = 0; j < w.NumScheduleEntries; ++j)
            {
                ref var e = ref w.EntryData[j];
                _tree.LeafNode($"Item {j}: {e.CraftObjectId} ({Service.LuminaRow<MJICraftworksObject>(e.CraftObjectId)?.Item.Value?.Name}), flags={e.Flags} startslot={e.StartingSlot}, dur={e.Duration}, started={e.Started}, efficient={e.Efficient}");
            }
        }
    }

    private void DrawMaterialAlloc(ref AgentMJICraftSchedule.MaterialAllocationEntry entry)
    {
        _tree.LeafNode($"index={entry.EntryIndex} unk={entry.uDC}");
        for (var i = 0; i < 109; ++i)
            if (entry.UsedAmounts[i] != 0)
                _tree.LeafNode($"{Service.LuminaRow<MJIItemPouch>((uint)i)?.Item.Value?.Name} = {entry.UsedAmounts[i]}");
    }

    private void DrawPopularity(string tag, byte index)
    {
        var sheetCraft = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>()!;
        var pop = Service.LuminaRow<MJICraftworksPopularity>(index)!;
        foreach (var np in _tree.Node($"{tag} popularity={index}"))
        {
            _tree.LeafNodes(sheetCraft.Where(o => o.RowId > 0), o => $"{o.RowId} '{o.Item.Value?.Name}' = {pop.Popularity[o.RowId].Value?.Ratio}");
        }
    }

    private void DrawFavorState(int offset, string tag)
    {
        var f = MJIManager.Instance()->FavorState;
        foreach (var n in _tree.Node($"{tag} favor state"))
        {
            for (var i = 0; i < 3; ++i)
            {
                var idx = f->CraftObjectIds[i + offset];
                _tree.LeafNode($"{idx} '{Service.LuminaRow<MJICraftworksObject>(idx)?.Item.Value?.Name}': delivered={f->NumDelivered[i + offset]}, scheduled={f->NumScheduled[i + offset]}, bonus={f->Bonus(i + offset)}, shipped={f->Shipped(i + offset)}");
            }
        }
    }

    private void DrawFavorSetup(int idx, int duration, int req)
    {
        var sheet = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>()!;
        ImGuiEx.TextV($"{duration}h:");
        ImGui.SameLine();
        UICombo.UInt($"###c{idx}", _itemNames, ref _favorState.CraftObjectIds[idx], i => i != 0 && sheet.GetRow(i)?.CraftingTime == duration);
        ImGui.SameLine();
        ImGui.DragInt($"###r{idx}", ref _favorState.CompletedCounts[idx], 0.03f, 0, req);
    }

    private void InitFavorsFromGame(int offset, int pop)
    {
        var state = MJIManager.Instance()->FavorState;
        for (var i = 0; i < 3; ++i)
        {
            _favorState.CraftObjectIds[i] = state->CraftObjectIds[i + offset];
            _favorState.CompletedCounts[i] = state->NumDelivered[i + offset] + state->NumScheduled[i + offset];
        }
        if (pop >= 0)
        {
            _favorState.Popularity.Set((uint)pop);
        }
    }

    private void DrawLinked(MJICraftworksObject obj, int duration, List<MJICraftworksObject> links)
    {
        foreach (var n in _tree.Node($"{duration}h linked to {obj.CraftingTime}h favor ({obj.Theme[0].Value?.Name}/{obj.Theme[1].Value?.Name})", links.Count == 0))
            _tree.Nodes(links, o => new($"{o.RowId} '{o.Item.Value?.Name}' {o.Theme[0].Value?.Name}/{o.Theme[1].Value?.Name} == {o.Value * _favorState.Popularity.Multiplier(o.RowId):f1}", true, _favorSolution!.Favors.Contains(o) ? 0xff00ff00 : 0xffffffff)).Count();
    }
}
