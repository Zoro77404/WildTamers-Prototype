using WildTamers.Animals;

namespace WildTamers.Battle
{
    public enum BattleAction
    {
        Attack,
        Skill,
        Defend,
        Run
    }

    /// <summary>
    /// One animal in a battle: the animal plus battle-only state (skill cooldown, guard).
    /// HP lives on the <see cref="AnimalInstance"/> so damage to a team animal carries over to the map.
    /// </summary>
    public class BattleFighter
    {
        private bool usedSkillThisTurn;

        public BattleFighter(AnimalInstance animal, bool isPlayer)
        {
            Animal = animal;
            IsPlayer = isPlayer;
        }

        public AnimalInstance Animal { get; }
        /// <summary>True for the player's animals, false for the wild boss.</summary>
        public bool IsPlayer { get; }
        public AnimalData Data => Animal.Data;

        /// <summary>Name used in the battle log ("Wild Camel" for the boss).</summary>
        public string DisplayName => IsPlayer ? Animal.Name : $"Wild {Animal.Name}";

        public string AttackName => Data != null && !string.IsNullOrEmpty(Data.normalAttackName) ? Data.normalAttackName : "Tackle";
        public string SkillName => Data != null && !string.IsNullOrEmpty(Data.skill.skillName) ? Data.skill.skillName : "Skill";
        public float SkillPower => Data != null ? Data.skill.power : 2f;

        /// <summary>Own turns left before the skill can be used again (0 = ready).</summary>
        public int SkillCooldown { get; private set; }
        public bool SkillReady => SkillCooldown <= 0;

        /// <summary>Takes reduced damage until its next turn.</summary>
        public bool Guarding { get; set; }

        public bool IsFainted => Animal.IsFainted;

        public void UseSkill()
        {
            SkillCooldown = Data != null ? Data.skill.cooldownTurns : 3;
            usedSkillThisTurn = true;
        }

        /// <summary>Called when this animal's turn is over: an unused skill cools down by one turn.</summary>
        public void EndTurn()
        {
            if (!usedSkillThisTurn && SkillCooldown > 0) SkillCooldown--;
            usedSkillThisTurn = false;
        }
    }
}
