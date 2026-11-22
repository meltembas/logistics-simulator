using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Logistics.Tests
{
    public class SimulationTests
    {
        GameConfig cfg;

        [SetUp]
        public void Setup()
        {
            cfg = GameConfig.CreateDefault();
            cfg.mapWidth = 20;
            cfg.mapHeight = 14;
        }

        [TearDown]
        public void Teardown() => Object.DestroyImmediate(cfg);

        static void DrawLine(World w, TileCoord from, TileCoord to)
        {
            TileCoord? last = null;
            var cur = from;
            while (true)
            {
                w.Editor.PaintStep(last, cur, out last);
                if (cur == to) break;
                if (cur.x != to.x) cur = new TileCoord(cur.x + (to.x > cur.x ? 1 : -1), cur.y);
                else cur = new TileCoord(cur.x, cur.y + (to.y > cur.y ? 1 : -1));
            }
        }

        [Test]
        public void ConnectAndRemoveKeepsMasksSymmetric()
        {
            var w = World.CreateEmpty(cfg);
            w.SpawningEnabled = false;
            DrawLine(w, new TileCoord(2, 2), new TileCoord(5, 2));
            Assert.IsTrue(w.Roads.HasConnection(new TileCoord(3, 2), Dir.E));
            Assert.IsTrue(w.Roads.HasConnection(new TileCoord(4, 2), Dir.W));
            Assert.AreEqual(EditResult.Ok, w.Editor.Erase(new TileCoord(4, 2)));
            Assert.IsFalse(w.Roads.HasConnection(new TileCoord(3, 2), Dir.E));
            Assert.IsFalse(w.Roads.HasConnection(new TileCoord(5, 2), Dir.W));
            Assert.AreNotEqual(w.Roads.Component(new TileCoord(3, 2)), w.Roads.Component(new TileCoord(5, 2)));
        }

        [Test]
        public void RoadBudgetIsSpentAndRefunded()
        {
            var w = World.CreateEmpty(cfg);
            w.SpawningEnabled = false;
            int before = w.Inventory.Roads;
            DrawLine(w, new TileCoord(1, 1), new TileCoord(1, 5));
            Assert.AreEqual(before - 5, w.Inventory.Roads);
            w.Editor.Erase(new TileCoord(1, 3));
            Assert.AreEqual(before - 4, w.Inventory.Roads);
        }

        [Test]
        public void PathfinderFollowsRoadsAndFailsWhenDisconnected()
        {
            var w = World.CreateEmpty(cfg);
            w.SpawningEnabled = false;
            DrawLine(w, new TileCoord(2, 2), new TileCoord(8, 2));
            DrawLine(w, new TileCoord(8, 2), new TileCoord(8, 7));
            var path = w.Pathfinder.FindPath(new TileCoord(2, 2), new TileCoord(8, 7));
            Assert.IsNotNull(path);
            Assert.AreEqual(12, path.Count);
            w.Editor.Erase(new TileCoord(8, 4));
            Assert.IsNull(w.Pathfinder.FindPath(new TileCoord(2, 2), new TileCoord(8, 7)));
        }

        [Test]
        public void BridgeCrossesWaterAndConsumesBridgeItem()
        {
            var w = World.CreateEmpty(cfg);
            w.SpawningEnabled = false;
            for (int y = 0; y < w.Grid.Height; y++)
            {
                w.Grid.SetTerrain(new TileCoord(10, y), TerrainKind.Water);
                w.Grid.SetTerrain(new TileCoord(11, y), TerrainKind.Water);
            }
            w.Inventory.Bridges = 1;
            DrawLine(w, new TileCoord(7, 5), new TileCoord(13, 5));
            Assert.AreEqual(0, w.Inventory.Bridges);
            Assert.IsTrue(w.Roads.IsBridge(new TileCoord(10, 5)));
            Assert.IsTrue(w.Roads.IsBridge(new TileCoord(11, 5)));
            Assert.IsNotNull(w.Pathfinder.FindPath(new TileCoord(7, 5), new TileCoord(13, 5)));
        }

        [Test]
        public void RouteStartsAndEndsInsideDockTiles()
        {
            var w = World.CreateEmpty(cfg);
            w.SpawningEnabled = false;
            var depot = w.AddDepot(CargoType.Food, new TileCoord(2, 2), Dir.N);
            var store = w.AddStore(CargoType.Food, new TileCoord(9, 6), Dir.S);
            DrawLine(w, depot.entrance, new TileCoord(store.entrance.x, depot.entrance.y));
            DrawLine(w, new TileCoord(store.entrance.x, depot.entrance.y), store.entrance);
            var path = w.Pathfinder.FindPath(depot.entrance, store.entrance);
            Assert.IsNotNull(path);
            var tiles = new List<TileCoord> { depot.dockTile };
            tiles.AddRange(path);
            tiles.Add(store.dockTile);
            var route = RouteBuilder.Build(tiles, w);
            Assert.Greater(route.Length, 10f);
            Assert.AreEqual(depot.dockTile, w.Grid.WorldToTile(route.PositionAt(0f)));
            Assert.AreEqual(store.dockTile, w.Grid.WorldToTile(route.PositionAt(route.Length)));
        }

        [Test]
        public void TrucksDeliverOrdersOverConnectedRoads()
        {
            var w = World.CreateEmpty(cfg);
            w.SpawningEnabled = false;
            // Pin the demand and fleet so the test checks movement/delivery, not game balance.
            cfg.orderIntervalStart = 8f;
            cfg.orderIntervalMin = 8f;
            var depot = w.AddDepot(CargoType.Food, new TileCoord(2, 2), Dir.N);
            w.AddTruck(depot);
            w.AddTruck(depot);
            var store = w.AddStore(CargoType.Food, new TileCoord(10, 7), Dir.W);
            DrawLine(w, depot.entrance, new TileCoord(depot.entrance.x, store.entrance.y));
            DrawLine(w, new TileCoord(depot.entrance.x, store.entrance.y), store.entrance);
            Assert.AreEqual(w.Roads.Component(depot.entrance), w.Roads.Component(store.entrance));

            for (int i = 0; i < 90 * 30; i++) w.Tick(1f / 30f);
            Assert.Greater(w.Score, 3, "trucks should complete several deliveries in 90 s");
            Assert.IsFalse(w.IsGameOver);
        }

        [Test]
        public void DisconnectedStoreOverflowsAndEndsGame()
        {
            var w = World.CreateEmpty(cfg);
            w.SpawningEnabled = false;
            w.AddDepot(CargoType.Food, new TileCoord(2, 2), Dir.N);
            w.AddStore(CargoType.Food, new TileCoord(12, 8), Dir.W);
            bool over = false;
            w.GameOver += _ => over = true;
            for (int i = 0; i < 400 * 10 && !over; i++) w.Tick(0.1f);
            Assert.IsTrue(over);
            Assert.IsTrue(w.IsGameOver);
        }

        [Test]
        public void TwoStoresWithManyTrucksDoNotGridlock()
        {
            var w = World.CreateEmpty(cfg);
            w.SpawningEnabled = false;
            var depot = w.AddDepot(CargoType.Food, new TileCoord(8, 2), Dir.N);
            for (int i = 0; i < 3; i++) w.AddTruck(depot);
            var s1 = w.AddStore(CargoType.Food, new TileCoord(3, 10), Dir.E);
            var s2 = w.AddStore(CargoType.Food, new TileCoord(15, 10), Dir.W);
            // a T junction shared by both directions
            DrawLine(w, depot.entrance, new TileCoord(depot.entrance.x, 10));
            DrawLine(w, new TileCoord(depot.entrance.x, 10), s1.entrance);
            DrawLine(w, new TileCoord(depot.entrance.x, 10), s2.entrance);
            int lastScore = 0;
            for (int minute = 0; minute < 3; minute++)
            {
                for (int i = 0; i < 60 * 30; i++) w.Tick(1f / 30f);
                Assert.Greater(w.Score, lastScore, $"deliveries must keep flowing (minute {minute})");
                lastScore = w.Score;
            }
        }
    }
}
