using ImGuiNET;
using visland.Helpers;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace visland.Granary;

unsafe class GranaryWindow : UIAttachedWindow
{
    private GranaryConfig _config;
    private GranaryDebug _debug;

    public GranaryWindow() : base("Granary Automation", "MJIGatheringHouse", new(400, 600))
    {
        _config = Service.Config.Get<GranaryConfig>();
        _debug = new();
    }

    public override void PreOpenCheck()
    {
        base.PreOpenCheck();
        var agent = AgentMJIGatheringHouse.Instance();
        IsOpen &= agent != null && agent->Data != null && agent->Data->Initialized;
    }

    public override void Draw()
    {
        using var tabs = ImRaii.TabBar("Tabs");
        if (tabs)
        {
            using (var tab = ImRaii.TabItem("Main"))
                if (tab)
                    DrawMain();
            using (var tab = ImRaii.TabItem("Debug"))
                if (tab)
                    _debug.Draw();
        }
    }

    public override void OnOpen()
    {
        if (_config.Reassign != GranaryConfig.UpdateStrategy.Manual)
        {
            uint reassignMask = 0;
            for (int i = 0; i < 2; ++i)
                if (TryAutoCollect(i) && GranaryUtils.GetGranaryState(i)->RemainingDays < 7)
                    reassignMask |= 1u << i;

            if (reassignMask != 0)
                ReassignImpl(reassignMask);
        }
    }

    private unsafe void DrawMain()
    {
        if (UICombo.Enum("Auto Collect", ref _config.Collect))
            _config.NotifyModified();
        if (UICombo.Enum("Auto Reassign", ref _config.Reassign))
            _config.NotifyModified();
        if (ImGui.Button("Apply!"))
            ForceReassign();

        ImGui.Separator();
        DrawTable();
    }

    private void DrawTable()
    {
        CollectResult[] collectStates = [GranaryUtils.CalculateGranaryCollectionState(0), GranaryUtils.CalculateGranaryCollectionState(1)];

        using var table = ImRaii.Table("table", 3);
        if (table)
        {
            ImGui.TableSetupColumn("Expedition");
            ImGui.TableSetupColumn("Granary 1", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableSetupColumn("Granary 2", ImGuiTableColumnFlags.WidthFixed, 100);
            ImGui.TableHeadersRow();

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            for (int i = 0; i < 2; ++i)
            {
                ImGui.TableNextColumn();
                using (ImRaii.Disabled(collectStates[i] is CollectResult.NothingToCollect or CollectResult.EverythingCapped))
                    if (ImGui.Button($"Collect##{i}"))
                        GranaryUtils.Collect(i);
            }

            var agent = AgentMJIGatheringHouse.Instance();
            for (var e = agent->Data->Expeditions.First; e != agent->Data->Expeditions.Last; ++e)
            {
                if (!agent->IsExpeditionUnlocked(e))
                    continue;

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{e->Name} ({Utils.NumItems(e->RareItemId)}/999)");

                for (int i = 0; i < 2; ++i)
                {
                    ImGui.TableNextColumn();
                    var curDest = GranaryUtils.GetGranaryState(i)->ActiveExpeditionId;
                    var curDays = GranaryUtils.GetGranaryState(i)->RemainingDays;
                    var maxDays = (byte)Math.Min(7, curDays + GranaryUtils.MaxDays());
                    using (ImRaii.Disabled(collectStates[i] != CollectResult.NothingToCollect || curDest == e->ExpeditionId && curDays == maxDays))
                        if (ImGui.Button($"{(curDest == e->ExpeditionId ? "Max" : "Reassign")}##{i}_{e->ExpeditionId}"))
                            GranaryUtils.SelectExpedition((byte)i, e->ExpeditionId, maxDays);
                }
            }
        }
    }

    private bool TryAutoCollect(int i)
    {
        switch (GranaryUtils.CalculateGranaryCollectionState(i))
        {
            case CollectResult.NothingToCollect:
                return true;
            case CollectResult.CanCollectSafely:
                if (_config.Collect != CollectStrategy.Manual)
                {
                    GranaryUtils.Collect(i);
                    return true;
                }
                break;
            case CollectResult.CanCollectWithOvercap:
                if (_config.Collect == CollectStrategy.FullAuto)
                {
                    GranaryUtils.Collect(i);
                    return true;
                }
                break;
        }
        return false;
    }

    private void ForceReassign()
    {
        uint reassignMask = 0;
        for (int i = 0; i < 2; ++i)
            if (GranaryUtils.CalculateGranaryCollectionState(i) == CollectResult.NothingToCollect)
                reassignMask |= 1u << i;
        ReassignImpl(reassignMask);
    }

    private void ReassignImpl(uint allowedMask)
    {
        byte[] currentDestinations = [GranaryUtils.GetGranaryState(0)->ActiveExpeditionId, GranaryUtils.GetGranaryState(1)->ActiveExpeditionId];
        byte[] newDestinations = [currentDestinations[0], currentDestinations[1]];
        var agent = AgentMJIGatheringHouse.Instance();
        if (_config.Reassign is GranaryConfig.UpdateStrategy.BestDifferent or GranaryConfig.UpdateStrategy.BestSame)
        {
            List<(byte id, int count)> destinations = [];
            for (var e = agent->Data->Expeditions.First; e != agent->Data->Expeditions.Last; ++e)
                if (agent->IsExpeditionUnlocked(e))
                    destinations.Add((e->ExpeditionId, Utils.NumItems(e->RareItemId)));
            destinations.SortBy(e => e.count);

            if (destinations.Count > 0)
            {
                newDestinations[0] = destinations[0].id;
                newDestinations[1] = destinations.Count > 1 && _config.Reassign == GranaryConfig.UpdateStrategy.BestDifferent ? destinations[1].id : destinations[0].id;
                if (newDestinations[0] == currentDestinations[1] || newDestinations[1] == currentDestinations[0])
                    Utils.Swap(ref newDestinations[0], ref newDestinations[1]); // don't reassign needlessly
            }
        }

        var max = GranaryUtils.MaxDays();
        for (int i = 0; i < 2; ++i)
        {
            if ((allowedMask & (1u << i)) == 0)
                continue; // this granary can't be reassigned
            var curDays = GranaryUtils.GetGranaryState(i)->RemainingDays;
            var newDays = (byte)Math.Min(7, curDays + max);
            if (currentDestinations[i] == newDestinations[i] && curDays == newDays)
                continue; // this is the best already
            GranaryUtils.SelectExpedition((byte)i, newDestinations[i], newDays);
        }
    }
}
