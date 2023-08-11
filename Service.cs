using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.IoC;

namespace visland;

public class Service
{
    [PluginService] public static CommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static DataManager DataManager { get; private set; } = null!;
    [PluginService] public static ObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static TargetManager TargetManager { get; private set; } = null!;
    [PluginService] public static ClientState ClientState { get; private set; } = null!;
    [PluginService] public static SigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static Condition Condition { get; private set; } = null!;
    [PluginService] public static GameGui GameGui { get; private set; } = null!;
    [PluginService] public static ChatGui ChatGui { get; private set; } = null!;

    public static Lumina.GameData LuminaGameData => DataManager.GameData;
    public static T? LuminaRow<T>(uint row) where T : Lumina.Excel.ExcelRow => LuminaGameData.GetExcelSheet<T>(Lumina.Data.Language.English)?.GetRow(row);
}
