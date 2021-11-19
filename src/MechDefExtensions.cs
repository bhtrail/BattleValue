using BattleTech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomComponents;
using MechEngineer.Features.OverrideTonnage;
using MechEngineer.Features.ComponentExplosions;
using MechEngineer.Features.Engines;
using MechEngineer.Features.Engines.Helper;
using HBS.Collections;

namespace BattleValue
{
    public static class MechDefExtensions
    {
        public static int CalculateBattleValue(this VehicleDef vehicleDef, PilotDef? pilotDef = null)
        {
            return vehicleDef.BattleValue;
        }

        public static int CalculateBattleValue(this MechDef mechDef, PilotDef? pilotDef = null)
        {
            if (mechDef == null) return 0;

            if (mechDef.MechTags == null || mechDef.MechTags.ContainsAny(Core.IgnorableTagSet))
            {
                return 0;
            }
            
            var core_sets = Core.Settings;

            float ArmorTypeModifier = 1.0f;
            float StructureTypeModifier = 1.0f;
            float EngineTypeModifier = 1.0f;
            float GyroTypeModifier = 0.5f;

            int BV = 0;
            var mechStructurePoints = (float)Math.Round(mechDef.MechDefMaxStructure / 5.0f);
            var mechArmorPoints = (float)Math.Round(mechDef.MechDefAssignedArmor / 5.0f);

            var mechEngine = mechDef.GetEngine();

            if (mechEngine == null)
            {
                Core.LogError("No Engine found");
                return 0;
            }

            var isClanMech = IsClanMech(mechDef);
            EngineType engineType = EngineType.Standard;

            // Calculate Defensive Value
            // Get armor type modifier
            ArmorTypeModifier = GetItemFactor(mechDef.Inventory, core_sets.ArmorTypes, ArmorTypeModifier);
            StructureTypeModifier = GetItemFactor(mechDef.Inventory, core_sets.StructureTypes, StructureTypeModifier);
            GyroTypeModifier = GetItemFactor(mechDef.Inventory, core_sets.GyroTypes, GyroTypeModifier);

            // Get engine type modifer
            // First - attempt find overrided item from settings
            bool factorHasBeenRedefined = false;
            (EngineTypeModifier, factorHasBeenRedefined) = GetItemFactorModified(mechDef.Inventory, core_sets.EngineTypes);

            // Second - attempt to find weights item (typically placed in engine type items)
            if (!factorHasBeenRedefined)
            {
                var weightInfo = mechEngine.Weights;

                var reservedSlots = weightInfo.ReservedSlots;
                var engineWeightRate = weightInfo.EngineFactor;
                Core.Log($"Got Engine item with ID = {mechEngine.CoreDef.Def.Description.Id}");

                if (reservedSlots != 0 || engineWeightRate != 1.0f)
                {
                    (EngineTypeModifier, engineType) = ClassifyEngine(reservedSlots, engineWeightRate);
                }
            }

            var DefensiveBV = (mechArmorPoints * 2.5f * ArmorTypeModifier) +
                (mechStructurePoints * 1.5f * StructureTypeModifier * EngineTypeModifier) +
                mechDef.Chassis.Tonnage * GyroTypeModifier;

            // TODO : Add calc for positive or negative defence factors
            // Positive factors
            // Get CASE, TSM and other specials
            var CASELocations = mechDef.Inventory.Where(item => ClassifyItem(item, core_sets.Specials.CASE) || item.Is<CASEComponent>())
                .Select(item => item.MountedLocation);

            var hasTSM = mechDef.Inventory.Any(item => ClassifyItem(item, new string[] { core_sets.Specials.TSM, core_sets.Specials.ProtoTSM }));

            var hasMASC = mechDef.Inventory.Any(item => ClassifyItem(item, core_sets.Specials.MASC));

            Core.Log($"Has CASE on {string.Join(", ", CASELocations)}, TSM:{hasTSM}, MASC:{hasMASC}");

            var jumpJetsCount = mechDef.Inventory.Count(item => item.ComponentDefType == ComponentType.JumpJet);

            var ammoBoxes = mechDef.Inventory.Where(item => (item.ComponentDefType == ComponentType.AmmunitionBox));
            // enumerate defense items
            foreach (var defItem in core_sets.DefensiveItems)
            {
                var items = mechDef.Inventory.Where(item => ClassifyItem(item, defItem.Tag));
                var itemsCount = items.Count();

                if (itemsCount > 0)
                {
                    Core.Log($"Found {itemsCount} of {defItem.Tag}");
                    // Check defensive ammo
                    int ammoCount = 0;
                    if (defItem.AmmoBattleValue != 0)
                    {
                        var ammoCat = (items.First().Def as WeaponDef)?.AmmoCategoryValue;
                        if (ammoCat != null)
                        {
                            ammoCount = ammoBoxes.Count(item => (item.Def as AmmunitionBoxDef)?.Ammo.AmmoCategoryValue == ammoCat);

                            Core.Log($"Found {ammoCount} of {ammoCat.FriendlyName} ammo");
                        }
                    }

                    DefensiveBV += ((itemsCount * defItem.BattleValue) + (ammoCount * defItem.AmmoBattleValue));
                }
            }

            var explosiveAmmo = mechDef.Inventory.Where(item => item.Is<ComponentExplosion>() && item.ComponentDefType == ComponentType.AmmunitionBox);
            var explosiveWeaponry = mechDef.Inventory.Where(item => item.Is<ComponentExplosion>() && item.ComponentDefType == ComponentType.Weapon);

            // Ammo for CT, Legs, Head for Clan Mechs
            if (isClanMech)
            {
                var itemsCount = explosiveAmmo.Count(item => CheckExplosiveItemInLocations(item));

                Core.Log($"Found {itemsCount} danger explosive items in ClanMech");

                DefensiveBV -= 15 * itemsCount;
            }
            else if (engineType == EngineType.XL)
            {
                // Ammo in any loc for IS mech
                DefensiveBV -= 15 * explosiveAmmo.Count();
                Core.Log($"Found {explosiveAmmo.Count()} danger explosive ammo in IS Mech with XL engine");
            }
            else
            {
                var expCount = explosiveAmmo.Count(item => CheckExplosiveItem(item, CASELocations));
                var expCountInArms = explosiveAmmo.Count(item => CheckExplosiveItemInArms(item, CASELocations));
                Core.Log($"Found {expCount} explosive Ammo in H/CT/Legs or not protected by CASE and {expCountInArms} in arms with adjacent torso not protected with CASE");

                DefensiveBV -= 15 * (expCount + expCountInArms);
            }

            // Gauss and other explosive equipment
            if (isClanMech)
            {
                var critsCount = explosiveWeaponry.Where(item => CheckExplosiveItemInLocations(item)).Select(item => item.Def.InventorySize).Sum();

                Core.Log($"Found {critsCount} of explosive weaponry in ClanMech");
                DefensiveBV -= critsCount;
            }
            else if (engineType == EngineType.XL)
            {
                var critCounts = explosiveWeaponry.Select(item => item.Def.InventorySize).Sum();
                Core.Log($"Found {critCounts} of explosive weaponry in IS Mech with XL engine");
                DefensiveBV -= critCounts;
            }
            else
            {
                var critCounts = explosiveWeaponry.Where(item => CheckExplosiveItem(item, CASELocations)).Select(item => item.Def.InventorySize).Sum();
                var critCountsInArms = explosiveWeaponry.Where(item => CheckExplosiveItemInArms(item, CASELocations)).Select(item => item.Def.InventorySize).Sum();

                Core.Log($"Found {critCounts} of explosive weaponry in H/CT/Legs or not protected by CASE and {critCountsInArms} in arms with adjacent location not protected with CASE");
                DefensiveBV -= (critCounts + critCountsInArms);
            }

            // Mech Defence factor
            var movement = mechEngine.CoreDef.GetMovement(mechDef.Chassis.Tonnage);

            jumpJetsCount = Math.Min(jumpJetsCount, movement.JumpJetCount);

            Core.Log($"Walk MP:{movement.WalkMovementPoint}, Run MP:{movement.RunMovementPoint}, JumpJets MP: {movement.JumpJetCount}");

            var maxMove = Math.Max(jumpJetsCount, (int)Math.Max(movement.WalkMovementPoint, movement.RunMovementPoint));
            var defenceFactor = GetDefenceFactor(maxMove);

            Core.Log($"Defence Factor {defenceFactor} for {maxMove} move");

            DefensiveBV *= defenceFactor;

            // Calculate Offensive Value
            float OffensiveBV = 0;

            // Get all weapons
            var weps = mechDef.Inventory.Where(item => item.ComponentDefType == ComponentType.Weapon).Select(item => item.Def as WeaponDef);
            var ammo = mechDef.Inventory.Where(item => item.ComponentDefType == ComponentType.AmmunitionBox).Select(item => item.Def as AmmunitionBoxDef);

            Core.Log($"Weapons count : {weps.Count()}, ammo count : {ammo.Count()}");
            // Is Artemis on board
            var hasArtemis = mechDef.Inventory.Any(item => ClassifyItem(item, Core.Settings.Specials.ArtemisIV));
            var hasStealth = mechDef.Inventory.Any(item => ClassifyItem(item, Core.Settings.Specials.StealthArmor));

            Core.Log($"Has Artemis : {hasArtemis}, has stealth : {hasStealth}");

            // Gets Cooling parameters
            // Total heatsinks on mech
            var totalHeatSinks = Engine.MatchingCount(mechDef.Inventory, mechEngine.HeatSinkDef.Def);
            var engineHeatsinks = mechEngine.CoreDef.Rating / 25;

            Core.Log($"Total Heatsinks {totalHeatSinks}, engine heat sinks {engineHeatsinks}");

            if (engineHeatsinks <= 10)
            {
                //remove engine external heatsinks from total count
                totalHeatSinks -= 10 - engineHeatsinks;
            }
            else
            {
                // Do we have engine heat sinks installed?
                totalHeatSinks += mechEngine.HeatBlockDef.HeatSinkCount;
            }
            if (totalHeatSinks < 0)
            {
                totalHeatSinks = 0;
            }

            Core.Log($"Total Heatsinks {totalHeatSinks} corrected, engine heat sinks {engineHeatsinks}");

            var movementHeat = 2;
            if (movement.JumpJetCount > 0)
            {
                movementHeat = Math.Min(3, movement.JumpJetCount);
            }
            var heatPerHeatsink = 1;
            if (mechEngine.HeatSinkDef.Def.DissipationCapacity > 3)
            {
                heatPerHeatsink = 2;
            }

            if (hasStealth)
            {
                movementHeat += 10;
            }

            Core.Log($"Movement Heat {movementHeat}, Heat per heatsink {heatPerHeatsink}");

            var heatDissipation = 6 + (10 + totalHeatSinks) * heatPerHeatsink - movementHeat;

            var totalWeaponHeat = weps.Sum(item => item?.HeatGenerated ?? 0) / 3;

            Core.Log($"Heat Dissipation : {heatDissipation}, Total Weapon Heat : {totalWeaponHeat}");

            var groupedByAmmoType = weps.GroupBy(item => item?.AmmoCategoryValue);
            // Calculcate Ammo BV
            foreach (var group in groupedByAmmoType)
            {
                // TODO : Add modification of Wep BV by outside factors (like Artemis, TComp, etc)
                var totalWeapBV = group.Sum(item => ModifyWeaponBV(item, hasArtemis));
                var totalAmmoBV = (float)ammo.Where(item => item?.Ammo.AmmoCategoryValue == group.Key).Sum(item => item?.BattleValue ?? 0);

                Core.Log($"Ammo Category Group: {group.Key?.FriendlyName}, Weapon BV: {totalWeapBV}, Ammo BV: {totalAmmoBV}");
                if (totalAmmoBV > totalWeapBV)
                {
                    totalAmmoBV = totalWeapBV;
                }

                OffensiveBV += totalAmmoBV;
            }

            var coldWeps = weps.Where(item => item?.HeatGenerated == 0);
            var hotWeps = weps.Where(item => item?.HeatGenerated != 0)
                .OrderByDescending(item => item?.BattleValue).ThenBy(item => item?.HeatGenerated);

            Core.Log($"\"Cold\" Weapons count: {coldWeps.Count()}, \"Hot\" Weapons count: {hotWeps.Count()}");

            // Add BV for cold weapons
            var coldWepBV = coldWeps.Sum(item => item?.BattleValue ?? 0);
            OffensiveBV += coldWepBV;

            float runningHeat = 0;
            foreach (var wep in hotWeps)
            {
                var wepHeat = (wep?.HeatGenerated ?? 0) / 3; // normalize to TT values
                var wepBV = ModifyWeaponBV(wep, hasArtemis);

                Core.Log($"Weapon : {wep?.Description.Name}, Heat :{wepHeat}, BV: {wepBV}, Running Heat : {runningHeat}");

                if (runningHeat > heatDissipation)
                {
                    wepBV /= 2;
                }

                runningHeat += wepHeat;
                OffensiveBV += wepBV;
            }

            OffensiveBV += mechDef.Chassis.Tonnage;

            // TODO: multiply for movement factor
            var movementFactor = (int)(movement.RunMovementPoint + Math.Round(jumpJetsCount / 2.0f));
            var moveFactorValue = MovementRateFactor[movementFactor];
            Core.Log($"Movement rate: {movementFactor}, Factor value : {moveFactorValue}");

            OffensiveBV *= moveFactorValue;

            Core.Log($"Offensive BV : {OffensiveBV}");


            BV = (int)Math.Round(DefensiveBV + OffensiveBV);
            // Correct on Pilot's skill value
            return BV;
        }

