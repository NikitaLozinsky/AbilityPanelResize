using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityPanelResize
{
    public static class Localization
    {
        public const string FileName = "Localization.json";
        private const Locale FallbackLocale = Locale.enGB;

        private static Dictionary<string, Dictionary<string, string>> s_Packs;
        private static Dictionary<string, string> s_Current;
        private static Dictionary<string, string> s_Fallback;
        private static Locale s_ResolvedLocale = FallbackLocale;
        private static bool s_Loaded;

        public static Locale CurrentLocale => s_ResolvedLocale;

        public static void Load(string modPath)
        {
            s_Packs = null;
            s_Current = null;
            s_Fallback = null;
            s_Loaded = false;

            string path = Path.Combine(modPath ?? string.Empty, FileName);
            if (!File.Exists(path))
            {
                Main.Logger?.Warning($"Localization file not found: {path}. Falling back to raw keys.");
                return;
            }

            LocalizationFile file;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                file = JsonConvert.DeserializeObject<LocalizationFile>(json);
            }
            catch (Exception exception)
            {
                Main.Logger?.Error($"Failed to read {FileName}: {exception.Message}");
                return;
            }

            if (file?.LocalizedStrings == null)
            {
                Main.Logger?.Error($"{FileName} has no LocalizedStrings array. Falling back to raw keys.");
                return;
            }

            s_Packs = BuildPacks(file.LocalizedStrings);
            s_Loaded = s_Packs.Count > 0;
            s_Fallback = FindPack(FallbackLocale);
            RefreshLocale();
        }

        public static void RefreshLocale()
        {
            Locale locale;
            try
            {
                locale = LocalizationManager.CurrentLocale;
            }
            catch (Exception)
            {
                locale = FallbackLocale;
            }

            if (s_Current != null && locale == s_ResolvedLocale)
            {
                return;
            }

            s_ResolvedLocale = locale;
            s_Current = FindPack(locale) ?? s_Fallback;
        }

        public static string Get(string key)
        {
            if (!s_Loaded)
            {
                return key;
            }

            if (s_Current != null && s_Current.TryGetValue(key, out string value))
            {
                return value;
            }

            if (s_Fallback != null && s_Fallback.TryGetValue(key, out string fallbackValue))
            {
                return fallbackValue;
            }

            return key;
        }

        public static string Get(string key, params object[] args)
        {
            string format = Get(key);
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        private static Dictionary<string, Dictionary<string, string>> BuildPacks(List<LocalizedStringEntry> entries)
        {
            var packs = new Dictionary<string, Dictionary<string, string>>();

            foreach (LocalizedStringEntry entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key) || entry.Translations == null)
                {
                    continue;
                }

                foreach (KeyValuePair<string, JToken> translation in entry.Translations)
                {
                    if (translation.Value == null || translation.Value.Type == JTokenType.Null)
                    {
                        continue;
                    }

                    if (!packs.TryGetValue(translation.Key, out Dictionary<string, string> pack))
                    {
                        pack = new Dictionary<string, string>();
                        packs[translation.Key] = pack;
                    }

                    pack[entry.Key] = translation.Value.Type == JTokenType.String
                        ? translation.Value.Value<string>()
                        : translation.Value.ToString();
                }
            }

            return packs;
        }

        private static Dictionary<string, string> FindPack(Locale locale)
        {
            if (s_Packs == null)
            {
                return null;
            }

            return s_Packs.TryGetValue(locale.ToString(), out Dictionary<string, string> pack) ? pack : null;
        }

        private class LocalizationFile
        {
            public List<LocalizedStringEntry> LocalizedStrings { get; set; }
        }

        private class LocalizedStringEntry
        {
            public string Key { get; set; }

            public string SimpleName { get; set; }

            public bool ProcessTemplates { get; set; }

            [JsonExtensionData]
            public IDictionary<string, JToken> Translations { get; set; }
        }
    }
}
