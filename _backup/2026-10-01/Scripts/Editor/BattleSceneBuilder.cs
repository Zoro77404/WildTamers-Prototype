using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using WildTamers.Battle;
using WildTamers.Map;

namespace WildTamers.EditorTools
{
    /// <summary>Builds the Phase 1 BattleScene: a small grassy arena, two animal spots, camera and placeholder UI.</summary>
    public static class BattleSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/BattleScene.unity";
        private const string MeshPath = "Assets/_Project/Art/Meshes/BattleArena.asset";

        // Solved so the animals sit in the open area between the portrait UI cards
        // (player ~28% / 58% of the screen, wild ~67% / 75%) for the camera below.
        public static readonly Vector3 PlayerSpot = new Vector3(-1.38f, 0f, 6.01f);
        public static readonly Vector3 WildSpot = new Vector3(1.56f, 0f, 13.7f);
        private static readonly Vector3 CameraPosition = new Vector3(0f, 6f, -8.5f);
        private static readonly Vector3 CameraTarget = new Vector3(0f, 0.6f, 4.5f);

        [MenuItem("Wild Tamers/Build/Battle Scene")]
        public static void Build()
        {
            SceneSetup.EnsureLayers();
            UIBuild.LoadAssets();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneSetup.ClearScene();

            var sky = MaterialLibrary.Hex("#CDEAF7");
            var lighting = SceneSetup.Group("--Lighting--");
            SceneSetup.Sun(lighting.transform, MaterialLibrary.Hex("#FFF3DF"), 1.3f, new Vector3(48f, -150f, 0f), 0.55f);
            SceneSetup.Ambient(MaterialLibrary.Hex("#D2E9FF"), MaterialLibrary.Hex("#E4EFD9"), MaterialLibrary.Hex("#A3B58F"),
                true, sky, 40f, 110f);
            SceneSetup.GlobalVolume(lighting.transform, "BattleVolumeProfile");

            // ---------- Arena ----------
            var env = SceneSetup.Group("--Environment--");
            var mats = MaterialLibrary.BuildMapMaterials();
            var arena = new GameObject("Arena");
            arena.transform.SetParent(env.transform, false);
            BuildArenaMesh(arena, mats);

            var spots = SceneSetup.Group("--Animals--");
            var playerSpot = new GameObject("PlayerSpot").transform;
            playerSpot.SetParent(spots.transform, false);
            playerSpot.localPosition = PlayerSpot;
            playerSpot.localRotation = Quaternion.LookRotation(WildSpot - PlayerSpot);
            var wildSpot = new GameObject("WildSpot").transform;
            wildSpot.SetParent(spots.transform, false);
            wildSpot.localPosition = WildSpot;
            wildSpot.localRotation = Quaternion.LookRotation(PlayerSpot - WildSpot);
            foreach (var spot in new[] { playerSpot, wildSpot })
            {
                var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(shadow.GetComponent<Collider>());
                shadow.name = "BlobShadow";
                shadow.transform.SetParent(spot, false);
                shadow.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                shadow.transform.localScale = new Vector3(4.4f, 4.4f, 1f);
                var r = shadow.GetComponent<MeshRenderer>();
                r.sharedMaterial = PrefabBuilder.BlobMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            // ---------- Camera ----------
            var camGroup = SceneSetup.Group("--Camera--");
            var cam = SceneSetup.MainCamera(camGroup.transform, sky, 40f, 200f);
            cam.transform.position = CameraPosition;
            cam.transform.LookAt(CameraTarget);

            // ---------- Systems & UI ----------
            var systems = SceneSetup.Group("--Systems--");
            SceneSetup.EventSystem(systems.transform);
            var controllerGo = new GameObject("BattleController");
            controllerGo.transform.SetParent(systems.transform, false);
            var battle = controllerGo.AddComponent<BattlePlaceholder>();

            var uiGroup = SceneSetup.Group("--UI--");
            var canvas = SceneSetup.Canvas("BattleCanvas", uiGroup.transform);
            BuildUI(canvas.transform, battle);

            UIBuild.Set(battle, "playerSpot", playerSpot);
            UIBuild.Set(battle, "wildSpot", wildSpot);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Wild Tamers] BattleScene built.");
        }

