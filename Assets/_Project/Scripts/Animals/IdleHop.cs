using UnityEngine;

namespace WildTamers.Animals
{
    /// <summary>
    /// Small procedural "alive" motion: periodic hops with squash &amp; stretch and a gentle breathing scale.
    /// Drives the transform it sits on, so put it on a child between the root and the model.
    /// </summary>
    public class IdleHop : MonoBehaviour
    {
        [SerializeField] private Vector2 hopInterval = new Vector2(1.6f, 3.4f);
        [SerializeField] private float hopHeight = 0.45f;
        [SerializeField] private float hopDuration = 0.42f;
        [SerializeField] private float squash = 0.16f;
        [SerializeField] private float breatheAmount = 0.025f;

        private float timer;
        private float hopTime = -1f;
        private float landTime = -1f;
        private float currentHeight;
        private float breathePhase;

        /// <summary>Multiplier for hop frequency/height (e.g. excited when the player is close).</summary>
        public float Energy { get; set; } = 1f;

        private void OnEnable()
        {
            timer = Random.Range(0.2f, hopInterval.y);
            breathePhase = Random.value * 10f;
        }

        /// <summary>Hop right now (e.g. as a reaction to being tapped).</summary>
        public void HopNow(float heightMultiplier = 1f)
        {
            hopTime = 0f;
            currentHeight = hopHeight * heightMultiplier;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            timer -= dt * Mathf.Max(0.1f, Energy);
            if (timer <= 0f && hopTime < 0f)
            {
                timer = Random.Range(hopInterval.x, hopInterval.y);
                HopNow(Mathf.Lerp(1f, 1.35f, Mathf.Clamp01(Energy - 1f)));
            }

            float y = 0f, sy = 1f;
            if (hopTime >= 0f)
            {
                hopTime += dt;
                float p = hopTime / hopDuration;
                if (p < 0.12f) sy = 1f - squash * Mathf.Sin(p / 0.12f * Mathf.PI); // crouch
                else if (p < 1f)
                {
                    float a = (p - 0.12f) / 0.88f;
                    y = Mathf.Sin(a * Mathf.PI) * currentHeight;
                    sy = 1f + squash * 0.8f * Mathf.Sin(a * Mathf.PI) * (1f - a); // stretch on the way up
                }
                else
                {
                    hopTime = -1f;
                    landTime = 0f;
                }
            }
            else if (landTime >= 0f)
            {
                landTime += dt;
                float p = landTime / 0.22f;
                if (p >= 1f) landTime = -1f;
                else sy = 1f - squash * Mathf.Sin(p * Mathf.PI) * (1f - p * 0.5f); // squash on landing
            }

            float breathe = 1f + Mathf.Sin(Time.time * 2.6f + breathePhase) * breatheAmount;
            sy *= breathe;
            float sxz = 1f / Mathf.Sqrt(Mathf.Max(0.5f, sy)); // keep volume
            transform.localPosition = new Vector3(0f, y, 0f);
            transform.localScale = new Vector3(sxz, sy, sxz);
        }
    }
}
