using System;
using System.IO;
using UnityEngine;

namespace WildTamers.Core
{
    /// <summary>What is written to the settings file.</summary>
    [Serializable]
    public class SettingsData
    {
        public int version = 1;
        /// <summary>"en" or "ar".</summary>
        public string language = "";
        public float musicVolume = GameSettings.DefaultMusicVolume;
        public float sfxVolume = GameSettings.DefaultSfxVolume;
    }

    /// <summary>
    /// Player settings: language and the two volume sliders. Saved in their own small JSON file so "Reset save"
    /// (which wipes the animals) never touches them. Loaded once at startup; the first launch picks Arabic when the
    /// device language is Arabic and English otherwise.
    /// </summary>
    public static class GameSettings
    {
        public const float DefaultMusicVolume = 0.7f;
        public const float DefaultSfxVolume = 0.85f;
        public const string English = "en";
        public const string Arabic = "ar";
        private const string FileName = "wildtamers_settings.json";

        private static SettingsData data;
        private static bool dirty;

        /// <summary>Raised when any setting changes (volumes, language).</summary>
        public static event Action Changed;

        /// <summary>Overrides the file location (tests).</summary>
        public static string PathOverride { get; set; }

        public static string FilePath => PathOverride ?? Path.Combine(Application.persistentDataPath, FileName);

        public static bool Loaded => data != null;

        public static string Language
        {
            get { EnsureLoaded(); return data.language; }
        }

        public static float MusicVolume
        {
            get { EnsureLoaded(); return data.musicVolume; }
        }

        public static float SfxVolume
        {
            get { EnsureLoaded(); return data.sfxVolume; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            data = null;
            dirty = false;
            Changed = null;
            PathOverride = null;
        }

        /// <summary>Language the device uses: Arabic if the system language is Arabic, otherwise English.</summary>
        public static string DetectDeviceLanguage() => LanguageFor(Application.systemLanguage);

        /// <summary>"ar" for an Arabic device, "en" for everything else.</summary>
        public static string LanguageFor(SystemLanguage device) => device == SystemLanguage.Arabic ? Arabic : English;

        public static void EnsureLoaded()
        {
            if (data == null) Load();
        }

        /// <summary>Reads the file; a missing or broken file means a first launch (device language, default volumes), which is saved right away.</summary>
        public static void Load()
        {
            data = null;
            try
            {
                var path = FilePath;
                if (File.Exists(path)) data = JsonUtility.FromJson<SettingsData>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Wild Tamers] Settings file could not be read ({e.Message}); using defaults.");
            }

            bool firstLaunch = data == null || string.IsNullOrEmpty(data.language);
            if (data == null) data = new SettingsData();
            if (firstLaunch) data.language = DetectDeviceLanguage();
            Sanitize();
            if (firstLaunch) Write();
        }

        private static void Sanitize()
        {
            if (data.language != Arabic) data.language = English;
            data.musicVolume = Mathf.Clamp01(data.musicVolume);
            data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
        }

        public static void SetLanguage(string code)
        {
            EnsureLoaded();
            code = code == Arabic ? Arabic : English;
            if (data.language == code) return;
            data.language = code;
            Write();
            Changed?.Invoke();
        }

        /// <summary>Sets the music volume (0–1). The file is written by <see cref="Flush"/> (when the slider is let go).</summary>
        public static void SetMusicVolume(float value)
        {
            EnsureLoaded();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(data.musicVolume, value)) return;
            data.musicVolume = value;
            dirty = true;
            Changed?.Invoke();
        }

        public static void SetSfxVolume(float value)
        {
            EnsureLoaded();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(data.sfxVolume, value)) return;
            data.sfxVolume = value;
            dirty = true;
            Changed?.Invoke();
        }

        /// <summary>Writes pending changes to disk.</summary>
        public static void Flush()
        {
            if (dirty) Write();
        }

        private static bool Write()
        {
            dirty = false;
            try
            {
                var path = FilePath;
                var temp = path + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wild Tamers] Could not write settings file: {e.Message}");
                return false;
            }
        }
    }
}
