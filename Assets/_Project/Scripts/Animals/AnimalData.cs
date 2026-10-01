using System;
using UnityEngine;

namespace WildTamers.Animals
{
    [Serializable]
    public class SkillData
    {
        public string skillName = "Skill";
        [Tooltip("Damage multiplier compared to a normal attack (1 = same as normal attack).")]
        [Min(0f)] public float power = 2f;
        [Tooltip("Turns before the skill can be used again.")]
        [Min(0)] public int cooldownTurns = 3;
        [TextArea(1, 3)] public string description;
    }

    /// <summary>Static definition of an animal species. Stats are the level-1 base values.</summary>
    [CreateAssetMenu(menuName = "Wild Tamers/Animal Data", fileName = "NewAnimal")]
    public class AnimalData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used for saving. Never change it after release.")]
        public string id;
        public string displayName;
        [Tooltip("Short stat style shown in UI, e.g. 'Fast & fragile'.")]
        public string styleLabel;
        [TextArea(2, 4)] public string description;
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
        public string normalAttackName = "Tackle";
        public SkillData skill = new SkillData();

        [Header("Spawning")]
        [Tooltip("Relative chance to appear in the wild.")]
        [Min(0f)] public float spawnWeight = 1f;

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
