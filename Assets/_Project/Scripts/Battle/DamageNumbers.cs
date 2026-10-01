using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using WildTamers.UI;

namespace WildTamers.Battle
{
    /// <summary>Floating damage numbers that pop out over an animal, rise and fade (pooled).</summary>
    public class DamageNumbers : MonoBehaviour
    {
        [Tooltip("Inactive template with a 'Value' and a 'Caption' text child.")]
        [SerializeField] private RectTransform template;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float rise = 150f;
        [SerializeField] private float lifetime = 1.15f;
        [Tooltip("Popups never start closer than this to the top of the screen (keeps them off the top card).")]
        [SerializeField] private float topMargin = 600f;

        private class Popup
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public TMP_Text Value;
            public TMP_Text Caption;
            public bool Busy;
        }

        private readonly List<Popup> pool = new List<Popup>();
        private RectTransform area;

        private void Awake()
        {
            area = (RectTransform)transform;
            if (template != null) template.gameObject.SetActive(false);
            if (worldCamera == null) worldCamera = Camera.main;
        }

        public void Show(Vector3 worldPosition, string value, Color color, float scale = 1f, string caption = null, Color? captionColor = null)
        {
            if (template == null || worldCamera == null) return;
            var screen = worldCamera.WorldToScreenPoint(worldPosition);
            if (screen.z < 0f) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out var local);
            local.y = Mathf.Min(local.y, area.rect.yMax - topMargin);

            var popup = Rent();
            popup.Value.text = value;
            popup.Value.color = color;
            bool hasCaption = !string.IsNullOrEmpty(caption);
            popup.Caption.gameObject.SetActive(hasCaption);
            if (hasCaption)
            {
                popup.Caption.text = caption;
                popup.Caption.color = captionColor ?? color;
            }
            popup.Rect.SetAsLastSibling();
            StartCoroutine(Animate(popup, local + new Vector2(Random.Range(-36f, 36f), 0f), scale));
        }

        private Popup Rent()
        {
            foreach (var p in pool)
                if (!p.Busy) { p.Busy = true; return p; }

            var rt = Instantiate(template, area);
            // Not '??': Unity's fake-null for a missing component would skip the AddComponent.
            var group = rt.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            var popup = new Popup
            {
                Rect = rt,
                Group = group,
                Value = rt.Find("Value").GetComponent<TMP_Text>(),
                Caption = rt.Find("Caption").GetComponent<TMP_Text>(),
                Busy = true
            };
            pool.Add(popup);
            return popup;
        }

        private IEnumerator Animate(Popup popup, Vector2 start, float scale)
        {
            var rt = popup.Rect;
            rt.gameObject.SetActive(true);
            popup.Group.alpha = 1f;
            for (float t = 0f; t < lifetime; t += UIEase.GameDeltaTime)
            {
                float p = t / lifetime;
                float pop = t < 0.22f ? UIEase.OutBack(t / 0.22f, 3f) : 1f;
                float s = scale * pop * (1f - 0.12f * Mathf.Max(0f, p - 0.6f));
                rt.localScale = new Vector3(s, s, 1f);
                rt.anchoredPosition = start + Vector2.up * (rise * UIEase.OutCubic(p));
                popup.Group.alpha = p < 0.62f ? 1f : 1f - (p - 0.62f) / 0.38f;
                yield return null;
            }
            rt.gameObject.SetActive(false);
            popup.Busy = false;
        }
    }
}
