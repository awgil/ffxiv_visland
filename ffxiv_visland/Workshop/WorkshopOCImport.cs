using Dalamud.Game;
using Dalamud.Interface.Components;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Bindings.ImGui;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;

namespace visland.Workshop;

public unsafe class WorkshopOCImport {
    public WorkshopSolver.Recs Recommendations = new();

    private readonly WorkshopConfig _config;
    private readonly WorkshopSeasonDB? _seasonDB;
    private readonly ExcelSheet<MJICraftworksObject> _craftSheet;
    private readonly List<uint> _craftIds = [];
    private readonly List<string> _botNames;
    private readonly List<Func<bool>> _pendingActions = [];
    private bool IgnoreFourthWorkshop;
    private int _loadedSeason;
    private bool _loadedNextWeek;

    public WorkshopOCImport() {
        _config = Service.Config.Get<WorkshopConfig>();
        if ((int)Service.ClientState.ClientLanguage <= 3)
            _seasonDB = new WorkshopSeasonDB();
        _craftSheet = GetSheet<MJICraftworksObject>(); // unlocalised sheet can't be fetched in english
        _botNames = [.. _craftSheet.Select(r => OfficialNameToBotName(GetRow<Item>(r.Item.RowId, ClientLanguage.English)!.Value.Name.ExtractText()))];
    }

    public void Update() {
        var numDone = _pendingActions.TakeWhile(f => f()).Count();
        _pendingActions.RemoveRange(0, numDone);
    }

    public void Draw() {
        using var globalDisable = ImRaii.Disabled(_pendingActions.Count > 0);

        if (_seasonDB != null) {
            var thisSeason = _seasonDB.CurrentSeason(false);
            var nextSeason = _seasonDB.CurrentSeason(true);
            ImGui.TextUnformatted($"Archive seasons {_seasonDB.RangeStart}-{_seasonDB.RangeEnd} (cycle {_seasonDB.CycleLength})");
            ImGui.TextUnformatted($"This week → Season {thisSeason}" + (_seasonDB.TryGet(thisSeason, out var cur) ? $" ({cur.Date})" : " (missing)"));
            ImGui.TextUnformatted($"Next week → Season {nextSeason}" + (_seasonDB.TryGet(nextSeason, out var nxt) ? $" ({nxt.Date})" : " (missing)"));

            if (ImGui.Button("Load This Week"))
                LoadSeasonRecs(false);
            ImGui.SameLine();
            if (ImGui.Button("Load Next Week"))
                LoadSeasonRecs(true);
            ImGuiComponents.HelpMarker("Loads Overseas Casuals archive recommendations for the mapped season, then applies the favor mode from Settings.");
        }

        if (ImGui.Button("Import Recommendations From Clipboard"))
            ImportRecsFromClipboard(false);
        ImGuiComponents.HelpMarker("Legacy importer for schedules copied from Discord.\n" +
                        "The importer detects item names (without \"Isleworks\" et al) on each line.\n" +
                        "You can copy an entire workshop schedule from discord, junk included.");

        if (Recommendations.Empty)
            return;

        if (_loadedSeason != 0)
            ImGui.TextUnformatted($"Loaded season {_loadedSeason}" + (_loadedNextWeek ? " (next week)" : " (this week)"));

        ImGui.Separator();

        if (_config.UseFavorSolver) {
            ImGui.TextUnformatted("Advanced favor overrides");
            ImGuiComponents.HelpMarker("Manual overrides for the currently loaded schedule. Archive loads already apply the favor mode from Settings.");

            ImGuiEx.TextV("Override 4th workshop with favors:");
            ImGui.SameLine();
            if (ImGui.Button($"This Week##4th"))
                OverrideSideRecsLastWorkshopSolver(false);
            ImGui.SameLine();
            if (ImGui.Button($"Next Week##4th"))
                OverrideSideRecsLastWorkshopSolver(true);

            ImGuiEx.TextV("Override closest workshops with favors:");
            ImGui.SameLine();
            if (ImGui.Button($"This Week##asap"))
                OverrideSideRecsAsapSolver(false);
            ImGui.SameLine();
            if (ImGui.Button($"Next Week##asap"))
                OverrideSideRecsAsapSolver(true);

            if (ImGui.Button("Override 4th workshop from clipboard"))
                OverrideSideRecsLastWorkshopClipboard();
            if (ImGui.Button("Override closest workshops from clipboard"))
                OverrideSideRecsAsapClipboard();

            if (ImGuiComponents.IconButtonWithText(Dalamud.Interface.FontAwesomeIcon.Clipboard, "Copy /favors (this week)"))
                ImGui.SetClipboardText(CreateFavorRequestCommand(false));
            ImGui.SameLine();
            if (ImGuiComponents.IconButtonWithText(Dalamud.Interface.FontAwesomeIcon.Clipboard, "Copy /favors (next week)"))
                ImGui.SetClipboardText(CreateFavorRequestCommand(true));

            ImGui.Separator();
        }

        ImGuiEx.TextV("Set Schedule:");
        ImGui.SameLine();
        if (ImGui.Button("This Week"))
            ApplyRecommendations(false);
        ImGui.SameLine();
        if (ImGui.Button("Next Week"))
            ApplyRecommendations(true);
        ImGui.SameLine();
        ImGui.Checkbox("Ignore 4th Workshop", ref IgnoreFourthWorkshop);
        ImGui.Separator();

        DrawCycleRecommendations();
    }

