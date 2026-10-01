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

        public BattleRequest(AnimalInstance wild, AnimalInstance player, int wildSpawnId)
        {
            Wild = wild;
            Player = player;
            WildSpawnId = wildSpawnId;
        }
    }
}
