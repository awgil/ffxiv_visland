using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;
using System;

namespace visland.Questing;
internal unsafe class ExecSelectYes
{
    internal static bool IsEnabled = false;

    internal static void Init()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", Click);
        IsEnabled = true;
    }

    internal static void Shutdown()
    {
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", Click);
        IsEnabled = false;
    }

    internal static void Toggle() => (IsEnabled ? new Action(Shutdown) : Init)();

    private static void Click(AddonEvent type, AddonArgs args)
    {
        if (IsEnabled)
        {
            var addonPtr = (AddonSelectYesno*)args.Addon;
            var yesButton = addonPtr->YesButton;
            if (yesButton != null && !yesButton->IsEnabled)
            {
                Svc.Log.Debug($"{nameof(ExecSelectYes)}: Enabling yes button");
                var flagsPtr = (ushort*)&yesButton->AtkComponentBase.OwnerNode->AtkResNode.NodeFlags;
                *flagsPtr ^= 1 << 5;
            }
            Callback.Fire((AtkUnitBase*)args.Addon, true, 0);
        }
    }
}
