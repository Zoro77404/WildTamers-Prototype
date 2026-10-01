using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WildTamers.Core
{
    /// <summary>One team animal in the save file.</summary>
    [Serializable]
    public class AnimalSaveData
    {
        public string uid;
        public string animalId;
        public int level;
        public int experience;
        public int currentHP;
    }

    /// <summary>Everything written to disk: the team and which animal is active.</summary>
    [Serializable]
    public class SaveData
    {
        public int version = SaveSystem.Version;
        public string savedAt;
        public int activeIndex;
        public List<AnimalSaveData> team = new List<AnimalSaveData>();
    }

    /// <summary>
    /// JSON save file in <see cref="Application.persistentDataPath"/>.
    /// Writes go to a temp file first so a crash mid-write never corrupts the last good save.
    /// </summary>
    public static class SaveSystem
    {
        public const int Version = 1;
        private const string FileName = "wildtamers_save.json";

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static bool Exists => File.Exists(FilePath);

        public static bool TryLoad(out SaveData data)
        {
            data = null;
            var path = FilePath;
            if (!File.Exists(path)) return false;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null || data.team == null) throw new InvalidDataException("Empty save.");
                return true;
            }
            catch (Exception e)
            {
                // Keep the broken file for inspection and start fresh instead of crashing.
                Debug.LogWarning($"[Wild Tamers] Save file could not be read ({e.Message}); starting a new game. Broken file kept as .bad.");
                TryMove(path, path + ".bad");
                data = null;
                return false;
            }
        }

        public static bool Write(SaveData data)
        {
            var path = FilePath;
            var temp = path + ".tmp";
            try
            {
                data.version = Version;
                data.savedAt = DateTime.UtcNow.ToString("o");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wild Tamers] Could not write save file: {e.Message}");
                return false;
            }
        }

        public static void Delete()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Wild Tamers] Could not delete save file: {e.Message}");
            }
        }

        private static void TryMove(string from, string to)
        {
            try
            {
                if (File.Exists(to)) File.Delete(to);
                File.Move(from, to);
            }
            catch (Exception)
            {
                // Best effort only.
            }
        }
    }
}
