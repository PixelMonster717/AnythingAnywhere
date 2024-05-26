﻿using StardewModdingAPI;
using StardewValley.Menus;
using StardewValley;
using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewValley.Buildings;
using StardewValley.Locations;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Xna.Framework;
using xTile.Dimensions;
using AnythingAnywhere.Framework.UI;
using StardewValley.Objects;
using Common.Util;

namespace AnythingAnywhere.Framework.Patches.Menus
{
    internal class CarpenterMenuPatch : PatchTemplate
    {
        internal CarpenterMenuPatch(Harmony harmony) : base(harmony, typeof(CarpenterMenu)) { }
        internal void Apply()
        {
            Patch(PatchType.Prefix, nameof(CarpenterMenu.receiveLeftClick), nameof(ReceiveLeftClickPrefix), [typeof(int), typeof(int), typeof(bool)]);
            Patch(PatchType.Prefix, nameof(CarpenterMenu.GetInitialBuildingPlacementViewport), nameof(GetInitialBuildingPlacementViewportPrefix), [typeof(GameLocation)]);
            Patch(PatchType.Transpiler, nameof(CarpenterMenu.draw), nameof(DrawTranspiler), [typeof(SpriteBatch)]);
        }

        private static bool ReceiveLeftClickPrefix(CarpenterMenu __instance, int x, int y, bool playSound = true)
        {
            if (!ModEntry.Config.EnableBuilding)
                return true;

            if (__instance.freeze)
                return true;

            if (!__instance.onFarm)
                return true;

            if (__instance.cancelButton.containsPoint(x, y))
                return true;

            if (!__instance.onFarm && __instance.backButton.containsPoint(x, y))
                return true;

            if (!__instance.onFarm && __instance.forwardButton.containsPoint(x, y))
                return true;

            if (!__instance.onFarm)
                return true;

            if (!__instance.onFarm || __instance.freeze || Game1.IsFading())
                return true;

            GameLocation farm;
            Building destroyed;
            GameLocation interior;
            Cabin cabin;
            if (__instance.demolishing)
            {
                farm = __instance.TargetLocation;
                destroyed = farm.getBuildingAt(new Vector2((Game1.viewport.X + Game1.getOldMouseX(ui_scale: false)) / 64, (Game1.viewport.Y + Game1.getOldMouseY(ui_scale: false)) / 64));
                if (destroyed == null)
                {
                    return false;
                }
                interior = destroyed.GetIndoors();
                cabin = interior as Cabin;
                if (destroyed != null)
                {
                    if (cabin != null && !Game1.IsMasterGame)
                    {
                        Game1.addHUDMessage(new HUDMessage(Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_LockFailed"), 3));
                        destroyed = null;
                        return false;
                    }
                    if (!__instance.CanDemolishThis(destroyed))
                    {
                        destroyed = null;
                        return false;
                    }
                    if (!Game1.IsMasterGame && !__instance.hasPermissionsToDemolish(destroyed))
                    {
                        destroyed = null;
                        return false;
                    }
                }
                Cabin cabin2 = cabin;
                if (cabin2 != null && cabin2.HasOwner && cabin.owner.isCustomized.Value)
                {
                    Game1.currentLocation.createQuestionDialogue(Game1.content.LoadString("Strings\\UI:Carpenter_DemolishCabinConfirm", cabin.owner.Name), Game1.currentLocation.createYesNoResponses(), delegate (Farmer f, string answer)
                    {
                        if (answer == "Yes")
                        {
                            Game1.activeClickableMenu = __instance;
                            Game1.player.team.demolishLock.RequestLock(ContinueDemolish, BuildingLockFailed);
                        }
                        else
                        {
                            DelayedAction.functionAfterDelay(__instance.returnToCarpentryMenu, 500);
                        }
                    });
                }
                else if (destroyed != null)
                {
                    Game1.player.team.demolishLock.RequestLock(ContinueDemolish, BuildingLockFailed);
                }
                return false;
            }

            if (__instance.upgrading)
            {
                Building toUpgrade = __instance.TargetLocation.getBuildingAt(new Vector2((Game1.viewport.X + Game1.getOldMouseX(ui_scale: false)) / 64, (Game1.viewport.Y + Game1.getOldMouseY(ui_scale: false)) / 64));
                if (toUpgrade != null && toUpgrade.buildingType.Value == __instance.Blueprint.UpgradeFrom)
                {
                    __instance.ConsumeResources();
                    toUpgrade.upgradeName.Value = __instance.Blueprint.Id;
                    toUpgrade.daysUntilUpgrade.Value = Math.Max(__instance.Blueprint.BuildDays, 1);
                    toUpgrade.showUpgradeAnimation(__instance.TargetLocation);
                    Game1.playSound("axe");
                    if (!ModEntry.Config.BuildModifier.IsDown() || !__instance.CanBuildCurrentBlueprint())
                    {
                        DelayedAction.functionAfterDelay(__instance.returnToCarpentryMenuAfterSuccessfulBuild, 1500);
                        __instance.freeze = true;
                    }
                    ModEntry.Multiplayer.globalChatInfoMessage("BuildingBuild", Game1.player.Name, "aOrAn:" + __instance.Blueprint.TokenizedDisplayName, __instance.Blueprint.TokenizedDisplayName, Game1.player.farmName.Value);
                    if (__instance.Blueprint.BuildDays < 1)
                    {
                        toUpgrade.FinishConstruction();
                    }
                    else
                    {
                        Game1.netWorldState.Value.MarkUnderConstruction(__instance.Builder, toUpgrade);
                    }
                }
                else if (toUpgrade != null)
                {
                    Game1.addHUDMessage(new HUDMessage(Game1.content.LoadString("Strings\\UI:Carpenter_CantUpgrade_BuildingType"), 3));
                }
                return false;
            }

            if (__instance.painting)
                return true;

            if (__instance.moving)
                return true;

            Game1.player.team.buildLock.RequestLock(delegate
            {
                if (__instance.onFarm && Game1.locationRequest == null)
                {
                    if (__instance.tryToBuild())
                    {
                        __instance.ConsumeResources();
                        if (!ModEntry.Config.BuildModifier.IsDown() || !__instance.CanBuildCurrentBlueprint())
                        {
                            DelayedAction.functionAfterDelay(__instance.returnToCarpentryMenuAfterSuccessfulBuild, 2000);
                            __instance.freeze = true;
                        }
                    }
                    else
                    {
                        Game1.addHUDMessage(new HUDMessage(Game1.content.LoadString("Strings\\UI:Carpenter_CantBuild"), 3));
                    }
                }
                Game1.player.team.buildLock.ReleaseLock();
            });

            void BuildingLockFailed()
            {
                if (__instance.demolishing)
                {
                    Game1.addHUDMessage(new HUDMessage(Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_LockFailed"), 3));
                }
            }

            void ContinueDemolish()
            {
                if (__instance.demolishing && destroyed != null && farm.buildings.Contains(destroyed))
                {
                    if ((int)destroyed.daysOfConstructionLeft.Value > 0 || (int)destroyed.daysUntilUpgrade.Value > 0)
                    {
                        Game1.addHUDMessage(new HUDMessage(Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_DuringConstruction"), 3));
                    }
                    else if (interior is AnimalHouse animalHouse && animalHouse.animalsThatLiveHere.Count > 0)
                    {
                        Game1.addHUDMessage(new HUDMessage(Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_AnimalsHere"), 3));
                    }
                    else if (interior != null && interior.farmers.Any())
                    {
                        Game1.addHUDMessage(new HUDMessage(Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_PlayerHere"), 3));
                    }
                    else
                    {
                        if (cabin != null)
                        {
                            foreach (Farmer farmer in Game1.getAllFarmers())
                            {
                                if (farmer.currentLocation != null && farmer.currentLocation.Name == cabin.GetCellarName())
                                {
                                    Game1.addHUDMessage(new HUDMessage(Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_PlayerHere"), 3));
                                    return;
                                }
                            }
                            if (cabin.IsOwnerActivated)
                            {
                                Game1.addHUDMessage(new HUDMessage(Game1.content.LoadString("Strings\\UI:Carpenter_CantDemolish_FarmhandOnline"), 3));
                                return;
                            }
                        }
                        destroyed.BeforeDemolish();
                        Chest chest = null;
                        if (cabin != null)
                        {
                            List<Item> items = cabin.demolish();
                            if (items.Count > 0)
                            {
                                chest = new Chest(playerChest: true);
                                chest.fixLidFrame();
                                chest.Items.OverwriteWith(items);
                            }
                        }
                        if (farm.destroyStructure(destroyed))
                        {
                            Game1.flashAlpha = 1f;
                            destroyed.showDestroyedAnimation(__instance.TargetLocation);
                            Game1.playSound("explosion");
                            Utility.spreadAnimalsAround(destroyed, farm);
                            if (!ModEntry.Config.BuildModifier.IsDown())
                            {
                                DelayedAction.functionAfterDelay(__instance.returnToCarpentryMenu, 1500);
                                __instance.freeze = true;
                            }
                            if (chest != null)
                            {
                                farm.objects[new Vector2((int)destroyed.tileX.Value + (int)destroyed.tilesWide.Value / 2, (int)destroyed.tileY.Value + (int)destroyed.tilesHigh.Value / 2)] = chest;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool GetInitialBuildingPlacementViewportPrefix(CarpenterMenu __instance, GameLocation location, ref Location __result)
        {
            if (!ModEntry.Config.EnableBuilding)
                return true;

            if (Game1.activeClickableMenu is BuildAnywhereMenu)
            {
                __result = CenterOnTile((int)Game1.player.Tile.X, (int)Game1.player.Tile.Y);
                static Location CenterOnTile(int x, int y)
                {
                    x = (int)((float)(x * 64) - (float)Game1.viewport.Width / 2f);
                    y = (int)((float)(y * 64) - (float)Game1.viewport.Height / 2f);
                    return new Location(x, y);
                }
                return false;
            }

            return true;
        }

        // Don't display gold if buildcost is less than 1 instead of 0
        private static IEnumerable<CodeInstruction> DrawTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase original)
        {
            try
            {
                var matcher = new CodeMatcher(instructions, generator);

                matcher.MatchEndForward(
                    new CodeMatch(OpCodes.Ldloc_0),
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Property(typeof(CarpenterMenu.BlueprintEntry), nameof(CarpenterMenu.BlueprintEntry.BuildCost)).GetGetMethod()),
                    new CodeMatch(OpCodes.Ldc_I4_0))
                    .Set(OpCodes.Ldc_I4_1, null)
                    .ThrowIfNotMatch("Could not find blueprint.BuildCost");

                return matcher.InstructionEnumeration();
            }
            catch (Exception e)
            {
                ModEntry.ModMonitor.Log($"There was an issue modifying the instructions for {typeof(CarpenterMenu)}.{original.Name}: {e}", LogLevel.Error);
                return instructions;
            }
        }
    }
}
