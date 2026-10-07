using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using WildTamers.Core;

namespace WildTamers.Lang
{
    public enum GameLanguage
    {
        English,
        Arabic
    }

    /// <summary>
    /// The one place the game gets its text from. Wraps Unity Localization: string tables "UI" (menus, battle log, popups…)
    /// and "Animals" (names, descriptions, histories, moves), the current language (saved in <see cref="GameSettings"/>)
    /// and the instant language switch. Every screen listens to <see cref="LanguageChanged"/> and redraws itself.
    /// </summary>
    public static class Loc
    {
        public const string UiTable = "UI";
        public const string AnimalsTable = "Animals";

        private static readonly HashSet<string> warned = new HashSet<string>();
        private static bool initialized;

        public static GameLanguage Language { get; private set; } = GameLanguage.English;

        /// <summary>True when the current language is written right-to-left (Arabic).</summary>
        public static bool IsRTL => Language == GameLanguage.Arabic;

        public static string Code => Language == GameLanguage.Arabic ? GameSettings.Arabic : GameSettings.English;

        /// <summary>Raised after the language changed and the tables of the new language are ready.</summary>
        public static event Action LanguageChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            initialized = false;
            Language = GameLanguage.English;
            LanguageChanged = null;
            warned.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot() => Initialize();

        /// <summary>Loads the saved language (or the device language on a first launch) and the Unity Localization tables.</summary>
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            GameSettings.EnsureLoaded();
            Language = GameSettings.Language == GameSettings.Arabic ? GameLanguage.Arabic : GameLanguage.English;
            ApplyLocale();
        }

        /// <summary>Switches the whole game to <paramref name="language"/> right now, saves the choice and tells every screen.</summary>
        public static void SetLanguage(GameLanguage language)
        {
            Initialize();
            if (language == Language) return;
            Language = language;
            GameSettings.SetLanguage(Code);
            ApplyLocale();
            LanguageChanged?.Invoke();
        }

        private static void ApplyLocale()
        {
            try
            {
                var init = LocalizationSettings.InitializationOperation;
                if (!init.IsDone) init.WaitForCompletion();
                var locale = LocalizationSettings.AvailableLocales.GetLocale(Code);
                if (locale != null && LocalizationSettings.SelectedLocale != locale) LocalizationSettings.SelectedLocale = locale;
                LocalizationSettings.StringDatabase.NoTranslationFoundMessage = "[{key}]";
                var preload = LocalizationSettings.StringDatabase.PreloadOperation;
                if (preload.IsValid() && !preload.IsDone) preload.WaitForCompletion();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wild Tamers] Localization could not start: {e.Message}");
            }
        }

        // ---------- Lookups ----------

        /// <summary>Text from the UI table. <paramref name="args"/> fill {0}, {1}… (Smart Strings: plurals work too).</summary>
        public static string T(string key, params object[] args) => Get(UiTable, key, args);

        public static bool Has(string table, string key)
        {
            Initialize();
            var t = LocalizationSettings.StringDatabase.GetTable(table);
            return t != null && t.GetEntry(key) != null;
        }

        public static string Get(string table, string key, params object[] args)
        {
            Initialize();
            try
            {
                var text = LocalizationSettings.StringDatabase.GetLocalizedString(table, key, null, FallbackBehavior.UseProjectSettings,
                    args ?? Array.Empty<object>());
                if (!string.IsNullOrEmpty(text)) return text;
            }
            catch (Exception e)
            {
                Warn(table, key, e.Message);
                return "[" + key + "]";
            }
            Warn(table, key, "empty");
            return "[" + key + "]";
        }

        private static void Warn(string table, string key, string why)
        {
            if (warned.Add(table + "/" + key)) Debug.LogWarning($"[Wild Tamers] Missing text '{key}' in table {table} ({why}).");
        }

        /// <summary>A field of an animal's texts: name, wild, style, description, history, attack, skill, skilldesc.</summary>
        public static string Animal(string animalId, string field) => Get(AnimalsTable, animalId + "." + field);

        // ---------- Small helpers used all over the UI ----------

        public static string Level(int level) => T("common.level", level);

        public static string Hp(int current, int max) => T("common.hp", current, max);

        /// <summary>"A, B and C" in the current language (Arabic: "A، B و C").</summary>
        public static string JoinNames(IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0) return "";
            if (names.Count == 1) return names[0];
            string head = names[0];
            for (int i = 1; i < names.Count - 1; i++) head = T("list.join", head, names[i]);
            return T("list.and", head, names[names.Count - 1]);
        }
    }
}
