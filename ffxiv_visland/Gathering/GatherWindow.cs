using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using visland.Helpers;

namespace visland.Gathering;

public class GatherWindow : Window, IDisposable
{
    private UITree _tree = new();
    private string _newName = "";
    private List<Action> _postDraw = new();

    public GatherRouteDB RouteDB;
    public GatherRouteExec Exec = new();

    public GatherWindow() : base("Island sanctuary automation")
    {
        Size = new Vector2(800, 800);
        SizeCondition = ImGuiCond.FirstUseEver;
        RouteDB = Service.Config.Get<GatherRouteDB>();
    }

    public void Dispose()
    {
        Exec.Dispose();
    }

    public override void PreOpenCheck()
    {
        Exec.Update();
    }

    public override void Draw()
    {
        DrawExecution();
        DrawRoutes();

        foreach (var a in _postDraw)
            a();
        _postDraw.Clear();
    }

    private void DrawExecution()
    {
        if (Exec.CurrentRoute == null || Exec.CurrentWaypoint >= Exec.CurrentRoute.Waypoints.Count)
        {
            ImGui.TextUnformatted("Route not running");
            return;
        }

        var curPos = Service.ClientState.LocalPlayer?.Position ?? new();
        var wp = Exec.CurrentRoute.Waypoints[Exec.CurrentWaypoint];
        if (ImGui.Button(Exec.Paused ? "Resume" : "Pause"))
        {
            Exec.Paused = !Exec.Paused;
        }
        ImGui.SameLine();
        if (ImGui.Button("Stop"))
        {
            Exec.Finish();
        }
        if (Exec.CurrentRoute != null) // Finish() call could've reset it
        {
            ImGui.SameLine();
            ImGui.TextUnformatted($"Executing: {Exec.CurrentRoute.Name} #{Exec.CurrentWaypoint + 1}: [{wp.Position.X:f3}, {wp.Position.Y:f3}, {wp.Position.Z:f3}] +- {wp.Radius:f3} (dist={(curPos - wp.Position).Length():f3}) @ {wp.InteractWithName} ({wp.InteractWithOID:X})");
        }
    }

    private void DrawRoutes()
    {
        foreach (var r in _tree.Nodes(RouteDB.Routes, r => new($"{r.Name} ({r.Waypoints.Count} steps)###{r.Name}"), ContextMenuRoute))
        {
            DrawRoute(r);
        }

        ImGui.SetNextItemWidth(200);
        ImGui.InputText("New route name", ref _newName, 256);
        using (ImRaii.Disabled(_newName.Length == 0 || RouteDB.Routes.Any(r => r.Name == _newName)))
        {
            ImGui.SameLine();
            if (ImGui.Button("Create new"))
            {
                RouteDB.Routes.Add(new() { Name = _newName });
                RouteDB.NotifyModified();
            }
            ImGui.SameLine();
            if (ImGui.Button("Import from clipboard"))
            {
                try
                {
                    RouteDB.Routes.Add(new() { Name = _newName, Waypoints = GatherRouteDB.LoadFromJSONWaypoints(JArray.Parse(ImGui.GetClipboardText())) });
                    RouteDB.NotifyModified();
                }
                catch (JsonReaderException ex)
                {
                    Service.ChatGui.PrintError($"Failed to import route: {ex.Message}");
                    Service.Log.Error(ex, "Failed to import route");
                }
            }
        }
    }

