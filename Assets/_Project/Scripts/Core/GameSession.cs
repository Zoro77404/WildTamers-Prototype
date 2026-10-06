using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using WildTamers.Animals;

namespace WildTamers.Core
{
    /// <summary>
    /// Game state that survives scene loads: the player's animals (saved to disk), the last fight team,
    /// map state (location, wild spawns) and the battle hand-off between MapScene and BattleScene. Created on first access.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameSession : MonoBehaviour
    {
        private const float AutosaveInterval = 8f;

        private static GameSession instance;
        private static bool quitting;

        [SerializeField] private List<AnimalInstance> team = new List<AnimalInstance>();
        [SerializeField] private List<WildSpawnRecord> wildSpawns = new List<WildSpawnRecord>();

        private readonly List<string> lastTeamUids = new List<string>();
        private readonly List<string> seenSpecies = new List<string>();
        private int nextSpawnId = 1;
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

        /// <summary>The animals picked for the last fight (the team select screen starts from these), in pick order.</summary>
        public IReadOnlyList<AnimalInstance> LastTeam => lastTeamUids.Select(uid => team.Find(a => a.Uid == uid)).Where(a => a != null).ToList();

        /// <summary>First animal of the last fight team (shown on the map HUD); the first animal owned if there is none yet.</summary>
        public AnimalInstance LeadAnimal => LastTeam.FirstOrDefault() ?? (team.Count > 0 ? team[0] : null);

        /// <summary>
        /// Team strength used for wild levels: average of the three highest levels, rounded (1 when empty).
        /// Weak new catches don't drag the wild animals down to trivial levels.
        /// </summary>
        public int TeamLevel => team.Count == 0 ? 1
            : Mathf.Max(1, Mathf.RoundToInt((float)team.Select(a => a.Level).OrderByDescending(l => l).Take(3).Average()));

        /// <summary>Raised when animals join, the last team changes or a battle changes levels/HP.</summary>
        public event Action TeamChanged;

        /// <summary>True if this animal can be picked for a fight: not fainted and not too hurt.</summary>
        public bool IsFightReady(AnimalInstance animal) => PartySelection.IsFightReady(animal, Config);

        /// <summary>Animals that can be picked for the next fight (see <see cref="PartySelection.Candidates"/>).</summary>
        public List<AnimalInstance> GetFightCandidates() => PartySelection.Candidates(team, Config);

        /// <summary>How many animals the next fight takes: the party size, or fewer if fewer animals can fight.</summary>
        public int PartyCapacity => PartySelection.Capacity(team, Config);

        /// <summary>The remembered team (those that can still fight), topped up with the strongest others.</summary>
        public List<AnimalInstance> ChooseDefaultParty() => PartySelection.Default(team, lastTeamUids, Config);

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
            CommitTeam();
            return animal;
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

        /// <summary>Remembers the fight team for next time and saves.</summary>
        public void SetLastTeam(IEnumerable<AnimalInstance> party)
        {
            lastTeamUids.Clear();
            foreach (var animal in party)
                if (animal != null && team.Contains(animal) && !lastTeamUids.Contains(animal.Uid)) lastTeamUids.Add(animal.Uid);
            TeamChanged?.Invoke();
            Save();
        }

        // ---------- "New animal!" cards ----------
        public bool HasSeen(string speciesId) => seenSpecies.Contains(speciesId);

        /// <summary>Species the player owns but has never seen the "New animal!" card for (in the order they were gained).</summary>
        public List<AnimalData> GetUnseenSpecies()
        {
            var list = new List<AnimalData>();
            foreach (var animal in team)
            {
                if (animal.Data == null || seenSpecies.Contains(animal.AnimalId) || list.Contains(animal.Data)) continue;
                list.Add(animal.Data);
            }
            return list;
        }

        /// <summary>Call when the card was shown; it never pops up again for this species. Saves.</summary>
        public void MarkSeen(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId) || seenSpecies.Contains(speciesId)) return;
            seenSpecies.Add(speciesId);
            Save();
        }

        // ---------- Save API ----------
        private void LoadSave()
        {
            if (Database == null) return;
            bool hadSave = SaveSystem.TryLoad(out var data);
            var result = SaveMigration.Apply(hadSave ? data : new SaveData(), Database, Config);

            team.Clear();
            team.AddRange(result.Team);
            lastTeamUids.Clear();
            lastTeamUids.AddRange(result.LastTeam);
            seenSpecies.Clear();
            seenSpecies.AddRange(result.SeenSpecies);

            if (hadSave && (result.Converted > 0 || result.Dropped > 0 || result.StartersAdded > 0))
                Debug.Log($"[Wild Tamers] Old save updated: {result.Converted} animal(s) became new species, {result.StartersAdded} starter(s) added.");
            if (!hadSave || result.Changed) Save();
        }

        /// <summary>Writes the animals, the last fight team and the seen cards to disk now.</summary>
        public void Save()
        {
            saveDirty = false;
            autosaveTimer = 0f;
            if (team.Count == 0)
            {
                SaveSystem.Delete();
                return;
            }

            var data = new SaveData();
            foreach (var animal in team) data.team.Add(animal.ToSave());
            data.lastTeam.AddRange(lastTeamUids);
            data.seenSpecies.AddRange(seenSpecies);
            SaveSystem.Write(data);
        }

        /// <summary>Testing helper: wipes the save and starts again with the three starter animals.</summary>
        public void ResetSave()
        {
            if (Fader.IsBusy) return;
            SaveSystem.Delete();
            team.Clear();
            lastTeamUids.Clear();
            seenSpecies.Clear();
            wildSpawns.Clear();
            CurrentBattle = null;
            pendingMapMessage = null;
            team.AddRange(SaveMigration.Apply(new SaveData(), Database, Config).Team);
            Save();
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
        /// <summary>
        /// Starts a battle: <paramref name="party"/> (up to the party size, fainted animals are left out)
        /// against the wild animal, which becomes a boss for the fight. Remembers the party, then loads the battle scene.
        /// </summary>
        public bool StartBattle(WildSpawnRecord wild, IReadOnlyList<AnimalInstance> party)
        {
            if (wild == null || party == null || Fader.IsBusy) return false;
            var species = Database != null ? Database.Get(wild.animalId) : null;
            if (species == null)
            {
                Debug.LogError($"Unknown animal id '{wild.animalId}'.");
                return false;
            }
            var fighters = party.Where(a => a != null && !a.IsFainted && team.Contains(a)).Distinct().Take(Config.partySize).ToList();
            if (fighters.Count == 0) return false;

            var boss = new AnimalInstance(species, wild.level);
            boss.MakeBoss(Config);
            SetLastTeam(fighters);
            CurrentBattle = new BattleRequest(boss, fighters, wild.id);
            Fader.LoadScene(SceneNames.Battle);
            return true;
        }

        /// <summary>Used when BattleScene is played directly in the editor.</summary>
        public void CreateDebugBattle()
        {
            if (Database == null || Database.Animals.Count == 0 || team.Count == 0) return;
            var wild = Database.PickRandomWild();
            int level = Mathf.Clamp(TeamLevel + UnityEngine.Random.Range(-1, 2), 1, Config.maxLevel);
            var boss = new AnimalInstance(wild, level);
            boss.MakeBoss(Config);
            CurrentBattle = new BattleRequest(boss, ChooseDefaultParty(), -1);
        }

        /// <summary>
        /// Applies a finished battle: the fought animal leaves the map; a win adds it to the team (full HP, no longer a boss)
        /// and gives every animal of the party the same XP; a loss heals the whole team. Saves right away.
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
                    int partyLevel = Mathf.Max(1, Mathf.RoundToInt((float)battle.Party.Average(a => a.Level)));
                    result.ExperienceGained = Config.ExperienceReward(partyLevel, battle.Wild.Level);
                    foreach (var animal in battle.Party)
                    {
                        bool fainted = animal.IsFainted;
                        result.Party.Add(new PartyGrowth { Animal = animal, WasFainted = fainted, Growth = animal.AddExperience(result.ExperienceGained, Config) });
                    }
                    battle.Wild.ClearBoss();
                    battle.Wild.HealFull();
                    result.JoinedIsNewSpecies = !seenSpecies.Contains(battle.Wild.AnimalId);
                    team.Add(battle.Wild);
                    result.Joined = battle.Wild;
                    SetMapMessage($"{battle.Wild.Name} joined your team!", false);
                    break;
                case BattleOutcome.Lost:
                    HealTeam();
                    SetMapMessage("Your team rested and is fully healed.", false);
                    break;
                case BattleOutcome.Escaped:
                    SetMapMessage("Your team got away safely!", false);
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
