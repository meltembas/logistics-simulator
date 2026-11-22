using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>Decides when and where depots and stores appear, and grows the playable area weekly.</summary>
    public class SpawnDirector
    {
        readonly World w;
        float storeTimer;
        readonly List<CargoType> activeCargo = new List<CargoType>();
        public IReadOnlyList<CargoType> ActiveCargo => activeCargo;

        public SpawnDirector(World world) { w = world; }

        float StoreInterval()
        {
            var c = w.Config;
            float i = c.storeSpawnIntervalStart / (1f + 0.14f * (w.Week - 1));
            return Mathf.Max(c.storeSpawnIntervalMin, i) * (0.8f + (float)w.Rng.NextDouble() * 0.4f);
        }

        public void Begin()
        {
            var first = (CargoType)0;
            activeCargo.Add(first);
            var depot = SpawnDepot(first, preferCenter: true);
            if (depot != null)
                for (int i = 0; i < w.Config.initialStores; i++) SpawnStore(first, depot);
            storeTimer = StoreInterval();
        }

        public void Tick(float dt)
        {
            storeTimer -= dt;
            if (storeTimer > 0f) return;
            storeTimer = StoreInterval();
            if (activeCargo.Count == 0) return;

            // Weight cargo choice toward types with the fewest stores per depot.
            CargoType pick = activeCargo[0];
            float best = float.MaxValue;
            foreach (var c in activeCargo)
            {
                int stores = 0, depots = 0;
                foreach (var s in w.Stores) if (s.cargo == c) stores++;
                foreach (var d in w.Depots) if (d.cargo == c) depots++;
                float ratio = (stores + (float)w.Rng.NextDouble() * 1.5f) / Mathf.Max(1, depots);
                if (ratio < best) { best = ratio; pick = c; }
            }
            SpawnStore(pick, null);
        }

        public void OnWeekStarted(int week)
        {
            var g = w.Grid;
            int grow = w.Config.boundsGrowPerWeek;
            var b = g.ActiveBounds;
            int xMin = Mathf.Max(0, b.xMin - grow), yMin = Mathf.Max(0, b.yMin - grow / 2);
            int xMax = Mathf.Min(g.Width, b.xMax + grow), yMax = Mathf.Min(g.Height, b.yMax + grow / 2 + (week % 2));
            g.ActiveBounds = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);

            var unlocks = w.Config.cargoUnlockWeeks;
            for (int i = 0; i < unlocks.Length && i < CargoInfo.Count; i++)
            {
                if (unlocks[i] != week) continue;
                var cargo = (CargoType)i;
                if (activeCargo.Contains(cargo)) continue;
                activeCargo.Add(cargo);
                var d = SpawnDepot(cargo, false);
                if (d != null) SpawnStore(cargo, d);
                return;
            }

            if (week >= 3 && week % 2 == 1)
            {
                // Extra depot for the busiest cargo.
                CargoType busiest = activeCargo[0];
                float worst = -1f;
                foreach (var c in activeCargo)
                {
                    int stores = 0, depots = 0;
                    foreach (var s in w.Stores) if (s.cargo == c) stores++;
                    foreach (var d in w.Depots) if (d.cargo == c) depots++;
                    float ratio = stores / (float)Mathf.Max(1, depots);
                    if (ratio > worst) { worst = ratio; busiest = c; }
                }
                SpawnDepot(busiest, false);
            }
        }

        public Depot SpawnDepot(CargoType cargo, bool preferCenter)
        {
            var g = w.Grid;
            var b = g.ActiveBounds;
            var center = new Vector2(b.center.x, b.center.y);
            Depot result = null;
            float bestScore = float.MinValue;
            TileCoord bestOrigin = default;
            Dir bestFacing = Dir.N;

            for (int attempt = 0; attempt < 400; attempt++)
            {
                var o = new TileCoord(w.Rng.Next(b.xMin + 1, b.xMax - 2), w.Rng.Next(b.yMin + 1, b.yMax - 2));
                var f = DirUtil.All[w.Rng.Next(4)];
                if (!w.CanPlaceDepot(o, f)) continue;
                if (!HasClearance(o, new Vector2Int(2, 2), 1)) continue;

                var c = new Vector2(o.x + 1f, o.y + 1f);
                float score = -Vector2.Distance(c, center) * (preferCenter ? 3f : 0.6f);
                foreach (var d in w.Depots)
                {
                    float dist = Vector2.Distance(c, new Vector2(d.origin.x + 1f, d.origin.y + 1f));
                    score += Mathf.Min(dist, 10f) * 1.2f;
                    if (dist < 6f) score -= 30f;
                }
                // prefer entrances facing the area centre
                var toCenter = center - c;
                var fv = f.Offset();
                score += Vector2.Dot(toCenter.normalized, new Vector2(fv.x, fv.y)) * 2f;
                score += (float)w.Rng.NextDouble() * 2f;
                if (score > bestScore) { bestScore = score; bestOrigin = o; bestFacing = f; }
            }
            if (bestScore > float.MinValue) result = w.AddDepot(cargo, bestOrigin, bestFacing);
            return result;
        }

        public Store SpawnStore(CargoType cargo, Depot near)
        {
            if (near == null)
            {
                // the depot of this cargo with the fewest stores nearby
                int fewest = int.MaxValue;
                foreach (var d in w.Depots)
                {
                    if (d.cargo != cargo) continue;
                    int n = 0;
                    foreach (var s in w.Stores) if (s.cargo == cargo && s.origin.Manhattan(d.origin) < 12) n++;
                    if (n < fewest) { fewest = n; near = d; }
                }
            }
            var g = w.Grid;
            var b = g.ActiveBounds;
            var anchor = near != null ? new Vector2(near.origin.x + 1f, near.origin.y + 1f) : new Vector2(b.center.x, b.center.y);

            float bestScore = float.MinValue;
            TileCoord bestTile = default;
            Dir bestFacing = Dir.N;
            for (int attempt = 0; attempt < 300; attempt++)
            {
                var t = new TileCoord(w.Rng.Next(b.xMin, b.xMax), w.Rng.Next(b.yMin, b.yMax));
                float dist = Vector2.Distance(new Vector2(t.x, t.y), anchor);
                if (dist < 3f || dist > 10f) continue;
                Dir f = FacingToward(t, anchor);
                if (!w.CanPlaceStore(t, f))
                {
                    f = DirUtil.All[w.Rng.Next(4)];
                    if (!w.CanPlaceStore(t, f)) continue;
                }
                if (!HasClearance(t, Vector2Int.one, 1)) continue;
                if (!EntranceHasExit(t.Step(f), t)) continue;
                float score = -Mathf.Abs(dist - 5.5f) + (float)w.Rng.NextDouble() * 2.5f;
                foreach (var s in w.Stores)
                    if (s.origin.Manhattan(t) < 3) score -= 6f;
                if (score > bestScore) { bestScore = score; bestTile = t; bestFacing = f; }
            }
            if (bestScore == float.MinValue) return null;
            var store = w.AddStore(cargo, bestTile, bestFacing);
            // New demand brings new capacity: a truck for the nearest depot of this cargo.
            Depot home = null;
            int best = int.MaxValue;
            foreach (var d in w.Depots)
                if (d.cargo == cargo && d.entrance.Manhattan(store.entrance) < best) { best = d.entrance.Manhattan(store.entrance); home = d; }
            for (int i = 0; i < w.Config.trucksPerStore && home != null && home.trucks.Count < w.Config.maxTrucksPerDepot; i++)
                w.AddTruck(home);
            return store;
        }

        static Dir FacingToward(TileCoord t, Vector2 anchor)
        {
            var d = anchor - new Vector2(t.x, t.y);
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) return d.x > 0 ? Dir.E : Dir.W;
            return d.y > 0 ? Dir.N : Dir.S;
        }

        bool EntranceHasExit(TileCoord entrance, TileCoord building)
        {
            foreach (var d in DirUtil.All)
            {
                var n = entrance.Step(d);
                if (n == building) continue;
                if (w.Grid.IsBuildableGround(n) || w.Roads.IsRoad(n)) return true;
            }
            return false;
        }

        /// <summary>No other building footprint within <paramref name="margin"/> tiles.</summary>
        bool HasClearance(TileCoord origin, Vector2Int size, int margin)
        {
            for (int y = origin.y - margin; y < origin.y + size.y + margin; y++)
            for (int x = origin.x - margin; x < origin.x + size.x + margin; x++)
            {
                var t = new TileCoord(x, y);
                if (!w.Grid.InMap(t)) continue;
                if (w.Grid.OccupantAt(t) == Occupant.Building) return false;
                if (w.Roads.IsFixed(t)) return false;
            }
            return true;
        }
    }
}
