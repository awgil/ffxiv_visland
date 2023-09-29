using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace visland;

public class Service
{
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider Hook { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;

    public static Lumina.GameData LuminaGameData => DataManager.GameData;
    public static T? LuminaRow<T>(uint row) where T : Lumina.Excel.ExcelRow => LuminaGameData.GetExcelSheet<T>(Lumina.Data.Language.English)?.GetRow(row);
}
