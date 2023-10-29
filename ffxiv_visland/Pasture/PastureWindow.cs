using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.Game.MJI;
using ImGuiNET;
using visland.Helpers;

namespace visland.Pasture;

unsafe class PastureWindow : UIAttachedWindow
{
    private PastureConfig _config;
    private PastureDebug _debug = new();

    public PastureWindow() : base("Pasture Automation", "MJIAnimalManagement", new(400, 600))
    {
        _config = Service.Config.Get<PastureConfig>();
    }

    public override void PreOpenCheck()
    {
        base.PreOpenCheck();
        var agent = AgentMJIAnimalManagement.Instance();
        IsOpen &= agent != null && !agent->UpdateNeeded;
    }

    public override void OnOpen()
    {
        if (_config.Collect != CollectStrategy.Manual)
        {
            var state = CalculateCollectResult();
            if (state == CollectResult.CanCollectSafely || _config.Collect == CollectStrategy.FullAuto && state == CollectResult.CanCollectWithOvercap)
            {
                CollectAll();
            }
        }
    }

    public override void Draw()
    {
        using var tabs = ImRaii.TabBar("Tabs");
        if (tabs)
        {
            using (var tab = ImRaii.TabItem("Main"))
                if (tab)
                    DrawMain();
            using (var tab = ImRaii.TabItem("Debug"))
                if (tab)
                    _debug.Draw();
        }
    }

    private void DrawMain()
    {
        if (UICombo.Enum("Auto Collect", ref _config.Collect))
            _config.NotifyModified();
        ImGui.Separator();

        var mji = MJIManager.Instance();
        var agent = AgentMJIAnimalManagement.Instance();
        if (mji == null || mji->PastureHandler == null || mji->IslandState.Pasture.EligibleForCare == 0 || agent == null)
        {
            ImGui.TextUnformatted("Mammets not available!");
            return;
        }

        DrawGlobalOperations();
    }

    private void DrawGlobalOperations()
    {
        var res = CalculateCollectResult();
        if (res != CollectResult.NothingToCollect)
        {
            // if there's uncollected stuff - propose to collect everything
            using (ImRaii.Disabled(res == CollectResult.EverythingCapped))
            {
                if (ImGui.Button("Collect all"))
                    CollectAll();
                if (res != CollectResult.CanCollectSafely)
                {
                    ImGui.SameLine();
                    using (ImRaii.PushColor(ImGuiCol.Text, 0xff0000ff))
                        Utils.TextV(res == CollectResult.EverythingCapped ? "Inventory is full!" : "Warning: some resources will overcap!");
                }
            }
        }
        else
        {
            // TODO: think about any other global operations?
            Utils.TextV("Nothing to collect!");
        }
    }

    private CollectResult CalculateCollectResult()
    {
        var mji = MJIManager.Instance();
        if (mji == null || mji->PastureHandler == null)
            return CollectResult.NothingToCollect;

        bool haveNone = true;
        bool anyOvercap = false;
        bool allFull = true;
        foreach (var (itemId, count) in mji->PastureHandler->AvailableMammetLeavings)
        {
            if (count <= 0)
                continue;
            haveNone = false;
            var inventory = Utils.NumItems(itemId);
            allFull &= inventory >= 999;
            anyOvercap |= inventory + count > 999;
        }

        return haveNone ? CollectResult.NothingToCollect : allFull ? CollectResult.EverythingCapped : anyOvercap ? CollectResult.CanCollectWithOvercap : CollectResult.CanCollectSafely;
    }

    private void CollectAll()
    {
        var mji = MJIManager.Instance();
        if (mji != null && mji->PastureHandler != null)
        {
            Service.Log.Info("Collecting everything from pasture");
            mji->PastureHandler->CollectLeavingsAll();
        }
    }
}
