using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;
using WildTamers.Lang;
using WildTamers.Audio;

namespace WildTamers.UI
{
    /// <summary>One animal on the team select screen: portrait, level, HP, a numbered tick when picked, and an info button.</summary>
    public class TeamPickRow : MonoBehaviour
    {
        [SerializeField] private AnimalPreviewImage portrait;
        [SerializeField] private Image portraitBackdrop;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private StatBar hpBar;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Image background;
        [SerializeField] private Image pickedFrame;
        [SerializeField] private GameObject pickBadge;
        [SerializeField] private TMP_Text pickNumber;
        [SerializeField] private GameObject emptyBadge;
        [SerializeField] private GameObject restingTag;
        [SerializeField] private TMP_Text restingText;
        [SerializeField] private Button selectButton;
        [SerializeField] private Button infoButton;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Color normalColor = new Color32(0xF3, 0xF6, 0xFA, 0xFF);
        [SerializeField] private Color pickedColor = new Color32(0xE2, 0xF7, 0xF4, 0xFF);

        private Action<TeamPickRow> onToggle;
        private Action<TeamPickRow> onInfo;

        public AnimalInstance Animal { get; private set; }
        public bool Selectable { get; private set; }

        private void Awake()
        {
            if (selectButton != null) selectButton.onClick.AddListener(() => { AudioManager.Play(Sfx.Click); onToggle?.Invoke(this); });
            if (infoButton != null) infoButton.onClick.AddListener(() => { AudioManager.Play(Sfx.Click); onInfo?.Invoke(this); });
        }

        public void Setup(AnimalInstance animal, bool selectable, Action<TeamPickRow> toggle, Action<TeamPickRow> info)
        {
            Animal = animal;
            Selectable = selectable;
            onToggle = toggle;
            onInfo = info;

            var data = animal.Data;
            RefreshTexts();
            hpBar.SetValue(animal.HPFraction, null, instant: true);
            if (data != null)
            {
                if (portraitBackdrop != null) portraitBackdrop.color = Color.Lerp(data.themeColor, Color.white, 0.65f);
                portrait.SetAnimal(data);
            }
            if (restingTag != null) restingTag.SetActive(!selectable);
            if (group != null) group.alpha = selectable ? 1f : 0.55f;
            if (selectButton != null) selectButton.interactable = selectable;
            SetPick(0);
        }

        private void OnEnable() => Loc.LanguageChanged += RefreshTexts;

        private void OnDisable() => Loc.LanguageChanged -= RefreshTexts;

        /// <summary>Name, level, HP and the resting tag in the current language.</summary>
        private void RefreshTexts()
        {
            if (Animal == null) return;
            nameText.text = Animal.Name;
            levelText.text = Loc.Level(Animal.Level);
            hpText.text = Loc.Hp(Animal.CurrentHP, Animal.MaxHP);
            if (restingText != null) restingText.text = Loc.T(Animal.IsFainted ? "select.fainted" : "select.resting");
        }

        /// <summary>Order the animal was picked in (1–3), or 0 when not picked.</summary>
        public void SetPick(int order)
        {
            bool picked = order > 0;
            if (pickBadge != null) pickBadge.SetActive(picked);
            if (emptyBadge != null) emptyBadge.SetActive(!picked && Selectable);
            if (pickNumber != null && picked) pickNumber.text = order.ToString();
            if (pickedFrame != null) pickedFrame.enabled = picked;
            if (background != null) background.color = picked ? pickedColor : normalColor;
        }
    }
}
