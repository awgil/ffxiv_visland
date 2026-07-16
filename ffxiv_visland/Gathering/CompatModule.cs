using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using visland.Helpers;

namespace visland.Gathering;

internal class CompatModule {
    public static unsafe void EnsureCompatibility(GatherRouteDB RouteDB) {
        if (RouteDB.GatherModeOnStart) {
            if (Player.IsOnIsland && MJIManager.Instance()->CurrentMode != 1) {
                AtkCallback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("MJIHud").Address, false, 11, 0);
                AtkCallback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("ContextIconMenu").Address, true, 0, 1, 82042, 0, 0);
            }

            if (Player.IsOnIsland && AddonUtils.TryGetAddonByName("ContextIconMenu", out var cim) && cim->IsVisible)
                AtkCallback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("ContextIconMenu").Address, true, -1);
        }

        if (!PurificationManager.ListenersActive)
            PurificationManager.EnableListeners();
        if (!RepairManager.ListenersActive)
            RepairManager.ToggleListeners(true);

        OverrideAFK.ResetTimers();
    }

    public static void RestoreChanges() {
        PurificationManager.DisableListeners();
        RepairManager.ToggleListeners(false);
    }
}
