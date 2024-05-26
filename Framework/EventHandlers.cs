﻿using Common.Util;
using StardewModdingAPI.Events;
using StardewModdingAPI;
using StardewValley;
using System.Collections.Generic;
using System.Linq;
using StardewValley.GameData.Locations;
using StardewValley.GameData.Buildings;
using AnythingAnywhere.Framework.UI;
using Common.Managers;
using StardewValley.TokenizableStrings;
using AnythingAnywhere.Framework.Patches.Locations;
using StardewValley.Locations;
using Common.Helpers;
using System.Threading;

namespace AnythingAnywhere.Framework
{
    internal class EventHandlers
    {
        private static bool buildingConfigChanged = false;

        #region Event Subscriptions
        internal void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            ResetBlacklist(true);
        }

        internal void OnBuildingListChanged(object sender, BuildingListChangedEventArgs e)
        {
            if (e.Added.Any())
            {
                ResetBlacklist(true); // For multiplayer
            }
        }

        internal void OnButtonsChanged(object sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady || !ModEntry.Config.EnableBuilding)
                return;

            if (ModEntry.Config.BuildMenu.JustPressed() && ModEntry.Config.EnableBuilding)
                HandleBuildButtonPress("Robin");

            if (ModEntry.Config.WizardBuildMenu.JustPressed() && ModEntry.Config.EnableBuilding)
                HandleBuildButtonPress("Wizard");
        }

        internal void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.Name.IsEquivalentTo("Data/Buildings"))
            {
                e.Edit(
                    asset =>
                    {
                        var data = asset.AsDictionary<string, BuildingData>().Data;
                        foreach (var buildingDataKey in data.Keys.ToList())
                        {
                            data[buildingDataKey] = ModifyBuildingData(data[buildingDataKey], ModEntry.Config.EnableFreeBuild, ModEntry.Config.EnableInstantBuild, ModEntry.Config.RemoveBuildConditions, ModEntry.Config.EnableGreenhouse);
                        }
                    }, AssetEditPriority.Late);
                return;
            }

            if (e.Name.IsEquivalentTo("Data/Locations"))
            {
                e.Edit(
                    asset =>
                    {
                        var data = asset.AsDictionary<string, LocationData>().Data;
                        foreach (var LocationDataKey in data.Keys.ToList())
                        {
                            data[LocationDataKey] = ModifyLocationData(data[LocationDataKey], ModEntry.Config.EnablePlanting);
                        }

                        if (ModEntry.Config.EnablePlanting)
                        {
                            foreach (var location in Game1.locations)
                            {
                                location.Map.Properties.TryAdd("ForceAllowTreePlanting", "T");
                            }
                        }
                    }, AssetEditPriority.Late);
                return;
            }
        }

        internal void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (buildingConfigChanged)
            {
                ModEntry.ModHelper.GameContent.InvalidateCache("Data/Buildings");
                buildingConfigChanged = false;
            }
        }

        internal void OnWarped(object sender, WarpedEventArgs e)
        {
            if (e.OldLocation.Name.StartsWith("ScienceHouse") || e.OldLocation.Name.EndsWith("ScienceHouse") || e.OldLocation.IsOutdoors)
                return;

            if (!(e.OldLocation is Cellar || e.OldLocation is FarmHouse || e.OldLocation is Cabin) && (e.NewLocation is FarmHouse || e.OldLocation is Cabin))
            {
                Game1.player.Position = FarmHousePatch.FarmHouseRealPos * 64f;
                Game1.xLocationAfterWarp = Game1.player.TilePoint.X;
                Game1.yLocationAfterWarp = Game1.player.TilePoint.Y;
            }
        }

        internal void OnConfigChanged(object sender, ConfigChangedEventArgs e)
        {
            if (Equals(e.OldValue, e.NewValue)) return;

            if (e.ConfigName == nameof(ModConfig.EnablePlanting))
            {
                ModEntry.ModHelper.GameContent.InvalidateCache("Data/Locations");
            }

            if (e.ConfigName == nameof(ModConfig.EnableFreeBuild) ||
                e.ConfigName == nameof(ModConfig.EnableInstantBuild) ||
                e.ConfigName == nameof(ModConfig.RemoveBuildConditions) ||
                e.ConfigName == nameof(ModConfig.EnableGreenhouse))
            {
                buildingConfigChanged = true; // Doesn't work if I don't do this
            }
        }

        internal void OnClick(ButtonClickEventArgs e)
        {
            if (e.FieldID.Equals("BlacklistCurrentLocation"))
            {
                if (!Context.IsWorldReady)
                {
                    Game1.playSound("thudStep");
                    return;
                }
                else if (Game1.player.currentLocation.IsFarm)
                {
                    Game1.playSound("thudStep");
                    return;
                }
                else if (ModEntry.Config.BlacklistedLocations.Contains(Game1.player.currentLocation.NameOrUniqueName))
                {
                    Game1.playSound("thudStep");
                    return;
                }
                else
                {
                    Game1.playSound("backpackIN");
                    ModEntry.Config.BlacklistedLocations.Add(Game1.player.currentLocation.NameOrUniqueName);
                    if (Game1.player.currentLocation.IsBuildableLocation())
                    {
                        Game1.currentLocation.Map.Properties.Remove("CanBuildHere");
                    }
                }
            }
            else
            {
                Game1.playSound("backpackIN");
                ConfigUtilities.InitializeDefaultConfig(ModEntry.Config, e.FieldID);
                PageHelper.OpenPage(PageHelper.CurrPage);

                if (e.FieldID.Equals("Building"))
                {
                    ResetBlacklist();
                }
            }
        }
        #endregion

        #region Modify Building Data
        private static BuildingData ModifyBuildingData(BuildingData data, bool enableFreeBuild, bool enableInstantBuild, bool removeBuildConditions, bool enableGreenhouse)
        {
            // Add greenhouse first
            if (enableGreenhouse && IsGreenhouse(data))
                SetGreenhouseAttributes(data);

            if (enableFreeBuild)
                SetFreeBuildAttributes(data);

            if (enableInstantBuild)
                SetInstantBuildAttributes(data);

            if (removeBuildConditions)
                RemoveBuildConditions(data);

            return data;
        }

        private static bool IsGreenhouse(BuildingData data)
        {
            return TokenParser.ParseText(data.Name) == Game1.content.LoadString("Strings\\Buildings:Greenhouse_Name");
        }

        private static void SetGreenhouseAttributes(BuildingData data)
        {
            // Define greenhouse materials
            List<BuildingMaterial> greenhouseMaterials = new List<BuildingMaterial>
            {
                new BuildingMaterial { ItemId = "(O)709", Amount = 100 },
                new BuildingMaterial { ItemId = "(O)338", Amount = 20 },
                new BuildingMaterial { ItemId = "(O)337", Amount = 10 },
            };

            // Set greenhouse attributes
            data.Builder = Game1.builder_robin;
            data.BuildCost = 150000;
            data.BuildDays = 3;
            data.BuildMaterials = greenhouseMaterials;
            data.BuildCondition = "PLAYER_HAS_MAIL Host ccPantry";
        }

        private static void SetFreeBuildAttributes(BuildingData data)
        {
            data.BuildCost = 0;
            data.BuildMaterials = [];
        }

        private static void SetInstantBuildAttributes(BuildingData data)
        {
            data.MagicalConstruction = true;
            data.BuildDays = 0;
        }

        private static void RemoveBuildConditions(BuildingData data)
        {
            data.BuildCondition = "";
        }
        #endregion

        #region Modify Location Data
        private static LocationData ModifyLocationData(LocationData data, bool enablePlanting)
        {
            if (enablePlanting)
            {
                data.CanPlantHere = true;
            }

            return data;
        }
        #endregion

        #region Activate Build Menu
        private void HandleBuildButtonPress(string builder)
        {
            if (Context.IsPlayerFree && Game1.activeClickableMenu == null)
            {
                ActivateBuildAnywhereMenu(builder);
            }
            else if (Game1.activeClickableMenu is BuildAnywhereMenu)
            {
                ResetBlacklist();
                Game1.displayFarmer = true;
                ((BuildAnywhereMenu)Game1.activeClickableMenu).returnToCarpentryMenu();
                ((BuildAnywhereMenu)Game1.activeClickableMenu).exitThisMenu();
            }
        }
        private void ActivateBuildAnywhereMenu(string builder)
        {
            if (!Game1.currentLocation.IsOutdoors && !ModEntry.Config.EnableBuildingIndoors)
            {
                Game1.addHUDMessage(new HUDMessage(I18n.Message("NoBuildingIndoors"), HUDMessage.error_type) { timeLeft = HUDMessage.defaultTime });
                return;
            }

            bool magicInkCheck = !(Game1.getFarmer(Game1.player.UniqueMultiplayerID).hasMagicInk || ModEntry.Config.BypassMagicInk);
            if (builder == "Wizard" && magicInkCheck && !ModEntry.Config.EnableInstantBuild)
            {
                Game1.addHUDMessage(new HUDMessage(I18n.Message("NoMagicInk"), HUDMessage.error_type) { timeLeft = HUDMessage.defaultTime });
                return;
            }

            if (!Game1.currentLocation.IsBuildableLocation())
            {
                Game1.currentLocation.Map.Properties.Add("CanBuildHere", "T");
                Game1.currentLocation.isAlwaysActive.Value = true;
            }

            Game1.activeClickableMenu = new BuildAnywhereMenu(builder, Game1.player.currentLocation);
        }

        #endregion

        #region Blacklist
        internal static void ResetBlacklist(bool setAlwaysActive = false)
        {
            foreach (GameLocation location in Game1.locations)
            {
                if (location.buildings.Any())
                {
                    if (!location.Map.Properties.TryGetValue("CanBuildHere", out var value) || value != "T")
                    {
                        if (ModEntry.Config.BlacklistedLocations != null && ModEntry.Config.BlacklistedLocations.Contains(location.NameOrUniqueName))
                            continue;

                        location.Map.Properties["CanBuildHere"] = "T";
                    }

                    if (setAlwaysActive && location.isAlwaysActive.Value == false)
                    {
                        location.isAlwaysActive.Value = true;
                    }
                }

                if (ModEntry.Config.BlacklistedLocations.Contains(location.NameOrUniqueName))
                {
                    location.Map.Properties.Remove("CanBuildHere");
                }
            }
        }
        #endregion
    }
}
