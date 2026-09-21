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
    [HarmonyPatch(typeof(ActionBarGroupPCView), nameof(ActionBarGroupPCView.Initialize))]
    public static class ActionBarGroupPCView_Initialize_AddScroll_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ActionBarGroupPCView __instance, ActionBarGroupType type)
        {
            if (type != ActionBarGroupType.Ability)
            {
                return;
            }

            RectTransform root = __instance.transform as RectTransform;
            if (root == null || root.Find(ModNames.Viewport) != null)
            {
                return;
            }

            Settings settings = Main.Settings;
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
                SlotClipping.Enable(slot.transform);
            }

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

            float nativeWidth = root.sizeDelta.x;
            float defaultHeight = settings != null ? settings.DefaultHeight : 400f;

            Vector2? characterSize = null;
            if (settings != null && settings.RememberSize)
            {
                UnitEntityData currentUnit = Game.Instance?.SelectionCharacter?.CurrentSelectedCharacter;
                if (currentUnit != null && settings.TryGetCharacterSize(currentUnit.UniqueId, out Vector2 savedSize))
                {
                    characterSize = savedSize;
                }
            }

            bool useSavedSize = settings != null && settings.RememberSize
                && (characterSize.HasValue || settings.HasSavedSize);

            float initialWidth = useSavedSize
                ? Mathf.Clamp(characterSize?.x ?? settings.Width, ResizeLimits.MinSize.x, ResizeLimits.MaxSize.x)
                : nativeWidth;
            float initialHeight = useSavedSize
                ? Mathf.Clamp(characterSize?.y ?? settings.Height, ResizeLimits.MinSize.y, ResizeLimits.MaxSize.y)
                : Mathf.Clamp(defaultHeight, ResizeLimits.MinSize.y, ResizeLimits.MaxSize.y);

            if (!Mathf.Approximately(initialWidth, nativeWidth))
            {
                float nativeCenterX = root.anchoredPosition.x + nativeWidth / 2f;
                Vector2 anchoredPosition = root.anchoredPosition;
                anchoredPosition.x = nativeCenterX - initialWidth / 2f;
                root.anchoredPosition = anchoredPosition;
            }

            root.sizeDelta = new Vector2(initialWidth, initialHeight);

            ScrollRect scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = settings != null ? settings.ScrollSensitivity : 20f;

            ResizeElementAdapter adapter = ResizeElementAdapter.Ensure(root.gameObject);

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
            diagnostics.HeaderSource = headerSource;

            ActionBarGroupAccess.SetSlotContainer(__instance, contentRect);

            Image backgroundImage = root.Find(ModNames.Background)?.GetComponent<Image>();
            if (backgroundImage != null)
            {
                backgroundImage.raycastTarget = false;
            }

            if (originalGrid != null)
            {
                Main.Logger.Log(Localization.Get("AbilityPanelResize.Log.GridPadding",
                    originalGrid.padding.left, originalGrid.padding.right));
            }

            Main.Logger.Log(Localization.Get("AbilityPanelResize.Log.ScrollAdded"));
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