        private static float ModifyWeaponBV(WeaponDef? weaponDef, bool hasArtemis)
        {
            if (weaponDef == null)
                return 0f;

            if (weaponDef.IsCategory(Core.Settings.Specials.ArtemisIV_Capable) && hasArtemis)
            {
                return weaponDef.BattleValue * 1.2f;
            }
            return weaponDef.BattleValue;
        }

        private static readonly int[] TMMToBonusTable = { 2, 4, 6, 9, 17, 24, 500 };
        private static readonly float[] MovementRateFactor = {
            0.44f, 0.54f, 0.65f, 0.77f, 0.88f, 1.00f, 1.12f, 1.24f, 1.37f, 1.50f, 1.63f, 1.76f, 1.89f,
            2.02f, 2.16f, 2.30f, 2.44f, 2.58f, 2.72f, 2.86f, 3.00f, 3.15f, 3.29f, 3.44f, 3.59f, 3.47f
        };

        private static float GetDefenceFactor(int TMM)
        {
            int TMMBonus = 6;
            for (int i = 0; i < TMMToBonusTable.Length; ++i)
            {
                if (TMM <= TMMToBonusTable[i])
                {
                    TMMBonus = i;
                    break;
                }
            }

            return 1.0f + (TMMBonus / 10.0f);
        }

