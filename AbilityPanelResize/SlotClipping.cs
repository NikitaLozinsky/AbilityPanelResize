using Kingmaker.UI.MVVM._PCView.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Разрешает маске обрезать иконки способностей.
    ///
    /// В префабе слота у графики стоит <c>maskable = false</c>. Для ванильной
    /// панели это было даром: она никогда не прокручивалась и маски над ней не
    /// было, а выключенный флаг экономит вариант материала. Нам он ломает всё —
    /// Unity в <c>MaskableGraphic.UpdateClipParent</c> первым делом смотрит
    /// именно на <c>maskable</c> и при <c>false</c> не регистрирует графику в
    /// маске вовсе.
    ///
    /// Сама игра делает ровно этот трюк там, где ей нужен прокручиваемый
    /// список: см. <c>SpellbookSpellView.BindViewImplementation</c> —
    /// <c>m_BackgroundImage.maskable = true</c>.
    /// </summary>
    public static class SlotClipping
    {
        /// <summary>
        /// Поднимает флаг у всей графики слота. Вызывается часто (при каждом
        /// перерисовывании группы и развороте панели), поэтому делает работу
        /// только там, где флаг реально сброшен: у уже обработанных слотов это
        /// просто обход списка без единой записи.
        /// </summary>
        public static void Enable(Transform slot)
        {
            foreach (MaskableGraphic graphic in slot.GetComponentsInChildren<MaskableGraphic>(includeInactive: true))
            {
                if (graphic.maskable)
                {
                    continue;
                }

                graphic.maskable = true;

                // Сам по себе флаг родителя-маску не пересчитывает: сеттер
                // помечает материал грязным (этого хватает трафарету), но в
                // список RectMask2D графику не вносит - а нам нужна и его
                // отбраковка того, что уехало за пределы окна целиком.
                graphic.RecalculateClipping();
            }
        }

        public static void EnableAll(Transform content)
        {
            for (int i = 0; i < content.childCount; i++)
            {
                Enable(content.GetChild(i));
            }
        }

        /// <summary>
        /// Обвязка для патчей: отсекает не-Ability группы и непропатченные
        /// панели, находит наш контент.
        /// </summary>
        public static void EnableForAbilityGroup(ActionBarGroupPCView view)
        {
            if (!ActionBarGroupAccess.IsAbilityGroup(view))
            {
                return;
            }

            Transform content = (view.transform as RectTransform)?.Find(ModNames.ContentPath);
            if (content != null)
            {
                EnableAll(content);
            }
        }
    }
}
