using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;
//using UnityModManager = UnityModManagerNet.UnityModManager;

namespace AbilityPanelResize
{
    public static class Main
    {
        // Хранилище для логгера, чтобы вызывать его из любого места нашего мода
        public static UnityModManager.ModEntry.ModLogger Logger;
        public static bool Enabled;

        // Нужен дальше, чтобы вызывать Settings.Save(ModEntry) из любого
        // места (например, из ResizeElementAdapter.EndResize) без протаскивания
        // ссылки через конструкторы MonoBehaviour-компонентов.
        public static UnityModManager.ModEntry ModEntry;

        // Наши собственные настройки (размер панели) - отдельно от сейва игры,
        // см. комментарий в Settings.cs.
        public static Settings Settings;

        private static Harmony s_Harmony;

        // Точка входа, которую UMM найдет по нашей инструкции из Info.json
        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            // Инициализируем логгер префиксом нашего мода
            Logger = modEntry.Logger;
            ModEntry = modEntry;

            // Грузим настройки сразу при загрузке мода (не при первом
            // Initialize панели) - если файла ещё нет, Load<T> вернёт объект
            // со значениями по умолчанию (-1/-1), это не ошибка.
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);

            // Привязываем событие переключения ползунка Вкл/Выкл в меню UMM (Ctrl+F10)
            modEntry.OnToggle = OnToggle;

            // Рисует вкладку настроек мода в том же окне UMM (Ctrl+F10) -
            // кнопка сброса сохранённого размера панели к значениям по
            // умолчанию.
            modEntry.OnGUI = OnGUI;

            Logger.Log("Привет, Мир! Мод AbilityPanelResize успешно запущен!");
            return true;
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            string current = Settings.Width > 0f && Settings.Height > 0f
                ? $"{Settings.Width:0}x{Settings.Height:0}"
                : "не задан (используется размер по умолчанию)";
            GUILayout.Label("Сохранённый размер панели способностей: " + current);

            if (GUILayout.Button("Сбросить размер к значениям по умолчанию"))
            {
                Settings.Width = -1f;
                Settings.Height = -1f;
                Settings.Save(modEntry);
            }

            GUILayout.Label("Изменение применится при следующем построении панели " +
                             "(например, при перезаходе на локацию).");
        }

        // Логика, которая срабатывает, когда игрок включает или выключает мод в меню
        public static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            // Защита: если состояние не изменилось, ничего не делаем
            if (Enabled == value) return true;

            Enabled = value;

            if (Enabled)
            {
                // PatchAll сканирует текущую сборку и сам находит все классы
                // с атрибутом [HarmonyPatch] - вручную перечислять патчи не нужно.
                if (s_Harmony == null)
                {
                    s_Harmony = new Harmony(modEntry.Info.Id);
                }
                s_Harmony.PatchAll(Assembly.GetExecutingAssembly());
                Logger.Log("Мод активирован, патчи Harmony применены.");
            }
            else
            {
                s_Harmony?.UnpatchAll(modEntry.Info.Id);
                Logger.Log("Мод деактивирован, патчи Harmony сняты.");
            }

            return true;
        }
    }
}
