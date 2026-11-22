using System.Collections.Generic;

namespace Logistics
{
    public enum EditResult
    {
        Ok, NoChange, OutOfBounds, Blocked, NoRoads, NoBridges, BridgeTooLong, NoRoundabouts, NoHighways,
        InUse, Fixed, NotRoad, TooFar, TooShort, Exists, Entrance
    }

    /// <summary>Player-facing construction rules on top of <see cref="RoadNetwork"/> (budget, bridges, refunds).</summary>
    public class RoadEditor
    {
        readonly World w;
        public event System.Action<TileCoord> RoadPlaced;
        public event System.Action<TileCoord> RoadRemoved;

        public RoadEditor(World world) { w = world; }

        GridMap Grid => w.Grid;
        RoadNetwork Roads => w.Roads;
        Inventory Inv => w.Inventory;

        public bool CanPlaceRoad(TileCoord t) => Roads.IsRoad(t) || (Grid.IsBuildableGround(t) && Inv.Roads > 0);

        /// <summary>
        /// One step of a drag: extend the road from <paramref name="last"/> (may be null) to <paramref name="t"/>.
        /// Returns the new chain end (null when the chain breaks).
        /// </summary>
        public EditResult PaintStep(TileCoord? last, TileCoord t, out TileCoord? newLast)
        {
            newLast = last;
            if (last.HasValue && last.Value == t) return EditResult.NoChange;
            if (!Grid.InActive(t)) { newLast = null; return EditResult.OutOfBounds; }
            if (last.HasValue && DirUtil.Between(last.Value, t) == null) { newLast = null; return EditResult.TooFar; }
            if (last.HasValue && Roads.IsBridge(last.Value) && !Roads.IsBridge(t) && !Grid.IsWater(t))
            {
                var axis = Roads.Bridges[Roads.BridgeIdAt(last.Value)].axis;
                var d = DirUtil.Between(last.Value, t).Value;
                if (d != axis && d != axis.Opposite()) { newLast = null; return EditResult.Blocked; }
            }

            if (Roads.IsRoad(t))
            {
                if (Roads.IsBridge(t) && last.HasValue && !Roads.IsBridge(last.Value))
                {
                    // Joining onto a bridge only along its axis.
                    var span = Roads.Bridges[Roads.BridgeIdAt(t)];
                    var dir = DirUtil.Between(last.Value, t).Value;
                    if (dir != span.axis && dir != span.axis.Opposite()) { newLast = t; return EditResult.Blocked; }
                }
                if (last.HasValue && Roads.IsRoad(last.Value)) Roads.Connect(last.Value, t);
                newLast = t;
                return EditResult.Ok;
            }

            if (Grid.IsWater(t))
            {
                if (!last.HasValue || !Roads.IsRoad(last.Value)) { newLast = null; return EditResult.Blocked; }
                return TryBridge(last.Value, t, out newLast);
            }

            if (!Grid.IsBuildableGround(t)) { newLast = null; return EditResult.Blocked; }
            if (!Inv.TrySpend(ItemKind.Road)) { newLast = null; return EditResult.NoRoads; }
            Roads.AddRoad(t);
            if (last.HasValue && Roads.IsRoad(last.Value)) Roads.Connect(last.Value, t);
            RoadPlaced?.Invoke(t);
            newLast = t;
            return EditResult.Ok;
        }

        EditResult TryBridge(TileCoord bank, TileCoord firstWater, out TileCoord? newLast)
        {
            newLast = null;
            var dir = DirUtil.Between(bank, firstWater).Value;
            var span = new List<TileCoord>();
            var cur = firstWater;
            while (Grid.InMap(cur) && Grid.IsWater(cur))
            {
                if (Roads.IsRoad(cur) || !Grid.InActive(cur)) return EditResult.Blocked;
                span.Add(cur);
                if (span.Count > w.Config.maxBridgeSpan) return EditResult.BridgeTooLong;
                cur = cur.Step(dir);
            }
            var far = cur;
            if (!Grid.InActive(far)) return EditResult.OutOfBounds;
            bool farIsRoad = Roads.IsRoad(far);
            if (!farIsRoad && !Grid.IsBuildableGround(far)) return EditResult.Blocked;
            if (Inv.Bridges <= 0) return EditResult.NoBridges;
            if (!farIsRoad && Inv.Roads <= 0) return EditResult.NoRoads;

            Inv.TrySpend(ItemKind.Bridge);
            Roads.CreateBridge(span, dir);
            if (!farIsRoad)
            {
                Inv.TrySpend(ItemKind.Road);
                Roads.AddRoad(far);
            }
            var prev = bank;
            foreach (var b in span) { Roads.Connect(prev, b); prev = b; }
            Roads.Connect(prev, far);
            foreach (var b in span) RoadPlaced?.Invoke(b);
            RoadPlaced?.Invoke(far);
            newLast = far;
            return EditResult.Ok;
        }

