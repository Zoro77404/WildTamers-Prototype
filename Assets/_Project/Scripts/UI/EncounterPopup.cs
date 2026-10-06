using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;

namespace WildTamers.UI
{
    /// <summary>"A wild Wolf appeared!" card with preview, level, stats and Fight / Leave.</summary>
    public class EncounterPopup : UIPanel
    {
        [SerializeField] private AnimalPreviewImage preview;
        [SerializeField] private Image previewBackdrop;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text styleText;
        [SerializeField] private AnimalStatsView stats;
        [SerializeField] private Button fightButton;
        [SerializeField] private Button leaveButton;
        [Tooltip("Tapping the dimmed background also leaves.")]
        [SerializeField] private Button backdropButton;

        private Action onFight;
        private Action onLeave;
        private bool resolved;

        protected override void Awake()
        {
            base.Awake();
            fightButton.onClick.AddListener(Fight);
            leaveButton.onClick.AddListener(Leave);
            if (backdropButton != null) backdropButton.onClick.AddListener(Leave);
        }

        public void Open(AnimalData data, int level, Action fight, Action leave)
        {
            onFight = fight;
            onLeave = leave;
            resolved = false;

            titleText.text = "Wild boss encounter!";
            nameText.text = data.displayName;
            levelText.text = $"Lv. {level}";
            styleText.text = data.styleLabel;
            if (previewBackdrop != null) previewBackdrop.color = Color.Lerp(data.themeColor, Color.white, 0.7f);
            preview.SetAnimal(data);
            stats.Show(data, level, instant: true);
            fightButton.interactable = true;
            leaveButton.interactable = true;
            Show();
        }

        private void Fight()
        {
            if (resolved) return;
            resolved = true;
            fightButton.interactable = false;
            leaveButton.interactable = false;
            onFight?.Invoke();
        }

        private void Leave()
        {
            if (resolved) return;
            resolved = true;
            Hide();
            onLeave?.Invoke();
        }
    }
}
