using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.UI;
using Kingmaker.UI.Log;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using UnityEngine;
using UnityEngine.UI;

namespace AbilityPanelResize
{
    // Лёгкая обёртка, которая делает RectTransform совместимым с готовым
    // игровым компонентом ResizePanel (Kingmaker.UI.Common) - тем самым,
    // что уже используется для лога боя. ActionBarGroupPCView сам по себе
    // IResizeElement не реализует, поэтому вешаем этот адаптер отдельным
    // компонентом на тот же GameObject, вместо того чтобы патчить
    // ActionBarGroupPCView под несвойственную ему задачу.
    public class ResizeElementAdapter : MonoBehaviour, IResizeElement
    {
        private RectTransform m_Rect;
        private ActionBarGroupPCView m_GroupView;
        private Traverse m_GroupViewTraverse;
        private ScrollRect m_ScrollRect;

        // "Родной" центр окна по X, захватывается один раз при первом же
        // изменении размера - точка, относительно которой окно должно расти
        // симметрично влево-вправо.
        private float? m_InitialCenterX;

        private RectTransform Rect => m_Rect != null ? m_Rect : (m_Rect = GetComponent<RectTransform>());

        private void EnsureGroupView()
        {
            if (m_GroupView == null)
            {
                m_GroupView = GetComponent<ActionBarGroupPCView>();
                if (m_GroupView != null)
                {
                    m_GroupViewTraverse = Traverse.Create(m_GroupView);
                }
            }
        }

        // Вызывается хендлом (PanelResizeHandle) в момент начала
        // перетаскивания - временно отключаем ScrollRect (не саму маску
        // обрезки RectMask2D, только компонент ScrollRect), чтобы его
        // внутренняя логика (пересчёт видимой области при каждом изменении
        // размера Viewport) не пересчитывалась на каждом кадре драга.
        public void BeginResize()
        {
            if (m_ScrollRect == null)
            {
                m_ScrollRect = GetComponent<ScrollRect>();
            }
            if (m_ScrollRect != null)
            {
                m_ScrollRect.enabled = false;
            }
        }

        public void EndResize()
        {
            if (m_ScrollRect != null)
            {
                m_ScrollRect.enabled = true;
            }

            // Персистентность размера между сессиями - сохраняем в СВОЙ
            // отдельный файл настроек мода (Settings.xml), не в сейв игры
            // (см. Settings.cs). Пишем именно здесь, а не на каждый кадр
            // драга - дешёвая операция, но незачем дёргать файловую
            // систему сотни раз за одно перетаскивание.
            if (Main.Settings != null && Main.ModEntry != null)
            {
                Vector2 size = Rect.sizeDelta;
                Main.Settings.Width = size.x;
                Main.Settings.Height = size.y;
                Main.Settings.Save(Main.ModEntry);
            }
        }

        public void SetSizeDelta(Vector2 size)
        {
            if (m_InitialCenterX == null)
            {
                // Захватываем ДО применения нового размера, пока окно ещё в
                // своём изначальном положении.
                m_InitialCenterX = Rect.anchoredPosition.x + Rect.sizeDelta.x / 2f;
            }

            Rect.sizeDelta = size;

            // Pivot окна (0,1) - левый край зафиксирован намертво, любой
            // рост ширины физически идёт только вправо. Кнопка сворачивания
            // сидит по горизонтальному центру ОКНА, поэтому при таком росте
            // она визуально "уезжала" вправо вместе с растущей шириной.
            // Готового игрового механизма для горизонтали, в отличие от
            // вертикали (SetStatePosition), нет - компенсируем сами: держим
            // центр окна на месте, сдвигая anchoredPosition.x так, чтобы обе
            // грани расширялись симметрично от исходного центра.
            Vector2 anchoredPosition = Rect.anchoredPosition;
            anchoredPosition.x = m_InitialCenterX.Value - size.x / 2f;
            Rect.anchoredPosition = anchoredPosition;

            EnsureGroupView();
            RepositionToKeepBottomFixed();
        }

        // Подстраховка от залипания курсора: у игры CursorController -
        // общий (статический) флаг IsResizeCursor на всю игру, не привязан
        // к конкретному окну. Наши несколько хендлов ресайза (плюс игровой
        // ResizePanel) выставляют/сбрасывают его по наведению/окончанию
        // драга - если у какого-то хендла OnPointerUp/OnPointerExit не
        // сработает вовремя (мышь отпущена не совсем там, фокус на миг
        // потерян и т.п.), флаг может "залипнуть" в true. Тогда система
        // прицеливания способностей (тоже проверяющая этот флаг) не сможет
        // показать свою иконку курсора - при этом сама способность всё
        // равно выбирается и работает, потому что это два независимых
        // механизма. Раз у нас теперь несколько хендлов одновременно на
        // одном окне (чего игра в оригинале, видимо, не делает) - шанс
        // словить такую нестыковку выше обычного. На каждое отпускание ЛКМ
        // где угодно принудительно сбрасываем флаг, если он завис - в
        // штатной ситуации хендл и так успеет сбросить его сам, эта
        // проверка просто окажется избыточной, а не вредной.
        private void Update()
        {
            if (Input.GetMouseButtonUp(0) && CursorController.IsResizeCursor)
            {
                CursorController.IsResizeCursor = false;
                Game.Instance.CursorController.ClearCursor();
                Game.Instance.CursorController.SetCustomCursor(CursorRoot.CursorType.None, Vector2.zero);
            }
        }

        // У ActionBarGroupPCView есть встроенный механизм, который держит
        // нижний край окна прижатым к панели быстрого доступа, пересчитывая
        // anchoredPosition по формуле от sizeDelta.y (см. SetStatePosition/
        // RecalculatePositionCoroutine в самой игре):
        //   anchoredPosition.y = sizeDelta.y - 5f - GetVisiblePositionDelta()
        //
        // Раньше здесь вызывался сам SetStatePosition через рефлексию - но
        // он не применяет позицию сразу, а запускает корутину с задержкой в
        // один кадр (yield return null). При непрерывном перетаскивании
        // мышью (вызов на каждом кадре) это давало заметное отставание
        // позиции от размера - окно "дёргалось". Считаем ту же формулу сами,
        // синхронно, в момент установки размера - тогда размер и позиция
        // всегда идут в ногу без единого кадра рассинхрона. Последующие
        // родные пересчёты игры (например, при сворачивании панели) дадут
        // тот же результат для того же sizeDelta.y, так что расхождения не
        // возникнет.
        private void RepositionToKeepBottomFixed()
        {
            if (m_GroupViewTraverse == null)
            {
                return;
            }

            bool visibleState = m_GroupViewTraverse.Field("VisibleState").GetValue<bool>();
            if (!visibleState)
            {
                // При свёрнутой панели игра держит anchoredPosition.y = 0 -
                // не мешаем.
                return;
            }

            float visibleDelta = m_GroupViewTraverse.Method("GetVisiblePositionDelta").GetValue<float>();
            float targetY = Rect.sizeDelta.y - 5f - visibleDelta;

            Vector2 anchoredPosition = Rect.anchoredPosition;
            anchoredPosition.y = targetY;
            Rect.anchoredPosition = anchoredPosition;
        }

        public Vector2 GetSize()
        {
            return Rect.sizeDelta;
        }

        public RectTransform GetTransform()
        {
            return Rect;
        }
    }
}