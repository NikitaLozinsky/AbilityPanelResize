using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ActionBar;

namespace AbilityPanelResize
{
    /// <summary>
    /// Игра добивает список пустыми слотами до кратности пяти, чтобы ряд не
    /// выглядел оборванным в узком ванильном окне. У растягиваемого окна число
    /// колонок произвольное, и эти пустышки превращаются в дыры посреди сетки.
    ///
    /// Попутно это снимает и рост памяти в самой игре: на каждую добивку
    /// создаётся <c>ActionBarSlotVM</c>, который уходит в <c>AddDisposable</c>
    /// вьюхи и живёт там до её уничтожения, то есть копится всю сессию.
    ///
    /// Целью перетаскивания пустышки не служат ни в одной вкладке: перетащить
    /// что-то можно только на ячейку нижнего хотбара
    /// (<c>ActionBarSlotPCView.OnEndDrag</c> ищет цель среди
    /// <c>ActionBarKeybindSlotPCView</c>). Вкладки Spell/Item патч не трогает
    /// просто потому, что мод их не строит. Отличаем по адаптеру — он есть
    /// только на построенной панели способностей.
    /// </summary>
    [HarmonyPatch(typeof(ActionBarGroupPCView), "AddEmptySlots")]
    public static class ActionBarGroupPCView_AddEmptySlots_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ActionBarGroupPCView __instance)
        {
            ResizeElementAdapter adapter = __instance.GetComponent<ResizeElementAdapter>();
            return adapter == null || !adapter.IsBuilt;
        }
    }
}
