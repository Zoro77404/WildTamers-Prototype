using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WildTamers.Core;
using WildTamers.UI;

namespace WildTamers.Battle
{
    /// <summary>
    /// Runs the turn-based battle: intro, then rounds where the player picks Attack / Skill / Defend / Run,
    /// the wild animal's AI picks too, and both act in order (Defend and Run first, then by speed).
    /// Ends in a win (the wild animal joins the team, XP and level-ups), a loss (team heals) or an escape.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private BattleActor playerActor;
        [SerializeField] private BattleActor wildActor;
        [SerializeField] private BattleHUD hud;
        [SerializeField] private CameraShake cameraShake;
        [SerializeField] private BattleSparks sparks;

        [Header("Pacing (seconds)")]
        [SerializeField] private float afterAction = 0.6f;
        [SerializeField] private float betweenRounds = 0.3f;
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
        private BattleFighter player;
        private BattleFighter wild;
        private BattleAction? chosenAction;
        private bool wildGuardedLastTurn;
        private bool escaped;

        private void Start()
        {
            session = GameSession.Instance;
            config = session.Config;
            if (session.CurrentBattle == null) session.CreateDebugBattle();
            var battle = session.CurrentBattle;
            if (battle == null || battle.Player == null || battle.Wild == null)
            {
                hud.SetLog("No animals to battle.");
                StartCoroutine(ReturnSoon());
                return;
            }

            player = new BattleFighter(battle.Player, isPlayer: true);
            wild = new BattleFighter(battle.Wild, isPlayer: false);
            playerActor.Spawn(player.Data);
            wildActor.Spawn(wild.Data);
            hud.Bind(player, wild, config);
            hud.ActionChosen += a => chosenAction = a;
            hud.Result.OnContinue(session.ReturnToMap);
            StartCoroutine(BattleLoop());
        }

        private IEnumerator BattleLoop()
        {
            // ---------- Intro ----------
            StartCoroutine(wildActor.Enter(0.1f, fromAbove: true));
            playerActor.transform.localScale = Vector3.zero;
            hud.SetLog($"A wild <b>{wild.Animal.Name}</b> appeared!");
            yield return Wait(1.0f);
            StartCoroutine(playerActor.Enter(0f, fromAbove: false));
            hud.SetLog($"Go, <b>{player.Animal.Name}</b>!");
            yield return Wait(0.85f);

            // ---------- Rounds ----------
            while (true)
            {
                hud.RefreshActions(player, BattleRules.EscapeChance(player, wild, config));
                hud.SetLog($"What will <b>{player.Animal.Name}</b> do?");
                chosenAction = null;
                hud.SetActionsVisible(true);
                while (chosenAction == null) yield return null;

                var playerChoice = chosenAction.Value;
                var wildChoice = BattleAI.Choose(wild, player, config, wildGuardedLastTurn);

                // A guard lasts until its owner's next turn, which starts now.
                SetGuard(player, false);
                SetGuard(wild, false);

                foreach (var (actor, action) in Order(playerChoice, wildChoice))
                {
                    var target = actor == player ? wild : player;
                    yield return Perform(actor, target, action);

                    if (escaped)
                    {
                        yield return FinishEscape();
                        yield break;
                    }
                    if (wild.IsFainted)
                    {
                        yield return FinishVictory();
                        yield break;
                    }
                    if (player.IsFainted)
                    {
                        yield return FinishDefeat();
                        yield break;
                    }
                }

                wildGuardedLastTurn = wildChoice == BattleAction.Defend;
                player.EndRound();
                wild.EndRound();
                yield return Wait(betweenRounds);
            }
        }

        private List<(BattleFighter, BattleAction)> Order(BattleAction playerChoice, BattleAction wildChoice)
        {
            var list = new List<(BattleFighter, BattleAction)>(2);
            bool playerPriority = BattleRules.IsPriority(playerChoice);
            bool wildPriority = BattleRules.IsPriority(wildChoice);
            bool playerFirst = playerPriority != wildPriority ? playerPriority : BattleRules.GoesFirst(player, wild);
            if (playerFirst)
            {
                list.Add((player, playerChoice));
                list.Add((wild, wildChoice));
            }
            else
            {
                list.Add((wild, wildChoice));
                list.Add((player, playerChoice));
            }
            return list;
        }

