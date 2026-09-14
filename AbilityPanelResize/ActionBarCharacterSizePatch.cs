using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM._VM.ActionBar;
using UnityEngine;

namespace AbilityPanelResize
{
    [HarmonyPatch(typeof(ActionBarVM), "OnUnitChanged")]
    public static class ActionBarVM_OnUnitChanged_Patch
    {
        private static string s_LastCharacterId;

        [HarmonyPostfix]
        public static void Postfix(UnitEntityData unit)
        {
            if (unit == null || unit.UniqueId == s_LastCharacterId)
            {
                return;
            }

            s_LastCharacterId = unit.UniqueId;

            ResizeElementAdapter adapter = Object.FindObjectOfType<ResizeElementAdapter>();
            adapter?.ApplyCharacterSize(unit.UniqueId);
        }
    }
}
