using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Core;
using WildTamers.Lang;

namespace WildTamers.UI
{
    /// <summary>List of the player's animals (name, level, HP). Tap one to see its info card.</summary>
    public class TeamPanel : UIPanel
    {
        [SerializeField] private RectTransform listContent;
        [SerializeField] private TeamRow rowPrefab;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private AnimalCardPanel animalCard;
        [Tooltip("Hint shown under the list.")]
        [SerializeField] private RectTransform footer;
        [Tooltip("The card hugs the list between these heights (a small team doesn't get a mostly empty sheet).")]
        [SerializeField] private float minCardHeight = 620f;
        [SerializeField] private float maxCardHeight = 1580f;
        [Tooltip("Card height that isn't the list: title area and bottom padding.")]
        [SerializeField] private float cardChrome = 244f;

        private readonly List<TeamRow> rows = new List<TeamRow>();
        private float hpTimer;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (backdropButton != null) backdropButton.onClick.AddListener(Hide);
        }

        protected override void OnShown()
        {
            GameSession.Instance.TeamChanged -= Rebuild;
            GameSession.Instance.TeamChanged += Rebuild;
            Loc.LanguageChanged -= Rebuild;
            Loc.LanguageChanged += Rebuild;
            Rebuild();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        protected override void OnHidden()
        {
            if (GameSession.Exists) GameSession.Instance.TeamChanged -= Rebuild;
            Loc.LanguageChanged -= Rebuild;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (GameSession.Exists) GameSession.Instance.TeamChanged -= Rebuild;
            Loc.LanguageChanged -= Rebuild;
        }

        private void Update()
        {
            if (!IsVisible) return;
            // HP slowly recovers while the panel is open.
            hpTimer -= UIEase.DeltaTime;
            if (hpTimer <= 0f)
            {
                hpTimer = 0.25f;
                foreach (var row in rows)
                    if (row.gameObject.activeSelf) row.RefreshHP();
            }
        }

        private void Rebuild()
        {
            var session = GameSession.Instance;
            var team = session.Team;
            var lastTeam = session.LastTeam.ToList();
            while (rows.Count < team.Count) rows.Add(Instantiate(rowPrefab, listContent));
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < team.Count;
                rows[i].gameObject.SetActive(used);
                if (used) rows[i].Setup(team[i], i, lastTeam.Contains(team[i]), OnRowSelected);
            }
            if (countText != null) countText.text = Loc.T("team.count", team.Count);
            if (footer != null) footer.SetAsLastSibling();
            FitCard();
        }

        private void OnRowSelected(TeamRow row)
        {
            row.PlaySelected();
            if (animalCard != null && !animalCard.IsVisible) animalCard.OpenInfo(row.Animal.Data, row.Animal.Level);
        }

        private void FitCard()
        {
            if (card == null || listContent == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            float height = LayoutUtility.GetPreferredHeight(listContent) + cardChrome;
            card.sizeDelta = new Vector2(card.sizeDelta.x, Mathf.Clamp(height, minCardHeight, maxCardHeight));
        }
    }
}
