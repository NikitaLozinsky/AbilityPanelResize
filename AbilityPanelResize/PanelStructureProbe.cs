using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Kingmaker.UI.Common;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    /// <summary>
    /// Раскладка окон экшн-бара (заклинания, способности, предметы) так, как её
    /// собрал префаб: из декомпилированного кода она не видна. Каждый узел
    /// помечается полями вьюхи, которые на него ссылаются.
    ///
    /// Зовётся из <c>Main.OnUpdate</c> по той же причине, что и
    /// <see cref="CursorProbe"/>: свёрнутая или пустая панель выключена, и
    /// компонент на ней <c>Update</c> не получает. Поиск через
    /// <c>Resources.FindObjectsOfTypeAll</c>, потому что он видит и выключенные
    /// объекты; для разового дампа его цена неважна.
    /// </summary>
    internal static class PanelStructureProbe
    {
        private const int MaxDepth = 10;

        /// Слоты однотипны: подробно разбираем первый, остальные сводим в итог.
        private const int SlotDetailDepth = 2;

        private static readonly FieldInfo s_SwitchButton =
            AccessTools.Field(typeof(ActionBarGroupPCView), "m_SwitchButton");

        private static readonly FieldInfo s_TogglableChildren =
            AccessTools.Field(typeof(ActionBarGroupPCView), "m_TogglableChildren");

        private static readonly FieldInfo s_GroupType =
            AccessTools.Field(typeof(ActionBarGroupPCView), "m_GroupType");

        private static readonly FieldInfo s_LevelButtons =
            AccessTools.Field(typeof(ActionBarSpellGroupPCView), "m_LevelButtons");

        private static readonly FieldInfo s_LevelButton =
            AccessTools.Field(typeof(ActionBarSpellGroupLevelPCView), "m_Button");

        private static readonly FieldInfo s_LevelBackground =
            AccessTools.Field(typeof(ActionBarSpellGroupLevelPCView), "m_Background");

        private static readonly FieldInfo s_LevelBaseX =
            AccessTools.Field(typeof(ActionBarSpellGroupLevelPCView), "m_BaseXPosition");

        private static readonly FieldInfo s_LevelHoverX =
            AccessTools.Field(typeof(ActionBarSpellGroupLevelPCView), "m_HoverXPosition");

        private static readonly Vector3[] s_Corners = new Vector3[4];

        public static string Build()
        {
            var report = new StringBuilder();
            report.AppendLine("=== AbilityPanelResize: устройство окон экшн-бара ===");
            report.AppendLine($"экран={Screen.width}x{Screen.height} режимОкна={Screen.fullScreenMode}");

            try
            {
                var views = new List<ActionBarGroupPCView>();
                foreach (ActionBarGroupPCView view in Resources.FindObjectsOfTypeAll<ActionBarGroupPCView>())
                {
                    if (view != null && view.gameObject.scene.IsValid())
                    {
                        views.Add(view);
                    }
                }

                if (views.Count == 0)
                {
                    report.AppendLine("окон не найдено: экшн-бар ещё не построен");
                    return report.ToString();
                }

                views.Sort((a, b) => GroupOrder(a).CompareTo(GroupOrder(b)));
                foreach (ActionBarGroupPCView view in views)
                {
                    AppendView(report, view);
                }
            }
            catch (System.Exception exception)
            {
                report.AppendLine("проба оборвалась: " + exception);
            }

            return report.ToString();
        }

        private static void AppendView(StringBuilder report, ActionBarGroupPCView view)
        {
            RectTransform root = view.transform as RectTransform;
            ResizeElementAdapter adapter = view.GetComponent<ResizeElementAdapter>();

            report.AppendLine();
            report.AppendLine($"##### {view.GetType().Name} тип={GroupTypeName(view)} "
                              + $"активно={view.gameObject.activeInHierarchy} "
                              + $"развёрнуто={ActionBarGroupAccess.IsVisible(view)} "
                              + $"поправкаНиза={ActionBarGroupAccess.GetVisiblePositionDelta(view):0.#} "
                              + $"построеноМодом={adapter != null && adapter.IsBuilt}");
            report.AppendLine("путь: " + FullPath(view.transform));

            AppendSiblings(report, view.transform);
            AppendCanvas(report, view.transform);

            if (root == null)
            {
                report.AppendLine("корень окна — не RectTransform, дальше смотреть нечего");
                return;
            }

            report.AppendLine($"окно в своих координатах: x[{root.rect.xMin:0.#}..{root.rect.xMax:0.#}] "
                              + $"y[{root.rect.yMin:0.#}..{root.rect.yMax:0.#}]");

            Dictionary<Transform, List<string>> marks = CollectMarks(view);
            AppendLevelButtonsSummary(report, view);

            report.AppendLine("дерево (вОкне — прямоугольник в координатах корня окна):");
            AppendNode(report, root, root, 0, marks, -1);
            AppendMarksOutside(report, root, marks);
        }

        private static Dictionary<Transform, List<string>> CollectMarks(ActionBarGroupPCView view)
        {
            var marks = new Dictionary<Transform, List<string>>();

            Mark(marks, ActionBarGroupAccess.GetSlotContainer(view), "m_SlotContainer");
            Mark(marks, TransformOf(s_SwitchButton?.GetValue(view)), "m_SwitchButton");
            Mark(marks, TransformOf(ActionBarGroupAccess.GetGroupNameLabel(view)), "m_GroupNameLabel");

            if (s_TogglableChildren?.GetValue(view) is IList togglable)
            {
                for (int i = 0; i < togglable.Count; i++)
                {
                    Mark(marks, TransformOf(togglable[i]), $"m_TogglableChildren[{i}]");
                }
            }

            if (view is ActionBarSpellGroupPCView && s_LevelButtons?.GetValue(view) is IList levels)
            {
                for (int i = 0; i < levels.Count; i++)
                {
                    object level = levels[i];
                    Mark(marks, TransformOf(level), $"m_LevelButtons[{i}]");
                    if (level != null)
                    {
                        Mark(marks, TransformOf(s_LevelButton?.GetValue(level)), $"m_LevelButtons[{i}].m_Button");
                        Mark(marks, TransformOf(s_LevelBackground?.GetValue(level)), $"m_LevelButtons[{i}].m_Background");
                    }
                }
            }

            return marks;
        }

        private static void AppendLevelButtonsSummary(StringBuilder report, ActionBarGroupPCView view)
        {
            if (!(view is ActionBarSpellGroupPCView) || !(s_LevelButtons?.GetValue(view) is IList levels))
            {
                return;
            }

            object first = levels.Count > 0 ? levels[0] : null;
            report.AppendLine($"вкладок уровней: {levels.Count}, "
                              + $"m_BaseXPosition={s_LevelBaseX?.GetValue(first) ?? "?"} "
                              + $"m_HoverXPosition={s_LevelHoverX?.GetValue(first) ?? "?"} (у первой)");
        }

        private static void AppendNode(
            StringBuilder report,
            RectTransform root,
            Transform node,
            int depth,
            Dictionary<Transform, List<string>> marks,
            int detailBudget)
        {
            string indent = new string(' ', depth * 2);

            report.Append(indent).Append(node.name);
            if (!node.gameObject.activeSelf)
            {
                report.Append(" [выкл]");
            }

            if (marks.TryGetValue(node, out List<string> labels))
            {
                report.Append("  <<").Append(string.Join(", ", labels)).Append(">>");
            }

            report.AppendLine();

            if (node is RectTransform rect)
            {
                report.Append(indent).Append("  · ").AppendLine(RectLine(rect, root));
            }

            string components = ComponentsLine(node);
            if (components.Length > 0)
            {
                report.Append(indent).Append("  · ").AppendLine(components);
            }

            if (node.childCount == 0)
            {
                return;
            }

            if (depth >= MaxDepth || detailBudget == 0)
            {
                report.Append(indent).AppendLine($"  … детей: {node.childCount}, глубже не смотрим");
                return;
            }

            int childBudget = detailBudget < 0 ? -1 : detailBudget - 1;
            int slots = 0;
            int activeSlots = 0;
            Rect slotBounds = default;
            RectTransform lastSlot = null;

            for (int i = 0; i < node.childCount; i++)
            {
                Transform child = node.GetChild(i);
                if (child.GetComponent<ActionBarBaseSlotPCView>() == null)
                {
                    AppendNode(report, root, child, depth + 1, marks, childBudget);
                    continue;
                }

                slots++;
                if (slots == 1)
                {
                    AppendNode(report, root, child, depth + 1, marks, SlotDetailDepth);
                    continue;
                }

                if (child.gameObject.activeSelf)
                {
                    activeSlots++;
                }

                if (child is RectTransform slotRect)
                {
                    Rect inRoot = RectInRoot(slotRect, root);
                    slotBounds = lastSlot == null ? inRoot : Union(slotBounds, inRoot);
                    lastSlot = slotRect;
                }
            }

            if (slots > 1)
            {
                report.Append(indent).AppendLine($"  … ещё слотов: {slots - 1}, активных {activeSlots}, "
                                                 + $"вместе вОкне={Fmt(slotBounds)}");
                if (lastSlot != null)
                {
                    report.Append(indent).AppendLine($"  … последний {lastSlot.name}: {RectLine(lastSlot, root)}");
                }
            }
        }

        private static void AppendMarksOutside(StringBuilder report, RectTransform root, Dictionary<Transform, List<string>> marks)
        {
            bool headerWritten = false;
            foreach (KeyValuePair<Transform, List<string>> mark in marks)
            {
                if (mark.Key == null || mark.Key.IsChildOf(root))
                {
                    continue;
                }

                if (!headerWritten)
                {
                    report.AppendLine("поля вьюхи, указывающие за пределы окна:");
                    headerWritten = true;
                }

                report.AppendLine($"  {string.Join(", ", mark.Value)} → {FullPath(mark.Key)}");
                AppendNode(report, root, mark.Key, 2, marks, SlotDetailDepth);
            }
        }

        private static void AppendSiblings(StringBuilder report, Transform target)
        {
            Transform parent = target.parent;
            if (parent == null)
            {
                return;
            }

            var siblings = new List<string>();
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                siblings.Add($"[{i}] {child.name}{(child.gameObject.activeSelf ? "" : " (выкл)")}{(child == target ? " ← это окно" : "")}");
            }

            report.AppendLine($"соседи внутри {parent.name}: {string.Join("; ", siblings)}");
        }

        private static void AppendCanvas(StringBuilder report, Transform target)
        {
            Canvas[] canvases = target.GetComponentsInParent<Canvas>(includeInactive: true);
            if (canvases == null || canvases.Length == 0)
            {
                report.AppendLine("холст: не найден");
                return;
            }

            Canvas nearest = canvases[0];
            Canvas rootCanvas = nearest.rootCanvas;
            RectTransform rootRect = rootCanvas != null ? rootCanvas.transform as RectTransform : null;
            CanvasScaler scaler = rootCanvas != null ? rootCanvas.GetComponent<CanvasScaler>() : null;

            report.AppendLine($"холст: ближайший={nearest.name} override={nearest.overrideSorting} order={nearest.sortingOrder}; "
                              + $"корневой={(rootCanvas != null ? rootCanvas.name : "?")} "
                              + $"размер={(rootRect != null ? $"{rootRect.rect.width:0.#}x{rootRect.rect.height:0.#}" : "?")} "
                              + $"scaleFactor={(rootCanvas != null ? rootCanvas.scaleFactor.ToString("0.###") : "?")} "
                              + (scaler != null
                                  ? $"scaler={scaler.uiScaleMode} ref={V(scaler.referenceResolution)} match={scaler.matchWidthOrHeight:0.##}"
                                  : "scaler=нет"));
        }

        private static string RectLine(RectTransform rect, RectTransform root)
        {
            string line = $"anchors={V(rect.anchorMin)}-{V(rect.anchorMax)} pivot={V(rect.pivot)} "
                          + $"pos={V(rect.anchoredPosition)} sizeDelta={V(rect.sizeDelta)} "
                          + $"размер={rect.rect.width:0.#}x{rect.rect.height:0.#} вОкне={Fmt(RectInRoot(rect, root))}";

            if (rect.localScale != Vector3.one)
            {
                line += $" scale=({rect.localScale.x:0.##},{rect.localScale.y:0.##})";
            }

            if (rect.localRotation != Quaternion.identity)
            {
                line += $" поворот={rect.localEulerAngles.z:0.#}";
            }

            return line;
        }

        private static string ComponentsLine(Transform node)
        {
            var parts = new List<string>();
            foreach (Component component in node.GetComponents<Component>())
            {
                if (component is Transform)
                {
                    continue;
                }

                parts.Add(Describe(component));
            }

            return string.Join(" | ", parts);
        }

        private static string Describe(Component component)
        {
            if (component == null)
            {
                return "<пропавший скрипт>";
            }

            string name = component.GetType().Name;
            if (component is Behaviour behaviour && !behaviour.enabled)
            {
                name += "(выкл)";
            }

            switch (component)
            {
                case GridLayoutGroup grid:
                    return $"{name}{{cell={V(grid.cellSize)} spacing={V(grid.spacing)} pad={Pad(grid.padding)} "
                           + $"{grid.constraint}:{grid.constraintCount} align={grid.childAlignment} "
                           + $"start={grid.startCorner}/{grid.startAxis}}}";

                case HorizontalOrVerticalLayoutGroup group:
                    return $"{name}{{spacing={group.spacing:0.#} pad={Pad(group.padding)} align={group.childAlignment} "
                           + $"control={group.childControlWidth}/{group.childControlHeight} "
                           + $"expand={group.childForceExpandWidth}/{group.childForceExpandHeight}}}";

                case ContentSizeFitter fitter:
                    return $"{name}{{h={fitter.horizontalFit} v={fitter.verticalFit}}}";

                case ContentSizeFitterExtended extended:
                    Traverse traverse = Traverse.Create(extended);
                    return $"{name}{{h={traverse.Field("m_HorizontalFit").GetValue()} "
                           + $"v={traverse.Field("m_VerticalFit").GetValue()}}}";

                case LayoutElement element:
                    return $"{name}{{ignore={element.ignoreLayout} "
                           + $"min={element.minWidth:0.#}x{element.minHeight:0.#} "
                           + $"pref={element.preferredWidth:0.#}x{element.preferredHeight:0.#} "
                           + $"flex={element.flexibleWidth:0.#}x{element.flexibleHeight:0.#}}}";

                case Image image:
                    return $"{name}{{sprite={(image.sprite != null ? image.sprite.name : "-")} type={image.type} "
                           + $"alpha={image.color.a:0.##} raycast={image.raycastTarget} maskable={image.maskable}}}";

                case MaskableGraphic graphic:
                    return $"{name}{{{TextOf(graphic)}alpha={graphic.color.a:0.##} "
                           + $"raycast={graphic.raycastTarget} maskable={graphic.maskable}}}";

                case CanvasGroup canvasGroup:
                    return $"{name}{{alpha={canvasGroup.alpha:0.##} raycasts={canvasGroup.blocksRaycasts} "
                           + $"interactable={canvasGroup.interactable} ignoreParent={canvasGroup.ignoreParentGroups}}}";

                case Canvas canvas:
                    return $"{name}{{override={canvas.overrideSorting} order={canvas.sortingOrder}}}";

                default:
                    return name;
            }
        }

        /// Текст надписи — по нему видно, какая вкладка какому уровню
        /// соответствует. TextMeshPro читаем рефлексией, чтобы не тащить его
        /// сборку в проект ради отладочного дампа.
        private static string TextOf(Graphic graphic)
        {
            PropertyInfo property = graphic.GetType().GetProperty("text", typeof(string));
            if (property == null)
            {
                return "";
            }

            string text = property.GetValue(graphic) as string ?? "";
            text = text.Replace("\n", " ");
            if (text.Length > 30)
            {
                text = text.Substring(0, 30) + "…";
            }

            return $"text='{text}' ";
        }

        private static Rect RectInRoot(RectTransform rect, RectTransform root)
        {
            rect.GetWorldCorners(s_Corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 4; i++)
            {
                Vector2 local = root.InverseTransformPoint(s_Corners[i]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect Union(Rect a, Rect b)
        {
            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }

        private static void Mark(Dictionary<Transform, List<string>> marks, Transform target, string label)
        {
            if (target == null)
            {
                return;
            }

            if (!marks.TryGetValue(target, out List<string> labels))
            {
                labels = new List<string>();
                marks[target] = labels;
            }

            labels.Add(label);
        }

        private static Transform TransformOf(object value)
        {
            return value is Component component && component != null ? component.transform : null;
        }

        private static int GroupOrder(ActionBarGroupPCView view)
        {
            return s_GroupType?.GetValue(view) is ActionBarGroupType type ? (int)type : int.MaxValue;
        }

        private static string GroupTypeName(ActionBarGroupPCView view)
        {
            return s_GroupType?.GetValue(view)?.ToString() ?? "?";
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

        private static string Fmt(Rect rect)
        {
            return $"x[{rect.xMin:0.#}..{rect.xMax:0.#}] y[{rect.yMin:0.#}..{rect.yMax:0.#}]";
        }

        private static string Pad(RectOffset padding)
        {
            return $"{padding.left},{padding.right},{padding.top},{padding.bottom}";
        }

        private static string V(Vector2 value)
        {
            return $"({value.x:0.##},{value.y:0.##})";
        }
    }
}
