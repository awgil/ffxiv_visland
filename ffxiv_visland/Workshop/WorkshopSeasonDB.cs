using Lumina.Excel;
using Lumina.Excel.Sheets;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace visland.Workshop;

public class WorkshopSeasonDB {
    public int RangeStart { get; private set; }
    public int RangeEnd { get; private set; }
    public int CycleLength { get; private set; }
    public int AnchorSeason { get; private set; }
    public DateTime AnchorStart { get; private set; }

    private readonly Dictionary<int, SeasonRec> _seasons = [];
    private readonly Dictionary<string, uint> _nameToId = new(StringComparer.OrdinalIgnoreCase);
    private readonly ExcelSheet<MJICraftworksObject> _craftSheet;

    public WorkshopSeasonDB() {
        _craftSheet = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>()!;
        foreach (var row in _craftSheet) {
            var item = Service.LuminaRow<Item>(row.Item.RowId);
            if (item == null)
                continue;
            // Prefer English bot-style names; fall back to the client's language sheet.
            var eng = Service.LuminaGameData.GetExcelSheet<Item>(Lumina.Data.Language.English)?.GetRowOrDefault(row.Item.RowId);
            var name = eng != null
                ? WorkshopOCImport.OfficialNameToBotName(eng.Value.Name.ExtractText())
                : WorkshopOCImport.OfficialNameToBotName(item.Value.Name.ExtractText());
            if (!string.IsNullOrEmpty(name))
                _nameToId.TryAdd(name, row.RowId);
        }

        LoadEmbedded();
    }

    public int SeasonForWeek(DateTime weekStart, bool nextWeek = false) {
        var weeksFromAnchor = (int)Math.Floor((weekStart.Date - AnchorStart.Date).TotalDays / 7.0);
        if (nextWeek)
            weeksFromAnchor++;
        var offset = ((AnchorSeason - RangeStart + weeksFromAnchor) % CycleLength + CycleLength) % CycleLength;
        return RangeStart + offset;
    }

    public int CurrentSeason(bool nextWeek = false) => SeasonForWeek(WorkshopUtils.CurrentWeek().startTime, nextWeek);

    public bool TryGet(int season, out SeasonRec rec) => _seasons.TryGetValue(season, out rec!);

    public WorkshopSolver.Recs BuildRecs(int season) {
        if (!_seasons.TryGetValue(season, out var seasonRec))
            throw new Exception($"No archived recommendations for season {season} (have {RangeStart}-{RangeEnd})");

        var result = new WorkshopSolver.Recs();
        foreach (var (cycle, day) in seasonRec.Cycles.OrderBy(kv => kv.Key)) {
            if (day.Rest)
                continue;

            var dayRec = new WorkshopSolver.DayRec();
            dayRec.Workshops.Add(BuildWorkshop(day.Main ?? []));
            if (day.Ws4 is { Count: > 0 })
                dayRec.Workshops.Add(BuildWorkshop(day.Ws4));
            result.Add(cycle, dayRec);
        }
        return result;
    }

    /// <summary>OC-posted rest days in 2-7, plus implied C1.</summary>
    public HashSet<int> RestCycles(int season) {
        var rests = new HashSet<int> { 1 };
        if (_seasons.TryGetValue(season, out var seasonRec)) {
            foreach (var (cycle, day) in seasonRec.Cycles) {
                if (day.Rest)
                    rests.Add(cycle);
            }
        }
        return rests;
    }

    private WorkshopSolver.WorkshopRec BuildWorkshop(List<uint> crafts) {
        var rec = new WorkshopSolver.WorkshopRec();
        var hour = 0;
        foreach (var id in crafts) {
            if (!_craftSheet.TryGetRow(id, out var row))
                throw new Exception($"Unknown craftworks id {id}");
            rec.Add(hour, id);
            hour += row.CraftingTime;
        }
        if (hour > 24)
            throw new Exception($"Workshop schedule exceeds 24h ({hour})");
        return rec;
    }

    private void LoadEmbedded() {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("visland.Workshop.Data.workshop-seasons.json")
            ?? throw new Exception("Embedded workshop-seasons.json not found");
        using var reader = new StreamReader(stream);
        var root = JObject.Parse(reader.ReadToEnd());

        var range = root["range"]!.ToObject<int[]>()!;
        RangeStart = range[0];
        RangeEnd = range[1];
        CycleLength = root["cycleLength"]?.Value<int>() ?? (RangeEnd - RangeStart + 1);
        AnchorSeason = root["anchorSeason"]?.Value<int>() ?? RangeEnd;
        AnchorStart = DateTime.Parse(root["anchorStart"]?.Value<string>() ?? "2026-07-07").Date;

        foreach (var prop in ((JObject)root["seasons"]!).Properties()) {
            var season = int.Parse(prop.Name);
            var cycles = new Dictionary<int, CycleRec>();
            foreach (var cycleProp in ((JObject)prop.Value["cycles"]!).Properties()) {
                var cycle = int.Parse(cycleProp.Name);
                var obj = (JObject)cycleProp.Value;
                if (obj["rest"]?.Value<bool>() == true) {
                    cycles[cycle] = new CycleRec { Rest = true };
                    continue;
                }

                cycles[cycle] = new CycleRec {
                    Main = ResolveCrafts(obj["main"] as JArray, season, cycle, "main"),
                    Ws4 = ResolveCrafts(obj["ws4"] as JArray, season, cycle, "ws4"),
                };
            }
            _seasons[season] = new SeasonRec {
                Season = season,
                Date = prop.Value["date"]?.Value<string>() ?? "",
                Cycles = cycles,
            };
        }

        Service.Log.Info($"Workshop season DB loaded: {_seasons.Count} seasons ({RangeStart}-{RangeEnd}), anchor S{AnchorSeason} @ {AnchorStart:yyyy-MM-dd}");
    }

    private List<uint>? ResolveCrafts(JArray? names, int season, int cycle, string workshop) {
        if (names == null || names.Count == 0)
            return null;
        var ids = new List<uint>(names.Count);
        foreach (var token in names) {
            var name = token.Value<string>() ?? "";
            if (!_nameToId.TryGetValue(name, out var id)) {
                var match = _nameToId.Keys.Where(n => n.Length > 0 && name.Contains(n, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(n => n.Length).FirstOrDefault();
                if (match == null)
                    throw new Exception($"Season {season} C{cycle} {workshop}: unknown craft '{name}'");
                id = _nameToId[match];
                Service.Log.Warning($"Season {season} C{cycle} {workshop}: resolved '{name}' → '{match}'");
            }
            ids.Add(id);
        }
        return ids;
    }

    public class SeasonRec {
        public int Season;
        public string Date = "";
        public Dictionary<int, CycleRec> Cycles = [];
    }

    public class CycleRec {
        public bool Rest;
        public List<uint>? Main;
        public List<uint>? Ws4;
    }
}
