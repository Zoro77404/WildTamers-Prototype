using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildTamers.Core;

namespace WildTamers.UI
{
    /// <summary>List of the player's animals (name, level, HP).</summary>
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

        private readonly List<TeamRow> rows = new List<TeamRow>();

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
            Rebuild();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        protected override void OnHidden()
        {
            if (GameSession.Exists) GameSession.Instance.TeamChanged -= Rebuild;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (GameSession.Exists) GameSession.Instance.TeamChanged -= Rebuild;
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
                if (used) rows[i].Setup(team[i], i == session.ActiveIndex);
            }
            if (countText != null) countText.text = team.Count == 1 ? "1 animal" : $"{team.Count} animals";
            if (footer != null) footer.SetAsLastSibling();
        }
    }
}
