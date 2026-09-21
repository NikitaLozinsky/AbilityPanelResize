using UnityEngine;

namespace AbilityPanelResize
{
    /// <summary>
    /// Держит верхнюю кромку области прокрутки (и скроллбара) ровно под
    /// заголовком окна.
    ///
    /// Зачем это вообще нужно: одного замера в момент <c>Initialize</c> может не
    /// хватить. Текст заголовка игра проставляет прямо перед нашим Postfix, а
    /// раскладка Canvas пересчитается только на следующем проходе — замер
    /// рискует попасть в ещё не посчитанный прямоугольник.
    ///
    /// Меряем не высоту заголовка, а его нижнюю кромку в координатах самого
    /// окна: так замер не зависит ни от вложенности заголовка внутри
    /// <c>Background</c>, ни от того, растянут он якорями или задан фиксированной
    /// высотой.
    ///
    /// Раньше проверка висела в <c>LateUpdate</c> постоянно. Высота шапки —
    /// величина неизменная (на практике трекер не сработал ни разу), поэтому
    /// сейчас он проверяет себя только несколько кадров после постройки и после
    /// каждого разворачивания панели, а потом замолкает совсем.
    /// </summary>
    public class HeaderInsetTracker : MonoBehaviour
    {
        private const float Epsilon = 0.5f;

        /// Сколько кадров перепроверять после каждого повода. Одного кадра мало:
        /// раскладка может устаканиваться не за один проход.
        private const int VerifyFrames = 5;

        public RectTransform Root;
        public RectTransform Header;
        public RectTransform Viewport;
        public RectTransform ScrollbarHolder;

        /// Правые отступы наших прямоугольников: их считает скроллбар, трекер
        /// только сохраняет их при перезаписи offsetMax.
        public float ViewportRightOffset;
        public float ScrollbarRightOffset;

        private readonly Vector3[] m_Corners = new Vector3[4];
        private float m_AppliedInset = float.NaN;
        private int m_FramesLeft = VerifyFrames;
        private bool m_CorrectionLogged;

        public float AppliedInset => m_AppliedInset;

        public void Apply(float inset)
        {
            m_AppliedInset = inset;

            if (Viewport != null)
            {
                Viewport.offsetMax = new Vector2(ViewportRightOffset, -inset);
            }

            if (ScrollbarHolder != null)
            {
                ScrollbarHolder.offsetMax = new Vector2(ScrollbarRightOffset, -inset);
            }
        }

        /// Повод снова присмотреться к шапке: панель развернули.
        public void Remeasure()
        {
            m_FramesLeft = VerifyFrames;
        }

        private void OnEnable()
        {
            Remeasure();
        }

        private void LateUpdate()
        {
            if (m_FramesLeft <= 0)
            {
                return;
            }

            m_FramesLeft--;

            if (Root == null || Header == null || Viewport == null)
            {
                return;
            }

            if (!TryMeasureInset(out float inset))
            {
                return;
            }

            if (!float.IsNaN(m_AppliedInset) && Mathf.Abs(inset - m_AppliedInset) < Epsilon)
            {
                return;
            }

            float previous = m_AppliedInset;
            Apply(inset);

            if (!m_CorrectionLogged)
            {
                m_CorrectionLogged = true;
                Main.Logger.Log(Localization.Get("AbilityPanelResize.Log.HeaderInsetFixed",
                    previous.ToString("0.#"), inset.ToString("0.#")));
            }
        }

        private bool TryMeasureInset(out float inset)
        {
            inset = 0f;

            // Угол 0 — левый нижний.
            Header.GetWorldCorners(m_Corners);
            float bottomY = Root.InverseTransformPoint(m_Corners[0]).y;
            float value = Root.rect.yMax - bottomY;

            if (float.IsNaN(value) || value <= 0f || value >= Root.rect.height)
            {
                return false;
            }

            inset = value;
            return true;
        }
    }
}
