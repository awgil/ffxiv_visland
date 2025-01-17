using Dalamud.Game.ClientState.Conditions;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ImGuiNET;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using visland.Helpers;
using visland.IPC;
using static visland.Gathering.GatherRouteDB;

namespace visland.Gathering;

public class GatherRouteDB : Configuration.Node
{
    public enum Movement
    {
        Normal = 0,
        MountFly = 1,
        MountNoFly = 2,
    }

    public enum InteractionType
    {
        None = 0,
        Standard = 1,
        //Emote = 2,
        //UseItem = 3,
        //UseAction = 4,
        //QuestTalk = 5,
        //Grind = 6,
        //PickupQuest = 7,
        //TurninQuest = 8,
        StartRoute = 9,
        //EquipRecommendedGear = 10,
        //ChatCommand = 11,
        NodeScan = 12,
    }

    public enum GrindStopConditions
    {
        None = 0,
        Kills = 1,
        QuestSequence = 2,
        QuestComplete = 3,
    }

    public enum NodeType : byte
    {
        Unknown = 0xFF,
        Regular = 0,
        Unspoiled = 1,
        Ephemeral = 2,
        Legendary = 3,
    }

    public class Node
    {
        public NodeType Type;
        public int GpThreshold;
    }

    public class Waypoint
    {
        public Vector3 Position;
        public int ZoneID;
        public float Radius;
        public Movement Movement;
        public bool Pathfind = true;
        public uint InteractWithOID = 0;
        public string InteractWithName = "";
        public Vector3 InteractWithPosition;

        public bool showInteractions;
        public InteractionType Interaction = InteractionType.Standard;
        public int EmoteID;
        public int ItemID;
        public int ActionID;
        public int QuestID;
        public int QuestSeq;
        public int MobID;
        public GrindStopConditions StopCondition;
        public int KillCount;
        public string RouteName = "";
        public string ChatCommand = "";

        public bool showWaits;
        public ConditionFlag WaitForCondition;
        public int WaitTimeMs;
        public Vector2 WaitTimeET;

        public uint GatheringType => IsNode ? GenericHelpers.GetRow<GatheringPoint>(InteractWithOID)!.Value.GatheringPointBase.Value.GatheringType.RowId : 99;
        public bool IsNode => GenericHelpers.GetSheet<GatheringPoint>().HasRow(InteractWithOID);
        public Job NodeJob
        {
            get
            {
                if (!IsNode) return Job.ADV;
                return GatheringType switch
                {
                    0 or 1 => Job.MIN,
                    2 or 3 => Job.BTN,
                    4 or 5 => Job.FSH,
                    _ => Job.ADV
                };
            }
        }
        public bool IsPhantom;
        public List<Vector3>? Path;
    }

    public class Route
    {
        public string Name = "";
        public string Group = "";
        public int Food = 0;
        public int Manual = 0;
        public int TargetGatherItem = 0;
        public List<Waypoint> Waypoints = [];
    }

    public List<Route> Routes = [];
    public float DefaultWaypointRadius = 3;
    public float DefaultInteractionRadius = 2;
    public bool GatherModeOnStart = true;
    public bool DisableOnErrors = false;

    public bool ExtractMateria = true;
    public bool RepairGear = true;
    public float RepairPercent = 20;
    public bool PurifyCollectables = false;

    public int GlobalFood = 0;
    public int GlobalManual = 0;

    public bool WasFlyingInManual = false;
    public bool AutoRetainerIntegration = false;

    public bool TeleportBetweenZones = true;
    public int LandDistance = 10;
    public int PathFindCancellationTime = 5;
    public bool AutoGather = false;

    public override void Deserialize(JObject j, JsonSerializer ser)
    {
        Routes.Clear();
        if (j["Routes"] is JArray ja)
        {
            foreach (var jr in ja)
            {
                var jn = jr["Name"]?.Value<string>();
                var jg = jr["Group"]?.Value<string>();
                var jf = jr["Food"]?.Value<int>();
                var jm = jr["Manual"]?.Value<int>();
                var ji = jr["TargetGatherItem"]?.Value<int>();
                if (jn != null && jr["Waypoints"] is JArray jw)
                {
                    if (jg != null)
                        Routes.Add(new Route() { Name = jn, Group = jg, Food = jf ?? 0, Manual = jm ?? 0, TargetGatherItem = ji ?? 0, Waypoints = LoadFromJSONWaypoints(jw) });
                    else
                        Routes.Add(new Route() { Name = jn, Food = jf ?? 0, Manual = jm ?? 0, TargetGatherItem = ji ?? 0, Waypoints = LoadFromJSONWaypoints(jw) });
                }
            }
        }
        DisableOnErrors = (bool?)j["DisableOnErrors"] ?? false;
        GatherModeOnStart = (bool?)j["GatherModeOnStart"] ?? true;
        DefaultWaypointRadius = (float?)j["DefaultWaypointRadius"] ?? 3;
        DefaultInteractionRadius = (float?)j["DefaultInteractionRadius"] ?? 2;

        TeleportBetweenZones = (bool?)j["TeleportBetweenZones"] ?? true;
        AutoRetainerIntegration = (bool?)j["AutoRetainerIntegration"] ?? false;

        AutoGather = (bool?)j["AutoGather"] ?? false;
        LandDistance = (int?)j["LandDistance"] ?? 10;
        PathFindCancellationTime = (int?)j["PathFindCancellationTime"] ?? 5;

        ExtractMateria = (bool?)j["ExtractMateria"] ?? true;
        RepairGear = (bool?)j["RepairGear"] ?? true;
        RepairPercent = (float?)j["RepairPercent"] ?? 20;
        PurifyCollectables = (bool?)j["Desynth"] ?? false;

        GlobalFood = (int?)j["GlobalFood"] ?? 0;
        GlobalManual = (int?)j["GlobalManual"] ?? 0;
    }

