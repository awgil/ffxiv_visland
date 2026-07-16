using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using visland.Helpers;

namespace visland.Gathering;

internal class CompatModule {
    public static unsafe void EnsureCompatibility(GatherRouteDB RouteDB) {
        if (RouteDB.GatherModeOnStart) {
            if (Player.IsOnIsland && MJIManager.Instance()->CurrentMode != 1) {
                AtkCallback.Fire("MJIHud", false, 11, 0);
                AtkCallback.Fire("ContextIconMenu", true, 0, 1, 82042, 0, 0);
            }

            if (Player.IsOnIsland)
                AtkCallback.Fire("ContextIconMenu", true, -1);
        }

        if (!PurificationManager.ListenersActive)
            PurificationManager.EnableListeners();

        OverrideAFK.ResetTimers();
    }

    public static void RestoreChanges() {
        PurificationManager.DisableListeners();
    }
}
