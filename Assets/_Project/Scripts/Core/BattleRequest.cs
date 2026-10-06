using System.Collections.Generic;
using WildTamers.Animals;

namespace WildTamers.Core
{
    /// <summary>Everything the battle scene needs, handed over through GameSession.</summary>
    public class BattleRequest
    {
        /// <summary>The wild animal, already turned into a boss.</summary>
        public AnimalInstance Wild { get; }
        /// <summary>The animals fighting for the player (1–3), in the order they were picked.</summary>
        public IReadOnlyList<AnimalInstance> Party { get; }
        /// <summary>Map spawn the wild animal came from (-1 if none, e.g. a debug battle).</summary>
        public int WildSpawnId { get; }
        /// <summary>Set once the outcome has been applied, so it can never be applied twice.</summary>
        public BattleResult Result { get; internal set; }

        public BattleRequest(AnimalInstance wild, IReadOnlyList<AnimalInstance> party, int wildSpawnId)
        {
            Wild = wild;
            Party = party;
            WildSpawnId = wildSpawnId;
        }
    }

    public enum BattleOutcome
    {
        Won,
        Lost,
        Escaped
    }

    /// <summary>What happened to one party animal after a win: its XP, level-ups and whether it had fainted.</summary>
    public class PartyGrowth
    {
        public AnimalInstance Animal;
        public LevelUpResult Growth;
        public bool WasFainted;
    }

    /// <summary>What a finished battle changed: who joined, XP gained and any level-ups.</summary>
    public class BattleResult
    {
        public BattleOutcome Outcome { get; }
        /// <summary>The wild animal that joined the team (only when won).</summary>
        public AnimalInstance Joined { get; internal set; }
        /// <summary>True when the joined animal is a species the player has never had (its "New animal!" card is due).</summary>
        public bool JoinedIsNewSpecies { get; internal set; }
        /// <summary>XP every party animal received (the whole party shares the win).</summary>
        public int ExperienceGained { get; internal set; }
        public List<PartyGrowth> Party { get; } = new List<PartyGrowth>();

        public BattleResult(BattleOutcome outcome) => Outcome = outcome;
    }
}
