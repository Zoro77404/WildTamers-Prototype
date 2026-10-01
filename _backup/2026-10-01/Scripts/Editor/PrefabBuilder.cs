using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.Map;
using WildTamers.Player;
using WildTamers.UI;

namespace WildTamers.EditorTools
{
    /// <summary>Builds the reusable prefabs: player avatar, wild animal wrapper, spawn puff, destination marker and UI cards.</summary>
    public static class PrefabBuilder
    {
        public const string PlayerPrefab = "Assets/_Project/Prefabs/Player/Player.prefab";
        public const string MarkerPrefab = "Assets/_Project/Prefabs/Player/DestinationMarker.prefab";
        public const string WildAnimalPrefab = "Assets/_Project/Prefabs/Animals/WildAnimal.prefab";
        public const string PuffPrefab = "Assets/_Project/Prefabs/Map/SpawnPuff.prefab";
        public const string StarterCardPrefab = "Assets/_Project/Prefabs/UI/StarterCard.prefab";
        public const string TeamRowPrefab = "Assets/_Project/Prefabs/UI/TeamRow.prefab";
        private const string MeshFolder = "Assets/_Project/Art/Meshes";

        public static Material RingMaterial => AssetDatabase.LoadAssetAtPath<Material>(MaterialLibrary.FxFolder + "/FX_Ring.mat");
        public static Material BlobMaterial => AssetDatabase.LoadAssetAtPath<Material>(MaterialLibrary.FxFolder + "/FX_BlobShadow.mat");

        [MenuItem("Wild Tamers/Build/Prefabs")]
        public static void BuildAll()
        {
            UIBuild.LoadAssets();
            Directory.CreateDirectory(MeshFolder);
            BuildFxMaterials();
            BuildPuff();
            BuildWildAnimal();
            BuildPlayer();
            BuildMarker();
            BuildStarterCard();
            BuildTeamRow();
            AssetDatabase.SaveAssets();
        }

        private static void BuildFxMaterials()
        {
            MaterialLibrary.UnlitTransparent(MaterialLibrary.FxFolder + "/FX_Ring.mat", Color.white, null, 10);
            var blob = AssetDatabase.LoadAssetAtPath<Texture2D>(UISpriteGenerator.BlobShadow);
            MaterialLibrary.UnlitTransparent(MaterialLibrary.FxFolder + "/FX_BlobShadow.mat", new Color(0.12f, 0.18f, 0.12f, 0.42f), blob, -10);
            MaterialLibrary.ParticleUnlit(MaterialLibrary.FxFolder + "/FX_Puff.mat", AssetDatabase.LoadAssetAtPath<Texture2D>(UISpriteGenerator.SoftDot));
        }

        // ---------- World prefabs ----------