        internal static bool CheckExplosiveItemInLocations(MechComponentRef compRef)
        {
            return compRef.MountedLocation == ChassisLocations.Head ||
                compRef.MountedLocation == ChassisLocations.CenterTorso ||
                compRef.MountedLocation == ChassisLocations.RightLeg ||
                compRef.MountedLocation == ChassisLocations.LeftLeg;
        }

        internal static bool CheckExplosiveItem(MechComponentRef compRef, IEnumerable<ChassisLocations> caseLocations)
        {
            return CheckExplosiveItemInLocations(compRef) ||
                !caseLocations.Any(location => compRef.MountedLocation == location);
        }

        internal static bool CheckExplosiveItemInArms(MechComponentRef compRef, IEnumerable<ChassisLocations> caseLocations)
        {
            return (compRef.MountedLocation == ChassisLocations.LeftArm && !caseLocations.Any(item => item == ChassisLocations.LeftTorso)) ||
                (compRef.MountedLocation == ChassisLocations.RightArm && !caseLocations.Any(item => item == ChassisLocations.RightTorso));
        }

        internal static float GetItemFactor(IEnumerable<MechComponentRef> inventory, IEnumerable<ItemFactorDef> factorDefs, float defValue = 1.0f)
        {
            var (factor, _) = GetItemFactorModified(inventory, factorDefs, defValue);
            return factor;
        }

