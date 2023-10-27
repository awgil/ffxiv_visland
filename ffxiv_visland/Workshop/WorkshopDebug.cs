using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using ImGuiNET;
using Lumina.Data;
using Lumina.Excel.GeneratedSheets2;
using System.Collections.Generic;
using System.Linq;
using visland.Helpers;

namespace visland.Workshop;

public unsafe class WorkshopDebug
{
    private WorkshopSchedule _sched;
    private WorkshopFavors _favors;
    private WorkshopSolver _solver = new();
    private UITree _tree = new();
    private WorkshopSolver.FavorState _favorState = new();
    private WorkshopSolverFavorSheet? _favorSolution;
    private string[] _itemNames;

    public WorkshopDebug(WorkshopSchedule sched, WorkshopFavors favors)
    {
        _sched = sched;
        _favors = favors;
        _itemNames = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>()!.Select(o => o.Item.Value?.Name ?? "").ToArray();
    }

    public void Draw()
    {
        if (ImGui.Button("Clear"))
            _sched.ClearCurrentCycleSchedule();

        var ad = _sched.AgentData;
        var sheet = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>(Language.English)!;
        foreach (var na in _tree.Node($"Agent data: {(nint)ad:X}", ad == null))
        {
            _tree.LeafNode($"init={ad->InitState}, cur-cycle={ad->CurrentCycle}");
            _tree.LeafNode($"setting addon={ad->SettingAddonId}, ws={ad->CurScheduleSettingWorkshop}, slot={ad->CurScheduleSettingStartingSlot}, item=#{ad->CurScheduleSettingObjectIndex}, numMats={ad->CurScheduleSettingNumMaterials}");
            _tree.LeafNode($"rest mask={ad->RestCycles:X}, in-progress={ad->CycleInProgress}");
            int i = 0;
            foreach (ref var w in ad->Workshops)
            {
                foreach (var n in _tree.Node($"Workshop {i++}: {w.NumScheduleEntries} entries, {w.UsedTimeSlots:X} used", w.NumScheduleEntries == 0))
                {
                    for (int j = 0; j < w.NumScheduleEntries; ++j)
                    {
                        ref var e = ref w.Entries[j];
                        _tree.LeafNode($"Item {j}: {e.CraftObjectId} ({sheet.GetRow(e.CraftObjectId)?.Item.Value?.Name}), u2={e.u2}, u4={e.u4:X}, startslot={e.StartingSlot}, dur={e.Duration}, started={e.Started != 0}, efficient={e.Efficient != 0}");
                    }
                }
            }

            foreach (var n in _tree.Node("Items", ad->Items.Size() == 0))
            {
                i = 0;
                foreach (var item in ad->Items.Span)
                {
                    _tree.LeafNode($"Item {i++}: id={item.ObjectId} ({sheet.GetRow(item.ObjectId)?.Item.Value?.Name})");
                }
            }
        }

        var mji = MJIManager.Instance();
        var mjiex = (MJIManagerEx*)mji;
        _tree.LeafNode($"Popularity: dirty={mjiex->DemandDirty}, req={mjiex->RequestDemandType} obj={mjiex->RequestDemandCraftId}");
        if (!mjiex->DemandDirty)
        {
            DrawPopularity("Curr", mji->CurrentPopularity);
            DrawPopularity("Next", mji->NextPopularity);
        }

        foreach (var nf in _tree.Node($"Favors: avail={_favors.DataAvailability}", _favors.DataAvailability != 2))
        {
            DrawFavorSetup(0, 4, 8);
            DrawFavorSetup(1, 6, 6);
            DrawFavorSetup(2, 8, 8);
            Utils.TextV("Init from game week:");
            ImGui.SameLine();
            if (ImGui.Button("Fetch demand"))
                _sched.RequestDemand();
            ImGui.SameLine();
            if (ImGui.Button("Prev"))
                InitFavorsFromGame(0, -1);
            using (ImRaii.Disabled(mjiex->DemandDirty))
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
                    int i = 0;
                    foreach (var r in _tree.Nodes(_favorSolution.Recs, r => new($"Schedule {i++}")))
                    {
                        _tree.LeafNodes(r.Slots, s => $"{s.Slot}: {s.CraftObjectId} '{sheet.GetRow(s.CraftObjectId)?.Item.Value?.Name}'");
                    }
                }
            }
        }
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

    private void DrawFavorSetup(int idx, int duration, int req)
    {
        var sheet = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>()!;
        Utils.TextV($"{duration}h:");
        ImGui.SameLine();
        UICombo.UInt($"###c{idx}", _itemNames, ref _favorState.CraftObjectIds[idx], i => i != 0 && sheet.GetRow(i)?.CraftingTime == duration);
        ImGui.SameLine();
        ImGui.DragInt($"###r{idx}", ref _favorState.CompletedCounts[idx], 0.03f, 0, req);
    }

    private void InitFavorsFromGame(int offset, int pop)
    {
        for (int i = 0; i < 3; ++i)
        {
            _favorState.CraftObjectIds[i] = _favors.CraftObjectID(i + offset);
            _favorState.CompletedCounts[i] = _favors.NumDelivered(i + offset) + _favors.NumScheduled(i + offset);
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
