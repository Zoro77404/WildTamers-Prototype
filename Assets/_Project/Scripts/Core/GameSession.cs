using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildTamers.Animals;

namespace WildTamers.Core
{
    /// <summary>
    /// Game state that survives scene loads: the player's team (saved to disk), map state (location, wild spawns)
    /// and the battle hand-off between MapScene and BattleScene. Created on first access.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameSession : MonoBehaviour
    {
        private const float AutosaveInterval = 8f;

        private static GameSession instance;
        private static bool quitting;

        [SerializeField] private List<AnimalInstance> team = new List<AnimalInstance>();
        [SerializeField] private int activeIndex;
        [SerializeField] private List<WildSpawnRecord> wildSpawns = new List<WildSpawnRecord>();

        private int nextSpawnId = 1;
        private bool persistenceEnabled = true;
        private bool saveDirty;
        private float autosaveTimer;
        private string pendingMapMessage;
        private bool pendingMapWarning;

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

        /// <summary>
        /// Team strength used for wild levels: average of the three highest levels, rounded (1 when empty).
        /// Weak new catches don't drag the wild animals down to trivial levels.
        /// </summary>
        public int TeamLevel => team.Count == 0 ? 1
            : Mathf.Max(1, Mathf.RoundToInt((float)team.Select(a => a.Level).OrderByDescending(l => l).Take(3).Average()));

        /// <summary>Raised when animals join, the active animal changes or a battle changes levels/HP.</summary>
        public event Action TeamChanged;

        /// <summary>False for a throwaway debug team (BattleScene played directly without a save).</summary>
        public bool IsPersistent => persistenceEnabled;

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

            LoadSave();
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

        private void Update()
        {
            // The team slowly recovers while walking around (never during a battle).
            if (CurrentBattle == null && team.Count > 0 && Config.teamHealPerSecond > 0f)
            {
                float amount = Config.teamHealPerSecond * Time.deltaTime;
                foreach (var animal in team)
                    if (animal.Regenerate(amount)) saveDirty = true;
            }

            if (saveDirty)
            {
                autosaveTimer += Time.unscaledDeltaTime;
                if (autosaveTimer >= AutosaveInterval) Save();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && saveDirty) Save();
        }

        private void OnApplicationQuit()
        {
            if (saveDirty) Save();
            quitting = true;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => MapInputGate.Clear();

        // ---------- Team API ----------
        public AnimalInstance AddToTeam(AnimalData species, int level)
        {
            var animal = new AnimalInstance(species, level);
            team.Add(animal);
            if (team.Count == 1) activeIndex = 0;
            CommitTeam();
            return animal;
        }

        public void SetActive(int index)
        {
            if (index < 0 || index >= team.Count || index == activeIndex) return;
            activeIndex = index;
            CommitTeam();
        }

        public void HealTeam()
        {
            foreach (var animal in team) animal.HealFull();
        }

        public void NotifyTeamChanged() => CommitTeam();

        private void CommitTeam()
        {
            TeamChanged?.Invoke();
            Save();
        }

        // ---------- Save API ----------
        private void LoadSave()
        {
            if (!SaveSystem.TryLoad(out var data)) return;

            team.Clear();
            foreach (var entry in data.team)
            {
                var animal = AnimalInstance.FromSave(entry, Database, Config);
                if (animal != null) team.Add(animal);
                else Debug.LogWarning($"[Wild Tamers] Skipped unknown animal '{entry?.animalId}' in the save file.");
            }
            activeIndex = team.Count == 0 ? 0 : Mathf.Clamp(data.activeIndex, 0, team.Count - 1);
        }

        /// <summary>Writes the team to disk now (no-op for a throwaway debug team).</summary>
        public void Save()
        {
            saveDirty = false;
            autosaveTimer = 0f;
            if (!persistenceEnabled) return;
            if (team.Count == 0)
            {
                SaveSystem.Delete();
                return;
            }

            var data = new SaveData { activeIndex = activeIndex };
            foreach (var animal in team) data.team.Add(animal.ToSave());
            SaveSystem.Write(data);
        }

        /// <summary>Testing helper: wipes the save and restarts at the starter pick.</summary>
        public void ResetSave()
        {
            if (Fader.IsBusy) return;
            SaveSystem.Delete();
            team.Clear();
            activeIndex = 0;
            wildSpawns.Clear();
            CurrentBattle = null;
            persistenceEnabled = true;
            pendingMapMessage = null;
            saveDirty = false;
            TeamChanged?.Invoke();
            Fader.LoadScene(SceneNames.Map);
        }

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

        /// <summary>One-shot message for the map after returning from a battle ("Wolf joined your team!").</summary>
        public bool TryTakeMapMessage(out string message, out bool warning)
        {
            message = pendingMapMessage;
            warning = pendingMapWarning;
            pendingMapMessage = null;
            return !string.IsNullOrEmpty(message);
        }

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
            {
                // No save yet: fight with a throwaway team so testing the battle never skips the real starter pick.
                persistenceEnabled = false;
                Debug.Log("[Wild Tamers] Debug battle with a temporary team (not saved).");
                AddToTeam(Database.Starters.Count > 0 ? Database.Starters[0] : Database.Animals[0], Config.starterLevel);
            }
            var wild = Database.PickRandomWild();
            int level = Mathf.Clamp(TeamLevel + UnityEngine.Random.Range(-1, 2), 1, Config.maxLevel);
            CurrentBattle = new BattleRequest(new AnimalInstance(wild, level), ActiveAnimal, -1);
        }

        /// <summary>
        /// Applies a finished battle: the fought animal leaves the map; a win adds it to the team (full HP)
        /// and gives XP to the fighter; a loss heals the whole team. Saves right away.
        /// </summary>
        public BattleResult CompleteBattle(BattleOutcome outcome)
        {
            var battle = CurrentBattle;
            if (battle == null) return new BattleResult(outcome);
            if (battle.Result != null) return battle.Result;

            var result = new BattleResult(outcome);
            battle.Result = result;
            if (battle.WildSpawnId >= 0) RemoveWildSpawn(battle.WildSpawnId);

            switch (outcome)
            {
                case BattleOutcome.Won:
                    result.ExperienceGained = Config.ExperienceReward(battle.Player.Level, battle.Wild.Level);
                    result.Growth = battle.Player.AddExperience(result.ExperienceGained, Config);
                    battle.Wild.HealFull();
                    team.Add(battle.Wild);
                    result.Joined = battle.Wild;
                    SetMapMessage($"{battle.Wild.Name} joined your team!", false);
                    break;
                case BattleOutcome.Lost:
                    HealTeam();
                    SetMapMessage("Your team rested and is fully healed.", false);
                    break;
                case BattleOutcome.Escaped:
                    SetMapMessage("Got away safely!", false);
                    break;
            }

            CommitTeam();
            return result;
        }

        private void SetMapMessage(string message, bool warning)
        {
            pendingMapMessage = message;
            pendingMapWarning = warning;
        }

        public void ReturnToMap()
        {
            if (Fader.IsBusy) return;
            CurrentBattle = null;
            Fader.LoadScene(SceneNames.Map);
        }
    }
}
