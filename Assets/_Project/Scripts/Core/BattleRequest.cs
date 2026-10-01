using WildTamers.Animals;

namespace WildTamers.Core
{
    /// <summary>Everything the battle scene needs, handed over through GameSession.</summary>
    public class BattleRequest
    {
        public AnimalInstance Wild { get; }
        public AnimalInstance Player { get; }
        /// <summary>Map spawn the wild animal came from (-1 if none, e.g. a debug battle).</summary>
        public int WildSpawnId { get; }
        /// <summary>Set once the outcome has been applied, so it can never be applied twice.</summary>
        public BattleResult Result { get; internal set; }

        public BattleRequest(AnimalInstance wild, AnimalInstance player, int wildSpawnId)
        {
            Wild = wild;
            Player = player;
            WildSpawnId = wildSpawnId;
        }
    }

    public enum BattleOutcome
    {
        Won,
        Lost,
        Escaped
    }

    /// <summary>What a finished battle changed: who joined, XP gained and any level-ups.</summary>
    public class BattleResult
    {
        public BattleOutcome Outcome { get; }
        /// <summary>The wild animal that joined the team (only when won).</summary>
        public AnimalInstance Joined { get; internal set; }
        public int ExperienceGained { get; internal set; }
        public LevelUpResult Growth { get; internal set; }

        public BattleResult(BattleOutcome outcome) => Outcome = outcome;
    }
}
