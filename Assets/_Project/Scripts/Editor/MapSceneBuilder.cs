using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.Map;
using WildTamers.Player;
using WildTamers.UI;

namespace WildTamers.EditorTools
{
    /// <summary>Builds MapScene from scratch: fake map, player, camera, spawner, HUD and all popups.</summary>
    public static class MapSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MapScene.unity";

        [MenuItem("Wild Tamers/Build/Map Scene")]
        public static void Build()
        {
            SceneSetup.EnsureLayers();
            UIBuild.LoadAssets();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneSetup.ClearScene();

            // ---------- Lighting ----------
            var sky = MaterialLibrary.Hex("#CDEAF7");
            var lighting = SceneSetup.Group("--Lighting--");
            SceneSetup.Sun(lighting.transform, MaterialLibrary.Hex("#FFF3DF"), 1.25f, new Vector3(52f, -38f, 0f), 0.5f);
            SceneSetup.Ambient(MaterialLibrary.Hex("#D2E9FF"), MaterialLibrary.Hex("#E4EFD9"), MaterialLibrary.Hex("#A3B58F"),
                true, sky, 140f, 330f);
            SceneSetup.GlobalVolume(lighting.transform, "MapVolumeProfile");
            SceneSetup.ShadowSettings(170f);

            // ---------- Map ----------
            var env = SceneSetup.Group("--Environment--");
            var mapGo = new GameObject("MapView");
            mapGo.transform.SetParent(env.transform, false);
            var mapView = mapGo.AddComponent<MapView>();
            var tiles = mapGo.AddComponent<FakeMapTileProvider>();
            tiles.Materials = MaterialLibrary.BuildMapMaterials();
            EditorUtility.SetDirty(tiles);

            // ---------- Player ----------
            var playerGroup = SceneSetup.Group("--Player--");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.PlayerPrefab), playerGroup.transform);
            var avatar = player.GetComponent<PlayerAvatar>();
            var range = player.GetComponentInChildren<RangeIndicator>();
            var locationGo = new GameObject("FakeLocationProvider");
            locationGo.transform.SetParent(playerGroup.transform, false);
            var location = locationGo.AddComponent<FakeLocationProvider>();
            var marker = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.MarkerPrefab), playerGroup.transform);

            // ---------- Camera ----------
            var camGroup = SceneSetup.Group("--Camera--");
            var cam = SceneSetup.MainCamera(camGroup.transform, sky, 55f, 600f);
            var camController = cam.gameObject.AddComponent<MapCameraController>();

            // ---------- Animals ----------
            var animals = SceneSetup.Group("--Animals--");
            var spawnerGo = new GameObject("WildAnimalSpawner");
            spawnerGo.transform.SetParent(animals.transform, false);
            var spawner = spawnerGo.AddComponent<WildAnimalSpawner>();

            // ---------- Systems ----------
            var systems = SceneSetup.Group("--Systems--");
            SceneSetup.EventSystem(systems.transform);
            var inputGo = new GameObject("MapPointerInput");
            inputGo.transform.SetParent(systems.transform, false);
            var pointer = inputGo.AddComponent<MapPointerInput>();
            var controllerGo = new GameObject("MapSceneController");
            controllerGo.transform.SetParent(systems.transform, false);
            var controller = controllerGo.AddComponent<MapSceneController>();

            // ---------- UI ----------
            var uiGroup = SceneSetup.Group("--UI--");
            var canvas = SceneSetup.Canvas("MapCanvas", uiGroup.transform);
            var hud = BuildHud(canvas.transform, out var teamButtonParent);
            var teamPanel = BuildTeamPanel(canvas.transform);
            var popup = BuildEncounterPopup(canvas.transform);
            var starter = BuildStarterScreen(canvas.transform);
            var toast = BuildToast(canvas.transform);
            UIBuild.Set(hud, "teamPanel", teamPanel);

            // ---------- Wiring ----------
            UIBuild.Set(mapView, "locationProvider", location);
            UIBuild.Set(mapView, "tileProvider", tiles);
            UIBuild.Set(mapView, "focusTarget", player.transform);

            UIBuild.Set(location, "mapView", mapView);
            UIBuild.Set(location, "cameraTransform", cam.transform);

            UIBuild.Set(avatar, "locationProvider", location);
            UIBuild.Set(avatar, "mapView", mapView);
            UIBuild.Set(marker.GetComponent<DestinationMarker>(), "provider", location);

            UIBuild.Set(camController, "target", player.transform);

            UIBuild.Set(spawner, "mapView", mapView);
            UIBuild.Set(spawner, "player", player.transform);
            UIBuild.Set(spawner, "wildAnimalPrefab", AssetDatabase.LoadAssetAtPath<WildAnimal>(PrefabBuilder.WildAnimalPrefab));
            UIBuild.Set(spawner, "container", animals.transform);

            UIBuild.Set(pointer, "mapCamera", cam);
            UIBuild.Set(pointer, "mapView", mapView);

            UIBuild.Set(controller, "pointerInput", pointer);
            UIBuild.Set(controller, "fakeLocation", location);
            UIBuild.Set(controller, "player", avatar);
            UIBuild.Set(controller, "rangeIndicator", range);
            UIBuild.Set(controller, "spawner", spawner);
            UIBuild.Set(controller, "starterScreen", starter);
            UIBuild.Set(controller, "encounterPopup", popup);
            UIBuild.Set(controller, "toast", toast);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Wild Tamers] MapScene built.");
        }

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------

        private static MapHUD BuildHud(Transform canvas, out Transform teamButtonParent)
        {
            var root = UIBuild.Stretch("HUD", canvas);
            var hud = root.gameObject.AddComponent<MapHUD>();
            teamButtonParent = root;

            // Active animal chip (top-left).
            var chip = UIBuild.Rect("ActiveAnimal", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -60f), new Vector2(460f, 150f));
            var chipGroup = chip.gameObject.AddComponent<CanvasGroup>();
            chipGroup.blocksRaycasts = false;
            var chipBody = UIBuild.Stretch("Body", chip);
            UIBuild.Round(chipBody, new Color(1f, 1f, 1f, 0.96f), 75f);
            UIBuild.DropShadow(chipBody, 26f, -10f, 0.2f);
            var portraitBg = UIBuild.Rect("PortraitBackdrop", chipBody, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(15f, 0f), new Vector2(120f, 120f));
            var portraitBgImg = UIBuild.Disc(portraitBg, Palette.Line);
            var portraitRt = UIBuild.Stretch("Portrait", portraitBg, -4f, -4f, -4f, -4f);
            portraitRt.gameObject.AddComponent<RawImage>().raycastTarget = false;
            var portrait = portraitRt.gameObject.AddComponent<AnimalPreviewImage>();
            UIBuild.SetBool(portrait, "live", false);
            var name = UIBuild.Label("Name", chipBody, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(152f, -22f), new Vector2(290f, 60f),
                "Fox", 46f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);
            var level = UIBuild.Label("Level", chipBody, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(152f, -82f), new Vector2(110f, 44f),
                "Lv. 5", 34f, Palette.Muted, TextAlignmentOptions.MidlineLeft);
            var hpRt = UIBuild.Rect("HP", chipBody, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(262f, -104f), new Vector2(166f, 20f));
            var hp = UIBuild.HealthBar(hpRt);

            // Team button (bottom-center), round candy button with a paw icon.
            var teamButton = UIBuild.CandyButton("TeamButton", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(236f, 248f),
                "TEAM", Palette.Primary, Palette.PrimaryDark, 118f, 36f, out var teamLabel, out var teamFace);
            UIBuild.DropShadow((RectTransform)teamButton.transform, 30f, -12f, 0.25f);
            var labelRt = teamLabel.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.offsetMin = new Vector2(10f, 36f);
            labelRt.offsetMax = new Vector2(-10f, 82f);
            var icon = UIBuild.Rect("Icon", teamFace.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(104f, 104f));
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = UISpriteGenerator.Load(UISpriteGenerator.Paw);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            // Controls hint (bottom, above the team button).
            var hint = UIBuild.Rect("Hint", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 352f), new Vector2(940f, 80f));
            var hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintGroup.blocksRaycasts = false;
            UIBuild.Round(hint, new Color(0.15f, 0.2f, 0.29f, 0.78f), 40f);
            UIBuild.Text(UIBuild.Stretch("Text", hint, 24f, 0f, 24f, 0f),
                "WASD or click to walk  •  Scroll to zoom  •  Q/E to turn", 30f, Color.white);

            UIBuild.Set(hud, "teamButton", teamButton);
            UIBuild.Set(hud, "activeChip", chipGroup);
            UIBuild.Set(hud, "activePortrait", portrait);
            UIBuild.Set(hud, "activePortraitBackdrop", portraitBgImg);
            UIBuild.Set(hud, "activeName", name);
            UIBuild.Set(hud, "activeLevel", level);
            UIBuild.Set(hud, "activeHp", hp);
            UIBuild.Set(hud, "hint", hintGroup);
            return hud;
        }

        // ------------------------------------------------------------------
        // Encounter popup
        // ------------------------------------------------------------------

        private static EncounterPopup BuildEncounterPopup(Transform canvas)
        {
            var root = UIBuild.Stretch("EncounterPopup", canvas);
            root.gameObject.AddComponent<CanvasGroup>();
            var backdrop = UIBuild.Backdrop(root, Palette.Backdrop);

            var card = UIBuild.Rect("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(920f, 1460f));
            var body = UIBuild.Stretch("Body", card);
            UIBuild.Round(body, Palette.Panel, 60f, raycast: true);
            UIBuild.DropShadow(body, 40f, -18f, 0.3f);

            var title = UIBuild.Label("Title", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(800f, 60f),
                "Wild encounter!", 42f, Palette.Muted, TextAlignmentOptions.Center, bold: true);

            var previewBg = UIBuild.Rect("PreviewBackdrop", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -116f), new Vector2(840f, 620f));
            var previewBgImg = UIBuild.Round(previewBg, Palette.PanelAlt, 46f);
            var previewRt = UIBuild.Rect("Preview", previewBg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 620f));
            previewRt.gameObject.AddComponent<RawImage>().raycastTarget = false;
            var preview = previewRt.gameObject.AddComponent<AnimalPreviewImage>();
            var pso = new SerializedObject(preview);
            pso.FindProperty("resolution").intValue = 768;
            pso.ApplyModifiedPropertiesWithoutUndo();

            var name = UIBuild.Label("Name", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -760f), new Vector2(820f, 100f),
                "Wolf", 86f, Palette.Ink, TextAlignmentOptions.Center, bold: true);
            var levelPill = UIBuild.Rect("LevelPill", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -870f), new Vector2(180f, 64f));
            UIBuild.Round(levelPill, Palette.Teal, 32f);
            var level = UIBuild.Text(UIBuild.Stretch("Text", levelPill), "Lv. 5", 38f, Color.white, TextAlignmentOptions.Center, bold: true);
            var style = UIBuild.Label("Style", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -944f), new Vector2(820f, 50f),
                "Strong hunter", 36f, Palette.Muted, TextAlignmentOptions.Center);

            var stats = PrefabBuilder.BuildStats(body, new Vector2(80f, -1014f), 760f, 40f, 14f, 30f);

            var leave = UIBuild.CandyButton("LeaveButton", body, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 50f), new Vector2(390f, 150f),
                "Leave", Palette.Neutral, Palette.NeutralDark, 48f, 54f, out _, out _);
            var fight = UIBuild.CandyButton("FightButton", body, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(14f, 50f), new Vector2(390f, 150f),
                "Fight!", Palette.Primary, Palette.PrimaryDark, 48f, 58f, out _, out _);

            var popup = root.gameObject.AddComponent<EncounterPopup>();
            UIBuild.Set(popup, "card", card);
            UIBuild.Set(popup, "preview", preview);
            UIBuild.Set(popup, "previewBackdrop", previewBgImg);
            UIBuild.Set(popup, "titleText", title);
            UIBuild.Set(popup, "nameText", name);
            UIBuild.Set(popup, "levelText", level);
            UIBuild.Set(popup, "styleText", style);
            UIBuild.Set(popup, "stats", stats);
            UIBuild.Set(popup, "fightButton", fight);
            UIBuild.Set(popup, "leaveButton", leave);
            UIBuild.Set(popup, "backdropButton", backdrop);
            root.gameObject.SetActive(false);
            return popup;
        }

        // ------------------------------------------------------------------
        // Team panel
        // ------------------------------------------------------------------

        private static TeamPanel BuildTeamPanel(Transform canvas)
        {
            var root = UIBuild.Stretch("TeamPanel", canvas);
            root.gameObject.AddComponent<CanvasGroup>();
            var backdrop = UIBuild.Backdrop(root, Palette.Backdrop);

            var card = UIBuild.Rect("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(980f, 1580f));
            var body = UIBuild.Stretch("Body", card);
            UIBuild.Round(body, Palette.Panel, 60f, raycast: true);
            UIBuild.DropShadow(body, 40f, -18f, 0.3f);

            UIBuild.Label("Title", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -44f), new Vector2(640f, 86f),
                "Your Team", 68f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);
            var count = UIBuild.Label("Count", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, -128f), new Vector2(500f, 48f),
                "1 animal", 36f, Palette.Muted, TextAlignmentOptions.MidlineLeft);

            var close = UIBuild.CandyButton("CloseButton", body, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(112f, 120f),
                "", Palette.Neutral, Palette.NeutralDark, 56f, 10f, out _, out var closeFace);
            var x = UIBuild.Rect("Icon", closeFace.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
            var xImg = x.gameObject.AddComponent<Image>();
            xImg.sprite = UISpriteGenerator.Load(UISpriteGenerator.Close);
            xImg.raycastTarget = false;

            // Small testing helper under the close button (asks for a second tap before wiping the save).
            var reset = UIBuild.Rect("ResetSave", body, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -168f), new Vector2(330f, 52f));
            var resetHit = UIBuild.Plain(reset, new Color(1f, 1f, 1f, 0f), raycast: true);
            var resetLabel = UIBuild.Text(UIBuild.Stretch("Text", reset), "Reset save", 30f, Palette.Muted, TextAlignmentOptions.MidlineRight, bold: true);
            resetLabel.fontStyle = FontStyles.Underline;
            var resetButton = reset.gameObject.AddComponent<Button>();
            resetButton.targetGraphic = resetHit;
            resetButton.transition = Selectable.Transition.None;
            var resetNav = resetButton.navigation;
            resetNav.mode = Navigation.Mode.None;
            resetButton.navigation = resetNav;

            // Scroll list.
            var scrollRt = UIBuild.Stretch("Scroll", body, 40f, 200f, 40f, 44f);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            var viewport = UIBuild.Stretch("Viewport", scrollRt);
            viewport.gameObject.AddComponent<RectMask2D>();
            UIBuild.Plain(viewport, new Color(1f, 1f, 1f, 0f), raycast: true);
            var content = UIBuild.Rect("Content", viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 0f));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 22f;
            layout.padding = new RectOffset(0, 0, 4, 20);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var footer = UIBuild.Rect("Footer", content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(880f, 120f));
            var footerLayout = footer.gameObject.AddComponent<LayoutElement>();
            footerLayout.preferredHeight = 120f;
            UIBuild.Text(UIBuild.Stretch("Text", footer, 20f, 0f, 20f, 0f), "Tap an animal to send it into battle.\nWin fights to grow your team!", 34f, Palette.Muted,
                TextAlignmentOptions.Center, wrap: true);
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            var panel = root.gameObject.AddComponent<TeamPanel>();
            UIBuild.Set(panel, "card", card);
            UIBuild.Set(panel, "listContent", content);
            UIBuild.Set(panel, "rowPrefab", AssetDatabase.LoadAssetAtPath<TeamRow>(PrefabBuilder.TeamRowPrefab));
            UIBuild.Set(panel, "countText", count);
            UIBuild.Set(panel, "closeButton", close);
            UIBuild.Set(panel, "backdropButton", backdrop);
            UIBuild.Set(panel, "scroll", scroll);
            UIBuild.Set(panel, "footer", footer);
            UIBuild.Set(panel, "resetButton", resetButton);
            UIBuild.Set(panel, "resetLabel", resetLabel);
            root.gameObject.SetActive(false);
            return panel;
        }

        // ------------------------------------------------------------------
        // Starter screen
        // ------------------------------------------------------------------

        private static StarterSelectScreen BuildStarterScreen(Transform canvas)
        {
            var root = UIBuild.Stretch("StarterSelectScreen", canvas);
            root.gameObject.AddComponent<CanvasGroup>();
            var bg = UIBuild.Stretch("Background", root);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = UISpriteGenerator.Load(UISpriteGenerator.StarterBackground);
            bgImg.raycastTarget = true;

            // Soft bubbles for a friendly backdrop.
            var bubbles = new[] { (new Vector2(-380f, 760f), 520f), (new Vector2(430f, 420f), 380f), (new Vector2(-460f, -520f), 420f), (new Vector2(470f, -820f), 560f) };
            foreach (var (pos, size) in bubbles)
            {
                var b = UIBuild.Rect("Bubble", bg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
                UIBuild.Disc(b, new Color(1f, 1f, 1f, 0.16f));
            }

            UIBuild.Label("Title", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(1000f, 104f),
                "Choose your partner!", 78f, Palette.Ink, TextAlignmentOptions.Center, bold: true);
            UIBuild.Label("Subtitle", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -204f), new Vector2(940f, 56f),
                "Your first animal will fight by your side.", 38f, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.7f));

            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabBuilder.StarterCardPrefab);
            var cards = new StarterCard[3];
            for (int i = 0; i < 3; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, root);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -290f - i * 504f);
                go.name = $"StarterCard {i + 1}";
                cards[i] = go.GetComponent<StarterCard>();
            }

            var screen = root.gameObject.AddComponent<StarterSelectScreen>();
            UIBuild.SetArray(screen, "cards", cards);
            root.gameObject.SetActive(false);
            return screen;
        }

        // ------------------------------------------------------------------
        // Toast
        // ------------------------------------------------------------------

        private static ToastMessage BuildToast(Transform canvas)
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
    }
}
