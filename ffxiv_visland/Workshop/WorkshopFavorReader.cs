using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using Lumina.Data;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace visland.Workshop;

internal unsafe class WorkshopFavorReader(List<string> botNames) {
    public WorkshopSolver.FavorState ReadFavorState(bool nextWeek) {
        var mji = MJIManager.Instance();
        if (mji == null || !mji->IsPlayerInSanctuary)
            throw new Exception("Favor data requires being on your island");
        var state = new WorkshopSolver.FavorState();
        var offset = nextWeek ? 6 : 3;
        for (var i = 0; i < 3; ++i) {
            state.CraftObjectIds[i] = mji->FavorState->CraftObjectIds[i + offset];
            state.CompletedCounts[i] = mji->FavorState->NumDelivered[i + offset] + mji->FavorState->NumScheduled[i + offset];
        }
        if (!mji->DemandDirty)
            state.Popularity.Set(nextWeek ? mji->NextPopularity : mji->CurrentPopularity);
        if (state.CraftObjectIds.Any(id => id == 0))
            throw new Exception("Favor craft IDs not available yet");
        return state;
    }

    public string CreateFavorRequestCommand(bool nextWeek) {
        var state = MJIManager.Instance()->FavorState;
        if (state == null || state->UpdateState != 2) {
            throw new Exception($"Favor data not available: {state->UpdateState}");
        }

        var sheetCraft = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>(Language.English)!;
        var res = "/favors";
        var offset = nextWeek ? 6 : 3;
        for (var i = 0; i < 3; ++i) {
            var id = state->CraftObjectIds[offset + i];
            var name = sheetCraft.GetRow(id).Item.Value.Name;
            if (!name.IsEmpty)
                res += $" favor{i + 1}:{botNames[id].Replace("\'", "")}";
        }
        return res;
    }

    public void EnsureDemandFavorsAvailable(List<Func<bool>> pendingActions) {
        if (MJIManager.Instance()->DemandDirty) {
            WorkshopUtils.RequestDemandFavors();
            pendingActions.Add(() => !MJIManager.Instance()->DemandDirty && MJIManager.Instance()->FavorState->UpdateState == 2);
        }
    }

    public List<WorkshopSolver.WorkshopRec> SolveRecOverrides(bool nextWeek)
        => new WorkshopSolverFavorSheet(ReadFavorState(nextWeek)).Recs;
}
