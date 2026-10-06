using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WildTamers.Core;

namespace WildTamers.Battle
{
    /// <summary>One resolved hit.</summary>
    public struct HitResult
    {
        public int Damage;
        public bool Critical;
        public bool Guarded;
    }

    /// <summary>
    /// The battle math, kept free of visuals so it is easy to tune (numbers live in <see cref="GameConfig"/>).
    /// Damage = scale × power × ATK² / (ATK + DEF) × level bonus × random spread × crit (normal attacks only) × guard.
    /// </summary>
    public static class BattleRules
    {
        /// <summary>Damage before randomness, crits and guarding (used for the AI and tests).</summary>
        public static float BaseDamage(BattleFighter attacker, BattleFighter defender, float power, GameConfig config)
        {
            float atk = attacker.Animal.Attack;
            float def = defender.Animal.Defense;
            float levelGap = attacker.Animal.Level - defender.Animal.Level;
            float levelBonus = 1f + Mathf.Clamp(config.levelDamageBonus * levelGap, -config.maxLevelDamageBonus, config.maxLevelDamageBonus);
            return config.damageScale * power * atk * atk / Mathf.Max(1f, atk + def) * levelBonus;
        }

        public static HitResult RollDamage(BattleFighter attacker, BattleFighter defender, float power, GameConfig config)
        {
            var hit = new HitResult
            {
                // Only normal attacks crit: skills are already the big hits.
                Critical = power <= 1f && Random.value < config.critChance,
                Guarded = defender.Guarding
            };
            float damage = BaseDamage(attacker, defender, power, config) * Random.Range(config.damageSpread.x, config.damageSpread.y);
            if (hit.Critical) damage *= config.critMultiplier;
            if (hit.Guarded) damage *= config.defendMultiplier;
            hit.Damage = Mathf.Max(1, Mathf.RoundToInt(damage));
            return hit;
        }

        /// <summary>Average speed of the animals in the party that can still run.</summary>
        public static float TeamSpeed(IEnumerable<BattleFighter> party)
        {
            var alive = party.Where(f => !f.IsFainted).ToList();
            return alive.Count == 0 ? 1f : (float)alive.Average(f => f.Animal.Speed);
        }

        /// <summary>
        /// Chance (0–1) that the whole team gets away: a faster team escapes more easily, and every failed try helps.
        /// </summary>
        public static float EscapeChance(float teamSpeed, float foeSpeed, int failedTries, GameConfig config)
        {
            float share = teamSpeed / Mathf.Max(1f, teamSpeed + foeSpeed);
            float chance = config.runBaseChance + config.runSpeedWeight * share + config.runBonusPerFail * failedTries;
            return Mathf.Clamp(chance, config.runChanceLimits.x, config.runChanceLimits.y);
        }

        public static float EscapeChance(IEnumerable<BattleFighter> party, BattleFighter foe, int failedTries, GameConfig config) =>
            EscapeChance(TeamSpeed(party), foe.Animal.Speed, failedTries, config);
    }

    /// <summary>Who acts in what order: everybody who can still fight, fastest first (ties broken at random).</summary>
    public static class TurnOrder
    {
        public static List<BattleFighter> Build(IEnumerable<BattleFighter> fighters)
        {
            return fighters.Where(f => !f.IsFainted)
                .Select(f => (fighter: f, tie: Random.value))
                .OrderByDescending(x => x.fighter.Animal.Speed)
                .ThenBy(x => x.tie)
                .Select(x => x.fighter)
                .ToList();
        }
    }

    /// <summary>
    /// Wild boss brain: mostly attacks, uses its skill when it is ready, sometimes defends when low on HP,
    /// and mostly hits your weakest animal.
    /// </summary>
    public static class BattleAI
    {
        public static BattleAction Choose(BattleFighter self, GameConfig config, bool guardedLastTurn)
        {
            bool low = self.Animal.HPFraction <= config.aiDefendBelowHP;
            if (low && !guardedLastTurn && Random.value < config.aiDefendChance) return BattleAction.Defend;
            if (self.SkillReady && Random.value < config.aiSkillChance) return BattleAction.Skill;
            return BattleAction.Attack;
        }

        /// <summary>The weakest (lowest HP) animal that can still fight most of the time, otherwise a random one.</summary>
        public static BattleFighter ChooseTarget(IReadOnlyList<BattleFighter> party, GameConfig config)
        {
            var alive = party.Where(f => !f.IsFainted).ToList();
            if (alive.Count == 0) return null;
            if (alive.Count == 1) return alive[0];
            if (Random.value < config.bossFocusWeakest)
                return alive.OrderBy(f => f.Animal.CurrentHP).ThenBy(f => f.Animal.Defense).First();
            return alive[Random.Range(0, alive.Count)];
        }
    }
}
