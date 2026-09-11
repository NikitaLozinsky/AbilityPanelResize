using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    // ЧАСТЬ 1: заворачиваем сетку иконок способностей в ScrollRect
    // (Viewport + Content), чтобы контент можно было прокручивать колесом
    // мыши, если он не помещается. Ручной ресайз мышью за грань окна -
    // отдельный патч следующим шагом, здесь его сознательно нет, чтобы
    // сначала проверить, что сама перестройка иерархии ничего не сломала
    // (тултипы иконок, драг-н-дролл способности на панель быстрого доступа).
    [HarmonyPatch(typeof(ActionBarGroupPCView), nameof(ActionBarGroupPCView.Initialize))]
    public static class ActionBarGroupPCView_Initialize_AddScroll_Patch
    {
        // Временная фиксированная высота окна, пока не добавлен ручной
        // ресайз (шаг 2). После него это же число станет стартовым
        // значением, которое пользователь сможет менять руками за грань.
        private const float DefaultHeight = 400f;

        [HarmonyPostfix]
        public static void Postfix(ActionBarGroupPCView __instance, ActionBarGroupType type)
        {
            // Патчим только панель способностей - Spell/Item группы
            // используют тот же класс, но пока их не трогаем.
            if (type != ActionBarGroupType.Ability)
            {
                return;
            }

            RectTransform root = __instance.transform as RectTransform;
            if (root == null || root.Find("AbilityPanelResize_Viewport") != null)
            {
                // root == null - подстраховка (не должно случиться).
                // Find(...) != null - патч уже применён к этому объекту
                // (повторный Initialize) - выходим, чтобы не задвоить структуру.
                return;
            }

            var traverse = Traverse.Create(__instance);

            // ---- 1. Забираем то, что нужно перенести, через рефлексию ----
            // (оба поля private в оригинальном классе игры)
            List<ActionBarBaseSlotPCView> slotsList =
                traverse.Field("m_SlotsList").GetValue<List<ActionBarBaseSlotPCView>>();

            GridLayoutGroupWorkaround originalGrid = root.GetComponent<GridLayoutGroupWorkaround>();
            ContentSizeFitterExtended originalFitter = root.GetComponent<ContentSizeFitterExtended>();

            // Высота полосы заголовка ("Способности") - раньше брал из
            // padding.top оригинальной сетки, но это оказался лишь
            // технический зазор, а не реальная высота заголовка (при
            // сильном скролле контент всё равно доскролливал до текста).
            // Берём напрямую измеренную высоту объекта Header.
            RectTransform headerRect = root.Find("Background/Header") as RectTransform;
            float headerHeight = headerRect != null
                ? headerRect.rect.height
                : (originalGrid != null ? originalGrid.padding.top : 0f);

            // ---- 2. Viewport - обрезает всё, что не помещается.
            //         ВАЖНО: верх Viewport'а сдвинут вниз на headerHeight -
            //         это физически исключает область заголовка из зоны
            //         отрисовки прокручиваемого контента. Раньше место под
            //         заголовок резервировалось через padding.top сетки
            //         (см. ниже), но это часть самого Content - при скролле
            //         этот отступ уезжает вместе с контентом, и следующие
            //         ряды иконок всё равно заезжали на заголовок. Теперь
            //         зона заголовка исключена на уровне маски Viewport'а -
            //         её не заденет ничто, при любой прокрутке. ----
            var viewportGO = new GameObject("AbilityPanelResize_Viewport", typeof(RectTransform));
            RectTransform viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.SetParent(root, worldPositionStays: false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(0f, -headerHeight);
            viewportGO.AddComponent<RectMask2D>();

            // ---- 3. Content - сюда переедет сетка иконок ----
            var contentGO = new GameObject("AbilityPanelResize_Content", typeof(RectTransform));
            RectTransform contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.SetParent(viewportRect, worldPositionStays: false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero; // пересчитает ContentSizeFitterExtended ниже

            // ---- 4. Переносим уже существующие слоты в Content ----
            foreach (ActionBarBaseSlotPCView slot in slotsList)
            {
                slot.transform.SetParent(contentRect, worldPositionStays: false);

                // Баг: часть графических элементов слота (например,
                // декоративная "срезанная" рамка ForeIcon/UICornerCut - из
                // стороннего пакета UnityEngine.UI.Extensions) не
                // пересчитывает свою "родительскую маску" при переносе в
                // другого родителя - подтверждено через UnityExplorer:
                // m_ParentMask остаётся null, хотя у соседних стандартных
                // Image в том же слоте он правильно указывает на наш
                // Viewport. Сама MaskableGraphic пересчитывает маску из
                // OnTransformParentChanged только если isActiveAndEnabled
                // на момент вызова - похоже, для части графики слота это
                // условие в момент SetParent не выполняется. Без пересчёта
                // RectMask2D эту графику никогда не обрежет - при скролле
                // контента за пределы Viewport от неё остаётся видимый
                // "остаток" (не иконка, просто декоративная рамка) вместо
                // полного исчезновения, как у всего остального. Форсируем
                // пересчёт явно для всей графики внутри слота.
                foreach (MaskableGraphic graphic in slot.GetComponentsInChildren<MaskableGraphic>(includeInactive: true))
                {
                    graphic.RecalculateClipping();
                }
            }

            // ---- 5. Копируем настройки сетки на Content, старую выключаем ----
            GridLayoutGroupWorkaround newGrid = contentGO.AddComponent<GridLayoutGroupWorkaround>();
            if (originalGrid != null)
            {
                newGrid.DoWorkaround = originalGrid.DoWorkaround;
                newGrid.cellSize = originalGrid.cellSize;
                newGrid.spacing = originalGrid.spacing;
                newGrid.constraint = originalGrid.constraint;
                newGrid.constraintCount = originalGrid.constraintCount;
                newGrid.childAlignment = originalGrid.childAlignment;
                newGrid.startCorner = originalGrid.startCorner;
                newGrid.startAxis = originalGrid.startAxis;
                // ВАЖНО: padding.top теперь НЕ копируем - место под
                // заголовок резервирует сам Viewport (см. headerHeight
                // выше), а не отступ внутри Content. Если бы оставили и
                // тут, и там - место под заголовок задвоилось бы.
                newGrid.padding = new RectOffset(
                    originalGrid.padding.left,
                    originalGrid.padding.right,
                    0,
                    originalGrid.padding.bottom);
                originalGrid.enabled = false;
            }

            // ---- 6. Content сам считает свою высоту под контент.
            //         Старый fitter на корне выключаем - это и была причина
            //         бесконтрольного роста самого окна. ----
            ContentSizeFitterExtended newFitter = contentGO.AddComponent<ContentSizeFitterExtended>();
            Traverse.Create(newFitter).Field("m_HorizontalFit")
                .SetValue(ContentSizeFitterExtended.FitMode.Unconstrained);
            Traverse.Create(newFitter).Field("m_VerticalFit")
                .SetValue(ContentSizeFitterExtended.FitMode.PreferredSize);

            if (originalFitter != null)
            {
                originalFitter.enabled = false;
            }

            // ---- 7. Стартовый размер окна - из сохранённых настроек мода,
            //         если пользователь уже когда-то ресайзил панель раньше
            //         (см. Settings.cs), иначе прежнее поведение по
            //         умолчанию (ширину не трогаем - её ставит сама игра,
            //         высоту - DefaultHeight). Clamp на случай устаревшего/
            //         повреждённого файла настроек или изменения границ в
            //         будущей версии мода. ----
            Settings settings = Main.Settings;
            float nativeWidth = root.sizeDelta.x;
            float initialWidth = settings != null && settings.Width > 0f
                ? Mathf.Clamp(settings.Width, ResizeLimits.MinSize.x, ResizeLimits.MaxSize.x)
                : nativeWidth;
            float initialHeight = settings != null && settings.Height > 0f
                ? Mathf.Clamp(settings.Height, ResizeLimits.MinSize.y, ResizeLimits.MaxSize.y)
                : DefaultHeight;

            if (!Mathf.Approximately(initialWidth, nativeWidth))
            {
                // Та же компенсация центра, что ResizeElementAdapter.SetSizeDelta
                // делает при ручном ресайзе (см. m_InitialCenterX там) - кнопка
                // сворачивания сидит по горизонтальному центру ОКНА, поэтому
                // при отличии восстановленной ширины от нативной нужно сразу
                // сдвинуть anchoredPosition.x, а не оставлять левый край на
                // месте. Считаем здесь же, напрямую: адаптер на этот момент
                // ещё может не существовать (создаётся другим Postfix'ом,
                // AbilityGroupResizePatch, порядок между двумя Postfix'ами на
                // одном Initialize не гарантирован) - без этой компенсации
                // окно стартует уже "съехавшим" вправо, и последующий первый
                // ручной драг зафиксирует в m_InitialCenterX уже неверный
                // центр, унаследовав перекос дальше.
                float nativeCenterX = root.anchoredPosition.x + nativeWidth / 2f;
                Vector2 anchoredPosition = root.anchoredPosition;
                anchoredPosition.x = nativeCenterX - initialWidth / 2f;
                root.anchoredPosition = anchoredPosition;
            }

            root.sizeDelta = new Vector2(initialWidth, initialHeight);

            // ---- 8. ScrollRect - даёт скролл колесом мыши "из коробки" ----
            ScrollRect scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 20f;

            // ---- 9. Подменяем m_SlotContainer, чтобы новые иконки,
            //         которые DrawSlots() добавит позже, тоже попадали
            //         в Content, а не обратно в корень. ----
            traverse.Field("m_SlotContainer").SetValue(contentRect);

            // ---- 10. Отключаем перехват кликов у декоративного фона.
            //          Background - чисто визуальная подложка панели, на
            //          неё ничего не завязано функционально. Раз панель
            //          теперь может вырасти как угодно, она может визуально
            //          перекрыть соседние элементы интерфейса (кнопки
            //          соседних панелей, индикаторы ресурсов классов вроде
            //          выгорания кинетика и т.п.) - таких соседей может
            //          быть сколько угодно, гоняться за каждым и
            //          перестраивать порядок отрисовки - тупиковый путь.
            //          Проще сделать так, чтобы наш фон просто не перехватывал
            //          клики - тогда что бы он ни перекрыл визуально, клик
            //          всё равно дойдёт до объекта под ним. ----
            Image backgroundImage = root.Find("Background")?.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.raycastTarget = false;
            }

            Main.Logger.Log("AbilityPanelResize: обёртка ScrollRect добавлена на панель способностей.");
        }
    }
}