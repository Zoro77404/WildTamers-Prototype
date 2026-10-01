using System.Collections.Generic;
using UnityEngine;
using WildTamers.Core;
using WildTamers.Map;

namespace WildTamers.Animals
{
    /// <summary>
    /// Keeps a handful of wild animals around the player: spawns them at random open spots,
    /// levels them near the team level, and removes ones that are far away or have wandered off.
    /// Spawns are stored in GameSession so they are still there after a battle.
    /// </summary>
    public class WildAnimalSpawner : MonoBehaviour
    {
        [SerializeField] private MapView mapView;
        [SerializeField] private Transform player;
        [SerializeField] private WildAnimal wildAnimalPrefab;
        [SerializeField] private Transform container;
        [Tooltip("Seconds between spawns while the map is still filling up.")]
        [SerializeField] private float fillInterval = 0.35f;
        [SerializeField] private int spawnAttempts = 14;

        private readonly Dictionary<int, WildAnimal> active = new Dictionary<int, WildAnimal>();
        private readonly List<int> scratch = new List<int>();
        private GameSession session;
        private GameConfig config;
        private float nextSpawnTime;

        public IEnumerable<WildAnimal> ActiveAnimals => active.Values;
        public int Count => active.Count;

        private void Start()
        {
            session = GameSession.Instance;
            config = session.Config;
            if (container == null) container = transform;
            RestoreFromSession();
            nextSpawnTime = Time.time + 0.6f;
        }

        private void RestoreFromSession()
        {
            var records = new List<WildSpawnRecord>(session.WildSpawns);
            foreach (var record in records)
            {
                var species = session.Database != null ? session.Database.Get(record.animalId) : null;
                var pos = mapView.GeoToWorld(record.location);
                if (species == null || GameSession.Clock >= record.expiresAt || FlatDistance(pos, player.position) > config.despawnDistance)
                {
                    session.RemoveWildSpawn(record.id);
                    continue;
                }
                Create(record, species, popIn: false);
            }
        }

        private void Update()
        {
            if (session == null || !session.HasStarter || player == null) return;

            UpdateExisting();

            if (active.Count < config.maxWildAnimals && Time.time >= nextSpawnTime)
            {
                // Keep a couple of animals close enough to see and reach; the rest fill the wider ring.
                float near = config.fightRange * 2f;
                TrySpawn(CountWithin(near) < 2 ? Mathf.Min(near, config.maxSpawnDistance) : config.maxSpawnDistance);
                bool filling = active.Count < Mathf.CeilToInt(config.maxWildAnimals * 0.5f);
                nextSpawnTime = Time.time + (filling ? fillInterval : Random.Range(config.spawnIntervalRange.x, config.spawnIntervalRange.y));
            }
        }

        private void UpdateExisting()
        {
            scratch.Clear();
            var playerPos = player.position;
            double now = GameSession.Clock;
            foreach (var pair in active)
            {
                var animal = pair.Value;
                if (animal == null) { scratch.Add(pair.Key); continue; }
                float distance = FlatDistance(animal.transform.position, playerPos);
                animal.SetInRange(distance <= config.fightRange);
                if (animal.IsEngaged) continue;
                if (distance > config.despawnDistance || now >= animal.Record.expiresAt) scratch.Add(pair.Key);
            }
            foreach (int id in scratch) Despawn(id);
        }

        private int CountWithin(float distance)
        {
            int n = 0;
            foreach (var animal in active.Values)
                if (animal != null && !animal.IsLeaving && FlatDistance(animal.transform.position, player.position) <= distance) n++;
            return n;
        }

        private bool TrySpawn(float maxDistance)
        {
            var db = session.Database;
            if (db == null) return false;

            for (int i = 0; i < spawnAttempts; i++)
            {
                // Uniform over the ring's area.
                float min = config.minSpawnDistance, max = Mathf.Max(min + 1f, maxDistance);
                float r = Mathf.Sqrt(Random.Range(min * min, max * max));
                float a = Random.Range(0f, Mathf.PI * 2f);
                var pos = player.position + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                pos.y = 0f;

                if (!mapView.IsSpawnable(pos) || TooCloseToOthers(pos)) continue;

                var species = db.PickRandomWild();
                if (species == null) return false;
                int level = Mathf.Clamp(session.TeamLevel + config.RollWildLevelOffset(), 1, config.maxLevel);
                float lifetime = Random.Range(config.wildLifetimeRange.x, config.wildLifetimeRange.y);
                var record = session.AddWildSpawn(species, level, mapView.WorldToGeo(pos), Random.Range(0f, 360f), lifetime);
                Create(record, species, popIn: true);
                return true;
            }
            return false;
        }

        private bool TooCloseToOthers(Vector3 pos)
        {
            float minSq = config.minSpawnSeparation * config.minSpawnSeparation;
            foreach (var animal in active.Values)
                if (animal != null && (animal.transform.position - pos).sqrMagnitude < minSq) return true;
            return false;
        }

        private void Create(WildSpawnRecord record, AnimalData species, bool popIn)
        {
            var animal = Instantiate(wildAnimalPrefab, mapView.GeoToWorld(record.location), Quaternion.identity, container);
            animal.Init(record, species, player, popIn);
            active[record.id] = animal;
        }

        /// <summary>Removes a wild animal from the map (and the session) with its leave animation.</summary>
        public void Despawn(int spawnId)
        {
            session.RemoveWildSpawn(spawnId);
            if (active.TryGetValue(spawnId, out var animal))
            {
                active.Remove(spawnId);
                if (animal != null) animal.Leave();
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
