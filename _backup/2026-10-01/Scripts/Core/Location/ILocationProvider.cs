using System;

namespace WildTamers.Core
{
    /// <summary>
    /// Source of the player's real-world position. The map and player only talk to this interface,
    /// so the fake keyboard/mouse provider can later be swapped for a GPS provider without touching other code.
    /// </summary>
    public interface ILocationProvider
    {
        /// <summary>True once the provider has a usable position.</summary>
        bool HasLocation { get; }

        /// <summary>Latest known position.</summary>
        GeoCoordinate Location { get; }

        /// <summary>Compass heading of travel in degrees (0 = north, 90 = east). NaN when unknown.</summary>
        float HeadingDegrees { get; }

        /// <summary>Raised whenever <see cref="Location"/> changes.</summary>
        event Action<GeoCoordinate> LocationChanged;
    }
}
