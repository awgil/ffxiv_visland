using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using visland.Helpers;

namespace visland.Workshop;

// this is taken from kd3's google sheet
public class WorkshopSolverFavorSheet
{
    public enum Strategy { Unknown, NoLinks_No48, AllLinks, NoLinks, Link48, Link46, Link68, Link68_NoF8L4, Link48_NoF6L8 }

    public WorkshopSolver.Popularity Popularity;
    public List<WorkshopSolver.WorkshopRec> Recs;
    public MJICraftworksObject[] Favors;
    public int[] Complete;
    public List<MJICraftworksObject>[][] Links; // [i][j] = links of duration j for favour i
    public Strategy Plan;

    private ExcelSheet<MJICraftworksObject> _sheet;

    public WorkshopSolverFavorSheet(WorkshopSolver.FavorState state)
    {
        _sheet = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>()!;
        Popularity = state.Popularity;
        if (state.CraftObjectIds.Any(i => i == 0))
            throw new Exception("Invalid state");
        Favors = state.CraftObjectIds.Select(i => _sheet.GetRow(i)).ToArray();            

        Complete = [.. state.CompletedCounts];
        Links = Favors.Select(BuildLinks).ToArray();
        Recs = [];

        var f4 = state.CraftObjectIds[0];
        var f6 = state.CraftObjectIds[1];
        var f8 = state.CraftObjectIds[2];
        var f4l4 = Links[0][0].FirstOrDefault().RowId;
        var f4l6 = Links[0][1].FirstOrDefault().RowId;
        var f4l8 = Links[0][2].FirstOrDefault().RowId;
        var f6l4 = Links[1][0].FirstOrDefault().RowId;
        var f6l4alt = Links[1][0].Skip(1).FirstOrDefault().RowId;
        var f6l6 = Links[1][1].FirstOrDefault().RowId;
        var f6l8 = Links[1][2].FirstOrDefault().RowId;
        var f8l4 = Links[2][0].FirstOrDefault().RowId;
        var f8l6 = Links[2][1].FirstOrDefault().RowId;

        var link46 = WorkshopSolver.IsLinked(Favors[0], Favors[1]);
        var link48 = WorkshopSolver.IsLinked(Favors[0], Favors[2]);
        var link68 = WorkshopSolver.IsLinked(Favors[1], Favors[2]);
        var noLinksNo48 = f6l4alt == 0 || f8l4 == 0 || f8l6 == 0; // very weird condition...
        var allLinks = link46 && link48 && link68;
        var noLinks = !link46 && !link48 && !link68;
        var link68No84 = link68 && f8l4 == 0;
        var link48No68 = link48 && f6l8 == 0; // TODO: sheet checks f6l6, i think this is a bug
        Plan = link68No84 ? Strategy.Link68_NoF8L4  // this seems to have been added in r2
            : link48No68 ? Strategy.Link48_NoF6L8  // this seems to have been added in r3
            : noLinksNo48 ? Strategy.NoLinks_No48 // this seems to have been added in r1
            : allLinks ? Strategy.AllLinks
            : noLinks ? Strategy.NoLinks
            : link48 ? Strategy.Link48
            : link46 ? Strategy.Link46
            : link68 ? Strategy.Link68
            : Strategy.Unknown;

        if (link48)
        {
            while (Complete[2] < 8 || Complete[0] < 6)
                AddDay(f4, f8, f4, f8);

            while (Complete[0] < 8 || Complete[1] < 6)
            {
                if (link46 && link68)
                    AddDayAssertPlan(Strategy.AllLinks, f6, f4, f6, f8);
                else if (f6l8 != 0)
                    AddDayAssertPlan(Strategy.Link48, f6, f6l8, f6, f4);
                else
                    AddDayAssertPlan(Strategy.Link48_NoF6L8, f4, f6l4, f6, f6l4, f6);
            }
        }
        else if (link46 && f8l4 != 0) // TODO: rationalize second check...
        {
            while (Complete[0] < 8 || Complete[1] < 6)
                AddDayAssertPlan(Strategy.Link46, f4, f6, f4, f6, f4);
            while (Complete[2] < 8)
                AddDayAssertPlan(Strategy.Link46, f8l4, f8, f8l4, f8);
        }
        else if (link68)
        {
            if (f8l4 != 0)
            {
                while (Complete[1] < 6)
                    AddDayAssertPlan(Strategy.Link68, f6, f8, f6, f4);
                while (Complete[0] < 8 || Complete[2] < 8)
                    AddDayAssertPlan(Strategy.Link68, f4, f4l4, f4, f8l4, f8);
            }
            else
            {
                while (Complete[0] < 8)
                    AddDayAssertPlan(Strategy.Link68_NoF8L4, f4l4, f4, f4l4, f4, f8);
                while (Complete[1] < 6 || Complete[2] < 8)
                    AddDayAssertPlan(Strategy.Link68_NoF8L4, f6l4, f6, f8, f8l6);
            }
        }
        else
        {
            // no-links variants (also link46_no84 falls here, for whatever reason?)
            if (f8l4 != 0 && f6l4alt != 0)
            {
                while (Complete[0] < 6)
                    AddDayAssertPlan(Strategy.NoLinks, f4l4, f4, f4l4, f4, f4l4, f4);
                while (Complete[1] < 4)
                    AddDayAssertPlan(Strategy.NoLinks, f6l4alt, f6l4, f6, f6l4, f6);
                while (Complete[0] < 8 || Complete[1] < 6)
                    AddDayAssertPlan(Strategy.NoLinks, f4l4, f4, f4l6, f6l4, f6);
                while (Complete[2] < 8)
                    AddDayAssertPlan(Strategy.NoLinks, f8l4, f8, f8l4, f8);
            }
            else
            {
                while (Complete[0] < 8)
                    AddDayAssertPlan(Strategy.NoLinks_No48, f4l4, f4, f4l4, f4, f8);
                while (Complete[1] < 6 || Complete[2] < 8)
                    AddDayAssertPlan(Strategy.NoLinks_No48, f6l4, f6, f8l6, f8);
            }
        }
    }

