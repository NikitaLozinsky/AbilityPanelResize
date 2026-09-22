using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM._PCView.ActionBar;

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
    ///
    /// Событие частое (в бою — по нескольку раз в секунду), поэтому «наша ли
    /// это панель» проверяется одним <c>GetComponent</c>: адаптер висит только
    /// на построенной панели способностей. Ни поиска ребёнка по имени, ни
    /// чтения приватного поля с типом группы здесь больше нет.
    /// </summary>
    [HarmonyPatch(typeof(ActionBarGroupPCView), "SetGroup")]
    public static class ActionBarGroupPCView_SetGroup_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ActionBarGroupPCView __instance)
        {
            ResizeElementAdapter adapter = __instance.GetComponent<ResizeElementAdapter>();
            if (adapter == null || !adapter.IsBuilt)
            {
                return;
            }

            SlotClipping.Enable(adapter.Content);

            UnitEntityData unit = ActionBarGroupAccess.GetViewModel(__instance)?.SelectedUnit.Value;
            if (unit != null)
            {
                adapter.ApplyCharacterSize(unit.UniqueId);
            }
        }
    }
}
