using System;
using UnityEngine;
using WildTamers.Core;

namespace WildTamers.Animals
{
    /// <summary>
    /// One specific animal (a team member or a wild one): species + level + XP + current HP.
    /// Plain data; <see cref="ToSave"/> / <see cref="FromSave"/> convert it for the save file.
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
        [NonSerialized] private float regenCarry;

        public AnimalInstance(AnimalData species, int level)
        {
            if (species == null) throw new ArgumentNullException(nameof(species));
            uid = Guid.NewGuid().ToString("N");
            animalId = species.id;
            data = species;
            this.level = Mathf.Max(1, level);
            currentHP = MaxHP;
        }

        private AnimalInstance() { }

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
        public bool IsFullHP => currentHP >= MaxHP;

        public void HealFull()
        {
            currentHP = MaxHP;
            regenCarry = 0f;
        }

        /// <summary>Restores a fraction of max HP; fractions of a point carry over to the next call. Returns true if HP changed.</summary>
        public bool Regenerate(float fractionOfMax)
        {
            if (IsFullHP || fractionOfMax <= 0f)
            {
                regenCarry = 0f;
                return false;
            }
            regenCarry += fractionOfMax * MaxHP;
            int whole = Mathf.FloorToInt(regenCarry);
            if (whole <= 0) return false;
            regenCarry -= whole;
            CurrentHP = currentHP + whole;
            return true;
        }

        // ---------- Experience ----------

        public int ExperienceToNext(GameConfig config) => config.ExperienceToNext(level);

        /// <summary>Adds XP and applies every level-up it buys. Current HP rises by the max-HP gained.</summary>
        public LevelUpResult AddExperience(int amount, GameConfig config)
        {
            var result = new LevelUpResult
            {
                OldLevel = level,
                OldExperience = experience,
                OldMaxHP = MaxHP,
                OldAttack = Attack,
                OldDefense = Defense,
                OldSpeed = Speed
            };

            experience += Mathf.Max(0, amount);
            while (level < config.maxLevel && experience >= config.ExperienceToNext(level))
            {
                experience -= config.ExperienceToNext(level);
                level++;
            }
            if (level >= config.maxLevel) experience = 0;

            result.NewLevel = level;
            result.NewExperience = experience;
            result.NewMaxHP = MaxHP;
            result.NewAttack = Attack;
            result.NewDefense = Defense;
            result.NewSpeed = Speed;
            CurrentHP = currentHP + (result.NewMaxHP - result.OldMaxHP);
            return result;
        }

        // ---------- Save ----------

        public AnimalSaveData ToSave() => new AnimalSaveData
        {
            uid = uid,
            animalId = animalId,
            level = level,
            experience = experience,
            currentHP = currentHP
        };

        /// <summary>Rebuilds an animal from the save file; null if the species no longer exists.</summary>
        public static AnimalInstance FromSave(AnimalSaveData save, AnimalDatabase database, GameConfig config)
        {
            if (save == null || database == null) return null;
            var species = database.Get(save.animalId);
            if (species == null) return null;

            var animal = new AnimalInstance
            {
                uid = string.IsNullOrEmpty(save.uid) ? Guid.NewGuid().ToString("N") : save.uid,
                animalId = species.id,
                data = species,
                level = Mathf.Clamp(save.level, 1, config.maxLevel)
            };
            animal.experience = animal.level >= config.maxLevel ? 0 : Mathf.Clamp(save.experience, 0, config.ExperienceToNext(animal.level) - 1);
            // A saved 0 HP can only come from an interrupted battle; never start fainted.
            animal.currentHP = save.currentHP <= 0 ? animal.MaxHP : Mathf.Min(save.currentHP, animal.MaxHP);
            return animal;
        }
    }

    /// <summary>What a batch of XP did to an animal (for the "Level up!" display).</summary>
    public struct LevelUpResult
    {
        public int OldLevel, NewLevel;
        public int OldExperience, NewExperience;
        public int OldMaxHP, NewMaxHP;
        public int OldAttack, NewAttack;
        public int OldDefense, NewDefense;
        public int OldSpeed, NewSpeed;

        public bool LeveledUp => NewLevel > OldLevel;
        public int LevelsGained => NewLevel - OldLevel;
    }
}