    public override JObject Serialize(JsonSerializer ser)
    {
        JArray res = [];
        foreach (var r in Routes)
        {
            res.Add(new JObject()
            {
                { "Name", r.Name },
                { "Group", r.Group },
                { "Food", r.Food },
                { "Manual", r.Manual },
                { "TargetGatherItem", r.TargetGatherItem },
                { "Waypoints", SaveToJSONWaypoints(r.Waypoints) }
            });
        }
        return new JObject() {
            { "Routes", res },
            { "DisableOnErrors", DisableOnErrors },
            { "GatherModeOnStart", GatherModeOnStart },
            { "DefaultWaypointRadius", DefaultWaypointRadius },
            { "DefaultInteractionRadius", DefaultInteractionRadius },
            { "TeleportBetweenZones", TeleportBetweenZones },
            { "ExtractMateria", ExtractMateria },
            { "RepairGear", RepairGear },
            { "RepairPercent", RepairPercent },
            { "Desynth", PurifyCollectables },
            { "GlobalFood", GlobalFood },
            { "GlobalManual", GlobalManual },
            { "AutoRetainerIntegration", AutoRetainerIntegration },
            { "AutoGather", AutoGather },
            { "LandDistance", LandDistance },
            { "PathFindCancellationTime", PathFindCancellationTime },
        };
    }

    public static JArray SaveToJSONWaypoints(List<Waypoint> waypoints)
    {
        JArray jw = [];

        foreach (var wp in waypoints)
        {
            if (wp.IsPhantom) continue;
            var wpObj = new JObject
            {
                { "X", wp.Position.X },
                { "Y", wp.Position.Y },
                { "Z", wp.Position.Z },
                { "ZoneID", wp.ZoneID },
                { "Radius", wp.Radius },
                { "InteractWithName", wp.InteractWithName },
                { "Movement", wp.Movement.ToString() },
                { "InteractWithOID", wp.InteractWithOID },
                { "iX", wp.InteractWithPosition.X },
                { "iY", wp.InteractWithPosition.Y },
                { "iZ", wp.InteractWithPosition.Z },
                { "showInteractions", wp.showInteractions },
                { "Interaction", wp.Interaction.ToString() },
                //{ "EmoteID", wp.EmoteID },
                //{ "ActionID", wp.ActionID },
                //{ "ItemID", wp.ItemID },
                { "showWaits", wp.showWaits },
                { "WaitTimeMs", wp.WaitTimeMs },
                { "WaitForCondition", wp.WaitForCondition.ToString() },
                { "Pathfind", wp.Pathfind },
                //{ "MobID", wp.MobID },
                //{ "QuestID", wp.QuestID },
                { "RouteName", wp.RouteName },
                //{ "ChatCommand", wp.ChatCommand }
            };

            jw.Add(wpObj);
        }

        return jw;
    }

