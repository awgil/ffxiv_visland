using Lumina.Excel.Sheets;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace visland.Helpers;

public static class Coords
{
    public static Vector3 PixelCoordsToWorldCoords(int x, int z, uint mapId)
    {
        var map = Service.LuminaRow<Lumina.Excel.Sheets.Map>(mapId);
        var scale = (map?.SizeFactor ?? 100) * 0.01f;
        var wx = PixelCoordToWorldCoord(x, scale, map?.OffsetX ?? 0);
        var wz = PixelCoordToWorldCoord(z, scale, map?.OffsetY ?? 0);
        return new(wx, 0, wz);
    }

    // see: https://github.com/xivapi/ffxiv-datamining/blob/master/docs/MapCoordinates.md
    // see: dalamud MapLinkPayload class
    public static float PixelCoordToWorldCoord(float coord, float scale, short offset)
    {
        // +1 - networkAdjustment == 0
        // (coord / scale * 2) * (scale / 100) = coord / 50
        // * 2048 / 41 / 50 = 0.999024
        const float factor = 2048.0f / (50 * 41);
        return (coord * factor - 1024f) / scale - offset * 0.001f;
    }

    public static uint FindClosestAetheryte(uint territoryTypeId, Vector3 worldPos)
    {
        if (territoryTypeId == 886)
        {
            // firmament special case - just return ishgard main aetheryte
            // firmament aetherytes are special (see 
            return 70;
        }
        List<Aetheryte> aetherytes = [.. GetSheet<Aetheryte>().Where(a => a.Territory.RowId == territoryTypeId)];
        return aetherytes.Count > 0 ? aetherytes.MinBy(a => (worldPos - AetherytePosition(a)).LengthSquared()).RowId : 0;
    }

    public static Vector3 AetherytePosition(Aetheryte a)
    {
        // stolen from HTA, uses pixel coordinates
        var level = a.Level[0].ValueNullable;
        if (level != null)
            return new(level.Value.X, level.Value.Y, level.Value.Z);
        var marker = GetSubrowSheet<MapMarker>()!.Flatten().FirstOrNull(m => m.DataType == 3 && m.DataKey.RowId == a.RowId)
            ?? GetSubrowSheet<MapMarker>()!.Flatten().First(m => m.DataType == 4 && m.DataKey.RowId == a.AethernetName.RowId);
        return PixelCoordsToWorldCoords(marker.X, marker.Y, a.Territory.Value.Map.RowId);
    }

    // if aetheryte is 'primary' (i.e. can be teleported to), return it; otherwise (i.e. aethernet shard) find and return primary aetheryte from same group
    public static uint FindPrimaryAetheryte(uint aetheryteId)
    {
        if (aetheryteId == 0)
            return 0;
        var row = Service.LuminaRow<Aetheryte>(aetheryteId)!.Value;
        if (row.IsAetheryte)
            return aetheryteId;
        var primary = Service.LuminaSheet<Aetheryte>()!.FirstOrNull(a => a.AethernetGroup == row.AethernetGroup);
        return primary?.RowId ?? 0;
    }
}