        internal static (float, bool) GetItemFactorModified(IEnumerable<MechComponentRef> inventory, IEnumerable<ItemFactorDef> factorDefs, float defValue = 1.0f)
        {
            foreach (var factorDef in factorDefs)
            {
                if (inventory.Any(item => item.ComponentDefID == factorDef.ItemID))
                {
                    return (factorDef.Factor, true);
                }
            }
            return (defValue, false);
        }

        internal static bool IsClanMech(MechDef mechdef)
        {
            return mechdef.MechTags.Any(item => item == Core.Settings.ClanMechTag) || mechdef.Chassis.ChassisTags.Any(item => item == Core.Settings.ClanMechTag);
        }

        internal static bool ClassifyItem(MechComponentRef mechComponent, IEnumerable<string> tagsToCheck)
        {
            foreach (var tag in tagsToCheck)
            {
                if (ClassifyItem(mechComponent, tag))
                    return true;
            }
            return false;
        }

        internal static bool ClassifyItem(MechComponentRef mechComponent, string tagToCheck)
        {
            return mechComponent.IsCategory(tagToCheck) || mechComponent.ComponentDefID == tagToCheck;
        }

        internal static (float, EngineType) ClassifyEngine(int reservedSlots, float weightRate)
        {
            float factor = 1.0f;
            EngineType engType = EngineType.Standard;
            if (reservedSlots != 0)
            {
                if (reservedSlots == 4 && weightRate == 0.75f)
                {
                    // light IS engine
                    factor = 0.75f;
                    engType = EngineType.Light;
                }
                else if (reservedSlots == 4 && weightRate == 0.5f)
                {
                    // clan XL engine
                    factor = 0.75f;
                    engType = EngineType.XL;
                }
                else if (reservedSlots == 6 && weightRate == 0.5f)
                {
                    // IS XL engine
                    factor = 0.5f;
                    engType = EngineType.XL;
                }
            }
            // standard and compact engines has engine factor 1
            return (factor, engType);
        }
    }

    internal enum EngineType
    {
        Standard,
        Light,
        XL
    }
}
