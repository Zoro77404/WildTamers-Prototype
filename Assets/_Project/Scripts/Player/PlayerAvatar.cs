using UnityEngine;
using WildTamers.Core;
using WildTamers.Map;

namespace WildTamers.Player
{
    /// <summary>
    /// Places the player's avatar where the location provider says, with smoothing (GPS is jittery),
    /// facing and a bouncy procedural walk.
    /// </summary>
    // Wakes after the location provider (-200) and MapView (-100), before anything that reads the player's position.
    [DefaultExecutionOrder(-90)]
    public class PlayerAvatar : MonoBehaviour
    {
        [SerializeField] private LocationProviderBase locationProvider;
        [SerializeField] private MapView mapView;
        [Tooltip("Child that holds the model; gets the walk bob and waddle.")]
        [SerializeField] private Transform visual;

        [Header("Follow")]
        [SerializeField] private float followSharpness = 16f;
        [Tooltip("Jumps larger than this (meters) snap instead of sliding.")]
        [SerializeField] private float snapDistance = 60f;
        [SerializeField] private float turnSharpness = 12f;

        [Header("Walk feel")]
        [SerializeField] private float bobHeight = 0.22f;
        [SerializeField] private float stepsPerMeter = 0.75f;
        [SerializeField] private float waddleAngle = 7f;
        [SerializeField] private float leanAngle = 8f;

        private Vector3 lastPosition;
        private float speed;
        private float stepPhase;

        public float Speed => speed;
        public LocationProviderBase LocationProvider => locationProvider;

        private void Awake()
        {
            // Snap now, not in Start: tile streaming, the camera and the spawner read this position on their first frame,
            // and after a battle the player is usually far from the scene's default spot.
            if (locationProvider != null && locationProvider.HasLocation && mapView != null)
                transform.position = mapView.GeoToWorld(locationProvider.Location);
            lastPosition = transform.position;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || locationProvider == null || mapView == null || !locationProvider.HasLocation) return;

            var target = mapView.GeoToWorld(locationProvider.Location);
            var pos = transform.position;
            pos = (target - pos).sqrMagnitude > snapDistance * snapDistance
                ? target
                : Vector3.Lerp(pos, target, 1f - Mathf.Exp(-followSharpness * dt));
            transform.position = pos;

            var delta = pos - lastPosition;
            delta.y = 0f;
            lastPosition = pos;
            speed = Mathf.Lerp(speed, delta.magnitude / dt, 1f - Mathf.Exp(-10f * dt));

            if (delta.sqrMagnitude > 1e-6f && speed > 0.3f)
            {
                var look = Quaternion.LookRotation(delta.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnSharpness * dt));
            }

            AnimateVisual(dt);
        }

        private void AnimateVisual(float dt)
        {
            if (visual == null) return;
            float moving = Mathf.Clamp01(speed / 4f);
            stepPhase += speed * stepsPerMeter * dt;
            float s = Mathf.Sin(stepPhase * Mathf.PI);

            float bob = Mathf.Abs(s) * bobHeight * moving;
            float breathe = 1f + Mathf.Sin(Time.time * 2.2f) * 0.018f * (1f - moving);
            visual.localPosition = new Vector3(0f, bob, 0f);
            visual.localRotation = Quaternion.Euler(leanAngle * moving, 0f, s * waddleAngle * moving);
            visual.localScale = new Vector3(1f, breathe, 1f);
        }
    }
}
