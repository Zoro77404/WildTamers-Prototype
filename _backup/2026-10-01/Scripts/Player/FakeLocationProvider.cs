using UnityEngine;
using UnityEngine.InputSystem;
using WildTamers.Core;
using WildTamers.Map;

namespace WildTamers.Player
{
    /// <summary>
    /// Stand-in for GPS: WASD/arrow keys (Shift to jog) or click-to-move change a simulated
    /// latitude/longitude. Swap for a GPS provider later; nothing else needs to change.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class FakeLocationProvider : LocationProviderBase
    {
        [SerializeField] private GeoCoordinate startLocation = new GeoCoordinate(51.5007, -0.1246);
        [SerializeField] private MapView mapView;
        [Tooltip("Keyboard movement is relative to this camera's facing.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Movement (meters / second)")]
        [SerializeField, Min(0.1f)] private float walkSpeed = 7f;
        [SerializeField, Min(0.1f)] private float runSpeed = 12f;
        [SerializeField, Min(0.1f)] private float acceleration = 45f;
        [SerializeField, Min(0.01f)] private float arriveDistance = 0.25f;

        private GeoCoordinate location;
        private bool hasLocation;
        private Vector2 velocity; // x = east, y = north
        private float heading = float.NaN;
        private bool hasDestination;
        private GeoCoordinate destination;

        public override bool HasLocation => hasLocation;
        public override GeoCoordinate Location => location;
        public override float HeadingDegrees => heading;

        public bool HasDestination => hasDestination;
        public Vector3 DestinationWorld => mapView != null ? mapView.GeoToWorld(destination) : Vector3.zero;
        public float CurrentSpeed => velocity.magnitude;

        private void Awake()
        {
            var session = GameSession.Instance;
            location = session.HasLastLocation ? session.LastLocation : startLocation;
            hasLocation = true;
            session.SetLastLocation(location);
        }

        /// <summary>Walk to a point on the map (click/tap to move).</summary>
        public void SetDestination(Vector3 worldPoint)
        {
            if (mapView == null) return;
            destination = mapView.WorldToGeo(worldPoint);
            hasDestination = true;
        }

        public void ClearDestination() => hasDestination = false;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector2 desired = Vector2.zero;
            if (MapInputGate.IsBlocked)
            {
                hasDestination = false;
            }
            else
            {
                Vector2 input = ReadMoveInput(out bool run);
                if (input.sqrMagnitude > 0.01f)
                {
                    hasDestination = false;
                    desired = CameraRelative(input) * (run ? runSpeed : walkSpeed);
                }
                else if (hasDestination && mapView != null)
                {
                    var here = mapView.GeoToWorld(location);
                    var there = mapView.GeoToWorld(destination);
                    var offset = new Vector2(there.x - here.x, there.z - here.z);
                    float distance = offset.magnitude;
                    if (distance <= arriveDistance) hasDestination = false;
                    // Ease into the stop so the avatar doesn't overshoot.
                    else desired = offset / distance * Mathf.Min(walkSpeed, distance * 3f);
                }
            }

            velocity = Vector2.MoveTowards(velocity, desired, acceleration * dt);
            if (velocity.sqrMagnitude < 1e-5f)
            {
                velocity = Vector2.zero;
                return;
            }

            location = GeoProjection.Offset(location, velocity.x * dt, velocity.y * dt);
            heading = Mathf.Atan2(velocity.x, velocity.y) * Mathf.Rad2Deg;
            GameSession.Instance.SetLastLocation(location);
            RaiseLocationChanged(location);
        }

        private static Vector2 ReadMoveInput(out bool run)
        {
            run = false;
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            var v = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
            run = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            return Vector2.ClampMagnitude(v, 1f);
        }

        private Vector2 CameraRelative(Vector2 input)
        {
            if (cameraTransform == null) return input;
            var f = cameraTransform.forward; f.y = 0f;
            var r = cameraTransform.right; r.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            f.Normalize(); r.Normalize();
            var dir = r * input.x + f * input.y;
            return new Vector2(dir.x, dir.z);
        }
    }
}
