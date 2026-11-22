using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>A* over road tiles. Costs are in meters of travel time-equivalent.</summary>
    public class RoadPathfinder
    {
        readonly GridMap map;
        readonly RoadNetwork roads;
        readonly float tileSize;
        readonly float highwayMult;

        readonly float[] g;
        readonly int[] parent;
        readonly int[] stamp;
        readonly bool[] closed;
        int run;
        readonly List<TileCoord> neighbors = new List<TileCoord>();
        readonly MinHeap open = new MinHeap();

        /// <summary>Optional congestion lookup: trucks currently on a tile.</summary>
        public System.Func<TileCoord, int> Occupancy;

        public RoadPathfinder(GridMap map, RoadNetwork roads, float highwayMult)
        {
            this.map = map;
            this.roads = roads;
            tileSize = map.TileSize;
            this.highwayMult = highwayMult;
            int n = map.Width * map.Height;
            g = new float[n];
            parent = new int[n];
            stamp = new int[n];
            closed = new bool[n];
        }

        int Idx(TileCoord t) => t.y * map.Width + t.x;
        TileCoord FromIdx(int i) => new TileCoord(i % map.Width, i / map.Width);

        float Heuristic(TileCoord a, TileCoord b) => a.Distance(b) * tileSize / highwayMult;

        float StepCost(TileCoord from, TileCoord to)
        {
            float cost;
            if (DirUtil.Between(from, to) != null) cost = tileSize;
            else cost = from.Distance(to) * tileSize / highwayMult + tileSize; // ramps
            if (roads.IsIntersection(to)) cost += 2.5f;
            if (Occupancy != null) cost += Occupancy(to) * 3f;
            return cost;
        }

        /// <summary>Returns the tile path start..goal inclusive, or null.</summary>
        public List<TileCoord> FindPath(TileCoord start, TileCoord goal)
        {
            if (!roads.IsRoad(start) || !roads.IsRoad(goal)) return null;
            if (roads.Component(start) != roads.Component(goal)) return null;
            run++;
            open.Clear();
            int si = Idx(start), gi = Idx(goal);
            Touch(si);
            g[si] = 0f;
            parent[si] = -1;
            open.Push(si, Heuristic(start, goal));

            while (open.Count > 0)
            {
                int ci = open.Pop();
                if (closed[ci]) continue;
                closed[ci] = true;
                if (ci == gi) return Reconstruct(gi);
                var c = FromIdx(ci);
                roads.Neighbors(c, neighbors);
                foreach (var nb in neighbors)
                {
                    if (!roads.IsRoad(nb)) continue;
                    int ni = Idx(nb);
                    Touch(ni);
                    if (closed[ni]) continue;
                    float ng = g[ci] + StepCost(c, nb);
                    if (ng < g[ni])
                    {
                        g[ni] = ng;
                        parent[ni] = ci;
                        open.Push(ni, ng + Heuristic(nb, goal));
                    }
                }
            }
            return null;
        }

        void Touch(int i)
        {
            if (stamp[i] == run) return;
            stamp[i] = run;
            g[i] = float.MaxValue;
            parent[i] = -1;
            closed[i] = false;
        }

        List<TileCoord> Reconstruct(int gi)
        {
            var path = new List<TileCoord>();
            for (int i = gi; i >= 0; i = parent[i]) path.Add(FromIdx(i));
            path.Reverse();
            return path;
        }

        class MinHeap
        {
            readonly List<int> items = new List<int>();
            readonly List<float> keys = new List<float>();
            public int Count => items.Count;
            public void Clear() { items.Clear(); keys.Clear(); }

            public void Push(int item, float key)
            {
                items.Add(item); keys.Add(key);
                int i = items.Count - 1;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (keys[p] <= keys[i]) break;
                    Swap(i, p); i = p;
                }
            }

            public int Pop()
            {
                int top = items[0];
                int last = items.Count - 1;
                items[0] = items[last]; keys[0] = keys[last];
                items.RemoveAt(last); keys.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < items.Count && keys[l] < keys[m]) m = l;
                    if (r < items.Count && keys[r] < keys[m]) m = r;
                    if (m == i) break;
                    Swap(i, m); i = m;
                }
                return top;
            }

            void Swap(int a, int b)
            {
                (items[a], items[b]) = (items[b], items[a]);
                (keys[a], keys[b]) = (keys[b], keys[a]);
            }
        }
    }
}
