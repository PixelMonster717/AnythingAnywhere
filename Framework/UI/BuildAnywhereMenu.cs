﻿using StardewValley;
using StardewValley.GameData.Buildings;
using StardewValley.Menus;


namespace AnythingAnywhere.Framework.UI
{
    internal class BuildAnywhereMenu : CarpenterMenu
    {
        // Prevents Better Juminos from spamming errors
        #pragma warning disable CS0649
        public bool magicalConstruction;
        #pragma warning restore CS0649

        public BuildAnywhereMenu(string builder, GameLocation targetLocation = null) : base(builder, targetLocation)
        {

        }

        public override bool IsValidBuildingForLocation(string typeId, BuildingData data, GameLocation targetLocation)
        {
            if ((typeId == "Cabin" && TargetLocation.Name != "Farm") && !ModEntry.modConfig.EnableCabinsAnywhere)
                return false;

            return true;
        }
    }
}
