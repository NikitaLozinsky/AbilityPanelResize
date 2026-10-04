using System.Collections.Generic;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Какой панель была до мода: всё, что постройка меняет у ванильных
    /// объектов. Снимается в самом начале постройки и нужно ровно для одного —
    /// разобрать панель обратно, когда её выключают в настройках.
    /// </summary>
    public class PanelOriginalState
    {
        private GridLayoutGroupWorkaround m_Grid;
        private ContentSizeFitterExtended m_Fitter;
        private Image m_Background;
        private bool m_BackgroundRaycast;
        private Vector2 m_SizeDelta;
        private float m_PositionX;
        private int m_SiblingIndex;

        public RectTransform SlotContainer { get; private set; }

        public static PanelOriginalState Capture(ActionBarGroupPCView view, RectTransform root)
        {
            Transform background = root.Find(ModNames.Background);
            var state = new PanelOriginalState
            {
                SlotContainer = ActionBarGroupAccess.GetSlotContainer(view),
                m_Grid = root.GetComponent<GridLayoutGroupWorkaround>(),
                m_Fitter = root.GetComponent<ContentSizeFitterExtended>(),
                m_Background = background != null ? background.GetComponent<Image>() : null,
                m_SizeDelta = root.sizeDelta,
                m_PositionX = root.anchoredPosition.x,
                m_SiblingIndex = root.GetSiblingIndex()
            };

            state.m_BackgroundRaycast = state.m_Background != null && state.m_Background.raycastTarget;
            return state;
        }

        public void Restore(RectTransform root)
        {
            if (m_Grid != null)
            {
                m_Grid.enabled = true;
            }

            if (m_Fitter != null)
            {
                m_Fitter.enabled = true;
            }

            if (m_Background != null)
            {
                m_Background.raycastTarget = m_BackgroundRaycast;
            }

            // Высоту дальше всё равно выставит вернувшийся fitter, а положение
            // над экшн-баром — перерисовка группы, которой заканчивается разбор.
            root.sizeDelta = m_SizeDelta;
            root.anchoredPosition = new Vector2(m_PositionX, root.anchoredPosition.y);

            if (root.parent != null)
            {
                root.SetSiblingIndex(Mathf.Min(m_SiblingIndex, root.parent.childCount - 1));
            }
        }
    }

    /// <summary>
    /// Включение и выключение панели на лету, из настроек. Выключенную панель
    /// мод разбирает обратно до вида игры, включённую — строит заново, без
    /// перезахода на локацию.
    ///
    /// Панели ищутся через <c>Resources.FindObjectsOfTypeAll</c>: реестр
    /// адаптеров знает только уже построенные, а включать надо как раз
    /// ванильную. Поиск дорогой, но случается только по щелчку выключателя.
    /// </summary>
    public static class PanelSwitch
    {
        private static readonly List<Transform> s_Moved = new List<Transform>();

        public static bool IsEnabled => Main.Settings == null || Main.Settings.ResizeAbilityPanel;

        public static void ApplyAll()
        {
            foreach (ActionBarGroupPCView view in Resources.FindObjectsOfTypeAll<ActionBarGroupPCView>())
            {
                if (view == null
                    || !view.gameObject.scene.IsValid()
                    || ActionBarGroupAccess.GetGroupType(view) != ActionBarGroupType.Ability)
                {
                    continue;
                }

                try
                {
                    bool built = view.GetComponent<ResizeElementAdapter>() != null;
                    if (IsEnabled && !built)
                    {
                        Enable(view);
                    }
                    else if (!IsEnabled && built)
                    {
                        Disable(view);
                    }
                }
                catch (System.Exception exception)
                {
                    Main.Logger.Error($"{view.name}: не удалось переключить панель — {exception}");
                }
            }
        }

        /// <summary>
        /// Постройка та же, что и при загрузке локации. Но тогда панель ещё
        /// пустая и свёрнутая, а сейчас в ней слоты из ванильной раскладки — с
        /// пустыми слотами-заглушками — и она может быть развёрнута. Поэтому
        /// после постройки перерисовываем группу (заглушки уйдут) и сразу
        /// показываем грани, если панель открыта.
        /// </summary>
        private static void Enable(ActionBarGroupPCView view)
        {
            ResizeElementAdapter adapter = ActionBarGroupPCView_Initialize_Patch.Build(view);
            if (adapter == null)
            {
                return;
            }

            if (ActionBarGroupAccess.GetViewModel(view) != null)
            {
                ActionBarGroupAccess.Redraw(view);
            }

            adapter.SetPanelPartsActive(ActionBarGroupAccess.IsVisible(view));
        }

        /// <summary>
        /// Разбор в обратном постройке порядке. Тонкие места:
        ///
        /// - Маски выключаются <b>до</b> того, как слоты уедут из-под них:
        ///   выключаясь, маска сама сообщает всей графике под собой, что обрезки
        ///   больше нет. Если сперва унести слоты, графика так и осталась бы
        ///   обрезанной по мёртвому прямоугольнику.
        /// - Из <c>Content</c> уносятся <b>все</b> дети, а не только живые слоты:
        ///   игра отдаёт лишние слоты в пул <c>WidgetFactory</c>, и прячет их в
        ///   своё хранилище не сразу. Погибни такой виджет вместе с нашим
        ///   <c>Content</c> — пул потом выдал бы мёртвый объект.
        /// - Удаление немедленное (<c>DestroyImmediate</c>): обычный
        ///   <c>Destroy</c> срабатывает в конце кадра, а перерисовка в конце
        ///   разбора — сейчас, и наши объекты попали бы в ванильную раскладку.
        /// </summary>
        private static void Disable(ActionBarGroupPCView view)
        {
            ResizeElementAdapter adapter = view.GetComponent<ResizeElementAdapter>();
            RectTransform root = view.transform as RectTransform;
            if (adapter == null || root == null)
            {
                return;
            }

            PanelOriginalState original = adapter.Original;
            RectTransform viewport = adapter.Viewport;
            RectTransform content = adapter.Content;

            adapter.Dismantle();

            foreach (ConvertPopupOriginalParent marker in root.GetComponentsInChildren<ConvertPopupOriginalParent>(includeInactive: true))
            {
                marker.Restore();
                Object.DestroyImmediate(marker);
            }

            if (viewport != null)
            {
                SetEnabled(viewport.GetComponent<Mask>(), false);
                SetEnabled(viewport.GetComponent<RectMask2D>(), false);
            }

            RectTransform container = original != null && original.SlotContainer != null
                ? original.SlotContainer
                : root;

            s_Moved.Clear();
            if (content != null)
            {
                while (content.childCount > 0)
                {
                    Transform child = content.GetChild(0);
                    child.SetParent(container, worldPositionStays: false);
                    s_Moved.Add(child);
                }
            }

            ActionBarGroupAccess.SetSlotContainer(view, container);

            foreach (Transform moved in s_Moved)
            {
                foreach (MaskableGraphic graphic in moved.GetComponentsInChildren<MaskableGraphic>(includeInactive: true))
                {
                    graphic.RecalculateClipping();
                    graphic.RecalculateMasking();
                }
            }

            s_Moved.Clear();

            // ScrollRect — первым: выключаясь, он отписывается от скроллбара, и
            // скроллбар к этому моменту должен быть ещё жив.
            DestroyComponent(root.GetComponent<ScrollRect>());

            foreach (PanelResizeHandle handle in adapter.Handles)
            {
                if (handle != null)
                {
                    Object.DestroyImmediate(handle.gameObject);
                }
            }

            if (adapter.ScrollbarHolder != null)
            {
                Object.DestroyImmediate(adapter.ScrollbarHolder);
            }

            if (viewport != null)
            {
                Object.DestroyImmediate(viewport.gameObject);
            }

            DestroyComponent(root.GetComponent<HeaderInsetTracker>());
            DestroyComponent(root.GetComponent<AbilityPanelDiagnostics>());

            original?.Restore(root);
            Object.DestroyImmediate(adapter);

            if (ActionBarGroupAccess.GetViewModel(view) != null)
            {
                ActionBarGroupAccess.Redraw(view);
            }

            Main.Logger.Log(Localization.Get("AbilityPanelResize.Log.PanelRestored"));
        }

        private static void SetEnabled(Behaviour behaviour, bool enabled)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }

        private static void DestroyComponent(Component component)
        {
            if (component != null)
            {
                Object.DestroyImmediate(component);
            }
        }
    }
}
