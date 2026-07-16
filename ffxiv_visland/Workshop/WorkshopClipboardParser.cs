using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace visland.Workshop;

internal unsafe class WorkshopClipboardParser(ExcelSheet<MJICraftworksObject> craftSheet, List<string> botNames) {
    public WorkshopSolver.Recs ParseRecs(string str) {
        var result = new WorkshopSolver.Recs();

        var curRec = new WorkshopSolver.DayRec();
        var nextSlot = 24;
        var curCycle = 0;
        foreach (var l in str.Split('\n', '\r')) {
            if (TryParseCycleStart(l, out var cycle)) {
                result.Add(curCycle > 0 ? curCycle : cycle - 1, curRec);
                curRec = new();
                nextSlot = 24;
                curCycle = cycle;
            }
            else if (l is "First 3 Workshops" or "All Workshops") {
                if (!curRec.Empty)
                    throw new Exception("Unexpected start of 1st workshop recs");
            }
            else if (l == "4th Workshop") {
                nextSlot = 24;
            }
            else if (TryParseItem(l) is var item && item != null) {
                if (nextSlot + item.Value.CraftingTime > 24) {
                    curRec.Workshops.Add(new());
                    nextSlot = 0;
                }
                curRec.Workshops.Last().Add(nextSlot, item.Value.RowId);
                nextSlot += item.Value.CraftingTime;
            }
            else
                Service.Log.Verbose($"Failed to parse {l}");
        }
        result.Add(curCycle > 0 ? curCycle : (AgentMJICraftSchedule.Instance()->Data->CycleInProgress + 2) % 8, curRec);

        return result;
    }

    public List<WorkshopSolver.WorkshopRec> ParseRecOverrides(string str) {
        var result = new List<WorkshopSolver.WorkshopRec>();
        var nextSlot = 24;

        foreach (var l in str.Split('\n', '\r')) {
            if (l.StartsWith("Schedule #")) {
                nextSlot = 24;
            }
            else if (TryParseItem(l) is var item && item != null) {
                if (nextSlot + item.Value.CraftingTime > 24) {
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

    private static bool TryParseCycleStart(string str, out int cycle) {
        if (str.StartsWith("Cycle "))
            return int.TryParse(str.AsSpan(6, 1), out cycle);
        if (str.StartsWith("Season ") && str.IndexOf(", Cycle ") is var cycleStart && cycleStart > 0)
            return int.TryParse(str.AsSpan(cycleStart + 8, 1), out cycle);
        cycle = 0;
        return false;
    }

    private MJICraftworksObject? TryParseItem(string line) {
        var matchingRows = botNames.Select((n, i) => (n, i)).Where(t => !string.IsNullOrEmpty(t.n) && IsMatch(line, t.n)).ToList();
        if (matchingRows.Count > 1) {
            matchingRows = [.. matchingRows.OrderByDescending(t => MatchingScore(t.n, line))];
            Service.Log.Info($"Row '{line}' matches {matchingRows.Count} items: {string.Join(", ", matchingRows.Select(r => r.n))}\n" +
                "First one is most likely the correct match. Please report if this is wrong.");
        }
        return matchingRows.Count > 0 ? craftSheet.GetRow((uint)matchingRows.First().i) : null;
    }

    private static bool IsMatch(string x, string y) => Regex.IsMatch(x, $@"\b{Regex.Escape(y)}\b");

    private static int MatchingScore(string item, string line) {
        var score = 0;
        if (line.Contains(item))
            score += item.Length;
        return score;
    }
}
