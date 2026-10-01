using System;

namespace WildTamers.Core
{
    /// <summary>A wild animal currently on the map. Kept in GameSession so spawns survive the trip to the battle scene.</summary>
    [Serializable]
    public class WildSpawnRecord
    {
        public int id;
        public string animalId;
        public int level;
        public GeoCoordinate location;
        /// <summary>Compass yaw the animal faces, degrees.</summary>
        public float yaw;
        /// <summary>Session time (unscaled seconds) at which it wanders off.</summary>
        public double expiresAt;
    }
}
