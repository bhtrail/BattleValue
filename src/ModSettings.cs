using System.Collections.Generic;

namespace BattleValue
{
    internal class ModSettings
    {
        public List<ItemFactorDef> ArmorTypes { get; private set; } = new List<ItemFactorDef>();
        public List<ItemFactorDef> StructureTypes { get; private set; } = new List<ItemFactorDef>();
        public List<ItemFactorDef> EngineTypes { get; private set; } = new List<ItemFactorDef>();
        public List<ItemFactorDef> GyroTypes { get; private set; } = new List<ItemFactorDef>();

        public CategoriesTags Specials { get; private set; } = new();

        public List<EquipmentInfo> DefensiveItems { get; private set; } = new();
        public List<EquipmentInfo> OffensiveItems { get; private set; } = new();

        public string ClanMechTag { get; private set; } = "ClanMech";
    }

    internal class ItemFactorDef
    {
        public string ItemID { get; set; } = "";
        public float Factor { get; set; } = 1.0f;
    }

    internal class CategoriesTags
    {
        public string CASE { get; set; } = "CASE";
        public string ArtemisIV { get; set; } = "ArtemisIV";
        public string ArtemisIV_Capable { get; set; } = "ArtIVCapable";
        public string TSM { get; set; } = "TSM";
        public string ProtoTSM { get; set; } = "ProtoTSM";
        public string MASC { get; set; } = "MASC";
        public string StealthArmor { get; set; } = "StealthArmor";
    }

    internal class EquipmentInfo
    {
        public string Tag { get; set; } = "";
        public float BattleValue { get; set; } = 0.0f;
        public float AmmoBattleValue { get; set; } = 0.0f;
    }
}
