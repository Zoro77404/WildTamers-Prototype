using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using WildTamers.Core;
using WildTamers.UI;

namespace WildTamers.Battle
{
    /// <summary>
    /// Portrait battle UI: both fighter cards, the one-line battle log, the four action buttons,
    /// floating damage numbers and the result sheet. Keys 1–4 also pick actions when testing on PC.
    /// </summary>
    public class BattleHUD : MonoBehaviour
    {
        [Header("Cards")]
        [SerializeField] private FighterCard playerCard;
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

        private bool actionsOpen;
        private Coroutine logRoutine;
        private Coroutine panelRoutine;
        private int revealedCharacters;

        public event Action<BattleAction> ActionChosen;

        public FighterCard PlayerCard => playerCard;
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

        public void Bind(BattleFighter player, BattleFighter wild, GameConfig config)
        {
            playerCard.Bind(player.Animal, config);
            wildCard.Bind(wild.Animal, config);
        }

        // ---------- Actions ----------

        public void RefreshActions(BattleFighter player, float escapeChance)
        {
            attackButton.Set("Attack", player.AttackName);
            attackButton.SetInteractable(true);

            bool ready = player.SkillReady;
            int left = player.SkillCooldown;
            skillButton.Set(player.SkillName, ready ? $"Ready!  ×{player.SkillPower:0.#} power" : left == 1 ? "Ready next turn" : $"Ready in {left} turns");
            skillButton.SetInteractable(ready);
            skillButton.SetBadge(ready ? null : left.ToString());

            defendButton.Set("Defend", "Half damage this turn");
            defendButton.SetInteractable(true);

            runButton.Set("Run", $"{Mathf.RoundToInt(escapeChance * 100f)}% to escape");
            runButton.SetInteractable(true);
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
            if (kb == null || !actionsOpen) return;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) attackButton.Press();
            else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) skillButton.Press();
            else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) defendButton.Press();
            else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) runButton.Press();
        }

        // ---------- Log ----------

        /// <summary>Replaces the log line (typed out quickly).</summary>
        public void SetLog(string text)
        {
            revealedCharacters = 0;
            logText.text = text;
            Reveal(pop: true);
        }

        /// <summary>Adds to the current line, e.g. the damage once the hit lands.</summary>
        public void AppendLog(string text)
        {
            logText.ForceMeshUpdate();
            revealedCharacters = logText.textInfo.characterCount;
            logText.text += text;
            Reveal(pop: false);
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
            float shown = revealedCharacters;
            logText.maxVisibleCharacters = revealedCharacters;
            float t = 0f;
            while (shown < total)
            {
                float dt = UIEase.DeltaTime;
                t += dt;
                shown += typeSpeed * dt;
                logText.maxVisibleCharacters = Mathf.Min(total, Mathf.CeilToInt(shown));
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
                int total = logText.textInfo.characterCount;
                return Mathf.Max(0f, (total - logText.maxVisibleCharacters) / Mathf.Max(1f, typeSpeed));
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

        public FighterCard CardFor(BattleFighter fighter) => fighter.IsPlayer ? playerCard : wildCard;

        public void ShowGuard(BattleFighter fighter, bool on) => CardFor(fighter).SetGuard(on);
    }
}
