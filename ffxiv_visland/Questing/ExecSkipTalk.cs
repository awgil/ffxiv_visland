using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ECommons.Automation;
using System;

namespace visland.Questing;
internal unsafe static class ExecSkipTalk
{
    internal static bool IsEnabled = false;

    internal static void Init()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Talk", Click);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, "Talk", Click);
        IsEnabled = true;
    }

    internal static void Shutdown()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Talk", Click);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "Talk", Click);
        IsEnabled = false;
    }

    internal static void Toggle() => (IsEnabled ? new Action(Shutdown) : Init)();

    private static void Click(AddonEvent type, AddonArgs args)
    {
        if (IsEnabled)
        {
            Callback.Fire((AtkUnitBase*)args.Addon, true, 0);
        }
    }
}
