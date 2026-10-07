using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using WildTamers.Core;
using WildTamers.UI;
using WildTamers.Lang;

namespace WildTamers.Battle
{
    /// <summary>
    /// Portrait battle UI: a big card for the wild boss on top, a small card for each of your (up to 3) animals,
    /// the one-line battle log, the four action buttons, floating damage numbers and the result sheet.
    /// The card of the animal whose turn it is lights up. Keys 1–4 also pick actions when testing on PC.
    /// </summary>
    public class BattleHUD : MonoBehaviour
    {
        [Header("Cards")]
        [Tooltip("Left, middle and right. One animal uses the middle card, two use the outer cards.")]
        [SerializeField] private FighterCard[] partyCards = new FighterCard[3];
        [SerializeField] private FighterCard wildCard;

        [Header("Log")]
        [SerializeField] private CanvasGroup logGroup;
        [SerializeField] private RectTransform logPanel;
        [SerializeField] private TMP_Text logText;
        [Tooltip("Characters revealed per second by the typewriter.")]
        [SerializeField] private float typeSpeed = 90f;

        [Header("Actions")]
        [SerializeField] private CanvasGroup actionPanel;
        [SerializeField] private BattleActionButton attackButton;
        [SerializeField] private BattleActionButton skillButton;
        [SerializeField] private BattleActionButton defendButton;
        [SerializeField] private BattleActionButton runButton;
        [SerializeField] private float hiddenAlpha = 0.35f;

        [Header("Other")]
        [SerializeField] private DamageNumbers damageNumbers;
        [SerializeField] private BattleResultPanel resultPanel;

        private readonly Dictionary<BattleFighter, FighterCard> cards = new Dictionary<BattleFighter, FighterCard>();
        private bool actionsOpen;
        private Coroutine logRoutine;
        private Coroutine panelRoutine;
        private int revealedCharacters;
        private float typedChars;
        private int typedTotal;
        private readonly List<Func<string>> logParts = new List<Func<string>>();
        private BattleFighter actionFighter;
        private float actionEscapeChance;

        public event Action<BattleAction> ActionChosen;

        public FighterCard WildCard => wildCard;
        public DamageNumbers Numbers => damageNumbers;
        public BattleResultPanel Result => resultPanel;

        private void Awake()
        {
            attackButton.Clicked += () => Choose(BattleAction.Attack);
            skillButton.Clicked += () => Choose(BattleAction.Skill);
            defendButton.Clicked += () => Choose(BattleAction.Defend);
            runButton.Clicked += () => Choose(BattleAction.Run);
            if (resultPanel != null) resultPanel.HideImmediate();
            SetActionsVisible(false, instant: true);
        }

        private void OnEnable() => Loc.LanguageChanged += OnLanguageChanged;

        private void OnDisable() => Loc.LanguageChanged -= OnLanguageChanged;

        /// <summary>Redraws the action buttons and the log line in the new language (the log is kept as functions, not text).</summary>
        private void OnLanguageChanged()
        {
            if (actionFighter != null) ApplyActionTexts();
            if (logParts.Count == 0) return;
            if (logRoutine != null)
            {
                StopCoroutine(logRoutine);
                logRoutine = null;
                if (logPanel != null) logPanel.localScale = Vector3.one;
            }
            logText.text = BuildLog();
            logText.maxVisibleCharacters = 99999;
        }

        /// <summary>Card slot (0 left, 1 middle, 2 right) for the <paramref name="index"/>-th of <paramref name="count"/> animals.</summary>
        public static int SlotFor(int index, int count) => count <= 1 ? 1 : count == 2 ? index * 2 : index;

