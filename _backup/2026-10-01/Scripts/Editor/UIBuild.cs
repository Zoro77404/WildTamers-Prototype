using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.UI;

namespace WildTamers.EditorTools
{
    /// <summary>Shared palette for every screen.</summary>
    public static class Palette
    {
        public static readonly Color Ink = MaterialLibrary.Hex("#2B3A55");
        public static readonly Color Muted = MaterialLibrary.Hex("#7A869A");
        public static readonly Color Panel = Color.white;
        public static readonly Color PanelAlt = MaterialLibrary.Hex("#F3F6FA");
        public static readonly Color Line = MaterialLibrary.Hex("#E3E9F1");
        public static readonly Color Primary = MaterialLibrary.Hex("#FF8A3D");
        public static readonly Color PrimaryDark = MaterialLibrary.Hex("#D9661C");
        public static readonly Color Teal = MaterialLibrary.Hex("#2EC4B6");
        public static readonly Color TealDark = MaterialLibrary.Hex("#1E9C90");
        public static readonly Color Neutral = MaterialLibrary.Hex("#8FA3BF");
        public static readonly Color NeutralDark = MaterialLibrary.Hex("#6B7F9C");
        public static readonly Color Danger = MaterialLibrary.Hex("#EF476F");
        public static readonly Color Backdrop = new Color(0.106f, 0.149f, 0.22f, 0.55f);
        public static readonly Color Hp = MaterialLibrary.Hex("#FF6B6B");
        public static readonly Color HpGood = MaterialLibrary.Hex("#5BD16A");
        public static readonly Color Atk = MaterialLibrary.Hex("#FF9F43");
        public static readonly Color Def = MaterialLibrary.Hex("#4DA3FF");
        public static readonly Color Spd = MaterialLibrary.Hex("#2ED3A0");
    }

    /// <summary>Small helpers for building uGUI hierarchies from editor code.</summary>
    public static class UIBuild
    {
        public static TMP_FontAsset Font { get; private set; }
        public static TMP_FontAsset FontBold { get; private set; }
        public static Sprite Rounded { get; private set; }
        public static Sprite Circle { get; private set; }
        public static Sprite ShadowSprite { get; private set; }

        public static void LoadAssets()
        {
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Fredoka-SemiBold SDF.asset");
            FontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Fredoka-Bold SDF.asset");
            Rounded = UISpriteGenerator.Load(UISpriteGenerator.RoundedRect);
            Circle = UISpriteGenerator.Load(UISpriteGenerator.Circle);
            ShadowSprite = UISpriteGenerator.Load(UISpriteGenerator.Shadow);
        }

