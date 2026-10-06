using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Animals;
using WildTamers.Core;

namespace WildTamers.UI
{
    /// <summary>
    /// Shown before every fight: pick the animals that will fight (3, or fewer if fewer can fight right now).
    /// Starts from the team you used last time. Tap an animal to pick or drop it; the little "i" opens its info card.
    /// </summary>
    public class TeamSelectScreen : UIPanel
    {
        [SerializeField] private RectTransform listContent;
        [SerializeField] private TeamPickRow rowPrefab;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private TMP_Text subtitleText;
        [SerializeField] private TMP_Text noteText;
        [SerializeField] private Button fightButton;
        [SerializeField] private TMP_Text fightLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private AnimalCardPanel infoCard;

        [Header("Chosen-team strip")]
        [SerializeField] private AnimalPreviewImage[] slotPortraits = new AnimalPreviewImage[3];
        [SerializeField] private Image[] slotBackdrops = new Image[3];
        [SerializeField] private GameObject[] slotEmpty = new GameObject[3];

        private readonly List<TeamPickRow> rows = new List<TeamPickRow>();
        private readonly List<AnimalInstance> picked = new List<AnimalInstance>();
        private Action<IReadOnlyList<AnimalInstance>> onConfirm;
        private Action onBack;
        private int capacity;
        private bool resolved;

        protected override void Awake()
        {
            base.Awake();
            fightButton.onClick.AddListener(Confirm);
            if (backButton != null) backButton.onClick.AddListener(Back);
            if (backdropButton != null) backdropButton.onClick.AddListener(Back);
        }

        /// <param name="foe">The wild animal about to be fought (for the subtitle).</param>
        public void Open(AnimalData foe, int foeLevel, Action<IReadOnlyList<AnimalInstance>> confirm, Action back)
        {
            var session = GameSession.Instance;
            onConfirm = confirm;
            onBack = back;
            resolved = false;

            var candidates = session.GetFightCandidates();
            capacity = Mathf.Min(session.Config.partySize, candidates.Count);
            picked.Clear();
            picked.AddRange(session.ChooseDefaultParty());

            subtitleText.text = foe != null ? $"Wild {foe.displayName}  •  Lv. {foeLevel}" : "Pick your fighters";
            if (noteText != null)
            {
                bool short_ = capacity < session.Config.partySize;
                noteText.gameObject.SetActive(true);
                noteText.text = short_
                    ? (capacity == 1 ? "Only 1 animal can fight right now." : $"Only {capacity} animals can fight right now.")
                    : $"Pick {capacity} animals for this fight.";
            }

            Rebuild(session, candidates);
            Refresh();
            Show();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void Rebuild(GameSession session, List<AnimalInstance> candidates)
        {
            // Animals that can fight first (strongest on top), the ones that are resting at the bottom.
            var ordered = session.Team.OrderByDescending(a => candidates.Contains(a)).ThenByDescending(a => a.Level).ToList();
            while (rows.Count < ordered.Count) rows.Add(Instantiate(rowPrefab, listContent));
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < ordered.Count;
                rows[i].gameObject.SetActive(used);
                if (used) rows[i].Setup(ordered[i], candidates.Contains(ordered[i]), OnRowToggled, OnRowInfo);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
        }

        private void OnRowToggled(TeamPickRow row)
        {
            if (resolved) return;
            if (picked.Contains(row.Animal)) picked.Remove(row.Animal);
            else
            {
                // Full team: the animal picked first makes room.
                if (picked.Count >= capacity && picked.Count > 0) picked.RemoveAt(0);
                picked.Add(row.Animal);
            }
            Refresh();
        }

        private void OnRowInfo(TeamPickRow row)
        {
            if (infoCard != null && !infoCard.IsVisible) infoCard.OpenInfo(row.Animal.Data, row.Animal.Level);
        }

        private void Refresh()
        {
            foreach (var row in rows)
                if (row.gameObject.activeSelf) row.SetPick(picked.IndexOf(row.Animal) + 1);

            for (int i = 0; i < slotPortraits.Length; i++)
            {
                bool has = i < picked.Count;
                if (slotEmpty[i] != null) slotEmpty[i].SetActive(!has && i < capacity);
                slotPortraits[i].gameObject.SetActive(has);
                if (slotBackdrops[i] != null) slotBackdrops[i].gameObject.SetActive(i < capacity);
                if (has)
                {
                    slotPortraits[i].SetAnimal(picked[i].Data);
                    if (slotBackdrops[i] != null && picked[i].Data != null)
                        slotBackdrops[i].color = Color.Lerp(picked[i].Data.themeColor, Color.white, 0.55f);
                }
                else if (slotBackdrops[i] != null) slotBackdrops[i].color = new Color32(0xE3, 0xE9, 0xF1, 0xFF);
            }

            bool ready = picked.Count == capacity && capacity > 0;
            fightButton.interactable = ready;
            int missing = capacity - picked.Count;
            fightLabel.text = ready ? "Fight!" : missing == 1 ? "Pick 1 more" : $"Pick {missing} more";
        }

        private void Confirm()
        {
            if (resolved || picked.Count != capacity || capacity == 0) return;
            resolved = true;
            fightButton.interactable = false;
            onConfirm?.Invoke(picked.ToList());
        }

        private void Back()
        {
            if (resolved || (infoCard != null && infoCard.IsVisible)) return;
            resolved = true;
            Hide();
            onBack?.Invoke();
        }

        // ---------- Test hooks ----------

        /// <summary>Picks exactly these animals (play-tests drive the screen through this).</summary>
        public void ForcePick(IEnumerable<AnimalInstance> animals)
        {
            picked.Clear();
            picked.AddRange(animals.Take(capacity));
            Refresh();
        }
    }
}
