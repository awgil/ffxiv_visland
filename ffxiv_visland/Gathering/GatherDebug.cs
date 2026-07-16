using Dalamud.Game.Text;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using System.Linq;
using visland.Helpers;

namespace visland.Gathering;

public unsafe class GatherDebug(GatherRouteExec exec) {
    private readonly GatherRouteExec exec = exec;

    public void Draw() {
        using var child = ImRaii.Child("child");
        if (!child) return;
        if (!Player.Available) return;

        if (exec.RouteDB.AutoRetainerIntegration) {
            Utils.DrawSection("AutoRetainer Integration", ImGuiColors.ParsedGold);
            Utils.DrawSection($"Conditions to Begin", ImGuiColors.ParsedGold, drawSeparator: false);
            ImGui.Text($"One of:");
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.Text($"Subs Ready: {Service.Retainers.HasSubsReady}");
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.Text($"Retainers Ready: {Service.Retainers.HasRetainersReady}");
            ImGui.Text($"Preferred Character == Current Character: {Service.Retainers.GetPreferredCharacter() == Player.CID}");
            Utils.DrawSection($"Conditions to End", ImGuiColors.ParsedGold, drawSeparator: false);
            ImGui.Text($"Route paused: {exec.Paused}");
            ImGui.Text($"Retainers finished: {Service.Retainers.Finished}");
            ImGui.Text($"All of the below:");
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.Text($"MultiMode Enabled: {Service.AutoRetainer.GetMultiEnabled()}");
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.Text($"Not Busy: {!Service.AutoRetainer.IsBusy()}");
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.Text($"Current Character == Starting Character: {Player.CID == Service.Retainers.StartingCharacter}");
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.Text($"No Retainers Ready: {!Service.Retainers.HasRetainersReady}");
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.Text($"No Subs Ready: {!Service.Retainers.HasSubsReady}");
            ImGui.Text($"Preferred Character == Current Character: {Service.Retainers.GetPreferredCharacter() == Player.CID}");
        }

        if (Service.TargetManager.Target != null) {
            Utils.DrawSection("Target", ImGuiColors.ParsedGold);
            var t = Service.TargetManager.Target;
            if (GatheringPoint.GetRow(t.BaseId) is { } gp) {
                ImGui.Text($"IsNode: {gp}");
                if (gp.GatheringPointBase.IsValid)
                    ImGui.Text($"GatheringType: {gp.GatheringPointBase.Value.GatheringType.RowId}");
            }
            else
                ImGui.Text($"Not a GatheringPoint (BaseId={t.BaseId})");
        }
        if (exec.CurrentRoute is { TargetGatherItem: not 0 } && Item.GetRow((uint)exec.CurrentRoute.TargetGatherItem) is { } item) {
            Utils.DrawSection("Target Item", ImGuiColors.ParsedGold);
            var wp = exec.CurrentRoute.Waypoints[exec.CurrentWaypoint];
            ImGui.Text($"[{exec.CurrentRoute.TargetGatherItem}] {item.Name}");
            ImGui.Text($"Waypoint: IsNode: {wp.IsNode} Type: {wp.GatheringType} NodeJob: {wp.NodeJob}");
        }
        if (exec.GatheringAM != null) {
            Utils.DrawSection("Gathering Addon", ImGuiColors.ParsedGold);
            ImGui.Text($"Integrity: {exec.GatheringAM.CurrentIntegrity}/{exec.GatheringAM.TotalIntegrity}");
            foreach (var gatherable in exec.GatheringAM.Items.Where(x => x.IsEnabled)) {
                ImGui.TextV($@"[{gatherable.ItemID}] Lv{gatherable.ItemLevel} {gatherable.GatherChance}% {gatherable.ItemName} {(gatherable.IsCollectable ? SeIconChar.Collectible : string.Empty)}");
                ImGui.SameLine();
                if (ImGui.IconButton(Dalamud.Interface.FontAwesomeIcon.BoreHole, $"###{gatherable.ItemID}"))
                    gatherable.Gather();
            }
        }
        if (exec.GatheredItem != null) {
            Utils.DrawSection("Gathered Item", ImGuiColors.ParsedGold);
            ImGui.Text($"[{exec.GatheredItem.ItemID}] {exec.GatheredItem.ItemName} {(exec.GatheredItem.IsCollectable ? SeIconChar.Collectible : string.Empty)}");
        }
        if (exec.GatheringCollectableAM != null) {
            Utils.DrawSection("Gathering Collectable Addon", ImGuiColors.ParsedGold);
            ImGui.Text($"Item: [{exec.GatheringCollectableAM.ItemID}] {exec.GatheringCollectableAM.ItemName}");
            ImGui.Text($"Integrity: {exec.GatheringCollectableAM.CurrentIntegrity}/{exec.GatheringCollectableAM.TotalIntegrity}");
            ImGui.Text($"Collectability: {exec.GatheringCollectableAM.CurrentCollectability}/{exec.GatheringCollectableAM.MaxCollectability}");
            ImGui.Text($"Scour: {exec.GatheringCollectableAM.ScourPower} Brazen: {exec.GatheringCollectableAM.BrazenPowerMin}/{exec.GatheringCollectableAM.BrazenPowerMax} Meticulous: {exec.GatheringCollectableAM.MeticulousPower}");
        }
    }
}
