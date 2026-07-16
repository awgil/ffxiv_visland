using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using Lumina.Data;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using visland.Helpers;

namespace visland.Workshop;

internal unsafe class FavourReader(List<string> botNames) {
    public WorkshopSolver.FavourState ReadFavourState(bool nextWeek) {
        var mji = MJIManager.Instance();
        if (mji == null || !mji->IsPlayerInSanctuary)
            throw new Exception("Favour data requires being on your island");
        var state = new WorkshopSolver.FavourState();
        var offset = nextWeek ? 6 : 3;
        for (var i = 0; i < 3; ++i) {
            state.CraftObjectIds[i] = mji->FavorState->CraftObjectIds[i + offset];
            state.CompletedCounts[i] = mji->FavorState->NumDelivered[i + offset] + mji->FavorState->NumScheduled[i + offset];
        }
        if (!mji->DemandDirty)
            state.Popularity.Set(nextWeek ? mji->NextPopularity : mji->CurrentPopularity);
        if (state.CraftObjectIds.Any(id => id == 0))
            throw new Exception("Favour craft IDs not available yet");
        return state;
    }

    public string CreateFavourRequestCommand(bool nextWeek) {
        var state = MJIManager.Instance()->FavorState;
        if (state == null || state->UpdateState != 2) {
            throw new Exception($"Favour data not available: {state->UpdateState}");
        }

        var res = "/favors";
        var offset = nextWeek ? 6 : 3;
        for (var i = 0; i < 3; ++i) {
            var id = state->CraftObjectIds[offset + i];
            var name = MJICraftworksObject.GetRow(id)?.WithLanguage(Language.English).Item.Value.Name ?? string.Empty;
            if (!name.IsEmpty)
                res += $" favor{i + 1}:{botNames[id].Replace("\'", "")}";
        }
        return res;
    }

    public void EnsureDemandFavoursAvailable(List<Func<bool>> pendingActions) {
        if (MJIManager.Instance()->DemandDirty) {
            WorkshopUtils.RequestDemandFavours();
            pendingActions.Add(() => !MJIManager.Instance()->DemandDirty && MJIManager.Instance()->FavorState->UpdateState == 2);
        }
    }

    public List<WorkshopSolver.WorkshopRec> SolveRecOverrides(bool nextWeek)
        => new WorkshopSolverFavourSheet(ReadFavourState(nextWeek)).Recs;
}
