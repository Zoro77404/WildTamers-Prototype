using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.UI;

namespace WildTamers.Battle
{
    /// <summary>Name / level / HP card for one side of the battle (the player's card also shows XP).</summary>
    public class FighterCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private HealthBar hpBar;
        [SerializeField] private StatBar xpBar;
        [SerializeField] private Image accent;
        [SerializeField] private CanvasGroup guardChip;
        [SerializeField] private RectTransform body;

        private Coroutine guardRoutine;
        private Coroutine punchRoutine;

        public void Bind(AnimalInstance animal, GameConfig config)
        {
            nameText.text = animal.Name;
            SetLevel(animal.Level);
            hpBar.Set(animal.CurrentHP, animal.MaxHP, instant: true);
            if (xpBar != null) xpBar.SetValue(XpFraction(animal, config), null, instant: true);
            if (accent != null && animal.Data != null) accent.color = animal.Data.themeColor;
            if (guardChip != null)
            {
                guardChip.alpha = 0f;
                guardChip.gameObject.SetActive(false);
            }
        }

        public void SetLevel(int level) => levelText.text = $"Lv. {level}";

        public void SetHP(AnimalInstance animal, bool instant = false) => hpBar.Set(animal.CurrentHP, animal.MaxHP, instant);

        public void SetXP(float fraction, bool instant)
        {
            if (xpBar != null) xpBar.SetValue(fraction, null, instant);
        }

        public static float XpFraction(AnimalInstance animal, GameConfig config) =>
            animal.Level >= config.maxLevel ? 1f : (float)animal.Experience / Mathf.Max(1, animal.ExperienceToNext(config));

        /// <summary>Little jolt when this side takes a hit.</summary>
        public void Punch()
        {
            if (body == null || !isActiveAndEnabled) return;
            if (punchRoutine != null) StopCoroutine(punchRoutine);
            punchRoutine = StartCoroutine(PunchRoutine());
        }

        public void SetGuard(bool on)
        {
            if (guardChip == null || !isActiveAndEnabled) return;
            if (guardRoutine != null) StopCoroutine(guardRoutine);
            guardRoutine = StartCoroutine(GuardRoutine(on));
        }

        private IEnumerator PunchRoutine()
        {
            for (float t = 0f; t < 0.3f; t += UIEase.DeltaTime)
            {
                float damp = 1f - t / 0.3f;
                body.anchoredPosition = new Vector2(Mathf.Sin(t * 70f) * 12f * damp, 0f);
                yield return null;
            }
            body.anchoredPosition = Vector2.zero;
            punchRoutine = null;
        }

        private IEnumerator GuardRoutine(bool on)
        {
            var rt = (RectTransform)guardChip.transform;
            if (on) guardChip.gameObject.SetActive(true);
            float from = guardChip.alpha, to = on ? 1f : 0f;
            for (float t = 0f; t < 0.22f; t += UIEase.DeltaTime)
            {
                float p = t / 0.22f;
                guardChip.alpha = Mathf.Lerp(from, to, p);
                float s = on ? UIEase.OutBack(p, 2.5f) : 1f - 0.2f * p;
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            guardChip.alpha = to;
            rt.localScale = Vector3.one;
            if (!on) guardChip.gameObject.SetActive(false);
            guardRoutine = null;
        }
    }
}
