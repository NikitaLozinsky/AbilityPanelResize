using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using UnityEngine;

namespace AbilityPanelResize
{
    [HarmonyPatch(typeof(ActionBarGroupPCView), "SetGroup")]
    public static class ActionBarGroupPCView_SetGroup_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ActionBarGroupPCView __instance)
        {
            var traverse = Traverse.Create(__instance);
            if (traverse.Field("m_GroupType").GetValue<ActionBarGroupType>() != ActionBarGroupType.Ability)
            {
                return;
            }

            RectTransform root = __instance.transform as RectTransform;
            if (root == null || root.Find(ModNames.Viewport) == null)
            {
                return;
            }

            ActionBarVM viewModel = traverse.Property("ViewModel").GetValue<ActionBarVM>();
            UnitEntityData unit = viewModel?.SelectedUnit.Value;
            if (unit == null)
            {
                return;
            }

            ResizeElementAdapter adapter = root.GetComponent<ResizeElementAdapter>();
            adapter?.ApplyCharacterSize(unit.UniqueId);
        }
    }
}
