using TMPro;
using UnityEngine;

namespace WildTamers.Lang
{
    /// <summary>
    /// Which TextMeshPro font each language uses: the rounded Fredoka fonts for English, Tajawal for Arabic.
    /// Lives in Resources; <see cref="LocalizedText"/> asks it for the font that matches the text's original (English) font.
    /// </summary>
    [CreateAssetMenu(menuName = "Wild Tamers/Localization Fonts", fileName = "LocalizationFonts")]
    public class LocalizationFonts : ScriptableObject
    {
        public const string ResourcePath = "LocalizationFonts";

        public TMP_FontAsset latinMedium;
        public TMP_FontAsset latinBold;
        public TMP_FontAsset arabicMedium;
        public TMP_FontAsset arabicBold;
        [Tooltip("Outlined damage-number material for each font (null = plain material).")]
        public Material latinBoldOutline;
        public Material arabicBoldOutline;

        private static LocalizationFonts instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        public static LocalizationFonts Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<LocalizationFonts>(ResourcePath);
                return instance;
            }
        }

        /// <summary>The font to use for <paramref name="original"/> (a Latin font) in the current direction.</summary>
        public TMP_FontAsset Resolve(TMP_FontAsset original, bool arabic)
        {
            if (original == null) return null;
            bool bold = original == latinBold || original == arabicBold;
            bool known = bold || original == latinMedium || original == arabicMedium;
            if (!known) return original;
            if (arabic) return bold ? arabicBold : arabicMedium;
            return bold ? latinBold : latinMedium;
        }

        /// <summary>The outline material that goes with the bold font, or null when <paramref name="original"/> is not the outline material.</summary>
        public Material ResolveOutline(Material original, bool arabic)
        {
            if (original == null) return null;
            if (original == latinBoldOutline || original == arabicBoldOutline) return arabic ? arabicBoldOutline : latinBoldOutline;
            return null;
        }
    }
}
