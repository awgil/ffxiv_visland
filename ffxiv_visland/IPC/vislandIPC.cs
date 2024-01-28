using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace visland.IPC
{
    internal class VislandIPC
    {
        private const string IsRouteRunning = "visland.IsRouteRunning";
        public bool Running;

        private readonly ICallGateProvider<bool> _isRouteRunning;

        public VislandIPC(DalamudPluginInterface pluginInterface)
        {
            _isRouteRunning = pluginInterface.GetIpcProvider<bool>(IsRouteRunning);
            _isRouteRunning.RegisterFunc(CheckIsRouteRunning);
        }

        private bool CheckIsRouteRunning() => Running;

        public void Dispose()
        {
            _isRouteRunning.UnregisterFunc();
        }
    }
}
