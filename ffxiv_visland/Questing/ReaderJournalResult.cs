using ECommons.UIHelpers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Collections.Generic;
using static ECommons.GenericHelpers;

namespace visland.Questing;
internal unsafe class ReaderJournalResult(AtkUnitBase* UnitBase, int BeginOffset = 0) : AtkReader(UnitBase, BeginOffset)
{
    internal List<OptionalReward> OptionalRewards => Loop<OptionalReward>(57, 1, 5);

    internal unsafe class OptionalReward(nint UnitBasePtr, int BeginOffset = 0) : AtkReader(UnitBasePtr, BeginOffset)
    {
        internal uint ItemID => (ReadUInt(0) ?? 0) % 1000000;
        internal bool IsHQ => (ReadUInt(0) ?? 0) > 1000000;
        internal uint IconID => ReadUInt(5) ?? 0;
        internal uint Amount => ReadUInt(10) ?? 0;
        internal string Name => ReadSeString(15)?.ExtractText() ?? "";
    }
}