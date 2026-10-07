using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RTLTMPro;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Builds the Arabic TextMeshPro font assets from Tajawal (Google Fonts, SIL OFL):
    /// a static atlas with every Arabic letter (base block + joined presentation forms), Arabic-Indic and Western digits,
    /// Latin letters and punctuation. Tajawal has no glyph for the "isolated" presentation forms RTLTMPro writes for letters that stand alone,
    /// so those code points are aliased to the glyph of the base letter (the same shape). The English Fredoka fonts get Tajawal as a fallback
    /// so Arabic words (the language button) still show while the game is in English.
    /// </summary>
    public static class ArabicFontBuilder
    {
        public const string FolderOut = "Assets/_Project/Fonts";
        public const string MediumPath = FolderOut + "/Tajawal-Medium SDF.asset";
        public const string BoldPath = FolderOut + "/Tajawal-Bold SDF.asset";
        public const string BoldOutlinePath = FolderOut + "/Tajawal-Bold SDF Outline.mat";
        private const string FontFolder = "Assets/ThirdParty/GoogleFonts/Tajawal";
        private const string FredokaMedium = FolderOut + "/Fredoka-SemiBold SDF.asset";
        private const string FredokaBold = FolderOut + "/Fredoka-Bold SDF.asset";
        private const int SamplingSize = 80;
        private const int Padding = 8;
        private const int AtlasSize = 2048;

        [MenuItem("Wild Tamers/Build/Arabic Fonts")]
        public static void BuildAll()
        {
            var medium = Build($"{FontFolder}/Tajawal-Medium.ttf", MediumPath);
            var bold = Build($"{FontFolder}/Tajawal-Bold.ttf", BoldPath);
            AddFallback(FredokaMedium, medium);
            AddFallback(FredokaBold, bold);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Wild Tamers] Arabic fonts built ({medium.characterTable.Count} + {bold.characterTable.Count} characters).");
        }

        private static TMP_FontAsset Build(string ttfPath, string assetPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null) throw new FileNotFoundException(ttfPath);

            var old = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (old != null) AssetDatabase.DeleteAsset(assetPath);

            var asset = TMP_FontAsset.CreateFontAsset(font, SamplingSize, Padding, GlyphRenderMode.SDFAA, AtlasSize, AtlasSize,
                AtlasPopulationMode.Dynamic, true);
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            asset.material.name = asset.name + " Material";
            var mobile = Shader.Find("TextMeshPro/Mobile/Distance Field");
            if (mobile != null) asset.material.shader = mobile;

            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            // Which of the wanted characters does the font really have?
            var wanted = WantedCharacters(font);
            asset.TryAddCharacters(wanted, out var missing);
            AliasIsolatedForms(asset);
            asset.ReadFontAssetDefinition();

            // The atlas texture(s) must live inside the asset file.
            for (int i = 0; i < asset.atlasTextures.Length; i++)
            {
                var tex = asset.atlasTextures[i];
                tex.name = asset.name + " Atlas" + (i == 0 ? "" : " " + i);
                AssetDatabase.AddObjectToAsset(tex, asset);
            }
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(asset.material);
            AssetDatabase.SaveAssetIfDirty(asset);

            var report = missing == null ? 0 : missing.Length;
            Debug.Log($"[Wild Tamers] {asset.name}: {asset.characterTable.Count} characters, {asset.atlasTextures.Length} atlas page(s), {report} requested code points not in the font.");
            return asset;
        }

        private static uint[] WantedCharacters(Font font)
        {
            var set = new SortedSet<uint>();
            for (uint c = 0x20; c <= 0x7E; c++) set.Add(c);
            for (uint c = 0xA0; c <= 0xFF; c++) set.Add(c);
            foreach (uint c in new uint[] { 0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2026, 0x00D7, 0x00AB, 0x00BB })
                set.Add(c);
            // Arabic: base letters, digits (٠-٩), punctuation, marks, then every joined presentation form the font has.
            for (uint c = 0x0600; c <= 0x06FF; c++) set.Add(c);
            for (uint c = 0xFB50; c <= 0xFDFF; c++) set.Add(c);
            for (uint c = 0xFE70; c <= 0xFEFF; c++) set.Add(c);
            return set.Where(c => font.HasCharacter((char)c)).ToArray();
        }

        /// <summary>Points the isolated presentation forms the font lacks (U+FE8D…) at the glyph of the base letter.</summary>
        private static void AliasIsolatedForms(TMP_FontAsset asset)
        {
            asset.ReadFontAssetDefinition();
            int aliased = 0;
            foreach (ArabicGeneralLetters letter in Enum.GetValues(typeof(ArabicGeneralLetters)))
            {
                uint baseCode = (uint)letter;
                uint isolated = GlyphTable.Convert((char)baseCode);
                if (isolated == baseCode || asset.characterLookupTable.ContainsKey(isolated)) continue;
                if (!asset.characterLookupTable.TryGetValue(baseCode, out var baseChar)) continue;
                var alias = new TMP_Character(isolated, baseChar.glyph) { scale = baseChar.scale };
                asset.characterTable.Add(alias);
                aliased++;
            }
            asset.ReadFontAssetDefinition();
            Debug.Log($"[Wild Tamers] {asset.name}: aliased {aliased} isolated forms to base letters.");
        }

        private static void AddFallback(string fredokaPath, TMP_FontAsset arabic)
        {
            var fredoka = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fredokaPath);
            if (fredoka == null || arabic == null) return;
            if (fredoka.fallbackFontAssetTable == null) fredoka.fallbackFontAssetTable = new List<TMP_FontAsset>();
            fredoka.fallbackFontAssetTable.RemoveAll(f => f == null || f.name.StartsWith("Tajawal"));
            fredoka.fallbackFontAssetTable.Add(arabic);
            EditorUtility.SetDirty(fredoka);
        }
    }
}
