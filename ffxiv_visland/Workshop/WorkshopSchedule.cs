using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using AtkValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace visland;

[StructLayout(LayoutKind.Explicit, Size = 0x40)]
public unsafe partial struct AgentMJICraftSchedule
{
    [StructLayout(LayoutKind.Explicit, Size = 0x98)]
    public unsafe partial struct ItemData
    {
        [FieldOffset(0x10)] public fixed ushort Materials[4];
        [FieldOffset(0x20)] public ushort ObjectId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xC)]
    public unsafe partial struct EntryData
    {
        [FieldOffset(0x0)] public ushort CraftObjectId;
        [FieldOffset(0x2)] public ushort u2;
        [FieldOffset(0x4)] public uint u4;
        [FieldOffset(0x8)] public byte StartingSlot;
        [FieldOffset(0x9)] public byte Duration;
        [FieldOffset(0xA)] public byte Started;
        [FieldOffset(0xB)] public byte Efficient;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x54)]
    public unsafe partial struct WorkshopData
    {
        [FieldOffset(0x00)] public byte NumScheduleEntries;
        [FieldOffset(0x08)] public fixed byte EntryData[6 * 0xC];
        [FieldOffset(0x50)] public uint UsedTimeSlots;

        public Span<EntryData> Entries => new(Unsafe.AsPointer(ref EntryData[0]), 6);
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xB60)]
    public unsafe partial struct AgentData
    {
        [FieldOffset(0x000)] public int InitState;
        [FieldOffset(0x004)] public int SettingAddonId;
        [FieldOffset(0x0D0)] public StdVector<ItemData> Items;
        [FieldOffset(0x400)] public fixed byte WorkshopData[4 * 0x54];
        [FieldOffset(0x5A8)] public uint CurScheduleSettingObjectIndex;
        [FieldOffset(0x5AC)] public int CurScheduleSettingWorkshop;
        [FieldOffset(0x5B0)] public int CurScheduleSettingStartingSlot;
        [FieldOffset(0x7E8)] public byte CurScheduleSettingNumMaterials;
        [FieldOffset(0x810)] public uint RestCycles;
        [FieldOffset(0x814)] public uint NewRestCycles;
        [FieldOffset(0xB58)] public byte CurrentCycle; // currently viewed
        [FieldOffset(0xB59)] public byte CycleInProgress;

        public Span<WorkshopData> Workshops => new(Unsafe.AsPointer(ref WorkshopData[0]), 4);
    }

    [FieldOffset(0)] public AgentInterface AgentInterface;
    [FieldOffset(0x28)] public AgentData* Data;
}

public unsafe class WorkshopSchedule
{
    public AgentMJICraftSchedule* Agent;

    private delegate void RequestDemandFullDelegate(MJIManager* self);
    private RequestDemandFullDelegate _requestDemandFull;

    // 'startingHour' is (slot + 17) % 24, where slot 0 is first hour of the cycle
    private delegate void ScheduleCraftDelegate(MJIManager* self, ushort craftObjectId, byte startingHour, byte cycle, byte workshop);
    private ScheduleCraftDelegate _scheduleCraft;

    private delegate void SetCurrentCycleDelegate(AgentMJICraftSchedule* self, int cycle);
    private SetCurrentCycleDelegate _setCurrentCycle;

    public AgentMJICraftSchedule.AgentData* AgentData => Agent != null ? Agent->Data : null;
    public int CurrentCycle => AgentData != null ? AgentData->CurrentCycle : 0;
    public int CycleInProgress => AgentData != null ? AgentData->CycleInProgress : 0;
    public uint RestCycles => AgentData != null ? AgentData->RestCycles : 0;

    public WorkshopSchedule()
    {
        Agent = (AgentMJICraftSchedule*)AgentModule.Instance()->GetAgentByInternalId(AgentId.MJICraftSchedule);
        _requestDemandFull = Marshal.GetDelegateForFunctionPointer<RequestDemandFullDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B CD E8 ?? ?? ?? ?? 32 C0"));
        _scheduleCraft = Marshal.GetDelegateForFunctionPointer<ScheduleCraftDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 46 28 41 8D 4E FF"));
        _setCurrentCycle = Marshal.GetDelegateForFunctionPointer<SetCurrentCycleDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 48 8B 45 28 48 8B 7C 24"));
    }

    public bool CurrentCycleIsEmpty()
    {
        if (AgentData == null)
            return false;
        foreach (ref var w in AgentData->Workshops)
            if (w.NumScheduleEntries != 0)
                return false;
        return true;
    }

    public void ClearCurrentCycleSchedule()
    {
        SynthesizeEvent(6, new AtkValue[] { new() { Type = AtkValueType.Int, Int = 0 } });
    }

    public void ScheduleItemToWorkshop(uint objId, int startingHour, int cycle, int workshop)
    {
        Service.Log.Info($"Adding schedule: {objId} @ {startingHour}/{cycle}/{workshop}");
        _scheduleCraft(MJIManager.Instance(), (ushort)objId, (byte)((startingHour + 17) % 24), (byte)cycle, (byte)workshop);
    }

    public void SetCurrentCycle(int cycle)
    {
        Service.Log.Info($"Setting cycle: {cycle}");
        _setCurrentCycle(Agent, cycle);
    }

    public void SetRestCycles(uint mask)
    {
        Service.Log.Info($"Setting rest: {mask:X}");
        AgentData->NewRestCycles = mask;
        SynthesizeEvent(5, new AtkValue[] { new() { Type = AtkValueType.Int, Int = 0 } });
    }

    public void RequestDemand()
    {
        Service.Log.Info("Fetching demand");
        _requestDemandFull(MJIManager.Instance());
    }

    public static unsafe int GetMaxWorkshops()
    {
        var mji = MJIManager.Instance();
        return mji == null ? 0 : mji->IslandState.CurrentRank switch
        {
            < 3 => 0,
            < 6 => 1,
            < 8 => 2,
            < 14 => 3,
            _ => 4,
        };
    }

    private void SynthesizeEvent(ulong eventKind, Span<AtkValue> args)
    {
        AtkValue res = new();
        Agent->AgentInterface.ReceiveEvent(&res, args.GetPointer(0), (uint)args.Length, eventKind);
    }
}