    public static List<Waypoint> LoadFromJSONWaypoints(JArray j)
    {
        List<Waypoint> res = [];

        try
        {
            foreach (var jwe in j)
            {
                if (jwe is not JObject jweObj)
                    continue;

                res.Add(new()
                {
                    Position = new Vector3(
                        jweObj["X"]?.Value<float>() ?? 0,
                        jweObj["Y"]?.Value<float>() ?? 0,
                        jweObj["Z"]?.Value<float>() ?? 0
                    ),
                    ZoneID = jweObj["ZoneID"]?.Value<int>() ?? 0,
                    Radius = jweObj["Radius"]?.Value<float>() ?? 0,
                    InteractWithName = jweObj["InteractWithName"]?.Value<string>() ?? "",
                    Movement = Enum.TryParse<Movement>(jweObj["Movement"]?.Value<string>(), out var movement) ? movement : Movement.Normal,
                    InteractWithOID = jweObj["InteractWithOID"]?.Value<uint>() ?? 0,
                    InteractWithPosition = new Vector3(
                        jweObj["iX"]?.Value<float>() ?? 0,
                        jweObj["iY"]?.Value<float>() ?? 0,
                        jweObj["iZ"]?.Value<float>() ?? 0
                    ),
                    showInteractions = jweObj["showInteractions"]?.Value<bool>() ?? false,
                    Interaction = Enum.TryParse<InteractionType>(jweObj["Interaction"]?.Value<string>(), out var interaction) ? interaction : InteractionType.Standard,
                    EmoteID = jweObj["EmoteID"]?.Value<int>() ?? 0,
                    ActionID = jweObj["ActionID"]?.Value<int>() ?? 0,
                    ItemID = jweObj["ItemID"]?.Value<int>() ?? 0,
                    showWaits = jweObj["showWaits"]?.Value<bool>() ?? false,
                    WaitTimeMs = jweObj["WaitTimeMs"]?.Value<int>() ?? 0,
                    WaitForCondition = Enum.TryParse<ConditionFlag>(jweObj["WaitForCondition"]?.Value<string>(), out var condition) ? condition : ConditionFlag.None,
                    Pathfind = jweObj["Pathfind"]?.Value<bool>() ?? false,
                    MobID = jweObj["MobID"]?.Value<int>() ?? 0,
                    QuestID = jweObj["QuestID"]?.Value<int>() ?? 0,
                    RouteName = jweObj["RouteName"]?.Value<string>() ?? "",
                    ChatCommand = jweObj["ChatCommand"]?.Value<string>() ?? ""
                });
            }
        }
        catch (Exception)
        {
            Svc.Log.Error($"Failed to load waypoints from JSON.");
        }

        return res;
    }

    public static List<string> GetGroups(GatherRouteDB gatherRouteDB, bool sort = false)
    {
        List<string> groups = ["Ungrouped"];
        for (var g = 0; g < gatherRouteDB.Routes.Count; g++)
        {
            var routeSource = gatherRouteDB.Routes;
            if (string.IsNullOrEmpty(routeSource[g].Group))
                routeSource[g].Group = "Ungrouped";
            if (!groups.Contains(routeSource[g].Group))
                groups.Add(routeSource[g].Group);
        }
        if (sort)
            groups = [.. groups.OrderBy(i => i == "Ungrouped").ThenBy(i => i)]; //Sort with None at the End

        return groups;
    }

    public static void TryImport(GatherRouteDB RouteDB)
    {
        try
        {
            var data = ImGui.GetClipboardText();
            var (IsBase64, Json) = Utils.FromCompressedBase64(data);
            Route? import = null;
            if (IsBase64)
                import = JsonConvert.DeserializeObject<Route>(Json);
            else if (Utils.IsJson(data))
                import = JsonConvert.DeserializeObject<Route>(data);
            if (import != null)
            {
                if (import.Waypoints.Any(x => (x.Pathfind || x.Interaction == InteractionType.NodeScan) && !NavmeshIPC.IsEnabled))
                    Svc.Chat.Print($"[{Plugin.Name}] Imported route uses pathfinding, but vnavmesh is not installed. It's located on the same repo as {Plugin.Name} ({Plugin.Repo}).");

                RouteDB.Routes.Add(new() { Name = import!.Name, Group = import.Group, Food = import.Food, Manual = import.Manual, TargetGatherItem = import.TargetGatherItem, Waypoints = import.Waypoints });
                RouteDB.NotifyModified();
            }
        }
        catch (JsonReaderException ex)
        {
            Service.ChatGui.PrintError($"Failed to import route: {ex.Message}");
            Service.Log.Error(ex, "Failed to import route");
        }
    }
}

public static class WaypointExtensions
{
    public static bool TryGetNextWaypoint(this Waypoint waypoint, Route route, bool loop, out Waypoint? nextWaypoint)
    {
        var index = route.Waypoints.IndexOf(waypoint);
        if (index >= 0 && index < route.Waypoints.Count - 1)
        {
            nextWaypoint = route.Waypoints[index + 1];
            return true;
        }
        else
        {
            if (loop)
            {
                nextWaypoint = route.Waypoints.First();
                return true;
            }
            nextWaypoint = null;
            return false;
        }
    }

    public static void AddWaypointsAfter(this Waypoint waypoint, Route route, List<Waypoint> waypoints)
    {
        var index = route.Waypoints.IndexOf(waypoint);
        route.Waypoints.InsertRange(index + 1, waypoints);
    }

    public static bool IsLast(this Waypoint waypoint, Route route) => waypoint.Equals(route.Waypoints.Last());
}