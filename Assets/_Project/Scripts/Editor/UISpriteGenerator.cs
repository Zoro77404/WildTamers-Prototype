using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Generates the flat UI sprites (rounded panels, circles, icons) and a few effect textures as PNGs,
    /// so the whole UI shares one clean, resolution-independent style.
    /// </summary>
    public static class UISpriteGenerator
    {
        public const string Folder = "Assets/_Project/Textures/UI";

        public const string RoundedRect = Folder + "/UI_RoundedRect.png";
        public const string Circle = Folder + "/UI_Circle.png";
        public const string Shadow = Folder + "/UI_Shadow.png";
        public const string Paw = Folder + "/UI_Paw.png";
        public const string Close = Folder + "/UI_Close.png";
        public const string StarterBackground = Folder + "/UI_StarterBackground.png";
        public const string Claw = Folder + "/UI_Claw.png";
        public const string Shield = Folder + "/UI_Shield.png";
        public const string Star = Folder + "/UI_Star.png";
        public const string Dash = Folder + "/UI_Dash.png";
        public const string BlobShadow = "Assets/_Project/Textures/FX_BlobShadow.png";
        public const string SoftDot = "Assets/_Project/Textures/FX_SoftDot.png";

        /// <summary>Corner radius (pixels) baked into <see cref="RoundedRect"/>.</summary>
        public const float RoundedRadius = 60f;

        [MenuItem("Wild Tamers/Build/UI Sprites")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Folder);
            WriteSdf(RoundedRect, 160, 160, (x, y) => RoundedBox(x, y, 80, 80, 80, 80, RoundedRadius));
            WriteSdf(Circle, 256, 256, (x, y) => Length(x - 128, y - 128) - 126f);
            WriteShadow(Shadow, 192, 48f, 40f);
            WriteSdf(Paw, 256, 256, PawSdf);
            WriteSdf(Close, 128, 128, (x, y) => Mathf.Min(
                Segment(x, y, 36, 36, 92, 92) - 9f,
                Segment(x, y, 36, 92, 92, 36) - 9f));
            WriteGradient(StarterBackground, new Color32(0x7F, 0xD8, 0xCF, 0xFF), new Color32(0xFF, 0xF1, 0xD2, 0xFF));
            WriteRadial(BlobShadow, 128, 0.85f, 2.2f);
            WriteRadial(SoftDot, 64, 1f, 1.6f);
            AssetDatabase.Refresh();

            ConfigureSprite(RoundedRect, new Vector4(70, 70, 70, 70));
            ConfigureSprite(Circle, Vector4.zero);
            ConfigureSprite(Shadow, new Vector4(90, 90, 90, 90));
            ConfigureSprite(Paw, Vector4.zero);
            ConfigureSprite(Close, Vector4.zero);
            ConfigureSprite(StarterBackground, Vector4.zero);
            ConfigureTexture(BlobShadow);
            ConfigureTexture(SoftDot);
            GenerateBattleIcons();
            AssetDatabase.SaveAssets();
        }

        /// <summary>White icons for the battle buttons (claw = attack, shield = defend, star = skill, dash = run).</summary>
        public static void GenerateBattleIcons()
        {
            Directory.CreateDirectory(Folder);
            WriteSdf(Claw, 256, 256, ClawSdf);
            WriteSdf(Shield, 256, 256, ShieldSdf);
            WriteSdf(Star, 256, 256, (x, y) => Star5(x - 128f, y - 122f, 112f, 0.5f) - 10f);
            WriteSdf(Dash, 256, 256, DashSdf);
            AssetDatabase.Refresh();
            foreach (var path in new[] { Claw, Shield, Star, Dash }) ConfigureSprite(path, Vector4.zero);
        }

        public static Sprite Load(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

        // ---------- Shapes ----------

        private static float PawSdf(float x, float y)
        {
            // y grows upward in texture space.
            float pad = Ellipse(x - 128, y - 92, 64, 54);
            float d = pad;
            d = Mathf.Min(d, Ellipse(x - 58, y - 158, 25, 31));
            d = Mathf.Min(d, Ellipse(x - 100, y - 200, 26, 32));
            d = Mathf.Min(d, Ellipse(x - 156, y - 200, 26, 32));
            d = Mathf.Min(d, Ellipse(x - 198, y - 158, 25, 31));
            return d;
        }

        private static float ClawSdf(float x, float y)
        {
            // Three tapered slashes, like a claw swipe.
            float d = float.MaxValue;
            for (int i = 0; i < 3; i++)
            {
                float ox = (i - 1) * 54f;
                float oy = i == 1 ? 10f : 0f;
                d = Mathf.Min(d, TaperedSegment(x, y, 100f + ox, 222f + oy, 152f + ox, 34f + oy, 19f));
            }
            return d;
        }

        private static float ShieldSdf(float x, float y)
        {
            // Heater shield: flat top with rounded corners, sides curving into a point at the bottom.
            float box = RoundedBox(x, y, 128f, 126f, 76f, 106f, 26f);
            float lens = Mathf.Max(Length(x - 180f, y - 150f) - 128f, Length(x - 76f, y - 150f) - 128f);
            return Mathf.Max(box, Mathf.Min(lens, 150f - y));
        }

        private static float DashSdf(float x, float y)
        {
            // Arrow running right with two speed lines behind it.
            float d = Segment(x, y, 96f, 128f, 206f, 128f) - 17f;
            d = Mathf.Min(d, Segment(x, y, 210f, 128f, 158f, 182f) - 17f);
            d = Mathf.Min(d, Segment(x, y, 210f, 128f, 158f, 74f) - 17f);
            d = Mathf.Min(d, Segment(x, y, 30f, 182f, 92f, 182f) - 12f);
            d = Mathf.Min(d, Segment(x, y, 46f, 74f, 100f, 74f) - 12f);
            return d;
        }

        /// <summary>Segment whose thickness swells in the middle and tapers to points at both ends.</summary>
        private static float TaperedSegment(float px, float py, float ax, float ay, float bx, float by, float radius)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float r = radius * Mathf.Pow(Mathf.Sin(h * Mathf.PI), 0.6f);
            return Length(pax - bax * h, pay - bay * h) - r;
        }

        /// <summary>Five-pointed star (Inigo Quilez' sdStar5); <paramref name="inner"/> sets how deep the points are.</summary>
        private static float Star5(float px, float py, float r, float inner)
        {
            const float k1x = 0.809016994375f, k1y = -0.587785252292f;
            const float k2x = -k1x, k2y = k1y;
            px = Mathf.Abs(px);
            float d1 = Mathf.Max(k1x * px + k1y * py, 0f);
            px -= 2f * d1 * k1x; py -= 2f * d1 * k1y;
            float d2 = Mathf.Max(k2x * px + k2y * py, 0f);
            px -= 2f * d2 * k2x; py -= 2f * d2 * k2y;
            px = Mathf.Abs(px);
            py -= r;
            float bax = inner * -k1y, bay = inner * k1x - 1f;
            float h = Mathf.Clamp((px * bax + py * bay) / (bax * bax + bay * bay), 0f, r);
            return Length(px - bax * h, py - bay * h) * Mathf.Sign(py * bax - px * bay);
        }

        private static float RoundedBox(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x - cx) - (hx - r);
            float qy = Mathf.Abs(y - cy) - (hy - r);
            return Length(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static float Ellipse(float x, float y, float rx, float ry)
        {
            float k = Length(x / rx, y / ry);
            return (k - 1f) * Mathf.Min(rx, ry);
        }

        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax, pay = py - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            return Length(pax - bax * h, pay - bay * h);
        }

        private static float Length(float x, float y) => Mathf.Sqrt(x * x + y * y);

        // ---------- Writers ----------

        private static void WriteSdf(string path, int w, int h, Func<float, float, float> sdf)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = sdf(x + 0.5f, y + 0.5f);
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
                px[y * w + x] = new Color32(255, 255, 255, a);
            }
            Save(tex, px, path);
        }

        private static void WriteShadow(string path, int size, float radius, float blur)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedBox(x + 0.5f, y + 0.5f, half, half, half - blur, half - blur, radius);
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + blur * 0.15f) / blur));
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            Save(tex, px, path);
        }

        private static void WriteGradient(string path, Color32 top, Color32 bottom)
        {
            const int w = 4, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                var c = Color32.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, y / (h - 1f)));
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            Save(tex, px, path);
        }

        private static void WriteRadial(string path, int size, float maxAlpha, float power)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Length(x + 0.5f - half, y + 0.5f - half) / half;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), power) * maxAlpha;
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
            Save(tex, px, path);
        }

        private static void Save(Texture2D tex, Color32[] px, string path)
        {
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void ConfigureSprite(string path, Vector4 border)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 100f;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
