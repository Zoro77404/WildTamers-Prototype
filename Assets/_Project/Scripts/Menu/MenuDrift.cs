using UnityEngine;

namespace WildTamers.Menu
{
    /// <summary>Clouds drifting across the sky, wrapping around so they never run out.</summary>
    public class MenuDrift : MonoBehaviour
    {
        [SerializeField] private Vector3 velocity = new Vector3(0.7f, 0f, 0f);
        [SerializeField] private float wrapAt = 90f;

        private void Update()
        {
            var p = transform.position + velocity * Time.deltaTime;
            if (p.x > wrapAt) p.x = -wrapAt;
            else if (p.x < -wrapAt) p.x = wrapAt;
            transform.position = p;
        }
    }
}
