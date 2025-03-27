using ECommons.GameHelpers;
using System.Threading;
using System.Threading.Tasks;
using visland.Helpers;
using visland.Tasks;
using static visland.Gathering.GatherRouteDB;

namespace visland.Gathering;
public sealed class GatherRouteTask(Route route, int waypoint, bool continueToNext, bool loopAtEnd, bool pathfind = false) : AutoCommon
{
    public override async Task Execute(CancellationToken token)
    {
        while (true)
        {
            if (!Player.Available || Paused || waypoint >= route.Waypoints.Count)
            {
                await NextFrame();
                continue;
            }
            var wp = route.Waypoints[waypoint];
            var needToGetCloser = (wp.Position - Player.Position).LengthSquared() > wp.Radius * wp.Radius;
            if (needToGetCloser)
            {
                if (wp.Pathfind || pathfind)
                    await MoveTo(wp.Position, wp.Radius, wp.NeedsMount, wp.Movement == Movement.MountFly);
                else
                    await MoveToDirectly(wp.Position, wp.Radius, wp.NeedsMount);
            }

            switch (wp.Interaction)
            {
                case InteractionType.Standard:
                    await WaitUntil(() => !Player.IsBusy, "WaitingForNotBusy");
                    if (Game.FindInteractable(wp.InteractWithOID) is { } obj)
                        await InteractWith(obj);
                    break;
            }

            if (!continueToNext) return;

            if (++waypoint >= route.Waypoints.Count)
            {
                if (loopAtEnd)
                {
                    route.Waypoints.RemoveAll(x => x.IsPhantom);
                    waypoint = 0;
                }
                else
                    return;
            }
        }
    }
}
