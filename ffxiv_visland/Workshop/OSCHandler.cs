namespace visland.Workshop;

public static class OSCHandler {
    public static string OfficialNameToBotName(string name) {
        if (name.StartsWith("Isleworks "))
            return name[10..];
        if (name.StartsWith("Islefish "))
            return name[9..];
        if (name.StartsWith("Island "))
            return name[7..];
        if (name == "Mammet of the Cycle Award")
            return "Mammet Award";
        return name;
    }
}
