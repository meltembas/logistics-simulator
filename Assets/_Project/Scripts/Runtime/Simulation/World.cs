using System;
using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>
    /// The whole game simulation, free of MonoBehaviours so it can be unit-tested and stepped at any speed.
    /// Views subscribe to the events and read state.
    /// </summary>
    public class World
    {
        public readonly GameConfig Config;
        public readonly GridMap Grid;
        public readonly RoadNetwork Roads;
        public readonly Inventory Inventory = new Inventory();
        public readonly RoadPathfinder Pathfinder;
        public readonly TrafficSystem Traffic;
        public readonly RoadEditor Editor;
        public readonly SpawnDirector Spawner;
        public readonly System.Random Rng;
        public readonly int Seed;

        public readonly List<Depot> Depots = new List<Depot>();
        public readonly List<Store> Stores = new List<Store>();
        readonly Dictionary<TileCoord, Building> buildingAt = new Dictionary<TileCoord, Building>();

        public int Score { get; private set; }
        public int Week { get; private set; } = 1;
        public float WeekTimer { get; private set; }
        public float WeekProgress => Mathf.Clamp01(WeekTimer / Config.weekSeconds);
        public float Elapsed { get; private set; }
        public bool IsGameOver { get; private set; }
        public Store FailedStore { get; private set; }
        /// <summary>Tests turn this off to keep the map fixed.</summary>
        public bool SpawningEnabled = true;

        int nextBuildingId = 1;
        int nextTruckId = 1;
        float dispatchTimer;

        public event Action<Depot> DepotAdded;
        public event Action<Store> StoreAdded;
        public event Action<TruckAgent> TruckAdded;
        public event Action<Store> OrderPlaced;
        public event Action<Store, TruckAgent> Delivered;
        public event Action<int> WeekEnded;
        public event Action<Store> GameOver;
        public event Action<Store> OverflowWarning;

        public World(GameConfig config, int seed)
        {
            Config = config;
            Seed = seed;
            Rng = new System.Random(seed);
            Grid = MapGenerator.Generate(config, seed);
            Roads = new RoadNetwork(Grid);
            Pathfinder = new RoadPathfinder(Grid, Roads, config.highwaySpeedMultiplier);
            Traffic = new TrafficSystem(this);
            Pathfinder.Occupancy = Traffic.OccupancyAt;
            Editor = new RoadEditor(this);
            Spawner = new SpawnDirector(this);
            Inventory.Roads = config.startRoadTiles;
            Inventory.Bridges = config.startBridges;
            Inventory.Roundabouts = config.startRoundabouts;
            Inventory.Highways = config.startHighways;
        }

        /// <summary>Creates a world with an empty map (no river, trees or filler) — for tests.</summary>
        public static World CreateEmpty(GameConfig config)
        {
            var w = new World(config, 1);
            for (int y = 0; y < w.Grid.Height; y++)
            for (int x = 0; x < w.Grid.Width; x++)
            {
                var t = new TileCoord(x, y);
                w.Grid.SetTerrain(t, TerrainKind.Ground);
                w.Grid.SetTree(t, false);
                if (w.Grid.OccupantAt(t) == Occupant.Filler) w.Grid.SetOccupant(t, Occupant.None);
            }
            w.Grid.ActiveBounds = new RectInt(0, 0, w.Grid.Width, w.Grid.Height);
            return w;
        }

        public Building BuildingAt(TileCoord t) => buildingAt.TryGetValue(t, out var b) ? b : null;

        // ───────────────────────────── Buildings ─────────────────────────────

        static TileCoord DepotDock(TileCoord origin, Dir facing)
        {
            // Front tiles are the two whose step along 'facing' leaves the 2x2 footprint; dock = first of them.
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
            {
                var t = new TileCoord(origin.x + x, origin.y + y);
                var n = t.Step(facing);
                bool outside = n.x < origin.x || n.y < origin.y || n.x > origin.x + 1 || n.y > origin.y + 1;
                if (outside) return t;
            }
            return origin;
        }

        public bool CanPlaceDepot(TileCoord origin, Dir facing)
        {
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
                if (!Grid.IsBuildableGround(new TileCoord(origin.x + x, origin.y + y))) return false;
            var entrance = DepotDock(origin, facing).Step(facing);
            return Grid.IsBuildableGround(entrance);
        }

        public bool CanPlaceStore(TileCoord tile, Dir facing) =>
            Grid.IsBuildableGround(tile) && Grid.IsBuildableGround(tile.Step(facing));

        public Depot AddDepot(CargoType cargo, TileCoord origin, Dir facing)
        {
            var d = new Depot
            {
                id = nextBuildingId++, cargo = cargo, origin = origin, size = new Vector2Int(2, 2), facing = facing
            };
            d.dockTile = DepotDock(origin, facing);
            d.entrance = d.dockTile.Step(facing);
            foreach (var t in d.Tiles())
            {
                Grid.SetOccupant(t, Occupant.Building);
                buildingAt[t] = d;
            }
            Roads.AddRoad(d.entrance, true);
            Roads.SetBuildingSide(d.entrance, facing.Opposite());
            Depots.Add(d);
            DepotAdded?.Invoke(d);
            for (int i = 0; i < Config.trucksPerDepot; i++) AddTruck(d);
            return d;
        }

        public Store AddStore(CargoType cargo, TileCoord tile, Dir facing)
        {
            var s = new Store
            {
                id = nextBuildingId++, cargo = cargo, origin = tile, size = Vector2Int.one, facing = facing,
                dockTile = tile, entrance = tile.Step(facing),
                orderTimer = 3f + (float)Rng.NextDouble() * 4f
            };
            Grid.SetOccupant(tile, Occupant.Building);
            buildingAt[tile] = s;
            Roads.AddRoad(s.entrance, true);
            Roads.SetBuildingSide(s.entrance, facing.Opposite());
            Stores.Add(s);
            StoreAdded?.Invoke(s);
            return s;
        }

        public TruckAgent AddTruck(Depot d)
        {
            var t = new TruckAgent { id = nextTruckId++, depot = d, parkSlot = d.trucks.Count, State = TruckState.Parked };
            ParkingPose(d, t.parkSlot, out t.position, out t.forward);
            d.trucks.Add(t);
            Traffic.Trucks.Add(t);
            TruckAdded?.Invoke(t);
            return t;
        }

        public void ParkingPose(Depot d, int slot, out Vector3 pos, out Vector3 fwd)
        {
            fwd = d.facing.Vec();
            var c = Grid.TileCenter(d.ParkingTile);
            var side = RouteBuilder.Right(fwd);
            float[] offsets = { -2.6f, 0f, 2.6f };
            if (slot < offsets.Length) pos = c + side * offsets[slot] - fwd * 0.4f;
            else pos = Grid.TileCenter(d.dockTile) - fwd * Grid.TileSize * 0.6f; // inside the warehouse
        }

        // ───────────────────────────── Simulation ─────────────────────────────

        public void Begin()
        {
            Spawner.Begin();
        }

        public void Tick(float dt)
        {
            if (IsGameOver || dt <= 0f) return;
            Elapsed += dt;

            WeekTimer += dt;
            if (WeekTimer >= Config.weekSeconds)
            {
                WeekTimer = 0f;
                Week++;
                Inventory.Add(ItemKind.Road, Config.weeklyRoadTiles);
                if (SpawningEnabled) Spawner.OnWeekStarted(Week);
                WeekEnded?.Invoke(Week);
            }

            if (SpawningEnabled) Spawner.Tick(dt);
            TickStores(dt);
            if (IsGameOver) return;

            dispatchTimer -= dt;
            if (dispatchTimer <= 0f)
            {
                dispatchTimer = 0.25f;
                Dispatch();
            }

            Traffic.Tick(dt);
            TickUnloading(dt);
        }

        public float OrderInterval()
        {
            float baseI = Config.orderIntervalStart / (1f + Config.orderIntervalWeeklyGrowth * (Week - 1));
            baseI = Mathf.Max(Config.orderIntervalMin, baseI);
            return baseI * (0.75f + (float)Rng.NextDouble() * 0.5f);
        }

        void TickStores(float dt)
        {
            foreach (var s in Stores)
            {
                s.age += dt;
                s.orderTimer -= dt;
                if (s.orderTimer <= 0f)
                {
                    s.undelivered++;
                    s.orderTimer = OrderInterval();
                    OrderPlaced?.Invoke(s);
                }

                float before = s.overflow;
                if (s.undelivered >= Config.overflowThreshold)
                    s.overflow += dt / Config.overflowSeconds;
                else
                    s.overflow -= dt * Config.overflowRecoverRate / Config.overflowSeconds;
                s.overflow = Mathf.Clamp01(s.overflow);
                if (before == 0f && s.overflow > 0f) OverflowWarning?.Invoke(s);

                if (s.overflow >= 1f)
                {
                    IsGameOver = true;
                    FailedStore = s;
                    GameOver?.Invoke(s);
                    return;
                }
            }
        }

        readonly List<Store> dispatchOrder = new List<Store>();

        void Dispatch()
        {
            dispatchOrder.Clear();
            foreach (var s in Stores) if (s.Unassigned > 0) dispatchOrder.Add(s);
            dispatchOrder.Sort((a, b) =>
            {
                int c = b.overflow.CompareTo(a.overflow);
                return c != 0 ? c : b.Unassigned.CompareTo(a.Unassigned);
            });

            foreach (var store in dispatchOrder)
            {
                int comp = Roads.Component(store.entrance);
                if (comp == 0) continue;
                while (store.Unassigned > 0)
                {
                    Depot best = null;
                    int bestDist = int.MaxValue;
                    foreach (var d in Depots)
                    {
                        if (d.cargo != store.cargo || d.IdleTrucks == 0) continue;
                        if (Roads.Component(d.entrance) != comp) continue;
                        int dist = d.entrance.Manhattan(store.entrance);
                        if (dist < bestDist) { bestDist = dist; best = d; }
                    }
                    if (best == null) break;
                    if (!SendTruck(best, store)) break;
                }
            }
        }

        bool SendTruck(Depot depot, Store store)
        {
            var path = Pathfinder.FindPath(depot.entrance, store.entrance);
            if (path == null) return false;
            var tiles = new List<TileCoord>(path.Count + 2) { depot.dockTile };
            tiles.AddRange(path);
            tiles.Add(store.dockTile);
            var route = RouteBuilder.Build(tiles, this);
            var truck = depot.FirstIdle();
            if (truck == null) return false;
            truck.route = route;
            truck.s = 0f;
            if (!Traffic.TryReserveChain(truck, 0))
            {
                truck.route = null;
                return false;
            }
            truck.target = store;
            store.assigned++;
            truck.State = TruckState.Departing;
            truck.timer = 0f;
            truck.lerpFrom = truck.position;
            truck.lerpTo = route.PositionAt(0f);
            truck.lerpFwdFrom = truck.forward;
            truck.lerpFwdTo = route.TangentAt(0f);
            truck.lerpDuration = truck.parkSlot < 3 ? 1.0f : 0.6f;
            return true;
        }

        internal void OnTruckLerpDone(TruckAgent t)
        {
            if (t.State == TruckState.Departing)
            {
                t.State = TruckState.Outbound;
                t.s = 0f;
                t.speed = 0f;
            }
            else if (t.State == TruckState.Parking)
            {
                Traffic.ReleaseAll(t);
                t.State = TruckState.Parked;
                t.route = null;
                t.target = null;
            }
        }

        internal void OnTruckArrived(TruckAgent t)
        {
            t.speed = 0f;
            if (t.State == TruckState.Outbound)
            {
                t.State = TruckState.Unloading;
                t.timer = 0f;
            }
            else if (t.State == TruckState.Returning)
            {
                t.State = TruckState.Parking;
                t.timer = 0f;
                t.lerpFrom = t.position;
                t.lerpFwdFrom = t.forward;
                ParkingPose(t.depot, t.parkSlot, out t.lerpTo, out t.lerpFwdTo);
                t.lerpDuration = 1.1f;
            }
        }

        void TickUnloading(float dt)
        {
            foreach (var t in Traffic.Trucks)
            {
                if (t.State != TruckState.Unloading) continue;
                float before = t.timer;
                t.timer += dt;
                var store = t.target;
                if (before < Config.unloadSeconds && t.timer >= Config.unloadSeconds)
                {
                    store.undelivered = Mathf.Max(0, store.undelivered - 1);
                    store.assigned = Mathf.Max(0, store.assigned - 1);
                    store.delivered++;
                    Score++;
                    Delivered?.Invoke(store, t);
                }
                if (t.timer < Config.unloadSeconds) continue;

                // Plan the trip home (retry until the road network allows it).
                var path = Pathfinder.FindPath(store.entrance, t.depot.entrance);
                if (path == null) continue;
                var tiles = new List<TileCoord>(path.Count + 2) { store.dockTile };
                tiles.AddRange(path);
                tiles.Add(t.depot.dockTile);
                var route = RouteBuilder.Build(tiles, this);
                Traffic.TransferHeld(t, route);
                t.s = 0f;
                t.speed = 0f;
                t.State = TruckState.Returning;
            }
        }

        // ───────────────────────────── Upgrades ─────────────────────────────

        public void AddScoreForTests(int n) => Score += n;

        /// <summary>Gives one extra truck to the depot whose cargo has the most waiting orders.</summary>
        public Depot GrantExtraTruck()
        {
            Depot best = null;
            float bestNeed = float.MinValue;
            foreach (var d in Depots)
            {
                float need = 0f;
                foreach (var s in Stores) if (s.cargo == d.cargo) need += s.Unassigned + s.overflow * 3f;
                need /= Mathf.Max(1, d.trucks.Count);
                if (need > bestNeed) { bestNeed = need; best = d; }
            }
            if (best != null) AddTruck(best);
            return best;
        }
    }
}
