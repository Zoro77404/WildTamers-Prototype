using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.UI;

namespace WildTamers.Battle
{
    /// <summary>
    /// End-of-battle sheet. Victory: who joined, XP bar filling up and a "Level up!" banner with stat gains.
    /// Defeat: fainted message and the team heal note. Continue goes back to the map.
    /// </summary>
    public class BattleResultPanel : UIPanel
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Image titleBadge;

        [Header("Experience")]
        [SerializeField] private GameObject xpSection;
        [SerializeField] private TMP_Text xpNameText;
        [SerializeField] private TMP_Text xpLevelText;
        [SerializeField] private TMP_Text xpGainText;
        [SerializeField] private StatBar xpBar;

        [Header("Level up")]
        [SerializeField] private CanvasGroup levelUpGroup;
        [SerializeField] private TMP_Text levelUpTitle;
        [SerializeField] private TMP_Text levelUpStats;

        [SerializeField] private Button continueButton;
        [Header("Card heights")]
        [SerializeField] private float victoryHeight = 444f;
        [SerializeField] private float levelUpHeight = 520f;
        [SerializeField] private float defeatHeight = 384f;
        [SerializeField] private Color victoryColor = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        [SerializeField] private Color defeatColor = new Color32(0x8F, 0xA3, 0xBF, 0xFF);

        private Action onContinue;
        private bool canContinue;

        protected override void Awake()
        {
            base.Awake();
            continueButton.onClick.AddListener(ContinuePressed);
        }

        /// <summary>Fires when the player taps Continue.</summary>
        public void OnContinue(Action action) => onContinue = action;

        public IEnumerator PlayVictory(BattleResult result, AnimalInstance fighter, GameConfig config, FighterCard card)
        {
            titleText.text = "Victory!";
            if (titleBadge != null) titleBadge.color = victoryColor;
            var joined = result.Joined;
            messageText.text = joined != null ? $"<b>{joined.Name}</b> (Lv. {joined.Level}) joined your team!" : "You won!";
            xpSection.SetActive(true);
            xpNameText.text = fighter.Name;
            xpGainText.text = $"+{result.ExperienceGained} XP";
            levelUpGroup.alpha = 0f;
            levelUpGroup.gameObject.SetActive(false);

            var growth = result.Growth;
            SetHeight(growth.LeveledUp ? levelUpHeight : victoryHeight);
            int level = growth.OldLevel;
            xpLevelText.text = $"Lv. {level}";
            xpBar.SetValue(Fraction(growth.OldExperience, level, config), null, instant: true);
            SetContinue(false);
            Show();
            yield return Wait(0.45f);

            // Fill the bar, wrapping once per level gained.
            float from = Fraction(growth.OldExperience, level, config);
            while (level < growth.NewLevel)
            {
                yield return FillBar(from, 1f, 0.55f);
                level++;
                xpLevelText.text = $"Lv. {level}";
                card.SetLevel(level);
                StartCoroutine(Punch(xpLevelText.rectTransform, 1.35f));
                from = 0f;
            }
            float to = level >= config.maxLevel ? 1f : Fraction(growth.NewExperience, level, config);
            yield return FillBar(from, to, 0.45f);
            card.SetXP(to, instant: false);

            if (growth.LeveledUp)
            {
                levelUpTitle.text = growth.LevelsGained > 1 ? $"Level up! ×{growth.LevelsGained}" : "Level up!";
                levelUpStats.text =
                    $"HP <b>+{growth.NewMaxHP - growth.OldMaxHP}</b>   ATK <b>+{growth.NewAttack - growth.OldAttack}</b>   " +
                    $"DEF <b>+{growth.NewDefense - growth.OldDefense}</b>   SPD <b>+{growth.NewSpeed - growth.OldSpeed}</b>";
                yield return ShowLevelUp();
            }
            SetContinue(true);
        }

        public void ShowDefeat(AnimalInstance fighter)
        {
            titleText.text = "Defeated…";
            if (titleBadge != null) titleBadge.color = defeatColor;
            messageText.text = $"<b>{fighter.Name}</b> fainted.\nYour team rested and is fully healed.";
            xpSection.SetActive(false);
            levelUpGroup.gameObject.SetActive(false);
            SetHeight(defeatHeight);
            SetContinue(true);
            Show();
        }

        private void Update()
        {
            if (!canContinue || !IsVisible) return;
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                ContinuePressed();
        }

        private void ContinuePressed()
        {
            if (!canContinue) return;
            SetContinue(false);
            onContinue?.Invoke();
        }

        private void SetHeight(float height)
        {
            if (card != null) card.sizeDelta = new Vector2(card.sizeDelta.x, height);
        }

        private void SetContinue(bool value)
        {
            canContinue = value;
            continueButton.interactable = value;
        }

        private IEnumerator FillBar(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += UIEase.DeltaTime)
            {
                xpBar.SetValue(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)), null, instant: true);
                yield return null;
            }
            xpBar.SetValue(to, null, instant: true);
        }

        private IEnumerator ShowLevelUp()
        {
            levelUpGroup.gameObject.SetActive(true);
            var rt = (RectTransform)levelUpGroup.transform;
            for (float t = 0f; t < 0.4f; t += UIEase.DeltaTime)
            {
                float p = t / 0.4f;
                levelUpGroup.alpha = Mathf.Clamp01(p * 2f);
                float s = UIEase.OutBack(p, 2.6f);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            levelUpGroup.alpha = 1f;
            rt.localScale = Vector3.one;
            yield return Wait(0.25f);
        }

        private static IEnumerator Punch(RectTransform rt, float amount)
        {
            for (float t = 0f; t < 0.3f; t += UIEase.DeltaTime)
            {
                float s = 1f + (amount - 1f) * Mathf.Sin(t / 0.3f * Mathf.PI);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        private static float Fraction(int xp, int level, GameConfig config) =>
            level >= config.maxLevel ? 1f : (float)xp / Mathf.Max(1, config.ExperienceToNext(level));

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += UIEase.DeltaTime) yield return null;
        }
    }
}
