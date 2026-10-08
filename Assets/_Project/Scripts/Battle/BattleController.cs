using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WildTamers.Core;
using WildTamers.UI;
using WildTamers.Lang;
using WildTamers.Audio;
using WildTamers.Animals;
using WildTamers.Vfx;

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

        private GameSession session;
        private GameConfig config;
        private Camera worldCamera;
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
            worldCamera = cameraShake != null ? cameraShake.GetComponent<Camera>() : Camera.main;
            AudioManager.Instance.PlayMusic(Music.Battle);
            if (session.CurrentBattle == null) session.CreateDebugBattle();
            var battle = session.CurrentBattle;
            if (battle == null || battle.Party == null || battle.Party.Count == 0 || battle.Wild == null)
            {
                hud.SetLog(() => Loc.T("log.noanimals"));
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
            hud.SetLog(() => Loc.T("log.appeared", wild.Animal.Name, wild.DisplayName));
            yield return Wait(1.0f);
            for (int i = 0; i < party.Count; i++) StartCoroutine(actors[party[i]].Enter(0.14f * i, fromAbove: false));
            hud.SetLog(() => Loc.T("log.go", JoinNames(party)));
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
            hud.SetLog(() => Loc.T("log.what", fighter.Animal.The));
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
            hud.SetLog(() => Loc.T("log.guarding", fighter.DisplayName));
            yield return Wait(afterAction + 0.15f);
        }

        private IEnumerator Strike(BattleFighter actor, BattleFighter target, bool skill)
        {
            var actorView = actors[actor];
            var targetView = actors[target];
            if (skill) actor.UseSkill();
            hud.SetLog(() =>
            {
                string move = skill ? actor.SkillName : actor.AttackName;
                return actor.IsPlayer
                    ? Loc.T("log.used", actor.DisplayName, move)
                    : Loc.T("log.usedon", actor.DisplayName, move, target.Animal.The);
            });

            var theme = actor.Data != null ? actor.Data.themeColor : Color.white;
            // Specials: the wind-up lasts as long as the effect needs so that its hit moment (bolt, spikes, flare) lands on the impact.
            var special = skill && actor.Data != null ? actor.Data.special : null;
            float charge = BattleActor.SkillCharge, spawnAt = 0f;
            if (skill)
            {
                charge = SpecialAttackPlayer.PlanCharge(special, BattleActor.SkillCharge, BattleActor.SkillLunge, out spawnAt);
                AudioManager.Play(Sfx.Charge);
                if (special != null && special.vfx != null)
                    StartCoroutine(SpawnSpecial(special, actorView, targetView, spawnAt, charge + BattleActor.SkillLunge));
            }

            float started = Time.time;
            yield return actorView.Attack(targetView, skill, theme, sparks, () =>
            {
                var hit = BattleRules.RollDamage(actor, target, skill ? actor.SkillPower : 1f, config);
                target.Animal.CurrentHP -= hit.Damage;
                ShowHit(actorView, targetView, target, hit, skill, theme, special);

                int damage = hit.Damage;
                bool crit = hit.Critical, guarded = hit.Guarded;
                hud.AppendLog(() => " " + (crit ? Loc.T("log.crit") + " " : guarded ? Loc.T("log.guarded") + " " : "") + Loc.T("log.damage", damage));
            }, charge, () => AudioManager.Play(Sfx.Swing));
            float elapsed = Time.time - started;

            // Let a special's effect play out (most of it) before the next turn starts.
            float effectLeft = special != null && special.vfx != null ? spawnAt + SpecialAttackPlayer.Duration(special) - elapsed - 0.35f : 0f;
            yield return Wait(Mathf.Max(afterAction + hud.LogTimeLeft, Mathf.Min(effectLeft, 1.6f)));

            // One of yours went down: it is out for the rest of the fight.
            if (target.IsPlayer && target.IsFainted) yield return Knockout(target);
        }

        private IEnumerator SpawnSpecial(SpecialAttackFx fx, BattleActor attacker, BattleActor target, float delay, float impactAt)
        {
            yield return Wait(delay);
            var onAnimal = fx.spawnAt == VfxAnchor.Attacker ? attacker : target;
            var effect = SpecialAttackPlayer.Spawn(fx, attacker.transform.position, target.BodyPoint, target.transform.position, worldCamera,
                SpecialAttackPlayer.SizeFor(onAnimal.WorldHeight));
            // A tall effect (storm cloud, tornado) reaching up under the boss's card: fade the card while it plays.
            if (effect != null && SpecialAttackPlayer.ViewportTop(effect, worldCamera) > hud.WildCardViewportRect.yMin)
                hud.DimWildCard(SpecialAttackPlayer.CardDimTime(fx, impactAt - delay));
        }

        private IEnumerator Knockout(BattleFighter fighter)
        {
            SetGuard(fighter, false);
            actors[fighter].SetTurnMarker(false);
            hud.CardFor(fighter).SetFainted(true);
            hud.SetLog(() => Loc.T("log.fainted", fighter.DisplayName));
            yield return actors[fighter].Faint();
            yield return Wait(readPause * 0.7f);
        }

        private void ShowHit(BattleActor attackerView, BattleActor targetView, BattleFighter target, HitResult hit, bool skill, Color theme,
            SpecialAttackFx special)
        {
            targetView.TakeHit(attackerView.transform.position, hit.Critical, hit.Guarded);
            // Normal hits: one of the attack sounds at random; critical hits: the heavy hit; specials: the animal's special sound.
            if (skill) SpecialAttackPlayer.PlayImpactSound(special);
            else AudioManager.Play(hit.Critical ? Sfx.Heavy : Sfx.NormalAttack);
            if (hit.Guarded) AudioManager.Play(Sfx.Guard);
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

            string caption = hit.Critical ? Loc.T("battle.crit") : hit.Guarded ? Loc.T("battle.guardcaption") : null;
            var color2 = hit.Critical ? critColor : hit.Guarded ? guardColor : damageColor;
            float size = hit.Critical ? 1.35f : skill ? 1.15f : 1f;
            hud.Numbers.Show(targetView.HeadPoint, hit.Damage.ToString(), color2, size, caption);
        }

        /// <summary>The whole team tries to get away together.</summary>
        private IEnumerator TryRun(BattleFighter runner)
        {
            float chance = BattleRules.EscapeChance(party, wild, failedEscapes, config);
            hud.SetLog(() => Loc.T("log.tryrun"));
            yield return Wait(0.45f);
            if (Random.value < chance)
            {
                escaped = true;
                hud.SetLog(() => Loc.T("log.gotaway"));
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
                hud.SetLog(() => Loc.T("log.cantrun", wild.DisplayName));
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
            hud.SetLog(() => Loc.T("log.fainted", wild.DisplayName));
            yield return wildActor.Faint();
            yield return Wait(readPause);

            var result = session.CompleteBattle(BattleOutcome.Won);
            AudioManager.Play(Sfx.Win);
            StartCoroutine(wildActor.Revive(sparks, wild.Data != null ? wild.Data.themeColor : Color.white));
            hud.WildCard.SetHP(wild.Animal);
            hud.SetLog(() => Loc.T("log.joined", wild.Animal.The));
            foreach (var fighter in party.Where(p => !p.IsFainted)) StartCoroutine(actors[fighter].Celebrate());
            yield return Wait(0.95f);

            hud.SetLog(() => Loc.T("log.xp", result.ExperienceGained));
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
            hud.SetLog(() => Loc.T("log.wipe"));
            yield return Wait(readPause);
            session.CompleteBattle(BattleOutcome.Lost);
            AudioManager.Play(Sfx.Lose);
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

        private static string JoinNames(IReadOnlyList<BattleFighter> fighters) =>
            Loc.JoinNames(fighters.Select(f => $"<b>{f.Animal.The}</b>").ToList());

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += UIEase.GameDeltaTime) yield return null;
        }
    }
}
