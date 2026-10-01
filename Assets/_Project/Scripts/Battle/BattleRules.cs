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

        /// <summary>Chance (0–1) that running away works: faster animals escape more easily, and every failed try helps.</summary>
        public static float EscapeChance(BattleFighter runner, BattleFighter foe, GameConfig config)
        {
            float mine = runner.Animal.Speed;
            float theirs = foe.Animal.Speed;
            float share = mine / Mathf.Max(1f, mine + theirs);
            float chance = config.runBaseChance + config.runSpeedWeight * share + config.runBonusPerFail * runner.FailedEscapes;
            return Mathf.Clamp(chance, config.runChanceLimits.x, config.runChanceLimits.y);
        }

        /// <summary>True if <paramref name="a"/> acts before <paramref name="b"/> this round (speed, ties broken at random).</summary>
        public static bool GoesFirst(BattleFighter a, BattleFighter b)
        {
            int sa = a.Animal.Speed, sb = b.Animal.Speed;
            return sa != sb ? sa > sb : Random.value < 0.5f;
        }

        /// <summary>Defend and Run happen before attacks, so guarding always covers this round's hit.</summary>
        public static bool IsPriority(BattleAction action) => action == BattleAction.Defend || action == BattleAction.Run;
    }

    /// <summary>
    /// Wild animal brain: mostly attacks, uses its skill when it is ready, and sometimes defends when low on HP.
    /// </summary>
    public static class BattleAI
    {
        public static BattleAction Choose(BattleFighter self, BattleFighter foe, GameConfig config, bool guardedLastTurn)
        {
            bool low = self.Animal.HPFraction <= config.aiDefendBelowHP;
            if (low && !guardedLastTurn && Random.value < config.aiDefendChance) return BattleAction.Defend;
            if (self.SkillReady && Random.value < config.aiSkillChance) return BattleAction.Skill;
            return BattleAction.Attack;
        }
    }
}
