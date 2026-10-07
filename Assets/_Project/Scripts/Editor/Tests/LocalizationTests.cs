using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RTLTMPro;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TestTools;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.Lang;

namespace WildTamers.EditorTools.Tests
{
    /// <summary>Edit-mode checks for the languages: complete tables, clean Arabic text, glyph coverage, settings and language switching.</summary>
    public class LocalizationTests
    {
        private static readonly string[] AnimalFields = { "name", "the", "wild", "style", "description", "history", "attack", "skill", "skilldesc" };
        // Texts that legitimately show Latin letters in Arabic: the English language button and keyboard keys in the controls hint.
        private static readonly HashSet<string> LatinAllowed = new HashSet<string> { "settings.lang.en", "hud.hint" };

        private string tempSettings;

        [SetUp]
        public void SetUp()
        {
            Loc.Initialize();
            Loc.SetLanguage(GameLanguage.English);
            tempSettings = Path.Combine(Path.GetTempPath(), "wildtamers_settings_test_" + System.Guid.NewGuid().ToString("N") + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            GameSettings.PathOverride = null;
            GameSettings.Load();
            Loc.SetLanguage(GameLanguage.English);
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
        }

        private static StringTable Table(string name, string code) =>
            LocalizationSettings.StringDatabase.GetTable(name, LocalizationSettings.AvailableLocales.GetLocale(code));

        private static IEnumerable<(string table, string key, string text)> AllArabic()
        {
            foreach (var name in new[] { Loc.UiTable, Loc.AnimalsTable })
            {
                var table = Table(name, "ar");
                foreach (var e in table.SharedData.Entries) yield return (name, e.Key, table.GetEntry(e.Key)?.Value);
            }
        }

        [Test]
        public void BothLanguagesHaveEveryText()
        {
            foreach (var name in new[] { Loc.UiTable, Loc.AnimalsTable })
            {
                var en = Table(name, "en");
                var ar = Table(name, "ar");
                Assert.IsNotNull(en, name + " (en)");
                Assert.IsNotNull(ar, name + " (ar)");
                Assert.Greater(en.SharedData.Entries.Count, 20, name + " has texts");
                foreach (var entry in en.SharedData.Entries)
                {
                    Assert.IsFalse(string.IsNullOrWhiteSpace(en.GetEntry(entry.Key)?.Value), $"{name}/{entry.Key} English");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(ar.GetEntry(entry.Key)?.Value), $"{name}/{entry.Key} Arabic");
                }
            }
        }

        [Test]
        public void EveryAnimalHasAllNineTextsInBothLanguages()
        {
            var db = AssetDatabase.LoadAssetAtPath<AnimalDatabase>("Assets/_Project/Resources/AnimalDatabase.asset");
            foreach (var language in new[] { GameLanguage.English, GameLanguage.Arabic })
            {
                Loc.SetLanguage(language);
                foreach (var animal in db.Animals)
                    foreach (var field in AnimalFields)
                        Assert.IsTrue(Loc.Has(Loc.AnimalsTable, animal.id + "." + field), $"{animal.id}.{field} ({language})");
            }
        }

        [Test]
        public void ArabicNamesAreRealArabic()
        {
            Loc.SetLanguage(GameLanguage.Arabic);
            Assert.AreEqual("جمل", Loc.Animal("camel", "name"));
            Assert.AreEqual("صقر", Loc.Animal("falcon", "name"));
            Assert.AreEqual("حصان عربي", Loc.Animal("arabian_horse", "name"));
            StringAssert.Contains("سفينة الصحراء", Loc.Animal("camel", "description"));
        }

        [Test]
        public void ArabicHasNoEnglishLettersAndNoDiacritics()
        {
            foreach (var (table, key, text) in AllArabic())
            {
                if (LatinAllowed.Contains(key)) continue;
                // Strip rich-text tags and smart-string placeholders before checking.
                var plain = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>|\\{[^}]*\\}", "");
                foreach (char c in plain)
                {
                    Assert.IsFalse(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z', $"{table}/{key} has the Latin letter '{c}'");
                    Assert.IsFalse(c >= 0x064B && c <= 0x0652, $"{table}/{key} has a diacritic");
                }
            }
        }

        [Test]
        public void EveryArabicLetterHasAGlyphInBothArabicFonts()
        {
            var fonts = LocalizationFonts.Instance;
            Assert.IsNotNull(fonts, "LocalizationFonts asset in Resources");
            var builder = new FastStringBuilder(RTLSupport.DefaultBufferSize);
            foreach (var font in new[] { fonts.arabicMedium, fonts.arabicBold })
            {
                font.ReadFontAssetDefinition();
                foreach (var (table, key, text) in AllArabic())
                {
                    // Expand placeholders with sample numbers, then shape exactly as the game does.
                    var sample = System.Text.RegularExpressions.Regex.Replace(text, "\\{[^}]*\\}", "5");
                    builder.Clear();
                    RTLSupport.FixRTL(sample, builder, farsi: false, fixTextTags: true, preserveNumbers: false);
                    var shaped = builder.ToString();
                    foreach (char c in shaped)
                    {
                        if (c == '\n' || c == '\r' || c == '<' || c == '>' || c == 0xFFFF) continue;
                        if (c < 0x20) continue;
                        Assert.IsTrue(font.characterLookupTable.ContainsKey(c) || font.HasCharacter(c, true),
                            $"{font.name} lacks U+{(int)c:X4} used in {table}/{key}");
                    }
                }
            }
        }

