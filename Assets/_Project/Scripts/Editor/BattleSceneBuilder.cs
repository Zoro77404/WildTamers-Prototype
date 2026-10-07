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
using WildTamers.Lang;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Builds BattleScene: a small grassy arena, the stages for your three animals and the wild boss, camera and effects,
    /// and the portrait battle UI (boss card, three team cards, log line, four action buttons, damage numbers, result sheet, animal card).
    /// </summary>
    public static class BattleSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/BattleScene.unity";
        private const string MeshPath = "Assets/_Project/Art/Meshes/BattleArena.asset";
        private const string DomeMeshPath = "Assets/_Project/Art/Meshes/GuardDome.asset";
        private const string GemMeshPath = "Assets/_Project/Art/Meshes/TurnGem.asset";
        private const string TurnRingMeshPath = "Assets/_Project/Art/Meshes/TurnRing.asset";
        private const string OutlineFontPath = "Assets/_Project/Fonts/Fredoka-Bold SDF Outline.mat";

        private static readonly Vector3 CameraPosition = new Vector3(0f, 10.5f, -11f);
        private static readonly Vector3 CameraTarget = new Vector3(0f, 1.2f, 6.5f);
        private const float CameraFov = 40f;

        // Where each animal's feet land on the portrait screen (viewport 0–1): your three animals in a shallow arc just above the
        // team cards, the wild boss behind them under its card. The stage spots are solved from these for the camera above.
        private static readonly Vector2[] PartyScreenPoints =
        {
            new Vector2(0.21f, 0.405f), new Vector2(0.50f, 0.35f), new Vector2(0.79f, 0.405f)
        };
        private static readonly Vector2 WildScreenPoint = new Vector2(0.5f, 0.63f);

        private static Vector3[] partySpots = new Vector3[3];
        private static Vector3 wildSpot;

        [MenuItem("Wild Tamers/Build/Battle Scene")]
        public static void Build()
        {
            SceneSetup.EnsureLayers();
            UIBuild.LoadAssets();
            UISpriteGenerator.GenerateBattleIcons();
            LocalizationBuilder.BuildFonts();
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
            var gemMesh = BuildGemMesh();
            var ringMesh = BuildTurnRingMesh();
            var gemMat = MaterialLibrary.Unlit(MaterialLibrary.FxFolder + "/FX_TurnGem.mat", MaterialLibrary.Hex("#FFC93C"));
            var ringMat = MaterialLibrary.UnlitTransparent(MaterialLibrary.FxFolder + "/FX_TurnRing.mat", new Color(1f, 0.79f, 0.24f, 0.85f), null, 12);
            var turnFx = new TurnFx { Gem = gemMesh, GemMaterial = gemMat, Ring = ringMesh, RingMaterial = ringMat };

            var stage = SceneSetup.Group("--Animals--");
            var partyActors = new BattleActor[3];
            string[] names = { "PartySpotLeft", "PartySpotMiddle", "PartySpotRight" };
            for (int i = 0; i < 3; i++)
                partyActors[i] = BuildStage(stage.transform, names[i], partySpots[i], wildSpot, 1.0f, 2.6f, flash, guardMat, domeMesh, puff, turnFx);
            var wildActor = BuildStage(stage.transform, "WildSpot", wildSpot, partySpots[1], 2.3f, 4.8f, flash, guardMat, domeMesh, puff, turnFx);

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
            var animalCard = CardBuilder.BuildAnimalCard(canvas.transform);

            // Pause button (top corner), pause menu, settings and the "Leave the battle?" popup.
            var topBar = UIBuild.Stretch("TopBar", canvas.transform);
            topBar.gameObject.AddComponent<RTLMirror>();
            var pauseButton = MenuUiBuilder.BuildPauseButton(topBar);
            var confirm = MenuUiBuilder.BuildConfirmPopup(canvas.transform);
            var settings = MenuUiBuilder.BuildSettingsPanel(canvas.transform, confirm, null, stayInSceneOnReset: false);
            MenuUiBuilder.BuildPauseMenu(canvas.transform, pauseButton, settings, confirm, inBattle: true);

            UIBuild.SetArray(controller, "playerActors", partyActors);
            UIBuild.Set(controller, "wildActor", wildActor);
            UIBuild.Set(controller, "hud", hud);
            UIBuild.Set(controller, "animalCard", animalCard);
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
                for (int i = 0; i < 3; i++) partySpots[i] = OnGround(cam, PartyScreenPoints[i]);
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

        private class TurnFx
        {
            public Mesh Gem, Ring;
            public Material GemMaterial, RingMaterial;
        }

        private static BattleActor BuildStage(Transform parent, string name, Vector3 position, Vector3 facing, float scale, float maxHeight,
            Material flash, Material guard, Mesh dome, ParticleSystem puff, TurnFx turn)
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
            shadow.transform.localScale = new Vector3(4.4f, 4.4f, 1f) * (scale / 1.85f * 0.5f + 0.55f);
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

            // Turn marker: a spinning gem above the head and a pulsing ring on the ground.
            var arrow = new GameObject("TurnArrow");
            arrow.transform.SetParent(spot, false);
            var gem = new GameObject("Gem");
            gem.transform.SetParent(arrow.transform, false);
            gem.AddComponent<MeshFilter>().sharedMesh = turn.Gem;
            var gr = gem.AddComponent<MeshRenderer>();
            gr.sharedMaterial = turn.GemMaterial;
            gr.shadowCastingMode = ShadowCastingMode.Off;
            gr.receiveShadows = false;
            arrow.SetActive(false);

            var ring = new GameObject("TurnRing");
            ring.transform.SetParent(spot, false);
            ring.transform.localPosition = new Vector3(0f, 0.34f, 0f);
            ring.AddComponent<MeshFilter>().sharedMesh = turn.Ring;
            var rr = ring.AddComponent<MeshRenderer>();
            rr.sharedMaterial = turn.RingMaterial;
            rr.shadowCastingMode = ShadowCastingMode.Off;
            rr.receiveShadows = false;
            ring.SetActive(false);

            var actor = spot.gameObject.AddComponent<BattleActor>();
            UIBuild.SetFloat(actor, "displayScale", scale);
            UIBuild.SetFloat(actor, "maxHeight", maxHeight);
            UIBuild.Set(actor, "blobShadow", shadow.transform);
            UIBuild.Set(actor, "guardDome", bubble.transform);
            UIBuild.Set(actor, "flashMaterial", flash);
            UIBuild.Set(actor, "puffPrefab", puff);
            UIBuild.Set(actor, "turnArrow", arrow.transform);
            UIBuild.Set(actor, "turnRing", ring.transform);
            return actor;
        }

        private static Mesh BuildDomeMesh()
        {
            var mb = new MeshBuilder(1);
            mb.Sphere(0, Vector3.zero, Vector3.one, 6, 12);
            return SaveMesh(mb.Build("GuardDome", out _), DomeMeshPath);
        }

        /// <summary>Faceted diamond (octahedron) that hovers above the animal whose turn it is.</summary>
        private static Mesh BuildGemMesh()
        {
            var mb = new MeshBuilder(1);
            mb.Sphere(0, Vector3.zero, new Vector3(0.34f, 0.5f, 0.34f), 2, 4);
            return SaveMesh(mb.Build("TurnGem", out _), GemMeshPath);
        }

        private static Mesh BuildTurnRingMesh()
        {
            return SaveMesh(RingMesh.Create(0.82f, 1f, 48, false, "TurnRing"), TurnRingMeshPath);
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
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
            var middle = partySpots[1];
            var mid = (middle + wildSpot) * 0.5f;
            ground.Disc((int)MapMaterial.Grass, Vector3.zero, 120f, 56);
            ground.Disc((int)MapMaterial.Park, new Vector3(mid.x, 0.03f, mid.z), 11f, 32);
            // Platforms under your three animals and the boss.
            foreach (var spot in partySpots)
                props.Cylinder((int)MapMaterial.Sand, spot + Vector3.down * 0.05f, 1.8f, 0.3f, 16, (int)MapMaterial.Path);
            props.Cylinder((int)MapMaterial.Sand, wildSpot + Vector3.down * 0.05f, 3.4f, 0.3f, 16, (int)MapMaterial.Path);

            var rng = new System.Random(7);
            float R() => (float)rng.NextDouble();

            bool NearAnimal(Vector3 p, float margin)
            {
                foreach (var s in partySpots) if (Vector3.Distance(p, s) < 3.2f + margin) return true;
                return Vector3.Distance(p, wildSpot) < 4.2f + margin;
            }

            // A few hand-placed bushes and flower beds where the camera sees open grass.
            var bushes = new[] { new Vector3(-7.2f, 0f, 9.5f), new Vector3(-6.4f, 0f, 15.8f), new Vector3(7.4f, 0f, 16.5f), new Vector3(7.0f, 0f, 5.2f) };
            foreach (var b in bushes)
            {
                if (NearAnimal(b, 1.2f)) continue;
                props.Blob((int)MapMaterial.Leaves1, b + Vector3.up * 0.5f, new Vector3(1.5f, 1f, 1.3f), 0.16f, rng, R() * 360f);
                props.Blob((int)MapMaterial.Leaves0, b + new Vector3(0.9f, 0.35f, 0.5f), new Vector3(0.9f, 0.7f, 0.9f), 0.16f, rng, R() * 360f);
            }
            var beds = new[] { new Vector3(-3.4f, 0f, 9.2f), new Vector3(-8.5f, 0f, 12.6f), new Vector3(5.6f, 0f, 9.8f) };
            for (int i = 0; i < beds.Length; i++)
            {
                if (NearAnimal(beds[i], 0.8f)) continue;
                for (int f = 0; f < 9; f++)
                    props.Blob(f % 3 == 0 ? (int)MapMaterial.FlowerWhite : f % 3 == 1 ? (int)MapMaterial.FlowerYellow : (int)MapMaterial.FlowerPink,
                        beds[i] + new Vector3(R() * 2.4f - 1.2f, 0.2f, R() * 2.4f - 1.2f), Vector3.one * 0.22f, 0.1f, null);
            }

            // Forest edge behind the wild boss and along the sides (the camera looks down, so this frames the top of the screen).
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
                float x = side * (11f + R() * 16f);
                float z = middle.z - 6f + R() * 18f;
                Tree(props, new Vector3(x, 0f, z), 0.9f + R() * 0.6f, rng);
            }

            // Rocks, bushes and flower patches around the clearing.
            for (int i = 0; i < 34; i++)
            {
                float a = R() * Mathf.PI * 2f;
                float d = 6f + R() * 9f;
                var p = new Vector3(mid.x + Mathf.Sin(a) * d * 1.15f, 0f, mid.z + Mathf.Cos(a) * d);
                if (NearAnimal(p, 0.5f)) continue;
                if (Mathf.Abs(p.x - mid.x) < 5f && p.z > middle.z - 3f && p.z < wildSpot.z + 2f) continue; // keep the lunge lanes clear
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

            // Big card for the wild boss on top, three small cards for your animals above the log.
            var wildCard = BuildCard(root, "WildCard", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(984f, 214f), CardKind.Wild);
            var partyCards = new FighterCard[3];
            string[] cardNames = { "PartyCardLeft", "PartyCardMiddle", "PartyCardRight" };
            for (int i = 0; i < 3; i++)
                partyCards[i] = BuildCard(root, cardNames[i], new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 344f, 396f), new Vector2(320f, 206f), CardKind.Party);

            // Log line above the buttons (in a group so it can fade out under the result sheet).
            var logGroupRt = UIBuild.Stretch("LogGroup", root);
            var logGroup = logGroupRt.gameObject.AddComponent<CanvasGroup>();
            logGroup.blocksRaycasts = false;
            var log = UIBuild.Rect("Log", logGroupRt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 276f), new Vector2(1008f, 100f));
            UIBuild.DropShadow(log, 26f, -10f, 0.2f);
            UIBuild.Round(log, new Color(1f, 1f, 1f, 0.97f), 40f);
            var logText = UIBuild.Text(UIBuild.Stretch("Text", log, 40f, 8f, 40f, 8f), "A wild Camel appeared!", 36f, Palette.Ink,
                TextAlignmentOptions.MidlineLeft, bold: true, wrap: true);
            logText.richText = true;
            logText.enableAutoSizing = true;
            logText.fontSizeMin = 26f;
            logText.fontSizeMax = 36f;

            // Action buttons in one row.
            var actions = UIBuild.Rect("Actions", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(1008f, 200f));
            var group = actions.gameObject.AddComponent<CanvasGroup>();
            actions.gameObject.AddComponent<RTLMirror>();
            var attack = ActionButton(actions, "AttackButton", 0, "Attack", "Stomp", UISpriteGenerator.Claw, Palette.Primary, Palette.PrimaryDark);
            var skill = ActionButton(actions, "SkillButton", 1, "Skill", "Ready!", UISpriteGenerator.Star, MaterialLibrary.Hex("#9B7BFF"), MaterialLibrary.Hex("#7A5AD9"));
            var defend = ActionButton(actions, "DefendButton", 2, "Defend", "Half damage", UISpriteGenerator.Shield, Palette.Def, MaterialLibrary.Hex("#2F7FD6"));
            var run = ActionButton(actions, "RunButton", 3, "Run", "62% team escape", UISpriteGenerator.Dash, Palette.Neutral, Palette.NeutralDark);

            var numbers = BuildDamageNumbers(root, cam);
            var result = BuildResultPanel(root);

            UIBuild.SetArray(hud, "partyCards", partyCards);
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

        private enum CardKind { Party, Wild }

        private static FighterCard BuildCard(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, CardKind kind)
        {
            bool wild = kind == CardKind.Wild;
            var card = UIBuild.Rect(name, parent, anchor, pivot, pos, size);
            card.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            var group = card.GetComponent<CanvasGroup>();

            // Glowing frame behind the card: lights up on this animal's turn.
            var glowRt = UIBuild.Stretch("TurnGlow", card, -12f, -12f, -12f, -12f);
            var glow = UIBuild.Round(glowRt, MaterialLibrary.Hex("#FFC93C"), wild ? 58f : 50f);
            glowRt.gameObject.SetActive(false);

            var body = UIBuild.Stretch("Body", card);
            UIBuild.Round(body, new Color(1f, 1f, 1f, 0.97f), wild ? 52f : 42f);
            UIBuild.DropShadow(body, 26f, -10f, 0.2f);

            Image accentImg = null;
            AnimalPreviewImage portrait = null;
            Image portraitBgImg = null;
            TMP_Text nameText, levelText;
            HealthBar hp;
            RectTransform guardRt;

            if (wild)
            {
                var accent = UIBuild.Rect("Accent", body, new Vector2(0f, 0f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                accent.anchorMin = new Vector2(0f, 0f);
                accent.anchorMax = new Vector2(0f, 1f);
                accent.offsetMin = new Vector2(0f, 38f);
                accent.offsetMax = new Vector2(16f, -38f);
                accentImg = UIBuild.Round(accent, Palette.Primary, 8f);

                nameText = UIBuild.Label("Name", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -18f), new Vector2(560f, 78f),
                    "Arabian Gazelle", 64f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);
                ((TextMeshProUGUI)nameText).enableAutoSizing = true;
                ((TextMeshProUGUI)nameText).fontSizeMin = 40f;
                ((TextMeshProUGUI)nameText).fontSizeMax = 64f;
                var tag = UIBuild.Rect("WildTag", body, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -28f), new Vector2(214f, 52f));
                UIBuild.Round(tag, Palette.Danger, 26f);
                UIBuild.Key(UIBuild.Text(UIBuild.Stretch("Text", tag), "WILD BOSS", 28f, Color.white, TextAlignmentOptions.Center, bold: true), "battle.wildtag");
                levelText = UIBuild.Label("Level", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -96f), new Vector2(150f, 50f),
                    "Lv. 5", 40f, Palette.Muted, TextAlignmentOptions.MidlineLeft, bold: true);

                var hpText = UIBuild.Label("HPText", body, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -96f), new Vector2(300f, 50f),
                    "120/120", 40f, Palette.Ink, TextAlignmentOptions.MidlineRight, bold: true);
                var hpRt = UIBuild.Rect("HPBar", body, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(48f, 30f), new Vector2(888f, 44f));
                hp = UIBuild.HealthBar(hpRt, hpText);
                guardRt = UIBuild.Rect("GuardChip", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(214f, -96f), new Vector2(190f, 50f));
            }
            else
            {
                var portraitBg = UIBuild.Rect("PortraitBackdrop", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -14f), new Vector2(98f, 98f));
                portraitBgImg = UIBuild.Disc(portraitBg, Palette.Line);
                var portraitRt = UIBuild.Stretch("Portrait", portraitBg, -4f, -4f, -4f, -4f);
                portraitRt.gameObject.AddComponent<RawImage>().raycastTarget = false;
                portrait = portraitRt.gameObject.AddComponent<AnimalPreviewImage>();
                UIBuild.SetBool(portrait, "live", false);

                nameText = UIBuild.Label("Name", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(122f, -14f), new Vector2(188f, 46f),
                    "Arabian Gazelle", 34f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);
                ((TextMeshProUGUI)nameText).enableAutoSizing = true;
                ((TextMeshProUGUI)nameText).fontSizeMin = 20f;
                ((TextMeshProUGUI)nameText).fontSizeMax = 34f;
                levelText = UIBuild.Label("Level", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(122f, -62f), new Vector2(110f, 40f),
                    "Lv. 5", 30f, Palette.Muted, TextAlignmentOptions.MidlineLeft, bold: true);

                var hpText = UIBuild.Label("HPText", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(288f, 34f),
                    "38/41", 28f, Palette.Ink, TextAlignmentOptions.Center, bold: true);
                var hpRt = UIBuild.Rect("HPBar", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(288f, 28f));
                hp = UIBuild.HealthBar(hpRt, hpText);
                guardRt = UIBuild.Rect("GuardChip", body, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -64f), new Vector2(76f, 40f));
            }

            // Guard chip pops up while the animal defends.
            var chipGroup = guardRt.gameObject.AddComponent<CanvasGroup>();
            chipGroup.blocksRaycasts = false;
            UIBuild.Round(guardRt, Palette.Def, guardRt.sizeDelta.y * 0.5f);
            float iconSize = guardRt.sizeDelta.y - 10f;
            var chipIcon = UIBuild.Rect("Icon", guardRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(wild ? 14f : 20f, 0f), new Vector2(iconSize, iconSize));
            var chipImg = chipIcon.gameObject.AddComponent<Image>();
            chipImg.sprite = UISpriteGenerator.Load(UISpriteGenerator.Shield);
            chipImg.preserveAspect = true;
            chipImg.raycastTarget = false;
            if (wild)
                UIBuild.Key(UIBuild.Text(UIBuild.Stretch("Text", guardRt, 64f, 0f, 14f, 0f), "GUARD", 28f, Color.white, TextAlignmentOptions.MidlineLeft, bold: true), "battle.guard");
            guardRt.gameObject.SetActive(false);

            // "TURN" tag shown while it is this animal's turn.
            var turnTag = wild
                ? UIBuild.Rect("TurnBadge", card, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(170f, 46f))
                : UIBuild.Rect("TurnBadge", card, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(140f, 44f));
            UIBuild.DropShadow(turnTag, 10f, -4f, 0.25f);
            UIBuild.Round(turnTag, MaterialLibrary.Hex("#FFC93C"), 22f);
            UIBuild.Key(UIBuild.Text(UIBuild.Stretch("Text", turnTag), "TURN", 28f, Palette.Ink, TextAlignmentOptions.Center, bold: true), "battle.turn");
            turnTag.gameObject.SetActive(false);

            // Turn-order number in the corner.
            var order = UIBuild.Rect("OrderBadge", card, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(wild ? -8f : -6f, wild ? -4f : -2f), new Vector2(54f, 54f));
            UIBuild.DropShadow(order, 8f, -3f, 0.25f);
            UIBuild.Disc(order, Palette.Ink);
            var orderText = UIBuild.Text(UIBuild.Stretch("Text", order), "1", 32f, Color.white, TextAlignmentOptions.Center, bold: true);
            order.gameObject.SetActive(false);

            // Shown on animals that have fainted.
            var out_ = UIBuild.Rect("FaintedBadge", card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(wild ? 240f : 150f, 56f));
            UIBuild.Round(out_, Palette.Danger, 28f);
            UIBuild.Key(UIBuild.Text(UIBuild.Stretch("Text", out_), "FAINTED", wild ? 34f : 28f, Color.white, TextAlignmentOptions.Center, bold: true), "battle.fainted");
            out_.gameObject.SetActive(false);

            card.gameObject.AddComponent<RTLMirror>();
            var fighter = card.gameObject.AddComponent<FighterCard>();
            UIBuild.Set(fighter, "nameText", nameText);
            UIBuild.Set(fighter, "levelText", levelText);
            UIBuild.Set(fighter, "hpBar", hp);
            UIBuild.Set(fighter, "accent", accentImg);
            UIBuild.Set(fighter, "guardChip", chipGroup);
            UIBuild.Set(fighter, "body", body);
            UIBuild.Set(fighter, "portrait", portrait);
            UIBuild.Set(fighter, "portraitBackdrop", portraitBgImg);
            UIBuild.Set(fighter, "turnGlow", glow);
            UIBuild.Set(fighter, "turnBadge", turnTag.gameObject);
            UIBuild.Set(fighter, "orderBadge", order.gameObject);
            UIBuild.Set(fighter, "orderText", orderText);
            UIBuild.Set(fighter, "faintedBadge", out_.gameObject);
            UIBuild.Set(fighter, "group", group);
            return fighter;
        }

        private static BattleActionButton ActionButton(RectTransform parent, string name, int column, string label, string hint,
            string iconPath, Color face, Color lip)
        {
            const float w = 240f, h = 200f, gap = 16f;
            var pos = new Vector2(column * (w + gap), 0f);
            var button = UIBuild.CandyButton(name, parent, Vector2.zero, Vector2.zero, pos, new Vector2(w, h),
                "", face, lip, 44f, 10f, out var text, out var faceImg);
            Object.DestroyImmediate(text.gameObject);
            var root = (RectTransform)button.transform;

            var icon = UIBuild.Rect("Icon", faceImg.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(74f, 74f));
            var iconImg = icon.gameObject.AddComponent<Image>();
            iconImg.sprite = UISpriteGenerator.Load(iconPath);
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;

            var labelText = UIBuild.Label("Label", faceImg.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(228f, 50f),
                label, 42f, Color.white, TextAlignmentOptions.Center, bold: true);
            var hintText = UIBuild.Label("Hint", faceImg.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(228f, 40f),
                hint, 24f, new Color(1f, 1f, 1f, 0.92f), TextAlignmentOptions.Center);
            hintText.enableAutoSizing = true;
            hintText.fontSizeMin = 18f;
            hintText.fontSizeMax = 24f;

            var badge = UIBuild.Rect("Badge", root, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-18f, -14f), new Vector2(58f, 58f));
            UIBuild.Disc(badge, Color.white);
            var badgeText = UIBuild.Text(UIBuild.Stretch("Text", badge), "2", 34f, lip, TextAlignmentOptions.Center, bold: true);
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

        private static Material OutlineFontMaterial() =>
            LocalizationBuilder.EnsureOutlineMaterial(UIBuild.FontBold, OutlineFontPath, "Fredoka-Bold SDF Outline");

        private static BattleResultPanel BuildResultPanel(RectTransform parent)
        {
            var root = UIBuild.Stretch("ResultPanel", parent);
            root.gameObject.AddComponent<CanvasGroup>();
            var dim = UIBuild.Stretch("Dim", root);
            UIBuild.Plain(dim, new Color(0.106f, 0.149f, 0.22f, 0.25f), raycast: true);

            var card = UIBuild.Rect("Card", root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1008f, 840f));
            UIBuild.DropShadow(card, 40f, -16f, 0.28f);
            var body = UIBuild.Stretch("Body", card);
            UIBuild.Round(body, Palette.Panel, 56f, raycast: true);

            var badge = UIBuild.Rect("TitleBadge", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(440f, 96f));
            UIBuild.DropShadow(badge, 22f, -8f, 0.2f);
            var badgeImg = UIBuild.Round(badge, Palette.Primary, 48f);
            var title = UIBuild.Text(UIBuild.Stretch("Text", badge), "Victory!", 58f, Color.white, TextAlignmentOptions.Center, bold: true);

            var message = UIBuild.Label("Message", body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(940f, 120f),
                "Wolf (Lv. 5) joined your team!\nEach animal of your team gets +50 XP.", 38f, Palette.Ink, TextAlignmentOptions.Center, bold: false, wrap: true);
            message.richText = true;

            // One XP row per animal of your team.
            var rows = new BattleXpRow[3];
            for (int i = 0; i < 3; i++) rows[i] = BuildXpRow(body, i);

            var cont = UIBuild.CandyButton("ContinueButton", body, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(500f, 130f),
                "Continue", Palette.Primary, Palette.PrimaryDark, 48f, 50f, out var contLabel, out _);
            UIBuild.Key(contLabel, "result.continue");
            UIBuild.DropShadow((RectTransform)cont.transform, 26f, -10f, 0.2f);

            root.gameObject.AddComponent<RTLMirror>();
            var panel = root.gameObject.AddComponent<BattleResultPanel>();
            UIBuild.Set(panel, "card", card);
            UIBuild.Set(panel, "titleText", title);
            UIBuild.Set(panel, "messageText", message);
            UIBuild.Set(panel, "titleBadge", badgeImg);
            UIBuild.SetArray(panel, "rows", rows);
            UIBuild.Set(panel, "continueButton", cont);
            UIBuild.SetBool(panel, "blocksMapInput", false);
            root.gameObject.SetActive(false);
            return panel;
        }

        private static BattleXpRow BuildXpRow(RectTransform body, int index)
        {
            var row = UIBuild.Rect("XpRow " + (index + 1), body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -196f - index * 150f), new Vector2(940f, 138f));
            UIBuild.Round(row, Palette.PanelAlt, 34f);

            var portraitBg = UIBuild.Rect("PortraitBackdrop", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(104f, 104f));
            var portraitBgImg = UIBuild.Disc(portraitBg, Palette.Line);
            var portraitRt = UIBuild.Stretch("Portrait", portraitBg, -4f, -4f, -4f, -4f);
            portraitRt.gameObject.AddComponent<RawImage>().raycastTarget = false;
            var portrait = portraitRt.gameObject.AddComponent<AnimalPreviewImage>();
            UIBuild.SetBool(portrait, "live", false);

            var name = UIBuild.Label("Name", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(144f, -12f), new Vector2(380f, 50f),
                "Arabian Gazelle", 40f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);
            name.enableAutoSizing = true;
            name.fontSizeMin = 26f;
            name.fontSizeMax = 40f;
            var level = UIBuild.Label("Level", row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(530f, -14f), new Vector2(140f, 46f),
                "Lv. 5", 36f, Palette.Teal, TextAlignmentOptions.MidlineLeft, bold: true);
            var gain = UIBuild.Label("Gain", row, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -14f), new Vector2(230f, 46f),
                "+50 XP", 38f, Palette.Def, TextAlignmentOptions.MidlineRight, bold: true);
            var barRt = UIBuild.Rect("Bar", row, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(144f, 24f), new Vector2(520f, 26f));
            var bar = UIBuild.Bar(barRt, Palette.Line, Palette.Def);

            var chipRt = UIBuild.Rect("LevelUpChip", row, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 16f), new Vector2(230f, 48f));
            var chipGroup = chipRt.gameObject.AddComponent<CanvasGroup>();
            chipGroup.blocksRaycasts = false;
            UIBuild.Round(chipRt, Palette.Primary, 24f);
            UIBuild.Key(UIBuild.Text(UIBuild.Stretch("Text", chipRt), "LEVEL UP!", 28f, Color.white, TextAlignmentOptions.Center, bold: true), "result.levelup");
            chipRt.gameObject.SetActive(false);

            row.gameObject.AddComponent<RTLMirror>();
            var xp = row.gameObject.AddComponent<BattleXpRow>();
            UIBuild.Set(xp, "portrait", portrait);
            UIBuild.Set(xp, "portraitBackdrop", portraitBgImg);
            UIBuild.Set(xp, "nameText", name);
            UIBuild.Set(xp, "levelText", level);
            UIBuild.Set(xp, "gainText", gain);
            UIBuild.Set(xp, "xpBar", bar);
            UIBuild.Set(xp, "levelUpChip", chipGroup);
            return xp;
        }
    }
}
