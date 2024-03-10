using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace visland.Questing;
internal unsafe class ExecKillHowTos
{
    internal static bool IsEnabled = false;

    internal static void Init()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "HowToNotice", Click);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "HowTo", Click);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "PlayGuide", Click);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "JobHudNotice", Click);
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Guide", Click);
        IsEnabled = true;
    }

    internal static void Shutdown()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "HowToNotice", Click);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "HowTo", Click);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "PlayGuide", Click);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "JobHudNotice", Click);
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Guide", Click);
        IsEnabled = false;
    }

    internal static void Toggle() => (IsEnabled ? new Action(Shutdown) : Init)();

    private static void Click(AddonEvent type, AddonArgs args)
    {
        if (IsEnabled)
        {
            Callback.Fire((AtkUnitBase*)args.Addon, true, -1);
        }
    }
}
