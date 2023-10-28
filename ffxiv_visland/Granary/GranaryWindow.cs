using ImGuiNET;
using visland.Helpers;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.Game;
using System;
using System.Collections.Generic;

namespace visland.Granary;

unsafe class GranaryWindow : UIAttachedWindow
{
    private GranaryConfig _config;
    private GranaryState _granary = new();
    private GranaryDebug _debug;

    public GranaryWindow() : base("Granary Automation", "MJIGatheringHouse", new(400, 600))
    {
        _config = Service.Config.Get<GranaryConfig>();
        _debug = new(_granary);
    }

    public override void PreOpenCheck()
    {
        base.PreOpenCheck();
        IsOpen &= _granary.Agent != null && _granary.Agent->Data != null && _granary.Agent->Data->Initialized != 0;
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
        if (_config.Reassign != GranaryConfig.UpdateStrategy.Manual && _granary.Agent->Data != null)
        {
            uint reassignMask = 0;
            for (int i = 0; i < 2; ++i)
                if (TryAutoCollect(i) && _granary.GetGranaryState(i)->RemainingDays < 7)
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

        var data = _granary.Agent != null ? _granary.Agent->Data : null;
        if (data == null)
            return;

        GranaryState.CollectResult[] collectStates = [_granary.CalculateGranaryCollectionState(0), _granary.CalculateGranaryCollectionState(1)];

        ImGui.Separator();
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
                using (ImRaii.Disabled(collectStates[i] is GranaryState.CollectResult.NothingToCollect or GranaryState.CollectResult.EverythingCapped))
                    if (ImGui.Button($"Collect##{i}"))
                        _granary.Collect(i);
            }

            foreach (var e in data->Expeditions.Span)
            {
                if (!_granary.IsExpeditionUnlocked(e.ExpeditionId))
                    continue;

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{e.Name} ({InventoryManager.Instance()->GetInventoryItemCount(e.RareItemId)}/999)");

                for (int i = 0; i < 2; ++i)
                {
                    ImGui.TableNextColumn();
                    var curDest = _granary.GetGranaryState(i)->ActiveExpeditionId;
                    var curDays = _granary.GetGranaryState(i)->RemainingDays;
                    var maxDays = (byte)Math.Min(7, curDays + _granary.MaxDays());
                    using (ImRaii.Disabled(collectStates[i] != GranaryState.CollectResult.NothingToCollect || curDest == e.ExpeditionId && curDays == maxDays))
                        if (ImGui.Button($"{(curDest == e.ExpeditionId ? "Max" : "Reassign")}##{i}_{e.ExpeditionId}"))
                            _granary.SelectExpedition((byte)i, e.ExpeditionId, maxDays);
                }
            }
        }
    }

    private bool TryAutoCollect(int i)
    {
        switch (_granary.CalculateGranaryCollectionState(i))
        {
            case GranaryState.CollectResult.NothingToCollect:
                return true;
            case GranaryState.CollectResult.CanCollectSafely:
                if (_config.Collect != CollectStrategy.Manual)
                {
                    _granary.Collect(i);
                    return true;
                }
                break;
            case GranaryState.CollectResult.CanCollectWithOvercap:
                if (_config.Collect == CollectStrategy.FullAuto)
                {
                    _granary.Collect(i);
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
            if (_granary.CalculateGranaryCollectionState(i) == GranaryState.CollectResult.NothingToCollect)
                reassignMask |= 1u << i;
        ReassignImpl(reassignMask);
    }

    private void ReassignImpl(uint allowedMask)
    {
        byte[] currentDestinations = [_granary.GetGranaryState(0)->ActiveExpeditionId, _granary.GetGranaryState(1)->ActiveExpeditionId];
        byte[] newDestinations = [currentDestinations[0], currentDestinations[1]];
        if (_config.Reassign is GranaryConfig.UpdateStrategy.BestDifferent or GranaryConfig.UpdateStrategy.BestSame)
        {
            List<(byte id, int count)> destinations = new();
            foreach (var e in _granary.Agent->Data->Expeditions.Span)
                if (_granary.IsExpeditionUnlocked(e.ExpeditionId))
                    destinations.Add((e.ExpeditionId, InventoryManager.Instance()->GetInventoryItemCount(e.RareItemId)));
            destinations.SortBy(e => e.count);

            if (destinations.Count > 0)
            {
                newDestinations[0] = destinations[0].id;
                newDestinations[1] = destinations.Count > 1 && _config.Reassign == GranaryConfig.UpdateStrategy.BestDifferent ? destinations[1].id : destinations[0].id;
                if (newDestinations[0] == currentDestinations[1] || newDestinations[1] == currentDestinations[0])
                    Utils.Swap(ref newDestinations[0], ref newDestinations[1]); // don't reassign needlessly
            }
        }

        var max = _granary.MaxDays();
        for (int i = 0; i < 2; ++i)
        {
            if ((allowedMask & (1u << i)) == 0)
                continue; // this granary can't be reassigned
            var curDays = _granary.GetGranaryState(i)->RemainingDays;
            var newDays = (byte)Math.Min(7, curDays + max);
            if (currentDestinations[i] == newDestinations[i] && curDays == newDays)
                continue; // this is the best already
            _granary.SelectExpedition((byte)i, newDestinations[i], newDays);
        }
    }
}
