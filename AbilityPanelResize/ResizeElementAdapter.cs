using System.Collections.Generic;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Мозг пропатченной панели: единственное место, которое меняет её размер.
    /// Хендлы только сообщают, какой размер хочет мышь, — компенсацию позиции,
    /// сохранение и применение настроек делает этот компонент.
    ///
    /// Он же — единственный признак «эта панель наша». Раньше каждый патч
    /// отвечал на этот вопрос сам, ища ребёнка по имени
    /// (<c>root.Find("AbilityPanelResize_Viewport")</c>), и делал это на каждой
    /// перерисовке группы. Теперь достаточно <c>GetComponent</c>: адаптер висит
    /// только на построенной панели способностей, а ссылки на её части держит
    /// у себя, найденные один раз при постройке.
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
        private RectTransform m_Content;
        private GridLayoutGroup m_Grid;
        private Mask m_Mask;
        private HeaderInsetTracker m_HeaderTracker;
        private AbilityPanelDiagnostics m_Diagnostics;

        private readonly List<PanelResizeHandle> m_Handles = new List<PanelResizeHandle>();
        private GameObject m_ScrollbarHolder;
        private string m_CurrentCharacterId;

        /// Хендлы рождаются выключенными: свёрнутое окно — обычное состояние
        /// панели при заходе в локацию.
        private bool m_PartsActive;

        private float? m_InitialCenterX;

        /// Сетка иконок. Нужна патчам, которые возвращают слотам обрезку.
        public RectTransform Content => m_Content;

        /// Панель достроена до конца. До этого момента на неё нельзя опираться:
        /// компонент появляется в самом начале постройки, части — по ходу.
        public bool IsBuilt => m_Content != null;

        public IReadOnlyList<PanelResizeHandle> Handles => m_Handles;

        private RectTransform Rect => m_Rect != null ? m_Rect : (m_Rect = GetComponent<RectTransform>());

        /// <summary>
        /// Достаёт адаптер панели, создавая его при необходимости.
        /// </summary>
        public static ResizeElementAdapter Ensure(ActionBarGroupPCView view)
        {
            ResizeElementAdapter adapter = view.GetComponent<ResizeElementAdapter>();
            if (adapter == null)
            {
                adapter = view.gameObject.AddComponent<ResizeElementAdapter>();
            }

            adapter.m_GroupView = view;

            // Потолки размера считаются от холста, а первый размер панели
            // выставляется сразу же при постройке — значит холст надо померить
            // раньше, чем что-то зависящее от него.
            adapter.ReportCanvasSize();

            if (!s_Instances.Contains(adapter))
            {
                s_Instances.Add(adapter);
            }

            return adapter;
        }

        /// <summary>
        /// Последний шаг постройки: разово запоминаем всё, что дальше
        /// понадобится в работе, чтобы не искать это заново на каждой
        /// перерисовке.
        /// </summary>
        public void CacheParts(RectTransform viewport, RectTransform content)
        {
            m_ScrollRect = GetComponent<ScrollRect>();
            m_Grid = content != null ? content.GetComponent<GridLayoutGroup>() : null;
            m_Mask = viewport != null ? viewport.GetComponent<Mask>() : null;
            m_HeaderTracker = GetComponent<HeaderInsetTracker>();
            m_Diagnostics = GetComponent<AbilityPanelDiagnostics>();

            if (m_Diagnostics != null)
            {
                m_Diagnostics.enabled = Main.Settings != null && Main.Settings.Diagnostics;
            }

            // Присваивается последним: именно по нему остальной код понимает,
            // что панель достроена.
            m_Content = content;
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

            if (m_ScrollRect != null)
            {
                m_ScrollRect.scrollSensitivity = settings.ScrollSensitivity;
            }

            if (m_Grid != null)
            {
                m_Grid.childAlignment = settings.CenterIcons ? TextAnchor.UpperCenter : TextAnchor.UpperLeft;
            }

            // Mask умеет включаться и выключаться на лету: при выключении он сам
            // возвращает детям их обычный материал.
            if (m_Mask != null)
            {
                m_Mask.enabled = settings.StencilMask;
            }

            // Выключенный компонент не получает Update вовсе — это дешевле, чем
            // выходить из него по проверке на каждом кадре.
            if (m_Diagnostics != null)
            {
                m_Diagnostics.enabled = settings.Diagnostics;
            }

            foreach (PanelResizeHandle handle in m_Handles)
            {
                if (handle != null)
                {
                    handle.ApplyThickness(settings.HandleThickness);
                }
            }

            // Разрешение могли сменить прямо в сессии, а потолки размера
            // считаются от холста.
            ReportCanvasSize();
            SetSizeDelta(ResizeLimits.Clamp(Rect.sizeDelta));
        }

        public void BeginResize()
        {
            if (m_ScrollRect != null)
            {
                m_ScrollRect.enabled = false;
            }
        }

        /// <summary>
        /// Конец работы с гранью. <paramref name="save"/> — было ли реальное
        /// перетаскивание: клик по грани без движения тоже приводит сюда, и
        /// писать из-за него файл настроек на диск незачем.
        /// </summary>
        public void EndResize(bool save)
        {
            if (m_ScrollRect != null)
            {
                m_ScrollRect.enabled = true;
            }

            Settings settings = Main.Settings;
            if (!save || settings == null || Main.ModEntry == null || !settings.RememberSize)
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
        ///
        /// Сюда приходят не только клики по ▼. Игра дёргает <c>SetVisible</c>
        /// ещё и через кадр после каждой перерисовки группы — то есть в бою
        /// регулярно, с тем же самым состоянием. Поэтому первым делом отсекаем
        /// повторы: иначе на каждую перерисовку шли бы шесть <c>SetActive</c>
        /// и новый цикл перепроверки шапки.
        /// </summary>
        public void SetPanelPartsActive(bool active)
        {
            if (active == m_PartsActive)
            {
                return;
            }

            // Хендлы — единственная часть окна, чьей активностью распоряжаемся
            // мы сами, и единственная, которая торчит наружу за его границу.
            // Если они погаснут не вовремя, на экране это выглядит как «окно на
            // месте, но грани не ловятся» — поэтому под диагностикой пишем в
            // лог и сам факт, и того, кто его вызвал.
            if (Main.Settings != null && Main.Settings.Diagnostics)
            {
                Main.Logger.Log($"=== AbilityPanelResize: части панели {(active ? "включены" : "ВЫКЛЮЧЕНЫ")} ===\n"
                                + new System.Diagnostics.StackTrace(fNeedFileInfo: false));
            }

            m_PartsActive = active;

            foreach (PanelResizeHandle handle in m_Handles)
            {
                if (handle == null)
                {
                    continue;
                }

                handle.gameObject.SetActive(active);

                // Хендлы обязаны лежать выше всего внутри окна. Постройка их и
                // так создаёт последними, но подменю вариантов способности на
                // время своего открытия перекладывает себя в корень панели.
                if (active)
                {
                    handle.transform.SetAsLastSibling();
                }
            }

            if (m_ScrollbarHolder != null)
            {
                m_ScrollbarHolder.SetActive(active);
            }

            if (!active)
            {
                return;
            }

            // Разворачивание — единственный момент, когда раскладка шапки
            // могла поменяться незаметно для нас. Заодно перемеряем холст:
            // при постройке панель могла висеть на выключенной ветке, где
            // мерить было нечего.
            m_HeaderTracker?.Remeasure();
            ReportCanvasSize();
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
            // Страховка от залипшего курсора: кнопку отпустили не над гранью, и
            // события выхода хендл не получил. Трогаем только тот курсор,
            // который взяли сами, — общий флаг может держать и боевой лог.
            if (PanelResizeHandle.CursorHeld && Input.GetMouseButtonUp(0))
            {
                PanelResizeHandle.ReleaseCursor();
            }

            // Прицеливание способностью может начаться и закончиться, пока
            // мышь стоит на грани, — а курсор прицеливания рисует игра, и
            // отдавать его ей надо вовремя.
            PanelResizeHandle.SyncWithAbilityTargeting(m_Handles);
        }

        /// <summary>
        /// Сообщает лимитам размер холста интерфейса.
        ///
        /// Ищем через <c>GetComponentsInParent(includeInactive: true)</c>, а не
        /// через <c>GetComponentInParent</c>: последний пропускает выключенные
        /// объекты, а в момент постройки панель вполне может висеть на
        /// выключенной ветке.
        /// </summary>
        private void ReportCanvasSize()
        {
            Canvas[] canvases = GetComponentsInParent<Canvas>(includeInactive: true);
            if (canvases == null || canvases.Length == 0)
            {
                return;
            }

            Canvas root = canvases[0].rootCanvas;
            if (root != null && root.transform is RectTransform canvasRect)
            {
                ResizeLimits.ReportCanvasSize(canvasRect.rect.size);
            }
        }

        /// <summary>
        /// Игра держит нижнюю кромку окна прижатой к экшн-бару. Штатно это
        /// делает корутина с задержкой в кадр, из-за чего размер и позиция
        /// расходились на кадр и картинка дёргалась. Повторяем ту же формулу
        /// сами, но синхронно.
        /// </summary>
        private void RepositionToKeepBottomFixed()
        {
            if (m_GroupView == null || !ActionBarGroupAccess.IsVisible(m_GroupView))
            {
                return;
            }

            // 5f - не наша константа. Она из формулы самой игры, и там она тоже
            // ничем не обоснована.
            float targetY = Rect.sizeDelta.y - 5f - ActionBarGroupAccess.GetVisiblePositionDelta(m_GroupView);

            Vector2 anchoredPosition = Rect.anchoredPosition;
            anchoredPosition.y = targetY;
            Rect.anchoredPosition = anchoredPosition;
        }
    }
}