        private static void BuildArenaMesh(GameObject arena, Material[] mats)
        {
            var ground = new MeshBuilder(FakeMapTileProvider.MaterialCount);
            var props = new MeshBuilder(FakeMapTileProvider.MaterialCount);
            ground.Disc((int)MapMaterial.Grass, Vector3.zero, 90f, 48);
            var mid = (PlayerSpot + WildSpot) * 0.5f;
            ground.Disc((int)MapMaterial.Park, new Vector3(mid.x, 0.03f, mid.z), 15f, 32);
            // Platforms under both animals.
            props.Cylinder((int)MapMaterial.Sand, PlayerSpot + Vector3.down * 0.05f, 2.4f, 0.3f, 16, (int)MapMaterial.Path);
            props.Cylinder((int)MapMaterial.Sand, WildSpot + Vector3.down * 0.05f, 2.6f, 0.3f, 16, (int)MapMaterial.Path);

            // A ring of trees, bushes, rocks and flowers framing the arena.
            var rng = new System.Random(7);
            float R() => (float)rng.NextDouble();
            for (int i = 0; i < 46; i++)
            {
                float a = i / 46f * Mathf.PI * 2f + R() * 0.12f;
                float d = 15f + R() * 16f;
                var p = new Vector3(Mathf.Sin(a) * d, 0f, mid.z + Mathf.Cos(a) * d);
                if (p.z < 2f && Mathf.Abs(p.x) < 12f) continue; // keep the camera's view clear
                float s = 0.9f + R() * 0.6f;
                if (R() < 0.7f)
                {
                    props.Cylinder((int)MapMaterial.Trunk, p, 0.3f * s, 2f * s, 6, -1, 0.22f * s);
                    props.Blob(R() < 0.5f ? (int)MapMaterial.Leaves0 : (int)MapMaterial.Leaves1, p + Vector3.up * 3f * s,
                        new Vector3(1.9f, 1.65f, 1.9f) * s, 0.13f, rng, R() * 360f);
                }
                else
                {
                    props.Cylinder((int)MapMaterial.Trunk, p, 0.28f * s, 1.3f * s, 6, -1, 0.22f * s);
                    props.Cone((int)MapMaterial.Leaves2, p + Vector3.up * 1.1f * s, 1.9f * s, 2.8f * s, 7);
                    props.Cone((int)MapMaterial.Leaves2, p + Vector3.up * 2.4f * s, 1.45f * s, 2.4f * s, 7, 0.3f);
                    props.Cone((int)MapMaterial.Leaves2, p + Vector3.up * 3.6f * s, 0.95f * s, 2f * s, 7, 0.6f);
                }
            }
            for (int i = 0; i < 18; i++)
            {
                float a = R() * Mathf.PI * 2f;
                float d = 6f + R() * 8f;
                var p = new Vector3(Mathf.Sin(a) * d, 0f, mid.z + Mathf.Cos(a) * d);
                if (p.z < 4f && Mathf.Abs(p.x) < 7f) continue;
                if (Vector3.Distance(p, PlayerSpot) < 4f || Vector3.Distance(p, WildSpot) < 4f) continue;
                if (R() < 0.5f)
                    props.Blob((int)MapMaterial.Rock, p + Vector3.up * 0.25f, new Vector3(1f, 0.6f, 0.85f) * (0.5f + R() * 0.7f), 0.22f, rng, R() * 360f);
                else
                    for (int f = 0; f < 7; f++)
                        props.Blob(f % 3 == 0 ? (int)MapMaterial.FlowerWhite : (int)MapMaterial.FlowerPink + (i % 2),
                            p + new Vector3(R() * 2f - 1f, 0.2f, R() * 2f - 1f), Vector3.one * 0.2f, 0.1f, null);
            }

            AddMesh(arena.transform, "Ground", ground, mats, ShadowCastingMode.Off, MeshPath.Replace(".asset", "_Ground.asset"));
            AddMesh(arena.transform, "Props", props, mats, ShadowCastingMode.On, MeshPath.Replace(".asset", "_Props.asset"));
        }

        private static void AddMesh(Transform parent, string name, MeshBuilder mb, Material[] mats, ShadowCastingMode shadows, string path)
        {
            var mesh = mb.Build(name, out List<int> used);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var m = new Material[used.Count];
            for (int i = 0; i < used.Count; i++) m[i] = mats[used[i]];
            mr.sharedMaterials = m;
            mr.shadowCastingMode = shadows;
        }

