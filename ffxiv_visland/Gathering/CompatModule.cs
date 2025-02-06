using ECommons;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using visland.Helpers;

namespace visland.Gathering;
internal class CompatModule
{
    public static unsafe void EnsureCompatibility(GatherRouteDB RouteDB)
    {
        // set flight activation to double jump to prevent flying when running off cliffs
        //if (Player.FlyingControlType == 0)
        //{
        //    RouteDB.WasFlyingInManual = false;
        //    Player.FlyingControlType = 1;
        //}

        if (RouteDB.GatherModeOnStart)
        {
            if (PlayerEx.OnIsland && MJIManager.Instance()->CurrentMode != 1)
            {
                // you can't just change the CurrentMode in MJIManager
                Callback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("MJIHud"), false, 11, 0);
                Callback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("ContextIconMenu"), true, 0, 1, 82042, 0, 0);
            }

            // the context menu doesn't respect the updateState for some reason
            if (PlayerEx.OnIsland && GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContextIconMenu", out var cim) && cim->IsVisible)
                Callback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("ContextIconMenu"), true, -1);
        }

        if (!PurificationManager.ListenersActive)
            PurificationManager.EnableListeners();
        if (!RepairManager.ListenersActive)
            RepairManager.ToggleListeners(true);

        // ensure we don't get afk-kicked while running the route
        OverrideAFK.ResetTimers();
        //UpdateLegacyMode();
        
    }

    private static bool _legacyMode;
    private static void UpdateLegacyMode()
    {
        _legacyMode = Service.GameConfig.UiControl.TryGetUInt("MoveMode", out var mode) && mode == 0;
        Service.Log.Info($"Legacy mode is now {(_legacyMode ? "enabled" : "disabled")}");
    }

    public static void RestoreChanges()
    {
        //if (!Service.Config.Get<GatherRouteDB>().WasFlyingInManual)
        //    Player.FlyingControlType = 0;
        PurificationManager.DisableListeners();
        RepairManager.ToggleListeners(false);
    }
}
