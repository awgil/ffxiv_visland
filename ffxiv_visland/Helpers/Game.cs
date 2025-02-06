using ECommons.DalamudServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Network;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.Interop;
using System;
using System.Linq;
using System.Numerics;

namespace visland.Helpers;

// utilities for interacting with game
public static unsafe class Game
{
    public static bool ExecuteTeleport(uint aetheryteId) => UIState.Instance()->Telepo.Teleport(aetheryteId, 0);

    public static bool InteractWith(ulong instanceId)
    {
        var obj = GameObjectManager.Instance()->Objects.GetObjectByGameObjectId(instanceId);
        if (obj == null)
            return false;
        TargetSystem.Instance()->InteractWithObject(obj);
        return true;
    }

    public static void TeleportToAethernet(uint currentAetheryte, uint destinationAetheryte)
    {
        Span<uint> payload = [4, destinationAetheryte];
        PacketDispatcher.SendEventCompletePacket(0x50000 | currentAetheryte, 0, 0, payload.GetPointer(0), (byte)payload.Length, null);
    }

    public static void TeleportToFirmament(uint currentAetheryte)
    {
        Span<uint> payload = [9];
        PacketDispatcher.SendEventCompletePacket(0x50000 | currentAetheryte, 0, 0, payload.GetPointer(0), (byte)payload.Length, null);
    }

    public static (ulong id, Vector3 pos) FindAetheryte(uint id)
    {
        foreach (var obj in GameObjectManager.Instance()->Objects.IndexSorted)
            if (obj.Value != null && obj.Value->ObjectKind == ObjectKind.Aetheryte && obj.Value->BaseId == id)
                return (obj.Value->GetGameObjectId(), *obj.Value->GetPosition());
        return (0, default);
    }

    public static ulong? FindInteractable(uint id) => Svc.Objects.FirstOrDefault(o => o?.DataId == id && (o.Position - Player.Position).LengthSquared() < 1 && o.IsTargetable, null)?.GameObjectId;

    public static int NumItemsInInventory(uint itemId, short minCollectibility) => InventoryManager.Instance()->GetInventoryItemCount(itemId, false, false, false, minCollectibility);

    public static AtkUnitBase* GetFocusedAddonByID(uint id)
    {
        var unitManager = &AtkStage.Instance()->RaptureAtkUnitManager->AtkUnitManager.FocusedUnitsList;
        foreach (var j in Enumerable.Range(0, Math.Min(unitManager->Count, unitManager->Entries.Length)))
        {
            var unitBase = unitManager->Entries[j].Value;
            if (unitBase != null && unitBase->Id == id)
            {
                return unitBase;
            }
        }
        return null;
    }

    public static AtkUnitBase* GetAddonByName(string name) => RaptureAtkUnitManager.Instance()->GetAddonByName(name);
    public static bool AddonActive(string name) => AddonActive(GetAddonByName(name));
    public static bool AddonActive(AtkUnitBase* addon) => addon != null && addon->IsVisible && addon->IsReady;

    public static void ProgressTalk()
    {
        var addon = GetAddonByName("Talk");
        if (addon != null && addon->IsReady)
        {
            var evt = new AtkEvent() { Listener = &addon->AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
            var data = new AtkEventData();
            addon->ReceiveEvent(AtkEventType.MouseClick, 0, &evt, &data);
        }
    }

    public static void SelectYes()
    {
        if (TryGetAddonByName<AtkUnitBase>("SelectYesno", out var addon))
        {
            var evt = new AtkEvent() { Listener = &addon->AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
            var data = new AtkEventData();
            addon->ReceiveEvent(AtkEventType.ButtonClick, 0, &evt, &data);
        }
    }
}