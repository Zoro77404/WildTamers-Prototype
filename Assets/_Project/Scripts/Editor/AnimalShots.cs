using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WildTamers.Animals;

namespace WildTamers.EditorTools
{
    /// <summary>
    /// Editor-only helper that renders animal models into a contact-sheet PNG (Temp/Shots) so new models
    /// and recolors can be judged without entering Play Mode.
    /// </summary>
    public static class AnimalShots
    {
        public const string OutputFolder = "Temp/Shots";

        [MenuItem("Wild Tamers/Debug/Render Animal Contact Sheet")]
        public static void RenderRoster()
        {
            var db = AssetDatabase.LoadAssetAtPath<AnimalDatabase>("Assets/_Project/Resources/AnimalDatabase.asset");
            var models = db.Animals.Where(a => a != null && a.prefab != null).Select(a => a.prefab).ToList();
            Debug.Log("[Wild Tamers] Contact sheet: " + RenderSheet(models, "roster", 4));
        }

        /// <summary>Renders each model (3/4 view, idle pose) into one grid image. Returns the PNG path.</summary>
        public static string RenderSheet(IList<GameObject> models, string name, int columns, int cell = 400, float yaw = 38f, float pitch = 14f)
        {
            int rows = Mathf.CeilToInt(models.Count / (float)columns);
            var sheet = new Texture2D(cell * columns, cell * rows, TextureFormat.RGBA32, false);
            var fill = new Color32(0xE9, 0xF1, 0xEA, 0xFF);
            sheet.SetPixels32(Enumerable.Repeat(fill, sheet.width * sheet.height).ToArray());
            for (int i = 0; i < models.Count; i++)
            {
                var shot = Render(models[i], cell, yaw, pitch);
                if (shot == null) continue;
                int x = (i % columns) * cell;
                int y = (rows - 1 - i / columns) * cell;
                sheet.SetPixels(x, y, cell, cell, shot.GetPixels());
                Object.DestroyImmediate(shot);
            }
            Directory.CreateDirectory(OutputFolder);
            var path = Path.Combine(OutputFolder, name + ".png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            return path;
        }

        public static Texture2D Render(GameObject model, int size, float yaw, float pitch)
        {
            var pru = new PreviewRenderUtility();
            GameObject instance = null;
            try
            {
                instance = Object.Instantiate(model);
                instance.hideFlags = HideFlags.HideAndDontSave;
                foreach (var animator in instance.GetComponentsInChildren<Animator>())
                {
                    var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                    var idle = controller != null ? controller.animationClips.FirstOrDefault(c => c.name.EndsWith("_Idle")) : null;
                    if (idle != null) idle.SampleAnimation(animator.gameObject, Mathf.Min(0.4f, idle.length * 0.4f));
                }
                pru.AddSingleGO(instance);

                var bounds = MeasureBounds(instance);
                float radius = Mathf.Max(0.3f, bounds.extents.magnitude);
                pru.camera.fieldOfView = 26f;
                pru.camera.clearFlags = CameraClearFlags.SolidColor;
                pru.camera.backgroundColor = new Color32(0xE9, 0xF1, 0xEA, 0xFF);
                pru.camera.nearClipPlane = 0.05f;
                pru.camera.farClipPlane = 200f;
                var dir = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
                float distance = radius / Mathf.Sin(13f * Mathf.Deg2Rad) * 0.95f;
                pru.camera.transform.position = bounds.center + dir * distance;
                pru.camera.transform.LookAt(bounds.center);
                pru.lights[0].intensity = 1.3f;
                pru.lights[0].transform.rotation = Quaternion.Euler(48f, -140f, 0f);
                pru.lights[1].intensity = 0.55f;
                pru.ambientColor = new Color(0.62f, 0.66f, 0.7f);

                pru.BeginStaticPreview(new Rect(0, 0, size, size));
                pru.camera.Render();
                return pru.EndStaticPreview();
            }
            finally
            {
                if (instance != null) Object.DestroyImmediate(instance);
                pru.Cleanup();
            }
        }

        private static Bounds MeasureBounds(GameObject go)
        {
            bool any = false;
            var b = new Bounds(go.transform.position, Vector3.zero);
            var mesh = new Mesh();
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.BakeMesh(mesh, false);
                var m = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
                foreach (var v in mesh.vertices) Add(ref b, ref any, m.MultiplyPoint3x4(v));
            }
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) Add(ref b, ref any, m.MultiplyPoint3x4(v));
            }
            Object.DestroyImmediate(mesh);
            return any ? b : new Bounds(go.transform.position + Vector3.up, Vector3.one * 2f);
        }

        private static void Add(ref Bounds b, ref bool any, Vector3 p)
        {
            if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
            else b.Encapsulate(p);
        }
    }
}
