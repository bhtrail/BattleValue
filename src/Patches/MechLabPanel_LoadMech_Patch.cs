using BattleTech.UI;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattleValue.Patches
{
    [HarmonyPatch(typeof(MechLabPanel), nameof(MechLabPanel.LoadMech))]
    public class MechLabPanel_LoadMech_Patch
    {
        public static void Postfix (MechLabPanel __instance)
        {
            var mechDef = __instance.activeMechDef;

            var bv = mechDef.CalculateBattleValue();
            Core.Log($"BV for {mechDef.Description.Id} : {bv}");
        }
    }
}