        private static void BuildUI(Transform canvas, BattlePlaceholder battle)
        {
            // Wild animal card (top-left).
            var wildCard = UIBuild.Rect("WildCard", canvas, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -70f), new Vector2(620f, 196f));
            var wildBody = UIBuild.Stretch("Body", wildCard);
            UIBuild.Round(wildBody, new Color(1f, 1f, 1f, 0.97f), 48f);
            UIBuild.DropShadow(wildBody, 26f, -10f, 0.2f);
            var wildName = UIBuild.Label("Name", wildBody, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -22f), new Vector2(380f, 70f),
                "Wolf", 56f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);
            var wildLevel = UIBuild.Label("Level", wildBody, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -26f), new Vector2(180f, 60f),
                "Lv. 5", 40f, Palette.Muted, TextAlignmentOptions.MidlineRight, bold: true);
            UIBuild.Label("HPLabel", wildBody, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 34f), new Vector2(70f, 40f),
                "HP", 30f, Palette.Muted, TextAlignmentOptions.MidlineLeft, bold: true);
            var wildHpRt = UIBuild.Rect("HPBar", wildBody, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(104f, 40f), new Vector2(482f, 30f));
            var wildHp = UIBuild.Bar(wildHpRt, Palette.Line, Palette.HpGood);

            // Player animal card (bottom-right).
            var myCard = UIBuild.Rect("PlayerCard", canvas, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 540f), new Vector2(620f, 220f));
            var myBody = UIBuild.Stretch("Body", myCard);
            UIBuild.Round(myBody, new Color(1f, 1f, 1f, 0.97f), 48f);
            UIBuild.DropShadow(myBody, 26f, -10f, 0.2f);
            var myName = UIBuild.Label("Name", myBody, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -22f), new Vector2(380f, 70f),
                "Fox", 56f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);
            var myLevel = UIBuild.Label("Level", myBody, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -26f), new Vector2(180f, 60f),
                "Lv. 5", 40f, Palette.Muted, TextAlignmentOptions.MidlineRight, bold: true);
            UIBuild.Label("HPLabel", myBody, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 70f), new Vector2(70f, 40f),
                "HP", 30f, Palette.Muted, TextAlignmentOptions.MidlineLeft, bold: true);
            var myHpRt = UIBuild.Rect("HPBar", myBody, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(104f, 76f), new Vector2(482f, 30f));
            var myHp = UIBuild.Bar(myHpRt, Palette.Line, Palette.HpGood);
            var myHpText = UIBuild.Label("HPText", myBody, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 18f), new Vector2(240f, 46f),
                "32/32", 34f, Palette.Ink, TextAlignmentOptions.MidlineRight, bold: true);

            // Battle log.
            var log = UIBuild.Rect("Log", canvas, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 262f), new Vector2(1000f, 236f));
            UIBuild.Round(log, new Color(1f, 1f, 1f, 0.97f), 44f);
            UIBuild.DropShadow(log, 26f, -10f, 0.2f);
            var logText = UIBuild.Text(UIBuild.Stretch("Text", log, 44f, 30f, 44f, 30f), "A wild Wolf wants to fight!", 44f, Palette.Ink,
                TextAlignmentOptions.MidlineLeft, bold: true, wrap: true);

            var back = UIBuild.CandyButton("BackButton", canvas, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 72f), new Vector2(580f, 156f),
                "Back to Map", Palette.Primary, Palette.PrimaryDark, 52f, 54f, out _, out _);
            UIBuild.DropShadow((RectTransform)back.transform, 28f, -10f, 0.22f);

            UIBuild.Set(battle, "wildName", wildName);
            UIBuild.Set(battle, "wildLevel", wildLevel);
            UIBuild.Set(battle, "wildHp", wildHp);
            UIBuild.Set(battle, "playerName", myName);
            UIBuild.Set(battle, "playerLevel", myLevel);
            UIBuild.Set(battle, "playerHp", myHp);
            UIBuild.Set(battle, "playerHpText", myHpText);
            UIBuild.Set(battle, "logText", logText);
            UIBuild.Set(battle, "backButton", back);
        }
    }
}
