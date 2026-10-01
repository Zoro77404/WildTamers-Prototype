using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using WildTamers.Battle;
using WildTamers.Map;
using WildTamers.UI;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Builds BattleScene: a small grassy arena, the two animal stages, camera and effects,
    /// and the portrait battle UI (fighter cards, log line, four action buttons, damage numbers, result sheet).
    /// </summary>
    public static class BattleSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/BattleScene.unity";
        private const string MeshPath = "Assets/_Project/Art/Meshes/BattleArena.asset";
        private const string DomeMeshPath = "Assets/_Project/Art/Meshes/GuardDome.asset";
        private const string OutlineFontPath = "Assets/_Project/Fonts/Fredoka-Bold SDF Outline.mat";

        private static readonly Vector3 CameraPosition = new Vector3(0f, 10f, -10f);
        private static readonly Vector3 CameraTarget = new Vector3(0f, 1f, 6f);
        private const float CameraFov = 38f;

        // Where each animal's feet land on the portrait screen (viewport 0–1): the player's animal on the left just above
        // the log, the wild one right of center under the top card. The stage spots are solved from these for the camera above.
        private static readonly Vector2 PlayerScreenPoint = new Vector2(0.27f, 0.335f);
        private static readonly Vector2 WildScreenPoint = new Vector2(0.70f, 0.60f);

        private static Vector3 playerSpot;
        private static Vector3 wildSpot;

        [MenuItem("Wild Tamers/Build/Battle Scene")]
        public static void Build()
        {
            SceneSetup.EnsureLayers();
            UIBuild.LoadAssets();
            UISpriteGenerator.GenerateBattleIcons();
            SolveSpots();
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

            // ---------- Animals ----------
            var flash = MaterialLibrary.Unlit(MaterialLibrary.FxFolder + "/FX_HitFlash.mat", Color.white);
            var guardMat = MaterialLibrary.LitTransparent(MaterialLibrary.FxFolder + "/FX_Guard.mat", new Color(0.56f, 0.83f, 1f, 0.32f), 0.85f);
            var domeMesh = BuildDomeMesh();
            var puff = AssetDatabase.LoadAssetAtPath<ParticleSystem>(PrefabBuilder.PuffPrefab);

            var stage = SceneSetup.Group("--Animals--");
            var playerActor = BuildStage(stage.transform, "PlayerSpot", playerSpot, wildSpot, 1.7f, 3.4f, flash, guardMat, domeMesh, puff);
            var wildActor = BuildStage(stage.transform, "WildSpot", wildSpot, playerSpot, 2.05f, 3.8f, flash, guardMat, domeMesh, puff);

            // ---------- Effects ----------
            var fx = SceneSetup.Group("--FX--");
            var sparks = BuildSparks(fx.transform);

            // ---------- Camera ----------
            var camGroup = SceneSetup.Group("--Camera--");
            var cam = SceneSetup.MainCamera(camGroup.transform, sky, CameraFov, 200f);
            cam.transform.position = CameraPosition;
            cam.transform.LookAt(CameraTarget);
            var shake = cam.gameObject.AddComponent<CameraShake>();

            // ---------- Systems & UI ----------
            var systems = SceneSetup.Group("--Systems--");
            SceneSetup.EventSystem(systems.transform);
            var controllerGo = new GameObject("BattleController");
            controllerGo.transform.SetParent(systems.transform, false);
            var controller = controllerGo.AddComponent<BattleController>();

            var uiGroup = SceneSetup.Group("--UI--");
            var canvas = SceneSetup.Canvas("BattleCanvas", uiGroup.transform);
            var hud = BuildUI(canvas.transform, cam);

            UIBuild.Set(controller, "playerActor", playerActor);
            UIBuild.Set(controller, "wildActor", wildActor);
            UIBuild.Set(controller, "hud", hud);
            UIBuild.Set(controller, "cameraShake", shake);
            UIBuild.Set(controller, "sparks", sparks);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Wild Tamers] BattleScene built.");
        }

        // ------------------------------------------------------------------
        // Stage
        // ------------------------------------------------------------------

        private static void SolveSpots()
        {
            var go = new GameObject("SpotSolver");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.aspect = 1080f / 1920f;
                cam.fieldOfView = CameraFov;
                cam.transform.position = CameraPosition;
                cam.transform.LookAt(CameraTarget);
                playerSpot = OnGround(cam, PlayerScreenPoint);
                wildSpot = OnGround(cam, WildScreenPoint);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static Vector3 OnGround(Camera cam, Vector2 viewport)
        {
            var ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance);
            var p = ray.GetPoint(distance);
            return new Vector3(Mathf.Round(p.x * 100f) / 100f, 0f, Mathf.Round(p.z * 100f) / 100f);
        }

        private static BattleActor BuildStage(Transform parent, string name, Vector3 position, Vector3 facing, float scale, float maxHeight,
            Material flash, Material guard, Mesh dome, ParticleSystem puff)
        {
            var spot = new GameObject(name).transform;
            spot.SetParent(parent, false);
            spot.localPosition = position;
            spot.localRotation = Quaternion.LookRotation(facing - position);

            var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(shadow.GetComponent<Collider>());
            shadow.name = "BlobShadow";
            shadow.transform.SetParent(spot, false);
            shadow.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = new Vector3(4.4f, 4.4f, 1f);
            var sr = shadow.GetComponent<MeshRenderer>();
            sr.sharedMaterial = PrefabBuilder.BlobMaterial;
            sr.shadowCastingMode = ShadowCastingMode.Off;

            var bubble = new GameObject("GuardDome");
            bubble.transform.SetParent(spot, false);
            bubble.AddComponent<MeshFilter>().sharedMesh = dome;
            var br = bubble.AddComponent<MeshRenderer>();
            br.sharedMaterial = guard;
            br.shadowCastingMode = ShadowCastingMode.Off;
            br.receiveShadows = false;
            bubble.SetActive(false);

            var actor = spot.gameObject.AddComponent<BattleActor>();
            UIBuild.SetFloat(actor, "displayScale", scale);
            UIBuild.SetFloat(actor, "maxHeight", maxHeight);
            UIBuild.Set(actor, "blobShadow", shadow.transform);
            UIBuild.Set(actor, "guardDome", bubble.transform);
            UIBuild.Set(actor, "flashMaterial", flash);
            UIBuild.Set(actor, "puffPrefab", puff);
            return actor;
        }

        private static Mesh BuildDomeMesh()
        {
            var mb = new MeshBuilder(1);
            mb.Sphere(0, Vector3.zero, Vector3.one, 6, 12);
            var mesh = mb.Build("GuardDome", out _);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(DomeMeshPath);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, DomeMeshPath);
            return mesh;
        }

        private static BattleSparks BuildSparks(Transform parent)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 0.35f;
            main.gravityModifier = 0.9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 2f;
            limit.dampen = 0.12f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0f)));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialLibrary.FxFolder + "/FX_Puff.mat");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = -10f;
            return go.AddComponent<BattleSparks>();
        }

        private static void BuildArenaMesh(GameObject arena, Material[] mats)
        {
            var ground = new MeshBuilder(FakeMapTileProvider.MaterialCount);
            var props = new MeshBuilder(FakeMapTileProvider.MaterialCount);
            var mid = (playerSpot + wildSpot) * 0.5f;
            ground.Disc((int)MapMaterial.Grass, Vector3.zero, 120f, 56);
            ground.Disc((int)MapMaterial.Park, new Vector3(mid.x, 0.03f, mid.z), 9.5f, 32);
            // Platforms under both animals.
            props.Cylinder((int)MapMaterial.Sand, playerSpot + Vector3.down * 0.05f, 2.4f, 0.3f, 16, (int)MapMaterial.Path);
            props.Cylinder((int)MapMaterial.Sand, wildSpot + Vector3.down * 0.05f, 2.6f, 0.3f, 16, (int)MapMaterial.Path);

            var rng = new System.Random(7);
            float R() => (float)rng.NextDouble();

            // A few hand-placed bushes and flower beds where the camera sees open grass.
            var bushes = new[] { new Vector3(-6.2f, 0f, 9.5f), new Vector3(-4.6f, 0f, 14.8f), new Vector3(6.4f, 0f, 16.5f), new Vector3(4.2f, 0f, 3.2f) };
            foreach (var b in bushes)
            {
                props.Blob((int)MapMaterial.Leaves1, b + Vector3.up * 0.5f, new Vector3(1.5f, 1f, 1.3f), 0.16f, rng, R() * 360f);
                props.Blob((int)MapMaterial.Leaves0, b + new Vector3(0.9f, 0.35f, 0.5f), new Vector3(0.9f, 0.7f, 0.9f), 0.16f, rng, R() * 360f);
            }
            var beds = new[] { new Vector3(-3.4f, 0f, 7.2f), new Vector3(-7.5f, 0f, 12.6f), new Vector3(5.2f, 0f, 8.8f) };
            for (int i = 0; i < beds.Length; i++)
                for (int f = 0; f < 9; f++)
                    props.Blob(f % 3 == 0 ? (int)MapMaterial.FlowerWhite : f % 3 == 1 ? (int)MapMaterial.FlowerYellow : (int)MapMaterial.FlowerPink,
                        beds[i] + new Vector3(R() * 2.4f - 1.2f, 0.2f, R() * 2.4f - 1.2f), Vector3.one * 0.22f, 0.1f, null);

            // Forest edge behind the wild animal and along the sides (the camera looks down, so this frames the top of the screen).
            for (int i = 0; i < 70; i++)
            {
                float x = -26f + R() * 52f;
                float z = wildSpot.z + 9f + R() * 26f;
                if (Mathf.Abs(x) < 3.5f && z < wildSpot.z + 12f) continue;
                Tree(props, new Vector3(x, 0f, z), 0.9f + R() * 0.6f, rng);
            }
            for (int i = 0; i < 40; i++)
            {
                float side = R() < 0.5f ? -1f : 1f;
                float x = side * (9f + R() * 16f);
                float z = playerSpot.z - 6f + R() * 18f;
                Tree(props, new Vector3(x, 0f, z), 0.9f + R() * 0.6f, rng);
            }

            // Rocks, bushes and flower patches around the clearing.
            for (int i = 0; i < 34; i++)
            {
                float a = R() * Mathf.PI * 2f;
                float d = 5f + R() * 9f;
                var p = new Vector3(mid.x + Mathf.Sin(a) * d * 1.1f, 0f, mid.z + Mathf.Cos(a) * d);
                if (Vector3.Distance(p, playerSpot) < 4.2f || Vector3.Distance(p, wildSpot) < 4.5f) continue;
                if (Mathf.Abs(p.x - mid.x) < 3f && p.z > playerSpot.z && p.z < wildSpot.z) continue; // keep the lunge lane clear
                float pick = R();
                if (pick < 0.35f)
                    props.Blob((int)MapMaterial.Rock, p + Vector3.up * 0.25f, new Vector3(1f, 0.6f, 0.85f) * (0.5f + R() * 0.7f), 0.22f, rng, R() * 360f);
                else if (pick < 0.6f)
                    props.Blob((int)MapMaterial.Leaves1, p + Vector3.up * 0.45f, new Vector3(1.3f, 0.9f, 1.2f) * (0.7f + R() * 0.5f), 0.16f, rng, R() * 360f);
                else
                    for (int f = 0; f < 7; f++)
                        props.Blob(f % 3 == 0 ? (int)MapMaterial.FlowerWhite : (int)MapMaterial.FlowerPink + (i % 2),
                            p + new Vector3(R() * 2f - 1f, 0.2f, R() * 2f - 1f), Vector3.one * 0.2f, 0.1f, null);
            }

            AddMesh(arena.transform, "Ground", ground, mats, ShadowCastingMode.Off, MeshPath.Replace(".asset", "_Ground.asset"));
            AddMesh(arena.transform, "Props", props, mats, ShadowCastingMode.On, MeshPath.Replace(".asset", "_Props.asset"));
        }

        private static void Tree(MeshBuilder props, Vector3 p, float s, System.Random rng)
        {
            float R() => (float)rng.NextDouble();
            if (R() < 0.65f)
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

        // ------------------------------------------------------------------
        // UI
        // ------------------------------------------------------------------

        private static BattleHUD BuildUI(Transform canvas, Camera cam)
        {
            var root = UIBuild.Stretch("BattleHUD", canvas);
            var hud = root.gameObject.AddComponent<BattleHUD>();

            var wildCard = BuildCard(root, "WildCard", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -64f), new Vector2(600f, 172f), wild: true);
            var playerCard = BuildCard(root, "PlayerCard", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 612f), new Vector2(600f, 214f), wild: false);

            // Log line above the buttons (in a group so it can fade out under the result sheet).
            var logGroupRt = UIBuild.Stretch("LogGroup", root);
            var logGroup = logGroupRt.gameObject.AddComponent<CanvasGroup>();
            logGroup.blocksRaycasts = false;
            var log = UIBuild.Rect("Log", logGroupRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 462f), new Vector2(1008f, 128f));
            UIBuild.DropShadow(log, 26f, -10f, 0.2f);
            UIBuild.Round(log, new Color(1f, 1f, 1f, 0.97f), 44f);
            var logText = UIBuild.Text(UIBuild.Stretch("Text", log, 44f, 14f, 44f, 14f), "A wild Wolf appeared!", 40f, Palette.Ink,
                TextAlignmentOptions.MidlineLeft, bold: true, wrap: true);
            logText.richText = true;

            // Action buttons, 2 × 2.
            var actions = UIBuild.Rect("Actions", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(1008f, 380f));
            var group = actions.gameObject.AddComponent<CanvasGroup>();
            var attack = ActionButton(actions, "AttackButton", 0, 1, "Attack", "Scratch", UISpriteGenerator.Claw, Palette.Primary, Palette.PrimaryDark);
            var skill = ActionButton(actions, "SkillButton", 1, 1, "Fox Fire", "Ready!", UISpriteGenerator.Star, MaterialLibrary.Hex("#9B7BFF"), MaterialLibrary.Hex("#7A5AD9"));
            var defend = ActionButton(actions, "DefendButton", 0, 0, "Defend", "Half damage", UISpriteGenerator.Shield, Palette.Def, MaterialLibrary.Hex("#2F7FD6"));
            var run = ActionButton(actions, "RunButton", 1, 0, "Run", "62% to escape", UISpriteGenerator.Dash, Palette.Neutral, Palette.NeutralDark);

            var numbers = BuildDamageNumbers(root, cam);
            var result = BuildResultPanel(root);

            UIBuild.Set(hud, "playerCard", playerCard);
            UIBuild.Set(hud, "wildCard", wildCard);
            UIBuild.Set(hud, "logGroup", logGroup);
            UIBuild.Set(hud, "logPanel", log);
            UIBuild.Set(hud, "logText", logText);
            UIBuild.Set(hud, "actionPanel", group);
            UIBuild.Set(hud, "attackButton", attack);
            UIBuild.Set(hud, "skillButton", skill);
            UIBuild.Set(hud, "defendButton", defend);
            UIBuild.Set(hud, "runButton", run);
            UIBuild.Set(hud, "damageNumbers", numbers);
            UIBuild.Set(hud, "resultPanel", result);
            return hud;
        }

        private static FighterCard BuildCard(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, bool wild)
        {
            var card = UIBuild.Rect(name, parent, anchor, pivot, pos, size);
            var body = UIBuild.Stretch("Body", card);
            UIBuild.Round(body, new Color(1f, 1f, 1f, 0.97f), 44f);
            UIBuild.DropShadow(body, 26f, -10f, 0.2f);

            var accent = UIBuild.Rect("Accent", body, new Vector2(0f, 0f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.offsetMin = new Vector2(0f, 34f);
            accent.offsetMax = new Vector2(14f, -34f);
            var accentImg = UIBuild.Round(accent, Palette.Primary, 7f);

            var nameText = UIBuild.Label("Name", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -18f), new Vector2(330f, 70f),
                "Wolf", 54f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);
            var level = UIBuild.Label("Level", body, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -22f), new Vector2(150f, 60f),
                "Lv. 5", 40f, Palette.Muted, TextAlignmentOptions.MidlineRight, bold: true);
            if (wild)
            {
                var tag = UIBuild.Rect("WildTag", body, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-188f, -30f), new Vector2(104f, 44f));
                UIBuild.Round(tag, Palette.Danger, 22f);
                UIBuild.Text(UIBuild.Stretch("Text", tag), "WILD", 26f, Color.white, TextAlignmentOptions.Center, bold: true);
            }

            float hpY = wild ? 32f : 76f;
            UIBuild.Label("HPLabel", body, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, hpY - 6f), new Vector2(70f, 42f),
                "HP", 30f, Palette.Muted, TextAlignmentOptions.MidlineLeft, bold: true);
            var hpText = UIBuild.Label("HPText", body, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, hpY - 8f), new Vector2(150f, 46f),
                "38/41", 32f, Palette.Ink, TextAlignmentOptions.MidlineRight, bold: true);
            var hpRt = UIBuild.Rect("HPBar", body, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(98f, hpY), new Vector2(330f, 30f));
            var hp = UIBuild.HealthBar(hpRt, hpText);

            StatBar xp = null;
            if (!wild)
            {
                UIBuild.Label("XPLabel", body, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 22f), new Vector2(70f, 36f),
                    "XP", 26f, Palette.Muted, TextAlignmentOptions.MidlineLeft, bold: true);
                var xpRt = UIBuild.Rect("XPBar", body, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(98f, 30f), new Vector2(468f, 18f));
                xp = UIBuild.Bar(xpRt, Palette.Line, Palette.Def);
            }

            // Guard chip pops up above the card's corner while the animal defends.
            var chip = UIBuild.Rect("GuardChip", body, new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-24f, -14f), new Vector2(190f, 58f));
            var chipGroup = chip.gameObject.AddComponent<CanvasGroup>();
            chipGroup.blocksRaycasts = false;
            UIBuild.Round(chip, Palette.Def, 29f);
            var chipIcon = UIBuild.Rect("Icon", chip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(40f, 40f));
            var chipImg = chipIcon.gameObject.AddComponent<Image>();
            chipImg.sprite = UISpriteGenerator.Load(UISpriteGenerator.Shield);
            chipImg.preserveAspect = true;
            chipImg.raycastTarget = false;
            UIBuild.Text(UIBuild.Stretch("Text", chip, 60f, 0f, 14f, 0f), "GUARD", 28f, Color.white, TextAlignmentOptions.MidlineLeft, bold: true);
            chip.gameObject.SetActive(false);

            var fighter = card.gameObject.AddComponent<FighterCard>();
            UIBuild.Set(fighter, "nameText", nameText);
            UIBuild.Set(fighter, "levelText", level);
            UIBuild.Set(fighter, "hpBar", hp);
            UIBuild.Set(fighter, "xpBar", xp);
            UIBuild.Set(fighter, "accent", accentImg);
            UIBuild.Set(fighter, "guardChip", chipGroup);
            UIBuild.Set(fighter, "body", body);
            return fighter;
        }

        private static BattleActionButton ActionButton(RectTransform parent, string name, int column, int row, string label, string hint,
            string iconPath, Color face, Color lip)
        {
            const float w = 492f, h = 178f, gap = 24f;
            var pos = new Vector2(column * (w + gap), row * (h + gap));
            var button = UIBuild.CandyButton(name, parent, Vector2.zero, Vector2.zero, pos, new Vector2(w, h),
                "", face, lip, 48f, 10f, out var text, out var faceImg);
            Object.DestroyImmediate(text.gameObject);
            var root = (RectTransform)button.transform;

            var icon = UIBuild.Rect("Icon", faceImg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(96f, 96f));
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = UISpriteGenerator.Load(iconPath);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var labelText = UIBuild.Label("Label", faceImg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(150f, -4f), new Vector2(320f, 62f),
                label, 50f, Color.white, TextAlignmentOptions.BottomLeft, bold: true);
            var hintText = UIBuild.Label("Hint", faceImg.transform, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(150f, -6f), new Vector2(320f, 40f),
                hint, 28f, new Color(1f, 1f, 1f, 0.9f), TextAlignmentOptions.TopLeft);

            var badge = UIBuild.Rect("Badge", root, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-20f, -16f), new Vector2(72f, 72f));
            UIBuild.Disc(badge, Color.white);
            var badgeText = UIBuild.Text(UIBuild.Stretch("Text", badge), "2", 42f, lip, TextAlignmentOptions.Center, bold: true);
            badge.gameObject.SetActive(false);

            var action = root.gameObject.AddComponent<BattleActionButton>();
            UIBuild.Set(action, "button", button);
            UIBuild.Set(action, "label", labelText);
            UIBuild.Set(action, "hint", hintText);
            UIBuild.Set(action, "face", faceImg);
            UIBuild.Set(action, "lip", root.GetComponent<Image>());
            UIBuild.Set(action, "badge", badge.gameObject);
            UIBuild.Set(action, "badgeText", badgeText);
            return action;
        }

        private static DamageNumbers BuildDamageNumbers(RectTransform parent, Camera cam)
        {
            var layer = UIBuild.Stretch("DamageNumbers", parent);
            var template = UIBuild.Rect("Popup", layer, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(420f, 190f));
            template.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var outline = OutlineFontMaterial();
            var value = UIBuild.Label("Value", template, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(420f, 120f),
                "12", 104f, Color.white, TextAlignmentOptions.Bottom, bold: true);
            value.fontSharedMaterial = outline;
            var caption = UIBuild.Label("Caption", template, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(420f, 60f),
                "CRITICAL!", 44f, Color.white, TextAlignmentOptions.Bottom, bold: true);
            caption.fontSharedMaterial = outline;
            template.gameObject.SetActive(false);

            var numbers = layer.gameObject.AddComponent<DamageNumbers>();
            UIBuild.Set(numbers, "template", template);
            UIBuild.Set(numbers, "worldCamera", cam);
            return numbers;
        }

        private static Material OutlineFontMaterial()
        {
            var font = UIBuild.FontBold;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(OutlineFontPath);
            if (mat == null)
            {
                mat = new Material(font.material);
                AssetDatabase.CreateAsset(mat, OutlineFontPath);
            }
            else
            {
                mat.shader = font.material.shader;
                mat.CopyPropertiesFromMaterial(font.material);
            }
            mat.name = "Fredoka-Bold SDF Outline";
            mat.SetFloat("_OutlineWidth", 0.26f);
            mat.SetColor("_OutlineColor", Palette.Ink);
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetColor("_UnderlayColor", new Color(0.08f, 0.12f, 0.2f, 0.4f));
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", -0.9f);
            mat.SetFloat("_UnderlayDilate", 0.3f);
            mat.SetFloat("_UnderlaySoftness", 0.25f);
            mat.EnableKeyword("UNDERLAY_ON");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static BattleResultPanel BuildResultPanel(RectTransform parent)
        {
            var root = UIBuild.Stretch("ResultPanel", parent);
            root.gameObject.AddComponent<CanvasGroup>();
            var dim = UIBuild.Stretch("Dim", root);
            UIBuild.Plain(dim, new Color(0.106f, 0.149f, 0.22f, 0.25f), raycast: true);

            var card = UIBuild.Rect("Card", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1008f, 520f));
            UIBuild.DropShadow(card, 40f, -16f, 0.28f);
            var body = UIBuild.Stretch("Body", card);
            UIBuild.Round(body, Palette.Panel, 56f, raycast: true);

            var badge = UIBuild.Rect("TitleBadge", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(440f, 96f));
            UIBuild.DropShadow(badge, 22f, -8f, 0.2f);
            var badgeImg = UIBuild.Round(badge, Palette.Primary, 48f);
            var title = UIBuild.Text(UIBuild.Stretch("Text", badge), "Victory!", 58f, Color.white, TextAlignmentOptions.Center, bold: true);

            var message = UIBuild.Label("Message", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(920f, 84f),
                "Wolf (Lv. 5) joined your team!", 40f, Palette.Ink, TextAlignmentOptions.Center, bold: false, wrap: true);
            message.richText = true;

            // XP block.
            var xp = UIBuild.Rect("XP", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(900f, 104f));
            var xpBg = UIBuild.Stretch("Background", xp);
            UIBuild.Round(xpBg, Palette.PanelAlt, 30f);
            var xpName = UIBuild.Label("Name", xp, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -10f), new Vector2(360f, 54f),
                "Fox", 44f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);
            var xpLevel = UIBuild.Label("Level", xp, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, -12f), new Vector2(200f, 52f),
                "Lv. 5", 38f, Palette.Teal, TextAlignmentOptions.MidlineLeft, bold: true);
            var xpGain = UIBuild.Label("Gain", xp, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -10f), new Vector2(300f, 54f),
                "+50 XP", 40f, Palette.Def, TextAlignmentOptions.MidlineRight, bold: true);
            var xpBarRt = UIBuild.Rect("Bar", xp, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(840f, 26f));
            var xpBar = UIBuild.Bar(xpBarRt, Palette.Line, Palette.Def);

            // Level-up banner.
            var levelUp = UIBuild.Rect("LevelUp", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -268f), new Vector2(900f, 60f));
            var levelGroup = levelUp.gameObject.AddComponent<CanvasGroup>();
            levelGroup.blocksRaycasts = false;
            var pill = UIBuild.Rect("Pill", levelUp, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(250f, 60f));
            UIBuild.Round(pill, Palette.Primary, 30f);
            var levelTitle = UIBuild.Text(UIBuild.Stretch("Text", pill), "Level up!", 36f, Color.white, TextAlignmentOptions.Center, bold: true);
            var levelStats = UIBuild.Label("Stats", levelUp, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(270f, 0f), new Vector2(630f, 60f),
                "HP +2   ATK +1   DEF +1   SPD +1", 32f, Palette.Ink, TextAlignmentOptions.MidlineLeft);
            levelStats.richText = true;

            var cont = UIBuild.CandyButton("ContinueButton", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(500f, 130f),
                "Continue", Palette.Primary, Palette.PrimaryDark, 48f, 50f, out _, out _);
            UIBuild.DropShadow((RectTransform)cont.transform, 26f, -10f, 0.2f);

            var panel = root.gameObject.AddComponent<BattleResultPanel>();
            UIBuild.Set(panel, "card", card);
            UIBuild.Set(panel, "titleText", title);
            UIBuild.Set(panel, "messageText", message);
            UIBuild.Set(panel, "titleBadge", badgeImg);
            UIBuild.Set(panel, "xpSection", xp.gameObject);
            UIBuild.Set(panel, "xpNameText", xpName);
            UIBuild.Set(panel, "xpLevelText", xpLevel);
            UIBuild.Set(panel, "xpGainText", xpGain);
            UIBuild.Set(panel, "xpBar", xpBar);
            UIBuild.Set(panel, "levelUpGroup", levelGroup);
            UIBuild.Set(panel, "levelUpTitle", levelTitle);
            UIBuild.Set(panel, "levelUpStats", levelStats);
            UIBuild.Set(panel, "continueButton", cont);
            UIBuild.SetBool(panel, "blocksMapInput", false);
            root.gameObject.SetActive(false);
            return panel;
        }
    }
}
