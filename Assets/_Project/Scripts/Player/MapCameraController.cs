using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using WildTamers.Core;

namespace WildTamers.Player
{
    /// <summary>
    /// Pokémon GO-style map camera: tilted top-down view that follows the player.
    /// Mouse wheel zooms (closer = lower, more 3D angle), Q/E or right-drag orbits.
    /// </summary>
    public class MapCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

        [Header("Zoom")]
        [SerializeField] private float minDistance = 26f;
        [SerializeField] private float maxDistance = 90f;
        [SerializeField] private float startDistance = 50f;
        [Tooltip("Fraction of the distance changed per wheel notch.")]
        [SerializeField] private float zoomStep = 0.12f;
        [SerializeField] private float zoomSharpness = 10f;

        [Header("Angle")]
        [Tooltip("Pitch at the closest zoom.")]
        [SerializeField] private float closePitch = 36f;
        [Tooltip("Pitch at the farthest zoom.")]
        [SerializeField] private float farPitch = 56f;
        [SerializeField] private float startYaw = 0f;
        [SerializeField] private float keyOrbitSpeed = 90f;
        [SerializeField] private float dragOrbitSpeed = 0.25f;

        [Header("Follow")]
        [SerializeField] private float followSharpness = 9f;

        private float distance;
        private float targetDistance;
        private float yaw;
        private float targetYaw;
        private Vector3 focus;

        public float Yaw => yaw;

        private void Start()
        {
            distance = targetDistance = Mathf.Clamp(startDistance, minDistance, maxDistance);
            yaw = targetYaw = startYaw;
            if (target != null) focus = target.position + targetOffset;
            Apply();
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            ReadInput(dt);

            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-zoomSharpness * dt));
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-12f * dt));
            if (target != null)
                focus = Vector3.Lerp(focus, target.position + targetOffset, 1f - Mathf.Exp(-followSharpness * dt));
            Apply();
        }

        private void ReadInput(float dt)
        {
            if (MapInputGate.IsBlocked) return;

            var mouse = Mouse.current;
            if (mouse != null && !IsPointerOverUI())
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    targetDistance = Mathf.Clamp(targetDistance * (1f - Mathf.Sign(scroll) * zoomStep), minDistance, maxDistance);

                if (mouse.rightButton.isPressed)
                    targetYaw += mouse.delta.ReadValue().x * dragOrbitSpeed;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.qKey.isPressed) targetYaw -= keyOrbitSpeed * dt;
                if (kb.eKey.isPressed) targetYaw += keyOrbitSpeed * dt;
                if (kb.equalsKey.wasPressedThisFrame || kb.numpadPlusKey.wasPressedThisFrame)
                    targetDistance = Mathf.Clamp(targetDistance * (1f - zoomStep), minDistance, maxDistance);
                if (kb.minusKey.wasPressedThisFrame || kb.numpadMinusKey.wasPressedThisFrame)
                    targetDistance = Mathf.Clamp(targetDistance * (1f + zoomStep), minDistance, maxDistance);
            }
        }

        private void Apply()
        {
            float t = Mathf.InverseLerp(minDistance, maxDistance, distance);
            float pitch = Mathf.Lerp(closePitch, farPitch, t);
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(focus + rot * new Vector3(0f, 0f, -distance), rot);
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
