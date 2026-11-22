#if UNITY_EDITOR || DEBUG
using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>
    /// Verification helper (Editor / development builds only): lays roads from every store to a depot of
    /// its cargo, auto-picks weekly upgrades, and logs progress — so a play session can be driven and
    /// checked without a human at the mouse.
    /// </summary>
    public class DebugAutoplay : MonoBehaviour
    {
        public bool connectStores = true;
        public bool autoUpgrade = true;
        public float logInterval = 10f;
        float logTimer;
        readonly HashSet<Store> connected = new HashSet<Store>();

        public static DebugAutoplay Enable(int speed = 2)
        {
            var gm = GameManager.Instance;
            var a = gm.GetComponent<DebugAutoplay>() ?? gm.gameObject.AddComponent<DebugAutoplay>();
            if (gm.State == GameState.Menu) gm.StartGame();
            gm.SetSpeed(speed);
            return a;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.World == null) return;
            var w = gm.World;
            if (gm.State == GameState.Upgrade && autoUpgrade)
            {
                gm.SendMessage("ChooseUpgrade", 0, SendMessageOptions.DontRequireReceiver);
            }
            if (gm.State != GameState.Playing) return;
            if (connectStores)
                foreach (var s in w.Stores)
                    if (!connected.Contains(s) && ConnectStore(w, s)) connected.Add(s);

            logTimer -= Time.deltaTime;
            if (logTimer <= 0f)
            {
                logTimer = logInterval;
                int moving = 0;
                foreach (var t in w.Traffic.Trucks) if (t.OnRoad) moving++;
                Debug.Log($"[Autoplay] week={w.Week} score={w.Score} stores={w.Stores.Count} depots={w.Depots.Count} " +
                          $"trucks={w.Traffic.Trucks.Count} moving={moving} roads={w.Inventory.Roads} gameOver={w.IsGameOver}");
            }
        }

        /// <summary>Builds a bridge over the river, a roundabout and a highway near the first depot (visual checks).</summary>
        public static string Showcase(World w)
        {
            var g = w.Grid;
            g.ActiveBounds = new RectInt(0, 0, g.Width, g.Height);
            w.Inventory.Add(ItemKind.Bridge, 2);
            w.Inventory.Add(ItemKind.Roundabout, 2);
            w.Inventory.Add(ItemKind.Highway, 2);
            w.Inventory.Add(ItemKind.Road, 60);
            var depot = w.Depots[0];
            string log = "";
            // Bridge: walk east from the depot entrance row to the river and across.
            int row = depot.entrance.y;
            for (int dy = 0; dy < 6; dy++)
            {
                int y = row + (dy % 2 == 0 ? dy / 2 : -(dy / 2 + 1));
                int riverX = -1;
                for (int x = depot.entrance.x; x < g.Width; x++) if (g.IsWater(new TileCoord(x, y))) { riverX = x; break; }
                if (riverX < 0) continue;
                TileCoord? last = null;
                int ok = 0;
                for (int x = riverX - 3; x <= Mathf.Min(g.Width - 1, riverX + 6); x++)
                {
                    var r = w.Editor.PaintStep(last, new TileCoord(x, y), out last);
                    if (r == EditResult.Ok) ok++;
                    if (last == null) break;
                }
                log += $"bridge row {y} at x={riverX} ok={ok}; ";
                if (w.Roads.Bridges.Count > 0) break;
            }
            // Roundabout on the first intersection we find.
            for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
            {
                var t = new TileCoord(x, y);
                if (w.Roads.IsIntersection(t) && !w.Roads.BuildingSide(t).HasValue && !w.Roads.IsBridge(t) && w.Inventory.Roundabouts > 1)
                {
                    log += $"roundabout {t} -> {w.Editor.PlaceRoundabout(t)}; ";
                    x = g.Width; y = g.Height;
                }
            }
            // Highway between two distant road tiles (5–10 apart).
            var roads = new List<TileCoord>();
            for (int y = 0; y < g.Height; y++)
            for (int x = 0; x < g.Width; x++)
                if (w.Roads.IsRoad(new TileCoord(x, y)) && !w.Roads.IsBridge(new TileCoord(x, y))) roads.Add(new TileCoord(x, y));
            foreach (var a in roads)
            {
                foreach (var b in roads)
                {
                    float d = a.Distance(b);
                    if (d >= 6f && d <= 9f && w.Roads.Component(a) == w.Roads.Component(b) && a.x != b.x && a.y != b.y)
                    {
                        log += $"highway {a}->{b} {w.Editor.PlaceHighway(a, b)}";
                        return log;
                    }
                }
            }
            return log;
        }

        /// <summary>BFS over buildable/road tiles from the store entrance to the nearest same-cargo depot entrance; paints the path.</summary>
        public static bool ConnectStore(World w, Store s)
        {
            Depot target = null;
            int best = int.MaxValue;
            foreach (var d in w.Depots)
                if (d.cargo == s.cargo && d.entrance.Manhattan(s.entrance) < best) { best = d.entrance.Manhattan(s.entrance); target = d; }
            if (target == null) return false;
            if (w.Roads.Component(target.entrance) == w.Roads.Component(s.entrance) && w.Roads.Component(s.entrance) != 0) return true;

            var prev = new Dictionary<TileCoord, TileCoord>();
            var q = new Queue<TileCoord>();
            q.Enqueue(s.entrance);
            prev[s.entrance] = s.entrance;
            bool found = false;
            while (q.Count > 0 && !found)
            {
                var c = q.Dequeue();
                foreach (var d in DirUtil.All)
                {
                    var n = c.Step(d);
                    if (prev.ContainsKey(n) || !w.Grid.InActive(n)) continue;
                    bool ok = n == target.entrance || (w.Roads.IsRoad(n) && !w.Roads.IsFixed(n)) || w.Grid.IsBuildableGround(n);
                    if (!ok) continue;
                    prev[n] = c;
                    if (n == target.entrance) { found = true; break; }
                    q.Enqueue(n);
                }
            }
            if (!found) return false;
            var path = new List<TileCoord>();
            for (var c = target.entrance; c != s.entrance; c = prev[c]) path.Add(c);
            path.Add(s.entrance);
            path.Reverse();
            TileCoord? last = null;
            foreach (var t in path)
            {
                var r = w.Editor.PaintStep(last, t, out last);
                if (r != EditResult.Ok && r != EditResult.NoChange) return false;
            }
            return true;
        }
    }
}
#endif
