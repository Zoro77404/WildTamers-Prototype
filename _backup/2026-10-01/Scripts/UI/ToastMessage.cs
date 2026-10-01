using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WildTamers.UI
{
    /// <summary>Short message that slides in, waits, and fades away (e.g. "Get closer!").</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastMessage : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image background;
        [SerializeField] private Color normalColor = new Color32(0x26, 0x32, 0x4A, 0xEE);
        [SerializeField] private Color warningColor = new Color32(0xEF, 0x47, 0x6F, 0xF2);
        [SerializeField] private float slideDistance = 40f;

        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 restPosition;
        private Coroutine routine;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
            restPosition = rect.anchoredPosition;
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        public void Show(string message, bool warning = false, float duration = 1.6f)
        {
            if (group == null) Awake();
            gameObject.SetActive(true);
            label.text = message;
            if (background != null) background.color = warning ? warningColor : normalColor;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run(duration, warning));
        }

        private IEnumerator Run(float duration, bool shake)
        {
            const float inTime = 0.22f, outTime = 0.3f;
            for (float t = 0f; t < inTime; t += Time.unscaledDeltaTime)
            {
                float p = UIEase.OutBack(t / inTime);
                group.alpha = Mathf.Clamp01(t / inTime * 1.5f);
                rect.anchoredPosition = restPosition + Vector2.up * (1f - p) * slideDistance;
                rect.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, p);
                yield return null;
            }
            group.alpha = 1f;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = restPosition;

            if (shake)
            {
                for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
                {
                    float damp = 1f - t / 0.3f;
                    rect.anchoredPosition = restPosition + Vector2.right * Mathf.Sin(t * 70f) * 14f * damp;
                    yield return null;
                }
                rect.anchoredPosition = restPosition;
            }

            float hold = duration;
            while (hold > 0f) { hold -= Time.unscaledDeltaTime; yield return null; }

            for (float t = 0f; t < outTime; t += Time.unscaledDeltaTime)
            {
                group.alpha = 1f - t / outTime;
                yield return null;
            }
            group.alpha = 0f;
            routine = null;
        }
    }
}
