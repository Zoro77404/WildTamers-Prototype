using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using WildTamers.Animals;

namespace WildTamers.Core
{
    /// <summary>
    /// Off-screen "photo studio" that renders animals into RenderTextures for UI:
    /// live turntable previews (popups, starter cards) and cached still portraits (team list).
    /// Lives under GameSession so it survives scene loads.
    /// </summary>
    public class PreviewStudio : MonoBehaviour
    {
        private static readonly Vector3 StudioOrigin = new Vector3(0f, -3000f, 0f);
        private const float SlotSpacing = 80f;
        private const float FieldOfView = 26f;

        public class Handle
        {
            internal Slot Slot;
            public RenderTexture Texture => Slot?.Texture;
            public bool IsValid => Slot != null;
        }

        internal class Slot
        {
            public int Index;
            public Transform Root;
            public Transform Pivot;
            public Camera Camera;
            public RenderTexture Texture;
            public GameObject Model;
            public AnimalData Data;
            public bool Live;
            public float SwayPhase;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private readonly Stack<Slot> freeSlots = new Stack<Slot>();
        private readonly Dictionary<AnimalData, RenderTexture> portraits = new Dictionary<AnimalData, RenderTexture>();

        /// <summary>Starts a live preview. Call <see cref="Release"/> when the UI hides.</summary>
        public Handle AcquireLive(AnimalData data, int resolution, Color background)
        {
            var slot = RentSlot(resolution, background);
            slot.Live = true;
            slot.SwayPhase = Random.value * 10f;
            SetModel(slot, data);
            slot.Camera.enabled = true;
            return new Handle { Slot = slot };
        }

        /// <summary>Swaps the animal shown by an existing live preview.</summary>
        public void SetAnimal(Handle handle, AnimalData data)
        {
            if (handle?.Slot == null) return;
            if (handle.Slot.Data != data) SetModel(handle.Slot, data);
        }

        public void Release(Handle handle)
        {
            if (handle?.Slot == null) return;
            var slot = handle.Slot;
            handle.Slot = null;
            ClearSlot(slot);
            freeSlots.Push(slot);
        }

        /// <summary>Still portrait of a species, rendered once and cached for the whole session.</summary>
        public RenderTexture GetPortrait(AnimalData data, Color background)
        {
            if (data == null) return null;
            if (portraits.TryGetValue(data, out var cached) && cached != null) return cached;

            var rt = CreateTexture(256);
            portraits[data] = rt;
            StartCoroutine(RenderPortrait(data, rt, background));
            return rt;
        }

        private IEnumerator RenderPortrait(AnimalData data, RenderTexture target, Color background)
        {
            var slot = RentSlot(256, background);
            var ownTexture = slot.Texture;
            slot.Camera.targetTexture = target;
            slot.Live = false;
            SetModel(slot, data, portrait: true);
            slot.Camera.enabled = true;
            // Two frames so the Animator has posed the idle before the shot.
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            slot.Camera.enabled = false;
            slot.Camera.targetTexture = ownTexture;
            ClearSlot(slot);
            freeSlots.Push(slot);
        }

        private void LateUpdate()
        {
            float t = Time.unscaledTime;
            foreach (var slot in slots)
            {
                if (!slot.Live || slot.Model == null) continue;
                // Gentle turntable sway so the 3/4 view keeps the face visible.
                slot.Pivot.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.7f + slot.SwayPhase) * 28f, 0f);
            }
        }

        private Slot RentSlot(int resolution, Color background)
        {
            Slot slot = freeSlots.Count > 0 ? freeSlots.Pop() : CreateSlot();
            if (slot.Texture == null || slot.Texture.width != resolution)
            {
                if (slot.Texture != null) Destroy(slot.Texture);
                slot.Texture = CreateTexture(resolution);
                slot.Camera.targetTexture = slot.Texture;
            }
            background.a = 0f;
            slot.Camera.backgroundColor = background;
            return slot;
        }

        private Slot CreateSlot()
        {
            var slot = new Slot { Index = slots.Count };
            slot.Root = new GameObject($"Slot {slot.Index}").transform;
            slot.Root.SetParent(transform, false);
            slot.Root.position = StudioOrigin + Vector3.right * (slot.Index * SlotSpacing);
            slot.Pivot = new GameObject("Pivot").transform;
            slot.Pivot.SetParent(slot.Root, false);

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(slot.Root, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(1f, 1f, 1f, 0f);
            cam.cullingMask = GameLayers.Preview >= 0 ? 1 << GameLayers.Preview : 0;
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 100f;
            cam.allowHDR = false; // keep an alpha channel in the intermediate target
            cam.allowMSAA = true;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            slot.Camera = cam;

            slots.Add(slot);
            return slot;
        }

        private static RenderTexture CreateTexture(int resolution)
        {
            var rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                name = "AnimalPreviewRT"
            };
            rt.Create();
            return rt;
        }

        private void SetModel(Slot slot, AnimalData data, bool portrait = false)
        {
            if (slot.Model != null) Destroy(slot.Model);
            slot.Model = null;
            slot.Data = data;
            slot.Pivot.localRotation = Quaternion.identity;
            if (data == null || data.prefab == null) return;

            slot.Model = Instantiate(data.prefab, slot.Pivot);
            slot.Model.transform.localPosition = Vector3.zero;
            slot.Model.transform.localRotation = Quaternion.identity;
            foreach (var c in slot.Model.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            GameLayers.SetLayerRecursively(slot.Model, GameLayers.Preview);

            var visual = slot.Model.GetComponent<AnimalVisual>();
            if (visual != null) visual.PlayIdle(randomStart: !portrait);

            Frame(slot, portrait);
        }

        private static void Frame(Slot slot, bool portrait)
        {
            var bounds = new Bounds(slot.Pivot.position, Vector3.zero);
            bool any = false;
            foreach (var r in slot.Model.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }

            // Live previews show the whole animal; portraits zoom toward the head (front-top of the bounds).
            float radius = Mathf.Max(0.25f, bounds.extents.magnitude);
            float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) * (portrait ? 0.62f : 0.86f);
            var focus = portrait
                ? bounds.center + new Vector3(0f, bounds.extents.y * 0.35f, bounds.extents.z * 0.35f)
                : bounds.center + Vector3.up * (bounds.extents.y * 0.05f);
            var dir = Quaternion.Euler(-14f, portrait ? 32f : 26f, 0f) * Vector3.forward;
            var cam = slot.Camera.transform;
            cam.position = focus + dir * distance;
            cam.LookAt(focus);
            slot.Camera.farClipPlane = distance + radius * 4f;
        }

        private void ClearSlot(Slot slot)
        {
            slot.Camera.enabled = false;
            slot.Live = false;
            slot.Data = null;
            if (slot.Model != null) Destroy(slot.Model);
            slot.Model = null;
        }

        private void OnDestroy()
        {
            foreach (var s in slots) if (s.Texture != null) Destroy(s.Texture);
            foreach (var rt in portraits.Values) if (rt != null) Destroy(rt);
        }
    }
}
