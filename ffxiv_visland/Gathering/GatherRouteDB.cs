using Dalamud.Game.ClientState.Conditions;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using visland.Helpers;

namespace visland.Gathering;

public class GatherRouteDB : Configuration.Node {
    // ClassJob row IDs used for gathering job swaps.
    public const uint ClassJobMiner = 16;
    public const uint ClassJobBotanist = 17;
    public const uint ClassJobFisher = 18;

    public enum Movement {
        Normal = 0,
        MountFly = 1,
        MountNoFly = 2,
    }

    public enum InteractionType {
        None = 0,
        Standard = 1,
        StartRoute = 9,
        NodeScan = 12,
    }

    public class Waypoint {
        public Vector3 Position;
        public uint ZoneID;
        public float Radius;
        public Movement Movement;
        public bool Pathfind = true;
        public uint InteractWithOID;
        public string InteractWithName = "";
        public Vector3 InteractWithPosition;

        public bool showInteractions;
        public InteractionType Interaction = InteractionType.Standard;
        public string RouteName = "";

        public bool showWaits;
        public ConditionFlag WaitForCondition;
        public int WaitTimeMs;
        public Vector2 WaitTimeET;

        public bool NeedsMount => Movement is Movement.MountFly or Movement.MountNoFly;
        public uint GatheringType => IsNode ? GatheringPoint.GetRow(InteractWithOID)!.Value.GatheringPointBase.Value.GatheringType.RowId : 99;
        public bool IsNode => GatheringPoint.Get().HasRow(InteractWithOID);
        public uint NodeJob => GatheringType switch {
            _ when !IsNode => 0,
            0 or 1 => ClassJobMiner,
            2 or 3 => ClassJobBotanist,
            4 or 5 => ClassJobFisher,
            _ => 0
        };
        public bool IsPhantom;
    }

    public class Route {
        public string Name = "";
        public string Group = "";
        public int Food;
        public int TargetGatherItem;
        public List<Waypoint> Waypoints = [];
    }

    public List<Route> Routes = [];
    public float DefaultWaypointRadius = 3;
    public float DefaultInteractionRadius = 2;
    public bool GatherModeOnStart = true;
    public bool DisableOnErrors;

    public bool ExtractMateria = true;
    public bool RepairGear = true;
    public float RepairPercent = 20;
    public bool PurifyCollectables;

    public int GlobalFood;

    public bool AutoRetainerIntegration;
    public bool TeleportBetweenZones = true;
    public bool AutoGather;

    public override void Deserialize(JObject j, JsonSerializer ser) {
        Routes.Clear();
        if (j["Routes"] is JArray ja) {
            foreach (var jr in ja) {
                var jn = jr["Name"]?.Value<string>();
                if (jn == null || jr["Waypoints"] is not JArray jw)
                    continue;

                Routes.Add(new Route {
                    Name = jn,
                    Group = jr["Group"]?.Value<string>() ?? "",
                    Food = jr["Food"]?.Value<int>() ?? 0,
                    TargetGatherItem = jr["TargetGatherItem"]?.Value<int>() ?? 0,
                    Waypoints = LoadFromJSONWaypoints(jw),
                });
            }
        }
        DisableOnErrors = (bool?)j["DisableOnErrors"] ?? false;
        GatherModeOnStart = (bool?)j["GatherModeOnStart"] ?? true;
        DefaultWaypointRadius = (float?)j["DefaultWaypointRadius"] ?? 3;
        DefaultInteractionRadius = (float?)j["DefaultInteractionRadius"] ?? 2;
        TeleportBetweenZones = (bool?)j["TeleportBetweenZones"] ?? true;
        AutoRetainerIntegration = (bool?)j["AutoRetainerIntegration"] ?? false;
        AutoGather = (bool?)j["AutoGather"] ?? false;
        ExtractMateria = (bool?)j["ExtractMateria"] ?? true;
        RepairGear = (bool?)j["RepairGear"] ?? true;
        RepairPercent = (float?)j["RepairPercent"] ?? 20;
        PurifyCollectables = (bool?)j["Desynth"] ?? false;
        GlobalFood = (int?)j["GlobalFood"] ?? 0;
        // Intentionally ignore obsolete keys: Manual, GlobalManual, WasFlyingInManual,
        // LandDistance, PathFindCancellationTime, EmoteID, ActionID, ItemID, MobID, QuestID, ChatCommand, etc.
    }

    public override JObject Serialize(JsonSerializer ser) {
        JArray res = [];
        foreach (var r in Routes) {
            res.Add(new JObject {
                { "Name", r.Name },
                { "Group", r.Group },
                { "Food", r.Food },
                { "TargetGatherItem", r.TargetGatherItem },
                { "Waypoints", SaveToJSONWaypoints(r.Waypoints) },
            });
        }
        return new JObject {
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
            { "AutoRetainerIntegration", AutoRetainerIntegration },
            { "AutoGather", AutoGather },
        };
    }

    public static JArray SaveToJSONWaypoints(List<Waypoint> waypoints) {
        JArray jw = [];
        foreach (var wp in waypoints) {
            if (wp.IsPhantom) continue;
            jw.Add(new JObject {
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
                { "showWaits", wp.showWaits },
                { "WaitTimeMs", wp.WaitTimeMs },
                { "WaitForCondition", wp.WaitForCondition.ToString() },
                { "Pathfind", wp.Pathfind },
                { "RouteName", wp.RouteName },
            });
        }
        return jw;
    }

