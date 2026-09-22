using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using UnityEngine;

namespace AbilityPanelResize
{
    /// <summary>
    /// Единственное место, где мод лезет в закрытые внутренности
    /// <see cref="ActionBarGroupPCView"/>. Всё разбирается один раз при первом
    /// обращении, дальше вызовы идут через готовые делегаты.
    ///
    /// Раньше те же поля читались через <c>Traverse</c> прямо по месту. Удобно,
    /// но дорого: <c>Traverse</c> ищет член заново на каждом обращении, а
    /// <c>FieldInfo.GetValue</c> для <c>bool</c> и enum ещё и упаковывает
    /// значение в объект. Для разовых вызовов это незаметно, но
    /// <see cref="GetVisiblePositionDelta"/> и <see cref="IsVisible"/> зовутся
    /// на каждом кадре перетаскивания панели — там это был мусор в куче каждый
    /// кадр.
    ///
    /// Если игра обновится и поля переименуют, здесь будет громкое исключение
    /// при первом же обращении — это лучше, чем тихо неверное поведение.
    /// </summary>
    public static class ActionBarGroupAccess
    {
        private static readonly AccessTools.FieldRef<ActionBarGroupPCView, bool> s_VisibleState =
            AccessTools.FieldRefAccess<ActionBarGroupPCView, bool>("VisibleState");

        private static readonly AccessTools.FieldRef<ActionBarGroupPCView, List<ActionBarBaseSlotPCView>> s_SlotsList =
            AccessTools.FieldRefAccess<ActionBarGroupPCView, List<ActionBarBaseSlotPCView>>("m_SlotsList");

        private static readonly AccessTools.FieldRef<ActionBarGroupPCView, RectTransform> s_SlotContainer =
            AccessTools.FieldRefAccess<ActionBarGroupPCView, RectTransform>("m_SlotContainer");

        private static readonly Func<ActionBarGroupPCView, float> s_GetVisiblePositionDelta =
            AccessTools.MethodDelegate<Func<ActionBarGroupPCView, float>>(
                AccessTools.Method(typeof(ActionBarGroupPCView), "GetVisiblePositionDelta"));

        // Тип поля - TextMeshProUGUI. Держим его как FieldInfo, а не как
        // типизированную ссылку, чтобы не тащить в проект сборку TMPro ради
        // единственного обращения при постройке панели.
        private static readonly FieldInfo s_GroupNameLabel =
            AccessTools.Field(typeof(ActionBarGroupPCView), "m_GroupNameLabel");

        // ViewModel объявлена в generic-базе ViewBase<ActionBarVM>. Собирать по
        // ней быстрый делегат рискованно (открытый делегат на метод generic-базы
        // — лишний повод для сюрприза), а вызов через готовый MethodInfo и так
        // избавляет от главного: поиска члена на каждом обращении.
        private static readonly MethodInfo s_ViewModelGetter =
            AccessTools.PropertyGetter(typeof(ActionBarGroupPCView), "ViewModel");

        /// Развёрнута ли панель прямо сейчас.
        public static bool IsVisible(ActionBarGroupPCView view)
        {
            return s_VisibleState(view);
        }

        /// Поправка, на которую игра приподнимает окно над экшн-баром.
        public static float GetVisiblePositionDelta(ActionBarGroupPCView view)
        {
            return s_GetVisiblePositionDelta(view);
        }

        public static List<ActionBarBaseSlotPCView> GetSlots(ActionBarGroupPCView view)
        {
            return s_SlotsList(view);
        }

        /// Куда игра будет парковать новые виджеты слотов.
        public static void SetSlotContainer(ActionBarGroupPCView view, RectTransform container)
        {
            s_SlotContainer(view) = container;
        }

        public static Component GetGroupNameLabel(ActionBarGroupPCView view)
        {
            return s_GroupNameLabel?.GetValue(view) as Component;
        }

        public static ActionBarVM GetViewModel(ActionBarGroupPCView view)
        {
            return s_ViewModelGetter?.Invoke(view, null) as ActionBarVM;
        }
    }
}
