using UnityEngine;

namespace WildTamers.Core
{
    /// <summary>Gameplay tuning shared by map, spawner and UI. Lives in Resources so GameSession can load it.</summary>
    [CreateAssetMenu(menuName = "Wild Tamers/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        public const string ResourcePath = "GameConfig";

        [Header("Encounters")]
        [Tooltip("How close (meters) the player must be to start a fight.")]
        [Min(1f)] public float fightRange = 13f;

        [Header("Wild spawns")]
        [Min(1)] public int maxWildAnimals = 8;
        [Min(1f)] public float minSpawnDistance = 7f;
        [Min(2f)] public float maxSpawnDistance = 40f;
        [Tooltip("Wild animals farther than this from the player disappear.")]
        [Min(5f)] public float despawnDistance = 60f;
        [Tooltip("Minimum gap between two wild animals.")]
        [Min(0f)] public float minSpawnSeparation = 7f;
        public Vector2 spawnIntervalRange = new Vector2(1.2f, 3f);
        [Tooltip("Seconds a wild animal stays before wandering off.")]
        public Vector2 wildLifetimeRange = new Vector2(150f, 300f);
        [Tooltip("Wild levels are the team level ± this.")]
        [Min(0)] public int wildLevelSpread = 2;
        [Min(1)] public int maxLevel = 50;

        [Tooltip("Wild animals are drawn larger than life on the map so they read well from the camera.")]
        [Min(0.1f)] public float wildAnimalScale = 1.7f;

        [Header("Team")]
        [Min(1)] public int starterLevel = 5;
    }
}
