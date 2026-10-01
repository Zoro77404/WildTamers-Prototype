using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WildTamers.Map;

namespace WildTamers.EditorTools
{
    /// <summary>Creates (or updates) the project's flat-colored URP materials.</summary>
    public static class MaterialLibrary
    {
        public const string MapFolder = "Assets/_Project/Materials/Map";
        public const string PlayerFolder = "Assets/_Project/Materials/Player";
        public const string FxFolder = "Assets/_Project/Materials/FX";

        // sRGB palette for the fake city (order = MapMaterial enum).
        private static readonly string[] MapColors =
        {
            "#A8DB72", // Grass
            "#86C95C", // Park
            "#EBDDBB", // Plaza
            "#F4E6C2", // Path
            "#EAE4D6", // Sidewalk
            "#8E98AD", // Road
            "#FFFFFF", // RoadLine
            "#63C5EF", // Water
            "#F2DFA7", // Sand
            "#F7C9A9", // Building0 peach
            "#FBE3A7", // Building1 butter
            "#BCD9F0", // Building2 sky
            "#D9C9F2", // Building3 lavender
            "#F5B7C5", // Building4 pink
            "#C9EBCB", // Building5 mint
            "#EEE9E1", // RoofFlat
            "#E8735A", // RoofRed
            "#6E83D1", // RoofBlue
            "#9A6B4B", // Trunk
            "#63BF5B", // Leaves0
            "#8DD45A", // Leaves1
            "#3FA46A", // Leaves2
            "#B4BAC4", // Rock
            "#FF8FB8", // FlowerPink
            "#FFD54F", // FlowerYellow
            "#FFFFFF", // FlowerWhite
            "#C48A5A", // Wood
            "#EFA587", // RoofTint0 peach
            "#F1C766", // RoofTint1 butter
            "#8DB9E4", // RoofTint2 sky
            "#B49CE6", // RoofTint3 lavender
            "#EC8EA5", // RoofTint4 pink
            "#93D49A", // RoofTint5 mint
        };

        public static Material[] BuildMapMaterials()
        {
            Directory.CreateDirectory(MapFolder);
            var names = System.Enum.GetNames(typeof(MapMaterial));
            var mats = new Material[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                float smooth = names[i] == nameof(MapMaterial.Water) ? 0.65f : 0.08f;
                mats[i] = Lit($"{MapFolder}/Map_{names[i]}.mat", Hex(MapColors[i]), smooth);
            }
            return mats;
        }

        public static Material Lit(string path, Color color, float smoothness = 0.1f)
        {
            var mat = LoadOrCreate(path, "Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            bool flat = smoothness < 0.3f;
            mat.SetFloat("_SpecularHighlights", flat ? 0f : 1f);
            mat.SetFloat("_EnvironmentReflections", flat ? 0f : 1f);
            SetKeyword(mat, "_SPECULARHIGHLIGHTS_OFF", flat);
            SetKeyword(mat, "_ENVIRONMENTREFLECTIONS_OFF", flat);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Unlit, alpha-blended (rings, blob shadows).</summary>
        public static Material UnlitTransparent(string path, Color color, Texture texture = null, int queueOffset = 0)
        {
            var mat = LoadOrCreate(path, "Universal Render Pipeline/Unlit");
            mat.SetColor("_BaseColor", color);
            if (texture != null) mat.SetTexture("_BaseMap", texture);
            MakeTransparent(mat);
            mat.renderQueue = (int)RenderQueue.Transparent + queueOffset;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material ParticleUnlit(string path, Texture texture)
        {
            var mat = LoadOrCreate(path, "Universal Render Pipeline/Particles/Unlit");
            mat.SetTexture("_BaseMap", texture);
            mat.SetColor("_BaseColor", Color.white);
            MakeTransparent(mat);
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("ShadowCaster", false);
        }

        private static Material LoadOrCreate(string path, string shaderName)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var shader = Shader.Find(shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader) mat.shader = shader;
            return mat;
        }

        private static void SetKeyword(Material m, string keyword, bool on)
        {
            if (on) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
