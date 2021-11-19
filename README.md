# BattleValue
BattleTech 2018 mod which provide function to calculate BV value for units of game

Calculates battle value (BV) for given unit. Implemented as extension function to `MechDef` and `VehicleDef` classes of the BattleTech 2018.
For usage include reference to this module to your project and call `CalculateBattleValue` function with optional parameter of `PilotDef` type.

This mod depends on [CustomComponents](https://github.com/BattletechModders/CustomComponents) and [MechEngineer](https://github.com/BattletechModders/MechEngineer) mods, versions 2.0.

## Settings
Calculations could be customized by `Settings` in `mod.json`

|Option|Description|
|------|-----------|
|ArmorTypes|Array. Lists items that define armor items with its Defensive BV modifications coefficients. Definition contains `ItemID` and `Factor`.|
|StructureTypes|Array. Same format as above|
|EngineTypes|Array. Same as above|
|GyroTypes|Array. Same format as above|
|Specials|Special items sections. Items, which do not have own BV but affects whole calculations. See table below
|DefensiveItems|Array of item BV redefinition. Usually, mod takes BV value from `ItemDef.BattleValue` field of game object, but if user needs to override values for some reasons, it could be done here. Objects in `DefensiveItems` contains 3 fields - `Tag`, `BattleValue` and `AmmoBattleValue`. `Tag` helps identify item in mech' inventory, `BattleValue` defines BV for this item and `AmmoBattleValue` gives BV for ammo of given item
|OffensiveItems|Array. Format - same as before|
|ClanMechTag|Tag value for clan mechs|
|IgnorableUnitTags|List of tags for units that should be ignored by calculator|
|RotaryCannonsTags|Tags to recognize RACs
|UltraCannonsTags|Tags to recognize UACs
|StreakSRMTags|Tags to recognize Streaks
|OneShotWeapons|Tags to recognize One-Shot weapons

### Specials

|Option|Description|
|---|---|
|CASE|Tag for CASE items|
|ArtemisIV|Tag for Artemis FCS systems|
|ArtemisIV_Capable|Tag for missile systems, compatible with Artemis FCS|
|TSM|TSM items tag|
|ProtoTSM|Prototype TSM tag|
|MASC|MASC items tag|
|StealthArmor|Tag for stealth armor

### TODO

- [ ] Vehicle BV calculations. Now it just reflects `BattleValue` field from VehicleDef
- [ ] Adds Pilot skills corrections
- [x] Add additional heat generation from UACs and RACs