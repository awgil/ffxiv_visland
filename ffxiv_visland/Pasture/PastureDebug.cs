using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using Lumina.Excel.GeneratedSheets2;
using System.Runtime.InteropServices;
using visland.Helpers;

namespace visland.Pasture;

[StructLayout(LayoutKind.Explicit, Size = 0x1E0)]
public unsafe partial struct AgentMJIAnimalManagement
{
    [FieldOffset(0)] public AgentInterface AgentInterface;
    [FieldOffset(0x100)] public StdVector<Slot> Slots;
    [FieldOffset(0x118)] public StdVector<AnimalDesc> AnimalDescs;
    [FieldOffset(0x130)] public StdVector<ItemDesc> ItemDescs;
    [FieldOffset(0x148)] public StdVector<Pointer<ItemDesc>> AvailableFood; // filled and updated on entrust
    [FieldOffset(0x160)] public int NumPastureSlots;
    [FieldOffset(0x164)] public int CurContextMenuRow;
    [FieldOffset(0x170)] public Utf8String u170;
    [FieldOffset(0x1DC)] public int ExpectedCollectLeavings;

    [StructLayout(LayoutKind.Explicit, Size = 0x170)]
    public unsafe partial struct Slot
    {
        [FieldOffset(0x000)] public uint ObjectId;
        [FieldOffset(0x004)] public uint u4;
        [FieldOffset(0x008)] public byte AnimalRowId;
        [FieldOffset(0x009)] public byte Rarity;
        [FieldOffset(0x00A)] public byte Sort;
        [FieldOffset(0x00C)] public uint IconId;
        [FieldOffset(0x010)] public uint Reward0;
        [FieldOffset(0x014)] public uint Reward1;
        [FieldOffset(0x018)] public uint BNpcNameId;
        [FieldOffset(0x020)] public Utf8String Nickname;
        [FieldOffset(0x088)] public uint FoodItemId;
        [FieldOffset(0x08C)] public uint FoodItemCategoryId;
        [FieldOffset(0x090)] public uint FoodCount;
        [FieldOffset(0x094)] public uint FoodIconId;
        [FieldOffset(0x098)] public Utf8String FoodName;
        [FieldOffset(0x100)] public Utf8String u100;
        [FieldOffset(0x168)] public byte Mood;
        [FieldOffset(0x169)] public byte FoodLevel;
        [FieldOffset(0x16A)] public byte AvailLeavings1;
        [FieldOffset(0x16B)] public byte AvailLeavings2;
        [FieldOffset(0x16C)] public bool HaveLeavings;
        [FieldOffset(0x16D)] public bool UnderCare;
        [FieldOffset(0x16E)] public byte u16E; // true if not under care
        [FieldOffset(0x16F)] public byte u16F;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x80)]
    public unsafe partial struct AnimalDesc
    {
        [FieldOffset(0x00)] public byte AnimalRowId;
        [FieldOffset(0x01)] public byte Rarity;
        [FieldOffset(0x02)] public byte Sort;
        [FieldOffset(0x04)] public uint IconId;
        [FieldOffset(0x08)] public uint RewardItem0;
        [FieldOffset(0x0C)] public uint RewardItem1;
        [FieldOffset(0x10)] public uint BNpcNameId;
        [FieldOffset(0x18)] public Utf8String Nickname;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xE0)]
    public unsafe partial struct ItemDesc
    {
        [FieldOffset(0x00)] public uint ItemId;
        [FieldOffset(0x04)] public uint CategoryId;
        [FieldOffset(0x08)] public uint CountInInventory;
        [FieldOffset(0x0C)] public uint IconId;
        [FieldOffset(0x10)] public Utf8String Name;
        [FieldOffset(0x78)] public Utf8String u78;
    }
}

[StructLayout(LayoutKind.Explicit, Size = 0xB78)]
public unsafe partial struct MJIPastureHandlerEx
{
    [FieldOffset(0x2D8)] public StdMap<uint, int> AvailableLeavings; // key=item id, value=uncollected value
}

[StructLayout(LayoutKind.Explicit, Size = 0x34)]
public unsafe struct MJIAnimalEx
{
    [FieldOffset(0x00)] public byte SlotId;
    [FieldOffset(0x01)] public fixed byte Nickname[24]; // string
    [FieldOffset(0x1C)] public uint BNPCNameId;
    [FieldOffset(0x20)] public uint ObjectId;
    [FieldOffset(0x24)] public byte AnimalType;
    [FieldOffset(0x25)] public byte FoodLevel;
    [FieldOffset(0x26)] public byte Mood;
    [FieldOffset(0x27)] public bool HaveLeavings;
    [FieldOffset(0x28)] public bool UnderCare;
    [FieldOffset(0x28)] public byte u29;
    [FieldOffset(0x2A)] public byte u2A; // true if not under care
    [FieldOffset(0x2B)] public byte pad2B;
    [FieldOffset(0x2C)] public uint FoodItemId;
    [FieldOffset(0x30)] public byte AvailableLeavings1;
    [FieldOffset(0x31)] public byte AvailableLeavings2;
    [FieldOffset(0x32)] public byte pad32;
    [FieldOffset(0x33)] public byte pad33;
}


public unsafe class PastureDebug
{
    private UITree _tree = new();

