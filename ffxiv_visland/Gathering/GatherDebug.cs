using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using visland.Helpers;
using visland.Questing;
using visland.Questing.Presets;
using static visland.Plugin;

namespace visland.Gathering;
public unsafe class GatherDebug(GatherRouteExec exec)
{
    private readonly UITree _tree = new();
    private GatherRouteExec exec = exec;

    public void Draw()
    {
        var qm = QuestManager.Instance();
        foreach (var _ in _tree.Node("Quests", qm->NormalQuestsSpan.Length == 0))
        {
            foreach (var q in qm->NormalQuestsSpan)
            {
                if (q.QuestId is 0) continue;
                foreach (var __ in _tree.Node($"[{q.QuestId}] {QuestsHelper.GetNameOfQuest(q.QuestId)}"))
                {
                    _tree.LeafNode($"Accepted: {qm->IsQuestAccepted(q.QuestId)}");
                    _tree.LeafNode($"Completed: {QuestManager.IsQuestComplete(q.QuestId)}");
                    _tree.LeafNode($"Current Sequence: {QuestManager.GetQuestSequence(q.QuestId)}");
                }
            }
        }

        foreach (var _ in _tree.Node($"Tasks {P.TaskManager.NumQueuedTasks}", select: P.TaskManager.Abort))
        {
            _tree.LeafNode($"Abort", select: P.TaskManager.Abort);

            foreach (var t in P.TaskManager.TaskStack)
            {
                _tree.LeafNode($"{t}");
            }
        }

        _tree.LeafNode($"{nameof(ExecKillHowTos)}: {ExecKillHowTos.IsEnabled}", select: ExecKillHowTos.Toggle);
        _tree.LeafNode($"{nameof(ExecSkipTalk)}: {ExecSkipTalk.IsEnabled}", select: ExecSkipTalk.Toggle);
        _tree.LeafNode($"{nameof(ExecSelectYes)}: {ExecSelectYes.IsEnabled}", select: ExecSelectYes.Toggle);
        _tree.LeafNode($"{nameof(ExecQuestJournalEvent)}: {ExecQuestJournalEvent.IsEnabled}", select: ExecQuestJournalEvent.Toggle);

        _tree.LeafNode($"{nameof(Gridania1_15)}", select: () => Gridania1_15.Run(exec));

        if (ImGui.Button("test")) QuestsHelper.PickUpQuest(67094, 1006950);
    }
}
