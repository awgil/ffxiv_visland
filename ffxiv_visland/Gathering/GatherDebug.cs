using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using System.Collections.Generic;
using System.Linq;
using visland.Helpers;
using visland.Questing;
using visland.Questing.Presets;
using static visland.Plugin;

namespace visland.Gathering;
public unsafe class GatherDebug(GatherRouteExec exec)
{
    private readonly UITree _tree = new();
    private GatherRouteExec exec = exec;
    private List<List<string>> _pluginlists = new();

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

        if (ImGui.Button("Get Loaded Plugins"))
            ImGui.SetClipboardText($"LOADED PLUGINS\n================\n{string.Join("\n", Service.Interface.InstalledPlugins.Where(p => p.IsLoaded).Select(p => p.Name).OrderBy(name => name))}");
        ImGui.SameLine();
        if (ImGui.Button("Add Plugin List"))
        {
            var clip = ImGui.GetClipboardText();
            if (clip != null)
            {
                _pluginlists.Add([.. clip.Split("\n")]);
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("clear"))
            _pluginlists.Clear();

        // find common elements across _pluginlists
        if (_pluginlists.Count > 1)
        {
            var common = _pluginlists
                .Skip(1)
                .Aggregate(new HashSet<string>(_pluginlists.First()), (h, e) =>
                {
                    h.IntersectWith(e);
                    return h;
                });
            if (ImGui.Button("Export"))
                ImGui.SetClipboardText(string.Join("\n", common));
            ImGui.TextUnformatted($"Lists checking: {_pluginlists.Count}");
            ImGui.TextUnformatted($"{string.Join("\n", common)}");
        }
    }
}
