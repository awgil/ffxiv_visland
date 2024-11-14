using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace visland.Workshop;

public class WorkshopSolver
{
    public struct SlotRec(int slot, uint craftObjectId)
    {
        public int Slot = slot;
        public uint CraftObjectId = craftObjectId;
    }

    public class WorkshopRec
    {
        public List<SlotRec> Slots = [];

        public void Add(int slot, uint craftObjectId) => Slots.Add(new(slot, craftObjectId));
    }

    public class DayRec
    {
        public List<WorkshopRec> Workshops = [];

        public bool Empty => Workshops.Count == 0;

        public IEnumerable<(int workshop, WorkshopRec rec)> Enumerate(int maxWorkshops)
        {
            if (Workshops.Count == 0)
                yield break;
            // first schedule is duplicated if we have less schedules than workshops to fill: numDuplicates + (Count-1) == maxWorkshops
            var numDuplicates = 1 + Math.Max(0, maxWorkshops - Workshops.Count);
            for (var i = 0; i < numDuplicates; ++i)
                yield return (i, Workshops[0]);
            // remaining: schedule #1 maps to workshop #numDuplicates etc
            for (var i = 1; i < Workshops.Count; ++i)
                yield return (numDuplicates + i - 1, Workshops[i]);
        }
    }

    public class Recs
    {
        private List<DayRec> _schedules = [];
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

    public class Popularity
    {
        private int[] _values = { };

        public float Multiplier(uint objId) => objId < _values.Length ? 0.01f * _values[objId] : 1;

        public void Set(uint rowId)
        {
            var popRow = Service.LuminaRow<MJICraftworksPopularity>(rowId);
            _values = popRow != null ? new int[popRow.Value.Popularity.Count] : [];
            for (var i = 0; i < _values.Length; ++i)
                _values[i] = popRow?.Popularity[i].Value.Ratio ?? 100;
        }
    }

    public struct FavorState
    {
        public uint[] CraftObjectIds;
        public int[] CompletedCounts;
        public Popularity Popularity;

        public FavorState()
        {
            CraftObjectIds = new uint[3];
            CompletedCounts = new int[3];
            Popularity = new();
        }

        public FavorState(uint favor4Id, uint favor6Id, uint favor8Id, int complete4 = 0, int complete6 = 0, int complete8 = 0) : this()
        {
            CraftObjectIds[0] = favor4Id;
            CraftObjectIds[1] = favor6Id;
            CraftObjectIds[2] = favor8Id;
            CompletedCounts[0] = complete4;
            CompletedCounts[1] = complete6;
            CompletedCounts[2] = complete8;
        }
    }

    public static bool IsLinked(MJICraftworksObject l, MJICraftworksObject r)
    {
        if (l.RowId == r.RowId)
            return false; // object is never linked with itself
        var l1 = l.Theme[0].Value.RowId;
        var l2 = l.Theme[1].Value.RowId;
        var r1 = r.Theme[0].Value.RowId;
        var r2 = r.Theme[1].Value.RowId;
        return l1 == r1 || l1 == r2 || l2 != 0 && (l2 == r1 || l2 == r2);
    }

    public static IEnumerable<MJICraftworksObject> Linked(MJICraftworksObject obj, int duration) => Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>()!.Where(o => o.CraftingTime == duration && IsLinked(o, obj));
}
