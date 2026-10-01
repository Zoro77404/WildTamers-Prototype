using System;
using UnityEngine;

namespace WildTamers.Animals
{
    /// <summary>
    /// One specific animal (a team member or a wild one): species + level + XP + current HP.
    /// Plain serializable data so it can be written to a save file later.
    /// </summary>
    [Serializable]
    public class AnimalInstance
    {
        [SerializeField] private string uid;
        [SerializeField] private string animalId;
        [SerializeField] private int level = 1;
        [SerializeField] private int experience;
        [SerializeField] private int currentHP;

        [NonSerialized] private AnimalData data;

        public AnimalInstance(AnimalData species, int level)
        {
            if (species == null) throw new ArgumentNullException(nameof(species));
            uid = Guid.NewGuid().ToString("N");
            animalId = species.id;
            data = species;
            this.level = Mathf.Max(1, level);
            currentHP = MaxHP;
        }

        public string Uid => uid;
        public string AnimalId => animalId;

        public AnimalData Data
        {
            get
            {
                if (data == null && AnimalDatabase.Instance != null) data = AnimalDatabase.Instance.Get(animalId);
                return data;
            }
        }

        public string Name => Data != null ? Data.displayName : animalId;
        public int Level => level;
        public int Experience => experience;

        public int MaxHP => Data != null ? Data.GetMaxHP(level) : 1;
        public int Attack => Data != null ? Data.GetAttack(level) : 1;
        public int Defense => Data != null ? Data.GetDefense(level) : 1;
        public int Speed => Data != null ? Data.GetSpeed(level) : 1;

        public int CurrentHP
        {
            get => currentHP;
            set => currentHP = Mathf.Clamp(value, 0, MaxHP);
        }

        public float HPFraction => MaxHP > 0 ? (float)currentHP / MaxHP : 0f;
        public bool IsFainted => currentHP <= 0;

        public void HealFull() => currentHP = MaxHP;
    }
}
