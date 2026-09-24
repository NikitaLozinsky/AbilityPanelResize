using System.Collections.Generic;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers.Clicks.Handlers;
using Kingmaker.UI;
using Kingmaker.UI.AbilityTarget;
using Kingmaker.UnitLogic.Abilities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AbilityPanelResize
{
    public class PanelResizeHandle : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IDragHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>
        /// Кто из хендлов сейчас держит курсор ресайза.
        ///
        /// Сам флаг <c>CursorController.IsResizeCursor</c> общий на всю игру, и
        /// взводит его не только наш мод — тем же механизмом пользуется рамка
        /// боевого лога. Поэтому снимать флаг можно только тому, кто его
        /// поставил: иначе, проведя мышью над нашей гранью во время чужого
        /// перетаскивания, мы сбросили бы чужой курсор.
        ///
        /// Ссылка живёт не дольше самого хендла: <see cref="OnDisable"/>
        /// отпускает её и при сворачивании панели, и при её уничтожении.
        /// </summary>
        private static PanelResizeHandle s_CursorOwner;

        public static bool CursorHeld => s_CursorOwner != null;

        /// <summary>
        /// Целится ли игрок способностью прямо сейчас.
        ///
        /// Мы следим за этим по той же причине, по которой сама игра проверяет
        /// свой <c>m_CastMode</c> перед каждой сменой курсора: курсор
        /// прицеливания — не одна текстура, а подложка плюс иконка способности
        /// поверх неё, и перерисовывает его игра не каждый кадр, а по событию
        /// начала прицеливания. Подменив у него подложку, вернуть всё как было
        /// мы уже не сможем — вернуть может только сама игра.
        ///
        /// Признак берём у <c>ClickWithSelectedAbilityHandler</c>: его
        /// <c>SelectedAbility</c> взводится и гаснет ровно теми же вызовами
        /// (<c>SetAbility</c>/<c>DropAbility</c>), которые поднимают событие
        /// начала и конца прицеливания, — то есть это и есть источник истины,
        /// а не его отражение.
        /// </summary>
        private static bool AbilityTargeting => SelectedAbility() != null;

        /// Прицеливание на предыдущем кадре: реагировать надо на смену
        /// состояния, а не на само состояние.
        private static bool s_WasTargeting;

        public static void ReleaseCursor()
        {
            if (s_CursorOwner != null)
            {
                s_CursorOwner.HideCursor();
            }
        }

        /// <summary>
        /// Раз в кадр сверяет захват курсора с прицеливанием способностью.
        /// Зовётся из <see cref="ResizeElementAdapter"/>: он на панели один, а
        /// хендлов пять — пять лишних <c>Update</c> на кадр тут ни к чему.
        ///
        /// Случай, ради которого это нужно: мышь стоит на грани, курсор держим
        /// мы, и в этот момент игрок берёт способность на прицел с клавиатуры.
        /// Игра попробует нарисовать её курсор, но наш <c>IsResizeCursor</c>
        /// этот вызов подавит — и без нас она больше не попытается. Поэтому
        /// курсор мы отдаём сами, а когда прицеливание закончится — забираем
        /// обратно, если мышь всё ещё на грани.
        /// </summary>
        public static void SyncWithAbilityTargeting(IReadOnlyList<PanelResizeHandle> handles)
        {
            bool targeting = AbilityTargeting;
            if (targeting == s_WasTargeting)
            {
                return;
            }

            s_WasTargeting = targeting;

            if (targeting)
            {
                ReleaseCursor();
                return;
            }

            if (handles == null)
            {
                return;
            }

            for (int i = 0; i < handles.Count; i++)
            {
                PanelResizeHandle handle = handles[i];
                if (handle != null && handle.isActiveAndEnabled && handle.m_PointerInside)
                {
                    handle.ShowCursor();
                    return;
                }
            }
        }

        private static AbilityData SelectedAbility()
        {
            try
            {
                ClickWithSelectedAbilityHandler handler = Game.Instance != null
                    ? Game.Instance.SelectedAbilityHandler
                    : null;

                return handler != null ? handler.SelectedAbility : null;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

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

        /// Точка курсора считается по картинке, а картинка у одного и того же
        /// типа курсора не одна: игра отдаёт разные текстуры в зависимости от
        /// режима курсора и высоты экрана (64/96/128 px). Поэтому кэш помнит,
        /// для какой именно текстуры он посчитан.
        private Texture2D m_HotSpotTexture;
        private Vector2 m_HotSpot;

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

            if (Adapter != null)
            {
                Adapter.BeginResize();
            }
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
            bool dragged = m_IsDrag;
            Diagnose(dragged ? $"конец перетаскивания, размер {Target.sizeDelta}" : "отпускание без перетаскивания");

            if (dragged)
            {
                m_IsDrag = false;
                HideCursor();
            }

            if (Adapter != null)
            {
                Adapter.EndResize(dragged);
            }
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

        /// <summary>
        /// Хендл могли выключить прямо посреди перетаскивания: панель
        /// сворачивается по клику ▼ и по Esc, и в этот момент объект гаснет, а
        /// события отпускания кнопки он уже не получит.
        ///
        /// Незакрытое перетаскивание оставляло бы за собой две неприятности:
        /// взведённый курсор ресайза на всю игру и выключенный <c>ScrollRect</c> —
        /// то есть панель, которая до следующего перезахода не прокручивается.
        /// </summary>
        private void OnDisable()
        {
            m_PointerInside = false;

            if (m_IsDrag)
            {
                m_IsDrag = false;

                // Проверка через оператор Unity, а не ?.: объект мог быть уже
                // уничтожен, и тогда ссылка не null, но обращаться по ней нельзя.
                if (Adapter != null)
                {
                    Adapter.EndResize(save: true);
                }
            }

            HideCursor();
        }

        private void ShowCursor()
        {
            if (s_CursorOwner == this)
            {
                return;
            }

            // Курсор прицеливания способностью не трогаем вовсе: он не наш и
            // возвращать его пришлось бы самим. Ровно так же ведёт себя игра —
            // пока взведён её m_CastMode, она курсор не перерисовывает.
            if (AbilityTargeting)
            {
                Diagnose("курсор ресайза НЕ выставлен: игрок целится способностью");
                return;
            }

            // Флаг общий на всю игру: его взводит и родной ResizePanel боевого
            // лога. Если он занят чужим окном, наш курсор молча не выставится —
            // под диагностикой говорим об этом вслух.
            if (CursorController.IsResizeCursor)
            {
                Diagnose("курсор ресайза НЕ выставлен: IsResizeCursor уже взведён");
                return;
            }

            s_CursorOwner = this;
            CursorController.IsResizeCursor = true;

            Texture2D texture = CursorTexture(CursorType);
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

        private void HideCursor()
        {
            if (s_CursorOwner != this)
            {
                return;
            }

            s_CursorOwner = null;
            CursorController.IsResizeCursor = false;

            if (RestoreAbilityCursor())
            {
                Diagnose("курсор ресайза снят, курсор способности отрисован заново");
                return;
            }

            // Обычный курсор игры показывает левым верхним углом, а не
            // серединой — возвращаем его с точкой (0,0), как делает она сама.
            //
            // Ставим именно её текстуру, а не null: null — это системная стрелка
            // Windows. Игра свой курсор обратно не перерисует, пока мышь не
            // наведут на что-нибудь новое, а уйти с грани можно и внутрь окна,
            // где наводиться не на что.
            ApplyCursor(CursorTexture(CursorRoot.CursorType.DefaultCursor), Vector2.zero);

            Diagnose("курсор ресайза снят");
        }

        /// <summary>
        /// Возвращает курсор прицеливания, если оно идёт прямо сейчас.
        ///
        /// Сюда попадают, только когда прицеливание началось уже после захвата
        /// грани: при входе мы такой курсор не трогаем вовсе. Рисуем не сами —
        /// зовём тот же публичный метод игры, которым она рисует этот курсор
        /// при начале прицеливания, вместе с иконкой способности поверх
        /// подложки. Через <c>SetCustomCursor</c>/<c>ClearCursor</c> идти
        /// по-прежнему нельзя: они сбрасывают режим прицеливания.
        /// </summary>
        private static bool RestoreAbilityCursor()
        {
            AbilityData ability = SelectedAbility();
            if (ability == null)
            {
                return false;
            }

            try
            {
                Sprite icon = ability.MagicHackData != null
                    ? ability.GetDeliverBlueprint().Icon
                    : ability.Icon;

                Game.Instance.CursorController.SetAbilityCursor(icon, forbidden: false, outOfRange: false);
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static Texture2D CursorTexture(CursorRoot.CursorType type)
        {
            CursorRoot cursors = BlueprintRoot.Instance != null ? BlueprintRoot.Instance.Cursors : null;
            return cursors != null ? cursors.GetCursorTexture(type) : null;
        }

        /// <summary>
        /// Режим курсора берём у игры: в её настройках есть «только программный
        /// курсор», и в этом режиме она рисует курсоры иначе (и текстуры отдаёт
        /// другие — 64/96/128 px по высоте экрана). Выставить в таком режиме
        /// аппаратный курсор — значит не выставить ничего.
        /// </summary>
        private static CursorMode Mode()
        {
            try
            {
                return Game.Instance.UISettingsManager.IsOnlySoftwareMode
                    ? CursorMode.ForceSoftware
                    : CursorMode.Auto;
            }
            catch (System.Exception)
            {
                return CursorMode.Auto;
            }
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
        private void ApplyCursor(Texture2D texture, Vector2? hotSpot = null)
        {
            if (texture == null)
            {
                return;
            }

            Vector2 point = hotSpot ?? HotSpotOf(texture);
            UnityEngine.Cursor.SetCursor(texture, point, Mode());

            PCCursor pcCursor = PCCursor.Instance;
            if (pcCursor != null)
            {
                pcCursor.SetCursor(texture, point);
            }
        }

        /// <summary>
        /// Активная точка курсора — та, которой он «показывает».
        ///
        /// Игра для своих курсоров держит её в левом верхнем углу: её курсоры —
        /// стрелки, растущие вправо-вниз. Наши — двусторонние стрелки ресайза,
        /// у них показывает середина.
        ///
        /// Геометрический центр текстуры на эту роль не годится: рисунок внутри
        /// текстуры лежит не по центру, и стрелка вставала правее и ниже мыши —
        /// на глаз это выглядело так, будто смещена сама зона захвата. Поэтому
        /// середину ищем по самому рисунку — по границам непрозрачных пикселей.
        /// Текстуры курсоров читаемы по определению: <c>SetCursor</c> иначе бы
        /// их не принял.
        /// </summary>
        private Vector2 HotSpotOf(Texture2D texture)
        {
            if (texture == null)
            {
                return Vector2.zero;
            }

            if (m_HotSpotTexture != texture)
            {
                m_HotSpotTexture = texture;
                m_HotSpot = MeasureHotSpot(texture);
            }

            return m_HotSpot;
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
    }
}
