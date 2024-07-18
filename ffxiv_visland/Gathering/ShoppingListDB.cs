using ECommons.Configuration;
using ImGuiNET;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using visland.Helpers;
using YamlDotNet.Serialization;

namespace visland.Gathering;
public class ShoppingListDB : IEzConfig
{
    public ObservableCollection<ShoppingList> ShoppingLists = [];

    public static void TryImport(ShoppingListDB shoppingListDB)
    {
        try
        {
            var raw = ImGui.GetClipboardText();
            var (IsBase64, yaml) = Utils.FromCompressedBase64(raw);
            ShoppingList? import = EzConfig.DefaultSerializationFactory.Deserialize<ShoppingList>(yaml);
            if (import != null)
            {
                shoppingListDB.ShoppingLists.Add(new() { Name = import!.Name, Group = import.Group });
            }
        }
        catch (JsonReaderException ex)
        {
            Service.ChatGui.PrintError($"Failed to import shopping list: {ex.Message}");
            Service.Log.Error(ex, "Failed to shopping list");
        }
    }

    public static List<string> GetGroups(ShoppingListDB shoppingListDB, bool sort = false)
    {
        List<string> groups = ["Ungrouped"];
        for (var g = 0; g < shoppingListDB.ShoppingLists.Count; g++)
        {
            var listSource = shoppingListDB.ShoppingLists;
            if (string.IsNullOrEmpty(listSource[g].Group))
                listSource[g].Group = "Ungrouped";
            if (!groups.Contains(listSource[g].Group))
                groups.Add(listSource[g].Group);
        }
        if (sort)
            groups = [.. groups.OrderBy(i => i == "Ungrouped").ThenBy(i => i)]; //Sort with None at the End

        return groups;
    }
}

public class ShoppingList
{
    public string Name = string.Empty;
    public string Group = string.Empty;
    public List<Order> Orders = [];
}

public class Order
{
    public uint ItemId;
    public int Quantity;
    public int Group;
    public uint FoodId;
    public uint ManualId;
    public uint MedicineId;
}

public class YamlFactory : ISerializationFactory
{
    public string DefaultConfigFileName => $"{Plugin.Name}ShoppingLists.yaml";

    public T Deserialize<T>(string inputData) => new DeserializerBuilder().IgnoreUnmatchedProperties().Build().Deserialize<T>(inputData);
    public string Serialize(object s, bool prettyPrint) => new SerializerBuilder().Build().Serialize(s);
}
