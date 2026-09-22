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

        /// Зазор между скроллбаром и правой кромкой рамки окна. Не меньше, чем
        /// зона захвата правой грани заходит внутрь окна, иначе она накроет
        /// ползунок и его станет не ухватить.
        public const float RightInset = 5f;

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

            // Дорожка и ползунок меряются отдельно: в игре ползунок заметно
            // шире дорожки, и именно он делает скроллбар заметным. Место под
            // скроллбар резервируем по самому широкому из них.
            float trackWidth = MeasureWidth(sourceRect);
            float handleWidth = MeasureHandleWidth(source, sourceRect, trackWidth);
            float width = Mathf.Max(trackWidth, handleWidth);

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

            CreateBack(source, sourceRect, barRect, trackWidth);

            RectTransform slidingArea = CreateSlidingArea(source, sourceRect, barRect, handleWidth);
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

            Main.Logger.Log(Localization.Get("AbilityPanelResize.Log.ScrollbarAdded", source.name,
                $"{trackWidth:0}/{handleWidth:0}"));
            return holder;
        }

        private static void CreateBack(Scrollbar source, RectTransform sourceRect, RectTransform parent, float width)
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
                PlaceCentered(sourceBack.rectTransform, back, width);
            }
            else
            {
                PlaceCentered(null, back, width);
            }

            CopyImage(sourceBack, backGO.AddComponent<Image>());
        }

        private static RectTransform CreateSlidingArea(Scrollbar source, RectTransform sourceRect, RectTransform parent, float width)
        {
            var areaGO = new GameObject(SlidingAreaName, typeof(RectTransform));
            RectTransform area = areaGO.GetComponent<RectTransform>();
            area.SetParent(parent, worldPositionStays: false);

            // У образца дорожка обычно чуть уже самого скроллбара — забираем её
            // отступы, иначе ползунок будет упираться в края.
            var sourceArea = source.handleRect != null ? source.handleRect.parent as RectTransform : null;
            if (sourceArea != null && sourceArea != sourceRect && sourceArea.parent == sourceRect)
            {
                PlaceCentered(sourceArea, area, width);
            }
            else
            {
                PlaceCentered(null, area, width);
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
            //
            // По горизонтали не копируем ничего. У образца ползунок шире своей
            // дорожки (положительный sizeDelta.x при растянутых якорях), и в
            // нашей узкой дорожке эта добавка вылезала за правый край окна —
            // прямо на хендл ресайза, перехватывая у него мышь. Поймано пробой
            // указателя: под курсором на самой грани первым лежал ползунок.
            // Ширину ползунка задаёт дорожка, и только она.
            if (source.handleRect != null)
            {
                handle.pivot = source.handleRect.pivot;
                handle.sizeDelta = new Vector2(0f, source.handleRect.sizeDelta.y);
                handle.anchoredPosition = new Vector2(0f, source.handleRect.anchoredPosition.y);
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

        /// <summary>
        /// Ширина ползунка у образца.
        ///
        /// Померить её напрямую нельзя: <c>Scrollbar</c> каждый кадр сам
        /// переписывает ползунку якоря, растягивая его по дорожке на всю
        /// ширину. Поэтому его видимая ширина — это ширина дорожки плюс
        /// собственные добавки: его самого и промежуточной «Sliding Area».
        /// Именно эта добавка и делает ползунок в игре заметнее дорожки.
        /// </summary>
        private static float MeasureHandleWidth(Scrollbar source, RectTransform sourceRect, float trackWidth)
        {
            RectTransform handle = source.handleRect;
            if (handle == null)
            {
                return trackWidth;
            }

            float extra = handle.sizeDelta.x;

            if (handle.parent is RectTransform area
                && area != sourceRect
                && !Mathf.Approximately(area.anchorMin.x, area.anchorMax.x))
            {
                extra += area.sizeDelta.x;
            }

            float width = trackWidth + extra;
            return width >= MinWidth && width <= MaxWidth ? width : trackWidth;
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

        /// <summary>
        /// Ставит деталь по центру полосы с заданной шириной, забирая у образца
        /// только вертикальную геометрию.
        ///
        /// Горизонталь у образца не копируется вовсе, и это принципиально: там
        /// ширина задана добавкой к своему родителю, а у нас родитель другой.
        /// Один раз скопированная как есть, такая добавка вылезла за правый
        /// край окна — ровно туда, где начинается зона захвата грани, — и
        /// перехватила у неё мышь. Ширину здесь задаёт вызывающий, измерив её
        /// у образца заранее.
        ///
        /// <paramref name="source"/> может быть null: тогда деталь просто
        /// растягивается по вертикали.
        /// </summary>
        private static void PlaceCentered(RectTransform source, RectTransform target, float width)
        {
            target.anchorMin = new Vector2(0.5f, source != null ? source.anchorMin.y : 0f);
            target.anchorMax = new Vector2(0.5f, source != null ? source.anchorMax.y : 1f);
            target.pivot = new Vector2(0.5f, source != null ? source.pivot.y : 0.5f);
            target.anchoredPosition = new Vector2(0f, source != null ? source.anchoredPosition.y : 0f);
            target.sizeDelta = new Vector2(width, source != null ? source.sizeDelta.y : 0f);
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
