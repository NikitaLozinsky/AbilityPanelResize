using System.Collections.Generic;
using Kingmaker.UI;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Мозг пропатченной панели: единственное место, которое меняет её размер.
    /// Хендлы только сообщают, какой размер хочет мышь, — компенсацию позиции,
    /// сохранение и применение настроек делает этот компонент.
    /// </summary>
    public class ResizeElementAdapter : MonoBehaviour
    {
        /// <summary>
        /// Живые экземпляры. Нужен именно реестр, а не
        /// <c>FindObjectsOfType</c>: тот не видит выключенные объекты, а панель
        /// со свёрнутым или пустым списком способностей как раз выключена — и
        /// настройки до неё не доезжали.
        ///
        /// Список статический, поэтому из него обязательно нужно вычищать
        /// умершие панели: этим занимается <see cref="OnDestroy"/>, а
        /// <see cref="ApplyLiveSettingsToAll"/> дополнительно выкидывает всё,
        /// что успело умереть мимо него.
        /// </summary>
        private static readonly List<ResizeElementAdapter> s_Instances = new List<ResizeElementAdapter>();

        private RectTransform m_Rect;
        private ActionBarGroupPCView m_GroupView;
        private ScrollRect m_ScrollRect;
        private GridLayoutGroup m_Grid;
        private HeaderInsetTracker m_HeaderTracker;
        private Mask m_Mask;

        private readonly List<PanelResizeHandle> m_Handles = new List<PanelResizeHandle>();
        private GameObject m_ScrollbarHolder;
        private string m_CurrentCharacterId;

        private float? m_InitialCenterX;

        private RectTransform Rect => m_Rect != null ? m_Rect : (m_Rect = GetComponent<RectTransform>());

        private ScrollRect Scroll => m_ScrollRect != null ? m_ScrollRect : (m_ScrollRect = GetComponent<ScrollRect>());

        private GridLayoutGroup Grid
        {
            get
            {
                if (m_Grid == null)
                {
                    m_Grid = transform.Find(ModNames.ContentPath)?.GetComponent<GridLayoutGroup>();
                }

                return m_Grid;
            }
        }

        private Mask Mask
        {
            get
            {
                if (m_Mask == null)
                {
                    m_Mask = transform.Find(ModNames.Viewport)?.GetComponent<Mask>();
                }

                return m_Mask;
            }
        }

        /// <summary>
        /// Достаёт адаптер панели, создавая его при необходимости. Оба патча на
        /// <c>Initialize</c> зовут этот метод, а порядок их выполнения Harmony
        /// не гарантирует — поэтому метод обязан быть идемпотентным.
        /// </summary>
        public static ResizeElementAdapter Ensure(GameObject panel)
        {
            ResizeElementAdapter adapter = panel.GetComponent<ResizeElementAdapter>();
            if (adapter == null)
            {
                adapter = panel.AddComponent<ResizeElementAdapter>();
            }

            if (!s_Instances.Contains(adapter))
            {
                s_Instances.Add(adapter);
            }

            return adapter;
        }

        public static void ApplyLiveSettingsToAll()
        {
            for (int i = s_Instances.Count - 1; i >= 0; i--)
            {
                ResizeElementAdapter adapter = s_Instances[i];
                if (adapter == null)
                {
                    s_Instances.RemoveAt(i);
                    continue;
                }

                adapter.ApplyLiveSettings();
            }
        }

        public void ApplyLiveSettings()
        {
            Settings settings = Main.Settings;
            if (settings == null)
            {
                return;
            }

            if (Scroll != null)
            {
                Scroll.scrollSensitivity = settings.ScrollSensitivity;
            }

            if (Grid != null)
            {
                Grid.childAlignment = settings.CenterIcons ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
            }

            // Mask умеет включаться и выключаться на лету: при выключении он сам
            // возвращает детям их обычный материал.
            if (Mask != null)
            {
                Mask.enabled = settings.StencilMask;
            }

            foreach (PanelResizeHandle handle in m_Handles)
            {
                if (handle != null)
                {
                    handle.ApplyThickness(settings.HandleThickness);
                }
            }

            SetSizeDelta(ResizeLimits.Clamp(Rect.sizeDelta));
        }

        public void BeginResize()
        {
            if (Scroll != null)
            {
                Scroll.enabled = false;
            }
        }

        public void EndResize()
        {
            if (Scroll != null)
            {
                Scroll.enabled = true;
            }

            Settings settings = Main.Settings;
            if (settings == null || Main.ModEntry == null || !settings.RememberSize)
            {
                return;
            }

            Vector2 size = Rect.sizeDelta;
            settings.Width = size.x;
            settings.Height = size.y;
            if (m_CurrentCharacterId != null)
            {
                settings.SetCharacterSize(m_CurrentCharacterId, size);
            }

            settings.Save(Main.ModEntry);
        }

        public void RegisterHandle(PanelResizeHandle handle)
        {
            handle.gameObject.SetActive(false);
            m_Handles.Add(handle);
        }

        public void RegisterScrollbar(GameObject holder)
        {
            holder.SetActive(false);
            m_ScrollbarHolder = holder;
        }

        /// <summary>
        /// Всё, что мы дорисовали к панели сами, живёт ровно столько, сколько
        /// панель развёрнута. У ванильных детей за это отвечает
        /// <c>m_TogglableChildren</c>, но лезть в чужой сериализованный список
        /// рискованнее, чем держать свой.
        /// </summary>
        public void SetPanelPartsActive(bool active)
        {
            foreach (PanelResizeHandle handle in m_Handles)
            {
                if (handle != null)
                {
                    handle.gameObject.SetActive(active);
                }
            }

            if (m_ScrollbarHolder != null)
            {
                m_ScrollbarHolder.SetActive(active);
            }

            if (active)
            {
                // Разворачивание — единственный момент, когда раскладка шапки
                // могла поменяться незаметно для нас.
                HeaderTracker()?.Remeasure();
            }
        }

        public void SeedCharacterId(string characterId)
        {
            if (m_CurrentCharacterId == null)
            {
                m_CurrentCharacterId = characterId;
            }
        }

        public void ApplyCharacterSize(string characterId)
        {
            if (characterId == m_CurrentCharacterId)
            {
                return;
            }

            m_CurrentCharacterId = characterId;

            Settings settings = Main.Settings;
            if (settings == null || !settings.RememberSize)
            {
                return;
            }

            if (!settings.TryGetCharacterSize(characterId, out Vector2 size))
            {
                if (!settings.HasSavedSize)
                {
                    return;
                }

                size = new Vector2(settings.Width, settings.Height);
            }

            SetSizeDelta(ResizeLimits.Clamp(size));
        }

        public void SetSizeDelta(Vector2 size)
        {
            if (m_InitialCenterX == null)
            {
                m_InitialCenterX = Rect.anchoredPosition.x + Rect.sizeDelta.x / 2f;
            }

            Rect.sizeDelta = size;

            // Pivot по X равен нулю, поэтому рост ширины физически идёт только
            // вправо. Держим центр на месте, чтобы кнопка сворачивания не уезжала.
            Vector2 anchoredPosition = Rect.anchoredPosition;
            anchoredPosition.x = m_InitialCenterX.Value - size.x / 2f;
            Rect.anchoredPosition = anchoredPosition;

            RepositionToKeepBottomFixed();
        }

        private void OnDestroy()
        {
            s_Instances.Remove(this);
        }

        private void Update()
        {
            // Порядок условий не случаен: чтение статического поля дешевле
            // обращения к вводу, а курсор ресайза взведён считанные кадры.
            if (CursorController.IsResizeCursor && Input.GetMouseButtonUp(0))
            {
                CursorController.IsResizeCursor = false;
                UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }

        private HeaderInsetTracker HeaderTracker()
        {
            if (m_HeaderTracker == null)
            {
                m_HeaderTracker = GetComponent<HeaderInsetTracker>();
            }

            return m_HeaderTracker;
        }

        private ActionBarGroupPCView GroupView()
        {
            if (m_GroupView == null)
            {
                m_GroupView = GetComponent<ActionBarGroupPCView>();
            }

            return m_GroupView;
        }

        /// <summary>
        /// Игра держит нижнюю кромку окна прижатой к экшн-бару. Штатно это
        /// делает корутина с задержкой в кадр, из-за чего размер и позиция
        /// расходились на кадр и картинка дёргалась. Повторяем ту же формулу
        /// сами, но синхронно.
        /// </summary>
        private void RepositionToKeepBottomFixed()
        {
            ActionBarGroupPCView view = GroupView();
            if (view == null || !ActionBarGroupAccess.IsVisible(view))
            {
                return;
            }

            // 5f - не наша константа. Она из формулы самой игры, и там она тоже
            // ничем не обоснована.
            float targetY = Rect.sizeDelta.y - 5f - ActionBarGroupAccess.GetVisiblePositionDelta(view);

            Vector2 anchoredPosition = Rect.anchoredPosition;
            anchoredPosition.y = targetY;
            Rect.anchoredPosition = anchoredPosition;
        }
    }
}
