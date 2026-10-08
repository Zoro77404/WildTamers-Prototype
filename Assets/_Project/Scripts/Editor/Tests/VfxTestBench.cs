using System.IO;
using UnityEditor;
using UnityEngine;
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
            var rotation = effect.faceCamera ? VfxInstance.FacingCamera(camera, Quaternion.identity) : Quaternion.identity;
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
    }
}
