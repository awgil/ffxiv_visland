using ECommons.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace visland.IPC;

public static class ItemVendorLocation
{
    public static List<Location> GetVendorLocations(uint itemId)
    {
        var locations = new List<Location>();
        if (DalamudReflector.TryGetDalamudPlugin("ItemVendorLocation", out var pl, false, true))
        {
            var itemLookup = pl.GetFoP("_itemLookup");
            var itemInfo = itemLookup.Call("GetItemInfo", [itemId]);
            if (itemInfo != null)
            {
                var npcinfos = (IEnumerable)itemInfo.GetFoP("NpcInfos");
                if (npcinfos != null)
                {
                    foreach (var npc in npcinfos)
                    {
                        var npcid = (uint)npc.GetFoP("Id");
                        var location = npc.GetFoP("Location");
                        locations.Add(new Location(npcid, (float)location.GetFoP("X"), (float)location.GetFoP("Y"), (uint)location.GetFoP("TerritoryType")));
                    }
                }
            }
        }
        return locations;
    }

    public static List<Location> GetVendorLocations(int itemId) => GetVendorLocations((uint)itemId);

    public class Location(uint npcid, float x, float y, uint territory)
    {
        public uint NPCID = npcid;
        public float X = x;
        public float Y = y;
        public uint TerritoryType = territory;
    }
}