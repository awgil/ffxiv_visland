using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;

namespace visland.Workshop;

public class WorkshopManual
{
    private List<uint> _recents = [];
    private string _filter = "";

    public void Draw()
    {
        ImGui.InputText("Filter", ref _filter, 256);
        var sheetCraft = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>()!;
        foreach (var row in sheetCraft)
        {
            var name = row.Item.Value.Name.ToString() ?? "";
            if (name.Length == 0 || !name.Contains(_filter, StringComparison.InvariantCultureIgnoreCase))
                continue;
            DrawRowCraft(row, false);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Recent items:");
        foreach (var i in _recents.ToArray()) // copy, since we might modify it...
        {
            DrawRowCraft(sheetCraft.GetRow(i)!, true);
        }
    }

    private void DrawRowCraft(MJICraftworksObject row, bool fromRecent)
    {
        var name = row.Item.Value.Name.ToString() ?? "???";
        ImGui.PushID((int)row.RowId * 2 + (fromRecent ? 1 : 0));
        if (ImGui.Button("+1"))
            AddToSchedule(row, 1);
        ImGui.SameLine();
        if (ImGui.Button("+2"))
            AddToSchedule(row, 2);
        ImGui.SameLine();
        if (ImGui.Button("+3"))
            AddToSchedule(row, 4);
        ImGui.SameLine();
        if (ImGui.Button("+4"))
            AddToSchedule(row, 8);
        ImGui.SameLine();
        if (ImGui.Button("+123"))
            AddToSchedule(row, 7);
        ImGui.SameLine();
        if (ImGui.Button("+1234"))
            AddToSchedule(row, 15);
        ImGui.SameLine();
        ImGui.TextUnformatted(name);
        ImGui.PopID();
    }

    private void AddToSchedule(MJICraftworksObject row, int workshopIndices)
    {
        for (var i = 0; i < 4; ++i)
            if ((workshopIndices & 1 << i) != 0)
                AddToScheduleSingle(row, i);
        WorkshopUtils.ResetCurrentCycleToRefreshUI();
        _recents.Remove(row.RowId);
        _recents.Insert(0, row.RowId);
    }

    private unsafe void AddToScheduleSingle(MJICraftworksObject row, int workshopIndex)
    {
        var agentData = AgentMJICraftSchedule.Instance()->Data;
        var slotMask = (1u << row.CraftingTime) - 1;
        var startingCycle = 0;
        var maxCycle = 24 - row.CraftingTime;
        var usedMask = agentData->WorkshopSchedules[workshopIndex].UsedTimeSlots;
        while ((usedMask & slotMask << startingCycle) != 0 && startingCycle <= maxCycle)
            ++startingCycle;
        if (startingCycle > maxCycle)
        {
            ReportError($"No free spots in workshop {workshopIndex + 1}");
            return;
        }

        WorkshopUtils.ScheduleItemToWorkshop(row.RowId, startingCycle, agentData->CycleDisplayed, workshopIndex);
    }

    private void ReportError(string msg)
    {
        Service.Log.Error(msg);
        Service.ChatGui.PrintError(msg);
    }
}