        // ---------- Rects ----------

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(string name, Transform parent, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        // ---------- Graphics ----------

        /// <summary>Rounded rectangle image; radius in reference pixels.</summary>
        public static Image Round(RectTransform rt, Color color, float radius, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UISpriteGenerator.RoundedRadius / Mathf.Max(1f, radius);
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Disc(RectTransform rt, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Circle;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static Image Plain(RectTransform rt, Color color, bool raycast = false)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Soft drop shadow placed behind <paramref name="target"/> (as its previous sibling).</summary>
        public static Image DropShadow(RectTransform target, float spread = 34f, float offsetY = -14f, float alpha = 0.22f)
        {
            var parent = target.parent;
            var go = new GameObject(target.name + " Shadow", typeof(RectTransform));
            go.layer = target.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.SetSiblingIndex(target.GetSiblingIndex());
            rt.anchorMin = target.anchorMin;
            rt.anchorMax = target.anchorMax;
            rt.pivot = target.pivot;
            rt.anchoredPosition = target.anchoredPosition + new Vector2(0f, offsetY);
            rt.sizeDelta = target.sizeDelta + Vector2.one * spread * 2f;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ShadowSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.08f, 0.12f, 0.2f, alpha);
            img.raycastTarget = false;
            return img;
        }

        // ---------- Text ----------

        public static TextMeshProUGUI Text(RectTransform rt, string text, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false, bool wrap = false)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = bold && FontBold != null ? FontBold : Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            // Overflow (not Ellipsis): TMP hides a whole line whose height exceeds the rect, which silently blanks big titles.
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static TextMeshProUGUI Label(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            string text, float fontSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool bold = false, bool wrap = false)
        {
            return Text(Rect(name, parent, anchor, pivot, pos, size), text, fontSize, color, align, bold, wrap);
        }

        // ---------- Buttons ----------

        /// <summary>
        /// Candy-style button: darker "lip" underneath, colored face on top, white label, springy press.
        /// Returns the Button; the face Image is its target graphic.
        /// </summary>
        public static Button CandyButton(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
            string label, Color face, Color lip, float radius, float fontSize, out TextMeshProUGUI text, out Image faceImage)
        {
            var root = Rect(name, parent, anchor, pivot, pos, size);
            Round(root, lip, radius, raycast: true);
            var faceRt = Stretch("Face", root, 0f, 0f, 0f, 12f);
            faceImage = Round(faceRt, face, radius);
            text = Text(Stretch("Label", faceRt, 12f, 4f, 12f, 4f), label, fontSize, Color.white, TextAlignmentOptions.Center, bold: true);

            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = faceImage;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            root.gameObject.AddComponent<ButtonJuice>();
            return button;
        }

        /// <summary>Full-screen dimmed backdrop that is also a button (tap outside to close).</summary>
        public static Button Backdrop(Transform parent, Color color)
        {
            var rt = Stretch("Backdrop", parent);
            var img = Plain(rt, color, raycast: true);
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.targetGraphic = img;
            var nav = b.navigation;
            nav.mode = Navigation.Mode.None;
            b.navigation = nav;
            return b;
        }

        // ---------- Stat bars ----------

        /// <summary>Label + rounded bar + value. Returns the StatBar component.</summary>
        public static StatBar StatRow(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, float width, float height,
            string label, Color fillColor, float labelWidth, float valueWidth, float fontSize)
        {
            var row = Rect(name, parent, anchor, pivot, pos, new Vector2(width, height));
            Label("Label", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(labelWidth, height),
                label, fontSize, Palette.Muted, TextAlignmentOptions.MidlineLeft, bold: true);
            var value = Label("Value", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(valueWidth, height),
                "0", fontSize, Palette.Ink, TextAlignmentOptions.MidlineRight, bold: true);

            float barHeight = Mathf.Max(12f, height * 0.5f);
            var track = Rect("Track", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(labelWidth, 0f),
                new Vector2(width - labelWidth - valueWidth - 14f, barHeight));
            Round(track, Palette.Line, barHeight * 0.5f);
            var fill = Stretch("Fill", track);
            fill.anchorMax = new Vector2(0.5f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
            Round(fill, fillColor, barHeight * 0.5f);

            var bar = row.gameObject.AddComponent<StatBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("valueText").objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bar;
        }

        /// <summary>Bare bar (no labels) stretched in a rect.</summary>
        public static StatBar Bar(RectTransform rt, Color trackColor, Color fillColor, TMP_Text valueText = null)
        {
            float h = rt.sizeDelta.y;
            Round(rt, trackColor, h * 0.5f);
            var fill = Stretch("Fill", rt, 4f, 4f, 4f, 4f);
            fill.anchorMax = new Vector2(1f, 1f);
            Round(fill, fillColor, (h - 8f) * 0.5f);
            // Fill is driven by anchorMax.x, so keep its insets via offsets.
            var bar = rt.gameObject.AddComponent<StatBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("fill").objectReferenceValue = fill;
            so.FindProperty("valueText").objectReferenceValue = valueText;
            so.ApplyModifiedPropertiesWithoutUndo();
            return bar;
        }

        public static void Set(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            if (p == null)
            {
                Debug.LogError($"[Wild Tamers] {target.GetType().Name} has no field '{property}'.");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string property, Object[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(property);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string property, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string property, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetColor(Object target, string property, Color value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
