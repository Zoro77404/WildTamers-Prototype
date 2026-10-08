using System.Collections.Generic;
using UnityEngine;
using WildTamers.UI;

namespace WildTamers.Vfx
{
    /// <summary>
    /// A playing <see cref="VfxEffect"/>: one child mesh per layer, animated frame by frame (with smooth in-between frames),
    /// faded in and out, then destroyed. In Play Mode it runs by itself; the editor preview drives it with <see cref="Tick"/>.
    /// </summary>
    public class VfxInstance : MonoBehaviour
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int RecolorId = Shader.PropertyToID("_Recolor");
        private static readonly int FadeId = Shader.PropertyToID("_Fade");

        private VfxEffect effect;
        private readonly List<LayerView> views = new List<LayerView>();
        private float time;
        private float speed = 1f;
        private float duration;
        private Color tint = Color.white;
        private float recolor;
        private bool manual;

        /// <summary>Seconds (in effect time) since it started.</summary>
        public float Time => time;
        public bool Finished => time >= duration;
        public VfxEffect Effect => effect;

        /// <summary>
        /// Starts an effect. <paramref name="scale"/> multiplies the effect's own base scale; <paramref name="recolor"/> 0 keeps
        /// the original colors, 1 paints the effect in <paramref name="tint"/>; <paramref name="speed"/> 2 = twice as fast.
        /// Set <paramref name="manualTick"/> to drive it yourself with <see cref="Tick"/> (editor preview).
        /// </summary>
        public static VfxInstance Spawn(VfxEffect effect, Vector3 position, Quaternion rotation, float scale, Color tint, float recolor,
            float speed = 1f, bool manualTick = false)
        {
            if (effect == null) return null;
            var go = new GameObject("VFX " + effect.name);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = Vector3.one * Mathf.Max(0.01f, scale * effect.baseScale);
            var instance = go.AddComponent<VfxInstance>();
            instance.Setup(effect, tint, recolor, speed, manualTick);
            return instance;
        }

        /// <summary>Yaw that makes a camera-facing effect show its front (the side the artist built it to be seen from).</summary>
        public static Quaternion FacingCamera(Camera camera, Quaternion fallback)
        {
            if (camera == null) return fallback;
            var forward = camera.transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude < 1e-4f ? fallback : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }

        private void Setup(VfxEffect fx, Color color, float recolorAmount, float playSpeed, bool manualTick)
        {
            effect = fx;
            tint = color;
            recolor = Mathf.Clamp01(recolorAmount);
            speed = Mathf.Max(0.05f, playSpeed);
            manual = manualTick;
            duration = fx.Duration;
            if (fx.layers != null)
            {
                foreach (var layer in fx.layers)
                {
                    if (layer == null || layer.clip == null || !layer.clip.IsValid) continue;
                    views.Add(new LayerView(this, layer));
                }
            }
            Apply();
        }

        private void Update()
        {
            if (!manual) Tick(UIEase.GameDeltaTime);
        }

        /// <summary>Advances the effect by <paramref name="deltaTime"/> real seconds; destroys it when finished (unless manual).</summary>
        public void Tick(float deltaTime)
        {
            time += Mathf.Max(0f, deltaTime) * speed;
            Apply();
            if (Finished && !manual) Destroy(gameObject);
        }

        /// <summary>Jumps to a moment (effect seconds), e.g. to scrub the editor preview.</summary>
        public void Seek(float effectTime)
        {
            time = Mathf.Max(0f, effectTime);
            Apply();
        }

        private void Apply()
        {
            foreach (var view in views) view.Update(time, tint, recolor);
        }

