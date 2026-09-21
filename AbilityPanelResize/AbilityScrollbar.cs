using Kingmaker.UI.MVVM._PCView.CombatLog;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Вертикальный скроллбар справа от панели способностей.
    ///
    /// Сам объект собираем с нуля, а внешний вид (спрайты, цвета, геометрию
    /// дорожки и ползунка) копируем с готового скроллбара игры — того самого,
    /// что стоит у окна боевого лога.
    ///
    /// Почему не клонируем игровой объект целиком: на игровых скроллбарах висят
    /// чужие скрипты. `ScrollSizeReset` каждый LateUpdate насмерть выставляет
    /// `size = 0`, а `SrollbarWithPlaceholder` дёргает посторонний GameObject —
    /// в копии эта ссылка осталась бы указывать на оригинал из боевого лога, и
    /// мы бы мигали чужим интерфейсом. Выкусывать компоненты из копии дороже и
    /// менее предсказуемо, чем перенести две картинки.
    /// </summary>
    public static class AbilityScrollbar
    {
        private const string BackName = "Back";
        private const string SlidingAreaName = "Sliding Area";
        private const string HandleName = "Handle";

        /// Имя игрового скроллбара, который мы ищем как образец.
        private const string SourceName = "ScrollbarVertical";

        private const float FallbackWidth = 14f;
        private const float MinWidth = 4f;
        private const float MaxWidth = 60f;

        /// Зазор между скроллбаром и правой кромкой рамки окна.
        public const float RightInset = 2f;

        /// Насколько дополнительно сужаем viewport, чтобы иконки не липли к дорожке.
        private const float ViewportGap = 2f;

        private static Scrollbar s_Source;

        /// <summary>
        /// Строит скроллбар и подключает его к <paramref name="scrollRect"/>.
        /// Возвращает держатель скроллбара (или null, если образец в игре не
        /// нашёлся — тогда панель останется рабочей, просто без бегунка, колесо
        /// мыши никуда не денется). В <paramref name="reservedWidth"/> кладёт,
        /// насколько вызывающий должен сузить область прокрутки справа.
        /// </summary>
        public static RectTransform Attach(
            RectTransform root,
            ScrollRect scrollRect,
            float headerInset,
            ResizeElementAdapter adapter,
            out float reservedWidth)
        {
            reservedWidth = 0f;

            Scrollbar source = FindSource();
            if (source == null)
            {
                Main.Logger.Warning(Localization.Get("AbilityPanelResize.Log.ScrollbarSourceNotFound"));
                return null;
            }

            var sourceRect = source.transform as RectTransform;
            float width = MeasureWidth(sourceRect);

            // Держатель нужен потому, что видимостью самого скроллбара
            // распоряжается ScrollRect (режим AutoHide сам зовёт SetActive, когда
            // контент помещается целиком), а видимостью всего блока — сворачивание
            // панели. На одном GameObject эти два механизма дрались бы за SetActive.
            var holderGO = new GameObject(ModNames.ScrollbarHolder, typeof(RectTransform));
            RectTransform holder = holderGO.GetComponent<RectTransform>();
            holder.SetParent(root, worldPositionStays: false);
            holder.anchorMin = new Vector2(1f, 0f);
            holder.anchorMax = new Vector2(1f, 1f);
            holder.offsetMin = new Vector2(-(width + RightInset), 0f);
            holder.offsetMax = new Vector2(-RightInset, -headerInset);

            var barGO = new GameObject(ModNames.Scrollbar, typeof(RectTransform));
            RectTransform barRect = barGO.GetComponent<RectTransform>();
            barRect.SetParent(holder, worldPositionStays: false);
            Stretch(barRect);

            CreateBack(source, sourceRect, barRect);

            RectTransform slidingArea = CreateSlidingArea(source, sourceRect, barRect);
            Image handleImage = CreateHandle(source, slidingArea);

            Scrollbar scrollbar = barGO.AddComponent<Scrollbar>();
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handleImage.rectTransform;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.transition = source.transition;
            scrollbar.colors = source.colors;
            scrollbar.spriteState = source.spriteState;
            scrollbar.numberOfSteps = 0;
            scrollbar.size = 1f;
            scrollbar.value = 1f;

            // Скроллбар — Selectable, и без этого он влезает в очередь
            // клавиатурной/геймпадной навигации игры. Нам он нужен только под мышь.
            Navigation navigation = scrollbar.navigation;
            navigation.mode = Navigation.Mode.None;
            scrollbar.navigation = navigation;

            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // Сужаем видимую область, чтобы правый столбец иконок не уезжал под дорожку.
            reservedWidth = width + RightInset + ViewportGap;

            if (adapter != null)
            {
                adapter.RegisterScrollbar(holderGO);
            }
            else
            {
                holderGO.SetActive(false);
            }

            Main.Logger.Log(Localization.Get("AbilityPanelResize.Log.ScrollbarAdded", source.name, width.ToString("0")));
            return holder;
        }

        private static void CreateBack(Scrollbar source, RectTransform sourceRect, RectTransform parent)
        {
            Image sourceBack = FindChildImage(sourceRect, BackName);
            if (sourceBack == null)
            {
                // У части скроллбаров игры фон лежит не отдельным ребёнком, а прямо
                // на самом объекте скроллбара.
                Image ownImage = sourceRect.GetComponent<Image>();
                if (ownImage != null && source.handleRect != ownImage.rectTransform)
                {
                    sourceBack = ownImage;
                }
            }

            if (sourceBack == null)
            {
                return;
            }

            var backGO = new GameObject(BackName, typeof(RectTransform));
            RectTransform back = backGO.GetComponent<RectTransform>();
            back.SetParent(parent, worldPositionStays: false);

            if (sourceBack.rectTransform.parent == sourceRect)
            {
                CopyRect(sourceBack.rectTransform, back);
            }
            else
            {
                Stretch(back);
            }

            CopyImage(sourceBack, backGO.AddComponent<Image>());
        }

        private static RectTransform CreateSlidingArea(Scrollbar source, RectTransform sourceRect, RectTransform parent)
        {
            var areaGO = new GameObject(SlidingAreaName, typeof(RectTransform));
            RectTransform area = areaGO.GetComponent<RectTransform>();
            area.SetParent(parent, worldPositionStays: false);

            // У образца дорожка обычно чуть уже самого скроллбара — забираем её
            // отступы, иначе ползунок будет упираться в края.
            var sourceArea = source.handleRect != null ? source.handleRect.parent as RectTransform : null;
            if (sourceArea != null && sourceArea != sourceRect && sourceArea.parent == sourceRect)
            {
                CopyRect(sourceArea, area);
            }
            else
            {
                Stretch(area);
            }

            return area;
        }

        private static Image CreateHandle(Scrollbar source, RectTransform parent)
        {
            var handleGO = new GameObject(HandleName, typeof(RectTransform));
            RectTransform handle = handleGO.GetComponent<RectTransform>();
            handle.SetParent(parent, worldPositionStays: false);

            Image handleImage = handleGO.AddComponent<Image>();

            Image sourceHandle = source.handleRect != null ? source.handleRect.GetComponent<Image>() : null;
            if (sourceHandle == null && source.handleRect != null)
            {
                sourceHandle = source.handleRect.GetComponentInChildren<Image>(includeInactive: true);
            }

            if (sourceHandle != null)
            {
                CopyImage(sourceHandle, handleImage);
            }

            // anchorMin/anchorMax ползунка каждый кадр переписывает сам Scrollbar —
            // копируем только то, что он не трогает.
            if (source.handleRect != null)
            {
                handle.pivot = source.handleRect.pivot;
                handle.sizeDelta = source.handleRect.sizeDelta;
                handle.anchoredPosition = source.handleRect.anchoredPosition;
            }
            else
            {
                handle.sizeDelta = Vector2.zero;
                handle.anchoredPosition = Vector2.zero;
            }

            handleImage.raycastTarget = true;
            return handleImage;
        }

        /// <summary>
        /// Ищет в уже загруженных объектах игры вертикальный скроллбар, который
        /// можно взять за образец. Сначала смотрим боевой лог — он есть всегда и
        /// это ровно тот вид, который просили.
        /// </summary>
        private static Scrollbar FindSource()
        {
            if (s_Source != null)
            {
                return s_Source;
            }

            foreach (CombatLogPCView logView in Resources.FindObjectsOfTypeAll<CombatLogPCView>())
            {
                foreach (Scrollbar candidate in logView.GetComponentsInChildren<Scrollbar>(includeInactive: true))
                {
                    if (IsUsable(candidate))
                    {
                        return s_Source = candidate;
                    }
                }
            }

            Scrollbar fallback = null;
            foreach (Scrollbar candidate in Resources.FindObjectsOfTypeAll<Scrollbar>())
            {
                if (!IsUsable(candidate))
                {
                    continue;
                }

                if (candidate.name == SourceName)
                {
                    return s_Source = candidate;
                }

                if (fallback == null)
                {
                    fallback = candidate;
                }
            }

            return s_Source = fallback;
        }

        private static bool IsUsable(Scrollbar candidate)
        {
            return candidate != null
                   && candidate.handleRect != null
                   && (candidate.direction == Scrollbar.Direction.BottomToTop
                       || candidate.direction == Scrollbar.Direction.TopToBottom)
                   // Свой собственный скроллбар за образец брать нельзя — так мы
                   // начали бы копировать копию.
                   && !candidate.name.StartsWith(ModNames.Prefix);
        }

        private static float MeasureWidth(RectTransform sourceRect)
        {
            if (sourceRect == null)
            {
                return FallbackWidth;
            }

            // У неактивного объекта rect.width может быть ещё не посчитан, поэтому
            // при фиксированной ширине (якоря по X совпадают) берём sizeDelta.
            float width = Mathf.Approximately(sourceRect.anchorMin.x, sourceRect.anchorMax.x)
                ? sourceRect.sizeDelta.x
                : sourceRect.rect.width;

            return width >= MinWidth && width <= MaxWidth ? width : FallbackWidth;
        }

        private static Image FindChildImage(Transform source, string childName)
        {
            Transform child = source != null ? source.Find(childName) : null;
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
        }

        private static void CopyImage(Image source, Image target)
        {
            target.sprite = source.sprite;
            target.overrideSprite = source.overrideSprite;
            target.color = source.color;
            target.material = source.material;
            target.type = source.type;
            target.fillCenter = source.fillCenter;
            target.preserveAspect = source.preserveAspect;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            target.raycastTarget = true;
        }
    }
}
