using Dalamud.Interface.Utility.Raii;
using visland.Helpers;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace visland.Workshop;

unsafe class WorkshopWindow : UIAttachedWindow {
    private readonly WorkshopConfig _config;
    private readonly WorkshopManual _manual = new();
    private readonly WorkshopOCImport _oc = new();
    private readonly WorkshopDebug _debug = new();

    public WorkshopWindow() : base("Workshop automation", "MJICraftSchedule", new(500, 650)) {
        _config = Service.Config.Get<WorkshopConfig>();
    }

    public override void PreOpenCheck() {
        base.PreOpenCheck();
        var agent = AgentMJICraftSchedule.Instance();
        IsOpen &= agent != null && agent->Data != null;

        _oc.Update();
    }

    public override void Draw() {
        using var tabs = ImRaii.TabBar("Tabs");
        if (tabs) {
            using (var tab = ImRaii.TabItem("Schedule"))
                if (tab)
                    _oc.Draw();
            using (var tab = ImRaii.TabItem("Manual schedule"))
                if (tab)
                    _manual.Draw();
            using (var tab = ImRaii.TabItem("Settings"))
                if (tab)
                    DrawSettings();
            using (var tab = ImRaii.TabItem("Debug"))
                if (tab)
                    _debug.Draw();
        }
    }

    public override void OnOpen() {
        if (_config.AutoOpenNextDay) {
            WorkshopUtils.SetCurrentCycle(AgentMJICraftSchedule.Instance()->Data->CycleInProgress + 1);
        }
        if (_config.FavorMode == WorkshopFavorMode.MinMaxFreeRestDay)
            WorkshopUtils.VoidSecondRestThisWeek();
        if (_config.AutoImport && GlobalClientFeatures.IsGlobalClient) {
            _oc.LoadSeasonRecs(false, silent: true);
        }
    }

    private void DrawSettings() {
        if (ImGui.Checkbox("Automatically select next cycle on open", ref _config.AutoOpenNextDay))
            _config.NotifyModified();
        using (ImRaii.Disabled(!GlobalClientFeatures.IsGlobalClient)) {
            if (ImGui.Checkbox("Automatically load archive recs on open", ref _config.AutoImport))
                _config.NotifyModified();
            if (!GlobalClientFeatures.IsGlobalClient)
                ImGui.TextWrapped(GlobalClientFeatures.UnavailableReason);
        }

        ImGui.Separator();
        ImGui.TextUnformatted("Favor integration");
        var mode = (int)_config.FavorMode;
        var modes = new[] {
            "None — OC schedule only",
            "Replace workshop 4 — credit favors already in WS1-3",
            "Min-max — substitutions + sacrifice low-value slots",
            "Min-max + free rest day — craft on OC's second rest day",
        };
        if (ImGui.Combo("##favorMode", ref mode, modes, modes.Length)) {
            _config.FavorMode = (WorkshopFavorMode)mode;
            _config.NotifyModified();
        }
        ImGui.TextWrapped(_config.FavorMode switch {
            WorkshopFavorMode.None => "Loads the archived Overseas Casuals schedule as-is. Use manual favor overrides if needed.",
            WorkshopFavorMode.ReplaceWorkshop4 => "Workshops 1-3 keep the archive schedule. Workshop 4 is filled from the built-in favor solver, after crediting any favor crafts already produced by the recommended agenda.",
            WorkshopFavorMode.MinMax => "Tries same-duration/category substitutions first, then places remaining favors on the lowest-value workshop slots so high-cowrie days stay intact when possible.",
            WorkshopFavorMode.MinMaxFreeRestDay => "Same as min-max, but turns the archive's second rest day into a crafting day (C1 stays rest) so most favors can land on a \"free\" day.",
            _ => "",
        });

        ImGui.Separator();
        if (ImGui.Checkbox("Show advanced favor override controls", ref _config.UseFavorSolver))
            _config.NotifyModified();
        ImGui.TextWrapped("Shows manual favor-solver / clipboard override buttons on the Schedule tab.");
    }
}
