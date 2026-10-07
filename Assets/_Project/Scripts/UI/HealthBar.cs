using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WildTamers.UI
{
    /// <summary>
    /// HP bar with a juicy drain: the fill slides down quickly while a pale "ghost" chunk lingers, then catches up.
    /// The fill shifts green → yellow → red as HP drops, and the number counts along with the bar.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [SerializeField] private RectTransform ghost;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Color highColor = new Color32(0x5B, 0xD1, 0x6A, 0xFF);
        [SerializeField] private Color midColor = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
        [SerializeField] private Color lowColor = new Color32(0xFF, 0x6B, 0x6B, 0xFF);
        [SerializeField] private float fillSharpness = 10f;
        [SerializeField] private float ghostDelay = 0.4f;
        [Tooltip("Ghost drain speed in bar-widths per second.")]
        [SerializeField] private float ghostSpeed = 0.8f;
        [Tooltip("Smallest visible fill so rounded caps never collapse.")]
        [SerializeField] private float minVisible = 0.06f;

        private float shown;
        private float target;
        private float ghostValue;
        private float ghostWait;
        private int maxValue = 1;
        private int lastShownNumber = -1;

        public void Set(int current, int max, bool instant = false)
        {
            maxValue = Mathf.Max(1, max);
            float value = Mathf.Clamp01((float)current / maxValue);
            if (value < target) ghostWait = ghostDelay;
            target = value;
            if (instant)
            {
                shown = ghostValue = target;
                ghostWait = 0f;
            }
            if (target > ghostValue) ghostValue = target; // healing: no ghost
            lastShownNumber = -1;
            Apply();
        }

        private void Update()
        {
            float dt = UIEase.DeltaTime;
            if (!Mathf.Approximately(shown, target))
            {
                shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-fillSharpness * dt));
                if (Mathf.Abs(shown - target) < 0.002f) shown = target;
            }
            if (ghostValue > shown)
            {
                if (ghostWait > 0f) ghostWait -= dt;
                else ghostValue = Mathf.Max(shown, ghostValue - ghostSpeed * dt);
            }
            else ghostValue = shown;
            Apply();
        }

        private void Apply()
        {
            if (fill != null)
            {
                BarAnchors.Fill(fill, Visible(shown));
                fill.gameObject.SetActive(shown > 0f);
            }
            if (ghost != null)
            {
                BarAnchors.Fill(ghost, Visible(ghostValue));
                ghost.gameObject.SetActive(ghostValue > shown + 0.001f);
            }
            if (fillImage != null)
                fillImage.color = shown > 0.5f ? Color.Lerp(midColor, highColor, (shown - 0.5f) * 4f)
                    : shown > 0.2f ? Color.Lerp(lowColor, midColor, (shown - 0.2f) * 6f) : lowColor;
            if (valueText != null)
            {
                int number = Mathf.RoundToInt(shown * maxValue);
                if (shown > 0f && number == 0) number = 1;
                if (number != lastShownNumber)
                {
                    lastShownNumber = number;
                    valueText.text = $"{number}/{maxValue}";
                }
            }
        }

        private float Visible(float v) => v <= 0f ? 0f : Mathf.Lerp(minVisible, 1f, v);
    }
}
