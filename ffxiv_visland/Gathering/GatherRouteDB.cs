using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Numerics;
using visland.Helpers;

namespace visland.Gathering;

public class GatherRouteDB : Configuration.Node
{
    public enum Movement
    {
        Normal = 0,
        MountFly = 1,
        MountNoFly = 2,
    }

    public class Waypoint
    {
        public Vector3 Position;
        public float Radius;
        public Movement Movement;
        public uint InteractWithOID = 0;
        public string InteractWithName = "";
    }

    public class Route
    {
        public string Name = "";
        public List<Waypoint> Waypoints = new();
    }

    public List<Route> Routes = new();

    public override void Deserialize(JObject j, JsonSerializer ser)
    {
        Routes.Clear();
        if (j["Routes"] is JArray ja)
        {
            foreach (var jr in ja)
            {
                var jn = jr["Name"]?.Value<string>();
                var jw = jr["Waypoints"] as JArray;
                if (jn != null && jw != null)
                    Routes.Add(new Route() { Name = jn, Waypoints = LoadFromJSONWaypoints(jw) });
            }
        }
    }

    public override JObject Serialize(JsonSerializer ser)
    {
        JArray res = new();
        foreach (var r in Routes)
        {
            res.Add(new JObject()
            {
                { "Name", r.Name },
                { "Waypoints", SaveToJSONWaypoints(r.Waypoints) }
            });
        }
        return new JObject() { { "Routes", res } };
    }

    public static JArray SaveToJSONWaypoints(List<Waypoint> waypoints)
    {
        JArray jw = new();
        foreach (var wp in waypoints)
            jw.Add(new JArray() { wp.Position.X, wp.Position.Y, wp.Position.Z, wp.Radius, wp.InteractWithName, wp.Movement, wp.InteractWithOID });
        return jw;
    }

    public static List<Waypoint> LoadFromJSONWaypoints(JArray j)
    {
        List<Waypoint> res = new();
        foreach (var jwe in j)
        {
            var jwea = jwe as JArray;
            if (jwea == null || jwea.Count < 5)
                continue;
            var movement = jwea.Count <= 5 ? Movement.Normal
                : jwea[5].Type == JTokenType.Boolean ? jwea[5].Value<bool>() ? Movement.MountFly : Movement.Normal
                : (Movement)jwea[5].Value<int>();
            res.Add(new()
            {
                Position = new(jwea[0].Value<float>(), jwea[1].Value<float>(), jwea[2].Value<float>()),
                Radius = jwea[3].Value<float>(),
                Movement = movement,
                InteractWithOID = jwea.Count > 6 ? jwea[6].Value<uint>() : 2012985,
                InteractWithName = jwea[4].Value<string>() ?? "",
            });
        }
        return res;
    }
}
