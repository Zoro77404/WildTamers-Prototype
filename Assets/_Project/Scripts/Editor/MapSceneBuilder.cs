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
using WildTamers.Lang;

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
            var hud = BuildHud(canvas.transform, out var teamButtonParent, out var pauseButton);
            var teamPanel = BuildTeamPanel(canvas.transform);
            var popup = BuildEncounterPopup(canvas.transform);
            var teamSelect = BuildTeamSelect(canvas.transform);
            var animalCard = CardBuilder.BuildAnimalCard(canvas.transform);
            var confirm = MenuUiBuilder.BuildConfirmPopup(canvas.transform);
            var settings = MenuUiBuilder.BuildSettingsPanel(canvas.transform, confirm, null, stayInSceneOnReset: false);
            var pause = MenuUiBuilder.BuildPauseMenu(canvas.transform, pauseButton, settings, confirm, inBattle: false);
            var toast = MenuUiBuilder.BuildToast(canvas.transform);
            UIBuild.Set(hud, "teamPanel", teamPanel);
            UIBuild.Set(teamPanel, "animalCard", animalCard);
            UIBuild.Set(teamSelect, "infoCard", animalCard);

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
            UIBuild.Set(controller, "animalCard", animalCard);
            UIBuild.Set(controller, "teamSelect", teamSelect);
            UIBuild.Set(controller, "encounterPopup", popup);
            UIBuild.Set(controller, "toast", toast);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Wild Tamers] MapScene built.");
        }

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------

        private static MapHUD BuildHud(Transform canvas, out Transform teamButtonParent, out Button pauseButton)
        {
            var root = UIBuild.Stretch("HUD", canvas);
            var hud = root.gameObject.AddComponent<MapHUD>();
            teamButtonParent = root;
            pauseButton = MenuUiBuilder.BuildPauseButton(root);

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
            UIBuild.Key(teamLabel, "hud.team");
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
            UIBuild.Key(UIBuild.Text(UIBuild.Stretch("Text", hint, 24f, 0f, 24f, 0f),
                "WASD or click to walk  •  Scroll to zoom  •  Q/E to turn", 30f, Color.white), "hud.hint");

            root.gameObject.AddComponent<RTLMirror>();
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
            UIBuild.Key(title, "encounter.title");

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
                "Leave", Palette.Neutral, Palette.NeutralDark, 48f, 54f, out var leaveLabel, out _);
            UIBuild.Key(leaveLabel, "encounter.leave");
            var fight = UIBuild.CandyButton("FightButton", body, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(14f, 50f), new Vector2(390f, 150f),
                "Fight!", Palette.Primary, Palette.PrimaryDark, 48f, 58f, out var fightLabel, out _);
            UIBuild.Key(fightLabel, "encounter.fight");

            root.gameObject.AddComponent<RTLMirror>();
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

        public static TeamPanel BuildTeamPanel(Transform canvas)
        {
            var root = UIBuild.Stretch("TeamPanel", canvas);
            root.gameObject.AddComponent<CanvasGroup>();
            var backdrop = UIBuild.Backdrop(root, Palette.Backdrop);

            var card = UIBuild.Rect("Card", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(980f, 1580f));
            var body = UIBuild.Stretch("Body", card);
            UIBuild.Round(body, Palette.Panel, 60f, raycast: true);
            UIBuild.DropShadow(body, 40f, -18f, 0.3f);

            UIBuild.Key(UIBuild.Label("Title", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, -44f), new Vector2(640f, 86f),
                "Your Team", 68f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true), "team.title");
            var count = UIBuild.Label("Count", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, -128f), new Vector2(500f, 48f),
                "1 animal", 36f, Palette.Muted, TextAlignmentOptions.MidlineLeft);

            var close = UIBuild.CandyButton("CloseButton", body, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(112f, 120f),
                "", Palette.Neutral, Palette.NeutralDark, 56f, 10f, out _, out var closeFace);
            var x = UIBuild.Rect("Icon", closeFace.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
            var xImg = x.gameObject.AddComponent<Image>();
            xImg.sprite = UISpriteGenerator.Load(UISpriteGenerator.Close);
            xImg.raycastTarget = false;

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
            UIBuild.Key(UIBuild.Text(UIBuild.Stretch("Text", footer, 20f, 0f, 20f, 0f), "Tap an animal to read its story.\nYou pick your fighters before every battle.", 34f, Palette.Muted,
                TextAlignmentOptions.Center, wrap: true), "team.footer");
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
            root.gameObject.AddComponent<RTLMirror>();
            root.gameObject.SetActive(false);
            return panel;
        }

        // ------------------------------------------------------------------
        // Team select (before every fight)
        // ------------------------------------------------------------------

        private static TeamSelectScreen BuildTeamSelect(Transform canvas)
        {
            var root = UIBuild.Stretch("TeamSelectScreen", canvas);
            root.gameObject.AddComponent<CanvasGroup>();
            var bg = UIBuild.Stretch("Background", root);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = UISpriteGenerator.Load(UISpriteGenerator.StarterBackground);
            bgImg.raycastTarget = true;
            // Soft bubbles for a friendly backdrop.
            var bubbles = new[] { (new Vector2(-400f, 780f), 520f), (new Vector2(440f, 380f), 380f), (new Vector2(-470f, -560f), 420f), (new Vector2(480f, -820f), 560f) };
            foreach (var (pos, size) in bubbles)
            {
                var b = UIBuild.Rect("Bubble", bg, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
                UIBuild.Disc(b, new Color(1f, 1f, 1f, 0.16f));
            }

            var content = UIBuild.Stretch("Content", root);

            UIBuild.Key(UIBuild.Label("Title", content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -76f), new Vector2(1000f, 104f),
                "Choose your team", 78f, Palette.Ink, TextAlignmentOptions.Center, bold: true), "select.title");
            var subtitle = UIBuild.Label("Subtitle", content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(980f, 56f),
                "Wild Camel  •  Lv. 5", 42f, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.85f), TextAlignmentOptions.Center, bold: true);
            var note = UIBuild.Label("Note", content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(980f, 48f),
                "Pick 3 animals for this fight.", 34f, new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.7f), TextAlignmentOptions.Center);

            // The three team slots.
            var slotBackdrops = new Image[3];
            var slotPortraits = new AnimalPreviewImage[3];
            var slotEmpty = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var slot = UIBuild.Rect("Slot " + (i + 1), content, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - 1) * 270f, -318f), new Vector2(200f, 200f));
                                slotBackdrops[i] = UIBuild.Disc(slot, Palette.Line);
                var portraitRt = UIBuild.Stretch("Portrait", slot, -6f, -6f, -6f, -6f);
                portraitRt.gameObject.AddComponent<RawImage>().raycastTarget = false;
                slotPortraits[i] = portraitRt.gameObject.AddComponent<AnimalPreviewImage>();
                UIBuild.SetBool(slotPortraits[i], "live", false);
                var empty = UIBuild.Text(UIBuild.Stretch("Empty", slot), (i + 1).ToString(), 80f, new Color(1f, 1f, 1f, 0.95f), TextAlignmentOptions.Center, bold: true);
                slotEmpty[i] = empty.gameObject;
            }

            // List of animals.
            var listCard = UIBuild.Stretch("ListCard", content, 50f, 540f, 50f, 262f);
            UIBuild.DropShadow(listCard, 26f, -10f, 0.18f);
            UIBuild.Round(listCard, new Color(1f, 1f, 1f, 0.97f), 52f, raycast: true);
            var scrollRt = UIBuild.Stretch("Scroll", listCard, 20f, 20f, 20f, 20f);
            var scroll = scrollRt.gameObject.AddComponent<ScrollRect>();
            var viewport = UIBuild.Stretch("Viewport", scrollRt);
            viewport.gameObject.AddComponent<RectMask2D>();
            UIBuild.Plain(viewport, new Color(1f, 1f, 1f, 0f), raycast: true);
            var list = UIBuild.Rect("Content", viewport, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.offsetMin = list.offsetMax = Vector2.zero;
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(0, 0, 6, 12);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = list;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            // Buttons.
            var back = UIBuild.CandyButton("BackButton", content, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 70f), new Vector2(440f, 150f),
                "Back", Palette.Neutral, Palette.NeutralDark, 48f, 54f, out var backLabel, out _);
            UIBuild.Key(backLabel, "common.back");
            UIBuild.DropShadow((RectTransform)back.transform, 24f, -10f, 0.2f);
            var fight = UIBuild.CandyButton("FightButton", content, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(14f, 70f), new Vector2(440f, 150f),
                "Fight!", Palette.Primary, Palette.PrimaryDark, 48f, 58f, out var fightLabel, out _);
            UIBuild.DropShadow((RectTransform)fight.transform, 24f, -10f, 0.2f);

            root.gameObject.AddComponent<RTLMirror>();
            var screen = root.gameObject.AddComponent<TeamSelectScreen>();
            UIBuild.Set(screen, "card", content);
            UIBuild.Set(screen, "listContent", list);
            UIBuild.Set(screen, "rowPrefab", AssetDatabase.LoadAssetAtPath<TeamPickRow>(PrefabBuilder.TeamPickRowPrefab));
            UIBuild.Set(screen, "scroll", scroll);
            UIBuild.Set(screen, "subtitleText", subtitle);
            UIBuild.Set(screen, "noteText", note);
            UIBuild.Set(screen, "fightButton", fight);
            UIBuild.Set(screen, "fightLabel", fightLabel);
            UIBuild.Set(screen, "backButton", back);
            UIBuild.SetArray(screen, "slotPortraits", slotPortraits);
            UIBuild.SetArray(screen, "slotBackdrops", slotBackdrops);
            UIBuild.SetArray(screen, "slotEmpty", slotEmpty);
            root.gameObject.SetActive(false);
            return screen;
        }
    }
}
