using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System.Runtime.CompilerServices;
using visland.Helpers;

namespace visland.Granary;

public static unsafe class GranaryUtils
{
    public static MJIGranariesState* State()
    {
        var agent = AgentMJIGatheringHouse.Instance();
        return agent != null ? agent->GranariesState : null;
    }

    public static MJIGranaryState* GetGranaryState(int index)
    {
        var state = State();
        return state != null ? (MJIGranaryState*)Unsafe.AsPointer(ref state->Granary[index]) : null;
    }

    public static void Collect(int index)
    {
        var state = State();
        if (state != null)
        {
            Service.Log.Info($"Gathering from granary {index}");
            state->CollectResources((byte)index);
        }
    }

    // note: make sure to check that expedition is unlocked before calling this
    public static void SelectExpedition(byte granaryIndex, byte expeditionId, byte numDays)
    {
        var gstate = GetGranaryState(granaryIndex);
        if (gstate != null)
        {
            Service.Log.Info($"Selecting expedition {expeditionId} for {numDays} days at granary {granaryIndex}");
            // set current agent fields to emulate user interactions, so that messages are correct
            var confirm = CalculateConfirmation(gstate->ActiveExpeditionId, gstate->RemainingDays, expeditionId, numDays);
            if (confirm == AgentMJIGatheringHouse.Confirmation.None)
            {
                Service.Log.Info($"=> nothing to do, this is already active");
            }
            else if (numDays - gstate->RemainingDays > MaxDays())
            {
                Service.Log.Info($"=> not enough cowries");
            }
            else
            {
                var agent = AgentMJIGatheringHouse.Instance();
                agent->CurGranaryIndex = granaryIndex;
                agent->CurActiveExpeditionId = gstate->ActiveExpeditionId;
                agent->CurActiveDays = gstate->RemainingDays;
                agent->CurHoveredExpeditionId = agent->CurSelectedExpeditionId = expeditionId;
                agent->CurSelectedDays = numDays;
                agent->CurExpeditionName.SetString(agent->Data->Expeditions[expeditionId].Name.ToString());
                agent->ConfirmType = confirm;
                agent->GranariesState->SelectExpeditionCommit(granaryIndex, expeditionId, numDays);
            }
        }
    }

    public static CollectResult CalculateGranaryCollectionState(int index)
    {
        var gstate = GetGranaryState(index);
        if (gstate == null)
            return CollectResult.NothingToCollect;

        var haveAnything = gstate->RareResourceCount > 0;
        var overcapSome = haveAnything && WillOvercap(gstate->RareResourcePouchId, gstate->RareResourceCount);
        var overcapAll = !haveAnything || overcapSome;
        for (var i = 0; i < gstate->NormalResourceCounts.Length; ++i)
        {
            if (gstate->NormalResourceCounts[i] > 0)
            {
                haveAnything = true;
                var overcap = WillOvercap(gstate->NormalResourcePouchIds[i], gstate->NormalResourceCounts[i]);
                overcapSome |= overcap;
                overcapAll &= overcap;
            }
        }
        return !haveAnything ? CollectResult.NothingToCollect : overcapAll ? CollectResult.EverythingCapped : overcapSome ? CollectResult.CanCollectWithOvercap : CollectResult.CanCollectSafely;
    }

    public static AgentMJIGatheringHouse.Confirmation CalculateConfirmation(byte curExpedition, byte curDays, byte newExpedition, byte newDays)
        => curExpedition == newExpedition && curDays >= newDays ? AgentMJIGatheringHouse.Confirmation.None
            : curExpedition == 0 && curDays == 0 ? AgentMJIGatheringHouse.Confirmation.Start
            : curExpedition != newExpedition && curDays < newDays ? AgentMJIGatheringHouse.Confirmation.ChangeExtend
            : curExpedition != newExpedition ? AgentMJIGatheringHouse.Confirmation.Change : AgentMJIGatheringHouse.Confirmation.Extend;

    public static int MaxDays() => Utils.NumCowries() / 50;

    private static bool WillOvercap(uint pouchId, int count) => Utils.NumItems(Service.LuminaRow<MJIItemPouch>(pouchId)!.Value.Item.RowId) + count > 999;
}
