using System.Collections.Generic;
using System.Text;
using Kingmaker.UI;
using TurnBased.Controllers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Дамп геометрии и клиппирования панели в лог. Срабатывает сам, когда
    /// контент уезжает под шапку (то есть ровно тогда, когда баг и виден), и
    /// принудительно по Ctrl+Alt+D. Отдельно по Ctrl+Alt+R — проба указателя
    /// (см. <see cref="BuildPointerProbe"/>).
    ///
    /// Инструмент уже дважды отличал причины, которые на экране выглядят
    /// одинаково, поэтому живёт в проекте, а не выпиливается после фикса.
    /// </summary>
    public class AbilityPanelDiagnostics : MonoBehaviour
    {
        public RectTransform Root;
        public RectTransform Viewport;
        public RectTransform Content;
        public HeaderInsetTracker Tracker;

        /// Как именно нашлась шапка (или почему не нашлась) — пишем словами.
        public string HeaderSource = "?";

        private const int MaxRowsLogged = 14;

        /// Сколько кадров расхождение должно продержаться, чтобы считать его
        /// настоящим, а не опережением события на кадр.
        private const int StuckFramesToReport = 10;

        private readonly Vector3[] m_Corners = new Vector3[4];
        private bool m_AuditDone;
        private bool m_PointerProbeDone;
        private int m_StuckFrames;

        private void Update()
        {
            // По умолчанию выключено: это отладочный инструмент, а не часть
            // работы мода. Тумблер живёт в настройках UMM и действует сразу.
            if (Main.Settings == null || !Main.Settings.Diagnostics)
            {
                return;
            }

            if (!m_AuditDone && Content != null && Content.anchoredPosition.y > 1f)
            {
                m_AuditDone = true;
                Main.Logger.Log(BuildClippingAudit());
                Main.Logger.Log(BuildOverflowAudit());
            }

            CheckPointerStuck();

            bool hotkey = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.R);
            if (!hotkey)
            {
                return;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (!ctrl || !alt)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                Main.Logger.Log(BuildPointerProbe());
                return;
            }

            Main.Logger.Log(BuildReport());
            Main.Logger.Log(BuildClippingAudit());
            Main.Logger.Log(BuildOverflowAudit());
        }

        /// <summary>
        /// Ловит баг сам, без хоткея: курсор геометрически внутри прямоугольника
        /// хендла, а события входа хендл так и не получил. Это ровно тот случай,
        /// который на экране выглядит как «грань не ловится» — значит рейкаст
        /// перехватил кто-то другой, и самое время выписать, кто именно.
        ///
        /// Требуем, чтобы расхождение держалось несколько кадров подряд: события
        /// указателя приходят из <c>EventSystem.Update</c>, и в первый кадр входа
        /// наш <c>Update</c> может опередить их на законных основаниях.
        /// </summary>
        private void CheckPointerStuck()
        {
            if (m_PointerProbeDone || Root == null)
            {
                return;
            }

            Camera camera = ProbeCamera();
            Vector2 mouse = Input.mousePosition;
            PanelResizeHandle stuck = null;

            foreach (PanelResizeHandle handle in Root.GetComponentsInChildren<PanelResizeHandle>(includeInactive: true))
            {
                if (handle.transform is RectTransform rect
                    && !handle.PointerInside
                    && RectTransformUtility.RectangleContainsScreenPoint(rect, mouse, camera))
                {
                    stuck = handle;
                    break;
                }
            }

            if (stuck == null)
            {
                m_StuckFrames = 0;
                return;
            }

            if (++m_StuckFrames < StuckFramesToReport)
            {
                return;
            }

            m_PointerProbeDone = true;
            Main.Logger.Log($"курсор внутри хендла {stuck.name}, но событие входа не пришло — разбираем, кто перехватил");
            Main.Logger.Log(BuildPointerProbe());
        }

        /// <summary>
        /// Камера для пересчёта экранных координат. Спрашивать её надо у
        /// корневого Canvas: у вложенного собственное поле камеры обычно пустое,
        /// и тест попадания в прямоугольник молча отвечал бы неправдой.
        /// </summary>
        private Camera ProbeCamera()
        {
            Canvas canvas = Root != null ? Root.GetComponentInParent<Canvas>() : null;
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            return root != null && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
        }

        /// <summary>
        /// Почему мышь не доезжает до хендла ресайза.
        ///
        /// «Курсор не меняется и окно не тянется» — это один и тот же симптом у
        /// трёх совершенно разных причин, и на экране они неразличимы:
        ///
        /// * хендл выключен — тогда <c>activeInHierarchy=False</c>;
        /// * хендл на месте, но сверху что-то лежит — тогда он есть в списке
        ///   рейкаста, но не первым;
        /// * хендл вообще не участвует в рейкасте — значит его отсекает фильтр
        ///   у кого-то из предков (<c>CanvasGroup.blocksRaycasts</c>, маска), и
        ///   искать надо в цепочке предков, а не среди соседей.
        ///
        /// Отдельной строкой пишем <c>IsResizeCursor</c>: этот флаг общий на всю
        /// игру, и если он завис в <c>True</c> от чужого окна, наш курсор молча
        /// не выставится, хотя с рейкастом всё в порядке.
        /// </summary>
        private string BuildPointerProbe()
        {
            var report = new StringBuilder();
            report.AppendLine("=== AbilityPanelResize: проба указателя ===");

            if (Root == null)
            {
                report.AppendLine("root: NULL");
                return report.ToString();
            }

            Vector2 mouse = Input.mousePosition;
            Canvas canvas = Root.GetComponentInParent<Canvas>()?.rootCanvas;
            Camera camera = ProbeCamera();

            report.AppendLine($"мышь={mouse} IsResizeCursor={CursorController.IsResizeCursor} "
                              + $"пошаговыйБой={SafeTurnBasedState()}");
            report.AppendLine($"корневой canvas={(canvas != null ? canvas.name : "NULL")} "
                              + $"renderMode={(canvas != null ? canvas.renderMode.ToString() : "?")} "
                              + $"camera={(camera != null ? camera.name : "null (overlay)")}");

            report.AppendLine("хендлы:");
            foreach (PanelResizeHandle handle in Root.GetComponentsInChildren<PanelResizeHandle>(includeInactive: true))
            {
                report.AppendLine("  " + DescribeHandle(handle, mouse, camera));
            }

            report.AppendLine("предки окна (снизу вверх):");
            for (Transform t = Root; t != null; t = t.parent)
            {
                report.AppendLine("  " + DescribeAncestor(t, mouse, camera));
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                report.AppendLine("EventSystem.current == null — рейкаст не проверить");
                return report.ToString();
            }

            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = mouse }, results);

            report.AppendLine($"под курсором объектов: {results.Count} (первый — самый верхний)");
            for (int i = 0; i < results.Count && i < MaxRowsLogged; i++)
            {
                RaycastResult result = results[i];
                report.AppendLine($"  {i}: {FullPath(result.gameObject.transform)} "
                                  + $"[{(result.module != null ? result.module.GetType().Name : "?")}] "
                                  + $"sortingOrder={result.sortingOrder} depth={result.depth}");
            }

            return report.ToString();
        }

        private string DescribeHandle(PanelResizeHandle handle, Vector2 mouse, Camera camera)
        {
            var rect = handle.transform as RectTransform;
            if (rect == null)
            {
                return handle.name + ": нет RectTransform";
            }

            rect.GetWorldCorners(m_Corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, m_Corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, m_Corners[2]);

            Image image = handle.GetComponent<Image>();
            bool underMouse = RectTransformUtility.RectangleContainsScreenPoint(rect, mouse, camera);

            return $"{handle.name}: activeSelf={handle.gameObject.activeSelf} "
                   + $"activeInHierarchy={handle.gameObject.activeInHierarchy} "
                   + $"raycastTarget={(image != null ? image.raycastTarget.ToString() : "нет Image")} "
                   + $"экран=({min.x:0}..{max.x:0} x {min.y:0}..{max.y:0}) подМышью={underMouse}";
        }

        /// Всё, что у предка может отсечь рейкаст или увести отрисовку в другой слой.
        private string DescribeAncestor(Transform t, Vector2 mouse, Camera camera)
        {
            var line = new StringBuilder(t.name);
            line.Append($" active={t.gameObject.activeSelf}");

            if (t is RectTransform rect)
            {
                line.Append($" подМышью={RectTransformUtility.RectangleContainsScreenPoint(rect, mouse, camera)}");
            }

            var group = t.GetComponent<CanvasGroup>();
            if (group != null)
            {
                line.Append($" CanvasGroup(alpha={group.alpha:0.##} blocksRaycasts={group.blocksRaycasts} "
                            + $"interactable={group.interactable} ignoreParent={group.ignoreParentGroups})");
            }

            var canvas = t.GetComponent<Canvas>();
            if (canvas != null)
            {
                line.Append($" Canvas(override={canvas.overrideSorting} order={canvas.sortingOrder})");
            }

            var rectMask = t.GetComponent<RectMask2D>();
            if (rectMask != null)
            {
                line.Append($" RectMask2D(enabled={rectMask.isActiveAndEnabled})");
            }

            var mask = t.GetComponent<Mask>();
            if (mask != null)
            {
                line.Append($" Mask(enabled={mask.isActiveAndEnabled})");
            }

            var raycaster = t.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                line.Append($" GraphicRaycaster(enabled={raycaster.enabled})");
            }

            return line.ToString();
        }

        private static string SafeTurnBasedState()
        {
            try
            {
                return CombatController.IsInTurnBasedCombat().ToString();
            }
            catch (System.Exception exception)
            {
                return "не определить: " + exception.Message;
            }
        }

        private static string FullPath(Transform target)
        {
            string path = target.name;
            for (Transform t = target.parent; t != null; t = t.parent)
            {
                path = t.name + "/" + path;
            }

            return path;
        }

        /// <summary>
        /// Что реально вылезает выше верхней кромки области прокрутки.
        ///
        /// Ключевое поле — `culled`: отбраковку `RectMask2D` делает на уровне
        /// рендерера, и она в этой игре работает. Если запись есть в списке, но
        /// `culled=True` — на экране её нет, беспокоиться не о чем. Опасны
        /// записи с `culled=False`: они видны поверх шапки.
        ///
        /// `stencil` показывает, поддерживает ли материал трафаретную обрезку
        /// (см. ViewportMask) — именно ей мы теперь и обрезаем.
        /// </summary>
        private string BuildOverflowAudit()
        {
            var report = new StringBuilder();
            report.AppendLine("=== AbilityPanelResize: что вылезает за верх области прокрутки ===");

            if (Root == null || Viewport == null)
            {
                report.AppendLine("root или viewport отсутствует");
                return report.ToString();
            }

            Viewport.GetWorldCorners(m_Corners);
            float viewportTop = Root.InverseTransformPoint(m_Corners[1]).y;
            report.AppendLine($"верх области прокрутки в координатах окна: {viewportTop:0.#}");

            int found = 0;
            foreach (Graphic graphic in Root.GetComponentsInChildren<Graphic>(includeInactive: false))
            {
                if (!graphic.isActiveAndEnabled)
                {
                    continue;
                }

                var rect = graphic.transform as RectTransform;
                if (rect == null || !IsInside(Content, graphic.transform))
                {
                    // Шапка, задник, скроллбар и хендлы выше viewport'а законно.
                    continue;
                }

                rect.GetWorldCorners(m_Corners);
                float top = Mathf.Max(
                    Root.InverseTransformPoint(m_Corners[1]).y,
                    Root.InverseTransformPoint(m_Corners[2]).y);

                if (top <= viewportTop + 1f)
                {
                    continue;
                }

                found++;
                if (found > MaxRowsLogged)
                {
                    continue;
                }

                report.AppendLine("  " + Describe(graphic, rect, top - viewportTop));
            }

            report.AppendLine(found == 0
                ? "  выше кромки ничего нет — перерисовки поверх шапки быть не должно"
                : $"  всего выше кромки: {found}");

            return report.ToString();
        }

        private string Describe(Graphic graphic, RectTransform rect, float above)
        {
            Material material = graphic.materialForRendering;
            string shader = material != null && material.shader != null ? material.shader.name : "NULL";
            bool stencil = material != null && material.HasProperty("_Stencil") && material.HasProperty("_StencilComp");
            bool clipping = graphic.canvasRenderer != null && graphic.canvasRenderer.hasRectClipping;
            bool culled = graphic.canvasRenderer != null && graphic.canvasRenderer.cull;

            // maskable есть только у MaskableGraphic; не-maskable графика в UI
            // встречается редко, но именно она нас бы и интересовала.
            string maskable = graphic is MaskableGraphic maskableGraphic
                ? maskableGraphic.maskable.ToString()
                : "н/д (не MaskableGraphic)";

            return $"{PathFrom(Content, rect)} [{graphic.GetType().Name}] выше на {above:0.#} "
                   + $"culled={culled} maskable={maskable} hasRectClipping={clipping} "
                   + $"stencil={stencil} shader='{shader}' mat='{(material != null ? material.name : "NULL")}'";
        }

        private static bool IsInside(Transform parent, Transform child)
        {
            if (parent == null)
            {
                return false;
            }

            for (Transform t = child; t != null; t = t.parent)
            {
                if (t == parent)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Проходит по всей графике внутри контента и выписывает ту, которой
        /// обрезка вообще не назначена (в отличие от аудита выше, где обрезка
        /// назначена, но может не работать).
        /// </summary>
        private string BuildClippingAudit()
        {
            var report = new StringBuilder();
            report.AppendLine("=== AbilityPanelResize: аудит клиппирования ===");

            if (Content == null || Viewport == null)
            {
                report.AppendLine("контента или viewport'а нет");
                return report.ToString();
            }

            RectMask2D expected = Viewport.GetComponent<RectMask2D>();
            int total = 0;
            int offenders = 0;

            foreach (MaskableGraphic graphic in Content.GetComponentsInChildren<MaskableGraphic>(includeInactive: true))
            {
                if (!graphic.isActiveAndEnabled)
                {
                    continue;
                }

                total++;
                if (graphic.canvasRenderer != null && graphic.canvasRenderer.hasRectClipping)
                {
                    continue;
                }

                offenders++;
                if (offenders > MaxRowsLogged)
                {
                    continue;
                }

                RectMask2D found = MaskUtilities.GetRectMaskForClippable(graphic);
                report.AppendLine($"  {PathFrom(Content, graphic.transform)} [{graphic.GetType().Name}] "
                                  + $"maskable={graphic.maskable} "
                                  + $"mask={(found != null ? found.name : "NULL")}{(found == expected ? "" : " (НЕ наша!)")} "
                                  + $"canvasBelowContent={NearestCanvasBelowContent(graphic.transform)}");
            }

            report.AppendLine($"итого активной графики: {total}, без обрезки: {offenders}");
            return report.ToString();
        }

        /// Ближайший Canvas между графикой и контентом — главный подозреваемый,
        /// если маска не нашлась: Unity обрывает поиск на Canvas'е с overrideSorting.
        private string NearestCanvasBelowContent(Transform from)
        {
            for (Transform t = from; t != null && t != Content; t = t.parent)
            {
                var canvas = t.GetComponent<Canvas>();
                if (canvas != null)
                {
                    return $"{t.name}(override={canvas.overrideSorting})";
                }
            }

            return "нет";
        }

        private string BuildReport()
        {
            var report = new StringBuilder();
            report.AppendLine("=== AbilityPanelResize: дамп геометрии панели ===");

            if (Root == null)
            {
                report.AppendLine("root: NULL");
                return report.ToString();
            }

            report.AppendLine($"root: rect={Fmt(Root.rect)} pivot={Root.pivot} anchoredPos={Root.anchoredPosition}");

            RectTransform header = Tracker != null ? Tracker.Header : null;
            report.AppendLine($"header: source={HeaderSource} found={(header != null ? header.name : "NULL")}");
            if (header != null)
            {
                header.GetWorldCorners(m_Corners);
                float bottomInRoot = Root.InverseTransformPoint(m_Corners[0]).y;
                float topInRoot = Root.InverseTransformPoint(m_Corners[1]).y;
                report.AppendLine($"header: rectH={header.rect.height:0.#} topInRoot={topInRoot:0.#} "
                                  + $"bottomInRoot={bottomInRoot:0.#} rootTop={Root.rect.yMax:0.#} "
                                  + $"=> inset={(Root.rect.yMax - bottomInRoot):0.#}");
            }

            if (Tracker != null)
            {
                report.AppendLine($"tracker: appliedInset={Tracker.AppliedInset:0.#}");
            }

            if (Viewport != null)
            {
                report.AppendLine($"viewport: offsetMin={Viewport.offsetMin} offsetMax={Viewport.offsetMax} "
                                  + $"rect={Fmt(Viewport.rect)}");

                RectMask2D mask = Viewport.GetComponent<RectMask2D>();
                report.AppendLine($"viewport mask: {(mask != null ? $"id={mask.GetInstanceID()} enabled={mask.isActiveAndEnabled}" : "НЕТ")}");
            }

            if (Content != null)
            {
                report.AppendLine($"content: rect={Fmt(Content.rect)} anchoredPos={Content.anchoredPosition} "
                                  + $"children={Content.childCount}");
            }

            return report.ToString();
        }

        private static string PathFrom(Transform root, Transform target)
        {
            string path = target.name;
            for (Transform t = target.parent; t != null && t != root; t = t.parent)
            {
                path = t.name + "/" + path;
            }

            return path;
        }

        private static string Fmt(Rect rect)
        {
            return $"({rect.width:0.#}x{rect.height:0.#} yMin={rect.yMin:0.#} yMax={rect.yMax:0.#})";
        }
    }
}
