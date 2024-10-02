using Dalamud.Game.Text;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.DalamudServices;
using ECommons.ImGuiMethods;
using ExdSheets.Sheets;
using ImGuiNET;
using System.Linq;
using visland.Helpers;

namespace visland.Gathering;
public unsafe class GatherDebug(GatherRouteExec exec)
{
    private readonly UITree _tree = new();
    private GatherRouteExec exec = exec;

    public void Draw()
    {
        using var child = ImRaii.Child("child");
        if (!child) return;
        if (!Player.Available) return;

        if (Svc.Targets.Target != null)
        {
            Utils.DrawSection("Target", ImGuiColors.ParsedGold);
            var t = Svc.Targets.Target;
            ImGuiEx.Text($"IsNode: {Utils.GetSheet<GatheringPoint>().HasRow(t.DataId)}");
            ImGuiEx.Text($"GatheringType: {Utils.GetRow<GatheringPoint>(t.DataId)!.Value.GatheringPointBase.Value.GatheringType.RowId}");
        }
        if (exec.CurrentRoute != null && exec.CurrentRoute.TargetGatherItem != default)
        {
            Utils.DrawSection("Target Item", ImGuiColors.ParsedGold);
            var item = Utils.GetRow<Item>((uint)exec.CurrentRoute.TargetGatherItem)!.Value;
            var wp = exec.CurrentRoute.Waypoints[exec.CurrentWaypoint];
            ImGuiEx.Text($"[{exec.CurrentRoute.TargetGatherItem}] {item.Name}");
            ImGuiEx.Text($"Waypoint: IsNode: {wp.IsNode} Type: {wp.GatheringType} NodeJob: {wp.NodeJob}");
        }
        if (exec.GatheringAM != null)
        {
            Utils.DrawSection("Gathering Addon", ImGuiColors.ParsedGold);
            ImGuiEx.Text($"Integrity: {exec.GatheringAM.CurrentIntegrity}/{exec.GatheringAM.TotalIntegrity}");
            foreach (var item in exec.GatheringAM.GatheredItems.Where(x => x.IsEnabled))
            {
                ImGuiEx.TextV($@"[{item.ItemID}] Lv{item.ItemLevel} {item.GatherChance}% {item.ItemName} {(item.IsCollectable ? SeIconChar.Collectible : string.Empty)}");
                ImGui.SameLine();
                if (ImGuiEx.IconButton(Dalamud.Interface.FontAwesomeIcon.BoreHole, $"###{item.ItemID}"))
                    item.Gather();
            }
        }
        if (exec.GatheredItem != null)
        {
            Utils.DrawSection("Gathered Item", ImGuiColors.ParsedGold);
            ImGuiEx.Text($"[{exec.GatheredItem.ItemID}] {exec.GatheredItem.ItemName} {(exec.GatheredItem.IsCollectable ? SeIconChar.Collectible : string.Empty)}");
        }
        if (exec.GatheringCollectableAM != null)
        {
            Utils.DrawSection("Gathering Collectable Addon", ImGuiColors.ParsedGold);
            ImGuiEx.Text($"Item: [{exec.GatheringCollectableAM.ItemID}] {exec.GatheringCollectableAM.ItemName}");
            ImGuiEx.Text($"Integrity: {exec.GatheringCollectableAM.CurrentIntegrity}/{exec.GatheringCollectableAM.TotalIntegrity}");
            ImGuiEx.Text($"Collectability: {exec.GatheringCollectableAM.CurrentCollectability}/{exec.GatheringCollectableAM.MaxCollectability}");
            ImGuiEx.Text($"Scour: {exec.GatheringCollectableAM.ScourPower} Brazen: {exec.GatheringCollectableAM.BrazenPowerMin}/{exec.GatheringCollectableAM.BrazenPowerMax} Meticulous: {exec.GatheringCollectableAM.MeticulousPower}");
        }
    }
}
