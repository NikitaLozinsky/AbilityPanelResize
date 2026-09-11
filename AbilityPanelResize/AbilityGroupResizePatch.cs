using HarmonyLib;
using Kingmaker.Blueprints.Root;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    // ЧАСТЬ 2: хендлы ручного растягивания панели способностей мышью.
    //
    // Панель имеет pivot (0,1) - верхний левый угол зафиксирован намертво,
    // физически двигаются только правая и нижняя грани. Но поверх этого
    // ResizeElementAdapter.SetSizeDelta ещё и держит НИЖНЮЮ грань прижатой
    // к панели быстрого доступа, а по X - центр окна на месте (симметричный
    // рост в обе стороны). То есть визуально на экране двигаются: правая и
    // левая грани (симметрично) при изменении ширины, и только ВЕРХНЯЯ
    // грань при изменении высоты.
    //
    // Все хендлы расширены НАРУЖУ от границы окна (не внутрь) - чтобы
    // расширенная зона захвата не отъедала место у иконок способностей.
    //
    // Верхняя грань разбита на ДВА сегмента (лево/право) с зазором по
    // центру - там сидит кнопка сворачивания панели (▼/▲).
    //
    // Все хендлы используют один и тот же компонент PanelResizeHandle (не
    // игровой Kingmaker.UI.Common.ResizePanel - см. подробный комментарий
    // в PanelResizeHandle.cs о том, почему измерение мыши относительно
    // самого окна давало тряску/отставание по высоте). Begin/EndResize
    // хендл вызывает сам (через тот же PointerDown/PointerUp, которым и
    // так обрабатывает драг) - отдельный уведомитель (как раньше
    // ResizeDragNotifier) больше не нужен.
    //
    // Работает независимо от патча со ScrollRect (AbilityGroupScrollPatch) -
    // оба это отдельные Postfix на одном и том же Initialize, порядок между
    // ними не важен: каждый хендл принудительно ставится последним в списке
    // дочерних объектов (SetAsLastSibling), поэтому всегда оказывается
    // "выше" сетки иконок для мыши, независимо от того, в каком порядке
    // отработали патчи.
    [HarmonyPatch(typeof(ActionBarGroupPCView), nameof(ActionBarGroupPCView.Initialize))]
    public static class ActionBarGroupPCView_Initialize_AddResize_Patch
    {
        // Ограничения размера - общие с AbilityGroupScrollPatch (там
        // используются для подрезки значения, загруженного из Settings.xml).
        private static readonly Vector2 MinSize = ResizeLimits.MinSize;
        private static readonly Vector2 MaxSize = ResizeLimits.MaxSize;

        // Толщина зоны захвата грани мышью. Увеличена по сравнению с
        // прошлой версией (было 10, стало 16) - была слишком узкой,
        // тяжело было точно попасть курсором.
        private const float HandleThickness = 16f;

        // Ширина зазора по центру верхней грани под кнопку сворачивания.
        private const float TopHandleButtonGap = 60f;

        [HarmonyPostfix]
        public static void Postfix(ActionBarGroupPCView __instance, ActionBarGroupType type)
        {
            if (type != ActionBarGroupType.Ability)
            {
                return;
            }

            RectTransform root = __instance.transform as RectTransform;
            if (root == null || root.Find("AbilityPanelResize_ResizeRight") != null)
            {
                // Уже пропатчено (повторный Initialize) - выходим.
                return;
            }

            // AbilityGroupView, SpellGroupView и ItemGroupView - соседи, все
            // дети общего родителя Groups. Ставим панель способностей
            // ПЕРВЫМ ребёнком Groups - тогда оба соседа гарантированно
            // рисуются поверх неё (см. подробный комментарий в истории
            // патча). Отдельно от этого AbilityGroupScrollPatch отключает
            // raycastTarget у декоративного фона панели - это решает более
            // широкий класс подобных перекрытий (не только соседей внутри
            // Groups, но и вообще любых элементов интерфейса рядом).
            root.SetAsFirstSibling();

            // Groups - стабильный предок, участвует в иерархии, но НЕ
            // двигается в процессе нашего ресайза (в отличие от самого
            // root) - используем его как систему отсчёта для измерения
            // мыши во всех хендлах (см. PanelResizeHandle). Подстраховка:
            // если вдруг родитель не RectTransform (не должно случиться),
            // используем сам root - тогда просто вернётся старое поведение
            // с самоссылающейся обратной связью, а не упадёт с NRE.
            RectTransform stableReference = root.parent as RectTransform ?? root;

            ResizeElementAdapter adapter = root.GetComponent<ResizeElementAdapter>();
            if (adapter == null)
            {
                adapter = root.gameObject.AddComponent<ResizeElementAdapter>();
            }

            // Правая грань - тянет ширину, наружу (вправо от границы окна).
            RectTransform rightHandle = CreateHandleRect(root, "AbilityPanelResize_ResizeRight",
                anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 1f),
                offsetMin: new Vector2(0f, 0f), offsetMax: new Vector2(HandleThickness, 0f));
            AddResizeHandle(rightHandle, root, adapter, stableReference,
                xSign: 1f, ySign: 0f, CursorRoot.CursorType.ArrowHorizontalCursor);

            // Левая грань - тоже тянет ширину, наружу (влево от границы).
            RectTransform leftHandle = CreateHandleRect(root, "AbilityPanelResize_ResizeLeft",
                anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(0f, 1f),
                offsetMin: new Vector2(-HandleThickness, 0f), offsetMax: new Vector2(0f, 0f));
            AddResizeHandle(leftHandle, root, adapter, stableReference,
                xSign: -1f, ySign: 0f, CursorRoot.CursorType.ArrowHorizontalCursor);

            // Верхняя грань, левый сегмент - от левого края до зазора,
            // наружу (вверх от границы окна).
            CreateTopEdgeSegment(root, adapter, stableReference, "AbilityPanelResize_ResizeTopLeftSeg",
                anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(0.5f, 1f),
                offsetMinX: 0f, offsetMaxX: -TopHandleButtonGap / 2f);

            // Верхняя грань, правый сегмент - от зазора до правого края.
            CreateTopEdgeSegment(root, adapter, stableReference, "AbilityPanelResize_ResizeTopRightSeg",
                anchorMin: new Vector2(0.5f, 1f), anchorMax: new Vector2(1f, 1f),
                offsetMinX: TopHandleButtonGap / 2f, offsetMaxX: 0f);

            // Верхний правый угол - тянет сразу оба измерения по диагонали,
            // наружу (вверх и вправо от угла окна).
            RectTransform topRightHandle = CreateHandleRect(root, "AbilityPanelResize_ResizeTopRight",
                anchorMin: new Vector2(1f, 1f), anchorMax: new Vector2(1f, 1f),
                offsetMin: new Vector2(0f, 0f), offsetMax: new Vector2(HandleThickness, HandleThickness));
            AddResizeHandle(topRightHandle, root, adapter, stableReference,
                xSign: 1f, ySign: 1f, CursorRoot.CursorType.ArrowDiagonally01Cursor);

            Main.Logger.Log("AbilityPanelResize: хендлы ручного ресайза добавлены.");
        }

        private static void AddResizeHandle(
            RectTransform handleRect,
            RectTransform target,
            ResizeElementAdapter adapter,
            RectTransform reference,
            float xSign,
            float ySign,
            CursorRoot.CursorType cursorType)
        {
            PanelResizeHandle handle = handleRect.gameObject.AddComponent<PanelResizeHandle>();
            handle.Target = target;
            handle.Reference = reference;
            handle.Adapter = adapter;
            handle.MinSize = MinSize;
            handle.MaxSize = MaxSize;
            handle.XSign = xSign;
            handle.YSign = ySign;
            handle.CursorType = cursorType;
        }

        // Создаёт RectTransform хендла с прозрачным Image (нужен как цель для
        // рейкаста мыши) через offsetMin/offsetMax ("резиновые" отступы от
        // анкеров) - это позволяет явно задать, расширяется ли зона наружу
        // или внутрь от границы окна. Принудительно ставит хендл последним
        // среди дочерних объектов root, чтобы он гарантированно перехватывал
        // клики поверх сетки иконок способностей, независимо от порядка
        // патчей.
        private static RectTransform CreateHandleRect(
            RectTransform root,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var handleGO = new GameObject(name, typeof(RectTransform));
            RectTransform handleRect = handleGO.GetComponent<RectTransform>();
            handleRect.SetParent(root, worldPositionStays: false);
            handleRect.anchorMin = anchorMin;
            handleRect.anchorMax = anchorMax;
            handleRect.offsetMin = offsetMin;
            handleRect.offsetMax = offsetMax;

            Image image = handleGO.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            handleRect.SetAsLastSibling();

            return handleRect;
        }

        // Сегмент верхней грани - с зазором по центру под кнопку и
        // расширением наружу (вверх от границы окна).
        private static void CreateTopEdgeSegment(
            RectTransform root,
            ResizeElementAdapter adapter,
            RectTransform reference,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            float offsetMinX,
            float offsetMaxX)
        {
            RectTransform handleRect = CreateHandleRect(root, name,
                anchorMin, anchorMax,
                offsetMin: new Vector2(offsetMinX, 0f),
                offsetMax: new Vector2(offsetMaxX, HandleThickness));

            AddResizeHandle(handleRect, root, adapter, reference,
                xSign: 0f, ySign: 1f, CursorRoot.CursorType.ArrowVerticalCursor);
        }
    }
}