        public EditResult Erase(TileCoord t)
        {
            if (!Roads.IsRoad(t)) return EditResult.NotRoad;
            if (Roads.IsFixed(t)) return EditResult.Fixed;

            int bridgeId = Roads.BridgeIdAt(t);
            if (bridgeId != 0 && Roads.Bridges.TryGetValue(bridgeId, out var span))
            {
                foreach (var b in span.tiles) if (w.Traffic.IsTileInUse(b)) return EditResult.InUse;
                foreach (var b in span.tiles) RemoveTile(b, refundRoad: false);
                Roads.RemoveBridgeRecord(bridgeId);
                Inv.Add(ItemKind.Bridge, 1);
                return EditResult.Ok;
            }

            if (w.Traffic.IsTileInUse(t)) return EditResult.InUse;
            RemoveTile(t, refundRoad: true);
            return EditResult.Ok;
        }

        void RemoveTile(TileCoord t, bool refundRoad)
        {
            int highways = 0;
            foreach (var _ in Roads.HighwaysAt(t)) highways++;
            if (highways > 0) Inv.Add(ItemKind.Highway, highways);
            if (Roads.IsRoundabout(t)) Inv.Add(ItemKind.Roundabout, 1);
            Roads.RemoveRoad(t);
            if (refundRoad) Inv.Add(ItemKind.Road, 1);
            RoadRemoved?.Invoke(t);
        }

        public EditResult PlaceRoundabout(TileCoord t)
        {
            if (!Roads.IsRoad(t) || Roads.IsBridge(t)) return EditResult.NotRoad;
            if (Roads.IsRoundabout(t)) return EditResult.Exists;
            if (Roads.BuildingSide(t).HasValue) return EditResult.Entrance;
            if (Inv.Roundabouts <= 0) return EditResult.NoRoundabouts;
            // Busy junctions are allowed (that is where a roundabout helps most): trucks already on
            // their way keep the lane curve they were given; routes planned from now on circle the island.
            Inv.TrySpend(ItemKind.Roundabout);
            Roads.SetRoundabout(t, true);
            RoadPlaced?.Invoke(t);
            return EditResult.Ok;
        }

        public EditResult CanPlaceHighway(TileCoord a, TileCoord b)
        {
            if (!Roads.IsRoad(a) || !Roads.IsRoad(b) || Roads.IsBridge(a) || Roads.IsBridge(b)) return EditResult.NotRoad;
            if (a == b) return EditResult.NoChange;
            float dist = a.Distance(b);
            if (dist < 3f) return EditResult.TooShort;
            if (dist > w.Config.maxHighwayLength) return EditResult.TooFar;
            if (Roads.HighwayBetween(a, b) != null) return EditResult.Exists;
            if (Inv.Highways <= 0) return EditResult.NoHighways;
            return EditResult.Ok;
        }

        public EditResult PlaceHighway(TileCoord a, TileCoord b)
        {
            var r = CanPlaceHighway(a, b);
            if (r != EditResult.Ok) return r;
            Inv.TrySpend(ItemKind.Highway);
            Roads.AddHighway(a, b);
            RoadPlaced?.Invoke(a);
            RoadPlaced?.Invoke(b);
            return EditResult.Ok;
        }

        public static string Describe(EditResult r)
        {
            switch (r)
            {
                case EditResult.NoRoads: return "Yol karosu kalmadı";
                case EditResult.NoBridges: return "Köprü yok — haftalık yükseltmeden alabilirsin";
                case EditResult.BridgeTooLong: return "Köprü bu kadar uzun olamaz";
                case EditResult.NoRoundabouts: return "Göbek kavşak kalmadı — haftalık yükseltme kartından alabilirsin";
                case EditResult.Entrance: return "Bina girişine göbek konamaz, bir sonraki kavşağı dene";
                case EditResult.Exists: return "Burada zaten göbek var";
                case EditResult.NoHighways: return "Otoyol yok";
                case EditResult.InUse: return "Bu yolda kamyon var";
                case EditResult.Fixed: return "Bina girişleri silinemez";
                case EditResult.OutOfBounds: return "Bu bölge henüz açılmadı";
                case EditResult.TooFar: return "Otoyol çok uzun";
                case EditResult.TooShort: return "Otoyol çok kısa";
                case EditResult.NotRoad: return "Önce yol döşemelisin";
                case EditResult.Blocked: return "Buraya yol yapılamaz";
                default: return null;
            }
        }
    }
}