        public void Bind(IReadOnlyList<BattleFighter> party, BattleFighter wild, GameConfig config)
        {
            cards.Clear();
            var used = new HashSet<int>();
            for (int i = 0; i < party.Count; i++)
            {
                int slot = SlotFor(i, party.Count);
                used.Add(slot);
                partyCards[slot].gameObject.SetActive(true);
                partyCards[slot].Bind(party[i].Animal, config);
                cards[party[i]] = partyCards[slot];
            }
            for (int i = 0; i < partyCards.Length; i++)
                if (!used.Contains(i)) partyCards[i].gameObject.SetActive(false);
            wildCard.Bind(wild.Animal, config);
            cards[wild] = wildCard;
        }

        public FighterCard CardFor(BattleFighter fighter) => cards[fighter];

        // ---------- Turn highlight ----------

        /// <summary>
        /// Numbers the cards in turn order and lights up the card of whoever acts now.
        /// <paramref name="currentIndex"/> is the position in <paramref name="order"/> of the animal whose turn it is.
        /// </summary>
        public void ShowTurn(IReadOnlyList<BattleFighter> order, int currentIndex)
        {
            for (int i = 0; i < order.Count; i++)
            {
                var card = cards[order[i]];
                card.SetTurn(i == currentIndex);
                card.SetOrder(i >= currentIndex ? i + 1 : 0);
            }
        }

        /// <summary>Clears every highlight and number (between turns / at the end).</summary>
        public void ClearTurn()
        {
            foreach (var card in cards.Values)
            {
                card.SetTurn(false);
                card.SetOrder(0);
            }
        }

        // ---------- Actions ----------

        public void RefreshActions(BattleFighter fighter, float escapeChance)
        {
            actionFighter = fighter;
            actionEscapeChance = escapeChance;
            ApplyActionTexts();
            attackButton.SetInteractable(true);

            bool ready = fighter.SkillReady;
            skillButton.SetInteractable(ready);
            skillButton.SetBadge(ready ? null : fighter.SkillCooldown.ToString());
            defendButton.SetInteractable(true);
            runButton.SetInteractable(true);
        }

        /// <summary>Button labels and hints for the fighter whose turn it is, in the current language.</summary>
        private void ApplyActionTexts()
        {
            var fighter = actionFighter;
            attackButton.Set(Loc.T("act.attack"), fighter.AttackName);

            bool ready = fighter.SkillReady;
            int left = fighter.SkillCooldown;
            skillButton.Set(Loc.T("act.skill"), ready ? fighter.SkillName : left == 1 ? Loc.T("act.skill.next") : Loc.T("act.skill.in", left));
            if (!ready) skillButton.SetBadge(left.ToString());

            defendButton.Set(Loc.T("act.defend"), Loc.T("act.defend.hint"));
            runButton.Set(Loc.T("act.run"), Loc.T("act.run.hint", Mathf.RoundToInt(actionEscapeChance * 100f)));
        }

        public void SetActionsVisible(bool visible, bool instant = false)
        {
            actionsOpen = visible;
            actionPanel.interactable = visible;
            actionPanel.blocksRaycasts = visible;
            if (panelRoutine != null) StopCoroutine(panelRoutine);
            if (instant || !isActiveAndEnabled)
            {
                actionPanel.alpha = visible ? 1f : hiddenAlpha;
                actionPanel.transform.localScale = Vector3.one;
                return;
            }
            panelRoutine = StartCoroutine(AnimatePanel(visible));
        }