    public void ImportRecsFromClipboard(bool silent) {
        try {
            Recommendations = ParseRecs(ImGui.GetClipboardText());
            _loadedSeason = 0;
        }
        catch (Exception ex) {
            ReportError($"Error: {ex.Message}", silent);
        }
    }

    public void LoadSeasonRecs(bool nextWeek, bool silent = false) {
        try {
            if (_seasonDB == null) {
                ReportError("Workshop archive is not available on this client.", silent);
                return;
            }

            if (_config.FavorMode == WorkshopFavorMode.None) {
                ApplySeason(nextWeek, null);
                return;
            }

            EnsureDemandFavorsAvailable();
            _pendingActions.Add(() => {
                try {
                    ApplySeason(nextWeek, ReadFavorState(nextWeek));
                }
                catch (Exception ex) {
                    ReportError($"Error: {ex.Message}", silent);
                }
                return true;
            });
        }
        catch (Exception ex) {
            ReportError($"Error: {ex.Message}", silent);
        }
    }

    private void ApplySeason(bool nextWeek, WorkshopSolver.FavorState? favors) {
        var seasonDB = _seasonDB ?? throw new Exception("Workshop archive is not available on this client.");
        var season = seasonDB.CurrentSeason(nextWeek);
        var baseRecs = seasonDB.BuildRecs(season);
        Recommendations = favors == null || _config.FavorMode == WorkshopFavorMode.None
            ? baseRecs
            : WorkshopFavorIntegration.Apply(baseRecs, _config.FavorMode, favors.Value, _craftSheet, seasonDB.RestCycles(season));
        _loadedSeason = season;
        _loadedNextWeek = nextWeek;
        Service.Log.Info($"Loaded workshop season {season} (favor mode {_config.FavorMode})");
    }

