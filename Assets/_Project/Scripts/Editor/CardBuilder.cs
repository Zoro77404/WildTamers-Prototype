using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.UI;
using WildTamers.Lang;

namespace WildTamers.EditorTools
{
    /// <summary>Builds the animal info card ("New animal!" / "Animal info"); both the map and the battle scene use it.</summary>
    public static class CardBuilder
    {
        public static AnimalCardPanel BuildAnimalCard(Transform canvas)
        {
            var root = UIBuild.Stretch("AnimalCard", canvas);
            root.gameObject.AddComponent<CanvasGroup>();
            var backdrop = UIBuild.Backdrop(root, Palette.Backdrop);

            var card = UIBuild.Rect("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(960f, 1650f));
            var body = UIBuild.Stretch("Body", card);
            UIBuild.Round(body, Palette.Panel, 60f, raycast: true);
            UIBuild.DropShadow(body, 40f, -18f, 0.3f);

            // Header pill ("NEW ANIMAL!").
            var header = UIBuild.Rect("Header", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(560f, 96f));
            UIBuild.DropShadow(header, 18f, -8f, 0.2f);
            var headerPill = UIBuild.Round(header, Palette.Primary, 48f);
            var headerText = UIBuild.Text(UIBuild.Stretch("Text", header), "NEW ANIMAL!", 54f, Color.white, TextAlignmentOptions.Center, bold: true);

            // Picture.
            var previewBg = UIBuild.Rect("PreviewBackdrop", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(860f, 560f));
            var previewBgImg = UIBuild.Round(previewBg, Palette.PanelAlt, 46f);
            var previewRt = UIBuild.Rect("Preview", previewBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(650f, 650f));
            previewRt.gameObject.AddComponent<RawImage>().raycastTarget = false;
            var preview = previewRt.gameObject.AddComponent<AnimalPreviewImage>();
            UIBuild.SetBool(preview, "live", true);
            var pso = new SerializedObject(preview);
            pso.FindProperty("resolution").intValue = 768;
            pso.ApplyModifiedPropertiesWithoutUndo();

            var levelPill = UIBuild.Rect("LevelPill", previewBg, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(160f, 58f));
            UIBuild.Round(levelPill, Palette.Teal, 29f);
            var levelText = UIBuild.Text(UIBuild.Stretch("Text", levelPill), "Lv. 5", 34f, Color.white, TextAlignmentOptions.Center, bold: true);

            // Twinkling stars around the picture (only on a new animal).
            var star = UISpriteGenerator.Load(UISpriteGenerator.Star);
            var spots = new[]
            {
                (new Vector2(-440f, -150f), 76f), (new Vector2(440f, -190f), 62f), (new Vector2(-470f, -480f), 54f),
                (new Vector2(470f, -510f), 80f), (new Vector2(-300f, -40f), 48f), (new Vector2(330f, -30f), 56f),
            };
            var sparkles = new RectTransform[spots.Length];
            for (int i = 0; i < spots.Length; i++)
            {
                var rt = UIBuild.Rect("Sparkle " + (i + 1), body, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), spots[i].Item1 + new Vector2(0f, -140f), Vector2.one * spots[i].Item2);
                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = star;
                img.color = MaterialLibrary.Hex("#FFC93C");
                img.preserveAspect = true;
                img.raycastTarget = false;
                sparkles[i] = rt;
            }

            var name = UIBuild.Label("Name", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -716f), new Vector2(860f, 100f),
                "Arabian Horse", 80f, Palette.Ink, TextAlignmentOptions.Center, bold: true);
            name.enableAutoSizing = true;
            name.fontSizeMin = 50f;
            name.fontSizeMax = 80f;
            var style = UIBuild.Label("Style", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -814f), new Vector2(860f, 50f),
                "Fast & enduring", 38f, Palette.Muted, TextAlignmentOptions.Center);

            // Text blocks.
            Heading(body, "ABOUT", -884f, "card.about");
            var description = Paragraph(body, "Description", -930f, 210f);
            Heading(body, "HISTORY", -1148f, "card.history");
            var history = Paragraph(body, "History", -1194f, 280f);

            var close = UIBuild.CandyButton("CloseButton", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(520f, 128f),
                "Awesome!", Palette.Primary, Palette.PrimaryDark, 46f, 50f, out var closeLabel, out _);
            UIBuild.DropShadow((RectTransform)close.transform, 22f, -8f, 0.2f);

            root.gameObject.AddComponent<RTLMirror>();
            var panel = root.gameObject.AddComponent<AnimalCardPanel>();
            UIBuild.Set(panel, "card", card);
            UIBuild.Set(panel, "preview", preview);
            UIBuild.Set(panel, "previewBackdrop", previewBgImg);
            UIBuild.Set(panel, "header", header);
            UIBuild.Set(panel, "headerPill", headerPill);
            UIBuild.Set(panel, "headerText", headerText);
            UIBuild.Set(panel, "nameText", name);
            UIBuild.Set(panel, "styleText", style);
            UIBuild.Set(panel, "levelText", levelText);
            UIBuild.Set(panel, "descriptionText", description);
            UIBuild.Set(panel, "historyText", history);
            UIBuild.Set(panel, "closeButton", close);
            UIBuild.Set(panel, "closeLabel", closeLabel);
            UIBuild.Set(panel, "backdropButton", backdrop);
            UIBuild.SetArray(panel, "sparkles", sparkles);
            root.gameObject.SetActive(false);
            return panel;
        }

        private static void Heading(RectTransform parent, string text, float y, string key)
        {
            var label = UIBuild.Label(text + " Heading", parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(860f, 44f),
                text, 30f, Palette.Primary, TextAlignmentOptions.MidlineLeft, bold: true);
            label.characterSpacing = 6f;
            UIBuild.Key(label, key);
        }

        private static TextMeshProUGUI Paragraph(RectTransform parent, string name, float y, float height)
        {
            var text = UIBuild.Label(name, parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(860f, height),
                "Text goes here.", 34f, Palette.Ink, TextAlignmentOptions.TopLeft, bold: false, wrap: true);
            text.enableAutoSizing = true;
            text.fontSizeMin = 24f;
            text.fontSizeMax = 34f;
            text.lineSpacing = 6f;
            return text;
        }
    }
}
