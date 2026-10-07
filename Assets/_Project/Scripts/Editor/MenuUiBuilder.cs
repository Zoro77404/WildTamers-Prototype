using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Lang;
using WildTamers.UI;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// UI pieces shared by the scenes: the toast, the "Are you sure?" popup, the settings screen, the pause button and the pause menu.
    /// The main menu, the map and the battle scene all build them from here, so they look and behave the same everywhere.
    /// </summary>
    public static class MenuUiBuilder
    {
        // ------------------------------------------------------------------
        // Toast
        // ------------------------------------------------------------------

        public static ToastMessage BuildToast(Transform canvas)
        {
            var rt = UIBuild.Rect("Toast", canvas, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(880f, 124f));
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var bg = UIBuild.Round(rt, new Color32(0x26, 0x32, 0x4A, 0xEE), 62f);
            var label = UIBuild.Text(UIBuild.Stretch("Text", rt, 30f, 0f, 30f, 0f), "Get closer!", 42f, Color.white, TextAlignmentOptions.Center, bold: true);
            var toast = rt.gameObject.AddComponent<ToastMessage>();
            UIBuild.Set(toast, "label", label);
            UIBuild.Set(toast, "background", bg);
            return toast;
        }

        // ------------------------------------------------------------------
        // Confirm popup
        // ------------------------------------------------------------------

        public static ConfirmPopup BuildConfirmPopup(Transform canvas)
        {
            var root = UIBuild.Stretch("ConfirmPopup", canvas);
            root.gameObject.AddComponent<CanvasGroup>();
            var backdrop = UIBuild.Backdrop(root, Palette.Backdrop);

            var card = UIBuild.Rect("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(900f, 620f));
            var body = UIBuild.Stretch("Body", card);
            UIBuild.Round(body, Palette.Panel, 60f, raycast: true);
            UIBuild.DropShadow(body, 40f, -18f, 0.3f);

            var title = UIBuild.Label("Title", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -52f), new Vector2(780f, 90f),
                "Reset save?", 64f, Palette.Ink, TextAlignmentOptions.Center, bold: true);
            var text = UIBuild.Label("Body", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(780f, 210f),
                "This deletes all your animals and starts a new game.", 40f, Palette.Muted, TextAlignmentOptions.Center, bold: false, wrap: true);
            text.enableAutoSizing = true;
            text.fontSizeMin = 28f;
            text.fontSizeMax = 40f;

            var cancel = UIBuild.CandyButton("CancelButton", body, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 44f), new Vector2(390f, 150f),
                "Cancel", Palette.Neutral, Palette.NeutralDark, 48f, 52f, out var cancelLabel, out _);
            var confirm = UIBuild.CandyButton("ConfirmButton", body, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(14f, 44f), new Vector2(390f, 150f),
                "Yes", Palette.Danger, MaterialLibrary.Hex("#C23356"), 48f, 52f, out var confirmLabel, out _);

            root.gameObject.AddComponent<RTLMirror>();
            var popup = root.gameObject.AddComponent<ConfirmPopup>();
            UIBuild.Set(popup, "card", card);
            UIBuild.Set(popup, "titleText", title);
            UIBuild.Set(popup, "bodyText", text);
            UIBuild.Set(popup, "confirmLabel", confirmLabel);
            UIBuild.Set(popup, "cancelLabel", cancelLabel);
            UIBuild.Set(popup, "confirmButton", confirm);
            UIBuild.Set(popup, "cancelButton", cancel);
            UIBuild.Set(popup, "backdropButton", backdrop);
            root.gameObject.SetActive(false);
            return popup;
        }

        // ------------------------------------------------------------------
        // Settings
        // ------------------------------------------------------------------

        public static SettingsPanel BuildSettingsPanel(Transform canvas, ConfirmPopup confirm, ToastMessage toast, bool stayInSceneOnReset)
        {
            var root = UIBuild.Stretch("SettingsPanel", canvas);
            root.gameObject.AddComponent<CanvasGroup>();
            var backdrop = UIBuild.Backdrop(root, Palette.Backdrop);

            var card = UIBuild.Rect("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(980f, 1290f));
            var body = UIBuild.Stretch("Body", card);
            UIBuild.Round(body, Palette.Panel, 60f, raycast: true);
            UIBuild.DropShadow(body, 40f, -18f, 0.3f);

            UIBuild.Key(UIBuild.Label("Title", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -44f), new Vector2(660f, 96f),
                "Settings", 70f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true), "settings.title");

            var close = UIBuild.CandyButton("CloseButton", body, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(112f, 120f),
                "", Palette.Neutral, Palette.NeutralDark, 56f, 10f, out _, out var closeFace);
            var x = UIBuild.Rect("Icon", closeFace.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
            var xImg = x.gameObject.AddComponent<Image>();
            xImg.sprite = UISpriteGenerator.Load(UISpriteGenerator.Close);
            xImg.raycastTarget = false;

            // ---- Language ----
            SectionLabel(body, "settings.language", -190f);
            var english = UIBuild.CandyButton("EnglishButton", body, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -256f), new Vector2(420f, 140f),
                "English", Palette.Teal, Palette.TealDark, 50f, 54f, out var englishLabel, out var englishFace);
            UIBuild.Key(englishLabel, "settings.lang.en");
            var arabic = UIBuild.CandyButton("ArabicButton", body, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(14f, -256f), new Vector2(420f, 140f),
                "العربية", Palette.Teal, Palette.TealDark, 50f, 58f, out var arabicLabel, out var arabicFace);
            UIBuild.Key(arabicLabel, "settings.lang.ar");
            // The language buttons keep their place: each one always shows its own language on its own side.
            english.gameObject.AddComponent<RTLMirrorIgnore>();
            arabic.gameObject.AddComponent<RTLMirrorIgnore>();

            // ---- Volumes ----
            var musicSlider = VolumeRow(body, "settings.music", -470f, "Music", out var musicValue);
            var sfxSlider = VolumeRow(body, "settings.sfx", -690f, "Sfx", out var sfxValue);

            // ---- Reset save ----
            var reset = UIBuild.CandyButton("ResetButton", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(620f, 120f),
                "Reset save", Palette.Danger, MaterialLibrary.Hex("#C23356"), 44f, 46f, out var resetLabel, out _);
            UIBuild.Key(resetLabel, "settings.reset");

            var done = UIBuild.CandyButton("DoneButton", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(620f, 150f),
                "Done", Palette.Primary, Palette.PrimaryDark, 48f, 56f, out var doneLabel, out _);
            UIBuild.Key(doneLabel, "common.done");
            UIBuild.DropShadow((RectTransform)done.transform, 22f, -8f, 0.2f);

            root.gameObject.AddComponent<RTLMirror>();
            var panel = root.gameObject.AddComponent<SettingsPanel>();
            UIBuild.Set(panel, "card", card);
            UIBuild.Set(panel, "englishButton", english);
            UIBuild.Set(panel, "arabicButton", arabic);
            UIBuild.Set(panel, "englishFace", englishFace);
            UIBuild.Set(panel, "arabicFace", arabicFace);
            UIBuild.Set(panel, "musicSlider", musicSlider);
            UIBuild.Set(panel, "sfxSlider", sfxSlider);
            UIBuild.Set(panel, "musicValue", musicValue);
            UIBuild.Set(panel, "sfxValue", sfxValue);
            UIBuild.Set(panel, "resetButton", reset);
            UIBuild.Set(panel, "doneButton", done);
            UIBuild.Set(panel, "closeButton", close);
            UIBuild.Set(panel, "backdropButton", backdrop);
            UIBuild.Set(panel, "confirm", confirm);
            UIBuild.Set(panel, "toast", toast);
            UIBuild.SetBool(panel, "stayInSceneOnReset", stayInSceneOnReset);
            root.gameObject.SetActive(false);
            return panel;
        }

        private static void SectionLabel(RectTransform body, string key, float y)
        {
            var label = UIBuild.Label("Section " + key, body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(860f, 56f),
                key, 40f, Palette.Muted, TextAlignmentOptions.MidlineLeft, bold: true);
            UIBuild.Key(label, key);
        }

        /// <summary>Label on the left, percentage on the right and a slider underneath.</summary>
        private static Slider VolumeRow(RectTransform body, string labelKey, float y, string name, out TMP_Text valueText)
        {
            SectionLabel(body, labelKey, y);
            valueText = UIBuild.Label(name + " Value", body, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, y), new Vector2(220f, 56f),
                "70%", 40f, Palette.Ink, TextAlignmentOptions.MidlineRight, bold: true);

            var sliderRt = UIBuild.Rect(name + " Slider", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 90f), new Vector2(860f, 72f));
            // The whole slider area takes taps (the bar itself is thin).
            UIBuild.Plain(sliderRt, new Color(1f, 1f, 1f, 0f), raycast: true);
            var track = UIBuild.Stretch("Background", sliderRt, 0f, 22f, 0f, 22f);
            UIBuild.Round(track, Palette.Line, 14f);
            track.gameObject.AddComponent<RTLMirrorIgnore>();

            var fillArea = UIBuild.Stretch("Fill Area", sliderRt, 0f, 22f, 0f, 22f);
            fillArea.gameObject.AddComponent<RTLMirrorIgnore>();
            var fill = UIBuild.Rect("Fill", fillArea, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 0f));
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(0f, 1f);
            fill.sizeDelta = new Vector2(10f, 0f);
            UIBuild.Round(fill, Palette.Teal, 14f);

            var handleArea = UIBuild.Stretch("Handle Slide Area", sliderRt, 36f, 0f, 36f, 0f);
            handleArea.gameObject.AddComponent<RTLMirrorIgnore>();
            // The handle is a plain rect that the slider moves; its look (soft shadow, white disc, teal dot) hangs below it.
            var handle = UIBuild.Rect("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
            var shadow = UIBuild.Rect("Shadow", handle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -5f), new Vector2(84f, 84f));
            UIBuild.Disc(shadow, new Color(0.08f, 0.12f, 0.2f, 0.18f));
            var faceRt = UIBuild.Stretch("Face", handle);
            var handleImg = UIBuild.Disc(faceRt, Color.white, raycast: true);
            var ring = UIBuild.Rect("Ring", handle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
            UIBuild.Disc(ring, Palette.Teal);

            var slider = sliderRt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.7f;
            slider.transition = Selectable.Transition.None;
            var nav = slider.navigation;
            nav.mode = Navigation.Mode.None;
            slider.navigation = nav;
            return slider;
        }

        // ------------------------------------------------------------------
        // Pause button + pause menu
        // ------------------------------------------------------------------

        /// <summary>Small round pause button in a corner (top-right; it moves to the top-left in Arabic with the rest of a mirrored HUD).</summary>
        public static Button BuildPauseButton(Transform parent)
        {
            var rt = UIBuild.Rect("PauseButton", parent, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -52f), new Vector2(104f, 104f));
            UIBuild.DropShadow(rt, 16f, -6f, 0.22f);
            var face = UIBuild.Disc(rt, new Color(1f, 1f, 1f, 0.96f), raycast: true);
            var icon = UIBuild.Stretch("Icon", rt, 28f, 28f, 28f, 28f);
            var img = icon.gameObject.AddComponent<Image>();
            if (UISpriteGenerator.Load(UISpriteGenerator.Pause) == null) UISpriteGenerator.GenerateMenuIcons();
            img.sprite = UISpriteGenerator.Load(UISpriteGenerator.Pause);
            img.color = Palette.Ink;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            var colors = button.colors;
            colors.pressedColor = new Color(0.88f, 0.92f, 0.96f, 1f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            rt.gameObject.AddComponent<ButtonJuice>();
            return button;
        }

        public static PauseMenu BuildPauseMenu(Transform canvas, Button pauseButton, SettingsPanel settings, ConfirmPopup confirm, bool inBattle)
        {
            var root = UIBuild.Stretch("PauseMenu", canvas);
            root.gameObject.AddComponent<CanvasGroup>();
            var backdrop = UIBuild.Backdrop(root, Palette.Backdrop);

            var card = UIBuild.Rect("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(900f, 880f));
            var body = UIBuild.Stretch("Body", card);
            UIBuild.Round(body, Palette.Panel, 60f, raycast: true);
            UIBuild.DropShadow(body, 40f, -18f, 0.3f);

            UIBuild.Key(UIBuild.Label("Title", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(780f, 100f),
                "Paused", 74f, Palette.Ink, TextAlignmentOptions.Center, bold: true), "pause.title");

            var resume = UIBuild.CandyButton("ResumeButton", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(720f, 170f),
                "Resume", Palette.Primary, Palette.PrimaryDark, 56f, 64f, out var resumeLabel, out _);
            UIBuild.Key(resumeLabel, "pause.resume");
            var settingsButton = UIBuild.CandyButton("SettingsButton", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(720f, 150f),
                "Settings", Palette.Teal, Palette.TealDark, 50f, 56f, out var settingsLabel, out _);
            UIBuild.Key(settingsLabel, "pause.settings");
            var mainMenu = UIBuild.CandyButton("MainMenuButton", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -610f), new Vector2(720f, 150f),
                "Main Menu", Palette.Neutral, Palette.NeutralDark, 50f, 56f, out var mainMenuLabel, out _);
            UIBuild.Key(mainMenuLabel, "pause.mainmenu");

            root.gameObject.AddComponent<RTLMirror>();
            var menu = root.gameObject.AddComponent<PauseMenu>();
            UIBuild.Set(menu, "card", card);
            var opener = pauseButton.gameObject.AddComponent<PauseButton>();
            UIBuild.Set(opener, "menu", menu);
            UIBuild.Set(menu, "resumeButton", resume);
            UIBuild.Set(menu, "settingsButton", settingsButton);
            UIBuild.Set(menu, "mainMenuButton", mainMenu);
            UIBuild.Set(menu, "backdropButton", backdrop);
            UIBuild.Set(menu, "settings", settings);
            UIBuild.Set(menu, "confirm", confirm);
            UIBuild.SetBool(menu, "inBattle", inBattle);
            root.gameObject.SetActive(false);
            return menu;
        }
    }
}
