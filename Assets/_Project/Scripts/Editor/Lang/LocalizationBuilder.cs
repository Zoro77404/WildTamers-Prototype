using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using WildTamers.Lang;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Builds everything Unity Localization needs: the English and Arabic locales, the active Localization Settings asset,
    /// the "UI" and "Animals" string tables (filled from <see cref="UiStrings"/> and <see cref="AnimalTexts"/>) and the
    /// per-language font mapping used by <see cref="LocalizedText"/>. Safe to re-run; it rewrites the tables from the data files.
    /// </summary>
    public static class LocalizationBuilder
    {
        public const string Folder = "Assets/_Project/Localization";
        public const string FontsAssetPath = "Assets/_Project/Resources/LocalizationFonts.asset";
        public const string LatinOutlinePath = "Assets/_Project/Fonts/Fredoka-Bold SDF Outline.mat";
        private const string FredokaMedium = "Assets/_Project/Fonts/Fredoka-SemiBold SDF.asset";
        private const string FredokaBold = "Assets/_Project/Fonts/Fredoka-Bold SDF.asset";

        [MenuItem("Wild Tamers/Build/Localization")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder + "/Locales");
            Directory.CreateDirectory(Folder + "/Tables");

            var en = EnsureLocale("en", "English");
            var ar = EnsureLocale("ar", "Arabic");
            EnsureSettings();

            var ui = EnsureCollection(Loc.UiTable);
            var uiEntries = new Dictionary<string, (string en, string ar)>();
            foreach (var e in UiStrings.All) uiEntries[e.Key] = (e.En, e.Ar);
            Fill(ui, en, ar, uiEntries);

            var animals = EnsureCollection(Loc.AnimalsTable);
            var animalEntries = new Dictionary<string, (string en, string ar)>();
            foreach (var a in AnimalTexts.All)
            {
                void Add(string field, string e, string r) => animalEntries[a.Id + "." + field] = (e, r);
                Add("name", a.En.Name, a.Ar.Name);
                Add("the", a.En.The, a.Ar.The);
                Add("wild", a.En.Wild, a.Ar.Wild);
                Add("style", a.En.Style, a.Ar.Style);
                Add("description", a.En.Description, a.Ar.Description);
                Add("history", a.En.History, a.Ar.History);
                Add("attack", a.En.Attack, a.Ar.Attack);
                Add("skill", a.En.Skill, a.Ar.Skill);
                Add("skilldesc", a.En.SkillDescription, a.Ar.SkillDescription);
            }
            Fill(animals, en, ar, animalEntries);

            BuildFonts();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Wild Tamers] Localization built: {uiEntries.Count} UI texts, {animalEntries.Count} animal texts, 2 languages.");
        }

        // ---------- Locales and settings ----------

        private static Locale EnsureLocale(string code, string englishName)
        {
            var existing = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code == code);
            if (existing != null) return existing;
            var locale = Locale.CreateLocale(new LocaleIdentifier(code));
            locale.name = englishName + " (" + code + ")";
            AssetDatabase.CreateAsset(locale, $"{Folder}/Locales/{englishName} ({code}).asset");
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        private static void EnsureSettings()
        {
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null) return;
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "Localization Settings";
            AssetDatabase.CreateAsset(settings, Folder + "/Localization Settings.asset");
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            AssetDatabase.SaveAssets();
        }

        // ---------- Tables ----------

        private static StringTableCollection EnsureCollection(string name)
        {
            var collection = LocalizationEditorSettings.GetStringTableCollection(name);
            if (collection != null) return collection;
            return LocalizationEditorSettings.CreateStringTableCollection(name, Folder + "/Tables");
        }

        private static void Fill(StringTableCollection collection, Locale en, Locale ar, Dictionary<string, (string en, string ar)> entries)
        {
            var enTable = collection.GetTable(en.Identifier) as StringTable ?? collection.AddNewTable(en.Identifier) as StringTable;
            var arTable = collection.GetTable(ar.Identifier) as StringTable ?? collection.AddNewTable(ar.Identifier) as StringTable;

            // Drop keys that no longer exist in the data files.
            foreach (var entry in collection.SharedData.Entries.ToList())
                if (!entries.ContainsKey(entry.Key)) collection.RemoveEntry(entry.Id);

            foreach (var pair in entries)
            {
                Set(enTable, pair.Key, pair.Value.en);
                Set(arTable, pair.Key, pair.Value.ar);
            }

            foreach (var table in new[] { enTable, arTable })
            {
                LocalizationEditorSettings.SetPreloadTableFlag(table, true);
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
        }

        private static void Set(StringTable table, string key, string text)
        {
            var entry = table.GetEntry(key) ?? table.AddEntry(key, text);
            entry.Value = text;
            entry.IsSmart = true;
        }

        // ---------- Fonts ----------

        /// <summary>Outlined damage-number material for a bold font (created next to the font).</summary>
        public static Material EnsureOutlineMaterial(TMP_FontAsset font, string path, string materialName)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(font.material);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = font.material.shader;
                mat.CopyPropertiesFromMaterial(font.material);
            }
            mat.name = materialName;
            mat.SetFloat("_OutlineWidth", 0.26f);
            mat.SetColor("_OutlineColor", Palette.Ink);
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetColor("_UnderlayColor", new Color(0.08f, 0.12f, 0.2f, 0.4f));
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", -0.9f);
            mat.SetFloat("_UnderlayDilate", 0.3f);
            mat.SetFloat("_UnderlaySoftness", 0.25f);
            mat.EnableKeyword("UNDERLAY_ON");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>The Resources asset that tells <see cref="LocalizedText"/> which font each language uses.</summary>
        public static LocalizationFonts BuildFonts()
        {
            var latinMedium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FredokaMedium);
            var latinBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FredokaBold);
            var arabicMedium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ArabicFontBuilder.MediumPath);
            var arabicBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ArabicFontBuilder.BoldPath);
            if (arabicMedium == null || arabicBold == null)
            {
                ArabicFontBuilder.BuildAll();
                arabicMedium = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ArabicFontBuilder.MediumPath);
                arabicBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ArabicFontBuilder.BoldPath);
            }

            var asset = AssetDatabase.LoadAssetAtPath<LocalizationFonts>(FontsAssetPath);
            if (asset == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FontsAssetPath));
                asset = ScriptableObject.CreateInstance<LocalizationFonts>();
                AssetDatabase.CreateAsset(asset, FontsAssetPath);
            }
            asset.latinMedium = latinMedium;
            asset.latinBold = latinBold;
            asset.arabicMedium = arabicMedium;
            asset.arabicBold = arabicBold;
            asset.latinBoldOutline = EnsureOutlineMaterial(latinBold, LatinOutlinePath, "Fredoka-Bold SDF Outline");
            asset.arabicBoldOutline = EnsureOutlineMaterial(arabicBold, ArabicFontBuilder.BoldOutlinePath, "Tajawal-Bold SDF Outline");
            EditorUtility.SetDirty(asset);
            return asset;
        }
    }
}