        [Test]
        public void ArabicShapingJoinsLettersAndKeepsEnglishWordsAndNumbers()
        {
            var builder = new FastStringBuilder(RTLSupport.DefaultBufferSize);
            RTLSupport.FixRTL("مروضو البرية", builder, farsi: false, fixTextTags: true, preserveNumbers: false);
            var shaped = builder.ToString();
            // No plain base-block letters are left: every letter was turned into a joined presentation form.
            Assert.IsFalse(shaped.Any(c => c >= 0x0621 && c <= 0x064A), "all letters are presentation forms");
            Assert.IsTrue(shaped.Any(c => c >= 0xFE70 && c <= 0xFEFF), "presentation forms are used");
            builder.Clear();
            RTLSupport.FixRTL("خبرة 25", builder, farsi: false, fixTextTags: true, preserveNumbers: false);
            var digits = new string(builder.ToString().Where(c => c >= '\u0660' && c <= '\u0669').OrderBy(c => c).ToArray());
            Assert.AreEqual("\u0662\u0665", digits, "the digits 2 and 5 become Arabic-Indic digits");
        }

        [Test]
        public void SwitchingLanguageChangesTextAndRaisesTheEvent()
        {
            int raised = 0;
            void Handler() => raised++;
            Loc.LanguageChanged += Handler;
            try
            {
                Assert.AreEqual("Play", Loc.T("menu.play"));
                Loc.SetLanguage(GameLanguage.Arabic);
                Assert.IsTrue(Loc.IsRTL);
                Assert.AreEqual("العب", Loc.T("menu.play"));
                Loc.SetLanguage(GameLanguage.Arabic);
                Assert.AreEqual(1, raised, "no event when the language did not change");
                Loc.SetLanguage(GameLanguage.English);
                Assert.IsFalse(Loc.IsRTL);
                Assert.AreEqual(2, raised);
            }
            finally { Loc.LanguageChanged -= Handler; }
        }

        [Test]
        public void ArabicPluralsAgreeWithTheNumber()
        {
            Loc.SetLanguage(GameLanguage.Arabic);
            StringAssert.Contains("حيوانا واحدا", Loc.T("select.pick", 1));
            StringAssert.Contains("حيوانين", Loc.T("select.pick", 2));
            StringAssert.Contains("3 حيوانات", Loc.T("select.pick", 3));
            Loc.SetLanguage(GameLanguage.English);
            Assert.AreEqual("Only 1 animal can fight right now.", Loc.T("select.short", 1));
            Assert.AreEqual("Only 2 animals can fight right now.", Loc.T("select.short", 2));
            Assert.AreEqual("3 animals", Loc.T("team.count", 3));
            Assert.AreEqual("1 animal", Loc.T("team.count", 1));
        }

        [Test]
        public void MirroringSwapsLeftAndRightOnly()
        {
            Assert.AreEqual(TextAlignmentOptions.MidlineRight, LocalizedText.Mirror(TextAlignmentOptions.MidlineLeft));
            Assert.AreEqual(TextAlignmentOptions.TopLeft, LocalizedText.Mirror(TextAlignmentOptions.TopRight));
            Assert.AreEqual(TextAlignmentOptions.Center, LocalizedText.Mirror(TextAlignmentOptions.Center));
            Assert.AreEqual(TextAlignmentOptions.Bottom, LocalizedText.Mirror(TextAlignmentOptions.Bottom));
        }

        [Test]
        public void SettingsAreSavedAndLoaded()
        {
            GameSettings.PathOverride = tempSettings;
            GameSettings.Load();
            Assert.IsTrue(File.Exists(tempSettings), "a first launch writes the file");
            Assert.AreEqual(GameSettings.DetectDeviceLanguage(), GameSettings.Language, "first launch follows the device language");
            Assert.AreEqual(GameSettings.DefaultMusicVolume, GameSettings.MusicVolume, 0.0001f);

            GameSettings.SetLanguage("ar");
            GameSettings.SetMusicVolume(0.25f);
            GameSettings.SetSfxVolume(0.4f);
            GameSettings.Flush();

            GameSettings.Load();
            Assert.AreEqual("ar", GameSettings.Language);
            Assert.AreEqual(0.25f, GameSettings.MusicVolume, 0.0001f);
            Assert.AreEqual(0.4f, GameSettings.SfxVolume, 0.0001f);

            GameSettings.SetMusicVolume(5f);
            Assert.AreEqual(1f, GameSettings.MusicVolume, "volumes are clamped");
        }

        [Test]
        public void ABrokenSettingsFileFallsBackToDefaults()
        {
            File.WriteAllText(tempSettings, "{ this is not json");
            GameSettings.PathOverride = tempSettings;
            LogAssert.ignoreFailingMessages = true;
            try { GameSettings.Load(); }
            finally { LogAssert.ignoreFailingMessages = false; }
            Assert.That(GameSettings.Language, Is.EqualTo("en").Or.EqualTo("ar"));
            Assert.AreEqual(GameSettings.DefaultSfxVolume, GameSettings.SfxVolume, 0.0001f);
        }
    }
}
