using Dalamud;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using ImGuiNET;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using visland.Helpers;

namespace visland.Workshop;

public unsafe class WorkshopOCImport
{
    public WorkshopSolver.Recs Recommendations = new();

    private WorkshopConfig _config;
    private ExcelSheet<MJICraftworksObject> _craftSheet;
    private List<string> _botNames;
    private List<Func<bool>> _pendingActions = [];
    private bool IgnoreFourthWorkshop;

    public WorkshopOCImport()
    {
        _config = Service.Config.Get<WorkshopConfig>();
        _craftSheet = Service.DataManager.GetExcelSheet<MJICraftworksObject>()!;
        _botNames = _craftSheet.Select(r => OfficialNameToBotName(r.Item.GetDifferentLanguage(ClientLanguage.English).Value?.Name.RawString ?? "")).ToList();
    }

    public void Update()
    {
        var numDone = _pendingActions.TakeWhile(f => f()).Count();
        _pendingActions.RemoveRange(0, numDone);
    }

    public void Draw()
    {
        using var globalDisable = ImRaii.Disabled(_pendingActions.Count > 0); // disallow any manipulations while delayed actions are in progress

        if (ImGui.Button("Import Recommendations From Clipboard"))
            ImportRecsFromClipboard(false);
        ImGuiComponents.HelpMarker("This is for importing schedules from the Overseas Casuals' Discord from your clipboard.\n" +
                        "This importer detects the presence of an item's name (not including \"Isleworks\" et al) on each line.\n" +
                        "You can copy an entire workshop's schedule from the discord, junk included.");

        if (Recommendations.Empty)
            return;

        ImGui.Separator();

        if (!_config.UseFavorSolver)
        {
            ImGui.TextUnformatted("Favours");
            ImGuiComponents.HelpMarker("Click the \"This Week's Favors\" or \"Next Week's Favors\" button to generate a bot command for the OC discord for your favors.\n" +
                    "Then click the #bot-spam button to open discord to the channel, paste in the command and copy its output.\n" +
                    "Finally, click the \"Override 4th workshop\" button to replace the regular recommendations with favor recommendations.");

            if (ImGuiComponents.IconButtonWithText(Dalamud.Interface.FontAwesomeIcon.Clipboard, "This Week's Favors"))
                ImGui.SetClipboardText(CreateFavorRequestCommand(false));
            ImGui.SameLine();
            if (ImGuiComponents.IconButtonWithText(Dalamud.Interface.FontAwesomeIcon.Clipboard, "Next Week's Favors"))
                ImGui.SetClipboardText(CreateFavorRequestCommand(true));

            if (ImGui.Button("Overseas Casuals > #bot-spam"))
                Util.OpenLink("discord://discord.com/channels/1034534280757522442/1034985297391407126");
            if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
                Util.OpenLink("https://discord.com/channels/1034534280757522442/1034985297391407126");
            ImGuiComponents.HelpMarker("\uE051: Discord app\n\uE052: Discord in browser");

            if (ImGui.Button("Override 4th workshop with favor schedules from clipboard"))
                OverrideSideRecsLastWorkshopClipboard();
            if (ImGui.Button("Override closest workshops with favor schedules from clipboard"))
                OverrideSideRecsAsapClipboard();
        }
        else
        {
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
        }

        ImGui.Separator();

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

    public void ImportRecsFromClipboard(bool silent)
    {
        try
        {
            Recommendations = ParseRecs(ImGui.GetClipboardText());
        }
        catch (Exception ex)
        {
            ReportError($"Error: {ex.Message}", silent);
        }
    }

    private void DrawCycleRecommendations()
    {
        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.NoKeepColumnsVisible;
        var maxWorkshops = WorkshopUtils.GetMaxWorkshops();

        using var scrollSection = ImRaii.Child("ScrollableSection");
        foreach (var (c, r) in Recommendations.Enumerate())
        {
            ImGuiEx.TextV($"Cycle {c}:");
            ImGui.SameLine();
            if (ImGui.Button($"Set on Active Cycle##{c}"))
                ApplyRecommendationToCurrentCycle(r);

            using var outerTable = ImRaii.Table($"table_{c}", r.Workshops.Count, tableFlags);
            if (outerTable)
            {
                var workshopLimit = r.Workshops.Count - (IgnoreFourthWorkshop && r.Workshops.Count > 1 ? 1 : 0);
                if (r.Workshops.Count <= 1)
                {
                    ImGui.TableSetupColumn(IgnoreFourthWorkshop ? $"Workshops 1-{maxWorkshops - 1}" : "All Workshops");
                }
                else if (r.Workshops.Count < maxWorkshops)
                {
                    var numDuplicates = 1 + maxWorkshops - r.Workshops.Count;
                    ImGui.TableSetupColumn($"Workshops 1-{numDuplicates}");
                    for (int i = 1; i < workshopLimit; ++i)
                        ImGui.TableSetupColumn($"Workshop {i + numDuplicates}");
                }
                else
                {
                    // favors
                    for (int i = 0; i < workshopLimit; ++i)
                        ImGui.TableSetupColumn($"Workshop {i + 1}");
                }
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                for (int i = 0; i < workshopLimit; ++i)
                {
                    ImGui.TableNextColumn();
                    using var innerTable = ImRaii.Table($"table_{c}_{i}", 2, tableFlags);
                    if (innerTable)
                    {
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                        foreach (var rec in r.Workshops[i].Slots)
                        {
                            ImGui.TableNextRow();

                            ImGui.TableNextColumn();
                            var iconSize = ImGui.GetTextLineHeight() * 1.5f;
                            var iconSizeVec = new Vector2(iconSize, iconSize);
                            var craftworkItemIcon = _craftSheet.GetRow(rec.CraftObjectId)!.Item.Value!.Icon;
                            ImGui.Image(Service.TextureProvider.GetIcon(craftworkItemIcon)!.ImGuiHandle, iconSizeVec, Vector2.Zero, Vector2.One);

                            ImGui.TableNextColumn();
                            ImGui.TextUnformatted(_botNames[(int)rec.CraftObjectId]);
                        }
                    }
                }
            }
        }
    }

    private unsafe string CreateFavorRequestCommand(bool nextWeek)
    {
        var state = MJIManager.Instance()->FavorState;
        if (state == null || state->UpdateState != 2)
        {
            ReportError($"Favor data not available: {state->UpdateState}");
            return "";
        }

        var sheetCraft = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>(Language.English)!;
        var res = "/favors";
        var offset = nextWeek ? 6 : 3;
        for (int i = 0; i < 3; ++i)
        {
            var id = state->CraftObjectIds[offset + i];
            // the bot doesn't like names with apostrophes because it "breaks their formulas"
            var name = sheetCraft.GetRow(id)?.Item.Value?.Name;
            if (name != null)
                res += $" favor{i + 1}:{_botNames[(int)id].Replace("\'", "")}";
        }
        return res;
    }

    private void OverrideSideRecsLastWorkshopClipboard()
    {
        try
        {
            var overrideRecs = ParseRecOverrides(ImGui.GetClipboardText());
            if (overrideRecs.Count > Recommendations.Schedules.Count)
                throw new Exception($"Override list is longer than base schedule: {overrideRecs.Count} > {Recommendations.Schedules.Count}");
            OverrideSideRecsLastWorkshop(overrideRecs);
        }
        catch (Exception ex)
        {
            ReportError($"Error: {ex.Message}");
        }
    }

    private void OverrideSideRecsLastWorkshopSolver(bool nextWeek)
    {
        EnsureDemandFavorsAvailable();
        _pendingActions.Add(() => {
            OverrideSideRecsLastWorkshop(SolveRecOverrides(nextWeek));
            return true;
        });
    }

    private void OverrideSideRecsLastWorkshop(List<WorkshopSolver.WorkshopRec> overrides)
    {
        foreach (var (r, o) in Recommendations.Schedules.Zip(overrides))
        {
            // if base recs have >1 workshop, remove last (assume we always want to override 4th workshop)
            if (r.Workshops.Count > 1)
                r.Workshops.RemoveAt(r.Workshops.Count - 1);
            // and add current override as a schedule for last workshop
            r.Workshops.Add(o);
        }
        if (overrides.Count > Recommendations.Schedules.Count)
            Service.ChatGui.Print("Warning: couldn't fit all overrides into base schedule", "visland");
    }

    private void OverrideSideRecsAsapClipboard()
    {
        try
        {
            var overrideRecs = ParseRecOverrides(ImGui.GetClipboardText());
            if (overrideRecs.Count > Recommendations.Schedules.Count * 4)
                throw new Exception($"Override list is longer than base schedule: {overrideRecs.Count} > 4 * {Recommendations.Schedules.Count}");
            OverrideSideRecsAsap(overrideRecs);
        }
        catch (Exception ex)
        {
            ReportError($"Error: {ex.Message}");
        }
    }

    private void OverrideSideRecsAsapSolver(bool nextWeek)
    {
        EnsureDemandFavorsAvailable();
        _pendingActions.Add(() => {
            OverrideSideRecsAsap(SolveRecOverrides(nextWeek));
            return true;
        });
    }

    private void OverrideSideRecsAsap(List<WorkshopSolver.WorkshopRec> overrides)
    {
        int nextOverride = 0;
        foreach (var r in Recommendations.Schedules)
        {
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

    private WorkshopSolver.Recs ParseRecs(string str)
    {
        var result = new WorkshopSolver.Recs();

        var curRec = new WorkshopSolver.DayRec();
        int nextSlot = 24;
        int curCycle = 0;
        foreach (var l in str.Split('\n', '\r'))
        {
            if (TryParseCycleStart(l, out var cycle))
            {
                // complete previous cycle; if the number was not known, assume it is next cycle - 1
                result.Add(curCycle > 0 ? curCycle : cycle - 1, curRec);
                curRec = new();
                nextSlot = 24;
                curCycle = cycle;
            }
            else if (l == "First 3 Workshops" || l == "All Workshops")
            {
                // just a sanity check...
                if (!curRec.Empty)
                    throw new Exception("Unexpected start of 1st workshop recs");
            }
            else if (l == "4th Workshop")
            {
                // ensure next item goes into new rec list
                // TODO: do we want to add an extra empty list if this is the first line?..
                nextSlot = 24;
            }
            else if (TryParseItem(l) is var item && item != null)
            {
                if (nextSlot + item.CraftingTime > 24)
                {
                    // start next workshop schedule
                    curRec.Workshops.Add(new());
                    nextSlot = 0;
                }
                curRec.Workshops.Last().Add(nextSlot, item.RowId);
                nextSlot += item.CraftingTime;
            }
        }
        // complete current cycle; if the number was not known, assume it is tomorrow.
        // On the 7th day, importing a rec will assume the next week, but we can't import into the next week so just modulo it to the first week. Theoretically shouldn't cause problems.
        result.Add(curCycle > 0 ? curCycle : (AgentMJICraftSchedule.Instance()->Data->CycleInProgress + 2) % 7, curRec);

        return result;
    }

    private static bool TryParseCycleStart(string str, out int cycle)
    {
        // OC has two formats:
        // - single day recs are 'Season N (mmm dd-dd), Cycle C Recommendations'
        // - multi day recs are 'Season N (mmm dd-dd) Cycle K-L Recommendations' followed by 'Cycle C'
        if (str.StartsWith("Cycle "))
        {
            return int.TryParse(str.Substring(6, 1), out cycle);
        }
        else if (str.StartsWith("Season ") && str.IndexOf(", Cycle ") is var cycleStart && cycleStart > 0)
        {
            return int.TryParse(str.Substring(cycleStart + 8, 1), out cycle);
        }
        else
        {
            cycle = 0;
            return false;
        }
    }

    private MJICraftworksObject? TryParseItem(string line)
    {
        var matchingRows = _botNames.Select((n, i) => (n, i)).Where(t => !string.IsNullOrEmpty(t.n) && IsMatch(line, t.n)).ToList();
        if (matchingRows.Count > 1)
        {
            matchingRows = matchingRows.OrderByDescending(t => MatchingScore(t.n, line)).ToList();
            Service.Log.Info($"Row '{line}' matches {matchingRows.Count} items: {string.Join(", ", matchingRows.Select(r => r.n))}\n" +
                "First one is most likely the correct match. Please report if this is wrong.");
        }
        return matchingRows.Count > 0 ? _craftSheet.GetRow((uint)matchingRows.First().i) : null;
    }


    private static bool IsMatch(string x, string y) => Regex.IsMatch(x, $@"\b{Regex.Escape(y)}\b");
    private static object MatchingScore(string item, string line)
    {
        int score = 0;

        // primitive matching based on how long the string matches. Enough for now but could need expanding later
        if (line.Contains(item))
            score += item.Length;

        return score;
    }

    private List<WorkshopSolver.WorkshopRec> ParseRecOverrides(string str)
    {
        var result = new List<WorkshopSolver.WorkshopRec>();
        int nextSlot = 24;

        foreach (var l in str.Split('\n', '\r'))
        {
            if (l.StartsWith("Schedule #"))
            {
                // ensure next item goes into new rec list
                nextSlot = 24;
            }
            else if (TryParseItem(l) is var item && item != null)
            {
                if (nextSlot + item.CraftingTime > 24)
                {
                    // start next workshop schedule
                    result.Add(new());
                    nextSlot = 0;
                }
                result.Last().Add(nextSlot, item.RowId);
                nextSlot += item.CraftingTime;
            }
        }

        return result;
    }

    private unsafe List<WorkshopSolver.WorkshopRec> SolveRecOverrides(bool nextWeek)
    {
        var mji = MJIManager.Instance();
        if (mji->IsPlayerInSanctuary == 0) return [];
        var state = new WorkshopSolver.FavorState();
        var offset = nextWeek ? 6 : 3;
        for (int i = 0; i < 3; ++i)
        {
            state.CraftObjectIds[i] = mji->FavorState->CraftObjectIds[i + offset];
            state.CompletedCounts[i] = mji->FavorState->NumDelivered[i + offset] + mji->FavorState->NumScheduled[i + offset];
        }
        if (!mji->DemandDirty)
        {
            state.Popularity.Set(nextWeek ? mji->NextPopularity : mji->CurrentPopularity);
        }

        try
        {
            return new WorkshopSolverFavorSheet(state).Recs;
        }
        catch (Exception ex)
        {
            ReportError(ex.Message);
            Service.Log.Error($"Current favors: {state.CraftObjectIds[0]} #{state.CompletedCounts[0]}, {state.CraftObjectIds[1]} #{state.CompletedCounts[1]}, {state.CraftObjectIds[2]} #{state.CompletedCounts[2]}");
            return [];
        }
    }

    public static string OfficialNameToBotName(string name)
    {
        // why do they keep fucking changing this!?
        if (name.StartsWith("Isleworks "))
            return name.Remove(0, 10);
        //if (name.StartsWith("Isleberry "))
        //    return name.Remove(0, 10);
        if (name.StartsWith("Islefish "))
            return name.Remove(0, 9);
        if (name.StartsWith("Island "))
            return name.Remove(0, 7);
        if (name == "Mammet of the Cycle Award")
            return "Mammet Award";
        return name;
    }

    private unsafe void EnsureDemandFavorsAvailable()
    {
        if (MJIManager.Instance()->DemandDirty)
        {
            WorkshopUtils.RequestDemandFavors();
            _pendingActions.Add(() => !MJIManager.Instance()->DemandDirty && MJIManager.Instance()->FavorState->UpdateState == 2);
        }
    }

    private unsafe void ApplyRecommendation(int cycle, WorkshopSolver.DayRec rec)
    {
        var maxWorkshops = WorkshopUtils.GetMaxWorkshops();
        foreach (var w in rec.Enumerate(maxWorkshops))
            if (!IgnoreFourthWorkshop || w.workshop < maxWorkshops - 1)
                foreach (var r in w.rec.Slots)
                    WorkshopUtils.ScheduleItemToWorkshop(r.CraftObjectId, r.Slot, cycle, w.workshop);
    }

    private void ApplyRecommendationToCurrentCycle(WorkshopSolver.DayRec rec)
    {
        var cycle = AgentMJICraftSchedule.Instance()->Data->CycleDisplayed;
        ApplyRecommendation(cycle, rec);
        WorkshopUtils.ResetCurrentCycleToRefreshUI();
    }

    private void ApplyRecommendations(bool nextWeek)
    {
        // TODO: clear recs!

        try
        {
            var agentData = AgentMJICraftSchedule.Instance()->Data;
            if (Recommendations.Schedules.Count > 5)
                throw new Exception($"Too many days in recs: {Recommendations.Schedules.Count}");

            uint forbiddenCycles = nextWeek ? 0 : (1u << (agentData->CycleInProgress + 1)) - 1;
            if ((Recommendations.CyclesMask & forbiddenCycles) != 0)
                throw new Exception("Some of the cycles in schedule are already in progress or are done");

            var currentRestCycles = nextWeek ? agentData->RestCycles >> 7 : agentData->RestCycles & 0x7F;
            if ((currentRestCycles & Recommendations.CyclesMask) != 0)
            {
                // we need to change rest cycles - set to C1 and last unused
                var freeCycles = ~Recommendations.CyclesMask & 0x7F;
                if ((freeCycles & 1) == 0)
                    throw new Exception($"Sorry, we assume C1 is always rest - set rest days manually to match your schedule");
                var rest = (1u << (31 - BitOperations.LeadingZeroCount(freeCycles))) | 1;
                if (BitOperations.PopCount(rest) != 2)
                    throw new Exception($"Something went wrong, failed to determine rest days");

                var changedRest = rest ^ currentRestCycles;
                if ((changedRest & forbiddenCycles) != 0)
                    throw new Exception("Can't apply this schedule: it would require changing rest days for cycles that are in progress or already done");

                var newRest = nextWeek ? (rest << 7) | (agentData->RestCycles & 0x7F) : (agentData->RestCycles & 0x3F80) | rest;
                WorkshopUtils.SetRestCycles(newRest);
            }

            var cycle = agentData->CycleDisplayed;
            foreach (var (c, r) in Recommendations.Enumerate())
                ApplyRecommendation(c - 1 + (nextWeek ? 7 : 0), r);
            WorkshopUtils.ResetCurrentCycleToRefreshUI();
        }
        catch (Exception ex)
        {
            ReportError($"Error: {ex.Message}");
        }
    }

    private static void ReportError(string msg, bool silent = false)
    {
        Service.Log.Error(msg);
        if (!silent)
            Service.ChatGui.PrintError(msg);
    }
}
