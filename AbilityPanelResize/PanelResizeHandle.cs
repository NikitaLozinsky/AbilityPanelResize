using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AbilityPanelResize
{
    // Единый хендл ручного ресайза - заменяет собой и игровой
    // Kingmaker.UI.Common.ResizePanel (использовался для Right/Left/Top), и
    // более раннюю версию TopRightResizeHandle (использовался для угла).
    // Раньше это были два разных механизма с разными наборами багов со
    // знаком по разным Pivot'ам игрового компонента - теперь один компонент
    // с явно задаваемым знаком по каждой оси, без скрытых инверсий.
    //
    // ГЛАВНОЕ ОТЛИЧИЕ от обоих прежних вариантов: положение мыши меряется
    // RectTransformUtility.ScreenPointToLocalPointInRectangle ОТНОСИТЕЛЬНО
    // СТАБИЛЬНОГО ПРЕДКА (Reference - на практике Groups, родитель root),
    // А НЕ относительно самого ресайзящегося окна (Target). И игровой
    // ResizePanel, и старый TopRightResizeHandle мерили относительно
    // Target - а мы же сами двигаем Target (anchoredPosition) синхронно с
    // изменением размера (компенсация центра по X и прижатие к низу по Y
    // в ResizeElementAdapter) - получалась самоссылающаяся обратная связь:
    // каждый следующий кадр драга мерил мышь уже в системе координат,
    // которую сдвинули мы сами на предыдущем кадре. По Y коэффициент этой
    // связи был ровно 1.0 (весь прирост размера тут же становился сдвигом
    // позиции) - это давало не просто отставание от курсора, а видимую
    // тряску иконок (посчитанный размер "дребезжал" от кадра к кадру).
    // Groups в нашем ресайзе не участвует и не двигается - измерение
    // относительно него полностью разрывает эту обратную связь, на любой
    // оси сразу.
    public class PanelResizeHandle : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform Target;
        public RectTransform Reference;
        public ResizeElementAdapter Adapter;
        public Vector2 MinSize = new Vector2(200f, 150f);
        public Vector2 MaxSize = new Vector2(900f, 800f);

        // Знак влияния перетаскивания на каждую ось: 0 - эту грань хендл не
        // трогает, +1/-1 - трогает с соответствующим знаком. Значения для
        // каждого хендла перенесены как есть из уже проверенной математики
        // ResizePanel.GetOffset() (Right: x=+1, Left: x=-1, Top: y=+1) и
        // TopRightResizeHandle (x=+1, y=+1) - см. AbilityGroupResizePatch.
        public float XSign;
        public float YSign;

        public CursorRoot.CursorType CursorType;

        private Vector2 m_OriginalSize;
        private Vector2 m_OriginalLocalPointerPosition;
        private bool m_IsDrag;

        public void OnPointerDown(PointerEventData data)
        {
            m_OriginalSize = Target.sizeDelta;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Reference, data.position, data.pressEventCamera, out m_OriginalLocalPointerPosition);

            // Раньше за это отвечал отдельный компонент-уведомитель
            // (ResizeDragNotifier), нужный, чтобы поймать начало/конец
            // драга независимо от того, что именно двигает размер (игровой
            // ResizePanel сам этого не знал). Теперь драг и Begin/EndResize
            // обрабатывает один и тот же компонент - отдельный уведомитель
            // не нужен.
            Adapter?.BeginResize();
        }

        public void OnDrag(PointerEventData data)
        {
            m_IsDrag = true;
            ShowCursor();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Reference, data.position, data.pressEventCamera, out Vector2 localPoint);

            Vector2 vector = localPoint - m_OriginalLocalPointerPosition;
            float newWidth = Mathf.Clamp(m_OriginalSize.x + XSign * vector.x, MinSize.x, MaxSize.x);
            float newHeight = Mathf.Clamp(m_OriginalSize.y + YSign * vector.y, MinSize.y, MaxSize.y);

            if (Adapter != null)
            {
                Adapter.SetSizeDelta(new Vector2(newWidth, newHeight));
            }
            else
            {
                Target.sizeDelta = new Vector2(newWidth, newHeight);
            }
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (m_IsDrag)
            {
                m_IsDrag = false;
                HideCursor();
            }

            Adapter?.EndResize();
        }

        public void OnPointerEnter(PointerEventData data)
        {
            ShowCursor();
        }

        public void OnPointerExit(PointerEventData data)
        {
            if (!m_IsDrag)
            {
                HideCursor();
            }
        }

        private void ShowCursor()
        {
            if (!CursorController.IsResizeCursor)
            {
                Game.Instance.CursorController.SetCustomCursor(CursorType, new Vector2(32f, 32f));
                CursorController.IsResizeCursor = true;
            }
        }

        private void HideCursor()
        {
            if (CursorController.IsResizeCursor)
            {
                CursorController.IsResizeCursor = false;
                Game.Instance.CursorController.ClearCursor();
                Game.Instance.CursorController.SetCustomCursor(CursorRoot.CursorType.None, Vector2.zero);
            }
        }
    }
}