        private IEnumerator AnimatePanel(bool visible)
        {
            float from = actionPanel.alpha, to = visible ? 1f : hiddenAlpha;
            var rt = actionPanel.transform;
            for (float t = 0f; t < 0.2f; t += UIEase.DeltaTime)
            {
                float p = t / 0.2f;
                actionPanel.alpha = Mathf.Lerp(from, to, p);
                float s = visible ? Mathf.LerpUnclamped(0.96f, 1f, UIEase.OutBack(p)) : Mathf.Lerp(1f, 0.98f, p);
                rt.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            actionPanel.alpha = to;
            rt.localScale = Vector3.one;
            panelRoutine = null;
        }

        private void Choose(BattleAction action)
        {
            if (!actionsOpen) return;
            SetActionsVisible(false);
            ActionChosen?.Invoke(action);
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !actionsOpen || Time.timeScale == 0f) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) attackButton.Press();
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) skillButton.Press();
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) defendButton.Press();
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) runButton.Press();
        }

        // ---------- Log ----------

        /// <summary>
        /// Replaces the log line (typed out quickly; Arabic shows at once). It is given as a function that builds the text,
        /// so the line can be rebuilt in the other language when the language is switched.
        /// </summary>
        public void SetLog(Func<string> line)
        {
            logParts.Clear();
            logParts.Add(line);
            revealedCharacters = 0;
            logText.text = BuildLog();
            Reveal(pop: true);
        }

        /// <summary>Adds to the current line, e.g. the damage once the hit lands.</summary>
        public void AppendLog(Func<string> part)
        {
            logText.ForceMeshUpdate();
            revealedCharacters = logText.textInfo.characterCount;
            logParts.Add(part);
            logText.text = BuildLog();
            Reveal(pop: false);
        }

        private string BuildLog()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var part in logParts) sb.Append(part());
            return sb.ToString();
        }

        private void Reveal(bool pop)
        {
            if (logRoutine != null) StopCoroutine(logRoutine);
            if (!isActiveAndEnabled)
            {
                logText.maxVisibleCharacters = 99999;
                return;
            }
            logRoutine = StartCoroutine(RevealRoutine(pop));
        }

        private IEnumerator RevealRoutine(bool pop)
        {
            logText.ForceMeshUpdate();
            int total = logText.textInfo.characterCount;
            // Right-to-left text is stored back to front, so typing it letter by letter would run backwards:
            // Arabic shows the whole line at once and only keeps the same pacing.
            bool rtl = Loc.IsRTL;
            float shown = revealedCharacters;
            typedChars = shown;
            typedTotal = total;
            logText.maxVisibleCharacters = rtl ? 99999 : revealedCharacters;
            float t = 0f;
            while (shown < total)
            {
                float dt = UIEase.DeltaTime;
                t += dt;
                shown += typeSpeed * dt;
                typedChars = shown;
                if (!rtl) logText.maxVisibleCharacters = Mathf.Min(total, Mathf.CeilToInt(shown));
                if (pop && logPanel != null)
                {
                    float s = 1f + 0.035f * Mathf.Max(0f, 1f - t / 0.18f);
                    logPanel.localScale = new Vector3(s, s, 1f);
                }
                yield return null;
            }
            logText.maxVisibleCharacters = 99999;
            if (logPanel != null) logPanel.localScale = Vector3.one;
            logRoutine = null;
        }

        /// <summary>Seconds the current log line still needs to finish typing.</summary>
        public float LogTimeLeft
        {
            get
            {
                if (logRoutine == null) return 0f;
                return Mathf.Max(0f, (typedTotal - typedChars) / Mathf.Max(1f, typeSpeed));
            }
        }

        /// <summary>Fades the log line out (the result sheet takes over that space) or back in.</summary>
        public void SetLogVisible(bool visible)
        {
            if (logGroup == null) return;
            if (!isActiveAndEnabled)
            {
                logGroup.alpha = visible ? 1f : 0f;
                return;
            }
            StartCoroutine(FadeLog(visible ? 1f : 0f));
        }

        private IEnumerator FadeLog(float to)
        {
            float from = logGroup.alpha;
            for (float t = 0f; t < 0.2f; t += UIEase.DeltaTime)
            {
                logGroup.alpha = Mathf.Lerp(from, to, t / 0.2f);
                yield return null;
            }
            logGroup.alpha = to;
        }

        // ---------- Status ----------

        public void ShowGuard(BattleFighter fighter, bool on) => CardFor(fighter).SetGuard(on);
    }
}
