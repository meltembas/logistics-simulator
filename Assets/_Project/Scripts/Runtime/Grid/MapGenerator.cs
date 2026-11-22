using UnityEngine;

namespace Logistics
{
    /// <summary>Seeded terrain: a meandering river east of the start area, tree clusters, sparse existing city.</summary>
    public static class MapGenerator
    {
        public static RectInt StartBounds(GameConfig cfg)
        {
            var s = cfg.startBoundsSize;
            int x = Mathf.Clamp(cfg.mapWidth / 2 - s.x / 2 - 4, 0, cfg.mapWidth - s.x);
            int y = Mathf.Clamp(cfg.mapHeight / 2 - s.y / 2, 0, cfg.mapHeight - s.y);
            return new RectInt(x, y, s.x, s.y);
        }

        public static GridMap Generate(GameConfig cfg, int seed)
        {
            var map = new GridMap(cfg.mapWidth, cfg.mapHeight, cfg.tileSize);
            var rng = new System.Random(seed);
            float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;
            var start = StartBounds(cfg);
            map.ActiveBounds = start;

            // River: runs south→north, kept east of the start area so the first weeks need no bridge.
            float riverBase = start.xMax + 3 + (float)rng.NextDouble() * 2f;
            for (int y = 0; y < map.Height; y++)
            {
                float meander = (Mathf.PerlinNoise(ox, y * 0.11f + oy) - 0.5f) * 9f;
                int cx = Mathf.RoundToInt(riverBase + meander);
                int width = Mathf.PerlinNoise(ox + 7.3f, y * 0.2f) > 0.62f ? 3 : 2;
                for (int dx = 0; dx < width; dx++)
                {
                    var t = new TileCoord(cx + dx, y);
                    if (map.InMap(t)) map.SetTerrain(t, TerrainKind.Water);
                }
                // keep the channel 4-connected when the meander shifts sideways
                if (y > 0)
                {
                    float prevMeander = (Mathf.PerlinNoise(ox, (y - 1) * 0.11f + oy) - 0.5f) * 9f;
                    int pcx = Mathf.RoundToInt(riverBase + prevMeander);
                    for (int x = Mathf.Min(pcx, cx); x <= Mathf.Max(pcx, cx); x++)
                    {
                        var t = new TileCoord(x, y);
                        if (map.InMap(t)) map.SetTerrain(t, TerrainKind.Water);
                    }
                }
            }

            // Tree clusters (cosmetic, cleared by construction). Sparse inside the start area.
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var t = new TileCoord(x, y);
                if (map.IsWater(t)) continue;
                float n = Mathf.PerlinNoise(x * 0.16f + ox + 31f, y * 0.16f + oy + 17f);
                float threshold = 1f - cfg.treeCoverage * 1.9f;
                bool inStart = start.Contains(new Vector2Int(x, y));
                if (inStart) threshold += 0.12f;
                if (n > threshold) map.SetTree(t, true);
            }

            // Existing city: small filler buildings (houses, apartments) that block construction.
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var t = new TileCoord(x, y);
                if (map.IsWater(t) || map.HasTree(t)) continue;
                bool nearStartCenter = Mathf.Abs(x - start.center.x) < 4 && Mathf.Abs(y - start.center.y) < 3;
                if (nearStartCenter) continue;
                float edge = Mathf.Min(Mathf.Min(x, map.Width - 1 - x), Mathf.Min(y, map.Height - 1 - y));
                float chance = cfg.fillerBuildingChance * (edge < 2 ? 2.2f : 1f);
                if (rng.NextDouble() < chance && !HasNeighborOccupant(map, t))
                    map.SetOccupant(t, Occupant.Filler);
            }
            return map;
        }

        static bool HasNeighborOccupant(GridMap map, TileCoord t)
        {
            foreach (var d in DirUtil.All)
            {
                var n = t.Step(d);
                if (map.InMap(n) && map.OccupantAt(n) != Occupant.None) return true;
            }
            return false;
        }
    }
}
