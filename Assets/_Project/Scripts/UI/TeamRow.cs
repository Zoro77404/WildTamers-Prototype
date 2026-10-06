using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;

namespace WildTamers.UI
{
    /// <summary>One animal in the team list: portrait, name, level and HP. Tap it to see its info card.</summary>
    public class TeamRow : MonoBehaviour
    {
        [SerializeField] private AnimalPreviewImage portrait;
        [SerializeField] private Image portraitBackdrop;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private StatBar hpBar;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private GameObject activeBadge;
        [SerializeField] private Image highlight;
        [SerializeField] private Button selectButton;
        [Tooltip("'Info' pill shown on rows that are not in the last fight team.")]
        [SerializeField] private GameObject choosePill;

        private AnimalInstance animal;
        private Action<TeamRow> onSelected;
        private int shownHP = -1;
        private Coroutine bounce;

        public int Index { get; private set; }
        public AnimalInstance Animal => animal;

        private void Awake()
        {
            if (selectButton != null) selectButton.onClick.AddListener(() => onSelected?.Invoke(this));
        }

        public void Setup(AnimalInstance animal, int index, bool inLastTeam, Action<TeamRow> selected)
        {
            this.animal = animal;
            Index = index;
            onSelected = selected;

            var data = animal.Data;
            nameText.text = animal.Name;
            levelText.text = $"Lv. {animal.Level}";
            shownHP = -1;
            RefreshHP(instant: true);
            if (activeBadge != null) activeBadge.SetActive(inLastTeam);
            if (choosePill != null) choosePill.SetActive(!inLastTeam);
            if (highlight != null) highlight.enabled = inLastTeam;
            if (data != null)
            {
                if (portraitBackdrop != null) portraitBackdrop.color = Color.Lerp(data.themeColor, Color.white, 0.65f);
                portrait.SetAnimal(data);
            }
        }

        /// <summary>Updates the HP bar/text if HP changed (the team heals while walking).</summary>
        public void RefreshHP(bool instant = false)
        {
            if (animal == null || animal.CurrentHP == shownHP) return;
            shownHP = animal.CurrentHP;
            hpBar.SetValue(animal.HPFraction, null, instant);
            hpText.text = $"HP {animal.CurrentHP}/{animal.MaxHP}";
        }

        /// <summary>Happy little pop when this row is tapped.</summary>
        public void PlaySelected()
        {
            if (!isActiveAndEnabled) return;
            if (bounce != null) StopCoroutine(bounce);
            bounce = StartCoroutine(Bounce());
        }

        private IEnumerator Bounce()
        {
            var t = transform;
            for (float time = 0f; time < 0.35f; time += UIEase.DeltaTime)
            {
                float s = 1f + 0.05f * Mathf.Sin(time / 0.35f * Mathf.PI);
                t.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            t.localScale = Vector3.one;
            bounce = null;
        }

        private void OnDisable()
        {
            transform.localScale = Vector3.one;
            bounce = null;
        }
    }
}