        private static void BuildPuff()
        {
            var go = new GameObject("SpawnPuff");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, MaterialLibrary.Hex("#FFF4D6"));
            main.gravityModifier = -0.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.limitVelocityOverLifetime;
            vel.enabled = true;
            vel.limit = 1.5f;
            vel.dampen = 0.25f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.3f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialLibrary.FxFolder + "/FX_Puff.mat");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            Save(go, PuffPrefab);
        }

        private static void BuildWildAnimal()
        {
            var root = new GameObject("WildAnimal");
            var wild = root.AddComponent<WildAnimal>();
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.isTrigger = true;
            capsule.radius = 1f;
            capsule.height = 2f;
            capsule.center = Vector3.up;

            var modelRoot = new GameObject("ModelRoot");
            modelRoot.transform.SetParent(root.transform, false);
            var hop = modelRoot.AddComponent<IdleHop>();

            var shadow = BlobShadow(root.transform, 1.6f);

            var ring = new GameObject("RangeRing");
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            ring.AddComponent<MeshFilter>();
            var ringRenderer = ring.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = RingMaterial;
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;

            UIBuild.Set(wild, "modelRoot", modelRoot.transform);
            UIBuild.Set(wild, "hop", hop);
            UIBuild.Set(wild, "blobShadow", shadow);
            UIBuild.Set(wild, "rangeRing", ringRenderer);
            UIBuild.Set(wild, "tapCollider", capsule);
            UIBuild.Set(wild, "puffPrefab", AssetDatabase.LoadAssetAtPath<ParticleSystem>(PuffPrefab));

            int layer = LayerMask.NameToLayer(GameLayers.AnimalsName);
            if (layer >= 0) GameLayers.SetLayerRecursively(root, layer);
            Save(root, WildAnimalPrefab);
        }

        private static Transform BlobShadow(Transform parent, float size)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "BlobShadow";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(size, size, 1f);
            var r = quad.GetComponent<MeshRenderer>();
            r.sharedMaterial = BlobMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return quad.transform;
        }

        private static void BuildPlayer()
        {
            // Faceted chibi trainer built from low-poly primitives, one mesh with a material per part.
            string[] parts = { "Skin", "Shirt", "Pants", "Cap", "Backpack", "Shoes", "Eyes" };
            string[] colors = { "#FFD2AE", "#4D8DF7", "#3D4A6B", "#FF5D5D", "#FFC93C", "#2F3447", "#2B2D42" };
            var mats = new Material[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                mats[i] = MaterialLibrary.Lit($"{MaterialLibrary.PlayerFolder}/Player_{parts[i]}.mat", MaterialLibrary.Hex(colors[i]), 0.15f);

            var mb = new MeshBuilder(parts.Length);
            const int skin = 0, shirt = 1, pants = 2, cap = 3, pack = 4, shoes = 5, eyes = 6;
            for (int s = -1; s <= 1; s += 2)
            {
                mb.Sphere(shoes, new Vector3(0.2f * s, 0.11f, 0.06f), new Vector3(0.19f, 0.12f, 0.27f), 4, 8);
                mb.Cylinder(pants, new Vector3(0.2f * s, 0.12f, 0f), 0.15f, 0.5f, 8);
                mb.Sphere(shirt, new Vector3(0.56f * s, 0.98f, 0.02f), new Vector3(0.14f, 0.32f, 0.14f), 5, 8, Quaternion.Euler(0f, 0f, 16f * s));
                mb.Sphere(skin, new Vector3(0.65f * s, 0.66f, 0.04f), Vector3.one * 0.12f, 4, 8);
                mb.Sphere(eyes, new Vector3(0.18f * s, 1.88f, 0.47f), new Vector3(0.07f, 0.1f, 0.05f), 4, 6);
            }
            mb.Cylinder(shirt, new Vector3(0f, 0.55f, 0f), 0.5f, 0.72f, 10, shirt, 0.42f);
            mb.Sphere(shirt, new Vector3(0f, 1.25f, 0f), new Vector3(0.43f, 0.2f, 0.43f), 4, 10);
            mb.Sphere(skin, new Vector3(0f, 1.84f, 0f), new Vector3(0.55f, 0.52f, 0.53f), 7, 12);
            mb.Sphere(cap, new Vector3(0f, 2.04f, -0.03f), new Vector3(0.58f, 0.36f, 0.58f), 5, 12);
            mb.Box(cap, new Rect(-0.33f, 0.2f, 0.66f, 0.62f), 1.97f, 2.03f);
            mb.Box(pack, new Rect(-0.36f, -0.7f, 0.72f, 0.34f), 0.72f, 1.36f);
            mb.Box(pack, new Rect(-0.3f, -0.76f, 0.6f, 0.08f), 0.8f, 1.1f);
            var mesh = mb.Build("PlayerAvatar", out var used);
            mesh = SaveMesh(mesh, $"{MeshFolder}/PlayerAvatar.asset");

            var root = new GameObject("Player");
            var avatar = root.AddComponent<PlayerAvatar>();
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var model = new GameObject("Model");
            model.transform.SetParent(visual.transform, false);
            // Larger than life, like map avatars in location games, so it reads well from the camera.
            model.transform.localScale = Vector3.one * 1.6f;
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = model.AddComponent<MeshRenderer>();
            var usedMats = new Material[used.Count];
            for (int i = 0; i < used.Count; i++) usedMats[i] = mats[used[i]];
            mr.sharedMaterials = usedMats;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            BlobShadow(root.transform, 2.9f);

            var range = new GameObject("RangeIndicator");
            range.transform.SetParent(root.transform, false);
            range.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            range.AddComponent<MeshFilter>();
            var rangeRenderer = range.AddComponent<MeshRenderer>();
            rangeRenderer.sharedMaterials = new[] { RingMaterial, RingMaterial };
            rangeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            rangeRenderer.receiveShadows = false;
            var indicator = range.AddComponent<RangeIndicator>();

            var ping = new GameObject("PingRing");
            ping.transform.SetParent(range.transform, false);
            ping.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            ping.AddComponent<MeshFilter>();
            var pingRenderer = ping.AddComponent<MeshRenderer>();
            pingRenderer.sharedMaterial = RingMaterial;
            pingRenderer.shadowCastingMode = ShadowCastingMode.Off;
            pingRenderer.receiveShadows = false;
            UIBuild.Set(indicator, "pingRing", pingRenderer);

            UIBuild.Set(avatar, "visual", visual.transform);
            Save(root, PlayerPrefab);
        }

        private static void BuildMarker()
        {
            var root = new GameObject("DestinationMarker");
            var marker = root.AddComponent<DestinationMarker>();

            var ring = new GameObject("Ring");
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            var filter = ring.AddComponent<MeshFilter>();
            var rr = ring.AddComponent<MeshRenderer>();
            rr.sharedMaterial = RingMaterial;
            rr.shadowCastingMode = ShadowCastingMode.Off;

            var mb = new MeshBuilder(1);
            mb.Blob(0, Vector3.zero, new Vector3(0.32f, 0.5f, 0.32f), 0f, null);
            var pinMesh = SaveMesh(mb.Build("MarkerGem", out _), $"{MeshFolder}/MarkerGem.asset");
            var pin = new GameObject("Pin");
            pin.transform.SetParent(root.transform, false);
            pin.AddComponent<MeshFilter>().sharedMesh = pinMesh;
            var pr = pin.AddComponent<MeshRenderer>();
            pr.sharedMaterial = MaterialLibrary.Lit($"{MaterialLibrary.PlayerFolder}/Marker_Gem.mat", MaterialLibrary.Hex("#FF8A3D"), 0.4f);
            pr.shadowCastingMode = ShadowCastingMode.Off;

            UIBuild.Set(marker, "ring", filter);
            UIBuild.Set(marker, "pin", pin.transform);
            Save(root, MarkerPrefab);
        }

        // ---------- UI prefabs ----------

        private static void BuildStarterCard()
        {
            var root = new GameObject("StarterCard", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(980f, 480f);
            root.AddComponent<CanvasGroup>();

            var body = UIBuild.Stretch("Body", rt);
            UIBuild.Round(body, Palette.Panel, 44f);
            UIBuild.DropShadow(body, 30f, -12f, 0.18f);

            var accent = UIBuild.Rect("Accent", body, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(18f, 480f));
            var accentImg = UIBuild.Round(accent, Palette.Primary, 9f);
            accent.anchorMin = new Vector2(0f, 0f);
            accent.anchorMax = new Vector2(0f, 1f);
            accent.offsetMin = new Vector2(0f, 40f);
            accent.offsetMax = new Vector2(18f, -40f);

            var backdrop = UIBuild.Rect("PreviewBackdrop", body, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(390f, 400f));
            var backdropImg = UIBuild.Round(backdrop, Palette.PanelAlt, 40f);
            var previewRt = UIBuild.Stretch("Preview", backdrop);
            previewRt.gameObject.AddComponent<RawImage>().raycastTarget = false;
            var preview = previewRt.gameObject.AddComponent<AnimalPreviewImage>();

            const float x = 462f;
            var name = UIBuild.Label("Name", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -28f), new Vector2(480f, 76f),
                "Fox", 64f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);
            var style = UIBuild.Label("Style", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -100f), new Vector2(480f, 44f),
                "Fast & fragile", 34f, Palette.Muted, TextAlignmentOptions.MidlineLeft);
            var moves = UIBuild.Label("Moves", body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -140f), new Vector2(480f, 40f),
                "Scratch • Fox Fire", 28f, Palette.Ink, TextAlignmentOptions.MidlineLeft);

            var stats = BuildStats(body, new Vector2(x, -186f), 480f, 30f, 8f, 27f);

            var choose = UIBuild.CandyButton("Choose", body, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(x, 30f), new Vector2(480f, 100f),
                "Choose", Palette.Primary, Palette.PrimaryDark, 40f, 42f, out _, out var faceImage);
            // Face and lip are tinted with the animal's color at runtime.

            var card = root.AddComponent<StarterCard>();
            UIBuild.Set(card, "preview", preview);
            UIBuild.Set(card, "previewBackdrop", backdropImg);
            UIBuild.Set(card, "accentStripe", accentImg);
            UIBuild.Set(card, "nameText", name);
            UIBuild.Set(card, "styleText", style);
            UIBuild.Set(card, "moveText", moves);
            UIBuild.Set(card, "stats", stats);
            UIBuild.Set(card, "chooseButton", choose);
            UIBuild.Set(card, "chooseButtonImage", faceImage);
            UIBuild.Set(card, "chooseButtonLip", choose.GetComponent<Image>());
            SetPreviewBackground(preview, Color.white);
            SetUILayer(root);
            Save(root, StarterCardPrefab);
        }

        /// <summary>Four stat rows stacked from a top-left point.</summary>
        public static AnimalStatsView BuildStats(RectTransform parent, Vector2 topLeft, float width, float rowHeight, float spacing, float fontSize)
        {
            var holder = UIBuild.Rect("Stats", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft,
                new Vector2(width, rowHeight * 4f + spacing * 3f));
            string[] labels = { "HP", "ATK", "DEF", "SPD" };
            Color[] colors = { Palette.Hp, Palette.Atk, Palette.Def, Palette.Spd };
            var bars = new StatBar[4];
            for (int i = 0; i < 4; i++)
            {
                bars[i] = UIBuild.StatRow(labels[i], holder, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, -i * (rowHeight + spacing)), width, rowHeight, labels[i], colors[i], fontSize * 2.6f, fontSize * 2.4f, fontSize);
            }
            var view = holder.gameObject.AddComponent<AnimalStatsView>();
            UIBuild.Set(view, "hp", bars[0]);
            UIBuild.Set(view, "attack", bars[1]);
            UIBuild.Set(view, "defense", bars[2]);
            UIBuild.Set(view, "speed", bars[3]);
            return view;
        }

        private static void BuildTeamRow()
        {
            var root = new GameObject("TeamRow", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(880f, 196f);
            var le = root.AddComponent<LayoutElement>();
            le.preferredHeight = 196f;
            le.minHeight = 196f;

            var bg = UIBuild.Stretch("Background", rt);
            UIBuild.Round(bg, Palette.PanelAlt, 36f);
            var highlight = UIBuild.Stretch("Highlight", rt);
            var hlImg = UIBuild.Round(highlight, new Color(Palette.Teal.r, Palette.Teal.g, Palette.Teal.b, 0.16f), 36f);

            var portraitBg = UIBuild.Rect("PortraitBackdrop", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(150f, 150f));
            var portraitBgImg = UIBuild.Disc(portraitBg, Palette.Line);
            var portraitRt = UIBuild.Stretch("Portrait", portraitBg, -6f, -6f, -6f, -6f);
            portraitRt.gameObject.AddComponent<RawImage>().raycastTarget = false;
            var portrait = portraitRt.gameObject.AddComponent<AnimalPreviewImage>();
            UIBuild.SetBool(portrait, "live", false);

            var name = UIBuild.Label("Name", rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(206f, -26f), new Vector2(420f, 64f),
                "Fox", 52f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);
            var level = UIBuild.Label("Level", rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(206f, -86f), new Vector2(200f, 44f),
                "Lv. 5", 36f, Palette.Muted, TextAlignmentOptions.MidlineLeft);

            var hpRt = UIBuild.Rect("HP", rt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(206f, 34f), new Vector2(420f, 30f));
            var hpBar = UIBuild.Bar(hpRt, Palette.Line, Palette.HpGood);
            var hpText = UIBuild.Label("HPText", rt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(642f, 26f), new Vector2(210f, 46f),
                "HP 32/32", 32f, Palette.Ink, TextAlignmentOptions.MidlineLeft, bold: true);

            var badge = UIBuild.Rect("ActiveBadge", rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -26f), new Vector2(170f, 56f));
            UIBuild.Round(badge, Palette.Teal, 28f);
            UIBuild.Text(UIBuild.Stretch("Text", badge), "ACTIVE", 28f, Color.white, TextAlignmentOptions.Center, bold: true);

            var row = root.AddComponent<TeamRow>();
            UIBuild.Set(row, "portrait", portrait);
            UIBuild.Set(row, "portraitBackdrop", portraitBgImg);
            UIBuild.Set(row, "nameText", name);
            UIBuild.Set(row, "levelText", level);
            UIBuild.Set(row, "hpBar", hpBar);
            UIBuild.Set(row, "hpText", hpText);
            UIBuild.Set(row, "activeBadge", badge.gameObject);
            UIBuild.Set(row, "highlight", hlImg);
            SetUILayer(root);
            Save(root, TeamRowPrefab);
        }

        public static void SetPreviewBackground(AnimalPreviewImage preview, Color c) => UIBuild.SetColor(preview, "background", c);

        private static void SetUILayer(GameObject root)
        {
            int ui = LayerMask.NameToLayer("UI");
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ui;
        }

        // ---------- Helpers ----------

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

        private static void Save(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
    }
}
