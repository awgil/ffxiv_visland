using AutoRetainerAPI;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;
using System;
using System.Linq;
using visland.Helpers;
using visland.IPC;

namespace visland;

public class Service
{
    [PluginService] public static IDalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider Hook { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] public static IGameConfig GameConfig { get; private set; } = null!;

    public static Lumina.GameData LuminaGameData => DataManager.GameData;
    public static Lumina.Excel.ExcelSheet<T>? LuminaSheet<T>() where T : struct, Lumina.Excel.IExcelRow<T> => LuminaGameData?.GetExcelSheet<T>(Lumina.Data.Language.English);
    public static T? LuminaRow<T>(uint row) where T : struct, Lumina.Excel.IExcelRow<T> => LuminaSheet<T>()?.GetRowOrDefault(row);

    public static Configuration Config = new();
    public static Retainers Retainers = new();

    internal static bool IsInitialized = false;
    public static void Init(IDalamudPluginInterface pi)
    {
        if (IsInitialized)
        {
            Log.Debug("Services already initialized, skipping");
        }
        IsInitialized = true;
        try
        {
            pi.Create<Service>();
        }
        catch (Exception ex)
        {
            Log.Error($"Error initalising {nameof(Service)}", ex);
        }
    }
}

public class Retainers
{
    public AutoRetainerApi API = null!;
    public AutoRetainerIPC IPC = null!;
    public Retainers()
    {
        API = new();
        IPC = new();
    }

    public ulong StartingCharacter = 0;
    public bool Finished
    {
        get
        {
            try
            {
                return IPC.GetMultiEnabled() && !IPC.IsBusy() && PlayerEx.CID == StartingCharacter && !HasRetainersReady && !HasSubsReady;
            }
            catch (IpcNotReadyError)
            {
                return false;
            }
        }
    }

    public ulong PreferredCharacter
    {
        get
        {
            try
            {
                return API.GetRegisteredCharacters().FirstOrDefault(c => API.GetOfflineCharacterData(c).Preferred);
            }
            catch (IpcNotReadyError)
            {
                return 0;
            }
        }
    }

    public bool HasRetainersReady
    {
        get
        {
            try
            {
                return API.GetRegisteredCharacters().Where(c => API.GetOfflineCharacterData(c).Enabled)
                .Any(character => API.GetOfflineCharacterData(character).RetainerData.Any(x => x.HasVenture && x.VentureEndsAt <= DateTime.Now.ToUnixTimestamp()));
            }
            catch (IpcNotReadyError)
            {
                return false;
            }
        }
    }

    public bool HasSubsReady
    {
        get
        {
            try
            {
                return API.GetRegisteredCharacters().Where(c => API.GetOfflineCharacterData(c).Enabled)
                .Any(c => API.GetOfflineCharacterData(c).OfflineSubmarineData.Any(x => API.GetOfflineCharacterData(c).EnabledSubs.Contains(x.Name) && x.ReturnTime <= DateTime.Now.ToUnixTimestamp()));
            }
            catch (IpcNotReadyError)
            {
                return false;
            }
        }
    }

    public uint NextReturn
    {
        get
        {
            try
            {
                return API.GetRegisteredCharacters().Where(c => API.GetOfflineCharacterData(c).Enabled).Min(c => API.GetOfflineCharacterData(c).OfflineSubmarineData.Min(x => x.ReturnTime));
            }
            catch (IpcNotReadyError)
            {
                return 0;
            }
        }
    }

    public ulong GetPreferredCharacter()
    {
        try
        {
            return API.GetRegisteredCharacters().FirstOrDefault(c => API.GetOfflineCharacterData(c).Preferred);
        }
        catch (IpcNotReadyError)
        {
            return 0;
        }
    }

    private ulong TempCharacter = 0;
    public void TempSwapPreferred(ulong cid, bool swapback)
    {
        if (swapback)
        {
            API.GetOfflineCharacterData(cid).Preferred = false;
            API.GetOfflineCharacterData(TempCharacter).Preferred = true;
        }
        else
        {
            TempCharacter = PreferredCharacter;
            API.GetOfflineCharacterData(PreferredCharacter).Preferred = false;
            API.GetOfflineCharacterData(cid).Preferred = true;
        }
    }
}