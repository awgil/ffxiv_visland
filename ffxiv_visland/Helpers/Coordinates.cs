using ECommons.DalamudServices;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace visland.Helpers;
internal class Coordinates
{
    public static Lumina.Excel.ExcelSheet<Aetheryte> Aetherytes = Svc.Data.GetExcelSheet<Aetheryte>(Svc.ClientState.ClientLanguage)!;
    public static Lumina.Excel.ExcelSheet<MapMarker> AetherytesMap = Svc.Data.GetExcelSheet<MapMarker>(Svc.ClientState.ClientLanguage)!;

    private static float ConvertMapMarkerToMapCoordinate(int pos, float scale)
    {
        var num = scale / 100f;
        var rawPosition = (int)((float)(pos - 1024.0) / num * 1000f);
        return ConvertRawPositionToMapCoordinate(rawPosition, scale);
    }

    private static float ConvertRawPositionToMapCoordinate(int pos, float scale)
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
            if (data.Territory.Value == null) continue;
            if (data.PlaceName.Value == null) continue;
            if (data.Territory.Value.RowId == zoneID)
            {
                var mapMarker = AetherytesMap.FirstOrDefault(m => m.DataType == 3 && m.DataKey == data.RowId);
                if (mapMarker == null)
                {
                    Svc.Log.Error($"Cannot find aetherytes position for {zoneID}#{data.PlaceName.Value.Name}");
                    continue;
                }
                var AethersX = ConvertMapMarkerToMapCoordinate(mapMarker.X, 100);
                var AethersY = ConvertMapMarkerToMapCoordinate(mapMarker.Y, 100);
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
