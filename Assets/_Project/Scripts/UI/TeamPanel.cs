using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Core;

namespace WildTamers.UI
{
    /// <summary>List of the player's animals (name, level, HP). Tap one to make it the active fighter; small "Reset save" for testing.</summary>
    public class TeamPanel : UIPanel
    {
        [SerializeField] private RectTransform listContent;
        [SerializeField] private TeamRow rowPrefab;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private ScrollRect scroll;
        [Tooltip("Hint shown under the list.")]
        [SerializeField] private RectTransform footer;
        [Tooltip("The card hugs the list between these heights (a small team doesn't get a mostly empty sheet).")]
        [SerializeField] private float minCardHeight = 620f;
        [SerializeField] private float maxCardHeight = 1580f;
        [Tooltip("Card height that isn't the list: title area and bottom padding.")]
        [SerializeField] private float cardChrome = 244f;

        [Header("Reset save (testing)")]
        [SerializeField] private Button resetButton;
        [SerializeField] private TMP_Text resetLabel;
        [SerializeField] private float confirmWindow = 4f;
        [SerializeField] private Color resetColor = new Color32(0x7A, 0x86, 0x9A, 0xFF);
        [SerializeField] private Color confirmColor = new Color32(0xEF, 0x47, 0x6F, 0xFF);

        private readonly List<TeamRow> rows = new List<TeamRow>();
        private float confirmUntil = -1f;
        private float hpTimer;

        protected override void Awake()
        {
            base.Awake();
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (backdropButton != null) backdropButton.onClick.AddListener(Hide);
            if (resetButton != null) resetButton.onClick.AddListener(OnResetPressed);
        }

        protected override void OnShown()
        {
            GameSession.Instance.TeamChanged -= Rebuild;
            GameSession.Instance.TeamChanged += Rebuild;
            SetResetArmed(false);
            Rebuild();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        protected override void OnHidden()
        {
            if (GameSession.Exists) GameSession.Instance.TeamChanged -= Rebuild;
            SetResetArmed(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (GameSession.Exists) GameSession.Instance.TeamChanged -= Rebuild;
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
            if (confirmUntil > 0f && Time.unscaledTime > confirmUntil) SetResetArmed(false);
        }

        private void Rebuild()
        {
            var session = GameSession.Instance;
            var team = session.Team;
            while (rows.Count < team.Count) rows.Add(Instantiate(rowPrefab, listContent));
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < team.Count;
                rows[i].gameObject.SetActive(used);
                if (used) rows[i].Setup(team[i], i, i == session.ActiveIndex, OnRowSelected);
            }
            if (countText != null) countText.text = team.Count == 1 ? "1 animal" : $"{team.Count} animals";
            if (footer != null) footer.SetAsLastSibling();
            FitCard();
        }

        private void OnRowSelected(TeamRow row)
        {
            var session = GameSession.Instance;
            if (row.Index == session.ActiveIndex) return;
            session.SetActive(row.Index); // raises TeamChanged → Rebuild
            row.PlaySelected();
        }

        private void FitCard()
        {
            if (card == null || listContent == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            float height = LayoutUtility.GetPreferredHeight(listContent) + cardChrome;
            card.sizeDelta = new Vector2(card.sizeDelta.x, Mathf.Clamp(height, minCardHeight, maxCardHeight));
        }

        // ---------- Reset save ----------

        private void OnResetPressed()
        {
            if (confirmUntil < 0f)
            {
                SetResetArmed(true);
                return;
            }
            SetResetArmed(false);
            Hide();
            GameSession.Instance.ResetSave();
        }

        private void SetResetArmed(bool armed)
        {
            confirmUntil = armed ? Time.unscaledTime + confirmWindow : -1f;
            if (resetLabel == null) return;
            resetLabel.text = armed ? "Tap again to reset" : "Reset save";
            resetLabel.color = armed ? confirmColor : resetColor;
        }
    }
}
