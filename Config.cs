using Dalamud.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace visland;

public class Config
{
    private const int _version = 1;

    public GatherNodeDB GatherNodeDB = new();
    public GatherRouteDB RouteDB = new();

    public void LoadFromFile(FileInfo file)
    {
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
                if (payload["GatherNodeDB"] as JObject is var jg && jg != null)
                    GatherNodeDB.LoadFromJSON(jg, ser);
                if (payload["RouteDB"] as JArray is var jr && jr != null)
                    RouteDB.LoadFromJSON(jr, ser);
            }
        }
        catch (Exception e)
        {
            PluginLog.Error($"Failed to load config from {file.FullName}: {e}");
        }
    }

    public void SaveToFile(FileInfo file)
    {
        try
        {
            var ser = BuildSerializer();
            JObject payload = new()
            {
                { "GatherNodeDB", GatherNodeDB.SaveToJSON(ser) },
                { "RouteDB", RouteDB.SaveToJSON(ser) },
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
            PluginLog.Error($"Failed to save config to {file.FullName}: {e}");
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
