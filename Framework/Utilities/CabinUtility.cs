﻿using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using System.Collections.Generic;
using System.Linq;

namespace AnythingAnywhere.Framework.Utilities
{
    internal static class CabinUtility
    {
        public static List<KeyValuePair<string, string>>? GetCabinsToUpgrade(bool toRenovate = false)
        {
            if (!Game1.IsMasterGame)
                return null;

            var cabins = GetCabins();
            List<KeyValuePair<string, string>> cabinPageNames = [];

            foreach (var cabin in cabins)
            {
                bool shouldAddCabin = toRenovate ? cabin.owner.HouseUpgradeLevel >= 2 : cabin.owner.HouseUpgradeLevel < 3;
                if (!shouldAddCabin) continue;

                string msg = Game1.content.LoadString("Strings\\Buildings:Cabin_Name");
                msg = string.IsNullOrEmpty(cabin.owner.Name) ? $"Empty {msg}" : $"{cabin.owner.displayName}'s {msg}";
                cabinPageNames.Add(new KeyValuePair<string, string>(cabin.uniqueName.Value, msg));
            }

            return cabinPageNames;
        }

        public static bool HasCabinsToUpgrade(bool toRenovate = false)
        {
            var cabinsToUpgrade = GetCabinsToUpgrade(toRenovate);
            return cabinsToUpgrade is { Count: > 0 };
        }


        private static List<Cabin> GetCabins()
        {
            List<Cabin> cabins = [];

            foreach (var location in Game1.locations)
            {
                var locationCabins = location.buildings.Where(building => building.isCabin);
                cabins.AddRange(locationCabins.Select(cabin => (Cabin)cabin.indoors.Value));
            }

            return cabins;
        }
    }
}
