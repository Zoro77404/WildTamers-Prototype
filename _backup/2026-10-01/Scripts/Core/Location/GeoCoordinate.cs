using System;
using UnityEngine;

namespace WildTamers.Core
{
    /// <summary>A latitude/longitude pair in degrees (WGS84), as a GPS would report it.</summary>
    [Serializable]
    public struct GeoCoordinate : IEquatable<GeoCoordinate>
    {
        public double latitude;
        public double longitude;

        public GeoCoordinate(double latitude, double longitude)
        {
            this.latitude = latitude;
            this.longitude = longitude;
        }

        public bool Equals(GeoCoordinate other) => latitude.Equals(other.latitude) && longitude.Equals(other.longitude);
        public override bool Equals(object obj) => obj is GeoCoordinate other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(latitude, longitude);
        public static bool operator ==(GeoCoordinate a, GeoCoordinate b) => a.Equals(b);
        public static bool operator !=(GeoCoordinate a, GeoCoordinate b) => !a.Equals(b);

        public override string ToString() => $"{latitude:F6}, {longitude:F6}";

        /// <summary>Great-circle distance in meters (haversine).</summary>
        public static double DistanceMeters(GeoCoordinate a, GeoCoordinate b)
        {
            const double earthRadius = 6371000.0;
            double dLat = (b.latitude - a.latitude) * Mathf.Deg2Rad;
            double dLon = (b.longitude - a.longitude) * Mathf.Deg2Rad;
            double lat1 = a.latitude * Mathf.Deg2Rad;
            double lat2 = b.latitude * Mathf.Deg2Rad;
            double h = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return 2 * earthRadius * Math.Asin(Math.Min(1.0, Math.Sqrt(h)));
        }
    }
}
