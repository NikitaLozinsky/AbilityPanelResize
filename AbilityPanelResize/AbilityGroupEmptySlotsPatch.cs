using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using UnityEngine;

namespace AbilityPanelResize
{
    /// <summary>
    /// Игра добивает список пустыми слотами до кратности пяти, чтобы ряд не
    /// выглядел оборванным в узком ванильном окне. У растягиваемого окна число
    /// колонок произвольное, и эти пустышки превращаются в дыры посреди сетки.
    /// </summary>
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

            return !ActionBarGroupAccess.IsAbilityGroup(__instance);
        }
    }
}
