using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using visland.Helpers;

namespace visland.Gathering;
public unsafe class GatherDebug(GatherRouteExec exec)
{
    private readonly UITree _tree = new();
    private GatherRouteExec exec = exec;

    private string? import;
    public void Draw()
    {
        var activeConditions = Enum.GetValues<ConditionFlag>().Where(c => Svc.Condition[c]).ToList();
        var activeAddons = RaptureAtkModule.Instance()->RaptureAtkUnitManager.AtkUnitManager.AllLoadedUnitsList.Entries.ToArray().Where(a => a.Value != null && a.Value->IsReady && a.Value->IsVisible).Select(a => a.Value->NameString);
        var activeRoute = exec.CurrentRoute;
        string x = $"Conditions: {string.Join(", ", activeConditions)}";
        string y = $"Addons: {string.Join(", ", activeAddons)}";
        string z = $"Route (Waypoint {exec.CurrentWaypoint}): {Utils.ToCompressedBase64(activeRoute)}";

        if (ImGui.Button("Export Debug Info"))
            ImGui.SetClipboardText(Utils.ToCompressedBase64($"{x}\n{y}\n{z}"));
        ImGui.SameLine();
        if (ImGui.Button("Import Debug Info"))
            (_, import)= Utils.FromCompressedBase64(ImGui.GetClipboardText());
        ImGui.SameLine();
        if (ImGui.Button("Wipe import"))
            import = null;

        if (import != null)
        {
            ImGui.TextWrapped(import);
            if (ImGui.IsItemClicked()) ImGui.SetClipboardText(import);
        }
        else
        {
            ImGui.TextWrapped(x);
            if (ImGui.IsItemClicked()) ImGui.SetClipboardText($"{x}");
            ImGui.TextWrapped(y);
            if (ImGui.IsItemClicked()) ImGui.SetClipboardText($"{y}");
            ImGui.TextWrapped(z);
            if (ImGui.IsItemClicked()) ImGui.SetClipboardText($"{z}");
        }

        ImGui.TextUnformatted($"Has Extractables: {SpiritbondManager.IsSpiritbondReadyAny()}");
        ImGui.TextUnformatted($"Has Repairables: {RepairManager.CanRepairAny()}");
        ImGui.TextUnformatted($"Has Desynthables: {PurificationManager.CanPurifyAny()}");
    }
}
