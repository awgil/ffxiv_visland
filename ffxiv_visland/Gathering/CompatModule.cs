using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace visland.Gathering;
internal class CompatModule
{
    public static unsafe void EnsureCompatibility(GatherRouteDB RouteDB)
    {
        // set flight activation to single jump for ease of flight activation
        if (Svc.GameConfig.UiControl.GetUInt("FlyingControlType") == 1)
        {
            Service.Config.Get<GatherRouteDB>().WasFlyingInManual = true;
            Svc.GameConfig.Set(Dalamud.Game.Config.UiControlOption.FlyingControlType, 0);
        }

        if (RouteDB.GatherModeOnStart)
        {
            if (MJIManager.Instance()->IsPlayerInSanctuary == 1 && MJIManager.Instance()->CurrentMode != 1)
            {
                // you can't just change the CurrentMode in MJIManager
                Callback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("MJIHud"), false, 11, 0);
                Callback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("ContextIconMenu"), true, 0, 1, 82042, 0, 0);
            }

            // the context menu doesn't respect the updateState for some reason
            if (MJIManager.Instance()->IsPlayerInSanctuary == 1 && GenericHelpers.TryGetAddonByName<AtkUnitBase>("ContextIconMenu", out var cim) && cim->IsVisible)
                Callback.Fire((AtkUnitBase*)Service.GameGui.GetAddonByName("ContextIconMenu"), true, -1);
        }

        // ensure we don't get afk-kicked while running the route
        OverrideAFK.ResetTimers();
    }

    public static void RestoreChanges()
    {
        if (Service.Config.Get<GatherRouteDB>().WasFlyingInManual)
            Svc.GameConfig.Set(Dalamud.Game.Config.UiControlOption.FlyingControlType, 1);
    }
}
