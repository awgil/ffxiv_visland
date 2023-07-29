using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace visland;

public class GatherWindow : Window, IDisposable
{
    private Plugin _plugin;
    private UITree _tree = new();
    private bool _updateDB;
    private GatherRouteExec _exec = new();

    public GatherWindow(Plugin plugin) : base("Island sanctuary automation")
    {
        Size = new Vector2(232, 75);
        SizeCondition = ImGuiCond.Once;
        _plugin = plugin;
    }

    public void Dispose()
    {
        _exec.Dispose();
    }

    public override void PreOpenCheck()
    {
        _exec.Update();
    }

    public override void Draw()
    {
        _exec.Draw(_tree);

        if (ImGui.Button("Save config"))
            _plugin.Config.SaveToFile(_plugin.Dalamud.ConfigFile);

        ImGui.Checkbox("Update database", ref _updateDB);
        ImGui.SameLine();
        if (ImGui.Button("Clear database"))
            _plugin.Config.GatherNodeDB.Clear();
        if (_updateDB)
            _plugin.Config.GatherNodeDB.UpdateFromObjects();
        foreach (var n in _tree.Node("Gathering node database"))
            _plugin.Config.GatherNodeDB.Draw(_tree);

        foreach (var n in _tree.Node("Gathering routes"))
            _plugin.Config.RouteDB.Draw(_tree, _exec);
    }
}
