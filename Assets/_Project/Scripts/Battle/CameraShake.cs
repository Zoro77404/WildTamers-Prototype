using UnityEngine;

namespace WildTamers.Battle
{
    /// <summary>Trauma-based camera shake: hits add trauma, which decays; offset grows with trauma².</summary>
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private float maxOffset = 0.32f;
        [SerializeField] private float maxRoll = 1.6f;
        [SerializeField] private float frequency = 24f;
        [SerializeField] private float decay = 2.4f;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private float trauma;
        private float seed;

        private void Awake()
        {
            restPosition = transform.localPosition;
            restRotation = transform.localRotation;
            seed = Random.value * 100f;
        }

        public void Shake(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        private void LateUpdate()
        {
            if (trauma <= 0f) return;
            float strength = trauma * trauma;
            float t = Time.unscaledTime * frequency;
            var offset = new Vector3(Mathf.PerlinNoise(seed, t) * 2f - 1f, Mathf.PerlinNoise(seed + 7f, t) * 2f - 1f, 0f) * (maxOffset * strength);
            float roll = (Mathf.PerlinNoise(seed + 13f, t) * 2f - 1f) * maxRoll * strength;
            transform.localPosition = restPosition + restRotation * offset;
            transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, roll);

            trauma = Mathf.Max(0f, trauma - decay * Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            if (trauma <= 0f)
            {
                transform.localPosition = restPosition;
                transform.localRotation = restRotation;
            }
        }
    }
}
