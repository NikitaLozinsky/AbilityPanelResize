using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using UnityEngine;

namespace AbilityPanelResize
{
    [HarmonyPatch(typeof(ActionBarGroupPCView), "AddEmptySlots")]
    public static class ActionBarGroupPCView_AddEmptySlots_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ActionBarGroupPCView __instance)
        {
            RectTransform root = __instance.transform as RectTransform;
            if (root == null || root.Find(ModNames.Viewport) == null)
            {
                return true;
            }

            ActionBarGroupType groupType = Traverse.Create(__instance).Field("m_GroupType").GetValue<ActionBarGroupType>();
            return groupType != ActionBarGroupType.Ability;
        }
    }
}
