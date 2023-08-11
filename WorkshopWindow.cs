using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace visland;

unsafe class WorkshopWindow : Window, IDisposable
{
    delegate nint ReceiveEventDelegate(AtkEventListener* eventListener, ushort evt, uint which, void* eventData, void* inputData);

    private List<Func<bool>> _pendingActions = new();
    private List<uint> _rowListIndices = new();
    private List<uint> _recents = new();
    private string _filter = "";
    private int _activeWait = -1;
    private DateTime _activeWaitStart;

    public WorkshopWindow(Plugin plugin) : base("Workshop automation")
    {
        ShowCloseButton = false; // opened/closed automatically
        Size = new Vector2(500, 650);
        SizeCondition = ImGuiCond.FirstUseEver;
        PositionCondition = ImGuiCond.Always; // updated every frame

        List<uint> r4 = new(), r6 = new(), r8 = new();
        foreach (var row in Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>()!)
        {
            List<uint>? rows = row.CraftingTime switch
            {
                4 => r4,
                6 => r6,
                8 => r8,
                _ => null
            };
            rows?.Add(row.RowId);
        }
        _rowListIndices.Add(0);
        _rowListIndices.AddRange(r4);
        _rowListIndices.Add(0);
        _rowListIndices.AddRange(r6);
        _rowListIndices.Add(0);
        _rowListIndices.AddRange(r8);
    }

    public void Dispose()
    {
    }

    public override void PreOpenCheck()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("MJICraftSchedule");
        IsOpen = addon != null && addon->IsVisible;
        if (IsOpen)
        {
            Position = new Vector2(addon->X + addon->GetScaledWidth(true), addon->Y);
        }

        // execute next pending action, if any
        if (_pendingActions.Count > 0 && _pendingActions[0]())
            _pendingActions.RemoveAt(0);
    }

    public override void Draw()
    {
        ImGui.InputText("Filter", ref _filter, 256);
        var sheetCraft = Service.LuminaGameData.GetExcelSheet<MJICraftworksObject>()!;
        foreach (var row in sheetCraft)
        {
            var name = row.Item.Value?.Name.ToString() ?? "";
            if (name.Length == 0 || !name.Contains(_filter, StringComparison.InvariantCultureIgnoreCase))
                continue;
            DrawRow(row, false);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Recent items:");
        foreach (var i in _recents.ToArray()) // copy, since we might modify it...
        {
            DrawRow(sheetCraft.GetRow(i)!, true);
        }
    }

    private void DrawRow(MJICraftworksObject row, bool fromRecent)
    {
        var name = row.Item.Value?.Name.ToString() ?? "???";
        ImGui.PushID((int)row.RowId * 2 + (fromRecent ? 1 : 0));
        if (ImGui.Button("+1"))
        {
            AddToSchedule(row, 0);
        }
        ImGui.SameLine();
        if (ImGui.Button("+2"))
        {
            AddToSchedule(row, 1);
        }
        ImGui.SameLine();
        if (ImGui.Button("+3"))
        {
            AddToSchedule(row, 2);
        }
        ImGui.SameLine();
        if (ImGui.Button("+4"))
        {
            AddToSchedule(row, 3);
        }
        ImGui.SameLine();
        if (ImGui.Button("+123"))
        {
            AddToSchedule(row, 0);
            AddToSchedule(row, 1);
            AddToSchedule(row, 2);
        }
        ImGui.SameLine();
        if (ImGui.Button("+1234"))
        {
            AddToSchedule(row, 0);
            AddToSchedule(row, 1);
            AddToSchedule(row, 2);
            AddToSchedule(row, 3);
        }
        ImGui.SameLine();
        ImGui.TextUnformatted(name);
        ImGui.PopID();
    }

    private void AddToSchedule(MJICraftworksObject row, int workshopIndex)
    {
        _pendingActions.Add(() => OpenAddWorkshopSchedule(workshopIndex));
        _pendingActions.Add(() => IsAddonVisible("MJICraftScheduleSetting"));
        _pendingActions.Add(() => Wait(1, 0));
        _pendingActions.Add(() => EnsureSortedByTime());
        _pendingActions.Add(() => SelectCraft(row));
        _pendingActions.Add(() => Wait(3, 0));
        _pendingActions.Add(() => ConfirmCraft());
        _pendingActions.Add(() => !IsAddonVisible("MJICraftScheduleSetting"));
        _pendingActions.Add(() => Wait(2, 0));
        _recents.Remove(row.RowId);
        _recents.Insert(0, row.RowId);
    }

    private AtkComponentButton* FindAddButton(int workshopIndex)
    {
        uint id = workshopIndex switch
        {
            0 => 8,
            1 => 80001,
            2 => 80002,
            3 => 80003,
            _ => 0
        };
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("MJICraftSchedule");
        return addon != null && id != 0 ? addon->GetButtonNodeById(id) : null;
    }

    private bool Wait(int id, float delay)
    {
        var now = DateTime.Now;
        if (_activeWait != id)
        {
            _activeWait = id;
            _activeWaitStart = now;
        }
        var passed = (now - _activeWaitStart).TotalSeconds;
        PluginLog.Log($"Wait #{id}: {passed:f3}/{delay:f3}");
        return passed > delay;
    }

    private bool IsAddonVisible(string name)
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName(name);
        bool visible = addon != null ? addon->IsVisible && addon->UldManager.LoadedState == AtkLoadState.Loaded : false;
        PluginLog.Log($"Addon visible check {name} = {visible}");
        return visible;
    }

    private bool OpenAddWorkshopSchedule(int workshopIndex)
    {
        PluginLog.Log($"Open workshop {workshopIndex} schedule");
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("MJICraftSchedule");
        var eventData = stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var inputData = stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var receiveEvent = Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>((nint)addon->AtkEventListener.vfunc[2])!;
        receiveEvent(&addon->AtkEventListener, 25, 6 + (uint)workshopIndex, eventData, inputData);
        return true;
    }

    private bool EnsureSortedByTime()
    {
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("MJICraftScheduleSetting");
        var eventData = stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var inputData = stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }; // [4] = category
        var receiveEvent = Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>((nint)addon->AtkEventListener.vfunc[2])!;
        receiveEvent(&addon->AtkEventListener, 35, 7, eventData, inputData);
        return true;

    }

    private bool SelectCraft(MJICraftworksObject row)
    {
        PluginLog.Log($"Select craft #{row.RowId} '{row.Item.Value?.Name}'");
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("MJICraftScheduleSetting");
        var eventData = stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var inputData = stackalloc int[] { 0, 0, 0, 0, _rowListIndices.IndexOf(row.RowId), 0, 0, 0, 0, 0 };
        var receiveEvent = Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>((nint)addon->AtkEventListener.vfunc[2])!;
        receiveEvent(&addon->AtkEventListener, 35, 1, eventData, inputData);
        return true;
    }

    private bool ConfirmCraft()
    {
        PluginLog.Log($"Confirm craft");
        var addon = (AtkUnitBase*)Service.GameGui.GetAddonByName("MJICraftScheduleSetting");
        var eventData = stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var inputData = stackalloc int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        var receiveEvent = Marshal.GetDelegateForFunctionPointer<ReceiveEventDelegate>((nint)addon->AtkEventListener.vfunc[2])!;
        receiveEvent(&addon->AtkEventListener, 25, 6, eventData, inputData);
        return true;
    }
}
