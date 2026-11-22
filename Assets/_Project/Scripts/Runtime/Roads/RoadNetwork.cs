using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    public class Highway
    {
        public int id;
        public TileCoord a;
        public TileCoord b;
        public float Length(float tileSize) => a.Distance(b) * tileSize;
    }

    public class BridgeSpan
    {
        public int id;
        public readonly List<TileCoord> tiles = new List<TileCoord>();
        public Dir axis;
    }

    /// <summary>
    /// Road graph on the tile grid. Each road tile stores a 4-bit connection mask; entrance tiles also
    /// remember which side their building is on. Highways are extra long edges between two road tiles.
    /// </summary>
    public class RoadNetwork
    {
        readonly GridMap map;
        readonly bool[] road;
        readonly byte[] mask;
        readonly sbyte[] buildingSide;
        readonly bool[] fixedTile;
        readonly bool[] roundabout;
        readonly int[] bridgeId;

        public readonly List<Highway> Highways = new List<Highway>();
        public readonly Dictionary<int, BridgeSpan> Bridges = new Dictionary<int, BridgeSpan>();
        int nextHighwayId = 1;
        int nextBridgeId = 1;

        public int Version { get; private set; }
        /// <summary>Tiles whose road visuals need rebuilding (consumed by the road view).</summary>
        public readonly HashSet<TileCoord> DirtyTiles = new HashSet<TileCoord>();
        int componentsVersion = -1;
        readonly int[] component;

        public RoadNetwork(GridMap map)
        {
            this.map = map;
            int n = map.Width * map.Height;
            road = new bool[n];
            mask = new byte[n];
            buildingSide = new sbyte[n];
            fixedTile = new bool[n];
            roundabout = new bool[n];
            bridgeId = new int[n];
            component = new int[n];
            for (int i = 0; i < n; i++) { buildingSide[i] = -1; bridgeId[i] = 0; }
        }

        int Idx(TileCoord t) => t.y * map.Width + t.x;

        void MarkDirty(TileCoord t)
        {
            DirtyTiles.Add(t);
            foreach (var d in DirUtil.All) DirtyTiles.Add(t.Step(d));
        }

        public bool IsRoad(TileCoord t) => map.InMap(t) && road[Idx(t)];
        public int Mask(TileCoord t) => IsRoad(t) ? mask[Idx(t)] : 0;
        public bool IsFixed(TileCoord t) => IsRoad(t) && fixedTile[Idx(t)];
        public bool IsRoundabout(TileCoord t) => IsRoad(t) && roundabout[Idx(t)];
        public bool IsBridge(TileCoord t) => IsRoad(t) && bridgeId[Idx(t)] != 0;
        public int BridgeIdAt(TileCoord t) => IsRoad(t) ? bridgeId[Idx(t)] : 0;

        public Dir? BuildingSide(TileCoord t)
        {
            if (!IsRoad(t)) return null;
            int s = buildingSide[Idx(t)];
            return s < 0 ? (Dir?)null : (Dir)s;
        }

        public bool HasConnection(TileCoord t, Dir d) => (Mask(t) & d.Bit()) != 0;

        /// <summary>Roads + building side + highways touching this tile.</summary>
        public int ConnectionCount(TileCoord t)
        {
            if (!IsRoad(t)) return 0;
            int c = DirUtil.PopCount(mask[Idx(t)]);
            if (buildingSide[Idx(t)] >= 0) c++;
            foreach (var h in Highways) if (h.a == t || h.b == t) c++;
            return c;
        }

        public bool IsIntersection(TileCoord t) => ConnectionCount(t) >= 3;

        public void AddRoad(TileCoord t, bool isFixed = false, int bridge = 0)
        {
            int i = Idx(t);
            road[i] = true;
            fixedTile[i] = isFixed;
            bridgeId[i] = bridge;
            map.SetOccupant(t, Occupant.Road);
            MarkDirty(t);
            Version++;
        }

        public void SetBuildingSide(TileCoord t, Dir side)
        {
            buildingSide[Idx(t)] = (sbyte)side;
            MarkDirty(t);
            Version++;
        }

        public bool Connect(TileCoord a, TileCoord b)
        {
            var d = DirUtil.Between(a, b);
            if (d == null || !IsRoad(a) || !IsRoad(b)) return false;
            if (HasConnection(a, d.Value)) return false;
            mask[Idx(a)] |= (byte)d.Value.Bit();
            mask[Idx(b)] |= (byte)d.Value.Opposite().Bit();
            MarkDirty(a);
            MarkDirty(b);
            Version++;
            return true;
        }

        public void SetRoundabout(TileCoord t, bool v)
        {
            if (!IsRoad(t)) return;
            roundabout[Idx(t)] = v;
            MarkDirty(t);
            Version++;
        }

        public BridgeSpan CreateBridge(List<TileCoord> tiles, Dir axis)
        {
            var span = new BridgeSpan { id = nextBridgeId++, axis = axis };
            span.tiles.AddRange(tiles);
            Bridges[span.id] = span;
            foreach (var t in tiles) AddRoad(t, false, span.id);
            return span;
        }

        public Highway AddHighway(TileCoord a, TileCoord b)
        {
            var h = new Highway { id = nextHighwayId++, a = a, b = b };
            Highways.Add(h);
            Version++;
            return h;
        }

        public void RemoveHighway(Highway h)
        {
            Highways.Remove(h);
            Version++;
        }

        public IEnumerable<Highway> HighwaysAt(TileCoord t)
        {
            foreach (var h in Highways) if (h.a == t || h.b == t) yield return h;
        }

        public Highway HighwayBetween(TileCoord a, TileCoord b)
        {
            foreach (var h in Highways)
                if ((h.a == a && h.b == b) || (h.a == b && h.b == a)) return h;
            return null;
        }

        /// <summary>Removes a road tile and all its links. Caller handles refunds of highways/roundabouts.</summary>
        public void RemoveRoad(TileCoord t)
        {
            if (!IsRoad(t)) return;
            int i = Idx(t);
            foreach (var d in DirUtil.All)
            {
                if ((mask[i] & d.Bit()) == 0) continue;
                var n = t.Step(d);
                if (map.InMap(n)) mask[Idx(n)] &= (byte)~d.Opposite().Bit();
            }
            Highways.RemoveAll(h => h.a == t || h.b == t);
            road[i] = false;
            mask[i] = 0;
            buildingSide[i] = -1;
            fixedTile[i] = false;
            roundabout[i] = false;
            bridgeId[i] = 0;
            map.SetOccupant(t, Occupant.None);
            MarkDirty(t);
            Version++;
        }

        public void RemoveBridgeRecord(int id) { Bridges.Remove(id); Version++; }

        /// <summary>Neighbors reachable from a road tile (adjacent roads and highway ends).</summary>
        public void Neighbors(TileCoord t, List<TileCoord> result)
        {
            result.Clear();
            if (!IsRoad(t)) return;
            int m = mask[Idx(t)];
            foreach (var d in DirUtil.All)
                if ((m & d.Bit()) != 0) result.Add(t.Step(d));
            foreach (var h in Highways)
            {
                if (h.a == t) result.Add(h.b);
                else if (h.b == t) result.Add(h.a);
            }
        }

        /// <summary>Connected-component id for a road tile (0 = not a road). Cached per version.</summary>
        public int Component(TileCoord t)
        {
            if (!IsRoad(t)) return 0;
            if (componentsVersion != Version) RebuildComponents();
            return component[Idx(t)];
        }

        readonly List<TileCoord> scratch = new List<TileCoord>();
        readonly Queue<TileCoord> queue = new Queue<TileCoord>();

        void RebuildComponents()
        {
            System.Array.Clear(component, 0, component.Length);
            int next = 1;
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var s = new TileCoord(x, y);
                int si = Idx(s);
                if (!road[si] || component[si] != 0) continue;
                component[si] = next;
                queue.Enqueue(s);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    Neighbors(c, scratch);
                    foreach (var n in scratch)
                    {
                        if (!IsRoad(n)) continue;
                        int ni = Idx(n);
                        if (component[ni] != 0) continue;
                        component[ni] = next;
                        queue.Enqueue(n);
                    }
                }
                next++;
            }
            componentsVersion = Version;
        }
    }
}
