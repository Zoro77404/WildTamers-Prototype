using TMPro;
using UnityEngine;

namespace WildTamers.UI
{
    /// <summary>Rounded bar whose fill eases to a 0–1 value, with an optional label/value text.</summary>
    public class StatBar : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private float sharpness = 9f;
        [Tooltip("Smallest visible fill so rounded caps never collapse.")]
        [SerializeField] private float minVisible = 0.06f;

        private float current;
        private float target;

        public void SetValue(float normalized, string text = null, bool instant = false)
        {
            target = Mathf.Clamp01(normalized);
            if (instant) current = target;
            if (valueText != null && text != null) valueText.text = text;
            Apply();
        }

        private void Update()
        {
            if (Mathf.Approximately(current, target)) return;
            current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-sharpness * Time.unscaledDeltaTime));
            if (Mathf.Abs(current - target) < 0.001f) current = target;
            Apply();
        }

        private void Apply()
        {
            if (fill == null) return;
            float v = current <= 0f ? 0f : Mathf.Lerp(minVisible, 1f, current);
            fill.anchorMax = new Vector2(v, fill.anchorMax.y);
            fill.gameObject.SetActive(current > 0f);
        }
    }
}
