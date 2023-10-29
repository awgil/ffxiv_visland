using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using visland.Helpers;
using AtkValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace visland.Export;

unsafe class ExportWindow : UIAttachedWindow
{
    private ExportConfig _config;
    private ExportDebug _debug = new();
    private Throttle _exportThrottle = new(); // export seems to close & reopen window?..

    public ExportWindow() : base("Exports Automation", "MJIDisposeShop", new(400, 600))
    {
        _config = Service.Config.Get<ExportConfig>();
    }

    public override void PreOpenCheck()
    {
        base.PreOpenCheck();
        var agent = AgentMJIDisposeShop.Instance();
        IsOpen &= agent != null && agent->Data != null && agent->Data->DataInitialized;
    }

    public override void OnOpen()
    {
        if (_config.AutoSell)
        {
            _exportThrottle.Exec(AutoExport, 2);
        }
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

    private void DrawMain()
    {
        if (ImGui.Checkbox("Auto Export", ref _config.AutoSell))
            _config.NotifyModified();
        ImGui.PushItemWidth(150);
        if (ImGui.SliderInt("Sell normal above", ref _config.NormalLimit, 0, 999))
            _config.NotifyModified();
        if (ImGui.SliderInt("Sell granary above", ref _config.GranaryLimit, 0, 999))
            _config.NotifyModified();
        if (ImGui.SliderInt("Sell farm above", ref _config.FarmLimit, 0, 999))
            _config.NotifyModified();
        if (ImGui.SliderInt("Sell pasture above", ref _config.PastureLimit, 0, 999))
            _config.NotifyModified();
        ImGui.PopItemWidth();

        if (ImGui.Button("Sell everything above configured limits"))
            AutoExport();
    }

    private void AutoExport()
    {
        try
        {
            var data = AgentMJIDisposeShop.Instance()->Data;
            int seafarerCowries = data->CurrencyCount[0], islanderCowries = data->CurrencyCount[1];
            AutoExportCategory(0, _config.NormalLimit, ref seafarerCowries, ref islanderCowries);
            AutoExportCategory(1, _config.GranaryLimit, ref seafarerCowries, ref islanderCowries);
            AutoExportCategory(2, _config.FarmLimit, ref seafarerCowries, ref islanderCowries);
            AutoExportCategory(3, _config.PastureLimit, ref seafarerCowries, ref islanderCowries);
        }
        catch (Exception ex)
        {
            Service.Log.Error($"Error: {ex}");
            Service.ChatGui.PrintError($"Auto export error: {ex.Message}");
        }
    }

    private void AutoExportCategory(int category, int limit, ref int seafarerCowries, ref int islanderCowries)
    {
        if (limit >= 999)
            return;
        var agent = AgentMJIDisposeShop.Instance();
        var data = agent->Data;
        List<AtkValue> args = new()
        {
            new() { Type = AtkValueType.UInt },
            new() { Type = AtkValueType.UInt, Int = limit }
        };
        int numItems = 0;
        foreach (var item in data->PerCategoryItemsSpan[category].Span)
        {
            var count = Utils.NumItems(item.Value->ItemId);
            if (count <= limit)
                continue;

            var export = count - limit;
            var value = item.Value->CowriesPerItem * export;
            if (item.Value->UseIslanderCowries)
            {
                islanderCowries += value;
                if (islanderCowries > data->CurrencyStackSize[1])
                    throw new Exception($"Islander cowries would overcap");
            }
            else
            {
                seafarerCowries += value;
                if (seafarerCowries > data->CurrencyStackSize[0])
                    throw new Exception($"Seafarer cowries would overcap");
            }

            args.Add(new() { Type = AtkValueType.UInt, UInt = item.Value->ShopItemRowId });
            args.Add(new() { Type = AtkValueType.UInt, Int = export });
            if (++numItems > 64)
                throw new Exception($"Too many items to export, please report this as a bug!");
        }
        var argsSpan = CollectionsMarshal.AsSpan(args);
        argsSpan[0].Int = numItems;

        Service.Log.Info($"Exporting {numItems} items above {limit} limit...");
        var listener = *(AgentInterface**)((nint)agent + 0x18);
        AtkEvent result = new();
        listener->VTable->ReceiveEvent(listener, &result, SpanExtensions.GetPointer(argsSpan, 0), (uint)args.Count, 0);
    }
}
