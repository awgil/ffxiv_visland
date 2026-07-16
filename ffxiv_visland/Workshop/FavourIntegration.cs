using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace visland.Workshop;

public enum FavourMode {
    None = 0,
    [Description("Keep workshops 1-3, fill 4 with favours")]
    ReplaceWorkshop4 = 1,
    [Description("Try to keep high-value crafts, try same-duration substitutions, sacrifice lowest-value days/workshops")]
    MinMax = 2,
    [Description("MinMax but use the second rest day for most of the favours")]
    MinMaxFreeRestDay = 3,
}

public static class FavourIntegration {
    public static readonly int[] FavourTargets = [8, 6, 8]; // 4h / 6h / 8h

    public static WorkshopSolver.Recs Apply(WorkshopSolver.Recs baseRecs, FavourMode mode, WorkshopSolver.FavourState favours, ExcelSheet<MJICraftworksObject> sheet, IEnumerable<int>? ocRestCycles = null) {
        if (mode == FavourMode.None)
            return Clone(baseRecs);

        var days = baseRecs.Enumerate().ToDictionary(e => e.cycle, e => CloneDay(e.rec));
        var freeRestCycle = 0;
        if (mode == FavourMode.MinMaxFreeRestDay) {
            freeRestCycle = ocRestCycles?.Where(c => c is >= 2 and <= 7).OrderBy(c => c).FirstOrDefault() ?? 0;
            if (freeRestCycle != 0 && !days.ContainsKey(freeRestCycle))
                days[freeRestCycle] = new WorkshopSolver.DayRec();
        }

        var credited = (int[])favours.CompletedCounts.Clone();
        if (mode == FavourMode.ReplaceWorkshop4) {
            // Only credit the kept main pattern (WS1-3). A single workshop entry is duplicated across them.
            foreach (var day in days.Values) {
                if (day.Workshops.Count == 0)
                    continue;
                var mainCopies = Math.Min(3, 1 + Math.Max(0, 4 - Math.Max(day.Workshops.Count, 1)));
                for (var i = 0; i < mainCopies; ++i)
                    CreditWorkshop(day.Workshops[0], favours.CraftObjectIds, credited, sheet);
            }
        }
        else {
            CreditSchedule(days.Values, favours.CraftObjectIds, credited, sheet);
        }

        if (mode is FavourMode.MinMax or FavourMode.MinMaxFreeRestDay) {
            foreach (var day in days.Values)
                TrySubstitutions(day, favours.CraftObjectIds, credited, sheet);
            credited = (int[])favours.CompletedCounts.Clone();
            CreditSchedule(days.Values, favours.CraftObjectIds, credited, sheet);
        }

        if (NeedsMet(credited))
            return ToRecs(days);

        List<WorkshopSolver.WorkshopRec> favourDays;
        try {
            favourDays = new WorkshopSolverFavourSheet(CloneFavourState(favours, credited)).Recs;
        }
        catch (Exception ex) {
            Service.Log.Error($"Favour sheet solver failed: {ex.Message}");
            return ToRecs(days);
        }

        return mode switch {
            FavourMode.ReplaceWorkshop4 => PlaceOnWorkshop4(days, favourDays),
            FavourMode.MinMax or FavourMode.MinMaxFreeRestDay
                => PlaceMinMax(days, favourDays, favours.Popularity, sheet, freeRestCycle),
            _ => ToRecs(days),
        };
    }

    private static WorkshopSolver.Recs PlaceOnWorkshop4(Dictionary<int, WorkshopSolver.DayRec> days, List<WorkshopSolver.WorkshopRec> favourDays) {
        var fi = 0;
        foreach (var cycle in days.Keys.OrderBy(c => c)) {
            if (fi >= favourDays.Count)
                break;
            var day = days[cycle];
            EnsureWorkshopSlots(day, 2);
            day.Workshops[1] = CloneWorkshop(favourDays[fi++]);
        }

        if (fi < favourDays.Count)
            Service.Log.Warning($"Could not fit {favourDays.Count - fi} favour day(s) into workshop 4 slots");
        return ToRecs(days);
    }

