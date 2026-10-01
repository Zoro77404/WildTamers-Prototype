using System;
using UnityEngine;
using WildTamers.Core;

namespace WildTamers.Map
{
    /// <summary>
    /// Local flat-earth projection around an origin: 1 world unit = 1 meter, +X = east, +Z = north.
    /// Accurate to well under a meter across the few kilometers a walking session covers.
    /// </summary>
    public readonly struct GeoProjection
    {
        public const double MetersPerDegreeLatitude = 111320.0;

        public readonly GeoCoordinate Origin;
        private readonly double metersPerDegreeLongitude;

        public GeoProjection(GeoCoordinate origin)
        {
            Origin = origin;
            metersPerDegreeLongitude = MetersPerDegreeLongitudeAt(origin.latitude);
        }

        public Vector3 GeoToWorld(GeoCoordinate geo)
        {
            double x = (geo.longitude - Origin.longitude) * metersPerDegreeLongitude;
            double z = (geo.latitude - Origin.latitude) * MetersPerDegreeLatitude;
            return new Vector3((float)x, 0f, (float)z);
        }

        public GeoCoordinate WorldToGeo(Vector3 world)
        {
            return new GeoCoordinate(
                Origin.latitude + world.z / MetersPerDegreeLatitude,
                Origin.longitude + world.x / metersPerDegreeLongitude);
        }

        /// <summary>Moves a coordinate by a metric offset (east, north) — usable without a map, e.g. by a fake GPS.</summary>
        public static GeoCoordinate Offset(GeoCoordinate from, double eastMeters, double northMeters)
        {
            return new GeoCoordinate(
                from.latitude + northMeters / MetersPerDegreeLatitude,
                from.longitude + eastMeters / MetersPerDegreeLongitudeAt(from.latitude));
        }

        private static double MetersPerDegreeLongitudeAt(double latitude)
        {
            return Math.Max(1.0, MetersPerDegreeLatitude * Math.Cos(latitude * Math.PI / 180.0));
        }
    }
}
