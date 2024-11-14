using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System.Runtime.CompilerServices;
using visland.Helpers;

namespace visland.Pasture;

public unsafe class PastureDebug
{
    private UITree _tree = new();

    public void Draw()
    {
        var mgr = MJIManager.Instance();
        var sheetItem = Service.LuminaGameData.GetExcelSheet<Item>()!;
        foreach (var n1 in _tree.Node($"State: level={mgr->IslandState.Pasture.Level}, htc={mgr->IslandState.Pasture.HoursToCompletion}, uc={mgr->IslandState.Pasture.UnderConstruction}, efc={mgr->IslandState.Pasture.EligibleForCare}", mgr->PastureHandler == null))
        {
            _tree.LeafNode($"PastureH = {(nint)mgr->PastureHandler:X}");
            foreach (var n2 in _tree.Node("Animal -> leavings"))
            {
                foreach (var (k, v) in mgr->PastureHandler->AnimalToLeavingItemIds)
                {
                    _tree.LeafNode($"{k} = {v.Item1} '{sheetItem.GetRow(v.Item1).Name}' / {v.Item2} '{sheetItem.GetRow(v.Item2).Name}'");
                }
            }
            foreach (var n2 in _tree.Node("Available Leavings"))
            {
                foreach (var (k, v) in mgr->PastureHandler->AvailableMammetLeavings)
                {
                    _tree.LeafNode($"{k} '{sheetItem.GetRow(k).Name}' = {v}");
                }
            }
            foreach (var n2 in _tree.Node("Animals"))
            {
                var sheetAnimals = Service.LuminaGameData.GetExcelSheet<MJIAnimals>()!; // AnimalType is row here
                var sheetName = Service.LuminaGameData.GetExcelSheet<BNpcName>()!;
                foreach (ref var a in mgr->PastureHandler->MJIAnimals)
                {
                    var baseName = sheetName.GetRow(a.BNPCNameId).Singular;
                    var pa = (byte*)Unsafe.AsPointer(ref a);
                    foreach (var n3 in _tree.Node($"{a.SlotId} '{baseName}'"))
                    {
                        var food = sheetItem.GetRow(a.AutoFoodItemId);
                        _tree.LeafNode($"Nickname: '{MemoryHelper.ReadString((nint)pa + 1, 24)}'");
                        _tree.LeafNode($"ObjectID: {a.EntityId:X}");
                        _tree.LeafNode($"MJIAnimal row id: {a.AnimalType}");
                        _tree.LeafNode($"Mood={a.Mood}, food={a.FoodLevel} hours");
                        _tree.LeafNode($"Have leavings: {a.ManualLeavingsAvailable}");
                        _tree.LeafNode($"Care: cared={a.UnderCare}, paid={a.WasUnderCare}, halted={a.CareHalted}");
                        _tree.LeafNode($"Mammet Food: {food.RowId} '{food.Name}");
                        _tree.LeafNode($"Mammet Collect: {a.AutoAvailableLeavings1} / {a.AutoAvailableLeavings2}");
                    }
                }
            }
        }

        var agent = AgentMJIAnimalManagement.Instance();
        foreach (var n1 in _tree.Node($"Agent: {(nint)agent:X}", agent == null))
        {
            _tree.LeafNode($"OpHandler: {(nint)agent->OpHandler:X}, vtable=+{(nint)agent->OpHandler->VirtualTable - Service.SigScanner.Module.BaseAddress:X}, f10={*(nint*)((nint)agent->OpHandler + 0x10):X}");
            _tree.LeafNode($"Dirtyness: data-ready={agent->DataInitialized}, need-update={agent->UpdateNeeded}");
            _tree.LeafNode($"Num pasture slots: {agent->NumPastureSlots}");
            _tree.LeafNode($"Cur ctx menu row: {agent->CurContextMenuRow}");
            _tree.LeafNode($"Pending release: {agent->PendingReleaseEntityId:X}");
            _tree.LeafNode($"Proposed nickname: {agent->ProposedNickname}");
            _tree.LeafNode($"During capture: {agent->DuringCapture}");
            _tree.LeafNode($"Expected collect leavings: {agent->ExpectedCollectLeavings}");
            foreach (var n2 in _tree.Node("Slots"))
            {
                foreach (ref readonly var a in agent->Slots.AsSpan())
                {
                    foreach (var n3 in _tree.Node($"{a.Desc.AnimalRowId} '{a.Desc.Nickname}'"))
                    {
                        _tree.LeafNode($"ObjectID: {a.EntityId:X}");
                        _tree.LeafNode($"Rarity: {a.Desc.Rarity}");
                        _tree.LeafNode($"Sort: {a.Desc.Sort}");
                        _tree.LeafNode($"Rewards: {a.Desc.Leaving1ItemId} '{sheetItem.GetRow(a.Desc.Leaving1ItemId).Name}' / {a.Desc.Leaving2ItemId} '{sheetItem.GetRow(a.Desc.Leaving2ItemId).Name}'");
                        _tree.LeafNode($"Food: {a.FoodItemId} '{a.FoodName}' ({a.FoodLink}) {a.FoodItemCategoryId} {a.FoodCount}");
                        _tree.LeafNode($"Tail: leavings={a.HaveUngatheredLeavings} cared={a.UnderCare}, paid={a.WasCared}, halted={a.CareHalted}");
                    }
                }
            }
            foreach (var n2 in _tree.Node("Animals"))
            {
                var sheetName = Service.LuminaGameData.GetExcelSheet<BNpcName>()!;
                foreach (ref readonly var a in agent->AnimalDescs.AsSpan())
                {
                    foreach (var n3 in _tree.Node($"{a.AnimalRowId} '{a.Nickname}'/{sheetName.GetRow(a.BNpcNameId).Singular}"))
                    {
                        _tree.LeafNode($"Rarity: {a.Rarity}");
                        _tree.LeafNode($"Sort: {a.Sort}");
                        _tree.LeafNode($"Rewards: {a.Leaving1ItemId} '{sheetItem.GetRow(a.Leaving1ItemId).Name}' / {a.Leaving2ItemId} '{sheetItem.GetRow(a.Leaving2ItemId).Name}'");
                    }
                }
            }
            foreach (var n2 in _tree.Node("Items"))
            {
                foreach (var a in agent->ItemDescs.AsSpan())
                {
                    foreach (var n3 in _tree.Node($"{a.ItemId} '{a.Name}' cat={a.CategoryId} #{a.CountInInventory}"))
                    {
                        _tree.LeafNode($"u={a.Link}");
                    }
                }
            }
            foreach (var n2 in _tree.Node($"Available food: {agent->EntrustAvailableFood.LongCount} items", agent->EntrustAvailableFood.LongCount == 0))
            {
                foreach (ref readonly var a in agent->EntrustAvailableFood.AsSpan())
                {
                    _tree.LeafNode($"{a.Value->ItemId} '{a.Value->Name}' cat={a.Value->CategoryId} #{a.Value->CountInInventory}");
                }
            }
        }
    }
}
