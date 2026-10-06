using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WildTamers.Core;
using WildTamers.UI;

namespace WildTamers.Battle
{
    /// <summary>
    /// Runs the 3-vs-1 battle: intro, then rounds. Every round all fighters who can still fight act once, fastest first.
    /// On each of your animals' turns you pick Attack / Skill / Defend / Run (Run = the whole team); the wild boss
    /// attacks, uses its skill or guards, and mostly goes for your weakest animal.
    /// Ends in a win (the wild animal joins, XP for the whole party), a loss (all of yours fainted, team heals) or an escape.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [Header("Scene")]
        [Tooltip("Left, middle, right. One animal uses the middle spot, two use the outer spots.")]
        [SerializeField] private BattleActor[] playerActors = new BattleActor[3];
        [SerializeField] private BattleActor wildActor;
        [SerializeField] private BattleHUD hud;
        [SerializeField] private AnimalCardPanel animalCard;
        [SerializeField] private CameraShake cameraShake;
        [SerializeField] private BattleSparks sparks;

        [Header("Pacing (seconds)")]
        [SerializeField] private float afterAction = 0.6f;
        [SerializeField] private float betweenTurns = 0.25f;
        [SerializeField] private float readPause = 0.55f;

        [Header("Colors")]
        [SerializeField] private Color hitSparkColor = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
        [SerializeField] private Color damageColor = Color.white;
        [SerializeField] private Color critColor = new Color32(0xFF, 0xC9, 0x3C, 0xFF);
        [SerializeField] private Color guardColor = new Color32(0x9F, 0xD8, 0xFF, 0xFF);

        private const string DamageHex = "#EF476F";
        private const string CritHex = "#FF8A3D";
        private const string GuardHex = "#4DA3FF";

        private GameSession session;
        private GameConfig config;
        private readonly List<BattleFighter> party = new List<BattleFighter>();
        private readonly List<BattleFighter> everyone = new List<BattleFighter>();
        private readonly Dictionary<BattleFighter, BattleActor> actors = new Dictionary<BattleFighter, BattleActor>();
        private BattleFighter wild;
        private BattleAction? chosenAction;
        private bool wildGuardedLastTurn;
        private bool escaped;
        private int failedEscapes;
        private bool finished;

        /// <summary>True once the battle is over and the result sheet is up (used by play-tests).</summary>
        public bool IsFinished => finished;
        public IReadOnlyList<BattleFighter> Party => party;
        public BattleFighter Wild => wild;

        private void Start()
        {
            session = GameSession.Instance;
            config = session.Config;
            if (session.CurrentBattle == null) session.CreateDebugBattle();
            var battle = session.CurrentBattle;
            if (battle == null || battle.Party == null || battle.Party.Count == 0 || battle.Wild == null)
            {
                hud.SetLog("No animals to battle.");
                StartCoroutine(ReturnSoon());
                return;
            }

            int count = Mathf.Min(battle.Party.Count, playerActors.Length);
            var used = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                int slot = BattleHUD.SlotFor(i, count);
                used.Add(slot);
                var fighter = new BattleFighter(battle.Party[i], isPlayer: true);
                party.Add(fighter);
                actors[fighter] = playerActors[slot];
                playerActors[slot].Spawn(fighter.Data);
                playerActors[slot].transform.localScale = Vector3.zero;
            }
            for (int i = 0; i < playerActors.Length; i++)
                if (!used.Contains(i)) playerActors[i].gameObject.SetActive(false);

            wild = new BattleFighter(battle.Wild, isPlayer: false);
            actors[wild] = wildActor;
            wildActor.Spawn(wild.Data);
            everyone.AddRange(party);
            everyone.Add(wild);

