using UnityEngine;

namespace WildTamers.Menu
{
    /// <summary>Slowly sways the camera around the animals so the background is never still.</summary>
    public class MenuCameraSway : MonoBehaviour
    {
        [SerializeField] private Vector3 lookAt = new Vector3(0f, 1.5f, 2f);
        [SerializeField] private float swingDegrees = 11f;
        [SerializeField] private float secondsPerSwing = 36f;
        [SerializeField] private float dolly = 0.8f;

        private Vector3 offset;
        private float baseYaw;

        private void Awake()
        {
            offset = transform.position - lookAt;
            baseYaw = 0f;
        }

        private void LateUpdate()
        {
            float t = Time.time / secondsPerSwing * Mathf.PI * 2f;
            var rotated = Quaternion.Euler(0f, baseYaw + Mathf.Sin(t) * swingDegrees, 0f) * offset;
            rotated *= 1f + Mathf.Sin(t * 0.5f) * dolly / offset.magnitude;
            transform.position = lookAt + rotated;
            transform.LookAt(lookAt + Vector3.up * Mathf.Sin(t * 0.7f) * 0.1f);
        }
    }
}
