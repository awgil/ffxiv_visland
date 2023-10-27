using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using System.Runtime.InteropServices;

namespace visland.Workshop;

// indices: 0-2 are 'prev', 3-5 are 'curr', 6-8 are 'next', order is 4/6/8h
[StructLayout(LayoutKind.Explicit, Size = 0x50)]
public unsafe partial struct MJIManagerFavors
{
    [FieldOffset(0x18)] public fixed byte ObjectsIDs[9]; // MJICraftworksObject sheet row
    [FieldOffset(0x21)] public fixed byte NumDelivered[9];
    [FieldOffset(0x2A)] public fixed byte Flags1[9]; // 0x1 = delivery done
    [FieldOffset(0x33)] public fixed byte Flags2[9]; // 0x1 = bonus
    [FieldOffset(0x3C)] public fixed byte NumScheduled[9];
    [FieldOffset(0x48)] public int DataAvailability; // 0 = not requested, 1 = requested, 2 = received
}

[StructLayout(LayoutKind.Explicit)]
public unsafe partial struct MJIManagerEx
{
    [FieldOffset(0x168)] public MJIManagerFavors* Favors;

    [FieldOffset(0x2E0)] public ushort RequestDemandCraftId;
    [FieldOffset(0x2E4)] public int RequestDemandType; // 0 = none, 1 = everything, 2 = specific object
    [FieldOffset(0x2E8)] public bool DemandDirty;
}

public class WorkshopFavors
{
    public static unsafe MJIManagerEx* Manager => ((MJIManagerEx*)MJIManager.Instance());
    public static unsafe MJIManagerFavors* FavorsData => Manager->Favors;
    public static unsafe bool DemandDirty => Manager->DemandDirty;
    public unsafe int DataAvailability => FavorsData != null ? FavorsData->DataAvailability : -1;
    public unsafe uint CraftObjectID(int index) => FavorsData != null ? FavorsData->ObjectsIDs[index] : 0u;
    public unsafe int NumDelivered(int index) => FavorsData != null ? FavorsData->NumDelivered[index] : 0;
    public unsafe int NumScheduled(int index) => FavorsData != null ? FavorsData->NumScheduled[index] : 0;
    public unsafe bool DeliveryDone(int index) => FavorsData != null ? (FavorsData->Flags1[index] & 1) != 0 : false;
    public unsafe bool IsBonus(int index) => FavorsData != null ? (FavorsData->Flags2[index] & 1) != 0 : false;
}
