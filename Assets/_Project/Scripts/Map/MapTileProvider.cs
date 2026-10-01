using UnityEngine;

namespace WildTamers.Map
{
    /// <summary>
    /// Supplies the visuals for one square map tile. The fake procedural city is one implementation;
    /// a real map-tile provider (e.g. Google Maps) can replace it without changing MapView's users.
    /// </summary>
    public abstract class MapTileProvider : MonoBehaviour
    {
        /// <summary>Builds the tile whose south-west corner is at <paramref name="coord"/> * tileSize.</summary>
        public abstract GameObject BuildTile(Vector2Int coord, float tileSize, Transform parent);

        /// <summary>Frees a tile that scrolled out of view.</summary>
        public virtual void ReleaseTile(Vector2Int coord, GameObject tile)
        {
            if (tile == null) return;
            foreach (var filter in tile.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) Destroy(filter.sharedMesh);
            Destroy(tile);
        }

        /// <summary>True where a wild animal may stand (not inside buildings, water or roads).</summary>
        public virtual bool IsOpenGround(Vector3 worldPosition, float tileSize) => true;
    }
}
