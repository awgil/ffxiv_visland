using Dalamud.Game.ClientState.Conditions;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using ImGuiNET;
using SharpDX;
using System;
using System.Linq;

namespace visland;

public class GatherRouteExec : IDisposable
{
    public GatherRouteDB.Route? CurrentRoute;
    public int CurrentWaypoint;
    public bool ContinueToNext;
    public bool LoopAtEnd;
    public bool Paused;

    private OverrideCamera _camera = new();
    private OverrideMovement _movement = new();
    private OverrideAFK _afk = new();

    private Throttle _interact = new();
    private Throttle _action = new();

    public GatherRouteExec()
    {
    }

    public void Dispose()
    {
        _camera.Dispose();
        _movement.Dispose();
    }

    public unsafe void Update()
    {
        var player = Service.ClientState.LocalPlayer;
        _camera.SpeedH = _camera.SpeedV = default;
        _movement.DesiredPosition = player?.Position ?? new();

        var gathering = Service.Condition[ConditionFlag.OccupiedInQuestEvent] || Service.Condition[ConditionFlag.OccupiedInEvent] || Service.Condition[ConditionFlag.OccupiedSummoningBell];
        bool aboutToBeMounted = Service.Condition[ConditionFlag.Unknown57]; // condition 57 is set while mount up animation is playing
        if (player == null || player.IsCasting || gathering || aboutToBeMounted || Paused || CurrentRoute == null || CurrentWaypoint >= CurrentRoute.Waypoints.Count)
            return;

        // ensure we don't get afk-kicked while running the route
        _afk.ResetTimers();

        var wp = CurrentRoute.Waypoints[CurrentWaypoint];
        var toWaypoint = wp.Position - player.Position;
        var toWaypointXZ = new Vector3(toWaypoint.X, 0, toWaypoint.Z);
        bool needToGetCloser = toWaypoint.LengthSquared() > wp.Radius * wp.Radius;

        if (needToGetCloser)
        {
            bool mounted = Service.Condition[ConditionFlag.Mounted];
            if (wp.Movement != GatherRouteDB.Movement.Normal && !mounted)
            {
                ExecuteMount();
                return;
            }

            _movement.DesiredPosition = wp.Position;
            _camera.SpeedH = _camera.SpeedV = 360.Degrees();
            _camera.DesiredAzimuth = Angle.FromDirection(toWaypoint.X, toWaypoint.Z) + 180.Degrees();

            var sprintStatus = player.StatusList.FirstOrDefault(s => s.StatusId == 50);
            var sprintRemaining = sprintStatus?.RemainingTime ?? 0;
            if (sprintRemaining < 5 && !mounted && MJIManager.Instance()->IsPlayerInSanctuary == 1)
            {
                ExecuteIslandSprint();
            }

            bool flying = Service.Condition[ConditionFlag.InFlight] || Service.Condition[ConditionFlag.Diving];
            if (wp.Movement == GatherRouteDB.Movement.MountFly && mounted && !flying && !Service.Condition[ConditionFlag.Jumping])
            {
                // TODO: improve, jump is not the best really...
                ExecuteJump();
            }

            return;
        }

        var interactObj = !gathering ? FindObjectToInteractWith(wp) : null;
        if (interactObj != null)
        {
            _interact.Exec(() =>
            {
                Service.Log.Debug("Interacting...");
                TargetSystem.Instance()->InteractWithObject(interactObj);
            });
            return;
        }

        if (!ContinueToNext)
        {
            Finish();
            return;
        }

        if (++CurrentWaypoint >= CurrentRoute!.Waypoints.Count)
        {
            if (LoopAtEnd)
            {
                CurrentWaypoint = 0;
            }
            else
            {
                Finish();
            }
        }
    }

    public void Start(GatherRouteDB.Route route, int waypoint, bool continueToNext, bool loopAtEnd)
    {
        CurrentRoute = route;
        CurrentWaypoint = waypoint;
        ContinueToNext = continueToNext;
        LoopAtEnd = loopAtEnd;
        _camera.Enabled = true;
        _movement.Enabled = true;
    }

    public void Finish()
    {
        if (CurrentRoute == null)
            return;
        CurrentRoute = null;
        CurrentWaypoint = 0;
        ContinueToNext = false;
        LoopAtEnd = false;
        _camera.Enabled = false;
        _movement.Enabled = false;
    }

    public void Draw(UITree tree)
    {
        if (CurrentRoute == null || CurrentWaypoint >= CurrentRoute.Waypoints.Count)
        {
            ImGui.TextUnformatted("Route not running");
            return;
        }

        var curPos = Service.ClientState.LocalPlayer?.Position ?? new();
        var wp = CurrentRoute.Waypoints[CurrentWaypoint];
        if (ImGui.Button(Paused ? "Resume" : "Pause"))
        {
            Paused = !Paused;
        }
        ImGui.SameLine();
        if (ImGui.Button("Stop"))
        {
            Finish();
        }
        if (CurrentRoute != null) // Finish() call could've reset it
        {
            ImGui.SameLine();
            ImGui.TextUnformatted($"Executing: {CurrentRoute.Name} #{CurrentWaypoint + 1}: [{wp.Position.X:f3}, {wp.Position.Y:f3}, {wp.Position.Z:f3}] +- {wp.Radius:f3} (dist={(curPos - wp.Position).Length():f3}) @ {wp.InteractWithName} ({wp.InteractWithOID:X})");
        }
    }

    private unsafe GameObject* FindObjectToInteractWith(GatherRouteDB.Waypoint wp)
    {
        if (wp.InteractWithOID == 0)
            return null;

        foreach (var obj in Service.ObjectTable.Where(o => o.DataId == wp.InteractWithOID && (o.Position - wp.Position).LengthSquared() < 1))
            return obj.IsTargetable ? (GameObject*)obj.Address : null;

        return null;
    }

    private unsafe void ExecuteActionSafe(ActionType type, uint id) => _action.Exec(() => ActionManager.Instance()->UseAction(type, id));
    private void ExecuteIslandSprint() => ExecuteActionSafe(ActionType.Action, 31314);
    private void ExecuteMount() => ExecuteActionSafe(ActionType.GeneralAction, 24); // flying mount roulette
    private void ExecuteJump() => ExecuteActionSafe(ActionType.GeneralAction, 2);
}
