using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WildTamers.Animals;
using WildTamers.Vfx;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Debug helper: renders a strip of frames of a VFX effect (or a single baked clip) from a camera into a PNG,
    /// so effects can be judged frame by frame without entering Play Mode. Used from execute_code / the Debug menu.
    /// </summary>
    public static class VfxTestBench
    {
        public const string ShotFolder = "Temp/Shots";

        /// <summary>Wraps one clip in a throwaway single-layer effect.</summary>
        public static VfxEffect Single(VfxClip clip, float speed = 1f)
        {
            var fx = ScriptableObject.CreateInstance<VfxEffect>();
            fx.name = clip.name;
            fx.layers = new[] { new VfxEffect.Layer { clip = clip, speed = speed, fadeOut = 0f } };
            fx.faceCamera = true;
            return fx;
        }

        /// <summary>
        /// Plays <paramref name="effect"/> at <paramref name="position"/> and renders it at each time in <paramref name="times"/>
        /// (effect seconds) from <paramref name="camera"/>; one tile per time, side by side.
        /// </summary>
        public static string Strip(VfxEffect effect, Camera camera, Vector3 position, float[] times, string fileName,
            float scale = 1f, Color? tint = null, float recolor = 0f, int tile = 360, int tileHeight = 0)
        {
            Directory.CreateDirectory(ShotFolder);
            if (tileHeight <= 0) tileHeight = tile;
            var rotation = effect.faceCamera ? VfxInstance.FacingCamera(camera, Quaternion.identity, effect.leanToCamera) : Quaternion.identity;
            var instance = VfxInstance.Spawn(effect, position, rotation, scale, tint ?? Color.white, recolor, 1f, manualTick: true);
            var rt = new RenderTexture(tile, tileHeight, 24);
            var sheet = new Texture2D(tile * times.Length, tileHeight, TextureFormat.RGB24, false);
            var oldTarget = camera.targetTexture;
            try
            {
                for (int i = 0; i < times.Length; i++)
                {
                    instance.Seek(times[i]);
                    camera.targetTexture = rt;
                    // The first render after switching targets can come back stale in URP; render twice.
                    camera.Render();
                    camera.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, tile, tileHeight), tile * i, 0);
                    RenderTexture.active = null;
                }
                sheet.Apply();
                var path = $"{ShotFolder}/{fileName}.png";
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                return path;
            }
            finally
            {
                camera.targetTexture = oldTarget;
                instance.Dispose();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(sheet);
            }
        }

        /// <summary>
        /// Edit-mode battle check (Battle scene open): stand-in models on the middle party spot and the boss spot, the animal's
        /// special placed exactly like in battle, rendered from the battle camera at <paramref name="offsets"/> seconds around
        /// the hit. A red line marks the bottom of the boss's health card (anything above it is hidden by the UI).
        /// </summary>
        public static string BattleStrip(AnimalData attackerData, bool bossAttacks, float[] offsets, string fileName,
            string targetPrefab = "Assets/_Project/Prefabs/Animals/ArabianWolf.prefab", float cardBottom = 0.796f, int width = 360)
        {
            var controller = Object.FindFirstObjectByType<Battle.BattleController>();
            if (controller == null) return "open the Battle scene first";
            var so = new SerializedObject(controller);
            var wild = (Component)so.FindProperty("wildActor").objectReferenceValue;
            var middle = (Component)so.FindProperty("playerActors").GetArrayElementAtIndex(1).objectReferenceValue;
            var camera = Camera.main;
            var attackerSpot = bossAttacks ? wild.transform : middle.transform;
            var targetSpot = bossAttacks ? middle.transform : wild.transform;

            var models = new List<GameObject>
            {
                StandIn(attackerData.prefab, attackerSpot),
                StandIn(AssetDatabase.LoadAssetAtPath<GameObject>(targetPrefab), targetSpot),
            };
            float targetHeight = Height(models[1]);
            var fx = attackerData.special;
            var instance = SpecialAttackPlayer.Spawn(fx, attackerSpot.position, targetSpot.position + Vector3.up * targetHeight * 0.5f,
                targetSpot.position, camera, SpecialAttackPlayer.SizeFor(fx.spawnAt == VfxAnchor.Attacker ? Height(models[0]) : targetHeight), true);
            int height = Mathf.RoundToInt(width * 16f / 9f);
            var rt = new RenderTexture(width, height, 24);
            var sheet = new Texture2D(width * offsets.Length, height, TextureFormat.RGB24, false);
            try
            {
                for (int i = 0; i < offsets.Length; i++)
                {
                    if (instance != null) instance.Seek(fx.vfx.hitTime + offsets[i] * fx.speed);
                    camera.targetTexture = rt;
                    camera.Render();
                    camera.Render();
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, width, height), width * i, 0);
                    RenderTexture.active = null;
                    int line = Mathf.RoundToInt(cardBottom * height);
                    for (int x = 0; x < width; x++) sheet.SetPixel(width * i + x, line, Color.red);
                }
                sheet.Apply();
                Directory.CreateDirectory(ShotFolder);
                var path = $"{ShotFolder}/{fileName}.png";
                File.WriteAllBytes(path, sheet.EncodeToPNG());
                return path + $" (target {targetHeight:0.0} m)";
            }
            finally
            {
                camera.targetTexture = null;
                if (instance != null) instance.Dispose();
                foreach (var m in models) if (m != null) Object.DestroyImmediate(m);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(sheet);
            }
        }

        /// <summary>The animal model as its battle stage shows it (the stage's display scale and height cap).</summary>
        private static GameObject StandIn(GameObject prefab, Transform spot)
        {
            if (prefab == null) return null;
            var stage = new SerializedObject(spot.GetComponent<Battle.BattleActor>());
            float display = stage.FindProperty("displayScale").floatValue;
            float maxHeight = stage.FindProperty("maxHeight").floatValue;
            var go = Object.Instantiate(prefab, spot.position, spot.rotation);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.localScale = Vector3.one * display;
            float h = Height(go);
            if (h > maxHeight) go.transform.localScale *= maxHeight / h;
            return go;
        }

        private static float Height(GameObject go)
        {
            if (go == null) return 2.4f;
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return 2.4f;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b.max.y - go.transform.position.y;
        }
    }
}
