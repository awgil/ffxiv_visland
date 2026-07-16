using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using System;
using System.Text.RegularExpressions;

namespace visland.Gathering.AutoGather;

public static unsafe partial class GatheringAddon {
    public sealed class Gathering {
        private readonly AddonGathering* _addon;

        public Gathering(nint addon) => _addon = (AddonGathering*)addon;
        public Gathering(void* addon) => _addon = (AddonGathering*)addon;

        public int CurrentIntegrity => ParseFirstInt(_addon->GetTextNodeById(9)->NodeText.ToString());
        public int TotalIntegrity => ParseFirstInt(_addon->GetTextNodeById(12)->NodeText.ToString());

        public GatheredItem GetItem(int index) => new(this, index);
        public GatheredItem[] Items {
            get {
                var items = new GatheredItem[8];
                for (var i = 0; i < items.Length; i++)
                    items[i] = GetItem(i);
                return items;
            }
        }

        public void Gather(int index) {
            var checkbox = CheckboxAt(index);
            if (checkbox == null || !checkbox->IsEnabled)
                return;
            var values = stackalloc AtkValue[2];
            values[0].Type = AtkValueType.Int;
            values[0].Int = 2;
            values[1].Type = AtkValueType.UInt;
            values[1].UInt = (uint)index;
            ((AtkUnitBase*)_addon)->FireCallback(1, values);
        }

        private AtkComponentCheckBox* CheckboxAt(int index) => index switch {
            0 => _addon->GatheredItemComponentCheckbox[0],
            1 => _addon->GatheredItemComponentCheckbox[1],
            2 => _addon->GatheredItemComponentCheckbox[2],
            3 => _addon->GatheredItemComponentCheckbox[3],
            4 => _addon->GatheredItemComponentCheckbox[4],
            5 => _addon->GatheredItemComponentCheckbox[5],
            6 => _addon->GatheredItemComponentCheckbox[6],
            7 => _addon->GatheredItemComponentCheckbox[7],
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };

        public sealed class GatheredItem(Gathering owner, int index) {
            private AtkComponentCheckBox* CheckBox => owner.CheckboxAt(index);
            public bool IsEnabled => CheckBox->IsEnabled;
            public string ItemName => CheckBox->GetTextNodeById(23)->GetAsAtkTextNode()->NodeText.ToString();
            public uint ItemID => owner._addon->ItemIds[index];
            public bool IsCollectable => Service.DataManager.GetExcelSheet<Item>()?.GetRowOrDefault(ItemID)?.IsCollectable ?? false;
            public int ItemLevel => ParseFirstInt(CheckBox->GetTextNodeById(21)->GetAsAtkTextNode()->NodeText.ToString());
            public int GatherChance => ParseFirstInt(CheckBox->GetTextNodeById(10)->GetAsAtkTextNode()->NodeText.ToString());
            public int BoonChance => ParseFirstInt(CheckBox->GetTextNodeById(16)->GetAsAtkTextNode()->NodeText.ToString());
            public void Gather() => owner.Gather(index);
        }
    }

    public sealed class GatheringMasterpiece {
        private readonly AddonGatheringMasterpiece* _addon;

        public GatheringMasterpiece(nint addon) => _addon = (AddonGatheringMasterpiece*)addon;
        public GatheringMasterpiece(void* addon) => _addon = (AddonGatheringMasterpiece*)addon;

        private AtkUnitBase* Unit => (AtkUnitBase*)_addon;

        public string ItemName => _addon->ItemName->NodeText.ToString();
        public uint ItemID => Unit->AtkValues[2].UInt;
        public int CurrentCollectability => Unit->AtkValues[13].Int;
        public int MaxCollectability => Unit->AtkValues[14].Int;
        public uint CurrentIntegrity => Unit->AtkValues[62].UInt;
        public uint TotalIntegrity => Unit->AtkValues[63].UInt;
        public int ScourPower => Unit->AtkValues[48].Int;
        public int BrazenPowerMin => Unit->AtkValues[49].Int;
        public int BrazenPowerMax => Unit->AtkValues[50].Int;
        public int MeticulousPower => Unit->AtkValues[51].Int;
    }

    private static int ParseFirstInt(string text) {
        var match = Digits().Match(text);
        return match.Success ? int.Parse(match.Value) : 0;
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}
