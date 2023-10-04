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

    public void LoadFromFile(FileInfo file)
    {
        Autosave = true;
        RouteDB = new();

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
