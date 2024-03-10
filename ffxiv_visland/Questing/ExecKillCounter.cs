using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using ECommons.DalamudServices;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace visland.Questing;
internal class ExecKillCounter
{
    public static List<(string Name, int Count)> Tally { get; } = [];
    protected static string[] NamesToMatch = [];
    protected static string RegexPattern = string.Empty;

    internal static void Init(string[] namesToMatch)
    {
        Reset();
        NamesToMatch = namesToMatch;
        Service.ChatGui.ChatMessage += ChatGui_ChatMessage;
    }

    internal static void Dispose()
    {
        Service.ChatGui.ChatMessage -= ChatGui_ChatMessage;
        Reset();
    }

    public static void Reset() => Tally.Clear();

    private static void ChatGui_ChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        if ((ushort)type is not 2874 and not 17210 and not 57) return; //2874 = you killed, 17210 = chocobo killed owo
        TryAddFromLogLine(message.ToString());
    }

    private static void TryAddFromLogLine(string msg)
    {
        switch (Service.ClientState.ClientLanguage)
        {
            case Dalamud.ClientLanguage.English:
                RegexPattern = "(?i)(defeat|defeats) the " + string.Join("|", NamesToMatch);
                break;
            case Dalamud.ClientLanguage.Japanese:
                RegexPattern = string.Join("|", NamesToMatch) + "を倒した。";
                break;
            case Dalamud.ClientLanguage.German:
                RegexPattern = "(?i)(hast|hat) .*" + string.Join("|", NamesToMatch);
                break;
            case Dalamud.ClientLanguage.French:
                RegexPattern = "(?i)(a|avez) vaincu .*" + string.Join("|", NamesToMatch);
                break;
        }
        if (Regex.IsMatch(msg, RegexPattern)) FindNameAndAdd(msg);
    }

    private static void FindNameAndAdd(string msg)
    {
        foreach (var name in NamesToMatch)
        {
            if (!Regex.IsMatch(msg.ToLowerInvariant(), name.ToLowerInvariant())) continue;
            AddOne(name);
            return;
        }
    }

    private static void AddOne(string name)
    {
        var index = Tally.FindIndex(i => i.Name == name);
        if (index == -1) return;
        Tally[index] = new(Tally[index].Name, Tally[index].Count + 1);
        Svc.Log.Debug($"Killed {name}, adding one to tracker");
    }
}
