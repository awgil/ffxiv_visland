using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;
using visland.Helpers;

namespace visland.Windows;

public class GatherWindow : Window, IDisposable
{
    private Plugin _plugin;
    private UITree _tree = new();
    private GatherRouteExec _exec = new();
    private bool _configModified;

    public GatherRouteExec Exec => _exec;

    public GatherWindow(Plugin plugin) : base("Island sanctuary automation")
    {
        Size = new Vector2(800, 800);
        SizeCondition = ImGuiCond.FirstUseEver;
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

        if (ImGui.Checkbox("Autosave config", ref _plugin.Config.Autosave))
            _configModified = true;

        if (!_plugin.Config.Autosave)
        {
            if (ImGui.Button(_configModified ? "Save modified config" : "Force resave config"))
            {
                _plugin.Config.SaveToFile(_plugin.Dalamud.ConfigFile);
                _configModified = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Reload config"))
            {
                _plugin.Config.LoadFromFile(_plugin.Dalamud.ConfigFile);
                _configModified = false;
            }
        }

        if (ImGui.Checkbox("Stop Route on Error", ref _plugin.Config.DisableOnErrors))
            _configModified = true;

        ImGuiComponents.HelpMarker("Stops executing a route when you encounter a node you can't gather from due to full inventory.");

        _plugin.Config.RouteDB.Draw(_tree, _exec, ref _configModified);

        if (_plugin.Config.Autosave && _configModified)
        {
            _plugin.Config.SaveToFile(_plugin.Dalamud.ConfigFile);
            _configModified = false;
        }
    }
}
