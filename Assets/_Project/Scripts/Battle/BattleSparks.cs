using UnityEngine;

namespace WildTamers.Battle
{
    /// <summary>One shared particle system for hit sparks, skill bursts and charge rings (emitted on demand).</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class BattleSparks : MonoBehaviour
    {
        private ParticleSystem system;

        private void Awake() => system = GetComponent<ParticleSystem>();

        /// <summary>Sparks flying out in every direction from a point.</summary>
        public void Burst(Vector3 position, Color color, int count, float speedScale = 1f)
        {
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                var dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
                p.position = position + dir * 0.2f;
                p.velocity = dir.normalized * Random.Range(3.5f, 7.5f) * speedScale;
                p.startColor = Color.Lerp(color, Color.white, Random.value * 0.3f);
                p.startSize = Random.Range(0.32f, 0.7f) * Mathf.Sqrt(speedScale);
                p.startLifetime = Random.Range(0.3f, 0.55f);
                system.Emit(p, 1);
            }
        }

        /// <summary>Sparks rising from a ring on the ground (skill charge-up).</summary>
        public void Ring(Vector3 center, Color color, float radius, int count)
        {
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                p.position = center + dir * radius;
                p.velocity = Vector3.up * Random.Range(2.5f, 5f) - dir * Random.Range(0.4f, 1.2f);
                p.startColor = Color.Lerp(color, Color.white, Random.value * 0.35f);
                p.startSize = Random.Range(0.25f, 0.45f);
                p.startLifetime = Random.Range(0.45f, 0.7f);
                system.Emit(p, 1);
            }
        }
    }
}
