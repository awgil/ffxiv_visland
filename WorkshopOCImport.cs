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
    public class DayRec
    {
        public int CycleNumber; // 0 means current
        public List<(int slot, uint item)> MainRecs = new(); // workshops 1-3
        public List<(int slot, uint item)> SideRecs = new(); // workshop 4

        public bool Empty => CycleNumber == 0 && MainRecs.Count == 0 && SideRecs.Count == 0;
    }

    public class Recs
    {
        public List<DayRec> Cycles = new();

        public bool Empty => Cycles.Count == 0;
        public bool MultiDay => Cycles.Any(c => c.CycleNumber != 0);

        public void Add(DayRec day)
        {
            if (MultiDay)
            {
                if (day.CycleNumber == 0)
                    throw new Exception("Multi-day / single-day rec mismatch");
                if (Cycles.Any(c => c.CycleNumber == day.CycleNumber))
                    throw new Exception("Duplicate entries for a single day");
            }
            else if (!Empty)
                throw new Exception("Multi-day / single-day rec mismatch");

            if (day.MainRecs.Count + day.SideRecs.Count > 0)
                Cycles.Add(day);
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
        else
        {
            foreach (var r in Recommendations.Cycles)
            {
                ImGui.SameLine();
                if (ImGui.Button($"C{r.CycleNumber}"))
                    ApplyRecommendationToCurrentCycle(r);
            }
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
        foreach (var r in Recommendations.Cycles)
        {
            ImGui.TextUnformatted($"Cycle {r.CycleNumber}:");
            ImGui.Indent();
            ImGui.TextUnformatted($"Main: {string.Join(", ", r.MainRecs.Select(r => $"{r.slot}={OfficialNameToBotName(sheetCraft.GetRow(r.item)?.Item.Value?.Name ?? "")}"))}");
            ImGui.TextUnformatted($"Side: {string.Join(", ", r.SideRecs.Select(r => $"{r.slot}={OfficialNameToBotName(sheetCraft.GetRow(r.item)?.Item.Value?.Name ?? "")}"))}");
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
            var newRecs = new Recs();
            var curRec = new DayRec();

            int completeTargets = 0;
            int curTargets = 0; // 1 for main, 2 for side, 3 for both
            int nextSlot = 0;
            void StartTarget(int mask)
            {
                if ((completeTargets & mask) != 0)
                    throw new Exception("Multiple workshop recs of a single kind");
                completeTargets |= curTargets;
                curTargets = mask;
                nextSlot = 0;
            }

            foreach (var l in str.Split('\n', '\r'))
            {
                if (l.StartsWith("Cycle "))
                {
                    if (!curRec.Empty)
                        newRecs.Add(curRec);
                    curRec = new() { CycleNumber = int.Parse(l.Remove(0, 6)) };
                    completeTargets = curTargets = 0;
                    nextSlot = 0;
                }
                else if (l == "First 3 Workshops")
                {
                    StartTarget(1);
                }
                else if (l == "4th Workshop")
                {
                    StartTarget(2);
                }
                else if (l == "All Workshops")
                {
                    StartTarget(3);
                }
                else if (l.StartsWith(":OC_"))
                {
                    if (curTargets == 0)
                        throw new Exception("Recommendation without defined target workshop");

                    var item = ParseBotName(l);
                    if (item == null)
                        continue;

                    if ((curTargets & 1) != 0)
                        curRec.MainRecs.Add((nextSlot, item.RowId));
                    if ((curTargets & 2) != 0)
                        curRec.SideRecs.Add((nextSlot, item.RowId));
                    nextSlot += item.CraftingTime;
                }
            }
            if (!curRec.Empty)
                newRecs.Add(curRec);

            Recommendations = newRecs;
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
            var overrideRecs = new Recs();
            var curRec = new DayRec();
            int nextSlot = 0;

            foreach (var l in str.Split('\n', '\r'))
            {
                if (l.StartsWith("Schedule #"))
                {
                    if (!curRec.Empty)
                        overrideRecs.Add(curRec);
                    curRec = new() { CycleNumber = int.Parse(l.Remove(0, 10)) };
                    nextSlot = 0;
                }
                else if (l.StartsWith(":OC_"))
                {
                    var item = ParseBotName(l);
                    if (item == null)
                        continue;
                    curRec.SideRecs.Add((nextSlot, item.RowId));
                    nextSlot += item.CraftingTime;
                }
            }
            if (!curRec.Empty)
                overrideRecs.Add(curRec);

            var resultRecs = new Recs();
            int overrideIndex = 0;
            foreach (var r in Recommendations.Cycles)
            {
                if (r.MainRecs.Count == 0 && r.SideRecs.Count == 0 || overrideIndex >= overrideRecs.Cycles.Count)
                {
                    // rest day, or overrides done - skip
                    resultRecs.Add(r);
                }
                else
                {
                    var curOverrides = overrideRecs.Cycles[overrideIndex++];
                    if ((r.CycleNumber != 0) != (curOverrides.CycleNumber != 0))
                        throw new Exception("Multi-day overrides for single-day schedule or vice versa");
                    resultRecs.Add(new DayRec() { CycleNumber = r.CycleNumber, MainRecs = r.MainRecs, SideRecs = curOverrides.SideRecs });
                }
            }

            if (overrideRecs.Cycles.Skip(overrideIndex).Any(r => r.SideRecs.Count > 0))
                throw new Exception("Override is longer than schedule!");

            Recommendations = resultRecs;
        }
        catch (Exception ex)
        {
            ReportError($"Error: {ex.Message}");
        }
    }

    private MJICraftworksObject? ParseBotName(string l)
    {
        // expected format: ':OC_ItemName: Item Name (4h)'
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

    private string OfficialNameToBotName(string name)
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
            _sched.ScheduleItemToWorkshop(r.item, r.slot, cycle, 0);
            _sched.ScheduleItemToWorkshop(r.item, r.slot, cycle, 1);
            _sched.ScheduleItemToWorkshop(r.item, r.slot, cycle, 2);
        }
        foreach (var r in rec.SideRecs)
        {
            _sched.ScheduleItemToWorkshop(r.item, r.slot, cycle, 3);
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
            if (Recommendations.Cycles.Count > 5)
                throw new Exception($"Too many days in recs: {Recommendations.Cycles.Count}");

            uint setCycles = 0;
            int minAllowedCycle = nextWeek ? 0 : _sched.CycleInProgress + 1;
            foreach (var r in Recommendations.Cycles)
            {
                if (r.CycleNumber < minAllowedCycle + 1)
                    throw new Exception($"Cycle {r.CycleNumber} is already in progress");
                var mask = 1u << (r.CycleNumber - 1);
                if ((setCycles & mask) != 0)
                    throw new Exception($"Duplicate cycle number {r.CycleNumber}");
                setCycles |= mask;
            }

            var currentRestCycles = nextWeek ? (_sched.RestCycles >> 7) : (_sched.RestCycles & 0x7F);
            if ((currentRestCycles & setCycles) != 0)
            {
                // we need to change rest cycles - set to C1 and last unused
                var freeCycles = ~setCycles & 0x7F;
                if ((freeCycles & 1) == 0)
                    throw new Exception($"Sorry, we assume C1 is always rest - set rest days manually to match your schedule");
                var rest = (1u << (31 - BitOperations.LeadingZeroCount(freeCycles))) | 1;
                if (BitOperations.PopCount(rest) != 2)
                    throw new Exception($"Something went wrong, failed to determine rest days");

                var newRest = nextWeek ? ((freeCycles << 7) | (_sched.RestCycles & 0x7F)) : ((_sched.RestCycles & 0x3F80) | freeCycles);
                _sched.SetRestCycles(newRest);
            }

            var cycle = _sched.CurrentCycle;
            foreach (var r in Recommendations.Cycles)
                ApplyRecommendation(r.CycleNumber - 1 + (nextWeek ? 7 : 0), r);
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