    private void DrawRoute(GatherRouteDB.Route r)
    {
        for (int i = 0; i < r.Waypoints.Count; ++i)
        {
            var wp = r.Waypoints[i];
            foreach (var wn in _tree.Node($"#{i + 1}: [{wp.Position.X:f3}, {wp.Position.Y:f3}, {wp.Position.Z:f3}] +- {wp.Radius:f3} ({wp.Movement}) @ {wp.InteractWithName} ({wp.InteractWithOID:X})###{i}", contextMenu: () => ContextMenuWaypoint(r, i)))
            {
                DrawWaypoint(wp);
            }
        }

        if (ImGui.Button("Interact with target"))
        {
            var target = Service.TargetManager.Target;
            if (target != null)
            {
                r.Waypoints.Add(new() { Position = target.Position, Radius = 2, Movement = Service.Condition[ConditionFlag.Mounted] ? GatherRouteDB.Movement.MountFly : GatherRouteDB.Movement.Normal, InteractWithOID = target.DataId, InteractWithName = target.Name.ToString().ToLower() });
                RouteDB.NotifyModified();
                Exec.Start(r, r.Waypoints.Count - 1, false, false);
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Move to current position"))
        {
            Exec.Finish();
            var player = Service.ClientState.LocalPlayer;
            if (player != null)
            {
                r.Waypoints.Add(new() { Position = player.Position, Radius = 3, Movement = Service.Condition[ConditionFlag.Mounted] ? GatherRouteDB.Movement.MountFly : GatherRouteDB.Movement.Normal });
                RouteDB.NotifyModified();
            }
        }
    }

    private void ContextMenuRoute(GatherRouteDB.Route r)
    {
        if (ImGui.MenuItem("Execute once"))
        {
            Exec.Start(r, 0, true, false);
        }

        if (ImGui.MenuItem("Execute continuously"))
        {
            Exec.Start(r, 0, true, true);
        }

        if (Utils.DangerousMenuItem("Delete"))
        {
            _postDraw.Add(() =>
            {
                if (Exec.CurrentRoute == r)
                    Exec.Finish();
                RouteDB.Routes.Remove(r);
                RouteDB.NotifyModified();
            });
        }

        if (ImGui.Button("Export to clipboard"))
        {
            ImGui.SetClipboardText(GatherRouteDB.SaveToJSONWaypoints(r.Waypoints).ToString(Formatting.None));
        }
    }

    private void DrawWaypoint(GatherRouteDB.Waypoint wp)
    {
        if (ImGui.InputFloat3("Position", ref wp.Position))
            RouteDB.NotifyModified();
        if (ImGui.InputFloat("Radius", ref wp.Radius))
            RouteDB.NotifyModified();
        if (UICombo.Enum("Movement mode", ref wp.Movement))
            RouteDB.NotifyModified();

        if (ImGui.Button("Set position to current") && Service.ClientState.LocalPlayer is var player && player != null)
        {
            wp.Position = player.Position;
            RouteDB.NotifyModified();
        }
    }

    private void ContextMenuWaypoint(GatherRouteDB.Route r, int i)
    {
        if (ImGui.MenuItem("Execute this step only"))
        {
            Exec.Start(r, i, false, false);
        }

        if (ImGui.MenuItem("Execute route once starting from this step"))
        {
            Exec.Start(r, i, true, false);
        }

        if (ImGui.MenuItem("Execute route starting from this step and then loop"))
        {
            Exec.Start(r, i, true, true);
        }

        if (ImGui.MenuItem("Move up"))
        {
            _postDraw.Add(() =>
            {
                if (i > 0 && i < r.Waypoints.Count)
                {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    var wp = r.Waypoints[i];
                    r.Waypoints.RemoveAt(i);
                    r.Waypoints.Insert(i - 1, wp);
                    RouteDB.NotifyModified();
                }
            });
        }

        if (ImGui.MenuItem("Move down"))
        {
            _postDraw.Add(() =>
            {
                if (i + 1 < r.Waypoints.Count)
                {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    var wp = r.Waypoints[i];
                    r.Waypoints.RemoveAt(i);
                    r.Waypoints.Insert(i + 1, wp);
                    RouteDB.NotifyModified();
                }
            });
        }

        if (ImGui.MenuItem("Delete"))
        {
            _postDraw.Add(() =>
            {
                if (i < r.Waypoints.Count)
                {
                    if (Exec.CurrentRoute == r)
                        Exec.Finish();
                    r.Waypoints.RemoveAt(i);
                    RouteDB.NotifyModified();
                }
            });
        }
    }
}
