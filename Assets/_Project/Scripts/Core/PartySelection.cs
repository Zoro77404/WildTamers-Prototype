using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WildTamers.Animals;

namespace WildTamers.Core
{
    /// <summary>Which animals can fight and which ones the team select screen starts with. Pure logic (no scene needed).</summary>
    public static class PartySelection
    {
        /// <summary>Fit to fight: not fainted and not too hurt.</summary>
        public static bool IsFightReady(AnimalInstance animal, GameConfig config) =>
            animal != null && !animal.IsFainted && animal.HPFraction >= config.fightReadyHPFraction;

        /// <summary>
        /// Animals that can be picked for the next fight: the fit ones, or if nobody is fit enough,
        /// every animal that has not fainted (you always fight with what you have).
        /// </summary>
        public static List<AnimalInstance> Candidates(IEnumerable<AnimalInstance> team, GameConfig config)
        {
            var all = team.ToList();
            var ready = all.Where(a => IsFightReady(a, config)).ToList();
            return ready.Count > 0 ? ready : all.Where(a => a != null && !a.IsFainted).ToList();
        }

        /// <summary>How many animals the next fight takes: the party size, or fewer if fewer animals can fight.</summary>
        public static int Capacity(IEnumerable<AnimalInstance> team, GameConfig config) =>
            Mathf.Min(config.partySize, Candidates(team, config).Count);

        /// <summary>The remembered team (those that can still fight), topped up with the strongest others.</summary>
        public static List<AnimalInstance> Default(IEnumerable<AnimalInstance> team, IReadOnlyList<string> lastTeamUids, GameConfig config)
        {
            var candidates = Candidates(team, config);
            int size = Mathf.Min(config.partySize, candidates.Count);
            var party = new List<AnimalInstance>();
            foreach (var uid in lastTeamUids)
            {
                var animal = candidates.Find(a => a.Uid == uid);
                if (animal != null && party.Count < size && !party.Contains(animal)) party.Add(animal);
            }
            foreach (var animal in candidates.OrderByDescending(a => a.Level).ThenByDescending(a => a.HPFraction))
            {
                if (party.Count >= size) break;
                if (!party.Contains(animal)) party.Add(animal);
            }
            return party;
        }
    }
}
