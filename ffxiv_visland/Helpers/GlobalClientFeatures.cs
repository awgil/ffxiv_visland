using Dalamud.Game;

namespace visland.Helpers;

public static class GlobalClientFeatures {
    public static bool IsGlobalClient => (int)Service.ClientState.ClientLanguage <= (int)ClientLanguage.German;

    public static string UnavailableReason
        => "This feature requires a global client (English/French/German/Japanese) for English sheet data.";
}