    private static WorkshopSolver.Recs PlaceMinMax(Dictionary<int, WorkshopSolver.DayRec> days, List<WorkshopSolver.WorkshopRec> favourDays, WorkshopSolver.Popularity popularity, ExcelSheet<MJICraftworksObject> sheet, int freeRestCycle) {
        var remaining = new Queue<WorkshopSolver.WorkshopRec>(favourDays.Select(CloneWorkshop));

        // Freed rest day: dump as many favour workshops as possible.
        if (freeRestCycle != 0 && days.TryGetValue(freeRestCycle, out var freeDay)) {
            freeDay.Workshops.Clear();
            while (remaining.Count > 0 && freeDay.Workshops.Count < 4)
                freeDay.Workshops.Add(remaining.Dequeue());
        }

        // Workshop 4 on other days, lowest estimated value first.
        foreach (var cycle in days.Keys.OrderBy(c => EstimateDayValue(days[c], popularity, sheet))) {
            if (remaining.Count == 0)
                break;
            if (cycle == freeRestCycle)
                continue;
            var day = days[cycle];
            EnsureWorkshopSlots(day, 2);
            day.Workshops[1] = remaining.Dequeue();
        }

        // Grow to 3 then 4 distinct workshop schedules on low-value days (keeps main + prior overrides).
        foreach (var targetCount in new[] { 3, 4 }) {
            foreach (var cycle in days.Keys.OrderBy(c => EstimateDayValue(days[c], popularity, sheet))) {
                if (remaining.Count == 0)
                    break;
                if (cycle == freeRestCycle)
                    continue;
                var day = days[cycle];
                if (day.Workshops.Count == 0)
                    day.Workshops.Add(new());
                if (day.Workshops.Count < targetCount)
                    day.Workshops.Add(remaining.Dequeue());
            }
        }

        // Last resort: whole-day favour overwrite on lowest-value days.
        foreach (var cycle in days.Keys.OrderBy(c => EstimateDayValue(days[c], popularity, sheet))) {
            if (remaining.Count == 0)
                break;
            if (cycle == freeRestCycle)
                continue;
            var day = days[cycle];
            day.Workshops.Clear();
            day.Workshops.Add(remaining.Dequeue());
        }

        if (remaining.Count > 0)
            Service.Log.Warning($"Favour min-max could not fully fit remaining favour day(s): {remaining.Count} left");

        return ToRecs(days);
    }

    private static void TrySubstitutions(WorkshopSolver.DayRec day, uint[] favourIds, int[] complete, ExcelSheet<MJICraftworksObject> sheet) {
        foreach (var workshop in day.Workshops) {
            for (var i = 0; i < workshop.Slots.Count; ++i) {
                if (!TryRow(sheet, workshop.Slots[i].CraftObjectId, out var current))
                    continue;

                for (var f = 0; f < favourIds.Length; ++f) {
                    if (complete[f] >= FavourTargets[f])
                        continue;
                    if (!TryRow(sheet, favourIds[f], out var favour))
                        continue;
                    if (favour.CraftingTime != current.CraftingTime)
                        continue;
                    if (favour.RowId == current.RowId)
                        break;

                    MJICraftworksObject? prev = i > 0 && TryRow(sheet, workshop.Slots[i - 1].CraftObjectId, out var p) ? p : null;
                    MJICraftworksObject? next = i + 1 < workshop.Slots.Count && TryRow(sheet, workshop.Slots[i + 1].CraftObjectId, out var n) ? n : null;

                    var oldPrevLink = prev != null && WorkshopSolver.IsLinked(prev.Value, current);
                    var oldNextLink = next != null && WorkshopSolver.IsLinked(current, next.Value);
                    var newPrevLink = prev != null && WorkshopSolver.IsLinked(prev.Value, favour);
                    var newNextLink = next != null && WorkshopSolver.IsLinked(favour, next.Value);
                    if (oldPrevLink && !newPrevLink)
                        continue;
                    if (oldNextLink && !newNextLink)
                        continue;
                    if (!SharesTheme(current, favour) && (oldPrevLink || oldNextLink))
                        continue;

                    workshop.Slots[i] = new WorkshopSolver.SlotRec(workshop.Slots[i].Slot, favour.RowId);
                    complete[f] += newPrevLink ? 2 : 1;
                    break;
                }
            }
        }
    }

    private static bool SharesTheme(MJICraftworksObject a, MJICraftworksObject b) {
        var a1 = a.Theme[0].RowId;
        var a2 = a.Theme[1].RowId;
        var b1 = b.Theme[0].RowId;
        var b2 = b.Theme[1].RowId;
        return a1 != 0 && (a1 == b1 || a1 == b2) || a2 != 0 && (a2 == b1 || a2 == b2);
    }

