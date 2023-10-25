using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace visland;

public class Config
{
    private const int _version = 1;

    public bool Autosave = true;
    public GatherRouteDB RouteDB = new();

    public bool OpenNextDay = false;
    public bool AutoImport = false;

    public bool AutoCollectGranary = false;
    public bool AutoMaxGranary = false;

    public bool AutoSell = false;
    public int AutoSellAmount = 900;

    public bool AutoCollectFarm = false;
    public bool AutoCollectPasture = false;

    public bool DisableOnErrors = false;

    public void LoadFromFile(FileInfo file)
    {
        Autosave = true;
        RouteDB = new();

        OpenNextDay = false;
        AutoImport = false;

        AutoCollectGranary = false;
        AutoMaxGranary = false;

        AutoSell = false;
        AutoSellAmount = 900;

        AutoCollectFarm = false;
        AutoCollectPasture = false;

        DisableOnErrors = false;

        try
        {
            var contents = File.ReadAllText(file.FullName);
            var json = JObject.Parse(contents);
            var version = (int?)json["Version"] ?? 0;
            var payload = json["Payload"] as JObject;
            if (payload != null)
            {
                payload = ConvertConfig(payload, version);
                var ser = BuildSerializer();
                if (payload["RouteDB"] as JArray is var jr && jr != null)
                    RouteDB.LoadFromJSON(jr, ser);
                if (payload["Autosave"] is var ja && ja != null)
                    Autosave = ja.Value<bool>();
                if (payload["OpenNextDay"] is var jb && jb != null)
                    OpenNextDay = jb.Value<bool>();
                if (payload["AutoImport"] is var jc && jc != null)
                    AutoImport = jc.Value<bool>();
                if (payload["AutoCollectGranary"] is var jd && jd != null)
                    AutoCollectGranary = jd.Value<bool>();
                if (payload["AutoMaxGranary"] is var je && je != null)
                    AutoMaxGranary = je.Value<bool>();
                if (payload["AutoSell"] is var jf && jf != null)
                    AutoSell = jf.Value<bool>();
                if (payload["AutoSellAmount"] is var jg && jg != null)
                    AutoSellAmount = jg.Value<int>();
                if (payload["AutoCollectFarm"] is var jh && jh != null)
                    AutoCollectFarm = jh.Value<bool>();
                if (payload["AutoCollectPasture"] is var ji && ji != null)
                    AutoCollectPasture = ji.Value<bool>();
                if (payload["DisableOnErrors"] is var jj && jj != null)
                    DisableOnErrors = jj.Value<bool>();
            }
        }
        catch (Exception e)
        {
            Service.Log.Error($"Failed to load config from {file.FullName}: {e}");
        }
    }

    public void SaveToFile(FileInfo file)
    {
        try
        {
            var ser = BuildSerializer();
            JObject payload = new()
            {
                { "Autosave", Autosave },
                { "RouteDB", RouteDB.SaveToJSON(ser) },
                { "OpenNextDay", OpenNextDay },
                { "AutoImport", AutoImport },
                { "AutoCollectGranary", AutoCollectGranary },
                { "AutoMaxGranary", AutoMaxGranary },
                { "AutoSell", AutoSell },
                { "AutoSellAmount", AutoSellAmount },
                { "AutoCollectFarm", AutoCollectFarm },
                { "AutoCollectPasture", AutoCollectPasture },
                { "DisableOnErrors", DisableOnErrors },
            };
            JObject jContents = new()
            {
                { "Version", _version },
                { "Payload", payload }
            };
            File.WriteAllText(file.FullName, jContents.ToString());
        }
        catch (Exception e)
        {
            Service.Log.Error($"Failed to save config to {file.FullName}: {e}");
        }
    }

    public static JsonSerializer BuildSerializer()
    {
        var res = new JsonSerializer();
        res.Converters.Add(new StringEnumConverter());
        return res;
    }

    private static JObject ConvertConfig(JObject payload, int version)
    {
        return payload;
    }
}
