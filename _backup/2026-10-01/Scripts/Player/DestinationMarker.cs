using UnityEngine;
using WildTamers.Map;

namespace WildTamers.Player
{
    /// <summary>Shows where click-to-move is heading: a pulsing ring with a bobbing pin.</summary>
    public class DestinationMarker : MonoBehaviour
    {
        [SerializeField] private FakeLocationProvider provider;
        [SerializeField] private MeshFilter ring;
        [SerializeField] private Transform pin;
        [SerializeField] private float ringRadius = 1.2f;

        private Vector3 lastDestination;
        private float appear;
        private bool shown;

        private void Awake()
        {
            if (ring != null) ring.sharedMesh = RingMesh.Create(ringRadius * 0.72f, ringRadius, 48, withFill: false, "DestinationRing");
            SetVisible(false);
        }

        private void LateUpdate()
        {
            bool active = provider != null && provider.HasDestination;
            if (!active)
            {
                if (shown) SetVisible(false);
                return;
            }

            var dest = provider.DestinationWorld;
            if (!shown || (dest - lastDestination).sqrMagnitude > 0.01f)
            {
                appear = 0f;
                lastDestination = dest;
                SetVisible(true);
            }

            transform.position = dest;
            appear = Mathf.Min(1f, appear + Time.deltaTime * 4f);
            float pop = EaseOutBack(appear);
            float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.06f;
            if (ring != null) ring.transform.localScale = Vector3.one * pop * pulse;
            if (pin != null)
            {
                pin.localPosition = new Vector3(0f, 1.1f + Mathf.Abs(Mathf.Sin(Time.time * 4f)) * 0.5f, 0f);
                pin.localScale = Vector3.one * pop;
                pin.localRotation = Quaternion.Euler(0f, Time.time * 120f, 0f);
            }
        }

        private void SetVisible(bool visible)
        {
            shown = visible;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
