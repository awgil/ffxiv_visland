using ImGuiNET;
using Lumina.Data;
using Lumina.Excel.GeneratedSheets;
using System.Linq;

namespace visland;

public unsafe class WorkshopDebug
{
    private WorkshopSchedule _sched = new();
    private WorkshopOCImport _oc = new();

    public void Draw()
    {
        if (ImGui.Button("Clear"))
            _sched.ClearCurrentCycleSchedule();

        var ad = _sched.AgentData;
        var sheet = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>(Language.English)!;
        if (ad != null)
        {
            ImGui.TextUnformatted($"init={ad->InitState}, cur-cycle={ad->CurrentCycle}, cur-rank={ad->CurrentIslandRank}");
            ImGui.TextUnformatted($"setting addon={ad->SettingAddonId}, ws={ad->CurScheduleSettingWorkshop}, slot={ad->CurScheduleSettingStartingSlot}, item=#{ad->CurScheduleSettingObjectIndex}, numMats={ad->CurScheduleSettingNumMaterials}");
            ImGui.TextUnformatted($"rest mask={ad->RestCycles:X}, in-progress={ad->CycleInProgress}");
            int i = 0;
            foreach (ref var w in ad->Workshops)
            {
                ImGui.TextUnformatted($"Workshop {i++}: {w.NumScheduleEntries} entries, {w.UsedTimeSlots:X} used");
                ImGui.Indent();
                for (int j = 0; j < w.NumScheduleEntries; ++j)
                {
                    ref var e = ref w.Entries[j];
                    ImGui.TextUnformatted($"Item {j}: {e.CraftObjectId} ({sheet.GetRow(e.CraftObjectId)?.Item.Value?.Name}), u2={e.u2}, u4={e.u4:X}, startslot={e.StartingSlot}, dur={e.Duration}, started={e.Started != 0}, efficient={e.Efficient != 0}");
                }
                ImGui.Unindent();
            }

            ImGui.TextUnformatted("Items:");
            ImGui.Indent();
            i = 0;
            foreach (var item in ad->Items.Span)
            {
                ImGui.TextUnformatted($"Item {i++}: id={item.ObjectId} ({sheet.GetRow(item.ObjectId)?.Item.Value?.Name})");
            }
            ImGui.Unindent();
        }
    }
}
