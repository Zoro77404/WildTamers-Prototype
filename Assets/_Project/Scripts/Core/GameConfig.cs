using UnityEngine;

namespace WildTamers.Core
{
    /// <summary>Gameplay tuning shared by map, spawner, battle and UI. Lives in Resources so GameSession can load it.</summary>
    [CreateAssetMenu(menuName = "Wild Tamers/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        [Header("Encounters")]
        [Tooltip("How close (meters) the player must be to start a fight.")]
        [Min(1f)] public float fightRange = 13f;

        [Header("Wild spawns")]
        [Min(1)] public int maxWildAnimals = 8;
        [Min(1f)] public float minSpawnDistance = 7f;
        [Min(2f)] public float maxSpawnDistance = 40f;
        [Tooltip("Wild animals farther than this from the player disappear.")]
        [Min(5f)] public float despawnDistance = 60f;
        [Tooltip("Minimum gap between two wild animals.")]
        [Min(0f)] public float minSpawnSeparation = 7f;
        public Vector2 spawnIntervalRange = new Vector2(1.2f, 3f);
        [Tooltip("Seconds a wild animal stays before wandering off.")]
        public Vector2 wildLifetimeRange = new Vector2(150f, 300f);
        [Tooltip("Wild levels are the team level ± this.")]
        [Min(0)] public int wildLevelSpread = 2;
        [Tooltip("Relative chance of each level offset from -spread to +spread (middle = same level as the team). " +
                 "Skewed low so most fights are winnable, with the odd tougher one.")]
        public float[] wildLevelWeights = { 25f, 30f, 25f, 15f, 5f };
        [Min(1)] public int maxLevel = 50;

        [Tooltip("Wild animals are drawn larger than life on the map so they read well from the camera.")]
        [Min(0.1f)] public float wildAnimalScale = 1.7f;

        [Header("Team")]
        [Min(1)] public int starterLevel = 5;
        [Tooltip("Fraction of max HP every team animal recovers per second while on the map.")]
        [Range(0f, 0.2f)] public float teamHealPerSecond = 0.015f;

        [Header("Battle — damage")]
        [Tooltip("Damage = scale × power × ATK² / (ATK + DEF) × level bonus × spread × crit × guard.")]
        [Min(0.01f)] public float damageScale = 0.85f;
        [Tooltip("Random damage multiplier (min, max).")]
        public Vector2 damageSpread = new Vector2(0.85f, 1.15f);
        [Tooltip("Chance a normal attack lands a critical hit (skills never crit, so no surprise one-shots).")]
        [Range(0f, 1f)] public float critChance = 0.12f;
        [Min(1f)] public float critMultiplier = 1.5f;
        [Tooltip("Damage taken while defending.")]
        [Range(0f, 1f)] public float defendMultiplier = 0.5f;
        [Tooltip("Extra damage per level the attacker is above the defender (less when below).")]
        [Range(0f, 0.2f)] public float levelDamageBonus = 0.01f;
        [Range(0f, 0.5f)] public float maxLevelDamageBonus = 0.1f;

        [Header("Battle — run")]
        [Tooltip("Escape chance = base + weight × mySpeed / (mySpeed + foeSpeed), plus a bonus for every failed try.")]
        [Range(0f, 1f)] public float runBaseChance = 0.15f;
        [Range(0f, 1f)] public float runSpeedWeight = 0.8f;
        [Range(0f, 1f)] public float runBonusPerFail = 0.15f;
        public Vector2 runChanceLimits = new Vector2(0.2f, 0.95f);

        [Header("Battle — enemy AI")]
        [Tooltip("Chance the wild animal uses its skill when it is ready.")]
        [Range(0f, 1f)] public float aiSkillChance = 0.8f;
        [Tooltip("Below this HP fraction the wild animal may defend.")]
        [Range(0f, 1f)] public float aiDefendBelowHP = 0.35f;
        [Range(0f, 1f)] public float aiDefendChance = 0.4f;

        [Header("Experience")]
        [Tooltip("XP for a win = (base + perLevel × wild level) × level-gap factor.")]
        [Min(0)] public int xpRewardBase = 10;
        [Min(0)] public int xpRewardPerLevel = 8;
        [Tooltip("Reward change per level the wild animal is above (or below) yours.")]
        [Range(0f, 0.5f)] public float xpLevelGapFactor = 0.15f;
        public Vector2 xpGapLimits = new Vector2(0.5f, 1.6f);
        [Tooltip("XP needed to go from level L to L+1 = base + perLevel × L.")]
        [Min(1)] public int xpToNextBase = 20;
        [Min(0)] public int xpToNextPerLevel = 10;

        /// <summary>Random level offset for a wild spawn (weighted by <see cref="wildLevelWeights"/>, uniform if they don't fit the spread).</summary>
        public int RollWildLevelOffset()
        {
            int count = wildLevelSpread * 2 + 1;
            if (wildLevelWeights == null || wildLevelWeights.Length != count) return Random.Range(-wildLevelSpread, wildLevelSpread + 1);
            float total = 0f;
            foreach (var w in wildLevelWeights) total += Mathf.Max(0f, w);
            if (total <= 0f) return 0;
            float roll = Random.value * total;
            for (int i = 0; i < count; i++)
            {
                roll -= Mathf.Max(0f, wildLevelWeights[i]);
                if (roll <= 0f) return i - wildLevelSpread;
            }
            return wildLevelSpread;
        }

        public int ExperienceToNext(int level) => xpToNextBase + xpToNextPerLevel * Mathf.Max(1, level);

        /// <summary>XP for beating a wild animal; more for tougher foes, less for weaker ones.</summary>
        public int ExperienceReward(int myLevel, int wildLevel)
        {
            float gap = Mathf.Clamp(1f + xpLevelGapFactor * (wildLevel - myLevel), xpGapLimits.x, xpGapLimits.y);
            return Mathf.Max(1, Mathf.RoundToInt((xpRewardBase + xpRewardPerLevel * wildLevel) * gap));
        }
    }
}
