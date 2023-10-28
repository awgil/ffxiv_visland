using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.STD;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System.Runtime.InteropServices;

namespace visland.Granary;

[StructLayout(LayoutKind.Explicit, Size = 0x48)]
public unsafe partial struct MJIGranaryState
{
    public const int NumNormalResources = 20;

    [FieldOffset(0x00)] public byte ActiveExpeditionId; // MJIStockyardManagementArea
    [FieldOffset(0x01)] public byte RemainingDays;
    [FieldOffset(0x02)] public byte RareResourcePouchId; // MJIItemPouch
    [FieldOffset(0x04)] public short RareResourceCount;
    [FieldOffset(0x06)] public fixed byte NormalResourcePouchIds[NumNormalResources];
    [FieldOffset(0x1A)] public fixed short NormalResourceCounts[NumNormalResources];
    [FieldOffset(0x44)] public uint FinishTime; // unix timestamp
}

[StructLayout(LayoutKind.Explicit, Size = 0x98)]
public unsafe partial struct MJIGranariesState
{
    [FieldOffset(0x00)] public MJIGranaryState Granary1;
    [FieldOffset(0x48)] public MJIGranaryState Granary2;
    [FieldOffset(0x90)] public void* u90; // some connection to agent
}

[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct MJIManagerEx
{
    [FieldOffset(0x140)] public MJIGranariesState* Granaries;
}

[StructLayout(LayoutKind.Explicit, Size = 0x200)]
public unsafe partial struct AgentMJIGatheringHouse
{
    public enum Confirmation { None, Start, ChangeExtend, Change, Extend }

    [FieldOffset(0)] public AgentInterface AgentInterface;
    [FieldOffset(0x028)] public MJIManagerEx* Manager;
    [FieldOffset(0x030)] public MJIGranariesState* GranariesState;
    [FieldOffset(0x038)] public AgentData* Data;

    // substruct
    [FieldOffset(0x040)] public Utf8String ConfirmText;
    [FieldOffset(0x0A8)] public Utf8String FinishTimeText1; // array[2]
    [FieldOffset(0x110)] public Utf8String FinishTimeText2;

    [FieldOffset(0x178)] public int ConfirmAddonHandle;
    [FieldOffset(0x17C)] public byte CurGranaryIndex;
    [FieldOffset(0x188)] public Utf8String CurExpeditionName;
    [FieldOffset(0x1F0)] public byte CurActiveExpeditionId;
    [FieldOffset(0x1F1)] public byte CurProposedExpeditionId;
    [FieldOffset(0x1F2)] public byte CurHoveredExpeditionId;
    [FieldOffset(0x1F3)] public byte CurActiveDays;
    [FieldOffset(0x1F4)] public byte CurProposedDays;
    [FieldOffset(0x1F8)] public int SelectExpeditionAddonHandle;
    [FieldOffset(0x1FC)] public Confirmation ConfirmType;

    [StructLayout(LayoutKind.Explicit, Size = 0xB8)]
    public unsafe partial struct AgentData
    {
        [FieldOffset(0x00)] public AgentMJIGatheringHouse* Owner;
        // 0x08: sheets[2]
        [FieldOffset(0x38)] public StdVector<ExpeditionData> Expeditions;
        [FieldOffset(0x50)] public StdVector<ExpeditionDesc> ExpeditionDescs;
        [FieldOffset(0x68)] public StdVector<ExpeditionItem> ExpeditionItems;
        [FieldOffset(0x80)] public StdVector<Resource> Resources;
        [FieldOffset(0x98)] public StdVector<uint> ItemsPendingIconUpdate;
        [FieldOffset(0xB0)] public byte Initialized;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x120)]
    public unsafe partial struct ExpeditionData
    {
        [FieldOffset(0x000)] public byte ExpeditionId;
        [FieldOffset(0x008)] public Utf8String Name;
        [FieldOffset(0x070)] public fixed uint NormalItemIds[MJIGranaryState.NumNormalResources];
        [FieldOffset(0x0C0)] public fixed uint NormalIconIds[MJIGranaryState.NumNormalResources];
        [FieldOffset(0x110)] public byte NumNormalItems;
        [FieldOffset(0x114)] public uint RareItemId;
        [FieldOffset(0x118)] public uint RareIconId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x8)]
    public unsafe partial struct ExpeditionDesc
    {
        [FieldOffset(0x0)] public byte ExpeditionId;
        [FieldOffset(0x1)] public byte u1;
        [FieldOffset(0x2)] public byte RarePouchId;
        [FieldOffset(0x3)] public byte u3;
        [FieldOffset(0x4)] public ushort u4;
        [FieldOffset(0x6)] public ushort NameId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x6)]
    public unsafe partial struct ExpeditionItem
    {
        [FieldOffset(0x0)] public ushort ExpeditionId;
        [FieldOffset(0x2)] public ushort u2;
        [FieldOffset(0x4)] public byte PouchId;
        [FieldOffset(0x5)] public byte u5;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xC)]
    public unsafe partial struct Resource
    {
        [FieldOffset(0x0)] public ushort PouchId;
        [FieldOffset(0x2)] public ushort u2;
        [FieldOffset(0x4)] public uint ItemId;
        [FieldOffset(0x8)] public uint IconId;
    }
}

public unsafe class GranaryState
{
    public enum CollectResult { NothingToCollect, CanCollectSafely, CanCollectWithOvercap, EverythingCapped }

    public AgentMJIGatheringHouse* Agent;
    private ExcelSheet<MJIItemPouch> _sheetPouch;

    private delegate void GatherDelegate(MJIGranariesState* self, byte granaryIndex);
    private GatherDelegate _gather;