            hud.Bind(party, wild, config);
            hud.ActionChosen += a => chosenAction = a;
            hud.Result.OnContinue(() => StartCoroutine(ContinueRoutine()));
            StartCoroutine(BattleLoop());
        }

        private IEnumerator BattleLoop()
        {
            // ---------- Intro ----------
            StartCoroutine(wildActor.Enter(0.1f, fromAbove: true));
            hud.SetLog($"A wild <b>{wild.Animal.Name}</b> appeared!");
            yield return Wait(1.0f);
            for (int i = 0; i < party.Count; i++) StartCoroutine(actors[party[i]].Enter(0.14f * i, fromAbove: false));
            hud.SetLog($"Go, {JoinNames(party)}!");
            yield return Wait(0.85f + 0.14f * party.Count);

            // ---------- Rounds ----------
            while (true)
            {
                var order = TurnOrder.Build(everyone);
                for (int turn = 0; turn < order.Count; turn++)
                {
                    var fighter = order[turn];
                    if (fighter.IsFainted) continue;

                    hud.ShowTurn(order, turn);
                    var view = actors[fighter];
                    view.SetTurnMarker(true);
                    // A guard lasts until its owner's next turn, which starts now.
                    SetGuard(fighter, false);

                    if (fighter.IsPlayer) yield return PlayerTurn(fighter);
                    else yield return WildTurn(fighter);

                    view.SetTurnMarker(false);
                    fighter.EndTurn();

                    if (escaped)
                    {
                        hud.ClearTurn();
                        yield return FinishEscape();
                        yield break;
                    }
                    if (wild.IsFainted)
                    {
                        hud.ClearTurn();
                        yield return FinishVictory();
                        yield break;
                    }
                    if (party.All(p => p.IsFainted))
                    {
                        hud.ClearTurn();
                        yield return FinishDefeat();
                        yield break;
                    }
                    yield return Wait(betweenTurns);
                }
            }
        }

        // ------------------------------------------------------------------
        // Turns
        // ------------------------------------------------------------------

        private IEnumerator PlayerTurn(BattleFighter fighter)
        {
            hud.RefreshActions(fighter, BattleRules.EscapeChance(party, wild, failedEscapes, config));
            hud.SetLog($"What will <b>{fighter.Animal.Name}</b> do?");
            chosenAction = null;
            hud.SetActionsVisible(true);
            while (chosenAction == null) yield return null;

            var action = chosenAction.Value;
            switch (action)
            {
                case BattleAction.Run:
                    yield return TryRun(fighter);
                    break;
                case BattleAction.Defend:
                    yield return Guard(fighter);
                    break;
                default:
                    yield return Strike(fighter, wild, action == BattleAction.Skill && fighter.SkillReady);
                    break;
            }
        }

        private IEnumerator WildTurn(BattleFighter fighter)
        {
            var action = BattleAI.Choose(fighter, config, wildGuardedLastTurn);
            wildGuardedLastTurn = action == BattleAction.Defend;
            if (action == BattleAction.Defend)
            {
                yield return Guard(fighter);
                yield break;
            }
            var target = BattleAI.ChooseTarget(party, config);
            if (target == null) yield break;
            yield return Strike(fighter, target, action == BattleAction.Skill && fighter.SkillReady);
        }

        private IEnumerator Guard(BattleFighter fighter)
        {
            SetGuard(fighter, true);
            hud.SetLog($"<b>{fighter.DisplayName}</b> is guarding!");
            yield return Wait(afterAction + 0.15f);
        }

        private IEnumerator Strike(BattleFighter actor, BattleFighter target, bool skill)
        {
            var actorView = actors[actor];
            var targetView = actors[target];
            string move = skill ? actor.SkillName : actor.AttackName;
            if (skill) actor.UseSkill();
            hud.SetLog(actor.IsPlayer
                ? $"<b>{actor.DisplayName}</b> used <b>{move}</b>!"
                : $"<b>{actor.DisplayName}</b> used <b>{move}</b> on <b>{target.Animal.Name}</b>!");

            var theme = actor.Data != null ? actor.Data.themeColor : Color.white;
            yield return actorView.Attack(targetView, skill, theme, sparks, () =>
            {
                var hit = BattleRules.RollDamage(actor, target, skill ? actor.SkillPower : 1f, config);
                target.Animal.CurrentHP -= hit.Damage;
                ShowHit(actorView, targetView, target, hit, skill, theme);

                string damage = $"<color={DamageHex}>{hit.Damage} damage</color>";
                if (hit.Critical) hud.AppendLog($" <color={CritHex}>Critical hit!</color> {damage}");
                else if (hit.Guarded) hud.AppendLog($" <color={GuardHex}>Guarded:</color> {damage}");
                else hud.AppendLog($" {damage}");
            });
            yield return Wait(afterAction + hud.LogTimeLeft);

            // One of yours went down: it is out for the rest of the fight.
            if (target.IsPlayer && target.IsFainted) yield return Knockout(target);
        }

        private IEnumerator Knockout(BattleFighter fighter)
        {
            SetGuard(fighter, false);
            actors[fighter].SetTurnMarker(false);
            hud.CardFor(fighter).SetFainted(true);
            hud.SetLog($"<b>{fighter.Animal.Name}</b> fainted!");
            yield return actors[fighter].Faint();
            yield return Wait(readPause * 0.7f);
        }

        private void ShowHit(BattleActor attackerView, BattleActor targetView, BattleFighter target, HitResult hit, bool skill, Color theme)
        {
            targetView.TakeHit(attackerView.transform.position, hit.Critical, hit.Guarded);
            var card = hud.CardFor(target);
            card.SetHP(target.Animal);
            card.Punch();

            if (sparks != null)
            {
                var color = skill ? theme : hitSparkColor;
                sparks.Burst(targetView.BodyPoint, color, skill ? 30 : 16, skill ? 1.35f : 1f);
            }

            float shake = skill ? 0.45f : 0.28f;
            if (hit.Critical) shake += 0.22f;
            if (hit.Guarded) shake *= 0.5f;
            if (cameraShake != null) cameraShake.Shake(shake);

            string caption = hit.Critical ? "CRITICAL!" : hit.Guarded ? "GUARD" : null;
            var color2 = hit.Critical ? critColor : hit.Guarded ? guardColor : damageColor;
            float size = hit.Critical ? 1.35f : skill ? 1.15f : 1f;
            hud.Numbers.Show(targetView.HeadPoint, hit.Damage.ToString(), color2, size, caption);
        }

        /// <summary>The whole team tries to get away together.</summary>
        private IEnumerator TryRun(BattleFighter runner)
        {
            float chance = BattleRules.EscapeChance(party, wild, failedEscapes, config);
            hud.SetLog("Your team tries to run…");
            yield return Wait(0.45f);
            if (Random.value < chance)
            {
                escaped = true;
                hud.SetLog("Your team got away safely!");
                int running = 0;
                foreach (var fighter in party.Where(p => !p.IsFainted))
                {
                    running++;
                    StartCoroutine(RunAway(actors[fighter], () => running--));
                }
                while (running > 0) yield return null;
            }
            else
            {
                failedEscapes++;
                hud.SetLog($"Couldn't get away from the <b>{wild.DisplayName}</b>!");
                yield return actors[runner].Stumble();
                yield return Wait(afterAction);
            }
        }

        private static IEnumerator RunAway(BattleActor actor, System.Action done)
        {
            yield return actor.RunAway();
            done();
        }

        private void SetGuard(BattleFighter fighter, bool on)
        {
            if (fighter.Guarding == on) return;
            fighter.Guarding = on;
            actors[fighter].SetGuard(on);
            hud.ShowGuard(fighter, on);
        }

        // ------------------------------------------------------------------
        // Endings
        // ------------------------------------------------------------------

        private IEnumerator FinishVictory()
        {
            foreach (var fighter in everyone) SetGuard(fighter, false);
            hud.SetLog($"<b>{wild.DisplayName}</b> fainted!");
            yield return wildActor.Faint();
            yield return Wait(readPause);

            var result = session.CompleteBattle(BattleOutcome.Won);
            StartCoroutine(wildActor.Revive(sparks, wild.Data != null ? wild.Data.themeColor : Color.white));
            hud.WildCard.SetHP(wild.Animal);
            hud.SetLog($"<b>{wild.Animal.Name}</b> joined your team!");
            foreach (var fighter in party.Where(p => !p.IsFainted)) StartCoroutine(actors[fighter].Celebrate());
            yield return Wait(0.95f);

            hud.SetLog($"Everyone gained <b>{result.ExperienceGained} XP</b>!");
            yield return Wait(0.5f);
            hud.SetLogVisible(false);
            finished = true;
            yield return hud.Result.PlayVictory(result, config, (animal, level) =>
            {
                var fighter = party.FirstOrDefault(p => p.Animal == animal);
                if (fighter != null) hud.CardFor(fighter).SetLevel(level);
            });
        }

        private IEnumerator FinishDefeat()
        {
            foreach (var fighter in everyone) SetGuard(fighter, false);
            hud.SetLog("Your whole team fainted…");
            yield return Wait(readPause);
            session.CompleteBattle(BattleOutcome.Lost);
            hud.SetLogVisible(false);
            finished = true;
            hud.Result.ShowDefeat(party.Select(p => p.Animal).ToList());
        }

        private IEnumerator FinishEscape()
        {
            session.CompleteBattle(BattleOutcome.Escaped);
            finished = true;
            yield return Wait(0.35f);
            session.ReturnToMap();
        }

        /// <summary>Continue pressed on the result sheet: a never-seen species gets its "New animal!" card first.</summary>
        private IEnumerator ContinueRoutine()
        {
            var result = session.CurrentBattle?.Result;
            if (result != null && result.Joined != null && result.JoinedIsNewSpecies && animalCard != null && result.Joined.Data != null)
            {
                bool closed = false;
                animalCard.OpenNew(result.Joined.Data, () => closed = true);
                while (!closed) yield return null;
            }
            session.ReturnToMap();
        }

        private IEnumerator ReturnSoon()
        {
            yield return Wait(1.5f);
            session.ReturnToMap();
        }

        private static string JoinNames(IReadOnlyList<BattleFighter> fighters)
        {
            var names = fighters.Select(f => $"<b>{f.Animal.Name}</b>").ToList();
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.Take(names.Count - 1)) + " and " + names[names.Count - 1];
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += UIEase.GameDeltaTime) yield return null;
        }
    }
}
