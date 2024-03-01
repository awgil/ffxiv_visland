using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using visland.Helpers;

namespace visland.IPC;
class BossModIPC
{
    internal static string Name = "BossMod";

    internal static ICallGateSubscriber<object>? IsMoving;
    internal static ICallGateSubscriber<object>? ForbiddenZonesCount;
    internal static ICallGateSubscriber<object>? InitiateCombat;
    internal static ICallGateSubscriber<bool, object>? SetAutorotationState;

    internal static void Init()
    {
        if (Utils.HasPlugin($"{Name}"))
        {
            IsMoving = Svc.PluginInterface.GetIpcSubscriber<object>($"{Name}.IsMoving");
            ForbiddenZonesCount = Svc.PluginInterface.GetIpcSubscriber<object>($"{Name}.ForbiddenZonesCount");
            InitiateCombat = Svc.PluginInterface.GetIpcSubscriber<object>($"{Name}.InitiateCombat");
            SetAutorotationState = Svc.PluginInterface.GetIpcSubscriber<bool, object>($"{Name}.SetAutorotationState");
        }
    }
}
