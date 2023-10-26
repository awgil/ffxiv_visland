using Dalamud;
using Dalamud.Interface.Components;
using Dalamud.Utility;
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

public class WorkshopOCImport
{
    public struct Rec
    {
        public int Slot;
        public uint CraftObjectId;

        public Rec(int slot, uint craftObjectId)
        {
            Slot = slot;
            CraftObjectId = craftObjectId;
        }
    }

    public class DayRec
    {
        public List<List<Rec>> Workshops = new();

        public bool Empty => Workshops.Count == 0;
    }

    public class Recs
    {
        private List<DayRec> _schedules = new();
        public uint CyclesMask { get; private set; } // num bits set equal to num schedules
        public IReadOnlyList<DayRec> Schedules => _schedules;

        public bool Empty => Schedules.Count == 0;

        public void Add(int cycle, DayRec schedule)
        {
            if (schedule.Empty)
                return; // don't care, rest day or something

            if (cycle is < 1 or > 7)
                throw new Exception($"Cycle index out of bounds: {cycle}");
            var mask = 1u << cycle - 1;
            if ((CyclesMask & mask) != 0)
                throw new Exception($"Duplicate cycle {cycle} in the recs");
            if ((CyclesMask & ~(mask - 1)) != 0)
                throw new Exception($"Bad cycle order: {cycle} found after future days");

            _schedules.Add(schedule);
            CyclesMask |= mask;
        }

        public IEnumerable<(int cycle, DayRec rec)> Enumerate()
        {
            var m = CyclesMask;
            foreach (var r in Schedules)
            {
                var c = BitOperations.TrailingZeroCount(m);
                yield return (c + 1, r);
                m &= ~(1u << c);
            }
        }

        public void Clear()
        {
            _schedules.Clear();
            CyclesMask = 0;
        }
    }

    public Recs Recommendations = new();
    public Recs RecommendationsCache = new();

    private WorkshopFavors _favors = new();
    private WorkshopSchedule _sched = new();
    private ExcelSheet<MJICraftworksObject> _craftSheet;
    private List<string> _botNames;
    private int selectedCycle = 0;
    private List<int> Cycles { get; set; } = new() { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };

    public WorkshopOCImport()
    {
        _craftSheet = Service.DataManager.GetExcelSheet<MJICraftworksObject>()!;
        _botNames = _craftSheet.Select(r => OfficialNameToBotName(r.Item.GetDifferentLanguage(ClientLanguage.English).Value?.Name.RawString ?? "")).ToList();
    }

    public void Draw()
    {
        if (ImGui.Button("Import Recommendations From Clipboard"))
            ImportRecs(ImGui.GetClipboardText());
        ImGuiComponents.HelpMarker("This is for importing schedules from the Overseas Casuals' Discord from your clipboard.\n" +
                        "This importer detects the presence of an item's name (not including \"Isleworks\" et al) on each line.\n" +
                        "You can copy an entire workshop's schedule from the discord, junk included.");

        if (Recommendations.Empty)
            return;

        ImGui.Separator();

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
        ImGuiComponents.HelpMarker("Left Click: Discord app\nRight Click: Discord in browser");

        if (ImGui.Button("Override 4th workshop with favor schedules from clipboard"))
            OverrideSideRecsLastWorkshop(ImGui.GetClipboardText());
        if (ImGui.Button("Override closest workshops with favor schedules from clipboard"))
            OverrideSideRecsAsap(ImGui.GetClipboardText());

        ImGui.Separator();

        // The UI does not like clearing the current recommendations even if you reset it right after. Need a solution

        //Utils.TextV("Cycle Override: ");
        //ImGui.SameLine();
        //var cyclePrev = selectedCycle == 0 ? "" : Cycles[selectedCycle - 1].ToString();
        //ImGui.SetNextItemWidth(50);
        //if (ImGui.BeginCombo("###CycleOverride", cyclePrev))
        //{
        //    foreach (var cycle in Cycles)
        //    {
        //        var selected = ImGui.Selectable(cycle.ToString(), selectedCycle == cycle - 1);

        //        if (selected)
        //        {
        //            selectedCycle = cycle - 1;
        //            OverrideCycleStart(selectedCycle);
        //        }

        //    }
        //    ImGui.EndCombo();
        //}

        Utils.TextV("Set Schedule:");
        ImGui.SameLine();
        if (ImGui.Button("This Week"))
            ApplyRecommendations(false);
        ImGui.SameLine();
        if (ImGui.Button("Next Week"))
            ApplyRecommendations(true);

        ImGui.Separator();

        DrawCycleRecommendations();
    }

