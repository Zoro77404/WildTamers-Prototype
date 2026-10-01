using System.Collections;
using UnityEngine;
using WildTamers.Core;

namespace WildTamers.UI
{
    /// <summary>
    /// Base for screens and popups: fades the backdrop, pops the card in with a little overshoot,
    /// and pauses map input while visible.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanel : MonoBehaviour
    {
        [Tooltip("The content that scales in. Leave empty to only fade.")]
        [SerializeField] protected RectTransform card;
        [SerializeField] private bool blocksMapInput = true;
        [SerializeField] private float showDuration = 0.32f;
        [SerializeField] private float hideDuration = 0.16f;

        private CanvasGroup group;
        private Coroutine routine;

        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            group = GetComponent<CanvasGroup>();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (group == null) group = GetComponent<CanvasGroup>();
            IsVisible = true;
            if (blocksMapInput) MapInputGate.Block(this);
            group.interactable = true;
            group.blocksRaycasts = true;
            Restart(Animate(true));
            OnShown();
        }

        public void Hide()
        {
            if (!IsVisible) return;
            IsVisible = false;
            MapInputGate.Unblock(this);
            group.interactable = false;
            if (gameObject.activeInHierarchy) Restart(Animate(false));
            else gameObject.SetActive(false);
            OnHidden();
        }

        /// <summary>Hides instantly without animation.</summary>
        public void HideImmediate()
        {
            IsVisible = false;
            MapInputGate.Unblock(this);
            gameObject.SetActive(false);
        }

        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }

        protected virtual void OnDisable()
        {
            MapInputGate.Unblock(this);
            routine = null;
        }

        private void Restart(IEnumerator r)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(r);
        }

        private IEnumerator Animate(bool show)
        {
            float duration = show ? showDuration : hideDuration;
            float startAlpha = group.alpha;
            float endAlpha = show ? 1f : 0f;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float p = t / duration;
                group.alpha = Mathf.Lerp(startAlpha, endAlpha, show ? UIEase.OutCubic(p * 1.6f) : p);
                if (card != null)
                {
                    float s = show ? Mathf.LerpUnclamped(0.86f, 1f, UIEase.OutBack(p)) : Mathf.Lerp(1f, 0.92f, UIEase.InCubic(p));
                    card.localScale = new Vector3(s, s, 1f);
                }
                yield return null;
            }
            group.alpha = endAlpha;
            if (card != null) card.localScale = show ? Vector3.one : new Vector3(0.92f, 0.92f, 1f);
            routine = null;
            if (!show)
            {
                group.blocksRaycasts = false;
                gameObject.SetActive(false);
            }
        }
    }
}
