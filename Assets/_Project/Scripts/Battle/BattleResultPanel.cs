using System;
using System.Collections;
using System.Collections.Generic;
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
    /// End-of-battle sheet. Victory: who joined and one XP row per animal of your team (all of them share the win),
    /// each with its bar filling up and a "Level up!" chip. Defeat: fainted message and the team heal note.
    /// Continue goes on (new-animal card, then back to the map).
    /// </summary>
    public class BattleResultPanel : UIPanel
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Image titleBadge;

        [Header("Experience")]
        [SerializeField] private BattleXpRow[] rows = new BattleXpRow[3];

        [SerializeField] private Button continueButton;
        [Header("Card heights")]
        [Tooltip("Card height = this + the height of every XP row shown.")]
        [SerializeField] private float victoryBaseHeight = 408f;
        [SerializeField] private float rowHeight = 150f;
        [SerializeField] private float defeatHeight = 400f;
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

        /// <param name="levelChanged">Called with the animal and its new level whenever an XP bar wraps (to update the battle cards).</param>
        public IEnumerator PlayVictory(BattleResult result, GameConfig config, Action<AnimalInstance, int> levelChanged)
        {
            titleText.text = "Victory!";
            if (titleBadge != null) titleBadge.color = victoryColor;
            var joined = result.Joined;
            string joinText = joined != null ? $"<b>{joined.Name}</b> (Lv. {joined.Level}) joined your team!" : "You won!";
            messageText.text = $"{joinText}\nEach animal of your team gets +{result.ExperienceGained} XP.";

            int count = Mathf.Min(rows.Length, result.Party.Count);
            for (int i = 0; i < rows.Length; i++)
            {
                bool used = i < count;
                rows[i].gameObject.SetActive(used);
                if (used) rows[i].Setup(result.Party[i], result.ExperienceGained, config);
            }
            SetHeight(victoryBaseHeight + rowHeight * count);
            SetContinue(false);
            Show();
            yield return Wait(0.5f);

            // Every bar fills at the same time.
            int running = count;
            for (int i = 0; i < count; i++)
            {
                var growth = result.Party[i];
                StartCoroutine(RunRow(rows[i], growth, config, levelChanged, () => running--));
            }
            while (running > 0) yield return null;

            bool anyLevelUp = false;
            for (int i = 0; i < count; i++)
            {
                if (!rows[i].LevelledUp) continue;
                anyLevelUp = true;
                StartCoroutine(rows[i].ShowLevelUp());
            }
            if (anyLevelUp) yield return Wait(0.5f);
            SetContinue(true);
        }

        private IEnumerator RunRow(BattleXpRow row, PartyGrowth growth, GameConfig config, Action<AnimalInstance, int> levelChanged, Action done)
        {
            yield return row.Fill(growth, config, level => levelChanged?.Invoke(growth.Animal, level));
            done();
        }

        public void ShowDefeat(IReadOnlyList<AnimalInstance> party)
        {
            titleText.text = "Defeated…";
            if (titleBadge != null) titleBadge.color = defeatColor;
            messageText.text = party.Count > 1
                ? "Your whole team fainted.\nIt rested and is fully healed."
                : $"<b>{party[0].Name}</b> fainted.\nYour team rested and is fully healed.";
            foreach (var row in rows) row.gameObject.SetActive(false);
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

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += UIEase.DeltaTime) yield return null;
        }
    }
}
