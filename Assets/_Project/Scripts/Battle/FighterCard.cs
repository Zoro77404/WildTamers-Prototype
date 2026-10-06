using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.UI;

namespace WildTamers.Battle
{
    /// <summary>
    /// Name / level / HP card for one animal in the battle: three small cards for your team and one big card for the wild boss.
    /// Shows whose turn it is (glowing frame, lifted card, order number), guarding and fainted states.
    /// </summary>
    public class FighterCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private HealthBar hpBar;
        [SerializeField] private Image accent;
        [SerializeField] private CanvasGroup guardChip;
        [SerializeField] private RectTransform body;

        [Header("Team card extras (optional)")]
        [SerializeField] private AnimalPreviewImage portrait;
        [SerializeField] private Image portraitBackdrop;

        [Header("Turn highlight")]
        [SerializeField] private Image turnGlow;
        [SerializeField] private GameObject turnBadge;
        [SerializeField] private GameObject orderBadge;
        [SerializeField] private TMP_Text orderText;
        [SerializeField] private GameObject faintedBadge;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Color glowColor = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
        [SerializeField] private float liftScale = 1.06f;

        private Coroutine guardRoutine;
        private Coroutine punchRoutine;
        private bool turn;
        private bool fainted;
        private float scale = 1f;

        public void Bind(AnimalInstance animal, GameConfig config)
        {
            nameText.text = animal.Name;
            SetLevel(animal.Level);
            hpBar.Set(animal.CurrentHP, animal.MaxHP, instant: true);
            if (accent != null && animal.Data != null) accent.color = animal.Data.themeColor;
            if (portrait != null) portrait.SetAnimal(animal.Data);
            if (portraitBackdrop != null && animal.Data != null) portraitBackdrop.color = Color.Lerp(animal.Data.themeColor, Color.white, 0.6f);
            if (guardChip != null)
            {
                guardChip.alpha = 0f;
                guardChip.gameObject.SetActive(false);
            }
            fainted = false;
            SetTurn(false);
            SetOrder(0);
            if (faintedBadge != null) faintedBadge.SetActive(false);
            if (group != null) group.alpha = 1f;
        }

        public void SetLevel(int level) => levelText.text = $"Lv. {level}";

        public void SetHP(AnimalInstance animal, bool instant = false) => hpBar.Set(animal.CurrentHP, animal.MaxHP, instant);

        // ---------- Turn highlight ----------

        /// <summary>Highlights the card of the animal whose turn it is.</summary>
        public void SetTurn(bool on)
        {
            turn = on && !fainted;
            if (turnGlow != null) turnGlow.gameObject.SetActive(turn);
            if (turnBadge != null) turnBadge.SetActive(turn);
            if (!isActiveAndEnabled) transform.localScale = Vector3.one;
        }

        /// <summary>Turn order number for this round (0 hides it).</summary>
        public void SetOrder(int number)
        {
            if (orderBadge == null) return;
            bool show = number > 0 && !fainted;
            orderBadge.SetActive(show);
            if (show && orderText != null) orderText.text = number.ToString();
        }

        /// <summary>Greys the card out for an animal that can't fight any more.</summary>
        public void SetFainted(bool value)
        {
            fainted = value;
            if (value)
            {
                SetTurn(false);
                SetOrder(0);
                SetGuard(false);
            }
            if (faintedBadge != null) faintedBadge.SetActive(value);
            if (group != null) group.alpha = value ? 0.55f : 1f;
        }

        private void Update()
        {
            float target = turn ? liftScale : 1f;
            scale = Mathf.MoveTowards(scale, target, UIEase.DeltaTime * 1.2f);
            transform.localScale = new Vector3(scale, scale, 1f);
            if (turn && turnGlow != null)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 7f);
                var c = glowColor;
                c.a = pulse;
                turnGlow.color = c;
            }
        }

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
            if (!on && !guardChip.gameObject.activeSelf) return;
            if (guardRoutine != null) StopCoroutine(guardRoutine);
            guardRoutine = StartCoroutine(GuardRoutine(on));
        }

        private IEnumerator PunchRoutine()
        {
            for (float t = 0f; t < 0.3f; t += UIEase.DeltaTime)
            {
                float damp = 1f - t / 0.3f;
                body.anchoredPosition = new Vector2(Mathf.Sin(t * 70f) * 10f * damp, 0f);
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

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
            scale = 1f;
        }
    }
}
