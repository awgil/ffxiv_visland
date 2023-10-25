using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using visland.Helpers;

namespace visland;

public class GatherRouteDB
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

    private string _newName = "";
    private Route? _routeToDelete;
    private Waypoint? _waypointToModify;
    private int _waypointMoveDir; // 0 = del, -1 = move up, +1 = move down
    public List<Route> Routes = new();

    public void Draw(UITree tree, GatherRouteExec exec, ref bool modified)
    {
        foreach (var r in tree.Nodes(Routes, r => new($"{r.Name} ({r.Waypoints.Count} steps)###{r.Name}"), r => RouteContextMenu(r, exec)))
        {
            for (int i = 0; i < r.Waypoints.Count; ++i)
            {
                var wp = r.Waypoints[i];
                foreach (var wn in tree.Node($"#{i+1}: [{wp.Position.X:f3}, {wp.Position.Y:f3}, {wp.Position.Z:f3}] +- {wp.Radius:f3} ({wp.Movement}) @ {wp.InteractWithName} ({wp.InteractWithOID:X})###{i}", contextMenu: () => WaypointContextMenu(r, wp, exec)))
                {
                    if (ImGui.InputFloat3("Position", ref wp.Position))
                        modified = true;
                    if (ImGui.InputFloat("Radius", ref wp.Radius))
                        modified = true;
                    if (UICombo.Enum("Movement mode", ref wp.Movement))
                        modified = true;

                    if (ImGui.Button("Set position to current") && Service.ClientState.LocalPlayer is var player && player != null)
                    {
                        wp.Position = player.Position;
                        modified = true;
                    }
                }
            }

            if (ImGui.Button("Interact with target"))
            {
                var target = Service.TargetManager.Target;
                if (target != null)
                {
                    r.Waypoints.Add(new() { Position = target.Position, Radius = 2, Movement = Service.Condition[ConditionFlag.Mounted] ? Movement.MountFly : Movement.Normal, InteractWithOID = target.DataId, InteractWithName = target.Name.ToString().ToLower() });
                    modified = true;
                    exec.Start(r, r.Waypoints.Count - 1, false, false);
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Move to current position"))
            {
                exec.Finish();
                var player = Service.ClientState.LocalPlayer;
                if (player != null)
                {
                    r.Waypoints.Add(new() { Position = player.Position, Radius = 3, Movement = Service.Condition[ConditionFlag.Mounted] ? Movement.MountFly : Movement.Normal });
                    modified = true;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Export to clipboard"))
            {
                ImGui.SetClipboardText(SaveToJSONWaypoints(r.Waypoints).ToString(formatting: 0));
            }

            if (_waypointToModify != null)
            {
                var index = r.Waypoints.IndexOf(_waypointToModify);
                if (index >= 0)
                {
                    if (exec.CurrentRoute == r)
                        exec.Finish();

                    r.Waypoints.RemoveAt(index);
                    if (_waypointMoveDir == -1)
                        r.Waypoints.Insert(Math.Max(0, index - 1), _waypointToModify);
                    else if (_waypointMoveDir == +1)
                        r.Waypoints.Insert(Math.Min(r.Waypoints.Count, index + 1), _waypointToModify);
                }
                modified = true;
                _waypointToModify = null;
            }
        }

        ImGui.SetNextItemWidth(200);
        ImGui.InputText("New route name", ref _newName, 256);
        using (ImRaii.Disabled(_newName.Length == 0 || Routes.Any(r => r.Name == _newName)))
        {
            ImGui.SameLine();
            if (ImGui.Button("Create new"))
            {
                Routes.Add(new() { Name = _newName });
                modified = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Import from clipboard"))
            {
                try
                {
                    Routes.Add(new() { Name = _newName, Waypoints = LoadFromJSONWaypoints(JArray.Parse(ImGui.GetClipboardText())) });
                    modified = true;
                }
                catch (JsonReaderException ex)
                {
                    Service.ChatGui.PrintError($"Failed to import route: {ex.Message}");
                    Service.Log.Error(ex, "Failed to import route");
                }
            }
        }

        if (_routeToDelete != null)
        {
            if (exec.CurrentRoute == _routeToDelete)
                exec.Finish();
            Routes.Remove(_routeToDelete);
            modified = true;
            _routeToDelete = null;
        }
    }

    public void LoadFromJSON(JArray j, JsonSerializer ser)
    {
        foreach (var jr in j)
        {
            var jn = jr["Name"]?.Value<string>();
            var jw = jr["Waypoints"] as JArray;
            if (jn != null && jw != null)
                Routes.Add(new Route() { Name = jn, Waypoints = LoadFromJSONWaypoints(jw) });
        }
    }

    public JArray SaveToJSON(JsonSerializer ser)
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
        return res;
    }

    private void RouteContextMenu(Route r, GatherRouteExec exec)
    {
        if (ImGui.MenuItem("Execute once"))
        {
            exec.Start(r, 0, true, false);
        }

        if (ImGui.MenuItem("Execute continuously"))
        {
            exec.Start(r, 0, true, true);
        }

        if (ImGui.MenuItem("Delete"))
        {
            _routeToDelete = r;
        }
    }

    private void WaypointContextMenu(Route r, Waypoint wp, GatherRouteExec exec)
    {
        if (ImGui.MenuItem("Execute this step only"))
        {
            exec.Start(r, r.Waypoints.IndexOf(wp), false, false);
        }

        if (ImGui.MenuItem("Execute route once starting from this step"))
        {
            exec.Start(r, r.Waypoints.IndexOf(wp), true, false);
        }

        if (ImGui.MenuItem("Execute route starting from this step and then loop"))
        {
            exec.Start(r, r.Waypoints.IndexOf(wp), true, true);
        }

        if (ImGui.MenuItem("Move up"))
        {
            _waypointToModify = wp;
            _waypointMoveDir = -1;
        }

        if (ImGui.MenuItem("Move down"))
        {
            _waypointToModify = wp;
            _waypointMoveDir = +1;
        }

        if (ImGui.MenuItem("Delete"))
        {
            _waypointToModify = wp;
            _waypointMoveDir = 0;
        }
    }

    private JArray SaveToJSONWaypoints(List<Waypoint> waypoints)
    {
        JArray jw = new();
        foreach (var wp in waypoints)
            jw.Add(new JArray() { wp.Position.X, wp.Position.Y, wp.Position.Z, wp.Radius, wp.InteractWithName, wp.Movement, wp.InteractWithOID });
        return jw;
    }

    private List<Waypoint> LoadFromJSONWaypoints(JArray j)
    {
        List<Waypoint> res = new();
        foreach (var jwe in j)
        {
            var jwea = jwe as JArray;
            if (jwea == null || jwea.Count < 5)
                continue;
            var movement = jwea.Count <= 5 ? Movement.Normal
                : jwea[5].Type == JTokenType.Boolean ? (jwea[5].Value<bool>() ? Movement.MountFly : Movement.Normal)
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
