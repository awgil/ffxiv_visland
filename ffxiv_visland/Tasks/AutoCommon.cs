using ECommons.GameHelpers;
using Lumina.Excel.Sheets;
using System;
using System.Numerics;
using System.Threading.Tasks;
using visland.Gathering;
using visland.Helpers;
using visland.IPC;

namespace visland.Tasks;
public abstract class AutoCommon : AutoTask
{
    private readonly OverrideMovement movement = new();

     protected async Task MoveTo(Vector3 dest, float tolerance, bool mount = false, bool fly = false)
    {
        using var scope = BeginScope("MoveTo");
        if (Player.DistanceTo(dest) < tolerance)
            return; // already in range

        if (mount)
            await Mount();

        Status = "Waiting on mesh";
        // ensure navmesh is ready
        await WaitWhile(() => NavmeshIPC.BuildProgress() >= 0, "BuildMesh");
        ErrorIf(!NavmeshIPC.IsReady(), "Failed to build navmesh for the zone");
        ErrorIf(!NavmeshIPC.PathfindAndMoveTo(dest, fly), "Failed to start pathfinding to destination");
        Status = $"Moving to {dest}";
        using var stop = new OnDispose(NavmeshIPC.Stop);
        await WaitWhile(() => !(Player.DistanceTo(dest) < tolerance), "Navigate");
    }

    protected async Task MoveToDirectly(Vector3 dest, float tolerance, bool mount = false)
    {
        using var scope = BeginScope("MoveToDirectly");
        if (Player.DistanceTo(dest) < tolerance)
            return;

        if (mount)
            await Mount();

        Status = $"Moving to {dest}";
        movement.DesiredPosition = dest;
        movement.Enabled = true;
        using var stop = new OnDispose(() => movement.Enabled = false);
        await WaitWhile(() => !(Player.DistanceTo(dest) < tolerance), "DirectNavigate");
    }

    protected async Task TeleportTo(uint territoryId, Vector3 destination)
    {
        using var scope = BeginScope("Teleport");
        if (Player.Territory == territoryId)
            return; // already in correct zone

        var closestAetheryteId = Coords.FindClosestAetheryte(territoryId, destination);
        var teleportAetheryteId = Coords.FindPrimaryAetheryte(closestAetheryteId);
        ErrorIf(teleportAetheryteId == 0, $"Failed to find aetheryte in {territoryId}");
        if (Player.Territory != GetRow<Aetheryte>(teleportAetheryteId)!.Value.Territory.RowId)
        {
            ErrorIf(!Game.ExecuteTeleport(teleportAetheryteId), $"Failed to teleport to {teleportAetheryteId}");
            await WaitWhile(() => !Player.IsBusy, "TeleportStart");
            await WaitWhile(() => Player.IsBusy, "TeleportFinish");
        }

        if (teleportAetheryteId != closestAetheryteId)
        {
            var (aetheryteId, aetherytePos) = Game.FindAetheryte(teleportAetheryteId);
            await MoveTo(aetherytePos, 10);
            ErrorIf(!Game.InteractWith(aetheryteId), "Failed to interact with aetheryte");
            await WaitUntilSkipTalk(() => Game.AddonActive("SelectString"), "WaitSelectAethernet");
            Game.TeleportToAethernet(teleportAetheryteId, closestAetheryteId);
            await WaitWhile(() => !Player.IsBusy, "TeleportAethernetStart");
            await WaitWhile(() => Player.IsBusy, "TeleportAethernetFinish");
        }

        if (territoryId == 886)
        {
            // firmament special case
            var (aetheryteId, aetherytePos) = Game.FindAetheryte(teleportAetheryteId);
            await MoveTo(aetherytePos, 10);
            ErrorIf(!Game.InteractWith(aetheryteId), "Failed to interact with aetheryte");
            await WaitUntilSkipTalk(() => Game.AddonActive("SelectString"), "WaitSelectFirmament");
            Game.TeleportToFirmament(teleportAetheryteId);
            await WaitWhile(() => !Player.IsBusy, "TeleportFirmamentStart");
            await WaitWhile(() => Player.IsBusy, "TeleportFirmamentFinish");
        }

        ErrorIf(Player.Territory != territoryId, "Failed to teleport to expected zone");
    }

    protected async Task Mount()
    {
        using var scope = BeginScope("Mount");
        if (Player.Mounted) return;
        Status = "Mounting";
        PlayerEx.Mount();
        await WaitUntil(() => Player.Mounted, "Mounting");
        ErrorIf(!Player.Mounted, "Failed to mount");
    }

    protected async Task WaitUntilSkipTalk(Func<bool> condition, string scopeName)
    {
        using var scope = BeginScope(scopeName);
        while (!condition())
        {
            if (Game.AddonActive("Talk"))
            {
                Log("progressing talk...");
                Game.ProgressTalk();
            }
            Log("waiting...");
            await NextFrame();
        }
    }

    protected async Task WaitUntilSkipYesNo(Func<bool> condition, string scopeName)
    {
        using var scope = BeginScope(scopeName);
        while (!condition())
        {
            if (Game.AddonActive("SelectYesno"))
            {
                Log("progressing yes/no...");
                Game.SelectYes();
            }
            Log("waiting...");
            await NextFrame();
        }
    }

    protected async Task InteractWith(ulong gameobjectId)
    {
        using var scope = BeginScope("InteractWith");
        Status = $"Interacting with {gameobjectId}";
        const int maxAttempts = 5;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (Game.InteractWith(gameobjectId))
                return;
            await NextFrame();
        }
        ErrorIf(true, $"Failed to interact with object after {maxAttempts} tries");
    }
}
