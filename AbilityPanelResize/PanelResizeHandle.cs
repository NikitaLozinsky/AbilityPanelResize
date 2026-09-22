using Kingmaker.Blueprints.Root;
using Kingmaker.UI;
using Kingmaker.UI.AbilityTarget;
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

        /// Ниже этой альфы пиксель считается пустым: у курсоров бывает еле
        /// заметная тень по краям, и она не должна растягивать границы рисунка.
        private const byte AlphaThreshold = 16;

        private RectTransform m_Rect;
        private Vector2? m_HotSpot;
        private Vector2 m_OriginalSize;
        private Vector2 m_OriginalLocalPointerPosition;
        private bool m_IsDrag;
        private bool m_PointerInside;

        /// Дошло ли до хендла событие входа курсора. Нужно диагностике: она
        /// сравнивает это с геометрией и ловит случай «мышь на грани, а событие
        /// не пришло» — то есть чужой объект перехватил рейкаст.
        public bool PointerInside => m_PointerInside;

        private RectTransform Rect => m_Rect != null ? m_Rect : (m_Rect = GetComponent<RectTransform>());

        public void ApplyThickness(float thickness)
        {
            Rect.offsetMin = BaseOffsetMin + ThicknessDirMin * thickness;
            Rect.offsetMax = BaseOffsetMax + ThicknessDirMax * thickness;
        }

        public void OnPointerDown(PointerEventData data)
        {
            Diagnose("нажатие на грани");
            m_OriginalSize = Target.sizeDelta;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Reference, data.position, data.pressEventCamera, out m_OriginalLocalPointerPosition);

            Adapter?.BeginResize();
        }

        public void OnDrag(PointerEventData data)
        {
            // Курсор достаточно выставить один раз за перетаскивание: пока
            // взведён IsResizeCursor, игра его всё равно не перерисовывает.
            if (!m_IsDrag)
            {
                m_IsDrag = true;
                Diagnose("начало перетаскивания");
                ShowCursor();
            }

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
            Diagnose(m_IsDrag ? $"конец перетаскивания, размер {Target.sizeDelta}" : "отпускание без перетаскивания");

            if (m_IsDrag)
            {
                m_IsDrag = false;
                HideCursor();
            }

            Adapter?.EndResize();
        }

        public void OnPointerEnter(PointerEventData data)
        {
            m_PointerInside = true;
            Diagnose("курсор вошёл в грань");
            ShowCursor();
        }

        public void OnPointerExit(PointerEventData data)
        {
            m_PointerInside = false;
            Diagnose("курсор вышел из грани");

            if (!m_IsDrag)
            {
                HideCursor();
            }
        }

        /// Под тумблером диагностики в настройках: события указателя — самый
        /// короткий ответ на вопрос «мышь вообще доходит до грани или нет».
        private void Diagnose(string message)
        {
            if (Main.Settings != null && Main.Settings.Diagnostics)
            {
                Main.Logger.Log($"[{name}] {message}");
            }
        }

        private void OnDisable()
        {
            // Выключенный объект события выхода уже не получит, а флаг иначе
            // залипнет во включённом состоянии до следующего входа.
            m_PointerInside = false;
        }

        private void ShowCursor()
        {
            // Флаг общий на всю игру: его взводит и родной ResizePanel боевого
            // лога. Если он завис от чужого окна, наш курсор молча не
            // выставится — под диагностикой говорим об этом вслух.
            if (CursorController.IsResizeCursor)
            {
                Diagnose("курсор ресайза НЕ выставлен: IsResizeCursor уже взведён");
                return;
            }

            CursorController.IsResizeCursor = true;
            Texture2D texture = BlueprintRoot.Instance.Cursors.GetCursorTexture(CursorType);
            ApplyCursor(texture);
            // Cursor.visible == false означает, что игра спрятала системный
            // курсор и рисует свой: PCCursor.SetActive делает ровно это. Тогда
            // наша точка крепления ни на что не влияет — он цепляется за мышь
            // левым верхним углом префаба.
            Diagnose(texture != null
                ? $"курсор ресайза выставлен, текстура {texture.width}x{texture.height}, "
                  + $"точка {HotSpotOf(texture)}, рисует {(UnityEngine.Cursor.visible ? "система" : "игра (PCCursor)")}"
                : "курсор ресайза выставлен, но текстуры нет");
        }

        /// <summary>
        /// Активная точка курсора — та, которой он «показывает».
        ///
        /// Игра для своих курсоров держит её в левом верхнем углу: её курсоры —
        /// стрелки, растущие вправо-вниз. Наши — двусторонние стрелки ресайза,
        /// у них показывает середина.
        ///
        /// Геометрический центр текстуры на эту роль не годится: рисунок внутри
        /// текстуры 64×64 лежит не по центру, и стрелка вставала правее и ниже
        /// мыши — на глаз это выглядело так, будто смещена сама зона захвата.
        /// Поэтому середину ищем по самому рисунку — по границам непрозрачных
        /// пикселей. Текстуры курсоров читаемы по определению: <c>SetCursor</c>
        /// иначе бы их не принял.
        ///
        /// Считается один раз на хендл: тип курсора у него не меняется.
        /// </summary>
        private Vector2 HotSpotOf(Texture2D texture)
        {
            if (texture == null)
            {
                return Vector2.zero;
            }

            if (m_HotSpot == null)
            {
                m_HotSpot = MeasureHotSpot(texture);
            }

            return m_HotSpot.Value;
        }

        private Vector2 MeasureHotSpot(Texture2D texture)
        {
            var fallback = new Vector2(texture.width / 2f, texture.height / 2f);

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch (System.Exception exception)
            {
                Diagnose("текстуру курсора не прочитать, берём центр: " + exception.Message);
                return fallback;
            }

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;

            for (int y = 0; y < texture.height; y++)
            {
                int row = y * texture.width;
                for (int x = 0; x < texture.width; x++)
                {
                    if (pixels[row + x].a <= AlphaThreshold)
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            if (minX > maxX)
            {
                Diagnose("текстура курсора пустая, берём центр");
                return fallback;
            }

            // GetPixels32 идёт снизу вверх, а точка курсора отсчитывается от
            // левого ВЕРХНЕГО угла — ось Y приходится развернуть.
            float centerX = (minX + maxX + 1) * 0.5f;
            float centerY = texture.height - (minY + maxY + 1) * 0.5f;

            Diagnose($"рисунок курсора занимает x {minX}..{maxX}, y {minY}..{maxY} (снизу)");
            return new Vector2(centerX, centerY);
        }

        /// <summary>
        /// Рисунок курсора живёт в двух местах сразу, и менять надо оба.
        ///
        /// Обычно курсор системный, и его хватает. Но в пошаговом бою (а ещё при
        /// прицеливании и в тактическом бою) игра включает <c>PCCursor</c> —
        /// собственный курсор, нарисованный элементом интерфейса, — и первым же
        /// делом гасит системный: <c>PCCursor.SetActive</c> выставляет
        /// <c>Cursor.visible = !active</c>. Отсюда и был симптом «вне боя
        /// стрелка есть, в пошаговом нет»: мы перекрашивали курсор, которого на
        /// экране в этот момент не было.
        ///
        /// Трогаем ровно подложку курсора (<c>PCCursor.SetCursor</c>) и ничего
        /// больше. Через <c>CursorController</c> идти нельзя: его
        /// <c>SetCustomCursor</c> гасит иконку способности, а <c>ClearCursor</c>
        /// сбрасывает режим прицеливания — этим уже обжигались.
        /// </summary>
        private void ApplyCursor(Texture2D texture)
        {
            UnityEngine.Cursor.SetCursor(texture, HotSpotOf(texture), CursorMode.Auto);

            PCCursor pcCursor = PCCursor.Instance;
            if (pcCursor != null && texture != null)
            {
                pcCursor.SetCursor(texture, HotSpotOf(texture));
            }
        }

        private void HideCursor()
        {
            if (!CursorController.IsResizeCursor)
            {
                return;
            }

            CursorController.IsResizeCursor = false;
            UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            // Системный курсор снимается в ноль, а вот нарисованный игрой сам в
            // исходное не вернётся: она перерисовывает его по событиям наведения,
            // а не каждый кадр. Возвращаем обычную стрелку сами — дальше первое
            // же движение мыши по полю поставит правильный курсор.
            PCCursor pcCursor = PCCursor.Instance;
            if (pcCursor != null)
            {
                Texture2D defaultTexture =
                    BlueprintRoot.Instance.Cursors.GetCursorTexture(CursorRoot.CursorType.DefaultCursor);
                if (defaultTexture != null)
                {
                    // Обычный курсор игры показывает левым верхним углом, а не
                    // серединой — возвращаем его с той же точкой, что и она.
                    pcCursor.SetCursor(defaultTexture, Vector2.zero);
                }
            }

            Diagnose("курсор ресайза снят");
        }
    }
}
