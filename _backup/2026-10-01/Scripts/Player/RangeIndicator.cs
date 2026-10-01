using UnityEngine;
using WildTamers.Core;
using WildTamers.Map;

namespace WildTamers.Player
{
    /// <summary>
    /// Circle on the ground around the player showing fight range, with a soft pulse,
    /// a periodic "ping" wave and a red flash when the player taps an animal that is too far.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RangeIndicator : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private float lineWidth = 0.32f;
        [SerializeField] private int segments = 128;
        [SerializeField] private Color ringColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color fillColor = new Color(1f, 1f, 1f, 0.1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.33f, 0.36f, 1f);
        [Tooltip("Optional thin ring that expands from the player every few seconds.")]
        [SerializeField] private MeshRenderer pingRing;
        [SerializeField] private float pingInterval = 2.6f;
        [SerializeField] private float pingDuration = 1.4f;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock block;
        private float radius;
        private float warning;
        private float pingTimer;

        public float Radius => radius;

        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
        }

        private void Start()
        {
            radius = GameSession.Instance.Config.fightRange;
            GetComponent<MeshFilter>().sharedMesh = RingMesh.Create(radius - lineWidth, radius, segments, withFill: true, "RangeRing");
            if (pingRing != null)
            {
                pingRing.GetComponent<MeshFilter>().sharedMesh = RingMesh.Create(0.94f, 1f, segments, withFill: false, "PingRing");
                pingRing.enabled = false;
            }
        }

        /// <summary>Flashes the circle red, e.g. when a tapped animal is out of range.</summary>
        public void FlashWarning() => warning = 1f;

        private void Update()
        {
            float t = Time.time;
            warning = Mathf.MoveTowards(warning, 0f, Time.deltaTime * 1.4f);
            float w = Mathf.SmoothStep(0f, 1f, warning);

            float pulse = 0.82f + 0.18f * Mathf.Sin(t * 2.4f);
            var ring = Color.Lerp(ringColor, warningColor, w);
            ring.a *= Mathf.Lerp(pulse, 1f, w);
            var fill = Color.Lerp(fillColor, new Color(warningColor.r, warningColor.g, warningColor.b, 0.22f), w);

            SetColor(meshRenderer, 0, ring);
            SetColor(meshRenderer, 1, fill);

            float punch = 1f + Mathf.Sin(w * Mathf.PI) * 0.035f;
            transform.localScale = new Vector3(punch, 1f, punch);

            UpdatePing();
        }

        private void UpdatePing()
        {
            if (pingRing == null || radius <= 0f) return;
            pingTimer += Time.deltaTime;
            if (pingTimer > pingInterval) pingTimer = 0f;
            float p = pingTimer / pingDuration;
            pingRing.enabled = p < 1f;
            if (!pingRing.enabled) return;

            float eased = 1f - (1f - p) * (1f - p);
            float r = Mathf.Lerp(radius * 0.15f, radius, eased);
            pingRing.transform.localScale = new Vector3(r, 1f, r);
            var c = ringColor;
            c.a = 0.55f * (1f - p);
            SetColor(pingRing, 0, c);
        }

        private void SetColor(Renderer r, int materialIndex, Color c)
        {
            r.GetPropertyBlock(block, materialIndex);
            block.SetColor(BaseColorId, c);
            r.SetPropertyBlock(block, materialIndex);
        }
    }
}