    private delegate void CommitExpeditionDelegate(MJIGranariesState* self, byte granaryIndex, byte expeditionIndex, byte numDays);
    private CommitExpeditionDelegate _commitExpedition;

    private delegate bool IsExpeditionUnlockedDelegate(AgentMJIGatheringHouse* self, AgentMJIGatheringHouse.ExpeditionData* expedition);
    private IsExpeditionUnlockedDelegate _isExpeditionUnlocked;

    public GranaryState()
    {
        Agent = (AgentMJIGatheringHouse*)AgentModule.Instance()->GetAgentByInternalId(AgentId.MJIGatheringHouse);
        _sheetPouch = Service.LuminaGameData.GetExcelSheet<MJIItemPouch>()!;
        _gather = Marshal.GetDelegateForFunctionPointer<GatherDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? C7 83 ?? ?? ?? ?? ?? ?? ?? ?? E9 ?? ?? ?? ?? 48 8D 54 24"));
        _commitExpedition = Marshal.GetDelegateForFunctionPointer<CommitExpeditionDelegate>(Service.SigScanner.ScanText("48 83 EC 38 45 0F B6 C9")); // sig is quite bad
        _isExpeditionUnlocked = Marshal.GetDelegateForFunctionPointer<IsExpeditionUnlockedDelegate>(Service.SigScanner.ScanText("E8 ?? ?? ?? ?? 84 C0 75 0A 66 FF C6"));
    }

    public MJIGranaryState* GetGranaryState(int index)
    {
        var state = Agent != null ? Agent->GranariesState : null;
        return state == null ? null : index > 0 ? &state->Granary2 : &state->Granary1;
    }

    public void Collect(int index)
    {
        Service.Log.Info($"Gathering from granary {index}");
        var state = Agent != null ? Agent->GranariesState : null;
        if (state != null)
            _gather(state, (byte)index);
    }

    public void SelectExpedition(byte granaryIndex, byte expeditionId, byte numDays)
    {
        Service.Log.Info($"Selecting expedition {expeditionId} for {numDays} days at granary {granaryIndex}");
        var gstate = GetGranaryState(granaryIndex);
        if (gstate != null)
        {
            // set current agent fields to emulate user interactions, so that messages are correct
            var confirm = CalculateConfirmation(gstate->ActiveExpeditionId, gstate->RemainingDays, expeditionId, numDays);
            if (confirm == AgentMJIGatheringHouse.Confirmation.None)
            {
                Service.Log.Info($"=> nothing to do, this is already active");
            }
            else if (!IsExpeditionUnlocked(expeditionId))
            {
                Service.Log.Info($"=> expedition is locked");
            }
            else if (numDays - gstate->RemainingDays > MaxDays())
            {
                Service.Log.Info($"=> not enough cowries");
            }
            else
            {
                Agent->CurGranaryIndex = granaryIndex;
                Agent->CurActiveExpeditionId = gstate->ActiveExpeditionId;
                Agent->CurActiveDays = gstate->RemainingDays;
                Agent->CurHoveredExpeditionId = Agent->CurProposedExpeditionId = expeditionId;
                Agent->CurProposedDays = numDays;
                Agent->CurExpeditionName.SetString(Agent->Data->Expeditions.Get(expeditionId).Name.ToString());
                Agent->ConfirmType = confirm;
                _commitExpedition(Agent->GranariesState, granaryIndex, expeditionId, numDays);
            }
        }
    }

    public CollectResult CalculateGranaryCollectionState(int index)
    {
        var gstate = GetGranaryState(index);
        if (gstate == null)
            return CollectResult.NothingToCollect;

        bool haveAnything = gstate->RareResourceCount > 0;
        bool overcapSome = haveAnything && WillOvercap(gstate->RareResourcePouchId, gstate->RareResourceCount);
        bool overcapAll = !haveAnything || overcapSome;
        for (int i = 0; i < MJIGranaryState.NumNormalResources; ++i)
        {
            if (gstate->NormalResourceCounts[i] > 0)
            {
                haveAnything = true;
                var overcap = WillOvercap(gstate->NormalResourcePouchIds[i], gstate->NormalResourceCounts[i]);
                overcapSome |= overcap;
                overcapAll &= overcap;
            }
        }
        return !haveAnything ? CollectResult.NothingToCollect : overcapAll ? CollectResult.EverythingCapped : overcapSome ? CollectResult.CanCollectWithOvercap : CollectResult.CanCollectSafely;
    }

    public static AgentMJIGatheringHouse.Confirmation CalculateConfirmation(byte curExpedition, byte curDays, byte newExpedition, byte newDays)
        => curExpedition == newExpedition && curDays >= newDays ? AgentMJIGatheringHouse.Confirmation.None
            : curExpedition == 0 && curDays == 0 ? AgentMJIGatheringHouse.Confirmation.Start
            : curExpedition != newExpedition && curDays < newDays ? AgentMJIGatheringHouse.Confirmation.ChangeExtend
            : curExpedition != newExpedition ? AgentMJIGatheringHouse.Confirmation.Change : AgentMJIGatheringHouse.Confirmation.Extend;

    public bool IsExpeditionUnlocked(byte id)
    {
        var data = Agent != null ? Agent->Data : null;
        return data != null && id < data->Expeditions.Size() ? _isExpeditionUnlocked(Agent, data->Expeditions.First + id) : false;
    }

    public int NumCowries() => InventoryManager.Instance()->GetInventoryItemCount(37549);
    public int MaxDays() => NumCowries() / 50;

    private bool WillOvercap(uint pouchId, int count) => InventoryManager.Instance()->GetInventoryItemCount(_sheetPouch.GetRow(pouchId)!.Item.Row) + count > 999;
}
