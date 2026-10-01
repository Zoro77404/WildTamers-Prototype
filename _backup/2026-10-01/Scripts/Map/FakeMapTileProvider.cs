using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WildTamers.Map
{
    /// <summary>Material slots used by the fake city. The order matches <see cref="FakeMapTileProvider"/>'s material array.</summary>
    public enum MapMaterial
    {
        Grass, Park, Plaza, Path, Sidewalk, Road, RoadLine, Water, Sand,
        Building0, Building1, Building2, Building3, Building4, Building5,
        RoofFlat, RoofRed, RoofBlue, Trunk, Leaves0, Leaves1, Leaves2, Rock,
        FlowerPink, FlowerYellow, FlowerWhite, Wood,
        RoofTint0, RoofTint1, RoofTint2, RoofTint3, RoofTint4, RoofTint5
    }

    /// <summary>
    /// Procedural, deterministic "map-like" city: a grid of roads with blocks of buildings, parks,
    /// plazas and meadows. Stands in for real map tiles during the prototype.
    /// </summary>
    public class FakeMapTileProvider : MapTileProvider
    {
        public static readonly int MaterialCount = System.Enum.GetValues(typeof(MapMaterial)).Length;

        [SerializeField] private int seed = 20261001;
        [Tooltip("One material per MapMaterial entry, in enum order.")]
        [SerializeField] private Material[] materials = new Material[0];

        [Header("Roads")]
        [SerializeField, Min(4f)] private float roadWidth = 9f;
        [SerializeField, Min(0.5f)] private float sidewalkWidth = 1.6f;
        [SerializeField, Range(0f, 1f)] private float roadChance = 0.9f;

        // Heights of the flat layers, spaced so they never z-fight at map distances.
        private const float YPatch = 0.04f, YSidewalk = 0.06f, YPath = 0.08f, YSand = 0.07f,
            YRoad = 0.1f, YWater = 0.11f, YLine = 0.13f, YFlower = 0.14f;

        private readonly Dictionary<Vector2Int, TileLayout> layouts = new Dictionary<Vector2Int, TileLayout>();

        public Material[] Materials { get => materials; set => materials = value; }

        private enum BlockKind { Buildings, Park, Plaza, Meadow }

        private class TileLayout
        {
            public bool RoadWest, RoadSouth, CornerHorizontal, CornerVertical;
            public BlockKind Kind;
            public readonly List<Rect> Blocked = new List<Rect>();          // world-space footprints
            public readonly List<Vector3> BlockedCircles = new List<Vector3>(); // x, z, radius
        }

        // ------------------------------------------------------------------
        // MapTileProvider
        // ------------------------------------------------------------------

        public override GameObject BuildTile(Vector2Int coord, float tileSize, Transform parent)
        {
            var layout = GetLayout(coord, tileSize, out var groundMb, out var propsMb);

            var tile = new GameObject($"Tile {coord.x},{coord.y}");
            tile.transform.SetParent(parent, false);
            tile.transform.localPosition = new Vector3(coord.x * tileSize, 0f, coord.y * tileSize);
            tile.isStatic = true;

            AddMeshObject(tile.transform, "Ground", groundMb, ShadowCastingMode.Off);
            AddMeshObject(tile.transform, "Props", propsMb, ShadowCastingMode.On);
            return tile;
        }

        public override void ReleaseTile(Vector2Int coord, GameObject tile)
        {
            layouts.Remove(coord);
            base.ReleaseTile(coord, tile);
        }

        public override bool IsOpenGround(Vector3 p, float tileSize)
        {
            var coord = new Vector2Int(Mathf.FloorToInt(p.x / tileSize), Mathf.FloorToInt(p.z / tileSize));
            // Layouts are deterministic; generating one for an unbuilt tile gives the same obstacles it will have.
            if (!layouts.TryGetValue(coord, out var layout))
                layout = GetLayout(coord, tileSize, out _, out _);

            float lx = p.x - coord.x * tileSize, lz = p.z - coord.y * tileSize;
            const float margin = 1.2f;
            bool nearWest = lx < roadWidth + margin, nearSouth = lz < roadWidth + margin;
            if (layout.RoadWest && nearWest) return false;
            if (layout.RoadSouth && nearSouth) return false;
            if ((layout.CornerHorizontal || layout.CornerVertical) && nearWest && nearSouth) return false;
            // The east/north neighbours' roads run along this tile's far edges.
            if (lx > tileSize - margin || lz > tileSize - margin) return false;

            foreach (var r in layout.Blocked)
                if (r.Contains(new Vector2(p.x, p.z))) return false;
            foreach (var c in layout.BlockedCircles)
                if ((new Vector2(p.x - c.x, p.z - c.y)).sqrMagnitude < c.z * c.z) return false;
            return true;
        }

        // ------------------------------------------------------------------
        // Layout + geometry
        // ------------------------------------------------------------------

        /// <summary>
        /// Generates a tile's geometry and obstacle list. Geometry and obstacles come from the same random
        /// sequence, so both are always produced together to keep them consistent.
        /// </summary>
        private TileLayout GetLayout(Vector2Int c, float size, out MeshBuilder ground, out MeshBuilder props)
        {
            ground = new MeshBuilder(MaterialCount);
            props = new MeshBuilder(MaterialCount);

            var layout = new TileLayout
            {
                RoadWest = HasRoadWest(c.x, c.y),
                RoadSouth = HasRoadSouth(c.x, c.y),
                CornerHorizontal = HasRoadSouth(c.x - 1, c.y),
                CornerVertical = HasRoadWest(c.x, c.y - 1)
            };
            var rng = new System.Random(Hash(c.x, c.y, 1));
            var origin = new Vector2(c.x * size, c.y * size);

            ground.RectXZ((int)MapMaterial.Grass, new Rect(0f, 0f, size, size), 0f);
            BuildRoads(ground, layout, size);

            float x0 = layout.RoadWest ? roadWidth : 0f;
            float z0 = layout.RoadSouth ? roadWidth : 0f;
            var interior = new Rect(x0, z0, size - x0, size - z0);

            double roll = rng.NextDouble();
            layout.Kind = roll < 0.44 ? BlockKind.Buildings : roll < 0.72 ? BlockKind.Park : roll < 0.84 ? BlockKind.Plaza : BlockKind.Meadow;

            var ctx = new BlockContext { Ground = ground, Props = props, Rng = rng, Layout = layout, Origin = origin };
            switch (layout.Kind)
            {
                case BlockKind.Buildings: BuildBuildingBlock(ctx, interior); break;
                case BlockKind.Park: BuildPark(ctx, interior); break;
                case BlockKind.Plaza: BuildPlaza(ctx, interior); break;
                default: BuildMeadow(ctx, interior); break;
            }

            layouts[c] = layout;
            return layout;
        }

        private class BlockContext
        {
            public MeshBuilder Ground, Props;
            public System.Random Rng;
            public TileLayout Layout;
            public Vector2 Origin;

            public float Range(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
            public int Range(int min, int maxExclusive) => Rng.Next(min, maxExclusive);
            public bool Chance(float p) => Rng.NextDouble() < p;

            public void BlockRect(Rect local, float pad)
            {
                Layout.Blocked.Add(new Rect(local.x + Origin.x - pad, local.y + Origin.y - pad, local.width + pad * 2f, local.height + pad * 2f));
            }

            public void BlockCircle(Vector2 local, float radius)
            {
                Layout.BlockedCircles.Add(new Vector3(local.x + Origin.x, local.y + Origin.y, radius));
            }
        }

        private void BuildRoads(MeshBuilder mb, TileLayout layout, float size)
        {
            float R = roadWidth, s = sidewalkWidth;
            int side = (int)MapMaterial.Sidewalk, road = (int)MapMaterial.Road;

            if (layout.RoadWest)
            {
                mb.RectXZ(side, new Rect(0f, 0f, R, size), YSidewalk);
                mb.RectXZ(road, new Rect(s, 0f, R - 2f * s, size), YRoad);
                Dashes(mb, new Vector2(R * 0.5f, R + 1.5f), new Vector2(R * 0.5f, size - 1.5f));
                Crosswalk(mb, new Rect(s, R + 0.6f, R - 2f * s, 2.6f), vertical: false);
            }
            if (layout.RoadSouth)
            {
                mb.RectXZ(side, new Rect(0f, 0f, size, R), YSidewalk);
                mb.RectXZ(road, new Rect(0f, s, size, R - 2f * s), YRoad);
                Dashes(mb, new Vector2(R + 1.5f, R * 0.5f), new Vector2(size - 1.5f, R * 0.5f));
                Crosswalk(mb, new Rect(R + 0.6f, s, 2.6f, R - 2f * s), vertical: true);
            }
            // Roads arriving at this tile's south-west corner from neighbours.
            if (layout.CornerHorizontal && !layout.RoadSouth)
            {
                mb.RectXZ(side, new Rect(0f, 0f, R, R), YSidewalk);
                mb.RectXZ(road, new Rect(0f, s, R, R - 2f * s), YRoad);
            }
            if (layout.CornerVertical && !layout.RoadWest)
            {
                mb.RectXZ(side, new Rect(0f, 0f, R, R), YSidewalk);
                mb.RectXZ(road, new Rect(s, 0f, R - 2f * s, R), YRoad);
            }
        }

        private static void Dashes(MeshBuilder mb, Vector2 from, Vector2 to)
        {
            const float dash = 2.4f, gap = 2.8f, w = 0.28f;
            var dir = to - from;
            float len = dir.magnitude;
            dir /= len;
            for (float d = 0f; d + dash <= len; d += dash + gap)
            {
                var a = from + dir * d;
                var b = from + dir * (d + dash);
                var r = Mathf.Abs(dir.x) > 0.5f
                    ? new Rect(a.x, a.y - w * 0.5f, b.x - a.x, w)
                    : new Rect(a.x - w * 0.5f, a.y, w, b.y - a.y);
                mb.RectXZ((int)MapMaterial.RoadLine, r, YLine);
            }
        }

        private static void Crosswalk(MeshBuilder mb, Rect area, bool vertical)
        {
            const float stripe = 0.55f, gap = 0.55f;
            if (vertical)
            {
                for (float z = area.yMin + 0.3f; z + stripe <= area.yMax - 0.2f; z += stripe + gap)
                    mb.RectXZ((int)MapMaterial.RoadLine, new Rect(area.xMin, z, area.width, stripe), YLine);
            }
            else
            {
                for (float x = area.xMin + 0.3f; x + stripe <= area.xMax - 0.2f; x += stripe + gap)
                    mb.RectXZ((int)MapMaterial.RoadLine, new Rect(x, area.yMin, stripe, area.height), YLine);
            }
        }

        // ---------- Block types ----------

        private void BuildBuildingBlock(BlockContext ctx, Rect interior)
        {
            var area = Shrink(interior, 1.5f);
            int nx = area.width > 46f ? ctx.Range(2, 4) : 2;
            int nz = area.height > 46f ? ctx.Range(2, 4) : 2;
            const float gap = 3f;
            float lotW = (area.width - gap * (nx - 1)) / nx;
            float lotH = (area.height - gap * (nz - 1)) / nz;

            ctx.Ground.RectXZ((int)MapMaterial.Sidewalk, Shrink(interior, 0.6f), YPatch);

            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                var lot = new Rect(area.xMin + ix * (lotW + gap), area.yMin + iz * (lotH + gap), lotW, lotH);
                if (ctx.Chance(0.84f)) Building(ctx, lot);
                else Garden(ctx, lot);
            }
        }

        private void Building(BlockContext ctx, Rect lot)
        {
            float l = ctx.Range(0.4f, 2.2f), r = ctx.Range(0.4f, 2.2f), b = ctx.Range(0.4f, 2.2f), t = ctx.Range(0.4f, 2.2f);
            var fp = new Rect(lot.xMin + l, lot.yMin + b, Mathf.Max(6f, lot.width - l - r), Mathf.Max(6f, lot.height - b - t));
            ctx.BlockRect(fp, 1.2f);
            int variant = ctx.Range(0, 6);
            int wall = (int)MapMaterial.Building0 + variant;
            int roofTint = (int)MapMaterial.RoofTint0 + variant;
            bool house = fp.width * fp.height < 200f && ctx.Chance(0.55f);

            var mb = ctx.Props;

            if (house)
            {
                float h = ctx.Range(4.2f, 6f);
                mb.Box(wall, fp, 0f, h);
                var roofFp = Grow(fp, 0.45f);
                int roof = ctx.Chance(0.6f) ? (int)MapMaterial.RoofRed : (int)MapMaterial.RoofBlue;
                mb.GableRoof(roof, roofFp, h, ctx.Range(2.4f, 3.6f), wall);
                // Door.
                var door = new Rect(fp.center.x - 0.7f, fp.yMin - 0.12f, 1.4f, 0.12f);
                mb.Box((int)MapMaterial.Wood, door, 0f, 2.2f);
            }
            else
            {
                float u = ctx.Range(0f, 1f);
                float h = 6f + u * u * 18f;
                mb.Box(wall, fp, 0f, h);
                // Floor ledges give a sense of storeys.
                for (float y = 3.4f; y < h - 1.5f; y += 3.4f)
                    mb.Box((int)MapMaterial.RoofFlat, Grow(fp, 0.14f), y, y + 0.28f);
                // The camera looks down, so roofs carry the color: a tinted slab inside a wall-colored rim.
                RoofSlab(mb, fp, h, roofTint);

                if (h > 12f && ctx.Chance(0.45f))
                {
                    var upper = Shrink(fp, Mathf.Min(fp.width, fp.height) * 0.22f);
                    float h2 = ctx.Range(3f, 7f);
                    mb.Box(wall, upper, h, h + h2);
                    RoofSlab(mb, upper, h + h2, roofTint);
                }
                else if (ctx.Chance(0.6f))
                {
                    var ac = new Rect(fp.center.x + ctx.Range(-2f, 1f), fp.center.y + ctx.Range(-2f, 1f), 1.8f, 1.4f);
                    mb.Box((int)MapMaterial.RoofFlat, ac, h + 0.3f, h + 1.3f);
                }
            }
        }

        private static void RoofSlab(MeshBuilder mb, Rect fp, float h, int tint)
        {
            float rim = Mathf.Clamp(Mathf.Min(fp.width, fp.height) * 0.08f, 0.5f, 1.1f);
            mb.Box(tint, Shrink(fp, rim), h, h + 0.3f);
        }

        private void Garden(BlockContext ctx, Rect lot)
        {
            ctx.Ground.RectXZ((int)MapMaterial.Park, Shrink(lot, 0.6f), YPath);
            int trees = ctx.Range(1, 3);
            for (int i = 0; i < trees; i++)
                Tree(ctx, new Vector2(ctx.Range(lot.xMin + 2f, lot.xMax - 2f), ctx.Range(lot.yMin + 2f, lot.yMax - 2f)), ctx.Range(0.85f, 1.15f));
            Flowers(ctx, lot.center + new Vector2(ctx.Range(-2f, 2f), ctx.Range(-2f, 2f)), 1.4f);
        }

        private void BuildPark(BlockContext ctx, Rect interior)
        {
            var area = Shrink(interior, 1f);
            ctx.Ground.RectXZ((int)MapMaterial.Park, area, YPatch);

            // Paths: a cross through the middle, sometimes offset.
            var c = area.center + new Vector2(ctx.Range(-6f, 6f), ctx.Range(-6f, 6f));
            const float pw = 2.6f;
            var pathH = new Rect(area.xMin, c.y - pw * 0.5f, area.width, pw);
            var pathV = new Rect(c.x - pw * 0.5f, area.yMin, pw, area.height);
            bool both = ctx.Chance(0.7f);
            ctx.Ground.RectXZ((int)MapMaterial.Path, pathH, YPath);
            if (both) ctx.Ground.RectXZ((int)MapMaterial.Path, pathV, YPath);
            ctx.Ground.Disc((int)MapMaterial.Path, new Vector3(c.x, YPath + 0.005f, c.y), 3.2f, 10);

            // Pond in one quadrant.
            bool hasPond = ctx.Chance(0.55f);
            Vector2 pond = Vector2.zero;
            float pondR = 0f;
            if (hasPond)
            {
                int q = ctx.Range(0, 4);
                var quad = Quadrant(area, c, q);
                pondR = Mathf.Min(ctx.Range(4.5f, 7.5f), Mathf.Min(quad.width, quad.height) * 0.5f - 2.2f);
                if (pondR > 2.5f)
                {
                    pond = quad.center;
                    ctx.BlockCircle(pond, pondR + 1.2f);
                    float ang = ctx.Range(0f, 1f);
                    ctx.Ground.Disc((int)MapMaterial.Sand, new Vector3(pond.x, YSand + 0.03f, pond.y), pondR + 0.9f, 11, ang);
                    ctx.Ground.Disc((int)MapMaterial.Water, new Vector3(pond.x, YWater, pond.y), pondR, 11, ang);
                }
                else hasPond = false;
            }

            var placed = new List<Vector2>();
            int attempts = ctx.Range(16, 26);
            for (int i = 0; i < attempts; i++)
            {
                var p = new Vector2(ctx.Range(area.xMin + 2f, area.xMax - 2f), ctx.Range(area.yMin + 2f, area.yMax - 2f));
                if (Mathf.Abs(p.y - c.y) < pw + 1f || (both && Mathf.Abs(p.x - c.x) < pw + 1f)) continue;
                if (hasPond && Vector2.Distance(p, pond) < pondR + 2.5f) continue;
                if (TooClose(placed, p, 4.2f)) continue;
                placed.Add(p);
                if (ctx.Chance(0.82f)) Tree(ctx, p, ctx.Range(0.8f, 1.25f));
                else Bush(ctx, p);
            }
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector2(ctx.Range(area.xMin + 3f, area.xMax - 3f), ctx.Range(area.yMin + 3f, area.yMax - 3f));
                if (hasPond && Vector2.Distance(p, pond) < pondR + 2f) continue;
                if (Mathf.Abs(p.y - c.y) < pw) continue;
                Flowers(ctx, p, 1.6f);
            }
            Bench(ctx, new Vector2(c.x + 5f, c.y + pw * 0.5f + 0.9f), alongX: true);
            Bench(ctx, new Vector2(c.x - 6f, c.y - pw * 0.5f - 0.9f), alongX: true);
        }

        private void BuildPlaza(BlockContext ctx, Rect interior)
        {
            var area = Shrink(interior, 1f);
            var c = area.center;
            const float basinR = 4.2f;
            ctx.BlockCircle(c, basinR + 1.2f);

            ctx.Ground.RectXZ((int)MapMaterial.Plaza, area, YPatch);
            ctx.Ground.Disc((int)MapMaterial.Path, new Vector3(c.x, YPath, c.y), basinR + 3f, 16);

            var mb = ctx.Props;
            mb.Cylinder((int)MapMaterial.Rock, new Vector3(c.x, 0f, c.y), basinR, 0.75f, 16, (int)MapMaterial.Rock);
            mb.Disc((int)MapMaterial.Water, new Vector3(c.x, 0.78f, c.y), basinR - 0.45f, 16);
            mb.Cylinder((int)MapMaterial.Rock, new Vector3(c.x, 0f, c.y), 0.55f, 2.4f, 8);
            mb.Cylinder((int)MapMaterial.Rock, new Vector3(c.x, 2.4f, c.y), 1.4f, 0.3f, 10);
            mb.Disc((int)MapMaterial.Water, new Vector3(c.x, 2.72f, c.y), 1.15f, 10);

            // Planters with trees at the corners.
            float ox = area.width * 0.32f, oz = area.height * 0.32f;
            for (int i = 0; i < 4; i++)
            {
                var p = c + new Vector2(i % 2 == 0 ? -ox : ox, i < 2 ? -oz : oz);
                mb.Box((int)MapMaterial.Wood, new Rect(p.x - 1.6f, p.y - 1.6f, 3.2f, 3.2f), 0f, 0.6f, (int)MapMaterial.Park);
                Tree(ctx, p, ctx.Range(0.85f, 1.05f), 0.6f);
                ctx.BlockCircle(p, 2.6f);
            }
            Bench(ctx, c + new Vector2(0f, basinR + 4.2f), alongX: true);
            Bench(ctx, c + new Vector2(0f, -basinR - 4.2f), alongX: true);
            Bench(ctx, c + new Vector2(basinR + 4.2f, 0f), alongX: false);
            Flowers(ctx, c + new Vector2(-ox, 0f), 1.5f);
            Flowers(ctx, c + new Vector2(ox, 0f), 1.5f);
        }

        private void BuildMeadow(BlockContext ctx, Rect interior)
        {
            var area = Shrink(interior, 1.5f);
            bool cottage = ctx.Chance(0.4f);
            Rect house = default;
            if (cottage)
            {
                var p = new Vector2(ctx.Range(area.xMin + 6f, area.xMax - 14f), ctx.Range(area.yMin + 6f, area.yMax - 12f));
                house = new Rect(p.x, p.y, ctx.Range(7f, 9f), ctx.Range(6f, 7.5f));
                Building(ctx, new Rect(house.xMin - 0.5f, house.yMin - 0.5f, house.width + 1f, house.height + 1f));
            }

            var placed = new List<Vector2>();
            int clusters = ctx.Range(2, 4);
            for (int k = 0; k < clusters; k++)
            {
                var cc = new Vector2(ctx.Range(area.xMin + 5f, area.xMax - 5f), ctx.Range(area.yMin + 5f, area.yMax - 5f));
                int n = ctx.Range(2, 6);
                for (int i = 0; i < n; i++)
                {
                    var p = cc + new Vector2(ctx.Range(-6f, 6f), ctx.Range(-6f, 6f));
                    if (!area.Contains(p) || TooClose(placed, p, 3.8f)) continue;
                    if (cottage && Grow(house, 3f).Contains(p)) continue;
                    placed.Add(p);
                    Tree(ctx, p, ctx.Range(0.8f, 1.3f));
                }
            }
            int rocks = ctx.Range(2, 6);
            for (int i = 0; i < rocks; i++)
            {
                var p = new Vector2(ctx.Range(area.xMin + 2f, area.xMax - 2f), ctx.Range(area.yMin + 2f, area.yMax - 2f));
                if (cottage && Grow(house, 2f).Contains(p)) continue;
                Rock(ctx, p, ctx.Range(0.6f, 1.4f));
            }
            for (int i = 0; i < 4; i++)
            {
                var p = new Vector2(ctx.Range(area.xMin + 3f, area.xMax - 3f), ctx.Range(area.yMin + 3f, area.yMax - 3f));
                if (cottage && Grow(house, 2f).Contains(p)) continue;
                Flowers(ctx, p, 2f);
            }
        }

        // ---------- Props ----------

        private void Tree(BlockContext ctx, Vector2 p, float s, float baseY = 0f)
        {
            var mb = ctx.Props;
            var b = new Vector3(p.x, baseY, p.y);
            float yaw = ctx.Range(0f, 360f);
            if (ctx.Chance(0.68f))
            {
                mb.Cylinder((int)MapMaterial.Trunk, b, 0.3f * s, 2f * s, 6, -1, 0.22f * s, yaw * Mathf.Deg2Rad);
                int leaves = ctx.Chance(0.5f) ? (int)MapMaterial.Leaves0 : (int)MapMaterial.Leaves1;
                mb.Blob(leaves, b + Vector3.up * 3f * s, new Vector3(1.9f, 1.65f, 1.9f) * s, 0.13f, ctx.Rng, yaw);
                if (ctx.Chance(0.4f))
                    mb.Blob(leaves, b + new Vector3(0.7f, 3.9f, 0.3f) * s, new Vector3(1.1f, 1f, 1.1f) * s, 0.12f, ctx.Rng, yaw);
            }
            else
            {
                int leaves = (int)MapMaterial.Leaves2;
                float a = yaw * Mathf.Deg2Rad;
                mb.Cylinder((int)MapMaterial.Trunk, b, 0.28f * s, 1.3f * s, 6, -1, 0.22f * s, a);
                mb.Cone(leaves, b + Vector3.up * 1.1f * s, 1.9f * s, 2.8f * s, 7, a);
                mb.Cone(leaves, b + Vector3.up * 2.4f * s, 1.45f * s, 2.4f * s, 7, a + 0.3f);
                mb.Cone(leaves, b + Vector3.up * 3.6f * s, 0.95f * s, 2f * s, 7, a + 0.6f);
            }
            ctx.BlockCircle(p, 1.4f * s);
        }

        private void Bush(BlockContext ctx, Vector2 p)
        {
            float s = ctx.Range(0.8f, 1.3f);
            int leaves = ctx.Chance(0.5f) ? (int)MapMaterial.Leaves0 : (int)MapMaterial.Leaves1;
            ctx.Props.Blob(leaves, new Vector3(p.x, 0.5f * s, p.y), new Vector3(1.1f, 0.75f, 1f) * s, 0.15f, ctx.Rng, ctx.Range(0f, 360f));
        }

        private void Rock(BlockContext ctx, Vector2 p, float s)
        {
            ctx.Props.Blob((int)MapMaterial.Rock, new Vector3(p.x, 0.25f * s, p.y), new Vector3(1f, 0.6f, 0.85f) * s, 0.22f, ctx.Rng, ctx.Range(0f, 360f));
            ctx.BlockCircle(p, 1.1f * s);
        }

        private void Flowers(BlockContext ctx, Vector2 center, float radius)
        {
            int n = ctx.Range(6, 11);
            int color = (int)MapMaterial.FlowerPink + ctx.Range(0, 3);
            for (int i = 0; i < n; i++)
            {
                var p = center + new Vector2(ctx.Range(-radius, radius), ctx.Range(-radius, radius));
                ctx.Props.Blob(i % 4 == 0 ? (int)MapMaterial.FlowerWhite : color, new Vector3(p.x, YFlower + 0.12f, p.y), Vector3.one * 0.22f, 0.1f, null);
            }
        }

        private void Bench(BlockContext ctx, Vector2 p, bool alongX)
        {
            var mb = ctx.Props;
            var seat = alongX ? new Rect(p.x - 1.1f, p.y - 0.3f, 2.2f, 0.6f) : new Rect(p.x - 0.3f, p.y - 1.1f, 0.6f, 2.2f);
            mb.Box((int)MapMaterial.Wood, seat, 0.42f, 0.55f);
            var leg1 = alongX ? new Rect(seat.xMin + 0.15f, seat.yMin + 0.1f, 0.18f, 0.4f) : new Rect(seat.xMin + 0.1f, seat.yMin + 0.15f, 0.4f, 0.18f);
            var leg2 = alongX ? new Rect(seat.xMax - 0.33f, seat.yMin + 0.1f, 0.18f, 0.4f) : new Rect(seat.xMin + 0.1f, seat.yMax - 0.33f, 0.4f, 0.18f);
            mb.Box((int)MapMaterial.Rock, leg1, 0f, 0.42f);
            mb.Box((int)MapMaterial.Rock, leg2, 0f, 0.42f);
        }

        // ---------- Helpers ----------

        private void AddMeshObject(Transform parent, string name, MeshBuilder mb, ShadowCastingMode shadows)
        {
            if (mb == null || mb.IsEmpty) return;
            var mesh = mb.Build(name, out var used);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mats = new Material[used.Count];
            for (int i = 0; i < used.Count; i++) mats[i] = used[i] < materials.Length ? materials[used[i]] : null;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = shadows;
            mr.receiveShadows = true;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private bool HasRoadWest(int x, int z) => Roll(x, z, 11) < roadChance;
        private bool HasRoadSouth(int x, int z) => Roll(x, z, 23) < roadChance;

        private float Roll(int x, int z, int salt) => (Hash(x, z, salt) & 0xFFFFFF) / (float)0x1000000;

        private int Hash(int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 374761393u + (uint)x * 668265263u + (uint)z * 2246822519u + (uint)salt * 3266489917u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        private static Rect Shrink(Rect r, float d) => new Rect(r.xMin + d, r.yMin + d, Mathf.Max(0f, r.width - 2f * d), Mathf.Max(0f, r.height - 2f * d));
        private static Rect Grow(Rect r, float d) => new Rect(r.xMin - d, r.yMin - d, r.width + 2f * d, r.height + 2f * d);

        private static Rect Quadrant(Rect area, Vector2 split, int q)
        {
            return q switch
            {
                0 => Rect.MinMaxRect(area.xMin, area.yMin, split.x, split.y),
                1 => Rect.MinMaxRect(split.x, area.yMin, area.xMax, split.y),
                2 => Rect.MinMaxRect(area.xMin, split.y, split.x, area.yMax),
                _ => Rect.MinMaxRect(split.x, split.y, area.xMax, area.yMax)
            };
        }

        private static bool TooClose(List<Vector2> points, Vector2 p, float minDistance)
        {
            foreach (var q in points)
                if ((q - p).sqrMagnitude < minDistance * minDistance) return true;
            return false;
        }
    }
}