    private List<MJICraftworksObject>[] BuildLinks(MJICraftworksObject o)
    {
        List<MJICraftworksObject>[] links = [WorkshopSolver.Linked(o, 4).ToList(), WorkshopSolver.Linked(o, 6).ToList(), WorkshopSolver.Linked(o, 8).ToList()];
        foreach (var l in links)
            l.SortByReverse(o => o.Value * Popularity.Multiplier(o.RowId));
        return links;
    }

    private void AddDay(params uint[] objs)
    {
        var rec = new WorkshopSolver.WorkshopRec();
        var hour = 0;
        MJICraftworksObject? prev = null;
        foreach (var obj in objs)
        {
            if (obj == 0)
                throw new Exception($"Invalid obj id {obj}");
            if (_sheet.TryGetRow(obj, out var row))
            {
                rec.Add(hour, obj);
                var iFav = Array.FindIndex(Favors, o => o.RowId == obj);
                if (iFav >= 0)
                {
                    var efficient = prev != null && WorkshopSolver.IsLinked((MJICraftworksObject)prev, row);
                    Complete[iFav] += efficient ? 2 : 1;
                }
                hour += row.CraftingTime;
                prev = row;
            }
        }
        if (hour != 24)
            throw new Exception($"Bad schedule: {hour}h");
        Recs.Add(rec);
    }

    private void AddDayAssertPlan(Strategy plan, params uint[] objs)
    {
        if (Plan != plan)
        {
            Service.Log.Warning($"I fucked up: expected plan {plan}, have {Plan} - for {string.Join(", ", Favors.Select(f => f.Item.Value.Name))}");
            Plan = plan;
        }
        AddDay(objs);
    }
}
