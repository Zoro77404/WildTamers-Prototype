using System;
using UnityEngine;

namespace WildTamers.Core
{
    /// <summary>
    /// Inspector-assignable base for <see cref="ILocationProvider"/> implementations
    /// (FakeLocationProvider now, a GPS provider later).
    /// </summary>
    public abstract class LocationProviderBase : MonoBehaviour, ILocationProvider
    {
        public abstract bool HasLocation { get; }
        public abstract GeoCoordinate Location { get; }
        public virtual float HeadingDegrees => float.NaN;

        public event Action<GeoCoordinate> LocationChanged;

        protected void RaiseLocationChanged(GeoCoordinate location) => LocationChanged?.Invoke(location);
    }
}