    public static List<Waypoint> LoadFromJSONWaypoints(JArray j) {
        List<Waypoint> res = [];
        try {
            foreach (var jwe in j) {
                if (jwe is not JObject o)
                    continue;

                // Parse known InteractionType values; map obsolete experimental values to None/Standard safely.
                var interactionRaw = o["Interaction"]?.Value<string>();
                var interaction = InteractionType.Standard;
                if (!string.IsNullOrEmpty(interactionRaw) && !Enum.TryParse(interactionRaw, out interaction))
                    interaction = InteractionType.None;

                res.Add(new Waypoint {
                    Position = new Vector3(
                        o["X"]?.Value<float>() ?? 0,
                        o["Y"]?.Value<float>() ?? 0,
                        o["Z"]?.Value<float>() ?? 0
                    ),
                    ZoneID = o["ZoneID"]?.Value<uint>() ?? 0,
                    Radius = o["Radius"]?.Value<float>() ?? 0,
                    InteractWithName = o["InteractWithName"]?.Value<string>() ?? "",
                    Movement = Enum.TryParse<Movement>(o["Movement"]?.Value<string>(), out var movement) ? movement : Movement.Normal,
                    InteractWithOID = o["InteractWithOID"]?.Value<uint>() ?? 0,
                    InteractWithPosition = new Vector3(
                        o["iX"]?.Value<float>() ?? 0,
                        o["iY"]?.Value<float>() ?? 0,
                        o["iZ"]?.Value<float>() ?? 0
                    ),
                    showInteractions = o["showInteractions"]?.Value<bool>() ?? false,
                    Interaction = interaction,
                    showWaits = o["showWaits"]?.Value<bool>() ?? false,
                    WaitTimeMs = o["WaitTimeMs"]?.Value<int>() ?? 0,
                    WaitForCondition = Enum.TryParse<ConditionFlag>(o["WaitForCondition"]?.Value<string>(), out var condition) ? condition : ConditionFlag.None,
                    Pathfind = o["Pathfind"]?.Value<bool>() ?? false,
                    RouteName = o["RouteName"]?.Value<string>() ?? "",
                });
            }
        }
        catch (Exception) {
            Service.Log.Error("Failed to load waypoints from JSON.");
        }
        return res;
    }

    public static List<string> GetGroups(GatherRouteDB gatherRouteDB, bool sort = false) {
        List<string> groups = ["Ungrouped"];
        for (var g = 0; g < gatherRouteDB.Routes.Count; g++) {
            if (string.IsNullOrEmpty(gatherRouteDB.Routes[g].Group))
                gatherRouteDB.Routes[g].Group = "Ungrouped";
            if (!groups.Contains(gatherRouteDB.Routes[g].Group))
                groups.Add(gatherRouteDB.Routes[g].Group);
        }
        if (sort)
            groups = [.. groups.OrderBy(i => i == "Ungrouped").ThenBy(i => i)];
        return groups;
    }

    public static void TryImport(GatherRouteDB routeDB) {
        try {
            var data = ImGui.GetClipboardText();
            (var isBase64, var json) = Utils.FromCompressedBase64(data);
            Route? import = null;
            if (isBase64)
                import = JsonConvert.DeserializeObject<Route>(json);
            else if (Utils.IsJson(data))
                import = JsonConvert.DeserializeObject<Route>(data);
            if (import == null)
                return;

            // Unknown / obsolete waypoint keys are ignored by Newtonsoft (MissingMemberHandling.Ignore).
            if (import.Waypoints.Any(x => (x.Pathfind || x.Interaction == InteractionType.NodeScan) && !Service.Navmesh.IsEnabled))
                Service.ChatGui.Print($"[{Service.Interface.InternalName}] Imported route uses pathfinding, but vnavmesh is not installed. It's located on the same repo as {Service.Interface.InternalName} ({Plugin.Repo}).");

            routeDB.Routes.Add(import);
            routeDB.NotifyModified();
        }
        catch (JsonReaderException ex) {
            Service.ChatGui.PrintError($"Failed to import route: {ex.Message}");
            Service.Log.Error(ex, "Failed to import route");
        }
    }
}

public static class WaypointExtensions {
    public static bool TryGetNextWaypoint(this GatherRouteDB.Waypoint waypoint, GatherRouteDB.Route route, bool loop, out GatherRouteDB.Waypoint? nextWaypoint) {
        var index = route.Waypoints.IndexOf(waypoint);
        if (index >= 0 && index < route.Waypoints.Count - 1) {
            nextWaypoint = route.Waypoints[index + 1];
            return true;
        }
        if (loop) {
            nextWaypoint = route.Waypoints.First();
            return true;
        }
        nextWaypoint = null;
        return false;
    }

    public static void AddWaypointsAfter(this GatherRouteDB.Waypoint waypoint, GatherRouteDB.Route route, List<GatherRouteDB.Waypoint> waypoints) {
        var index = route.Waypoints.IndexOf(waypoint);
        route.Waypoints.InsertRange(index + 1, waypoints);
    }

    public static bool IsLast(this GatherRouteDB.Waypoint waypoint, GatherRouteDB.Route route) => waypoint.Equals(route.Waypoints.Last());
}
