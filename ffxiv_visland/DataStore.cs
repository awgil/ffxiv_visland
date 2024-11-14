using ECommons;
using Lumina.Excel.Sheets;
using System;
using System.Linq;

namespace visland;
public class DataStore
{
    public DataStore()
    {
        rawCordialsData = GenericHelpers.GetSheet<Item>()
                .Where(row => Enum.GetValues<CordialIDs>().Any(num => (uint)num == row.RowId))
                .Select(row => (
                    Name: row.Name.ToString(),
                    Id: row.RowId,
                    CanBeHQ: row.CanBeHq,
                    NQGP: row.ItemAction.Value!.Data[0],
                    HQGP: row.ItemAction.Value.DataHQ[0]
                )).ToArray();

        Cordials = [.. rawCordialsData
            .SelectMany((cordial, index) => cordial.CanBeHQ
                ?
                [
                        (cordial.Name, Id: cordial.Id + 1_000_000, GP: cordial.HQGP),
                        (cordial.Name, cordial.Id, GP: cordial.NQGP)
                ]
                : new[]
                {
                        (cordial.Name, cordial.Id, GP: cordial.NQGP)
                }).OrderByDescending(cordial => cordial.GP)];
    }

    private enum CordialIDs : uint
    {
        Hi = 12669,
        Regular = 6141,
        Watered = 16911,
    }

    private readonly (string Name, uint Id, bool CanBeHQ, ushort NQGP, ushort HQGP)[] rawCordialsData;
    public static (string Name, uint Id, ushort GP)[] Cordials = null!;
}
