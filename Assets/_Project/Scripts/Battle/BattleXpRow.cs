using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;
using WildTamers.Core;
using WildTamers.UI;
using WildTamers.Lang;

namespace WildTamers.Battle
{
    /// <summary>One line of the victory sheet: an animal's portrait, level, XP gained and a bar that fills (and wraps on level-ups).</summary>
    public class BattleXpRow : MonoBehaviour
    {
        [SerializeField] private AnimalPreviewImage portrait;
        [SerializeField] private Image portraitBackdrop;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text gainText;
        [SerializeField] private StatBar xpBar;
        [SerializeField] private CanvasGroup levelUpChip;

        private AnimalInstance shownAnimal;
        private int shownLevel;
        private int shownXp;

        public bool LevelledUp { get; private set; }

        private void OnEnable() => Loc.LanguageChanged += RefreshTexts;

        private void OnDisable() => Loc.LanguageChanged -= RefreshTexts;

        private void RefreshTexts()
        {
            if (shownAnimal == null) return;
            nameText.text = shownAnimal.Name;
            levelText.text = Loc.Level(shownLevel);
            gainText.text = Loc.T("common.xp", shownXp);
        }

        public void Setup(PartyGrowth growth, int xp, GameConfig config)
        {
            var animal = growth.Animal;
            shownAnimal = animal;
            shownLevel = growth.Growth.OldLevel;
            shownXp = xp;
            RefreshTexts();
            LevelledUp = growth.Growth.LeveledUp;
            if (animal.Data != null)
            {
                portrait.SetAnimal(animal.Data);
                if (portraitBackdrop != null) portraitBackdrop.color = Color.Lerp(animal.Data.themeColor, Color.white, 0.6f);
            }
            xpBar.SetValue(Fraction(growth.Growth.OldExperience, growth.Growth.OldLevel, config), null, instant: true);
            levelUpChip.alpha = 0f;
            levelUpChip.gameObject.SetActive(false);
        }

        /// <summary>Fills the bar, wrapping once per level gained; <paramref name="levelChanged"/> fires on every level-up.</summary>
        public IEnumerator Fill(PartyGrowth growth, GameConfig config, Action<int> levelChanged)
        {
            var g = growth.Growth;
            int level = g.OldLevel;
            float from = Fraction(g.OldExperience, level, config);
            while (level < g.NewLevel)
            {
                yield return FillBar(from, 1f, 0.55f);
                level++;
                shownLevel = level;
                levelText.text = Loc.Level(level);
                levelChanged?.Invoke(level);
                StartCoroutine(Punch(levelText.rectTransform, 1.35f));
                from = 0f;
            }
            float to = level >= config.maxLevel ? 1f : Fraction(g.NewExperience, level, config);
            yield return FillBar(from, to, 0.45f);
        }

        /// <summary>"LEVEL UP!" chip pops in next to the bar.</summary>
        public IEnumerator ShowLevelUp()
        {
            levelUpChip.gameObject.SetActive(true);
            var rt = (RectTransform)levelUpChip.transform;
            for (float t = 0f; t < 0.4f; t += UIEase.DeltaTime)
            {
                float p = t / 0.4f;
                levelUpChip.alpha = Mathf.Clamp01(p * 2f);
                float s = UIEase.OutBack(p, 2.6f);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            levelUpChip.alpha = 1f;
            rt.localScale = Vector3.one;
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
    }
}
