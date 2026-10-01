using System.Collections.Generic;
using UnityEngine;

namespace WildTamers.Animals
{
    /// <summary>Registry of every species plus the starter choices. Lives in Resources.</summary>
    [CreateAssetMenu(menuName = "Wild Tamers/Animal Database", fileName = "AnimalDatabase")]
    public class AnimalDatabase : ScriptableObject
    {
        public const string ResourcePath = "AnimalDatabase";

        [SerializeField] private List<AnimalData> animals = new List<AnimalData>();
        [SerializeField] private List<AnimalData> starters = new List<AnimalData>();

        private Dictionary<string, AnimalData> byId;
        private static AnimalDatabase loaded;

        public IReadOnlyList<AnimalData> Animals => animals;
        public IReadOnlyList<AnimalData> Starters => starters;

        public static AnimalDatabase Instance
        {
            get
            {
                if (loaded == null)
                {
                    loaded = Resources.Load<AnimalDatabase>(ResourcePath);
                    if (loaded == null) Debug.LogError($"AnimalDatabase missing at Resources/{ResourcePath}.");
                }
                return loaded;
            }
        }

        public AnimalData Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (byId == null)
            {
                byId = new Dictionary<string, AnimalData>();
                foreach (var a in animals)
                    if (a != null && !string.IsNullOrEmpty(a.id)) byId[a.id] = a;
            }
            return byId.TryGetValue(id, out var data) ? data : null;
        }

        /// <summary>Weighted random species for a wild spawn.</summary>
        public AnimalData PickRandomWild()
        {
            float total = 0f;
            foreach (var a in animals) if (a != null) total += a.spawnWeight;
            if (total <= 0f) return animals.Count > 0 ? animals[Random.Range(0, animals.Count)] : null;

            float roll = Random.value * total;
            foreach (var a in animals)
            {
                if (a == null) continue;
                roll -= a.spawnWeight;
                if (roll <= 0f) return a;
            }
            return animals[animals.Count - 1];
        }

        private void OnValidate() => byId = null;
    }
}
