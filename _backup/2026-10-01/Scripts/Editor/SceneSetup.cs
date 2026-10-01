using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using WildTamers.Core;

namespace WildTamers.EditorTools
{
    /// <summary>Shared scene plumbing for the builders: layers, lighting, post-processing, canvas, event system.</summary>
    public static class SceneSetup
    {
        public const string SettingsFolder = "Assets/_Project/Settings";

        public static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            foreach (var name in new[] { GameLayers.AnimalsName, GameLayers.PreviewName })
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize; i++)
                    if (layers.GetArrayElementAtIndex(i).stringValue == name) exists = true;
                if (exists) continue;
                for (int i = 8; i < layers.arraySize; i++)
                {
                    var p = layers.GetArrayElementAtIndex(i);
                    if (!string.IsNullOrEmpty(p.stringValue)) continue;
                    p.stringValue = name;
                    break;
                }
            }
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        public static GameObject Group(string name)
        {
            var go = new GameObject(name);
            return go;
        }

        public static Light Sun(Transform parent, Color color, float intensity, Vector3 euler, float shadowStrength)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = shadowStrength;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
            go.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.sun = light;
            return light;
        }

        public static void Ambient(Color sky, Color equator, Color ground, bool fog, Color fogColor, float fogStart, float fogEnd)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
        }

        /// <summary>Soft, bright post-processing that keeps flat colors friendly.</summary>
        public static Volume GlobalVolume(Transform parent, string profileName)
        {
            Directory.CreateDirectory(SettingsFolder);
            var path = $"{SettingsFolder}/{profileName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(profile.components[i], true);
            }
            profile.components.Clear();

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.12f);
            color.contrast.Override(8f);
            color.saturation.Override(14f);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.6f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.5f);
            foreach (var c in profile.components)
            {
                c.name = c.GetType().Name;
                if (!AssetDatabase.Contains(c)) AssetDatabase.AddObjectToAsset(c, profile);
            }
            EditorUtility.SetDirty(profile);

            var go = new GameObject("Global Volume");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            return volume;
        }

        public static Camera MainCamera(Transform parent, Color background, float fov, float far)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = far;
            int preview = LayerMask.NameToLayer(GameLayers.PreviewName);
            cam.cullingMask = preview >= 0 ? ~(1 << preview) : ~0;
            go.AddComponent<AudioListener>();
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            return cam;
        }

        public static void ShadowSettings(float distance)
        {
            var urp = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (urp == null) return;
            urp.shadowDistance = distance;
            urp.shadowCascadeCount = 2;
            EditorUtility.SetDirty(urp);
        }

        public static Canvas Canvas(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EventSystem(Transform parent)
        {
            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            // The project's action asset has the standard "UI" map (Point, Click, ScrollWheel, ...).
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (actions != null) module.actionsAsset = actions;
        }

        public static void ClearScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects()) Object.DestroyImmediate(root);
        }
    }
}
