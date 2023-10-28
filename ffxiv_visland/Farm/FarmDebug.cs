using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace visland.Farm;

public unsafe class FarmDebug
{
    public void Draw()
    {
        var agent = AgentModule.Instance()->GetAgentByInternalId(AgentId.MJIFarmManagement);
        var owner = *(AgentInterface**)((nint)agent + 0x28);
        if (owner != null)
        {
            ImGui.TextUnformatted($"u28: {(nint)owner->VTable - Service.SigScanner.Module.BaseAddress:X}");
        }
    }
}
