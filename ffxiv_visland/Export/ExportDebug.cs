using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.GeneratedSheets2;
using visland.Helpers;

namespace visland.Export;

public unsafe class ExportDebug
{
    private UITree _tree = new();

    public void Draw()
    {
        var sheetItem = Service.LuminaGameData.GetExcelSheet<Item>()!;
        var agent = AgentMJIDisposeShop.Instance();
        foreach (var n1 in _tree.Node($"Agent: {(nint)agent:X}", agent == null && agent->Data != null))
        {
            var opHandler = *(AgentInterface**)((nint)agent + 0x18); // it's really an atkeventlistener pointer, but atkeventlistener in CS doesn't define vfuncs...
            _tree.LeafNode($"OpHandler: {(nint)opHandler:X}, vtable=+{(nint)opHandler->AtkEventInterface.vtbl - Service.SigScanner.Module.BaseAddress:X}, obj={*(nint*)((nint)opHandler + 0x10):X}");
            _tree.LeafNode($"Unk2C: {agent->Data->u2C}");
            _tree.LeafNode($"Init: stage={agent->Data->InitializationState}, data-init={agent->Data->DataInitialized}, dirty={agent->Data->AddonDirty}");
            _tree.LeafNode($"Currency 0: {agent->Data->CurrencyItemId[0]} '{sheetItem.GetRow(agent->Data->CurrencyItemId[0])?.Name}': {agent->Data->CurrencyCount[0]}/{agent->Data->CurrencyStackSize[0]}");
            _tree.LeafNode($"Currency 1: {agent->Data->CurrencyItemId[1]} '{sheetItem.GetRow(agent->Data->CurrencyItemId[1])?.Name}': {agent->Data->CurrencyCount[1]}/{agent->Data->CurrencyStackSize[1]}");
            _tree.LeafNode($"Cur category: {agent->Data->CurSelectedCategory} '{agent->Data->CategoryNames.Span[agent->Data->CurSelectedCategory]}'");
            _tree.LeafNode($"Cur ship item: {agent->Data->CurShipItemIndex} qty={agent->Data->CurShipQuantity}");
            _tree.LeafNode($"Cur ship bulk: limit={agent->Data->CurBulkShiptLimit} stage={agent->Data->CurBulkShipCheckStage}");

            foreach (var n2 in _tree.Node($"All items ({agent->Data->Items.Size()})"))
            {
                foreach (ref readonly var a in agent->Data->Items.Span)
                {
                    _tree.LeafNode($"{a.ItemIndex}: {a.ItemId} '{a.Name}', shop-row={a.ShopItemRowId}, count={a.CountInInventory}");
                }
            }

            for (int i = 0; i < AgentMJIDisposeShop.AgentData.NumCategories; ++i)
            {
                foreach (var n2 in _tree.Node($"Category {agent->Data->CategoryNames.Span[i]}: {agent->Data->PerCategoryItemsSpan[i].Size()} items"))
                {
                    foreach (var item in agent->Data->PerCategoryItemsSpan[i].Span)
                    {
                        _tree.LeafNode($"{item.Value->ItemIndex}: {item.Value->ItemId} '{item.Value->Name}' count={item.Value->CountInInventory}");
                    }
                }
            }
        }
    }
}
