using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WildTamers.Animals;

namespace WildTamers.Core
{
    /// <summary>
    /// Turns whatever is in the save file (nothing, a version 1 save with the old animals, or a current save)
    /// into a valid team: removed species become their replacement, missing starters are added,
    /// and the last fight team / seen-card list are cleaned up. Pure logic so it can be tested without a scene.
    /// </summary>
    public static class SaveMigration
    {
        public class Result
        {
            public readonly List<AnimalInstance> Team = new List<AnimalInstance>();
            public readonly List<string> LastTeam = new List<string>();
            public readonly List<string> SeenSpecies = new List<string>();
            /// <summary>True if the data differs from what was saved (a save is worth writing).</summary>
            public bool Changed;
            public int Converted, Dropped, StartersAdded;
        }

        public static Result Apply(SaveData data, AnimalDatabase database, GameConfig config)
        {
            var result = new Result();
            data ??= new SaveData();
            bool oldVersion = data.version < SaveSystem.Version;
            if (oldVersion) result.Changed = true;

            // ---------- Animals ----------
            // Maps an old uid to the animal that came out of it (uids are kept, so lastTeam stays valid).
            var byOriginalIndex = new Dictionary<int, AnimalInstance>();
            for (int i = 0; i < data.team.Count; i++)
            {
                var entry = data.team[i];
                var animal = AnimalInstance.FromSave(entry, database, config);
                if (animal == null)
                {
                    Debug.LogWarning($"[Wild Tamers] Skipped unknown animal '{entry?.animalId}' in the save file.");
                    result.Dropped++;
                    result.Changed = true;
                    continue;
                }
                if (animal.AnimalId != entry.animalId)
                {
                    result.Converted++;
                    result.Changed = true;
                }
                // Version 1 healed a saved 0 HP animal on load; keep that for old saves only.
                if (oldVersion && entry.currentHP <= 0) animal.HealFull();
                result.Team.Add(animal);
                byOriginalIndex[i] = animal;
            }

            // ---------- Starters ----------
            foreach (var starter in database.Starters)
            {
                if (starter == null || result.Team.Any(a => a.AnimalId == starter.id)) continue;
                result.Team.Add(new AnimalInstance(starter, config.starterLevel));
                result.StartersAdded++;
                result.Changed = true;
            }

            // ---------- Last fight team ----------
            var uids = new HashSet<string>(result.Team.Select(a => a.Uid));
            foreach (var uid in data.lastTeam)
            {
                if (!string.IsNullOrEmpty(uid) && uids.Contains(uid) && !result.LastTeam.Contains(uid) && result.LastTeam.Count < config.partySize)
                    result.LastTeam.Add(uid);
            }
            if (oldVersion && result.LastTeam.Count == 0 && byOriginalIndex.TryGetValue(data.activeIndex, out var legacyActive))
                result.LastTeam.Add(legacyActive.Uid); // the animal that used to fight alone leads the new team
            if (result.LastTeam.Count != data.lastTeam.Count) result.Changed = true;

            // ---------- Seen cards ----------
            foreach (var id in data.seenSpecies)
            {
                var species = database.Resolve(id);
                if (species != null && !result.SeenSpecies.Contains(species.id)) result.SeenSpecies.Add(species.id);
            }
            if (result.SeenSpecies.Count != data.seenSpecies.Count) result.Changed = true;

            return result;
        }
    }
}