    private unsafe WorkshopSolver.FavorState ReadFavorState(bool nextWeek) {
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

    private void DrawCycleRecommendations() {
        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.NoKeepColumnsVisible;
        var maxWorkshops = WorkshopUtils.GetMaxWorkshops();

        using var scrollSection = ImRaii.Child("ScrollableSection");
        foreach ((var c, var r) in Recommendations.Enumerate()) {
            ImGuiEx.TextV($"Cycle {c}:");
            ImGui.SameLine();
            if (ImGui.Button($"Set on Active Cycle##{c}"))
                ApplyRecommendationToCurrentCycle(r);

            using var outerTable = ImRaii.Table($"table_{c}", r.Workshops.Count, tableFlags);
            if (outerTable) {
                var workshopLimit = r.Workshops.Count - (IgnoreFourthWorkshop && r.Workshops.Count > 1 ? 1 : 0);
                if (r.Workshops.Count <= 1) {
                    ImGui.TableSetupColumn(IgnoreFourthWorkshop ? $"Workshops 1-{maxWorkshops - 1}" : "All Workshops");
                }
                else if (r.Workshops.Count < maxWorkshops) {
                    var numDuplicates = 1 + maxWorkshops - r.Workshops.Count;
                    ImGui.TableSetupColumn($"Workshops 1-{numDuplicates}");
                    for (var i = 1; i < workshopLimit; ++i)
                        ImGui.TableSetupColumn($"Workshop {i + numDuplicates}");
                }
                else {
                    // favors
                    for (var i = 0; i < workshopLimit; ++i)
                        ImGui.TableSetupColumn($"Workshop {i + 1}");
                }
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                for (var i = 0; i < workshopLimit; ++i) {
                    ImGui.TableNextColumn();
                    using var innerTable = ImRaii.Table($"table_{c}_{i}", 2, tableFlags);
                    if (innerTable) {
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                        foreach (var rec in r.Workshops[i].Slots) {
                            ImGui.TableNextRow();

                            ImGui.TableNextColumn();
                            var iconSize = ImGui.GetTextLineHeight() * 1.5f;
                            var iconSizeVec = new Vector2(iconSize, iconSize);
                            var craftworkItemIcon = _craftSheet.GetRow(rec.CraftObjectId)!.Item.Value!.Icon;
                            ImGui.Image(Service.TextureProvider.GetFromGameIcon(new GameIconLookup(craftworkItemIcon)).GetWrapOrEmpty().Handle, iconSizeVec, Vector2.Zero, Vector2.One);

                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(_botNames[(int)rec.CraftObjectId]);
                        }
                    }
                }
            }
        }
    }

    private unsafe string CreateFavorRequestCommand(bool nextWeek) {
        var state = MJIManager.Instance()->FavorState;
        if (state == null || state->UpdateState != 2) {
            ReportError($"Favor data not available: {state->UpdateState}");
            return "";
        }

        var sheetCraft = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>(Language.English)!;
        var res = "/favors";
        var offset = nextWeek ? 6 : 3;
        for (var i = 0; i < 3; ++i) {
            var id = state->CraftObjectIds[offset + i];
            // the bot doesn't like names with apostrophes because it "breaks their formulas"
            var name = sheetCraft.GetRow(id).Item.Value.Name;
            if (!name.IsEmpty)
                res += $" favor{i + 1}:{_botNames[id].Replace("\'", "")}";
        }
        return res;
    }

    private void OverrideSideRecsLastWorkshopClipboard() {
        try {
            var overrideRecs = ParseRecOverrides(ImGui.GetClipboardText());
            if (overrideRecs.Count > Recommendations.Schedules.Count)
                throw new Exception($"Override list is longer than base schedule: {overrideRecs.Count} > {Recommendations.Schedules.Count}");
            OverrideSideRecsLastWorkshop(overrideRecs);
        }
        catch (Exception ex) {
            ReportError($"Error: {ex.Message}");
        }
    }

    private void OverrideSideRecsLastWorkshopSolver(bool nextWeek) {
        EnsureDemandFavorsAvailable();
        _pendingActions.Add(() => {
            OverrideSideRecsLastWorkshop(SolveRecOverrides(nextWeek));
            return true;
        });
    }

    private void OverrideSideRecsLastWorkshop(List<WorkshopSolver.WorkshopRec> overrides) {
        foreach ((var r, var o) in Recommendations.Schedules.Zip(overrides)) {
            // if base recs have >1 workshop, remove last (assume we always want to override 4th workshop)
            if (r.Workshops.Count > 1)
                r.Workshops.RemoveAt(r.Workshops.Count - 1);
            // and add current override as a schedule for last workshop
            r.Workshops.Add(o);
        }
        if (overrides.Count > Recommendations.Schedules.Count)
            Service.ChatGui.Print("Warning: couldn't fit all overrides into base schedule", "visland");
    }

    private void OverrideSideRecsAsapClipboard() {
        try {
            var overrideRecs = ParseRecOverrides(ImGui.GetClipboardText());
            if (overrideRecs.Count > Recommendations.Schedules.Count * 4)
                throw new Exception($"Override list is longer than base schedule: {overrideRecs.Count} > 4 * {Recommendations.Schedules.Count}");
            OverrideSideRecsAsap(overrideRecs);
        }
        catch (Exception ex) {
            ReportError($"Error: {ex.Message}");
        }
    }

    private void OverrideSideRecsAsapSolver(bool nextWeek) {
        EnsureDemandFavorsAvailable();
        _pendingActions.Add(() => {
            OverrideSideRecsAsap(SolveRecOverrides(nextWeek));
            return true;
        });
    }

    private void OverrideSideRecsAsap(List<WorkshopSolver.WorkshopRec> overrides) {
        var nextOverride = 0;
        foreach (var r in Recommendations.Schedules) {
            var batchSize = Math.Min(4, overrides.Count - nextOverride);
            if (batchSize == 0)
                break; // nothing left to override

            // if base recs have >1 workshop, remove last (assume we always want to override 4th workshop)
            if (r.Workshops.Count > 1)
                r.Workshops.RemoveAt(r.Workshops.Count - 1);
            var maxLeft = 4 - batchSize;
            if (r.Workshops.Count > maxLeft)
                r.Workshops.RemoveRange(maxLeft, r.Workshops.Count - maxLeft);
            r.Workshops.AddRange(overrides.Skip(nextOverride).Take(batchSize));
            nextOverride += batchSize;
        }
        if (nextOverride < overrides.Count)
            Service.ChatGui.Print("Warning: couldn't fit all overrides into base schedule", "visland");
    }

    private WorkshopSolver.Recs ParseRecs(string str) {
        var result = new WorkshopSolver.Recs();

        var curRec = new WorkshopSolver.DayRec();
        var nextSlot = 24;
        var curCycle = 0;
        foreach (var l in str.Split('\n', '\r')) {
            if (TryParseCycleStart(l, out var cycle)) {
                // complete previous cycle; if the number was not known, assume it is next cycle - 1
                result.Add(curCycle > 0 ? curCycle : cycle - 1, curRec);
                curRec = new();
                nextSlot = 24;
                curCycle = cycle;
            }
            else if (l is "First 3 Workshops" or "All Workshops") {
                // just a sanity check...
                if (!curRec.Empty)
                    throw new Exception("Unexpected start of 1st workshop recs");
            }
            else if (l == "4th Workshop") {
                // ensure next item goes into new rec list
                // TODO: do we want to add an extra empty list if this is the first line?..
                nextSlot = 24;
            }
            else if (TryParseItem(l) is var item && item != null) {
                if (nextSlot + item.Value.CraftingTime > 24) {
                    // start next workshop schedule
                    curRec.Workshops.Add(new());
                    nextSlot = 0;
                }
                curRec.Workshops.Last().Add(nextSlot, item.Value.RowId);
                nextSlot += item.Value.CraftingTime;
            }
            else
                Service.Log.Verbose($"Failed to parse {l}");
        }
        // complete current cycle; if the number was not known, assume it is tomorrow.
        // On the 7th day, importing a rec will assume the next week, but we can't import into the next week so just modulo it to the first week. Theoretically shouldn't cause problems.
        result.Add(curCycle > 0 ? curCycle : (AgentMJICraftSchedule.Instance()->Data->CycleInProgress + 2) % 8, curRec);

        return result;
    }

    private static bool TryParseCycleStart(string str, out int cycle) {
        // OC has two formats:
        // - single day recs are 'Season N (mmm dd-dd), Cycle C Recommendations'
        // - multi day recs are 'Season N (mmm dd-dd) Cycle K-L Recommendations' followed by 'Cycle C'
        if (str.StartsWith("Cycle "))
            return int.TryParse(str.AsSpan(6, 1), out cycle);
        else if (str.StartsWith("Season ") && str.IndexOf(", Cycle ") is var cycleStart && cycleStart > 0)
            return int.TryParse(str.AsSpan(cycleStart + 8, 1), out cycle);
        else {
            cycle = 0;
            return false;
        }
    }

    private MJICraftworksObject? TryParseItem(string line) {
        var matchingRows = _botNames.Select((n, i) => (n, i)).Where(t => !string.IsNullOrEmpty(t.n) && IsMatch(line, t.n)).ToList();
        if (matchingRows.Count > 1) {
            matchingRows = [.. matchingRows.OrderByDescending(t => MatchingScore(t.n, line))];
            Service.Log.Info($"Row '{line}' matches {matchingRows.Count} items: {string.Join(", ", matchingRows.Select(r => r.n))}\n" +
                "First one is most likely the correct match. Please report if this is wrong.");
        }
        return matchingRows.Count > 0 ? _craftSheet.GetRow((uint)matchingRows.First().i) : null;
    }

    private static bool IsMatch(string x, string y) => Regex.IsMatch(x, $@"\b{Regex.Escape(y)}\b");
    private static int MatchingScore(string item, string line) {
        var score = 0;

        // primitive matching based on how long the string matches. Enough for now but could need expanding later
        if (line.Contains(item))
            score += item.Length;

        return score;
    }

    private List<WorkshopSolver.WorkshopRec> ParseRecOverrides(string str) {
        var result = new List<WorkshopSolver.WorkshopRec>();
        var nextSlot = 24;

        foreach (var l in str.Split('\n', '\r')) {
            if (l.StartsWith("Schedule #")) {
                // ensure next item goes into new rec list
                nextSlot = 24;
            }
            else if (TryParseItem(l) is var item && item != null) {
                if (nextSlot + item.Value.CraftingTime > 24) {
                    // start next workshop schedule
                    result.Add(new());
                    nextSlot = 0;
                }
                result.Last().Add(nextSlot, item.Value.RowId);
                nextSlot += item.Value.CraftingTime;
            }
            else
                Service.Log.Verbose($"Failed to parse {l}");
        }

        return result;
    }

    private unsafe List<WorkshopSolver.WorkshopRec> SolveRecOverrides(bool nextWeek) {
        try {
            return new WorkshopSolverFavorSheet(ReadFavorState(nextWeek)).Recs;
        }
        catch (Exception ex) {
            ReportError(ex.Message);
            return [];
        }
    }

    public static string OfficialNameToBotName(string name) {
        // why do they keep fucking changing this!?
        if (name.StartsWith("Isleworks "))
            return name[10..];
        //if (name.StartsWith("Isleberry "))
        //    return name.Remove(0, 10);
        if (name.StartsWith("Islefish "))
            return name[9..];
        if (name.StartsWith("Island "))
            return name[7..];
        if (name == "Mammet of the Cycle Award")
            return "Mammet Award";
        return name;
    }

    private unsafe void EnsureDemandFavorsAvailable() {
        if (MJIManager.Instance()->DemandDirty) {
            WorkshopUtils.RequestDemandFavors();
            _pendingActions.Add(() => !MJIManager.Instance()->DemandDirty && MJIManager.Instance()->FavorState->UpdateState == 2);
        }
    }

    private unsafe int ApplyRecommendation(int cycle, WorkshopSolver.DayRec rec, int minStartingHour = 0) {
        var maxWorkshops = WorkshopUtils.GetMaxWorkshops();
        var scheduled = 0;
        foreach (var w in rec.Enumerate(maxWorkshops))
            if (!IgnoreFourthWorkshop || w.workshop < maxWorkshops - 1)
                foreach (var r in w.rec.Slots) {
                    if (r.Slot < minStartingHour)
                        continue;
                    WorkshopUtils.ScheduleItemToWorkshop(r.CraftObjectId, r.Slot, cycle, w.workshop);
                    scheduled++;
                }
        return scheduled;
    }

    private void ApplyRecommendationToCurrentCycle(WorkshopSolver.DayRec rec) {
        var agentData = AgentMJICraftSchedule.Instance()->Data;
        var cycle = agentData->CycleDisplayed;
        var minHour = cycle == agentData->CycleInProgress ? agentData->HourSinceCycleStart : 0;
        ApplyRecommendation(cycle, rec, minHour);
        WorkshopUtils.ResetCurrentCycleToRefreshUI();
    }

    private void ApplyRecommendations(bool nextWeek) {
        // TODO: clear recs!

        try {
            var agentData = AgentMJICraftSchedule.Instance()->Data;
            var restDaysCount = BitOperations.PopCount(~Recommendations.CyclesMask & 0x7F);
            if (Recommendations.Schedules.Count + restDaysCount > 7)
                throw new Exception($"Too many days in recs: {Recommendations.Schedules.Count} crafts + {restDaysCount} rest > 7");

            var cycleInProgress = nextWeek ? -1 : agentData->CycleInProgress;
            var hourSinceStart = nextWeek ? 0 : agentData->HourSinceCycleStart;
            // Only fully-completed cycles are skipped; the in-progress cycle gets partial apply.
            var completedCycles = cycleInProgress > 0 ? (1u << cycleInProgress) - 1 : 0u;
            var skippedMask = Recommendations.CyclesMask & completedCycles;
            if (skippedMask != 0) {
                var skipped = FormatCycleMask(skippedMask);
                Service.Log.Info($"Skipping completed cycles: {skipped}");
                Service.ChatGui.Print($"Skipping completed cycles: {skipped}", "visland");
            }

            var hasApplicable = false;
            foreach ((var c, var r) in Recommendations.Enumerate()) {
                if ((completedCycles & (1u << (c - 1))) != 0)
                    continue;
                if (c - 1 == cycleInProgress)
                    hasApplicable |= r.Workshops.Any(w => w.Slots.Any(s => s.Slot >= hourSinceStart));
                else
                    hasApplicable = true;
            }
            if (!hasApplicable)
                throw new Exception("No remaining cycles to apply — the whole schedule is already done or in progress");

            var currentRestCycles = nextWeek ? agentData->RestCycles >> 7 : agentData->RestCycles & 0x7F;
            if ((currentRestCycles & Recommendations.CyclesMask) != 0) {
                // we need to change rest cycles - set to C1 and last unused (or C1 only when FreeRestDay mode packed the second rest)
                var freeCycles = ~Recommendations.CyclesMask & 0x7F;
                if ((freeCycles & 1) == 0)
                    throw new Exception($"Sorry, we assume C1 is always rest - set rest days manually to match your schedule");

                uint rest;
                if (BitOperations.PopCount(freeCycles) == 1) {
                    rest = freeCycles; // only one free day (typically C1) — free-rest / single-rest week
                }
                else {
                    rest = (1u << (31 - BitOperations.LeadingZeroCount(freeCycles))) | 1;
                    if (BitOperations.PopCount(rest) != 2)
                        throw new Exception($"Something went wrong, failed to determine rest days");
                }

                var changedRest = rest ^ currentRestCycles;
                if ((changedRest & completedCycles) != 0) {
                    Service.Log.Warning("Skipping rest-day adjustment: would affect cycles already done or in progress");
                    Service.ChatGui.Print("Skipping rest-day adjustment for this week — set rest days manually if needed", "visland");
                }
                else {
                    var newRest = nextWeek ? (rest << 7) | (agentData->RestCycles & 0x7F) : (agentData->RestCycles & 0x3F80) | rest;
                    WorkshopUtils.SetRestCycles(newRest);
                }
            }

            var appliedCycles = 0;
            var appliedSlots = 0;
            foreach ((var c, var r) in Recommendations.Enumerate()) {
                if ((completedCycles & (1u << (c - 1))) != 0)
                    continue;
                var minHour = c - 1 == cycleInProgress ? hourSinceStart : 0;
                var scheduled = ApplyRecommendation(c - 1 + (nextWeek ? 7 : 0), r, minHour);
                if (scheduled > 0) {
                    appliedCycles++;
                    appliedSlots += scheduled;
                }
                else if (c - 1 == cycleInProgress && minHour > 0)
                    Service.Log.Info($"Cycle {c}: no remaining slots after hour {minHour}");
            }

            if (appliedSlots == 0)
                throw new Exception("No cycles were applied");

            WorkshopUtils.ResetCurrentCycleToRefreshUI();
            if (skippedMask != 0 || cycleInProgress >= 0 && hourSinceStart > 0)
                Service.ChatGui.Print($"Applied {appliedSlots} craft(s) across {appliedCycles} cycle(s)", "visland");
        }
        catch (Exception ex) {
            ReportError($"Error: {ex.Message}");
        }
    }

    private static string FormatCycleMask(uint mask) {
        var cycles = new List<int>();
        for (var c = 1; c <= 7; ++c) {
            if ((mask & (1u << (c - 1))) != 0)
                cycles.Add(c);
        }
        return string.Join(", ", cycles.Select(c => $"C{c}"));
    }

    private static void ReportError(string msg, bool silent = false) {
        Service.Log.Error(msg);
        if (!silent)
            Service.ChatGui.PrintError(msg);
    }
}
