using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using WildTamers.Animals;
using WildTamers.Core;

namespace WildTamers.Map
{
    /// <summary>
    /// Turns clicks/taps on the map into events: a wild animal was tapped, or the ground was tapped.
    /// Uses the Input System's Pointer so it works for mouse now and touch later.
    /// </summary>
    public class MapPointerInput : MonoBehaviour
    {
        [SerializeField] private Camera mapCamera;
        [SerializeField] private MapView mapView;
        [Tooltip("Taps within this radius of an animal's collider count as a hit (more forgiving than a thin ray).")]
        [SerializeField] private float tapRadius = 0.9f;
        [Tooltip("A press that moves more than this many pixels is a drag, not a tap.")]
        [SerializeField] private float maxTapMovement = 20f;

        public event Action<WildAnimal> AnimalTapped;
        public event Action<Vector3> GroundTapped;

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private PointerEventData pointerData;
        private Vector2 pressPosition;
        private bool tracking;

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || mapCamera == null) return;

            var position = pointer.position.ReadValue();
            if (pointer.press.wasPressedThisFrame)
            {
                pressPosition = position;
                tracking = !MapInputGate.IsBlocked && !IsOverUI(position);
            }

            if (pointer.press.wasReleasedThisFrame && tracking)
            {
                tracking = false;
                if (MapInputGate.IsBlocked) return;
                if ((position - pressPosition).sqrMagnitude > maxTapMovement * maxTapMovement) return;
                HandleTap(position);
            }
        }

        private void HandleTap(Vector2 screenPosition)
        {
            var ray = mapCamera.ScreenPointToRay(screenPosition);
            int mask = GameLayers.Animals >= 0 ? 1 << GameLayers.Animals : Physics.DefaultRaycastLayers;

            // Exact hit first, then a forgiving sphere cast.
            if (Physics.Raycast(ray, out var hit, 1000f, mask, QueryTriggerInteraction.Collide) ||
                Physics.SphereCast(ray, tapRadius, out hit, 1000f, mask, QueryTriggerInteraction.Collide))
            {
                var animal = hit.collider.GetComponentInParent<WildAnimal>();
                if (animal != null && !animal.IsLeaving)
                {
                    AnimalTapped?.Invoke(animal);
                    return;
                }
            }

            if (mapView != null && mapView.RaycastGround(ray, out var ground))
                GroundTapped?.Invoke(ground);
        }

        private bool IsOverUI(Vector2 screenPosition)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            pointerData ??= new PointerEventData(es);
            pointerData.position = screenPosition;
            uiHits.Clear();
            es.RaycastAll(pointerData, uiHits);
            return uiHits.Count > 0;
        }
    }
}
