using ImGuiNET;
using visland.Helpers;

namespace visland.Workshop;

public class WorkshopSettings
{
    public readonly Config _config;

    public class Config : Configuration.Node
    {
        public bool AutoOpenNextDay = false;
        public bool AutoImport = false;
    }

    public void Draw()
    {
        if (ImGui.Checkbox("Auto Collect", ref _config.AutoOpenNextDay))
            _config.NotifyModified();
        if (ImGui.Checkbox("Auto Max", ref _config.AutoImport))
            _config.NotifyModified();
    }
}
