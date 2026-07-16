using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Bindings.ImGui;

namespace visland.Helpers;

public static class ImGuiExtensions {
    extension(ImGui) {
        public static void TextV(string text) {
            ImGui.AlignTextToFramePadding();
            ImGui.Text(text);
        }

        public static bool IconButton(FontAwesomeIcon icon, string? tooltip = null) {
            var res = ImGuiComponents.IconButton(icon);
            if (res && tooltip != null && ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
            return res;
        }
    }
}