        /// <summary>Removes the effect now (also works outside Play Mode, where OnDestroy is not called).</summary>
        public void Dispose()
        {
            ReleaseMeshes();
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void OnDestroy() => ReleaseMeshes();

        private void ReleaseMeshes()
        {
            foreach (var view in views) view.Release();
            views.Clear();
        }

        // ------------------------------------------------------------------

        private sealed class LayerView
        {
            private readonly VfxEffect.Layer layer;
            private readonly VfxClip clip;
            private readonly GameObject go;
            private readonly Transform transform;
            private readonly MeshRenderer renderer;
            private readonly Mesh mesh;
            private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            private readonly Vector3[] posA, posB, pos, nrm;
            private readonly Color[] colA, colB, col;
            private readonly float teleportSq;
            private int frameA = -1, frameB = -1;
            private bool visible;

            public LayerView(VfxInstance owner, VfxEffect.Layer layer)
            {
                this.layer = layer;
                clip = layer.clip;
                go = new GameObject(clip.name);
                go.hideFlags = owner.gameObject.hideFlags;
                transform = go.transform;
                transform.SetParent(owner.transform, false);
                transform.localPosition = layer.offset;
                transform.localScale = Vector3.one * layer.scale;
                mesh = clip.CreateMesh();
                mesh.hideFlags = HideFlags.DontSave;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = go.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                var mats = new Material[clip.parts.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = clip.parts[i].material;
                renderer.sharedMaterials = mats;

                int n = clip.VertexCount;
                posA = new Vector3[n]; posB = new Vector3[n]; pos = new Vector3[n]; nrm = new Vector3[n];
                colA = new Color[n]; colB = new Color[n]; col = new Color[n];
                // Points that jump further than this between two frames are respawned particles: no in-between for them.
                float size = clip.Bounds.size.magnitude;
                teleportSq = (size * 0.08f) * (size * 0.08f);
                go.SetActive(false);
            }

            public void Update(float effectTime, Color tint, float recolor)
            {
                float local = effectTime - layer.start;
                float duration = layer.Duration;
                bool on = local >= 0f && local <= duration;
                if (on != visible)
                {
                    visible = on;
                    go.SetActive(on);
                }
                if (!on) return;

                // Clip time (loops while held).
                float span = layer.ClipSpan;
                float clipTime = local * layer.speed;
                if (layer.hold > 0f && span > 0f && clipTime > span) clipTime %= span;
                clipTime = layer.clipFrom + Mathf.Min(clipTime, span);
                ShowTime(clipTime);

                // Fade + grow in, fade (+ shrink) out.
                float fade = 1f, grow = 1f;
                if (layer.fadeIn > 0f && local < layer.fadeIn)
                {
                    float p = local / layer.fadeIn;
                    fade = p;
                    grow = Mathf.Lerp(0.55f, 1f, 1f - (1f - p) * (1f - p));
                }
                if (layer.fadeOut > 0f && local > duration - layer.fadeOut)
                {
                    float p = Mathf.Clamp01((duration - local) / layer.fadeOut);
                    fade = Mathf.Min(fade, p);
                    if (layer.shrinkOut) grow *= Mathf.Lerp(0.35f, 1f, p);
                }
                transform.localScale = Vector3.one * (layer.scale * grow);

                block.SetColor(TintId, tint);
                block.SetFloat(RecolorId, recolor);
                var parts = clip.parts;
                for (int i = 0; i < parts.Length; i++)
                {
                    block.SetFloat(FadeId, fade * (parts[i] != null ? parts[i].VisibilityAt(clipTime) : 1f));
                    renderer.SetPropertyBlock(block, i);
                }
            }

            private void ShowTime(float clipTime)
            {
                float f = Mathf.Clamp(clipTime * clip.FrameRate, 0f, clip.FrameCount - 1);
                int a = Mathf.FloorToInt(f);
                int b = Mathf.Min(a + 1, clip.FrameCount - 1);
                float t = f - a;

                if (a != frameA || b != frameB)
                {
                    if (a == frameB)
                    {
                        // Moving forward one frame: the old "next" frame becomes the current one.
                        System.Array.Copy(posB, posA, posA.Length);
                        System.Array.Copy(colB, colA, colA.Length);
                    }
                    else clip.DecodeFrame(a, posA, colA, null);
                    clip.DecodeFrame(a, null, null, nrm);
                    if (b != a) clip.DecodeFrame(b, posB, colB, null);
                    else
                    {
                        System.Array.Copy(posA, posB, posA.Length);
                        System.Array.Copy(colA, colB, colA.Length);
                    }
                    frameA = a;
                    frameB = b;
                    mesh.SetNormals(nrm);
                }

                bool movePos = !clip.SamePositions(a, b);
                bool moveCol = !clip.SameColors(a, b);
                int n = pos.Length;
                for (int i = 0; i < n; i++)
                {
                    var p0 = posA[i];
                    if (movePos)
                    {
                        var d = posB[i] - p0;
                        pos[i] = d.sqrMagnitude > teleportSq ? (t < 0.5f ? p0 : posB[i]) : p0 + d * t;
                    }
                    else pos[i] = p0;
                    col[i] = moveCol ? Color.LerpUnclamped(colA[i], colB[i], t) : colA[i];
                }
                mesh.SetVertices(pos);
                mesh.SetColors(col);
            }

            public void Release()
            {
                if (mesh == null) return;
                if (Application.isPlaying) Object.Destroy(mesh);
                else Object.DestroyImmediate(mesh);
            }
        }
    }
}
