using System;
using UnityEngine;
using WildTamers.Lang;

namespace WildTamers.Animals
{
    [Serializable]
    public class SkillData
    {
        [Tooltip("Damage multiplier compared to a normal attack (1 = same as normal attack).")]
        [Min(0f)] public float power = 2f;
        [Tooltip("Turns before the skill can be used again.")]
        [Min(0)] public int cooldownTurns = 3;
    }

    /// <summary>Static definition of an animal species. Stats are the level-1 base values.</summary>
    [CreateAssetMenu(menuName = "Wild Tamers/Animal Data", fileName = "NewAnimal")]
    public class AnimalData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used for saving. Never change it after release.")]
        public string id;
        [Tooltip("Label for the editor only. Everything the player reads comes from the 'Animals' string table (see the Localized… properties).")]
        public string displayName;
        public Color themeColor = Color.white;

        [Header("Visuals")]
        [Tooltip("Model prefab (low-poly, with Animator). Its root sits on the ground at the origin.")]
        public GameObject prefab;
        [Tooltip("Extra scale applied when shown on the map.")]
        [Min(0.1f)] public float mapScale = 1f;

        [Header("Base stats (level 1)")]
        [Min(1)] public int maxHP = 40;
        [Min(1)] public int attack = 10;
        [Min(1)] public int defense = 10;
        [Min(1)] public int speed = 10;
        [Tooltip("Fraction each stat grows per level above 1.")]
        [Range(0f, 0.5f)] public float growthPerLevel = 0.06f;

        [Header("Moves")]
        public SkillData skill = new SkillData();

        [Header("Spawning")]
        [Tooltip("Relative chance to appear in the wild.")]
        [Min(0f)] public float spawnWeight = 1f;

        // ---------- Texts (Unity Localization, table "Animals", keys "<id>.<field>") ----------

        /// <summary>The animal's name in the current language ("Camel", "جمل").</summary>
        public string LocalizedName => Loc.Animal(id, "name");

        /// <summary>The name with "the" where the language has one (Arabic "الجمل"); used inside sentences.</summary>
        public string LocalizedThe => Loc.Animal(id, "the");

        /// <summary>The wild animal as the boss of a fight ("Wild Camel", "الجمل البري").</summary>
        public string LocalizedWild => Loc.Animal(id, "wild");

        /// <summary>Short stat style shown in UI, e.g. "Fast &amp; enduring".</summary>
        public string LocalizedStyle => Loc.Animal(id, "style");

        /// <summary>What the animal is (2–3 short, simple sentences).</summary>
        public string LocalizedDescription => Loc.Animal(id, "description");

        /// <summary>How this animal belongs to Saudi / Arab history and culture (2–3 short sentences, real facts only).</summary>
        public string LocalizedHistory => Loc.Animal(id, "history");

        public string LocalizedAttackName => Loc.Animal(id, "attack");
        public string LocalizedSkillName => Loc.Animal(id, "skill");
        public string LocalizedSkillDescription => Loc.Animal(id, "skilldesc");

        public int GetMaxHP(int level) => Scale(maxHP, level);
        public int GetAttack(int level) => Scale(attack, level);
        public int GetDefense(int level) => Scale(defense, level);
        public int GetSpeed(int level) => Scale(speed, level);

        private int Scale(int baseValue, int level)
        {
            int lv = Mathf.Max(1, level);
            return Mathf.Max(1, Mathf.RoundToInt(baseValue * (1f + growthPerLevel * (lv - 1))));
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) id = name.ToLowerInvariant().Replace(" ", "_");
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
        }
    }
}
