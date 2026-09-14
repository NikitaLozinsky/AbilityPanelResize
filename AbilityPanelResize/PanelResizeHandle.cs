using Kingmaker.Blueprints.Root;
using Kingmaker.UI;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AbilityPanelResize
{
    public class PanelResizeHandle : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform Target;

        // Мерить линейкой, которую сам же и двигаешь, — способ узнать не размер, а собственную нервозность.
        public RectTransform Reference;

        public ResizeElementAdapter Adapter;

        public float XSign;
        public float YSign;

        public CursorRoot.CursorType CursorType;

        public Vector2 BaseOffsetMin;
        public Vector2 BaseOffsetMax;
        public Vector2 ThicknessDirMin;
        public Vector2 ThicknessDirMax;

        private RectTransform m_Rect;
        private Vector2 m_OriginalSize;
        private Vector2 m_OriginalLocalPointerPosition;
        private bool m_IsDrag;

        private RectTransform Rect => m_Rect != null ? m_Rect : (m_Rect = GetComponent<RectTransform>());

        public void ApplyThickness(float thickness)
        {
            Rect.offsetMin = BaseOffsetMin + ThicknessDirMin * thickness;
            Rect.offsetMax = BaseOffsetMax + ThicknessDirMax * thickness;
        }

        public void OnPointerDown(PointerEventData data)
        {
            m_OriginalSize = Target.sizeDelta;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Reference, data.position, data.pressEventCamera, out m_OriginalLocalPointerPosition);

            Adapter?.BeginResize();
        }

        public void OnDrag(PointerEventData data)
        {
            m_IsDrag = true;
            ShowCursor();

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Reference, data.position, data.pressEventCamera, out Vector2 localPoint);

            Vector2 delta = localPoint - m_OriginalLocalPointerPosition;
            Vector2 size = ResizeLimits.Clamp(new Vector2(
                m_OriginalSize.x + XSign * delta.x,
                m_OriginalSize.y + YSign * delta.y));

            if (Adapter != null)
            {
                Adapter.SetSizeDelta(size);
            }
            else
            {
                Target.sizeDelta = size;
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
                CursorController.IsResizeCursor = true;
                Texture2D texture = BlueprintRoot.Instance.Cursors.GetCursorTexture(CursorType);
                UnityEngine.Cursor.SetCursor(texture, new Vector2(32f, 32f), CursorMode.Auto);
            }
        }

        private void HideCursor()
        {
            if (CursorController.IsResizeCursor)
            {
                CursorController.IsResizeCursor = false;
                UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            }
        }
    }
}
