using ECommons.EzHookManager;
using ECommons;
using System;
using System.Runtime.InteropServices;
using ECommons.DalamudServices;

namespace visland.Questing;
internal unsafe class Memory
{
    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct JournalCanvasInputData
    {
        [FieldOffset(0)] internal int Unk0;
        [FieldOffset(4)] internal byte Unk4;
        [FieldOffset(6)] internal byte Unk6;
    }

    delegate nint AtkComponentJournalCanvas_ReceiveEventDelegate(nint a1, ushort a2, int a3, JournalCanvasInputData* a4, void* a5);
    EzHook<AtkComponentJournalCanvas_ReceiveEventDelegate> AtkComponentJournalCanvas_ReceiveEventHook;

    internal Memory()
    {
        AtkComponentJournalCanvas_ReceiveEventHook = new("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 41 56 48 83 EC 50 48 8B F1 0F B7 C2", AtkComponentJournalCanvas_ReceiveEventDetour);
    }

    internal void PickRewardItemUnsafe(nint canvas, int index)
    {
        var emptyBytes = stackalloc byte[50];
        var data = stackalloc JournalCanvasInputData[1];
        AtkComponentJournalCanvas_ReceiveEventDetour(canvas, 9, 5 + index, data, emptyBytes);
    }

    nint AtkComponentJournalCanvas_ReceiveEventDetour(nint a1, ushort a2, int a3, JournalCanvasInputData* a4, void* a5)
    {
        var ret = AtkComponentJournalCanvas_ReceiveEventHook.Original(a1, a2, a3, a4, a5);
        try
        {
            var d = (JournalCanvasInputData*)a4;
            Svc.Log.Debug($"AtkComponentJournalCanvas_ReceiveEventDetour: {(nint)a1:X16}, {a2}, {a3}, {(nint)a4:X16} ({d->Unk0}, {d->Unk4}, {d->Unk6}), {(nint)a5:X16}");
        }
        catch (Exception e)
        {
            e.Log();
        }
        return ret;
    }
}
