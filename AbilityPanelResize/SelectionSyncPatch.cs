using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.Selection;

namespace AbilityPanelResize
{
    /// <summary>
    /// В игре два разных «выбранного персонажа», и клик по модельке обновляет
    /// только один из них.
    ///
    /// <c>SelectionCharacterController.SelectedUnits</c> — кем игрок сейчас
    /// управляет; туда пишет выделение мышью в мире. А окна
    /// (инвентарь, лист персонажа, книга заклинаний) смотрят на
    /// <c>SelectedUnit</c> — отдельное реактивное поле, которое меняет только
    /// <c>SetSelected</c>, то есть клик по портрету.
    ///
    /// Панель способностей путаницы не замечает: она берёт
    /// <c>CurrentSelectedCharacter</c>, а у того приоритет обратный —
    /// сначала мировое выделение и лишь потом <c>SelectedUnit</c>. Отсюда и
    /// симптом: моделькой персонаж выбирается «по-настоящему», а инвентарь
    /// открывается на том, кого последним ткнули в портретах.
    ///
    /// Чиним тем же способом, каким это делает сама игра в <c>SetSelected</c>:
    /// после выбора мышью в мире дописываем героя и во второе поле. Пишем
    /// только когда выделен ровно один — при выделении нескольких игра и так
    /// считает «одного текущего» неопределённым.
    /// </summary>
    internal static class SelectionSync
    {
        public static void SyncSelectedUnit()
        {
            if (Main.Settings == null || !Main.Settings.SyncWorldSelection)
            {
                return;
            }

            SelectionCharacterController selection = Game.Instance?.SelectionCharacter;
            if (selection == null || selection.SelectedUnits.Count != 1)
            {
                return;
            }

            UnitEntityData unit = selection.SelectedUnits[0];
            if (unit == null || selection.SelectedUnit.Value == unit)
            {
                return;
            }

            selection.SelectedUnit.Value = unit;
        }
    }

    /// <summary>
    /// Клик по модельке союзника на локации.
    /// </summary>
    [HarmonyPatch(typeof(SelectionManagerPC), nameof(SelectionManagerPC.SwitchSelectionUnitInGroup))]
    public static class SelectionManagerPC_SwitchSelectionUnitInGroup_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            SelectionSync.SyncSelectedUnit();
        }
    }

    /// <summary>
    /// Выделение рамкой. Тот же баг, та же починка: если в рамку попал ровно
    /// один герой, это такой же одиночный выбор, как и клик по модельке.
    /// </summary>
    [HarmonyPatch(typeof(SelectionManagerPC), nameof(SelectionManagerPC.MultiSelect))]
    public static class SelectionManagerPC_MultiSelect_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            SelectionSync.SyncSelectedUnit();
        }
    }
}
