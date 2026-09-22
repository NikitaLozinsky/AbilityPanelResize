using System.Collections.Generic;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Единственная точка, где панель способностей превращается в прокручиваемое
    /// и растягиваемое окно. Раньше этим занимались два независимых постфикса на
    /// одном и том же <c>Initialize</c>; их слили в один, потому что порядок
    /// между несколькими патчами одного метода Harmony не определяет, а здесь он
    /// важен — хендлы граней должны родиться последними, чтобы лежать поверх
    /// скроллбара.
    /// </summary>
    [HarmonyPatch(typeof(ActionBarGroupPCView), nameof(ActionBarGroupPCView.Initialize))]
    public static class ActionBarGroupPCView_Initialize_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ActionBarGroupPCView __instance, ActionBarGroupType type)
        {
            if (type != ActionBarGroupType.Ability)
            {
                return;
            }

            RectTransform root = __instance.transform as RectTransform;
            if (root == null || __instance.GetComponent<ResizeElementAdapter>() != null)
            {
                return;
            }

            Settings settings = Main.Settings;
            ResizeElementAdapter adapter = ResizeElementAdapter.Ensure(__instance);
            List<ActionBarBaseSlotPCView> slotsList = ActionBarGroupAccess.GetSlots(__instance);

            GridLayoutGroupWorkaround originalGrid = root.GetComponent<GridLayoutGroupWorkaround>();
            ContentSizeFitterExtended originalFitter = root.GetComponent<ContentSizeFitterExtended>();

            RectTransform headerRect = FindHeader(root, __instance, out string headerSource);
            float headerHeight = headerRect != null
                ? headerRect.rect.height
                : (originalGrid != null ? originalGrid.padding.top : 0f);

            if (headerRect == null)
            {
                Main.Logger.Warning(Localization.Get("AbilityPanelResize.Log.HeaderNotFound", headerHeight.ToString("0.#")));
            }

            var viewportGO = new GameObject(ModNames.Viewport, typeof(RectTransform));
            RectTransform viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.SetParent(root, worldPositionStays: false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = new Vector2(0f, -headerHeight);
            viewportGO.AddComponent<RectMask2D>();
            ViewportMask.Apply(viewportGO);

            var contentGO = new GameObject(ModNames.Content, typeof(RectTransform));
            RectTransform contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.SetParent(viewportRect, worldPositionStays: false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;

            foreach (ActionBarBaseSlotPCView slot in slotsList)
            {
                slot.transform.SetParent(contentRect, worldPositionStays: false);
            }

            SlotClipping.Enable(contentRect);

            GridLayoutGroupWorkaround newGrid = contentGO.AddComponent<GridLayoutGroupWorkaround>();
            if (originalGrid != null)
            {
                newGrid.DoWorkaround = originalGrid.DoWorkaround;
                newGrid.cellSize = originalGrid.cellSize;
                newGrid.spacing = originalGrid.spacing;
                newGrid.constraint = originalGrid.constraint;
                newGrid.constraintCount = originalGrid.constraintCount;
                newGrid.startCorner = originalGrid.startCorner;
                newGrid.startAxis = originalGrid.startAxis;
                newGrid.padding = new RectOffset(
                    originalGrid.padding.left,
                    originalGrid.padding.right,
                    0,
                    originalGrid.padding.bottom);
                originalGrid.enabled = false;
            }

            newGrid.childAlignment = settings != null && !settings.CenterIcons
                ? TextAnchor.UpperLeft
                : TextAnchor.UpperCenter;

            // Режимы подгонки у этого компонента защищённые, публичных свойств
            // нет - только рефлексия. Traverse здесь допустим: строится панель
            // один раз, в отличие от мест, которые работают на каждом кадре.
            ContentSizeFitterExtended newFitter = contentGO.AddComponent<ContentSizeFitterExtended>();
            Traverse fitter = Traverse.Create(newFitter);
            fitter.Field("m_HorizontalFit").SetValue(ContentSizeFitterExtended.FitMode.Unconstrained);
            fitter.Field("m_VerticalFit").SetValue(ContentSizeFitterExtended.FitMode.PreferredSize);

            if (originalFitter != null)
            {
                originalFitter.enabled = false;
            }

            UnitEntityData currentUnit = Game.Instance?.SelectionCharacter?.CurrentSelectedCharacter;
            adapter.SetSizeDelta(InitialSize(root, settings, currentUnit));

            ScrollRect scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = settings != null ? settings.ScrollSensitivity : 20f;

            RectTransform scrollbarHolder =
                AbilityScrollbar.Attach(root, scrollRect, headerHeight, adapter, out float reservedWidth);

            // Один замер высоты заголовка ненадёжен (см. HeaderInsetTracker) —
            // дальше верхней кромкой области прокрутки заведует трекер.
            var headerTracker = root.gameObject.AddComponent<HeaderInsetTracker>();
            headerTracker.Root = root;
            headerTracker.Header = headerRect;
            headerTracker.Viewport = viewportRect;
            headerTracker.ScrollbarHolder = scrollbarHolder;
            headerTracker.ViewportRightOffset = -reservedWidth;
            headerTracker.ScrollbarRightOffset = -AbilityScrollbar.RightInset;
            headerTracker.Apply(headerHeight);

            var diagnostics = root.gameObject.AddComponent<AbilityPanelDiagnostics>();
            diagnostics.Root = root;
            diagnostics.Viewport = viewportRect;
            diagnostics.Content = contentRect;
            diagnostics.Tracker = headerTracker;
            diagnostics.Adapter = adapter;
            diagnostics.HeaderSource = headerSource;

            ActionBarGroupAccess.SetSlotContainer(__instance, contentRect);

            // Задник ловит клики, а не пропускает их насквозь. Раньше было
            // наоборот — так решали перехват кликов у соседей по интерфейсу. Но
            // у окна произвольного размера пустого места много, и клик по нему
            // уходил в мир: отряд шёл туда, куда игрок целился внутри окна.
            // Соседям это не мешает: root.SetAsFirstSibling() держит наше окно
            // самым нижним внутри Groups, поэтому рейкаст отдаёт им приоритет.
            Image backgroundImage = root.Find(ModNames.Background)?.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.raycastTarget = true;
            }

            // Хендлы — последними: так они и лежат поверх остальных детей окна,
            // без перестановок задним числом.
            ResizeHandles.Build(root, adapter);

            adapter.CacheParts(viewportRect, contentRect);

            if (currentUnit != null)
            {
                adapter.SeedCharacterId(currentUnit.UniqueId);
            }

            if (originalGrid != null)
            {
                Main.Logger.Log(Localization.Get("AbilityPanelResize.Log.GridPadding",
                    originalGrid.padding.left, originalGrid.padding.right));
            }

            Main.Logger.Log(Localization.Get("AbilityPanelResize.Log.ScrollAdded"));
            Main.Logger.Log(Localization.Get("AbilityPanelResize.Log.HandlesAdded"));
        }

        /// <summary>
        /// Размер, с которым окно рождается: персональный размер текущего героя,
        /// иначе общий шаблон, иначе родная ширина и высота из настроек.
        /// </summary>
        private static Vector2 InitialSize(RectTransform root, Settings settings, UnitEntityData currentUnit)
        {
            float nativeWidth = root.sizeDelta.x;
            float defaultHeight = settings != null ? settings.DefaultHeight : 400f;
            Vector2 max = ResizeLimits.MaxSize;

            if (settings == null || !settings.RememberSize)
            {
                return new Vector2(nativeWidth, Mathf.Clamp(defaultHeight, ResizeLimits.MinSize.y, max.y));
            }

            Vector2? saved = null;
            if (currentUnit != null && settings.TryGetCharacterSize(currentUnit.UniqueId, out Vector2 characterSize))
            {
                saved = characterSize;
            }
            else if (settings.HasSavedSize)
            {
                saved = new Vector2(settings.Width, settings.Height);
            }

            if (saved == null)
            {
                return new Vector2(nativeWidth, Mathf.Clamp(defaultHeight, ResizeLimits.MinSize.y, max.y));
            }

            return new Vector2(
                Mathf.Clamp(saved.Value.x, ResizeLimits.MinSize.x, max.x),
                Mathf.Clamp(saved.Value.y, ResizeLimits.MinSize.y, max.y));
        }

        /// <summary>
        /// Ищет прямоугольник шапки окна. Путь "Background/Header" — только
        /// первая попытка: если разметка окажется другой, честнее взять родителя
        /// текста заголовка, на который у самой вьюхи есть прямая ссылка.
        /// </summary>
        private static RectTransform FindHeader(RectTransform root, ActionBarGroupPCView view, out string source)
        {
            if (root.Find(ModNames.HeaderPath) is RectTransform byPath)
            {
                source = "path:" + ModNames.HeaderPath;
                return byPath;
            }

            Component label = ActionBarGroupAccess.GetGroupNameLabel(view);
            if (label != null && label.transform.parent is RectTransform labelParent)
            {
                source = "labelParent:" + labelParent.name;
                return labelParent;
            }

            foreach (RectTransform child in root.GetComponentsInChildren<RectTransform>(includeInactive: true))
            {
                if (child != root && child.name == ModNames.Header)
                {
                    source = "byName:" + child.name;
                    return child;
                }
            }

            source = "NOT FOUND";
            return null;
        }
    }
}
