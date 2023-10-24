using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Lumina.Data;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace visland;

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
        public List<Rec> MainRecs; // workshops 1-3
        public List<Rec> SideRecs; // workshop 4, by default same as 1-3

        public DayRec()
        {
            MainRecs = SideRecs = new();
        }

        public bool Empty => MainRecs.Count + SideRecs.Count == 0;
    }

    public class Recs
    {
        // if cycles mask == 0, main/side recs contain 0 (empty) or 1 (single-day) entries
        // otherwise main/side recs size is equal to mask's popcount
        private List<DayRec> _schedules = new();
        public uint CyclesMask { get; private set; }
        public IReadOnlyList<DayRec> Schedules => _schedules;

        public bool Empty => Schedules.Count == 0;
        public bool SingleDay => Schedules.Count > 0 && CyclesMask == 0;
        public bool MultiDay => CyclesMask != 0;

        public void Add(int cycle, DayRec schedule)
        {
            if (schedule.Empty)
                return; // don't care, rest day or something

            if (cycle == 0)
            {
                // single-day rec can only be added to the empty rec list
                if (!Empty)
                    throw new Exception(MultiDay ? "Trying to add a single-day rec to a multi-day schedule" : "Trying to add several single-day recs");
                _schedules.Add(schedule);
            }
            else
            {
                // multi-day rec can only be added to the empty rec list or a multi-day rec list that doesn't have this or future days set
                if (SingleDay)
                    throw new Exception("Invalid multi-day rec; make sure that first 'cycle X' line is included");
                var mask = 1u << (cycle - 1);
                if ((CyclesMask & mask) != 0)
                    throw new Exception($"Duplicate cycle {cycle} in the recs");
                if ((CyclesMask & ~(mask - 1)) != 0)
                    throw new Exception($"Bad cycle order: {cycle} found after future days");

                _schedules.Add(schedule);
                CyclesMask |= mask;
            }
        }

        public IEnumerable<(int cycle, DayRec rec)> Enumerate()
        {
            if (MultiDay)
            {
                var m = CyclesMask;
                foreach (var r in Schedules)
                {
                    var c = BitOperations.TrailingZeroCount(m);
                    yield return (c + 1, r);
                    m &= ~(1u << c);
                }
            }
            else if (Schedules.Count > 0)
            {
                yield return (0, Schedules[0]);
            }
        }
    }

    public Recs Recommendations = new();

    private WorkshopFavors _favors = new();
    private WorkshopSchedule _sched = new();

    public unsafe void Draw()
    {
        ImGui.TextUnformatted("This tab allows copy-pasting recommendations from Overseas Casuals discord");

        if (ImGui.Button("Start by copying any type of recs (single day or multi day) to clipboard and click here to import"))
            ImportRecs(ImGui.GetClipboardText());

        if (Recommendations.Empty)
            return;

        ImGui.TextUnformatted("Favour support: first click one of the buttons to generate bot command, paste it into bot-spam channel, then copy output");

        if (ImGui.Button("This week favors"))
            ImGui.SetClipboardText(CreateFavorRequestCommand(false));
        ImGui.SameLine();
        if (ImGui.Button("Next week favors"))
            ImGui.SetClipboardText(CreateFavorRequestCommand(true));

        if (ImGui.Button("Override 4th workshop with favor schedules from clipboard"))
            OverrideSideRecs(ImGui.GetClipboardText());

        ImGui.Separator();

        ImGui.TextUnformatted("Set single day schedule:");
        if (_sched.CurrentCycle <= _sched.CycleInProgress)
        {
            ImGui.SameLine();
            ImGui.TextUnformatted("Current cycle is already in progress");
        }
        else if (!_sched.CurrentCycleIsEmpty())
        {
            ImGui.SameLine();
            ImGui.TextUnformatted("Clear schedule first");
        }
        else if (Recommendations.MultiDay)
        {
            foreach (var (c, r) in Recommendations.Enumerate())
            {
                ImGui.SameLine();
                if (ImGui.Button($"C{c}"))
                    ApplyRecommendationToCurrentCycle(r);
            }
        }
        else
        {
            ImGui.SameLine();
            if (ImGui.Button("C0"))
                ApplyRecommendation(0, Recommendations.Schedules.First());
        }

        ImGui.TextUnformatted("Set full week schedule:");
        using (ImRaii.Disabled(!Recommendations.MultiDay))
        {
            ImGui.SameLine();
            if (ImGui.Button("This week"))
                ApplyRecommendations(false);
            ImGui.SameLine();
            if (ImGui.Button("Next week"))
                ApplyRecommendations(true);
        }

        ImGui.TextUnformatted("Current recs:");
        var sheetCraft = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>(Language.English)!;
        foreach (var (c, r) in Recommendations.Enumerate())
        {
            ImGui.TextUnformatted($"Cycle {c}:");
            ImGui.Indent();
            ImGui.TextUnformatted($"Main: {string.Join(", ", r.MainRecs.Select(r => $"{r.Slot}={OfficialNameToBotName(sheetCraft.GetRow(r.CraftObjectId)?.Item.Value?.Name ?? "")}"))}");
            ImGui.TextUnformatted($"Side: {string.Join(", ", r.SideRecs.Select(r => $"{r.Slot}={OfficialNameToBotName(sheetCraft.GetRow(r.CraftObjectId)?.Item.Value?.Name ?? "")}"))}");
            ImGui.Unindent();
        }
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
            var name = sheetCraft.GetRow(id)?.Item.Value?.Name;
            if (name != null)
                res += $" favor{i + 1}:{OfficialNameToBotName(name)}";
        }
        return res;
    }

    private void ImportRecs(string str)
    {
        try
        {
            Recommendations = ParseRecs(str);
        }
        catch (Exception ex)
        {
            ReportError($"Error: {ex.Message}");
        }
    }

    private void OverrideSideRecs(string str)
    {
        try
        {
            var overrideRecs = ParseRecOverrides(str);
            if (overrideRecs.Count > Recommendations.Schedules.Count)
                throw new Exception($"Override list is longer than base schedule: {overrideRecs.Count} > {Recommendations.Schedules.Count}");

            foreach (var (r, o) in Recommendations.Schedules.Zip(overrideRecs))
                r.SideRecs = o.SideRecs;
        }
        catch (Exception ex)
        {
            ReportError($"Error: {ex.Message}");
        }
    }

    private static Recs ParseRecs(string str)
    {
        var result = new Recs();

        var curRec = new DayRec();
        var curWS = curRec.MainRecs; // start filling both recommendations (people might not copy first line)
        int nextSlot = 0;
        int curCycle = 0;
        foreach (var l in str.Split('\n', '\r'))
        {
            if (TryParseCycleStart(l, out var cycle))
            {
                result.Add(curCycle, curRec);
                curRec = new();
                curWS = curRec.MainRecs;
                nextSlot = 0;
                curCycle = cycle;
            }
            else if (l == "First 3 Workshops")
            {
                if (curWS.Count > 0 || nextSlot != 0)
                    throw new Exception("Unexpected start of 1-3 workshop recs");
                curRec.SideRecs = new(); // separate list for side recs, do that here in case user didn't copy 4th workshop at all
            }
            else if (l == "4th Workshop")
            {
                curWS = curRec.SideRecs = new(); // separate list for side recs
                nextSlot = 0;
            }
            else if (l == "All Workshops")
            {
                if (curWS.Count > 0 || nextSlot != 0)
                    throw new Exception("Unexpected start of all workshop recs");
            }
            else if (TryParseBotName(l) is var item && item != null)
            {
                curWS.Add(new(nextSlot, item.RowId));
                nextSlot += item.CraftingTime;
            }
        }
        result.Add(curCycle, curRec);

        return result;
    }

    private static List<DayRec> ParseRecOverrides(string str)
    {
        var result = new List<DayRec>();
        var curRec = new DayRec();
        int nextSlot = 0;

        foreach (var l in str.Split('\n', '\r'))
        {
            if (l.StartsWith("Schedule #"))
            {
                if (!curRec.Empty)
                    result.Add(curRec);
                curRec = new();
                nextSlot = 0;
            }
            else if (TryParseBotName(l) is var item && item != null)
            {
                curRec.SideRecs.Add(new(nextSlot, item.RowId));
                nextSlot += item.CraftingTime;
            }
        }
        if (!curRec.Empty)
            result.Add(curRec);

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

    private static MJICraftworksObject? TryParseBotName(string l)
    {
        // expected format: ':OC_ItemName: Item Name (4h)'
        if (!l.StartsWith(":OC_"))
            return null;

        // strip off everything before last ':' and everything after first '(', then strip off spaces
        var actualItem = l.Substring(l.LastIndexOf(':') + 1);
        if (actualItem.IndexOf('(') is var tail && tail >= 0)
            actualItem = actualItem.Substring(0, tail);
        actualItem = actualItem.Trim();
        if (actualItem.Length == 0)
            return null;

        var matchingRows = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>(Language.English)!.Where(row => OfficialNameToBotName(row.Item.Value?.Name ?? "") == actualItem).ToList();
        if (matchingRows.Count != 1)
            throw new Exception($"Failed to import schedule: {matchingRows.Count} items matching row '{l}': {string.Join(", ", matchingRows.Select(r => r.RowId))}");

        return matchingRows.First();
    }

    private static string OfficialNameToBotName(string name)
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

    private void ApplyRecommendation(int cycle, DayRec rec)
    {
        foreach (var r in rec.MainRecs)
        {
            _sched.ScheduleItemToWorkshop(r.CraftObjectId, r.Slot, cycle, 0);
            _sched.ScheduleItemToWorkshop(r.CraftObjectId, r.Slot, cycle, 1);
            _sched.ScheduleItemToWorkshop(r.CraftObjectId, r.Slot, cycle, 2);
        }
        foreach (var r in rec.SideRecs)
        {
            _sched.ScheduleItemToWorkshop(r.CraftObjectId, r.Slot, cycle, 3);
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

            uint forbiddenCycles = nextWeek ? 0 : (1u << _sched.CycleInProgress) - 1;
            if ((Recommendations.CyclesMask & forbiddenCycles) != 0)
                throw new Exception("Some of the cycles in schedule are already in progress or are done");

            var currentRestCycles = nextWeek ? (_sched.RestCycles >> 7) : (_sched.RestCycles & 0x7F);
            if ((currentRestCycles & Recommendations.CyclesMask) != 0)
            {
                // we need to change rest cycles - set to C1 and last unused
                var freeCycles = ~Recommendations.CyclesMask & 0x7F;
                if ((freeCycles & 1) == 0)
                    throw new Exception($"Sorry, we assume C1 is always rest - set rest days manually to match your schedule");
                var rest = (1u << (31 - BitOperations.LeadingZeroCount(freeCycles))) | 1;
                if (BitOperations.PopCount(rest) != 2)
                    throw new Exception($"Something went wrong, failed to determine rest days");

                var newRest = nextWeek ? ((freeCycles << 7) | (_sched.RestCycles & 0x7F)) : ((_sched.RestCycles & 0x3F80) | freeCycles);
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

    private void ReportError(string msg)
    {
        Service.Log.Error(msg);
        Service.ChatGui.PrintError(msg);
    }
}
