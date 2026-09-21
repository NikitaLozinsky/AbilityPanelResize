using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Дамп геометрии и клиппирования панели в лог. Срабатывает сам, когда
    /// контент уезжает под шапку (то есть ровно тогда, когда баг и виден), и
    /// принудительно по Ctrl+Alt+D.
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

        private readonly Vector3[] m_Corners = new Vector3[4];
        private bool m_AuditDone;

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

            if (!Input.GetKeyDown(KeyCode.D))
            {
                return;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (!ctrl || !alt)
            {
                return;
            }

            Main.Logger.Log(BuildReport());
            Main.Logger.Log(BuildClippingAudit());
            Main.Logger.Log(BuildOverflowAudit());
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
