using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using visland.Helpers;

namespace visland.Farm;

public unsafe class FarmDebug
{
    private UITree _tree = new();

    public void Draw()
    {
        var mgr = MJIManager.Instance();
        var sheetItem = Service.LuminaGameData.GetExcelSheet<Item>()!;
        var sheetCrop = Service.LuminaGameData.GetExcelSheet<MJICropSeed>()!;
        foreach (var n1 in _tree.Node($"State: level={mgr->IslandState.Farm.Level}, htc={mgr->IslandState.Farm.HoursToCompletion}, uc={mgr->IslandState.Farm.UnderConstruction}, efc={mgr->IslandState.Farm.EligibleForCare}", mgr->FarmState == null))
        {
            _tree.LeafNode($"Expected total yield: {mgr->FarmState->ExpectedTotalYield}");
            _tree.LeafNode($"Layout state: {mgr->FarmState->LayoutInitialized} {mgr->FarmState->ReactionEventObjectRowId}");
            _tree.LeafNode($"Slot update: {mgr->FarmState->SlotUpdatePending} {mgr->FarmState->SlotUpdateIndex}");

            foreach (var n2 in _tree.Node("Slots"))
            {
                for (var i = 0; i < 20; ++i)
                {
                    _tree.LeafNode($"{i}: seed={mgr->FarmState->SeedType[i]} '{sheetCrop.GetRow(mgr->FarmState->SeedType[i]).Name.Value.Singular}', growth={mgr->FarmState->GrowthLevel[i]}, water={mgr->FarmState->WaterLevel[i]}, yield={mgr->FarmState->GardenerYield[i]}, flags={mgr->FarmState->FarmSlotFlags[i]}, pi={mgr->FarmState->PlotObjectIndex[i]}, lay={mgr->FarmState->LayoutId[i]:X}");
                }
            }
            foreach (var n2 in _tree.Node("Seeds"))
            {
                var i = 0;
                foreach (var id in mgr->FarmState->SeedItemIds.AsSpan())
                {
                    _tree.LeafNode($"{i++} = {id} '{sheetItem.GetRow(id).Name}'");
                }
            }
        }

        var agent = AgentMJIFarmManagement.Instance();
        foreach (var n1 in _tree.Node($"Agent: {(nint)agent:X}", agent == null))
        {
            _tree.LeafNode($"Delay show: {agent->DelayShow}");
            _tree.LeafNode($"OpHandler: {(nint)agent->OpHandler:X}, vtable=+{(nint)agent->OpHandler->VirtualTable - Service.SigScanner.Module.BaseAddress:X}");
            _tree.LeafNode($"Cur ctx menu: row={agent->CurContextMenuRow}, op={agent->CurContextOpType}");
            _tree.LeafNode($"Total yield: avail={agent->TotalAvailableYield}, expected={agent->ExpectedTotalAvailableYield}");
            foreach (var n2 in _tree.Node($"Slots: {agent->NumSlots}", agent->NumSlots == 0))
            {
                for (var i = 0; i < agent->NumSlots; ++i)
                {
                    ref var slot = ref agent->Slots[i];
                    foreach (var n3 in _tree.Node($"{i}: {slot.YieldName}"))
                    {
                        _tree.LeafNode($"Seed: {slot.SeedItemId} '{slot.SeedName}' have={slot.SeedInventoryCount}");
                        _tree.LeafNode($"Yield: {slot.YieldItemId} '{slot.YieldName}' avail={slot.YieldAvailable}");
                        _tree.LeafNode($"State: water={slot.WaterLevel}, growth={slot.GrowthLevel}, care={slot.UnderCare}, halt={slot.CareHalted}, was-cared={slot.WasUnderCare}, flag8={slot.Flag8}");
                    }
                }
            }
            foreach (var n2 in _tree.Node("Seeds"))
            {
                foreach (ref readonly var a in agent->Seeds.AsSpan())
                {
                    _tree.LeafNode($"{a.ItemId} '{a.Name}', count={a.Count}, u={a.IconId}");
                }
            }
        }
    }
}
