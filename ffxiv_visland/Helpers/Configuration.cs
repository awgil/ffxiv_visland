using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace visland.Helpers;

public class Configuration
{
    // base class for configuration nodes
    public abstract class Node
    {
        public event EventHandler? Modified;

        // notify that configuration node was modified; should be called by derived classes whenever they make any modifications
        // implementation dispatches modification event
        // root subscribes to modification event to save updated configuration
        public void NotifyModified()
        {
            Modified?.Invoke(this, EventArgs.Empty);
        }

        // deserialize fields from json; default implementation should work fine for most cases
        public virtual void Deserialize(JObject j, JsonSerializer ser)
        {
            var type = GetType();
            foreach (var (f, data) in j)
            {
                var field = type.GetField(f);
                if (field != null)
                {
                    var value = data?.ToObject(field.FieldType, ser);
                    if (value != null)
                    {
                        field.SetValue(this, value);
                    }
                }
            }
        }

        // serialize node to json; default implementation should work fine for most cases
        public virtual JObject Serialize(JsonSerializer ser)
        {
            return JObject.FromObject(this, ser);
        }
    }

    private const int _version = 3;

    public event EventHandler? Modified;
    private Dictionary<Type, Node> _nodes = [];

    public IEnumerable<Node> Nodes => _nodes.Values;

    public void Initialize()
    {
        foreach (var t in Utils.GetDerivedTypes<Node>(Assembly.GetExecutingAssembly()).Where(t => !t.IsAbstract))
        {
            if (Activator.CreateInstance(t) is not Node inst)
            {
                Service.Log.Error($"[Config] Failed to create an instance of {t}");
                continue;
            }
            inst.Modified += (sender, args) => Modified?.Invoke(sender, args);
            _nodes[t] = inst;
        }
    }

    public T Get<T>() where T : Node => (T)_nodes[typeof(T)];
    public T Get<T>(Type derived) where T : Node => (T)_nodes[derived];

    public void LoadFromFile(FileInfo file)
    {
        try
        {
            var contents = File.ReadAllText(file.FullName);
            var json = JObject.Parse(contents);
            var version = (int?)json["Version"] ?? 0;
            if (json["Payload"] is JObject payload)
            {
                payload = ConvertConfig(payload, version);
                var ser = BuildSerializer();
                foreach (var (t, j) in payload)
                {
                    var type = Type.GetType(t);
                    var node = type != null ? _nodes.GetValueOrDefault(type) : null;
                    if (node != null && j is JObject jObj)
                    {
                        node.Deserialize(jObj, ser);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Service.Log.Error($"[Config] Failed to load config from {file.FullName}: {e}");
        }
    }

    public void SaveToFile(FileInfo file)
    {
        try
        {
            var ser = BuildSerializer();
            JObject payload = [];
            foreach (var (t, n) in _nodes)
            {
                var jNode = n.Serialize(ser);
                if (jNode.Count > 0)
                {
                    payload.Add(t.FullName!, jNode);
                }
            }
            JObject jContents = new()
            {
                { "Version", _version },
                { "Payload", payload }
            };
            File.WriteAllText(file.FullName, jContents.ToString());
        }
        catch (Exception e)
        {
            Service.Log.Error($"[Config] Failed to save config to {file.FullName}: {e}");
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
        // v2: backported vbm config framework
        if (version < 2)
        {
            var routes = payload["RouteDB"];
            payload = new() { { "visland.Gathering.GatherRouteDB", new JObject() { { "Routes", routes } } } };
        }
        // v3: turned waypoints into an array of jobjects
        if (version < 3)
        {
            //var routes = payload["RouteDB"];
            if (payload["visland.Gathering.GatherRouteDB"] is JObject gatherRouteDB)
            {
                if (gatherRouteDB["Routes"] is JArray routes)
                {
                    JArray newRoutes = [];
                    foreach (var route in routes)
                    {
                        if (route is JObject routeObj && routeObj["Waypoints"] is JArray waypoints)
                        {
                            var newWaypoints = new JArray();
                            foreach (var waypoint in waypoints)
                            {
                                if (waypoint is JArray oldWaypoint)
                                {
                                    var newWaypoint = new JObject
                                    {
                                        { "X", oldWaypoint.Count > 0 ? oldWaypoint[0] : 0 },
                                        { "Y", oldWaypoint.Count > 1 ? oldWaypoint[1] : 0 },
                                        { "Z", oldWaypoint.Count > 2 ? oldWaypoint[2] : 0 },
                                        { "Radius", oldWaypoint.Count > 3 ? oldWaypoint[3] : 0 },
                                        { "InteractWithName", oldWaypoint.Count > 4 ? oldWaypoint[4] : "" },
                                        { "Movement", oldWaypoint.Count > 5 ? oldWaypoint[5] : "" },
                                        { "InteractWithOID", oldWaypoint.Count > 6 ? oldWaypoint[6] : 0 },
                                        { "showInteractions", oldWaypoint.Count > 7 ? oldWaypoint[7] : false },
                                        { "Interaction", oldWaypoint.Count > 8 ? oldWaypoint[8] : "" },
                                        { "EmoteID", oldWaypoint.Count > 9 ? oldWaypoint[9] : 0 },
                                        { "ActionID", oldWaypoint.Count > 10 ? oldWaypoint[10] : 0 },
                                        { "ItemID", oldWaypoint.Count > 11 ? oldWaypoint[11] : 0 },
                                        { "showWaits", oldWaypoint.Count > 12 ? oldWaypoint[12] : false },
                                        { "WaitTimeMs", oldWaypoint.Count > 13 ? oldWaypoint[13] : 0 },
                                        { "WaitForCondition", oldWaypoint.Count > 14 ? oldWaypoint[14] : "" },
                                        { "Pathfind", oldWaypoint.Count > 15 ? oldWaypoint[15] : false },
                                        { "MobID", oldWaypoint.Count > 16 ? oldWaypoint[16] : 0 },
                                        { "QuestID", oldWaypoint.Count > 17 ? oldWaypoint[17] : 0 },
                                        { "RouteName", oldWaypoint.Count > 18 ? oldWaypoint[18] : "" },
                                        { "ChatCommand", oldWaypoint.Count > 19 ? oldWaypoint[19] : "" },
                                    };
                                    newWaypoints.Add(newWaypoint);
                                }
                            }
                            routeObj["Waypoints"] = newWaypoints;
                            newRoutes.Add(routeObj);
                        }
                    }
                    gatherRouteDB["Routes"] = newRoutes;
                }
            }
        }
        return payload;
    }
}