    private void DrawCycleRecommendations()
    {
        var tableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.NoKeepColumnsVisible;
        var maxWorkshops = Utils.GetMaxWorkshops();

        ImGui.BeginChild("ScrollableSection");
        foreach (var (c, r) in Recommendations.Enumerate())
        {
            ImGui.TextUnformatted($"Cycle {c}:");
            if (ImGui.BeginTable($"table_{c}", r.Workshops.Count, tableFlags))
            {
                if (r.Workshops.Count <= 1)
                {
                    ImGui.TableSetupColumn("All Workshops");
                }
                else if (r.Workshops.Count < maxWorkshops)
                {
                    var numDuplicates = 1 + maxWorkshops - r.Workshops.Count;
                    ImGui.TableSetupColumn($"Workshops 1-{numDuplicates}");
                    for (int i = 1; i < r.Workshops.Count; ++i)
                        ImGui.TableSetupColumn($"Workshop {i + numDuplicates}");
                }
                else
                {
                    for (int i = 0; i < r.Workshops.Count; ++i)
                        ImGui.TableSetupColumn($"Workshop {i + 1}");
                }
                ImGui.TableHeadersRow();

                ImGui.TableNextRow();
                for (int i = 0; i < r.Workshops.Count; ++i)
                {
                    ImGui.TableNextColumn();
                    if (ImGui.BeginTable($"table_{c}_{i}", 2, tableFlags))
                    {
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed);
                        foreach (var rec in r.Workshops[i])
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
                    ImGui.EndTable();
                }
            }
            ImGui.EndTable();
        }
        ImGui.EndChild();
    }

    private string CreateFavorRequestCommand(bool nextWeek)
    {
        if (_favors.DataAvailability != 2)
        {
            ReportError($"Favor data not available: {_favors.DataAvailability}");
            return "";
        }

        var sheetCraft = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>(Language.English)!;
        var res = "/favors";
        var offset = nextWeek ? 6 : 3;
        for (int i = 0; i < 3; ++i)
        {
            var id = _favors.CraftObjectID(offset + i);
            // the bot doesn't like names with apostrophes because it "breaks their formulas"
            var name = sheetCraft.GetRow(id)?.Item.Value?.Name;
            if (name != null)
                res += $" favor{i + 1}:{_botNames[(int)id].Replace("\'", "")}";
        }
        return res;
    }

    private void ImportRecs(string str)
    {
        try
        {
            Recommendations = ParseRecs(str);
            RecommendationsCache = Recommendations;
        }
        catch (Exception ex)
        {
            ReportError($"Error: {ex.Message}");
        }
    }

    private void OverrideSideRecsLastWorkshop(string str)
    {
        try
        {
            var overrideRecs = ParseRecOverrides(str);
            if (overrideRecs.Count > Recommendations.Schedules.Count)
                throw new Exception($"Override list is longer than base schedule: {overrideRecs.Count} > {Recommendations.Schedules.Count}");

            foreach (var (r, o) in Recommendations.Schedules.Zip(overrideRecs))
            {
                // if base recs have >1 workshop, remove last (assume we always want to override 4th workshop)
                if (r.Workshops.Count > 1)
                    r.Workshops.RemoveAt(r.Workshops.Count - 1);
                // and add current override as a schedule for last workshop
                r.Workshops.Add(o);
            }
        }
        catch (Exception ex)
        {
            ReportError($"Error: {ex.Message}");
        }
    }

    private void OverrideSideRecsAsap(string str)
    {
        try
        {
            var overrideRecs = ParseRecOverrides(str);
            if (overrideRecs.Count > Recommendations.Schedules.Count * 4)
                throw new Exception($"Override list is longer than base schedule: {overrideRecs.Count} > 4 * {Recommendations.Schedules.Count}");

            int nextOverride = 0;
            foreach (var r in Recommendations.Schedules)
            {
                var batchSize = Math.Min(4, overrideRecs.Count - nextOverride);
                if (batchSize == 0)
                    break; // nothing left to override

                // if base recs have >1 workshop, remove last (assume we always want to override 4th workshop)
                if (r.Workshops.Count > 1)
                    r.Workshops.RemoveAt(r.Workshops.Count - 1);
                var maxLeft = 4 - batchSize;
                if (r.Workshops.Count > maxLeft)
                    r.Workshops.RemoveRange(maxLeft, r.Workshops.Count - maxLeft);
                r.Workshops.AddRange(overrideRecs.Skip(nextOverride).Take(batchSize));
                nextOverride += batchSize;
            }
        }
        catch (Exception ex)
        {
            ReportError($"Error: {ex.Message}");
        }
    }

