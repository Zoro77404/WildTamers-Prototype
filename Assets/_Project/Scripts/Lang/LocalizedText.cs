using RTLTMPro;
using TMPro;
using UnityEngine;

namespace WildTamers.Lang
{
    /// <summary>
    /// Put on every TextMeshPro text. It swaps the font for Arabic, turns on the Arabic letter joining / right-to-left shaping
    /// (RTLTMPro) and mirrors the alignment (left ↔ right) whenever the language changes. If a <see cref="key"/> is set,
    /// it also fills the text from that string table entry; texts that code fills (names, numbers, log lines) leave it empty.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Entry in the UI table. Leave empty when a script sets the text.")]
        [SerializeField] private string key;
        [Tooltip("Flip left/right alignment in Arabic.")]
        [SerializeField] private bool mirrorAlignment = true;

        private TMP_Text text;
        private RTLTextMeshPro rtl;
        private TMP_FontAsset latinFont;
        private Material latinMaterial;
        private TextAlignmentOptions latinAlignment;
        private bool captured;

        public string Key => key;

        private void Awake() => Capture();

        private void OnEnable()
        {
            Capture();
            Loc.LanguageChanged += Apply;
            Apply();
        }

        private void OnDisable() => Loc.LanguageChanged -= Apply;

        /// <summary>Points this text at another table entry (and shows it).</summary>
        public void SetKey(string newKey)
        {
            key = newKey;
            Apply();
        }

        private void Capture()
        {
            if (captured) return;
            captured = true;
            text = GetComponent<TMP_Text>();
            rtl = text as RTLTextMeshPro;
            latinFont = text.font;
            latinAlignment = text.alignment;
            var fonts = LocalizationFonts.Instance;
            // The outline material is only used by the floating damage numbers.
            if (fonts != null && text.fontSharedMaterial != null && fonts.ResolveOutline(text.fontSharedMaterial, false) != null)
                latinMaterial = text.fontSharedMaterial;
        }

        /// <summary>Mirrors left/right in a TextMeshPro alignment (vertical part and Center stay).</summary>
        public static TextAlignmentOptions Mirror(TextAlignmentOptions a)
        {
            int v = (int)a;
            int left = v & (int)HorizontalAlignmentOptions.Left;
            int right = v & (int)HorizontalAlignmentOptions.Right;
            v &= ~((int)HorizontalAlignmentOptions.Left | (int)HorizontalAlignmentOptions.Right);
            if (left != 0) v |= (int)HorizontalAlignmentOptions.Right;
            if (right != 0) v |= (int)HorizontalAlignmentOptions.Left;
            return (TextAlignmentOptions)v;
        }

        private void Apply()
        {
            Capture();
            bool arabic = Loc.IsRTL;
            var fonts = LocalizationFonts.Instance;
            if (fonts != null)
            {
                var font = fonts.Resolve(latinFont, arabic);
                if (font != null && text.font != font) text.font = font;
                if (latinMaterial != null)
                {
                    var mat = fonts.ResolveOutline(latinMaterial, arabic);
                    if (mat != null && text.fontSharedMaterial != mat) text.fontSharedMaterial = mat;
                }
            }
            text.alignment = arabic && mirrorAlignment ? Mirror(latinAlignment) : latinAlignment;

            if (rtl != null)
            {
                rtl.Farsi = false;
                rtl.PreserveNumbers = false;
                rtl.ForceFix = arabic;
            }
            if (!string.IsNullOrEmpty(key)) text.text = Loc.T(key);
            if (rtl != null) rtl.UpdateText();
        }
    }
}
