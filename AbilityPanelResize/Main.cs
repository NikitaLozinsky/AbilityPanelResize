using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace AbilityPanelResize
{
    public static class Main
    {
        public static UnityModManager.ModEntry.ModLogger Logger;
        public static UnityModManager.ModEntry ModEntry;
        public static Settings Settings;
        public static bool Enabled;

        private static Harmony s_Harmony;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            ModEntry = modEntry;

            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            Localization.Load(modEntry.Path);

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            modEntry.OnUpdate = OnUpdate;

            Logger.Log(Localization.Get("AbilityPanelResize.Log.Loaded"));
            return true;
        }

        public static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (Enabled == value)
            {
                return true;
            }

            Enabled = value;

            if (Enabled)
            {
                if (s_Harmony == null)
                {
                    s_Harmony = new Harmony(modEntry.Info.Id);
                }

                s_Harmony.PatchAll(Assembly.GetExecutingAssembly());
                Logger.Log(Localization.Get("AbilityPanelResize.Log.Enabled"));
            }
            else
            {
                s_Harmony?.UnpatchAll(modEntry.Info.Id);
                Logger.Log(Localization.Get("AbilityPanelResize.Log.Disabled"));
            }

            return true;
        }

        /// <summary>
        /// Дамп состояния курсора по Ctrl+Alt+C.
        ///
        /// Хоткей висит здесь, а не в <see cref="AbilityPanelDiagnostics"/>,
        /// потому что тот компонент живёт на самой панели: пока панель выключена
        /// (а вне боя она чаще всего именно такая), Unity не зовёт его
        /// <c>Update</c>, и дамп снимался бы только в бою — то есть ровно в
        /// одном из двух состояний, которые и надо сравнить. UMM зовёт свой
        /// <c>OnUpdate</c> независимо от игровых объектов.
        /// </summary>
        private static void OnUpdate(UnityModManager.ModEntry modEntry, float deltaTime)
        {
            if (Settings == null || !Settings.Diagnostics || !Input.GetKeyDown(KeyCode.C))
            {
                return;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            if (ctrl && alt)
            {
                Logger.Log(CursorProbe.Build());
            }
        }

        private static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (Settings == null)
            {
                return;
            }

            Localization.RefreshLocale();

            GUILayout.Label(Localization.Get("AbilityPanelResize.Settings.Language", Localization.CurrentLocale));
            GUILayout.Space(8f);

            Section("AbilityPanelResize.Settings.Section.Size");

            string savedSize = Settings.HasSavedSize
                ? $"{Settings.Width:0}x{Settings.Height:0}"
                : Localization.Get("AbilityPanelResize.Settings.SavedSize.None");
            GUILayout.Label(Localization.Get("AbilityPanelResize.Settings.SavedSize", savedSize));

            Settings.RememberSize = GUILayout.Toggle(Settings.RememberSize, Localization.Get("AbilityPanelResize.Settings.RememberSize"));

            if (GUILayout.Button(Localization.Get("AbilityPanelResize.Settings.Reset"), GUILayout.Width(360f)))
            {
                Settings.ResetSize();
                Settings.Save(modEntry);
            }

            Settings.DefaultHeight = Slider("AbilityPanelResize.Settings.DefaultHeight", Settings.DefaultHeight,
                ResizeLimits.MinSize.y, ResizeLimits.MaxHeightCeiling, 10f);

            GUILayout.Space(8f);
            Section("AbilityPanelResize.Settings.Section.Limits");

            float maxWidth = Slider("AbilityPanelResize.Settings.MaxWidth", Settings.MaxWidth,
                ResizeLimits.MinSize.x, ResizeLimits.MaxWidthCeiling, 10f);
            float maxHeight = Slider("AbilityPanelResize.Settings.MaxHeight", Settings.MaxHeight,
                ResizeLimits.MinSize.y, ResizeLimits.MaxHeightCeiling, 10f);
            Hint("AbilityPanelResize.Settings.LimitsHint");

            GUILayout.Space(8f);
            Section("AbilityPanelResize.Settings.Section.Behaviour");

            bool centerIcons = GUILayout.Toggle(Settings.CenterIcons, Localization.Get("AbilityPanelResize.Settings.CenterIcons"));
            Hint("AbilityPanelResize.Settings.CenterIconsHint");

            bool stencilMask = GUILayout.Toggle(Settings.StencilMask, Localization.Get("AbilityPanelResize.Settings.StencilMask"));
            Hint("AbilityPanelResize.Settings.StencilMaskHint");

            float scrollSensitivity = Slider("AbilityPanelResize.Settings.ScrollSensitivity", Settings.ScrollSensitivity, 5f, 80f, 1f);
            float handleThickness = Slider("AbilityPanelResize.Settings.HandleThickness", Settings.HandleThickness, 6f, 40f, 1f);
            Hint("AbilityPanelResize.Settings.HandleThicknessHint");

            GUILayout.Space(8f);
            Section("AbilityPanelResize.Settings.Section.GameFixes");

            Settings.SyncWorldSelection = GUILayout.Toggle(Settings.SyncWorldSelection,
                Localization.Get("AbilityPanelResize.Settings.SyncWorldSelection"));
            Hint("AbilityPanelResize.Settings.SyncWorldSelectionHint");

            GUILayout.Space(8f);
            Section("AbilityPanelResize.Settings.Section.Diagnostics");
            bool diagnostics = GUILayout.Toggle(Settings.Diagnostics,
                Localization.Get("AbilityPanelResize.Settings.Diagnostics"));
            Hint("AbilityPanelResize.Settings.DiagnosticsHint");

            GUILayout.Space(8f);
            Hint("AbilityPanelResize.Settings.ApplyHint");

            bool changed =
                !Mathf.Approximately(maxWidth, Settings.MaxWidth) ||
                !Mathf.Approximately(maxHeight, Settings.MaxHeight) ||
                !Mathf.Approximately(scrollSensitivity, Settings.ScrollSensitivity) ||
                !Mathf.Approximately(handleThickness, Settings.HandleThickness) ||
                centerIcons != Settings.CenterIcons ||
                stencilMask != Settings.StencilMask ||
                diagnostics != Settings.Diagnostics;

            Settings.MaxWidth = maxWidth;
            Settings.MaxHeight = maxHeight;
            Settings.ScrollSensitivity = scrollSensitivity;
            Settings.HandleThickness = handleThickness;
            Settings.CenterIcons = centerIcons;
            Settings.StencilMask = stencilMask;
            Settings.Diagnostics = diagnostics;

            if (changed)
            {
                ApplyLiveSettings();
            }
        }

        private static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            Settings?.Save(modEntry);
        }

        public static void ApplyLiveSettings()
        {
            if (Settings == null)
            {
                return;
            }

            // Через реестр адаптеров, а не FindObjectsOfType: тот не видит
            // выключенные объекты, а свёрнутая или пустая панель как раз
            // выключена — настройки до неё просто не доезжали.
            ResizeElementAdapter.ApplyLiveSettingsToAll();
        }

        private static void Section(string key)
        {
            GUILayout.Label($"<b>{Localization.Get(key)}</b>");
        }

        private static void Hint(string key)
        {
            GUILayout.Label($"<i>{Localization.Get(key)}</i>");
        }

        private static float Slider(string labelKey, float value, float min, float max, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(Localization.Get(labelKey, value.ToString("0")), GUILayout.Width(360f));
            float result = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(300f));
            GUILayout.EndHorizontal();
            return Mathf.Round(result / step) * step;
        }
    }
}