    private Recs ParseRecs(string str)
    {
        var result = new Recs();

        var curRec = new DayRec();
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
                curRec.Workshops.Last().Add(new(nextSlot, item.RowId));
                nextSlot += item.CraftingTime;
            }
        }
        // complete current cycle; if the number was not known, assume it is tomorrow
        result.Add(curCycle > 0 ? curCycle : _sched.CycleInProgress + 2, curRec);

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

    private void OverrideCycleStart(int selectedCycle)
    {
        if (selectedCycle == 0)
        {
            Recommendations = RecommendationsCache;
            return;
        }

        Recommendations.Clear();
        var i = 0;
        foreach (var (c, r) in RecommendationsCache.Enumerate())
        {
            Recommendations.Add(selectedCycle - 1 + i, r);
            i++;
        }
        _sched.SetCurrentCycle(selectedCycle - 1);
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

    private List<List<Rec>> ParseRecOverrides(string str)
    {
        var result = new List<List<Rec>>();
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
                result.Last().Add(new(nextSlot, item.RowId));
                nextSlot += item.CraftingTime;
            }
        }

        return result;
    }

    public static string OfficialNameToBotName(string name)
    {
        if (name.StartsWith("Isleworks "))
            return name.Remove(0, 10);
        if (name.StartsWith("Isleberry "))
            return name.Remove(0, 10);
        if (name.StartsWith("Islefish "))
            return name.Remove(0, 9);
        if (name.StartsWith("Island "))
            return name.Remove(0, 7);
        if (name == "Mammet of the Cycle Award")
            return "Mammet Award";
        return name;
    }

    private unsafe void ApplyRecommendation(int cycle, DayRec rec)
    {
        var maxWorkshops = Utils.GetMaxWorkshops();
        var numDuplicates = 1 + Math.Max(0, maxWorkshops - rec.Workshops.Count);
        for (var i = 0; i < maxWorkshops; i++)
        {
            var recs = rec.Workshops[i < numDuplicates ? 0 : 1 + (i - numDuplicates)];
            foreach (var r in recs)
            {
                _sched.ScheduleItemToWorkshop(r.CraftObjectId, r.Slot, cycle, i);
            }
        }
    }

    private void ApplyRecommendationToCurrentCycle(DayRec rec)
    {
        var cycle = _sched.CurrentCycle;
        ApplyRecommendation(cycle, rec);
        _sched.SetCurrentCycle(cycle); // needed to refresh the ui
    }

    private void ApplyRecommendations(bool nextWeek)
    {
        // TODO: clear recs!

        try
        {
            if (Recommendations.Schedules.Count > 5)
                throw new Exception($"Too many days in recs: {Recommendations.Schedules.Count}");

            uint forbiddenCycles = nextWeek ? 0 : (1u << (_sched.CycleInProgress + 1)) - 1;
            if ((Recommendations.CyclesMask & forbiddenCycles) != 0)
                throw new Exception("Some of the cycles in schedule are already in progress or are done");

            var currentRestCycles = nextWeek ? _sched.RestCycles >> 7 : _sched.RestCycles & 0x7F;
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

                var newRest = nextWeek ? (rest << 7) | (_sched.RestCycles & 0x7F) : (_sched.RestCycles & 0x3F80) | rest;
                _sched.SetRestCycles(newRest);
            }

            var cycle = _sched.CurrentCycle;
            foreach (var (c, r) in Recommendations.Enumerate())
                ApplyRecommendation(c - 1 + (nextWeek ? 7 : 0), r);
            _sched.SetCurrentCycle(cycle); // needed to refresh the ui
        }
        catch (Exception ex)
        {
            ReportError($"Error: {ex.Message}");
        }
    }

    private static void ReportError(string msg)
    {
        Service.Log.Error(msg);
        Service.ChatGui.PrintError(msg);
    }
}
