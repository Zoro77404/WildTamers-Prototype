using UnityEngine;

namespace WildTamers.Menu
{
    /// <summary>The falcon glides in a slow circle above the others, banking into the turn.</summary>
    public class MenuFalconFlight : MonoBehaviour
    {
        [SerializeField] private Vector3 center = new Vector3(0f, 4.6f, 1f);
        [SerializeField] private float radius = 6.5f;
        [SerializeField] private float secondsPerLap = 26f;
        [SerializeField] private float bob = 0.35f;

        private float phase;

        private void Update()
        {
            phase += Time.deltaTime / secondsPerLap * Mathf.PI * 2f;
            var offset = new Vector3(Mathf.Cos(phase) * radius, Mathf.Sin(phase * 3f) * bob, Mathf.Sin(phase) * radius * 0.55f);
            var next = new Vector3(Mathf.Cos(phase + 0.05f) * radius, 0f, Mathf.Sin(phase + 0.05f) * radius * 0.55f);
            transform.position = center + offset;
            var heading = next - new Vector3(offset.x, 0f, offset.z);
            if (heading.sqrMagnitude > 0.0001f)
            {
                var look = Quaternion.LookRotation(heading.normalized, Vector3.up);
                transform.rotation = look * Quaternion.Euler(0f, 0f, -14f); // bank into the circle
            }
        }
    }
}
