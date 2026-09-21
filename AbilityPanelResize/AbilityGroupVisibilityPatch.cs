using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ActionBar;

namespace AbilityPanelResize
{
    /// <summary>
    /// Разворачивание и сворачивание панели. Раньше на это событие висели два
    /// отдельных Postfix'а; они слиты в один, потому что порядок между
    /// несколькими патчами одного метода Harmony не определяет, а тут он важен:
    /// сначала показываем свои части, потом проверяем обрезку уже показанного.
    /// </summary>
    [HarmonyPatch(typeof(ActionBarGroupPCView), nameof(ActionBarGroupPCView.SetVisible))]
    public static class ActionBarGroupPCView_SetVisible_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ActionBarGroupPCView __instance, bool state)
        {
            ResizeElementAdapter adapter = __instance.GetComponent<ResizeElementAdapter>();
            adapter?.SetPanelPartsActive(state);

            if (!state)
            {
                return;
            }

            // Unity помечает маску у неактивной графики как отсутствующую, а
            // при постройке панель ещё свёрнута. Разворачивание - первый
            // момент, когда всё точно активно и разложено.
            SlotClipping.EnableForAbilityGroup(__instance);
        }
    }
}
