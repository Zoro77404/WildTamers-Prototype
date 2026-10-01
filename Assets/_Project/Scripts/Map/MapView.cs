using System.Collections.Generic;
using UnityEngine;
using WildTamers.Core;

namespace WildTamers.Map
{
    /// <summary>
    /// The map the player walks on. Converts GPS coordinates to world space and streams map tiles
    /// around a focus point. Visuals come from a <see cref="MapTileProvider"/> (fake city now, real tiles later).
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MapView : MonoBehaviour
    {
        [Header("Location")]
        [Tooltip("Origin used when nothing else is known (also the fake provider's start point).")]
        [SerializeField] private GeoCoordinate defaultOrigin = new GeoCoordinate(51.5007, -0.1246);
        [Tooltip("Optional: the first position from this provider becomes the map origin.")]
        [SerializeField] private LocationProviderBase locationProvider;

        [Header("Tiles")]
        [SerializeField] private MapTileProvider tileProvider;
        [SerializeField] private Transform focusTarget;
        [SerializeField, Min(8f)] private float tileSize = 64f;
        [Tooltip("Tiles within this distance (meters) of the focus are kept loaded.")]
        [SerializeField, Min(16f)] private float viewDistance = 170f;
        [SerializeField, Min(1)] private int maxTileBuildsPerFrame = 2;

        private readonly Dictionary<Vector2Int, GameObject> tiles = new Dictionary<Vector2Int, GameObject>();
        private readonly List<Vector2Int> buildQueue = new List<Vector2Int>();
        private readonly List<Vector2Int> scratch = new List<Vector2Int>();
        private Transform tileRoot;
        private Vector2Int lastCenter = new Vector2Int(int.MinValue, int.MinValue);
        private bool initialized;

        public GeoProjection Projection { get; private set; }
        public float TileSize => tileSize;
        public MapTileProvider TileProvider => tileProvider;

        private void Awake()
        {
            var session = GameSession.Instance;
            GeoCoordinate origin;
            if (session.HasMapOrigin) origin = session.MapOrigin;
            else if (locationProvider != null && locationProvider.HasLocation) origin = locationProvider.Location;
            else origin = defaultOrigin;
            session.SetMapOrigin(origin);
            Projection = new GeoProjection(origin);

            tileRoot = new GameObject("Tiles").transform;
            tileRoot.SetParent(transform, false);
        }

        private void Start()
        {
            if (focusTarget != null) RefreshTiles(focusTarget.position, buildAllNow: true);
            initialized = true;
        }

        private void Update()
        {
            if (!initialized || focusTarget == null) return;
            RefreshTiles(focusTarget.position, buildAllNow: false);
        }

        public Vector3 GeoToWorld(GeoCoordinate geo) => Projection.GeoToWorld(geo);
        public GeoCoordinate WorldToGeo(Vector3 world) => Projection.WorldToGeo(world);

        /// <summary>Intersects a screen ray with the ground plane (y = 0).</summary>
        public bool RaycastGround(Ray ray, out Vector3 point)
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter) && enter < 5000f)
            {
                point = ray.GetPoint(enter);
                return true;
            }
            point = default;
            return false;
        }

        /// <summary>True where a wild animal may appear.</summary>
        public bool IsSpawnable(Vector3 worldPosition)
        {
            return tileProvider == null || tileProvider.IsOpenGround(worldPosition, tileSize);
        }

        public void SetFocus(Transform target) => focusTarget = target;

        private void RefreshTiles(Vector3 focus, bool buildAllNow)
        {
            var center = new Vector2Int(Mathf.FloorToInt(focus.x / tileSize), Mathf.FloorToInt(focus.z / tileSize));
            if (center != lastCenter)
            {
                lastCenter = center;
                QueueVisibleTiles(focus);
                UnloadFarTiles(focus);
            }

            int budget = buildAllNow ? int.MaxValue : maxTileBuildsPerFrame;
            while (budget-- > 0 && buildQueue.Count > 0)
            {
                var coord = buildQueue[0];
                buildQueue.RemoveAt(0);
                if (tiles.ContainsKey(coord) || tileProvider == null) continue;
                var tile = tileProvider.BuildTile(coord, tileSize, tileRoot);
                if (tile != null) tiles[coord] = tile;
            }
        }

        private void QueueVisibleTiles(Vector3 focus)
        {
            int radius = Mathf.CeilToInt(viewDistance / tileSize) + 1;
            var center = lastCenter;
            buildQueue.Clear();
            for (int x = -radius; x <= radius; x++)
            for (int z = -radius; z <= radius; z++)
            {
                var coord = new Vector2Int(center.x + x, center.y + z);
                if (tiles.ContainsKey(coord)) continue;
                if (DistanceToTile(focus, coord) <= viewDistance) buildQueue.Add(coord);
            }
            buildQueue.Sort((a, b) => DistanceToTile(focus, a).CompareTo(DistanceToTile(focus, b)));
        }

        private void UnloadFarTiles(Vector3 focus)
        {
            scratch.Clear();
            foreach (var pair in tiles)
                if (DistanceToTile(focus, pair.Key) > viewDistance + tileSize) scratch.Add(pair.Key);
            foreach (var coord in scratch)
            {
                tileProvider.ReleaseTile(coord, tiles[coord]);
                tiles.Remove(coord);
            }
        }

        /// <summary>Distance from a point to the nearest edge of a tile (0 when inside).</summary>
        private float DistanceToTile(Vector3 p, Vector2Int coord)
        {
            float minX = coord.x * tileSize, minZ = coord.y * tileSize;
            float dx = Mathf.Max(minX - p.x, 0f, p.x - (minX + tileSize));
            float dz = Mathf.Max(minZ - p.z, 0f, p.z - (minZ + tileSize));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
