﻿using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.GeneratedSheets2;
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
            foreach (var n2 in _tree.Node("Available Leavings"))
            {
                foreach (var (k, v) in mgr->PastureHandler->AvailableMammetLeavings)
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
                    var pa = (byte*)Unsafe.AsPointer(ref a);
                    foreach (var n3 in _tree.Node($"{a.SlotId} '{baseName}': unk={pa[0x29]} {pa[0x2A]} pad={pa[0x2B]:X} {pa[0x32]:X} {pa[0x33]:X}"))
                    {
                        var food = sheetItem.GetRow(a.AutoFoodItemId);
                        _tree.LeafNode($"Nickname: '{MemoryHelper.ReadString((nint)pa + 1, 24)}'");
                        _tree.LeafNode($"ObjectID: {a.ObjectId:X}");
                        _tree.LeafNode($"MJIAnimal row id: {a.AnimalType}");
                        _tree.LeafNode($"Mood={a.Mood}, food={a.FoodLevel} hours");
                        _tree.LeafNode($"Have leavings: {a.ManualLeavingsAvailable}");
                        _tree.LeafNode($"Under care: {a.UnderCare}");
                        _tree.LeafNode($"Mammet Food: {food?.RowId} '{food?.Name}");
                        _tree.LeafNode($"Mammet Collect: {a.AutoAvailableLeavings1} / {a.AutoAvailableLeavings2}");
                    }
                }
            }
        }

        var agent = AgentMJIAnimalManagement.Instance();
        foreach (var n1 in _tree.Node($"Agent: {(nint)agent:X}", agent == null))
        {
            _tree.LeafNode($"Num pasture slots: {agent->NumPastureSlots}");
            _tree.LeafNode($"Cur ctx menu row: {agent->CurContextMenuRow}");
            _tree.LeafNode($"Expected collect leavings: {agent->ExpectedCollectLeavings}");
            foreach (var n2 in _tree.Node("Slots"))
            {
                foreach (ref readonly var a in agent->Slots.Span)
                {
                    foreach (var n3 in _tree.Node($"{a.AnimalRowId} '{a.Nickname}'"))
                    {
                        _tree.LeafNode($"ObjectID: {a.ObjectId:X}");
                        _tree.LeafNode($"Rarity: {a.Rarity}");
                        _tree.LeafNode($"Sort: {a.Sort}");
                        _tree.LeafNode($"Rewards: {a.Leaving1ItemId} '{sheetItem.GetRow(a.Leaving1ItemId)?.Name}' / {a.Leaving2ItemId} '{sheetItem.GetRow(a.Leaving2ItemId)?.Name}'");
                        _tree.LeafNode($"Food: {a.FoodItemId} '{a.FoodName}' ({a.FoodLink}) {a.FoodItemCategoryId} {a.FoodCount}");
                        _tree.LeafNode($"Tail: {a.HaveUngatheredLeavings} {a.UnderCare}");
                    }
                }
            }
            foreach (var n2 in _tree.Node("Animals"))
            {
                var sheetName = Service.LuminaGameData.GetExcelSheet<BNpcName>()!;
                foreach (ref readonly var a in agent->AnimalDescs.Span)
                {
                    foreach (var n3 in _tree.Node($"{a.AnimalRowId} '{a.Nickname}'/{sheetName.GetRow(a.BNpcNameId)?.Singular}"))
                    {
                        _tree.LeafNode($"Rarity: {a.Rarity}");
                        _tree.LeafNode($"Sort: {a.Sort}");
                        _tree.LeafNode($"Rewards: {a.Leaving1ItemId} '{sheetItem.GetRow(a.Leaving1ItemId)?.Name}' / {a.Leaving2ItemId} '{sheetItem.GetRow(a.Leaving2ItemId)?.Name}'");
                    }
                }
            }
            foreach (var n2 in _tree.Node("Items"))
            {
                foreach (var a in agent->ItemDescs.Span)
                {
                    foreach (var n3 in _tree.Node($"{a.ItemId} '{a.Name}' cat={a.CategoryId} #{a.CountInInventory}"))
                    {
                        _tree.LeafNode($"u={a.Link}");
                    }
                }
            }
            foreach (var n2 in _tree.Node($"Available food: {agent->EntrustAvailableFood.Size()} items", agent->EntrustAvailableFood.Size() == 0))
            {
                foreach (ref readonly var a in agent->EntrustAvailableFood.Span)
                {
                    _tree.LeafNode($"{a.Value->ItemId} '{a.Value->Name}' cat={a.Value->CategoryId} #{a.Value->CountInInventory}");
                }
            }
        }
    }
}