    public static void CreditSchedule(IEnumerable<WorkshopSolver.DayRec> days, uint[] favourIds, int[] complete, ExcelSheet<MJICraftworksObject> sheet) {
        foreach (var day in days)
            foreach (var workshop in day.Workshops)
                CreditWorkshop(workshop, favourIds, complete, sheet);
    }

    public static void CreditWorkshop(WorkshopSolver.WorkshopRec workshop, uint[] favourIds, int[] complete, ExcelSheet<MJICraftworksObject> sheet) {
        MJICraftworksObject? prev = null;
        foreach (var slot in workshop.Slots) {
            if (!TryRow(sheet, slot.CraftObjectId, out var row))
                continue;
            var idx = Array.IndexOf(favourIds, row.RowId);
            if (idx >= 0)
                complete[idx] += prev != null && WorkshopSolver.IsLinked(prev.Value, row) ? 2 : 1;
            prev = row;
        }
    }

    private static float EstimateDayValue(WorkshopSolver.DayRec day, WorkshopSolver.Popularity pop, ExcelSheet<MJICraftworksObject> sheet)
        => day.Workshops.Sum(w => EstimateWorkshopValue(w, pop, sheet));

    private static float EstimateWorkshopValue(WorkshopSolver.WorkshopRec workshop, WorkshopSolver.Popularity pop, ExcelSheet<MJICraftworksObject> sheet) {
        float total = 0;
        MJICraftworksObject? prev = null;
        foreach (var slot in workshop.Slots) {
            if (!TryRow(sheet, slot.CraftObjectId, out var row))
                continue;
            var mult = pop.Multiplier(row.RowId);
            var efficiency = prev != null && WorkshopSolver.IsLinked(prev.Value, row) ? 2f : 1f;
            total += row.Value * mult * efficiency;
            prev = row;
        }
        return total;
    }

    private static bool NeedsMet(int[] complete) {
        for (var i = 0; i < FavourTargets.Length; ++i)
            if (complete[i] < FavourTargets[i])
                return false;
        return true;
    }

    private static WorkshopSolver.FavourState CloneFavourState(WorkshopSolver.FavourState src, int[] complete) {
        var s = new WorkshopSolver.FavourState();
        Array.Copy(src.CraftObjectIds, s.CraftObjectIds, 3);
        Array.Copy(complete, s.CompletedCounts, 3);
        s.Popularity = src.Popularity;
        return s;
    }

    private static void EnsureWorkshopSlots(WorkshopSolver.DayRec day, int count) {
        if (day.Workshops.Count == 0)
            day.Workshops.Add(new());
        while (day.Workshops.Count < count)
            day.Workshops.Add(CloneWorkshop(day.Workshops[0]));
    }

    private static WorkshopSolver.Recs Clone(WorkshopSolver.Recs src) {
        var result = new WorkshopSolver.Recs();
        foreach (var (cycle, day) in src.Enumerate())
            result.Add(cycle, CloneDay(day));
        return result;
    }

    private static WorkshopSolver.DayRec CloneDay(WorkshopSolver.DayRec src) {
        var day = new WorkshopSolver.DayRec();
        foreach (var w in src.Workshops)
            day.Workshops.Add(CloneWorkshop(w));
        return day;
    }

    private static WorkshopSolver.WorkshopRec CloneWorkshop(WorkshopSolver.WorkshopRec src) {
        var w = new WorkshopSolver.WorkshopRec();
        foreach (var s in src.Slots)
            w.Slots.Add(new WorkshopSolver.SlotRec(s.Slot, s.CraftObjectId));
        return w;
    }

    private static WorkshopSolver.Recs ToRecs(Dictionary<int, WorkshopSolver.DayRec> days) {
        var result = new WorkshopSolver.Recs();
        foreach (var (cycle, day) in days.OrderBy(kv => kv.Key)) {
            if (!day.Empty)
                result.Add(cycle, day);
        }
        return result;
    }

    private static bool TryRow(ExcelSheet<MJICraftworksObject> sheet, uint id, out MJICraftworksObject row) {
        if (sheet.TryGetRow(id, out row))
            return true;
        row = default;
        return false;
    }
}
