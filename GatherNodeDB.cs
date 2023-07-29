using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace visland;

// TODO: figure out how to read all that out of game data...
// TODO: do I really need this thing at all?..
public class GatherNodeDB
{
    public List<List<Vector3>> KnownNodes = new();
    public Dictionary<string, int> NameLookup = new();
    public List<int> ExpectedCounts = new();

    public const int GatherNodeDataId = 2012985;

    public GatherNodeDB()
    {
        var sheetObj = Service.LuminaGameData.GetExcelSheet<MJIGatheringObject>()!;
        var sheetInst = Service.LuminaGameData.GetExcelSheet<MJIGathering>()!;
        for (int i = 0; i < sheetObj.RowCount; ++i)
        {
            KnownNodes.Add(new());
            ExpectedCounts.Add(sheetInst.Count(r => r.GatheringObject.Row == (uint)i));

            var nameRow = sheetObj.GetRow((uint)i)?.Name.Value;
            if (nameRow != null)
                NameLookup[nameRow.Singular.ToString()] = i;
        }
    }

    public void Clear()
    {
        foreach (var n in KnownNodes)
            n.Clear();
    }

    public void UpdateFromObjects()
    {
        // note: we only care about subset of objects, but whatever...
        foreach (var o in Service.ObjectTable.Where(o => o.DataId == GatherNodeDataId))
        {
            if (NameLookup.TryGetValue(o.Name.ToString().ToLower(), out var index))
            {
                AddNode(index, o.Position);
            }
        }
    }

    public void Draw(UITree tree)
    {
        foreach (var (name, index) in NameLookup)
        {
            var nodes = KnownNodes[index];
            foreach (var nt in tree.Node($"{name} ({nodes.Count}/{ExpectedCounts[index]} objects)###{name}", nodes.Count == 0, nodes.Count == ExpectedCounts[index] ? 0xff00ff00 : 0xff0000ff))
            {
                tree.LeafNodes(nodes, p => $"[{p.X:f3}, {p.Y:f3}, {p.Z:f3}]");
            }
        }
    }

    public void LoadFromJSON(JObject j, JsonSerializer ser)
    {
        var sheet = Service.LuminaGameData.GetExcelSheet<MJIGatheringObject>()!;
        for (int i = 1; i < sheet.RowCount; ++i)
        {
            var en = sheet.GetRow((uint)i)?.Name.Value;
            if (en == null)
                continue;

            var jn = j[en.Singular.ToString()] as JArray;
            if (jn == null)
                continue;

            foreach (var je in jn)
            {
                var jea = je as JArray;
                if (jea == null || jea.Count != 3)
                    continue;

                Vector3 pos = new(jea[0].Value<float>(), jea[1].Value<float>(), jea[2].Value<float>());
                AddNode(i, pos);
            }
        }
    }

    public JObject SaveToJSON(JsonSerializer ser)
    {
        JObject res = new();
        var sheet = Service.LuminaGameData.GetExcelSheet<MJIGatheringObject>()!;
        for (int i = 1; i < sheet.RowCount; ++i)
        {
            var en = sheet.GetRow((uint)i)?.Name.Value;
            if (en == null)
                continue;

            var nodes = KnownNodes[i];
            if (nodes.Count == 0)
                continue;

            JArray jn = new();
            foreach (var n in nodes)
                jn.Add(new JArray() { n.X, n.Y, n.Z });
            res.Add(en.Singular.ToString(), jn);
        }
        return res;
    }

    private void AddNode(int type, Vector3 pos)
    {
        var nodes = KnownNodes[type];
        if (!nodes.Any(n => (n - pos).LengthSquared() < 1))
            nodes.Add(pos);
    }
}