    public void Draw()
    {
        var mgr = MJIManager.Instance();
        var sheetItem = Service.LuminaGameData.GetExcelSheet<Item>()!;
        foreach (var n1 in _tree.Node($"State: level={mgr->IslandState.Pasture.Level}, htc={mgr->IslandState.Pasture.HoursToCompletion}, uc={mgr->IslandState.Pasture.UnderConstruction}, efc={mgr->IslandState.Pasture.EligibleForCare}", mgr->PastureHandler == null))
        {
            var phex = (MJIPastureHandlerEx*)mgr->PastureHandler;
            foreach (var n2 in _tree.Node("Available Leavings"))
            {
                foreach (var (k, v) in phex->AvailableLeavings)
                {
                    _tree.LeafNode($"{k} '{sheetItem.GetRow(k)?.Name}' = {v}");
                }
            }
            foreach (var n2 in _tree.Node("Animals"))
            {
                var sheetAnimals = Service.LuminaGameData.GetExcelSheet<MJIAnimals>()!; // AnimalType is row here
                var sheetName = Service.LuminaGameData.GetExcelSheet<BNpcName>()!;
                foreach (ref var a in mgr->PastureHandler->MJIAnimalsSpan)
                {
                    var baseName = sheetName.GetRow(a.BNPCNameId)?.Singular;
                    fixed (MJIAnimal* pa = &a)
                    {
                        var p = (byte*)pa;
                        var pex = (MJIAnimalEx*)pa;
                        foreach (var n3 in _tree.Node($"{a.SlotId} '{baseName}': unk={pex->u29} {pex->u2A} pad={pex->pad2B:X} {pex->pad32:X} {pex->pad33:X}"))
                        {
                            var food = sheetItem.GetRow(*(uint*)(p + 0x2C));
                            _tree.LeafNode($"Nickname: '{MemoryHelper.ReadString((nint)p + 1, 24)}'");
                            _tree.LeafNode($"ObjectID: {a.ObjectId:X}");
                            _tree.LeafNode($"MJIAnimal row id: {a.AnimalType}");
                            _tree.LeafNode($"Mood={a.Mood}, food={a.FoodLevel} hours");
                            _tree.LeafNode($"Have leavings: {pex->HaveLeavings}");
                            _tree.LeafNode($"Under care: {pex->UnderCare}");
                            _tree.LeafNode($"Mammet Food: {food?.RowId} '{food?.Name}");
                            _tree.LeafNode($"Mammet Collect: {pex->AvailableLeavings1} / {pex->AvailableLeavings2}");
                        }
                    }
                }
            }
        }

        var agent = (AgentMJIAnimalManagement*)AgentModule.Instance()->GetAgentByInternalId(AgentId.MJIAnimalManagement);
        foreach (var n1 in _tree.Node($"Agent: {(nint)agent:X}", agent == null))
        {
            _tree.LeafNode($"Num pasture slots: {agent->NumPastureSlots}");
            _tree.LeafNode($"Cur ctx menu row: {agent->CurContextMenuRow}");
            _tree.LeafNode($"u170={agent->u170} expected={agent->ExpectedCollectLeavings}");
            foreach (var n2 in _tree.Node("Slots"))
            {
                foreach (var a in agent->Slots.Span)
                {
                    foreach (var n3 in _tree.Node($"{a.AnimalRowId} '{a.Nickname}': u4={a.u4:X8}"))
                    {
                        _tree.LeafNode($"ObjectID: {a.ObjectId:X}");
                        _tree.LeafNode($"Rarity: {a.Rarity}");
                        _tree.LeafNode($"Sort: {a.Sort}");
                        _tree.LeafNode($"Rewards: {a.Reward0} '{sheetItem.GetRow(a.Reward0)?.Name}' / {a.Reward1} '{sheetItem.GetRow(a.Reward1)?.Name}'");
                        _tree.LeafNode($"Food: {a.FoodItemId} '{a.FoodName}' ({a.u100}) {a.FoodItemCategoryId} {a.FoodCount}");
                        _tree.LeafNode($"Tail: {a.HaveLeavings} {a.UnderCare} {a.u16E} {a.u16F}");
                    }
                }
            }
            foreach (var n2 in _tree.Node("Animals"))
            {
                var sheetName = Service.LuminaGameData.GetExcelSheet<BNpcName>()!;
                foreach (var a in agent->AnimalDescs.Span)
                {
                    foreach (var n3 in _tree.Node($"{a.AnimalRowId} '{a.Nickname}'/{sheetName.GetRow(a.BNpcNameId)?.Singular}"))
                    {
                        _tree.LeafNode($"Rarity: {a.Rarity}");
                        _tree.LeafNode($"Sort: {a.Sort}");
                        _tree.LeafNode($"Rewards: {a.RewardItem0} '{sheetItem.GetRow(a.RewardItem0)?.Name}' / {a.RewardItem1} '{sheetItem.GetRow(a.RewardItem1)?.Name}'");
                    }
                }
            }
            foreach (var n2 in _tree.Node("Items"))
            {
                foreach (var a in agent->ItemDescs.Span)
                {
                    foreach (var n3 in _tree.Node($"{a.ItemId} '{a.Name}' cat={a.CategoryId} #{a.CountInInventory}"))
                    {
                        _tree.LeafNode($"u={a.u78}");
                    }
                }
            }
            foreach (var n2 in _tree.Node("Available food"))
            {
                foreach (var a in agent->AvailableFood.Span)
                {
                    _tree.LeafNode($"{a.Value->ItemId} '{a.Value->Name}' cat={a.Value->CategoryId} #{a.Value->CountInInventory}");
                }
            }
        }
    }
}
