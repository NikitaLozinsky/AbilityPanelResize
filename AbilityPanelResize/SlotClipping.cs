using System.Collections.Generic;
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
        /// Обход сетки идёт часто — на каждой перерисовке группы и на каждом
        /// развороте панели, то есть в бою по нескольку раз в секунду. Версия
        /// <c>GetComponentsInChildren</c>, пишущая в готовый список, не создаёт
        /// мусора; общий список для этого и держим. За собой он чистится сразу:
        /// иначе между вызовами в нём лежали бы ссылки на уже мёртвые слоты.
        /// </summary>
        private static readonly List<MaskableGraphic> s_Buffer = new List<MaskableGraphic>();

        /// <summary>
        /// Поднимает флаг у всей графики сетки. Реальная работа делается только
        /// там, где флаг сброшен: у уже обработанных слотов это просто проход
        /// по списку без единой записи.
        ///
        /// Слоты действительно бывают новыми: <c>DrawSlots</c> дозаказывает их
        /// у <c>WidgetFactory</c>, а у свежего виджета флаг снова префабный.
        /// </summary>
        public static void Enable(Transform content)
        {
            if (content == null)
            {
                return;
            }

            content.GetComponentsInChildren(true, s_Buffer);

            for (int i = 0; i < s_Buffer.Count; i++)
            {
                MaskableGraphic graphic = s_Buffer[i];
                if (graphic == null || graphic.maskable)
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

            s_Buffer.Clear();
        }
    }
}
