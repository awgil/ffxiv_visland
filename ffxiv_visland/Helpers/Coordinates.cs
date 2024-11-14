using ECommons;
using ECommons.DalamudServices;
using ECommons.Logging;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using System.Linq;
using System.Numerics;

namespace visland.Helpers;
internal class Coordinates
{
    public static ExcelSheet<Aetheryte> Aetherytes = GenericHelpers.GetSheet<Aetheryte>()!;

    public static float ConvertMapMarkerToMapCoordinate(float pos, float scale)
    {
        var num = scale / 100f;
        var rawPosition = (int)((float)(pos - 1024.0) / num * 1000f);
        return ConvertRawPositionToMapCoordinate(rawPosition, scale);
    }

    public static float ConvertRawPositionToMapCoordinate(float pos, float scale)
    {
        var num = scale / 100f;
        return (float)((((pos / 1000f * num) + 1024.0) / 2048.0 * 41.0 / num) + 1.0);
    }

    public static uint GetNearestAetheryte(int zoneID, Vector3 pos)
    {
        var aetheryte = 0u;
        double distance = 0;
        foreach (var data in Aetherytes)
        {
            if (!data.IsAetheryte) continue;
            if (data.Territory.Value.RowId == zoneID)
            {
                var mapMarker = GenericHelpers.FindRow<MapMarker>(m => m!.DataType == 3 && m.DataKey.RowId == data.RowId);
                if (mapMarker == null)
                {
                    PluginLog.Error($"Cannot find aetherytes position for {zoneID}#{data.PlaceName.Value.Name}");
                    continue;
                }
                var AethersX = ConvertMapMarkerToMapCoordinate(mapMarker.Value.X, 100);
                var AethersY = ConvertMapMarkerToMapCoordinate(mapMarker.Value.Y, 100);
                var temp_distance = Math.Pow(AethersX - pos.X, 2) + Math.Pow(AethersY - pos.Z, 2);
                if (aetheryte == default || temp_distance < distance)
                {
                    distance = temp_distance;
                    aetheryte = data.RowId;
                }
            }
        }

        return aetheryte;
    }

    public static bool HasAetheryteInZone(uint TerritoryType) => Svc.AetheryteList.Any(a => a.TerritoryId == TerritoryType);
}
