using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using UnityEngine;

namespace AbilityPanelResize
{
    /// <summary>
    /// Перерисовка группы: сменился выбранный персонаж или его набор
    /// способностей. Два дела на одно событие — подогнать размер окна под
    /// нового персонажа и вернуть обрезку свежим слотам.
    ///
    /// Слоты здесь действительно могут быть новыми: <c>DrawSlots</c>
    /// дозаказывает их у <c>WidgetFactory</c>, а у виджета из пула флаг
    /// <c>maskable</c> снова префабный.
    /// </summary>
    [HarmonyPatch(typeof(ActionBarGroupPCView), "SetGroup")]
    public static class ActionBarGroupPCView_SetGroup_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ActionBarGroupPCView __instance)
        {
            if (!ActionBarGroupAccess.IsAbilityGroup(__instance))
            {
                return;
            }

            RectTransform root = __instance.transform as RectTransform;
            if (root == null || root.Find(ModNames.Viewport) == null)
            {
                return;
            }

            SlotClipping.EnableForAbilityGroup(__instance);

            UnitEntityData unit = ActionBarGroupAccess.GetViewModel(__instance)?.SelectedUnit.Value;
            if (unit == null)
            {
                return;
            }

            root.GetComponent<ResizeElementAdapter>()?.ApplyCharacterSize(unit.UniqueId);
        }
    }
}