        // ------------------------------------------------------------------
        // Actions
        // ------------------------------------------------------------------

        private IEnumerator Perform(BattleFighter actor, BattleFighter target, BattleAction action)
        {
            switch (action)
            {
                case BattleAction.Run:
                    yield return TryRun();
                    break;
                case BattleAction.Defend:
                    SetGuard(actor, true);
                    hud.SetLog($"<b>{actor.DisplayName}</b> is guarding!");
                    yield return Wait(afterAction + 0.15f);
                    break;
                default:
                    yield return Strike(actor, target, action == BattleAction.Skill && actor.SkillReady);
                    break;
            }
        }

        private IEnumerator Strike(BattleFighter actor, BattleFighter target, bool skill)
        {
            var actorView = ActorFor(actor);
            var targetView = ActorFor(target);
            string move = skill ? actor.SkillName : actor.AttackName;
            if (skill) actor.UseSkill();
            hud.SetLog($"<b>{actor.DisplayName}</b> used <b>{move}</b>!");

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
        }

        private void ShowHit(BattleActor attackerView, BattleActor targetView, BattleFighter target, HitResult hit, bool skill, Color theme)
        {
            targetView.TakeHit(attackerView.transform.position, hit.Critical, hit.Guarded);
            hud.CardFor(target).SetHP(target.Animal);
            hud.CardFor(target).Punch();

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

        private IEnumerator TryRun()
        {
            float chance = BattleRules.EscapeChance(player, wild, config);
            hud.SetLog($"<b>{player.Animal.Name}</b> tries to run…");
            yield return Wait(0.45f);
            if (Random.value < chance)
            {
                escaped = true;
                hud.SetLog("Got away safely!");
                yield return playerActor.RunAway();
            }
            else
            {
                player.FailedEscapes++;
                hud.SetLog($"Couldn't get away from the <b>{wild.DisplayName}</b>!");
                yield return playerActor.Stumble();
                yield return Wait(afterAction);
            }
        }

        private void SetGuard(BattleFighter fighter, bool on)
        {
            if (fighter.Guarding == on) return;
            fighter.Guarding = on;
            ActorFor(fighter).SetGuard(on);
            hud.ShowGuard(fighter, on);
        }

        private BattleActor ActorFor(BattleFighter fighter) => fighter.IsPlayer ? playerActor : wildActor;

        // ------------------------------------------------------------------
        // Endings
        // ------------------------------------------------------------------

        private IEnumerator FinishVictory()
        {
            SetGuard(player, false);
            SetGuard(wild, false);
            hud.SetLog($"<b>{wild.DisplayName}</b> fainted!");
            yield return wildActor.Faint();
            yield return Wait(readPause);

            var result = session.CompleteBattle(BattleOutcome.Won);
            StartCoroutine(wildActor.Revive(sparks, wild.Data != null ? wild.Data.themeColor : Color.white));
            hud.WildCard.SetHP(wild.Animal);
            hud.SetLog($"<b>{wild.Animal.Name}</b> joined your team!");
            StartCoroutine(playerActor.Celebrate());
            yield return Wait(0.95f);

            hud.SetLog($"<b>{player.Animal.Name}</b> gained <b>{result.ExperienceGained} XP</b>!");
            yield return Wait(0.5f);
            hud.SetLogVisible(false);
            yield return hud.Result.PlayVictory(result, player.Animal, config, hud.PlayerCard);
            hud.PlayerCard.SetHP(player.Animal);
        }

        private IEnumerator FinishDefeat()
        {
            SetGuard(player, false);
            SetGuard(wild, false);
            hud.SetLog($"<b>{player.Animal.Name}</b> fainted…");
            yield return playerActor.Faint();
            yield return Wait(readPause);
            session.CompleteBattle(BattleOutcome.Lost);
            hud.SetLogVisible(false);
            hud.Result.ShowDefeat(player.Animal);
        }

        private IEnumerator FinishEscape()
        {
            session.CompleteBattle(BattleOutcome.Escaped);
            yield return Wait(0.35f);
            session.ReturnToMap();
        }

        private IEnumerator ReturnSoon()
        {
            yield return Wait(1.5f);
            session.ReturnToMap();
        }

        private static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += UIEase.GameDeltaTime) yield return null;
        }
    }
}
