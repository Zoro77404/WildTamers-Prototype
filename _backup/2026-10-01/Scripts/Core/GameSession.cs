using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildTamers.Animals;

namespace WildTamers.Core
{
    /// <summary>
    /// Game state that survives scene loads: the player's team, map state (location, wild spawns)
    /// and the battle hand-off between MapScene and BattleScene. Created on first access.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameSession : MonoBehaviour
    {
        private static GameSession instance;
        private static bool quitting;

        [SerializeField] private List<AnimalInstance> team = new List<AnimalInstance>();
        [SerializeField] private int activeIndex;
        [SerializeField] private List<WildSpawnRecord> wildSpawns = new List<WildSpawnRecord>();

        private int nextSpawnId = 1;

        public static GameSession Instance
        {
            get
            {
                if (instance == null && !quitting)
                {
                    var go = new GameObject("[GameSession]");
                    instance = go.AddComponent<GameSession>();
                }
                return instance;
            }
        }

        public static bool Exists => instance != null;

        public AnimalDatabase Database { get; private set; }
        public GameConfig Config { get; private set; }
        public SceneFader Fader { get; private set; }
        public PreviewStudio Previews { get; private set; }

        // ---------- Team ----------
        public IReadOnlyList<AnimalInstance> Team => team;
        public bool HasStarter => team.Count > 0;
        public int ActiveIndex => activeIndex;
        public AnimalInstance ActiveAnimal => team.Count == 0 ? null : team[Mathf.Clamp(activeIndex, 0, team.Count - 1)];

        /// <summary>Average team level, rounded; 1 when the team is empty.</summary>
        public int TeamLevel => team.Count == 0 ? 1 : Mathf.Max(1, Mathf.RoundToInt((float)team.Average(a => a.Level)));

        public event Action TeamChanged;

        // ---------- Map ----------
        public bool HasMapOrigin { get; private set; }
        public GeoCoordinate MapOrigin { get; private set; }
        public bool HasLastLocation { get; private set; }
        public GeoCoordinate LastLocation { get; private set; }
        public List<WildSpawnRecord> WildSpawns => wildSpawns;

        // ---------- Battle ----------
        public BattleRequest CurrentBattle { get; private set; }

        /// <summary>Unscaled seconds since the session started; used for spawn lifetimes across scenes.</summary>
        public static double Clock => Time.unscaledTimeAsDouble;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            quitting = false;
            MapInputGate.Clear();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);

            Database = AnimalDatabase.Instance;
            Config = Resources.Load<GameConfig>(GameConfig.ResourcePath);
            if (Config == null)
            {
                Debug.LogWarning($"GameConfig missing at Resources/{GameConfig.ResourcePath}; using defaults.");
                Config = ScriptableObject.CreateInstance<GameConfig>();
            }

            Fader = new GameObject("SceneFader").AddComponent<SceneFader>();
            Fader.transform.SetParent(transform, false);
            Previews = new GameObject("PreviewStudio").AddComponent<PreviewStudio>();
            Previews.transform.SetParent(transform, false);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                instance = null;
            }
        }

        private void OnApplicationQuit() => quitting = true;

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => MapInputGate.Clear();

        // ---------- Team API ----------
        public AnimalInstance AddToTeam(AnimalData species, int level)
        {
            var animal = new AnimalInstance(species, level);
            team.Add(animal);
            if (team.Count == 1) activeIndex = 0;
            TeamChanged?.Invoke();
            return animal;
        }

        public void SetActive(int index)
        {
            if (index < 0 || index >= team.Count || index == activeIndex) return;
            activeIndex = index;
            TeamChanged?.Invoke();
        }

        public void NotifyTeamChanged() => TeamChanged?.Invoke();

        // ---------- Map API ----------
        public void SetMapOrigin(GeoCoordinate origin)
        {
            MapOrigin = origin;
            HasMapOrigin = true;
        }

        public void SetLastLocation(GeoCoordinate location)
        {
            LastLocation = location;
            HasLastLocation = true;
        }

        public WildSpawnRecord AddWildSpawn(AnimalData species, int level, GeoCoordinate location, float yaw, double lifetime)
        {
            var record = new WildSpawnRecord
            {
                id = nextSpawnId++,
                animalId = species.id,
                level = level,
                location = location,
                yaw = yaw,
                expiresAt = Clock + lifetime
            };
            wildSpawns.Add(record);
            return record;
        }

        public void RemoveWildSpawn(int id) => wildSpawns.RemoveAll(r => r.id == id);

        public WildSpawnRecord FindWildSpawn(int id) => wildSpawns.Find(r => r.id == id);

        // ---------- Battle API ----------
        /// <summary>Starts a battle against a wild spawn using the active team animal, then loads the battle scene.</summary>
        public bool StartBattle(WildSpawnRecord wild)
        {
            if (wild == null || ActiveAnimal == null || Fader.IsBusy) return false;
            var species = Database != null ? Database.Get(wild.animalId) : null;
            if (species == null)
            {
                Debug.LogError($"Unknown animal id '{wild.animalId}'.");
                return false;
            }

            CurrentBattle = new BattleRequest(new AnimalInstance(species, wild.level), ActiveAnimal, wild.id);
            Fader.LoadScene(SceneNames.Battle);
            return true;
        }

        /// <summary>Used when BattleScene is played directly in the editor.</summary>
        public void CreateDebugBattle()
        {
            if (Database == null || Database.Animals.Count == 0) return;
            if (!HasStarter)
                AddToTeam(Database.Starters.Count > 0 ? Database.Starters[0] : Database.Animals[0], Config.starterLevel);
            var wild = Database.PickRandomWild();
            CurrentBattle = new BattleRequest(new AnimalInstance(wild, TeamLevel), ActiveAnimal, -1);
        }

        public void ReturnToMap()
        {
            if (Fader.IsBusy) return;
            CurrentBattle = null;
            Fader.LoadScene(SceneNames.Map);
        }
    }
}
