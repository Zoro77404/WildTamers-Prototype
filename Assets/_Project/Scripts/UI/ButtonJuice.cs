using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WildTamers.UI
{
    /// <summary>Springy press/hover scale for buttons, plus the click sound (on every press, also keyboard shortcuts).</summary>
    public class ButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float pressedScale = 0.93f;
        [SerializeField] private float hoverScale = 1.03f;
        [SerializeField] private float stiffness = 380f;
        [SerializeField] private float damping = 18f;

        private Selectable selectable;
        private bool pressed, hovered;
        private float scale = 1f, velocity;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            if (selectable is Button button) ClickSound.Hook(button);
        }

        private void OnDisable()
        {
            pressed = hovered = false;
            scale = 1f;
            velocity = 0f;
            transform.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData) => pressed = true;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;
        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }

        private void Update()
        {
            bool interactable = selectable == null || selectable.IsInteractable();
            float target = !interactable ? 1f : pressed ? pressedScale : hovered ? hoverScale : 1f;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            velocity += (target - scale) * stiffness * dt;
            velocity *= Mathf.Exp(-damping * dt);
            scale += velocity * dt;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
