using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using WildTamers.Lang;
using WildTamers.Map;
using WildTamers.Menu;
using WildTamers.UI;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Builds MainMenuScene: a low-poly desert (dunes, palms, rocks, a Bedouin tent, drifting clouds) with a few animals idling in it
    /// and a camera that sways slowly, plus the portrait menu on top: title, Play, Team, Settings, Quit.
    /// </summary>
    public static class MainMenuSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MainMenuScene.unity";
        private const string MatFolder = "Assets/_Project/Materials/Menu";
        private const string MeshFolder = "Assets/_Project/Art/Meshes/Menu";

        private enum M { SandLight, SandMid, SandDark, Rock, RockDark, Trunk, Palm, PalmDark, Tent, TentLight, Cloud, Cactus, Grass, Coconut }

        private static readonly string[] Colors =
        {
            "#F6DDA0", "#EDC784", "#DFAD68", "#C99A6E", "#A87855", "#9A6B4B", "#5BB35C", "#3F9A5B", "#6B4636", "#C99A6C", "#FFFFFF", "#6DBB73", "#A8DB72", "#5A3A28"
        };

        [MenuItem("Wild Tamers/Build/Main Menu Scene")]
        public static void Build()
        {
            SceneSetup.EnsureLayers();
            UIBuild.LoadAssets();
            var fonts = LocalizationBuilder.BuildFonts();
            if (UISpriteGenerator.Load(UISpriteGenerator.Pause) == null) UISpriteGenerator.GenerateMenuIcons();
            Directory.CreateDirectory(MatFolder);
            Directory.CreateDirectory(MeshFolder);

            UnityEngine.SceneManagement.Scene scene;
            if (File.Exists(ScenePath)) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            SceneSetup.ClearScene();

            var mats = BuildMaterials();
            var skyMat = BuildSky();

            // ---------- Lighting ----------
            var lighting = SceneSetup.Group("--Lighting--");
            SceneSetup.Sun(lighting.transform, MaterialLibrary.Hex("#FFE9C4"), 1.35f, new Vector3(34f, -148f, 0f), 0.6f);
            SceneSetup.Ambient(MaterialLibrary.Hex("#CFE3F7"), MaterialLibrary.Hex("#F6E7CF"), MaterialLibrary.Hex("#D9B98A"),
                true, MaterialLibrary.Hex("#F6DDB0"), 60f, 190f);
            RenderSettings.skybox = skyMat;
            SceneSetup.GlobalVolume(lighting.transform, "MenuVolumeProfile");

            // ---------- Scenery ----------
            var env = SceneSetup.Group("--Environment--");
            BuildScenery(env.transform, mats);

            // ---------- Animals ----------
            var animals = SceneSetup.Group("--Animals--");
            Spot(animals.transform, "Camel", "camel", new Vector3(-2.9f, 0f, 3.4f), 158f, 1.25f, new[] { "Eat" });
            Spot(animals.transform, "ArabianHorse", "arabian_horse", new Vector3(3.3f, 0f, 2.6f), 208f, 1.2f, new[] { "Eat" });
            Spot(animals.transform, "Saluki", "saluki", new Vector3(0.2f, 0f, -2.4f), 188f, 1.35f, new[] { "Eat", "Jump" });
            var falcon = new GameObject("Falcon");
            falcon.transform.SetParent(animals.transform, false);
            var flight = falcon.AddComponent<MenuFalconFlight>();
            var flightSo = new SerializedObject(flight);
            flightSo.FindProperty("center").vector3Value = new Vector3(0f, 6.6f, 9f);
            flightSo.FindProperty("radius").floatValue = 7.5f;
            flightSo.FindProperty("secondsPerLap").floatValue = 30f;
            flightSo.ApplyModifiedPropertiesWithoutUndo();
            var falconModel = new GameObject("Model");
            falconModel.transform.SetParent(falcon.transform, false);
            ConfigureAnimal(falconModel.AddComponent<MenuAnimal>(), "falcon", 1.1f, new string[0]);

            // ---------- Camera ----------
            var camGroup = SceneSetup.Group("--Camera--");
            var cam = SceneSetup.MainCamera(camGroup.transform, MaterialLibrary.Hex("#F6DDB0"), 38f, 400f);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.transform.position = new Vector3(0f, 4.4f, -21.5f);
            cam.transform.LookAt(new Vector3(0f, 0.6f, 2f));
            var sway = cam.gameObject.AddComponent<MenuCameraSway>();
            var so = new SerializedObject(sway);
            so.FindProperty("lookAt").vector3Value = new Vector3(0f, 0.6f, 2f);
            so.ApplyModifiedPropertiesWithoutUndo();

            // ---------- Systems ----------
            var systems = SceneSetup.Group("--Systems--");
            SceneSetup.EventSystem(systems.transform);
            var controllerGo = new GameObject("MainMenuController");
            controllerGo.transform.SetParent(systems.transform, false);
            var controller = controllerGo.AddComponent<MainMenuController>();

            // ---------- UI ----------
            var uiGroup = SceneSetup.Group("--UI--");
            var canvas = SceneSetup.Canvas("MenuCanvas", uiGroup.transform);
            var buttons = BuildMenu(canvas.transform, fonts, out var play, out var team, out var settingsButton, out var quit);
            var teamPanel = MapSceneBuilder.BuildTeamPanel(canvas.transform);
            var animalCard = CardBuilder.BuildAnimalCard(canvas.transform);
            UIBuild.Set(teamPanel, "animalCard", animalCard);
            var confirm = MenuUiBuilder.BuildConfirmPopup(canvas.transform);
            var toast = MenuUiBuilder.BuildToast(canvas.transform);
            var settings = MenuUiBuilder.BuildSettingsPanel(canvas.transform, confirm, toast, stayInSceneOnReset: true);

            UIBuild.Set(controller, "playButton", play);
            UIBuild.Set(controller, "teamButton", team);
            UIBuild.Set(controller, "settingsButton", settingsButton);
            UIBuild.Set(controller, "quitButton", quit);
            UIBuild.Set(controller, "teamPanel", teamPanel);
            UIBuild.Set(controller, "settingsPanel", settings);
            UIBuild.Set(controller, "animalCard", animalCard);
            UIBuild.Set(controller, "confirm", confirm);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Wild Tamers] MainMenuScene built.");
        }

        // ------------------------------------------------------------------
        // Materials and sky
        // ------------------------------------------------------------------

        private static Material[] BuildMaterials()
        {
            var names = System.Enum.GetNames(typeof(M));
            var mats = new Material[names.Length];
            for (int i = 0; i < names.Length; i++)
                mats[i] = MaterialLibrary.Lit($"{MatFolder}/Menu_{names[i]}.mat", MaterialLibrary.Hex(Colors[i]), 0.05f);
            return mats;
        }

        private static Material BuildSky()
        {
            const string path = MatFolder + "/Menu_Sky.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_SkyTint", new Color(0.5f, 0.5f, 0.5f));
            mat.SetColor("_GroundColor", new Color(0.93f, 0.85f, 0.66f));
            mat.SetFloat("_SunSize", 0.07f);
            mat.SetFloat("_SunSizeConvergence", 5f);
            mat.SetFloat("_AtmosphereThickness", 1.35f);
            mat.SetFloat("_Exposure", 1.4f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------
        // Scenery
        // ------------------------------------------------------------------

        private static void BuildScenery(Transform parent, Material[] mats)
        {
            var rng = new System.Random(77);
            float R() => (float)rng.NextDouble();
            var mb = new MeshBuilder(mats.Length);

            // Ground, the trodden clearing and a patch of grass under the palms.
            mb.Disc((int)M.SandLight, Vector3.zero, 170f, 64);
            mb.Disc((int)M.SandMid, new Vector3(0f, 0.03f, 1f), 12.5f, 40);
            mb.Disc((int)M.SandLight, new Vector3(0.5f, 0.05f, 1.5f), 8f, 36);
            mb.Disc((int)M.Grass, new Vector3(-8.5f, 0.05f, 9.5f), 4.2f, 24);
            mb.Disc((int)M.Grass, new Vector3(9.8f, 0.05f, 10.5f), 3.8f, 24);

            // Dunes: big soft shapes behind and beside the clearing so the horizon rolls.
            int[] shades = { (int)M.SandLight, (int)M.SandMid, (int)M.SandDark, (int)M.SandMid };
            for (int i = 0; i < 26; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * (22f + R() * 80f);
                float z = 6f + R() * 110f;
                if (i < 8) { x = (R() - 0.5f) * 150f; z = 55f + R() * 70f; }
                var scale = new Vector3(16f + R() * 26f, 3f + R() * 7f, 12f + R() * 16f);
                mb.Blob(shades[i % shades.Length], new Vector3(x, 0f, z), scale, 0.07f, rng, R() * 360f);
            }

            // Rocks around the clearing.
            var rocks = new[]
            {
                (new Vector3(-9.5f, 0f, 5.5f), 1.7f), (new Vector3(10.5f, 0f, -1.5f), 1.3f), (new Vector3(7.5f, 0f, 8.5f), 2.3f),
                (new Vector3(-12f, 0f, -1f), 1.1f), (new Vector3(-5.5f, 0f, 13f), 1.8f), (new Vector3(13f, 0f, 5f), 1.5f)
            };
            foreach (var (pos, size) in rocks)
            {
                mb.Blob((int)M.Rock, pos + Vector3.up * size * 0.35f, new Vector3(1.5f, 0.95f, 1.2f) * size, 0.2f, rng, R() * 360f);
                mb.Blob((int)M.RockDark, pos + new Vector3(size * 0.8f, size * 0.2f, size * 0.3f), new Vector3(0.9f, 0.55f, 0.8f) * size, 0.2f, rng, R() * 360f);
            }

            // Palms.
            Palm(mb, new Vector3(-8.5f, 0f, 9.5f), 1f, 8.4f, rng);
            Palm(mb, new Vector3(9.8f, 0f, 10.5f), -1f, 7.4f, rng);
            Palm(mb, new Vector3(2.5f, 0f, 19f), 0.6f, 10f, rng);
            Palm(mb, new Vector3(-17f, 0f, 14f), -0.4f, 9f, rng);

            // Cacti.
            Cactus(mb, new Vector3(-6.6f, 0f, -3f), 1.2f);
            Cactus(mb, new Vector3(7.2f, 0f, -3.6f), 0.9f);
            Cactus(mb, new Vector3(-15f, 0f, 6f), 1.6f);

            SaveAndPlace(parent, "Scenery", mb, mats, ShadowCastingMode.On);

            // A Bedouin tent (dark goat-hair cloth) at the back, as its own object so it can be turned.
            var tent = new MeshBuilder(mats.Length);
            tent.Box((int)M.Tent, new Rect(-3.6f, -2.2f, 7.2f, 4.4f), 0f, 2.1f, (int)M.Tent);
            tent.GableRoof((int)M.Tent, new Rect(-4.2f, -2.8f, 8.4f, 5.6f), 2.1f, 1.5f, (int)M.TentLight);
            foreach (float px in new[] { -4.1f, 4.1f })
                tent.Cylinder((int)M.Trunk, new Vector3(px, 0f, 2.9f), 0.1f, 2.6f, 6);
            var tentGo = SaveAndPlace(parent, "BedouinTent", tent, mats, ShadowCastingMode.On);
            tentGo.transform.SetPositionAndRotation(new Vector3(13.5f, 0f, 16f), Quaternion.Euler(0f, -148f, 0f));

            // Clouds.
            var clouds = SceneSetup.Group("Clouds");
            clouds.transform.SetParent(parent, false);
            for (int i = 0; i < 6; i++)
            {
                var cb = new MeshBuilder(mats.Length);
                for (int k = 0; k < 4; k++)
                    cb.Blob((int)M.Cloud, new Vector3((k - 1.5f) * 4.2f + R() * 1.5f, R() * 1.4f, R() * 2f), new Vector3(4.8f + R() * 2f, 1.9f + R(), 3.4f), 0.1f, rng, R() * 360f);
                var cloud = SaveAndPlace(clouds.transform, "Cloud" + (i + 1), cb, mats, ShadowCastingMode.Off);
                cloud.transform.position = new Vector3(-70f + i * 28f + R() * 8f, 26f + R() * 12f, 70f + R() * 50f);
                var drift = cloud.AddComponent<MenuDrift>();
                var so = new SerializedObject(drift);
                so.FindProperty("velocity").vector3Value = new Vector3(0.5f + R() * 0.6f, 0f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void Palm(MeshBuilder mb, Vector3 b, float lean, float height, System.Random rng)
        {
            const int segs = 8;
            float segH = height / segs;
            var p = b;
            for (int i = 0; i < segs; i++)
            {
                float r0 = Mathf.Lerp(0.46f, 0.27f, i / (float)segs);
                float r1 = Mathf.Lerp(0.46f, 0.27f, (i + 1) / (float)segs);
                mb.Cylinder((int)M.Trunk, p, r0, segH * 1.04f, 7, -1, r1);
                p += new Vector3(lean * segH * 0.07f * (1f + i * 0.25f), segH, 0f);
            }
            var crown = p;
            for (int layer = 0; layer < 2; layer++)
            {
                int count = layer == 0 ? 9 : 7;
                for (int f = 0; f < count; f++)
                {
                    float yaw = f * (360f / count) + layer * 22f + (float)rng.NextDouble() * 12f;
                    float pitch = layer == 0 ? 22f + (float)rng.NextDouble() * 14f : 6f + (float)rng.NextDouble() * 8f;
                    var rot = Quaternion.Euler(pitch, yaw, 0f);
                    float len = (layer == 0 ? 3.2f : 2.6f) + (float)rng.NextDouble() * 0.5f;
                    mb.Sphere(layer == 0 ? (int)M.Palm : (int)M.PalmDark, crown + rot * new Vector3(0f, 0f, len * 0.8f),
                        new Vector3(0.55f, 0.09f, len), 3, 6, rot);
                }
            }
            for (int c = 0; c < 3; c++)
                mb.Sphere((int)M.Coconut, crown + new Vector3((c - 1) * 0.32f, -0.35f, (c % 2) * 0.25f), Vector3.one * 0.27f, 4, 6);
        }

        private static void Cactus(MeshBuilder mb, Vector3 b, float scale)
        {
            mb.Cylinder((int)M.Cactus, b, 0.36f * scale, 2.6f * scale, 7);
            mb.Sphere((int)M.Cactus, b + Vector3.up * 2.6f * scale, new Vector3(0.36f, 0.34f, 0.36f) * scale, 3, 7);
            mb.Cylinder((int)M.Cactus, b + new Vector3(-0.34f * scale, 1.0f * scale, 0f), 0.2f * scale, 1.0f * scale, 6);
            mb.Cylinder((int)M.Cactus, b + new Vector3(-0.34f * scale, 1.0f * scale, 0f) + Vector3.left * 0.34f * scale, 0.2f * scale, 0.5f * scale, 6);
            mb.Sphere((int)M.Cactus, b + new Vector3(-0.68f * scale, 2.0f * scale, 0f), new Vector3(0.2f, 0.2f, 0.2f) * scale, 3, 6);
            mb.Cylinder((int)M.Cactus, b + new Vector3(0.34f * scale, 1.5f * scale, 0f), 0.18f * scale, 0.9f * scale, 6);
            mb.Sphere((int)M.Cactus, b + new Vector3(0.34f * scale, 2.4f * scale, 0f), new Vector3(0.18f, 0.18f, 0.18f) * scale, 3, 6);
        }

        private static GameObject SaveAndPlace(Transform parent, string name, MeshBuilder mb, Material[] mats, ShadowCastingMode shadows)
        {
            var mesh = mb.Build("Menu_" + name, out List<int> used);
            var path = $"{MeshFolder}/Menu_{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.isStatic = name == "Scenery";
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var m = new Material[used.Count];
            for (int i = 0; i < used.Count; i++) m[i] = mats[used[i]];
            mr.sharedMaterials = m;
            mr.shadowCastingMode = shadows;
            return go;
        }

        // ------------------------------------------------------------------
        // Animals
        // ------------------------------------------------------------------

        private static void Spot(Transform parent, string name, string id, Vector3 position, float yaw, float scale, string[] extras)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            ConfigureAnimal(go.AddComponent<MenuAnimal>(), id, scale, extras);
        }

        private static void ConfigureAnimal(MenuAnimal animal, string id, float scale, string[] extras)
        {
            var so = new SerializedObject(animal);
            so.FindProperty("animalId").stringValue = id;
            so.FindProperty("scale").floatValue = scale;
            var list = so.FindProperty("extras");
            list.arraySize = extras.Length;
            for (int i = 0; i < extras.Length; i++) list.GetArrayElementAtIndex(i).stringValue = extras[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // Menu UI
        // ------------------------------------------------------------------

        private static RectTransform BuildMenu(Transform canvas, LocalizationFonts fonts, out Button play, out Button team, out Button settings, out Button quit)
        {
            var root = UIBuild.Stretch("MainMenu", canvas);

            // Title: a little paw, then the name of the game in big outlined letters.
            var paw = UIBuild.Rect("Paw", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(150f, 150f));
            var pawImg = paw.gameObject.AddComponent<Image>();
            pawImg.sprite = UISpriteGenerator.Load(UISpriteGenerator.Paw);
            pawImg.color = Palette.Primary;
            pawImg.preserveAspect = true;
            pawImg.raycastTarget = false;
            var title = UIBuild.Label("Title", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1000f, 240f),
                "Wild Tamers", 180f, Color.white, TextAlignmentOptions.Center, bold: true);
            title.fontSharedMaterial = fonts.latinBoldOutline;
            title.color = Palette.Primary;
            UIBuild.Key(title, "menu.title");

            // Buttons at the bottom, big enough for thumbs.
            quit = MenuButton(root, "QuitButton", 96f, 150f, "menu.quit", Palette.Neutral, Palette.NeutralDark, 60f);
            settings = MenuButton(root, "SettingsButton", 274f, 150f, "menu.settings", MaterialLibrary.Hex("#4DA3FF"), MaterialLibrary.Hex("#2F7FD6"), 60f);
            team = MenuButton(root, "TeamButton", 452f, 150f, "menu.team", Palette.Teal, Palette.TealDark, 60f);
            play = MenuButton(root, "PlayButton", 630f, 184f, "menu.play", Palette.Primary, Palette.PrimaryDark, 84f);

            root.gameObject.AddComponent<RTLMirror>();
            return root;
        }

        private static Button MenuButton(RectTransform parent, string name, float bottom, float height, string key, Color face, Color lip, float fontSize)
        {
            var button = UIBuild.CandyButton(name, parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottom), new Vector2(780f, height),
                key, face, lip, height * 0.34f, fontSize, out var label, out _);
            UIBuild.Key(label, key);
            UIBuild.DropShadow((RectTransform)button.transform, 26f, -10f, 0.22f);
            return button;
        }
    }
}
