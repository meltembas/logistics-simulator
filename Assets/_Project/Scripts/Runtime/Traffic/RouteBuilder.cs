using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>
    /// Turns a tile path (origin dock → roads → destination dock) into right-hand-traffic lane geometry.
    /// Dock tiles are driven as half U-turns: the truck finishes a route stopped deep in the destination
    /// lot and starts the next one from the same spot.
    /// </summary>
    public static class RouteBuilder
    {
        public const float HighwayHeight = 5.5f;
        const float RoundaboutRadius = 2.6f;
        const int BezierSteps = 10;

        public static Vector3 Right(Vector3 d) => new Vector3(d.z, 0f, -d.x);

        struct Port
        {
            public bool isSide;
            public Dir side;
            public Vector3 dir; // travel direction through this port
        }

        public static Route Build(List<TileCoord> tiles, World w)
        {
            var route = new Route();
            route.tiles.AddRange(tiles);
            int n = tiles.Count;
            if (n < 2) return route;
            var cfg = w.Config;
            float h = w.Grid.TileSize * 0.5f;
            float lo = cfg.laneOffset;

            for (int i = 0; i < n; i++)
            {
                var t = tiles[i];
                var c = w.Grid.TileCenter(t);
                float sStart = route.Length;
                float speed = 1f;
                int capacity = 0;
                bool isDock = i == 0 || i == n - 1;

                if (isDock)
                {
                    var other = i == 0 ? tiles[1] : tiles[n - 2];
                    var side = DirUtil.Between(t, other) ?? Dir.N;
                    var dIn = -side.Vec();
                    var p0 = c + side.Vec() * h + Right(dIn) * lo;
                    var p3 = c + side.Vec() * h + Right(side.Vec()) * lo;
                    var p1 = p0 + dIn * h * 0.9f;
                    var p2 = p3 + dIn * h * 0.9f;
                    if (i == 0) EmitBezier(route, p0, p1, p2, p3, 0.5f, 1f);
                    else EmitBezier(route, p0, p1, p2, p3, 0f, 0.5f);
                    speed = 0.42f;
                    capacity = 1;
                }
                else
                {
                    var entry = MakeEntryPort(tiles[i - 1], t);
                    var exit = MakeExitPort(t, tiles[i + 1]);
                    bool roundabout = w.Roads.IsRoundabout(t) && entry.isSide && exit.isSide;
                    if (roundabout)
                    {
                        EmitRoundabout(route, c, h, lo, entry.side, exit.side);
                        speed = 0.6f;
                        capacity = 3;
                    }
                    else
                    {
                        var p0 = entry.isSide ? c + entry.side.Vec() * h + Right(entry.dir) * lo : c + Right(entry.dir) * lo;
                        var p3 = exit.isSide ? c + exit.side.Vec() * h + Right(exit.dir) * lo : c + Right(exit.dir) * lo;
                        bool straight = Vector3.Dot(entry.dir, exit.dir) > 0.99f;
                        bool uTurn = Vector3.Dot(entry.dir, exit.dir) < -0.99f;
                        float k1 = entry.isSide ? h * (uTurn ? 1.3f : 0.55f) : h * 0.3f;
                        float k2 = exit.isSide ? h * (uTurn ? 1.3f : 0.55f) : h * 0.3f;
                        EmitBezier(route, p0, p0 + entry.dir * k1, p3 - exit.dir * k2, p3, 0f, 1f);
                        speed = straight ? 1f : uTurn ? 0.4f : cfg.cornerSpeedFactor;
                        if (!entry.isSide || !exit.isSide) speed = Mathf.Min(speed, 0.8f);
                        capacity = w.Roads.IsIntersection(t) ? 1 : 0;
                    }
                }

                route.spans.Add(new RouteSpan
                {
                    tile = t, sStart = sStart, sEnd = route.Length, speedFactor = speed, capacity = capacity
                });

                // Highway link to the next tile
                if (i < n - 1 && DirUtil.Between(t, tiles[i + 1]) == null)
                {
                    var next = tiles[i + 1];
                    var a = w.Grid.TileCenter(t);
                    var b = w.Grid.TileCenter(next);
                    var dir = (b - a).normalized;
                    var r = Right(dir) * lo;
                    HighwayControls(a + r, b + r, out var q1, out var q2);
                    float hs = route.Length;
                    EmitBezier(route, a + r, q1, q2, b + r, 0f, 1f, 24);
                    route.spans.Add(new RouteSpan
                    {
                        tile = t, sStart = hs, sEnd = route.Length, speedFactor = cfg.highwaySpeedMultiplier,
                        highway = true, capacity = 0
                    });
                }
            }
            return route;
        }

        public static void HighwayControls(Vector3 a, Vector3 b, out Vector3 p1, out Vector3 p2)
        {
            var d = b - a;
            float len = d.magnitude;
            var dir = d / Mathf.Max(0.001f, len);
            var up = Vector3.up * HighwayHeight * 1.33f;
            p1 = a + dir * len * 0.22f + up;
            p2 = b - dir * len * 0.22f + up;
        }

        static Port MakeEntryPort(TileCoord prev, TileCoord t)
        {
            var side = DirUtil.Between(t, prev);
            if (side.HasValue) return new Port { isSide = true, side = side.Value, dir = -side.Value.Vec() };
            return new Port { isSide = false, dir = Flat(t, prev, true) };
        }

        static Port MakeExitPort(TileCoord t, TileCoord next)
        {
            var side = DirUtil.Between(t, next);
            if (side.HasValue) return new Port { isSide = true, side = side.Value, dir = side.Value.Vec() };
            return new Port { isSide = false, dir = Flat(t, next, false) };
        }

        static Vector3 Flat(TileCoord a, TileCoord b, bool incoming)
        {
            var d = new Vector3(b.x - a.x, 0f, b.y - a.y).normalized;
            return incoming ? -d : d;
        }

        public static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float u = 1f - t;
            return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
        }

        static void EmitBezier(Route r, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t0, float t1, int steps = BezierSteps)
        {
            for (int k = 0; k <= steps; k++)
            {
                float t = Mathf.Lerp(t0, t1, k / (float)steps);
                r.AddPoint(Bezier(p0, p1, p2, p3, t));
            }
        }

        static float SideAngle(Dir d)
        {
            switch (d)
            {
                case Dir.E: return 0f;
                case Dir.N: return 90f;
                case Dir.W: return 180f;
                default: return 270f;
            }
        }

        static void EmitRoundabout(Route r, Vector3 c, float h, float lo, Dir sIn, Dir sOut)
        {
            var dIn = -sIn.Vec();
            var dOut = sOut.Vec();
            var entry = c + sIn.Vec() * h + Right(dIn) * lo;
            var exit = c + sOut.Vec() * h + Right(dOut) * lo;
            float a0 = SideAngle(sIn) + 28f;
            float a1 = SideAngle(sOut) - 28f;
            while (a1 <= a0 + 1f) a1 += 360f;
            r.AddPoint(entry);
            var join = c + new Vector3(Mathf.Cos(a0 * Mathf.Deg2Rad), 0f, Mathf.Sin(a0 * Mathf.Deg2Rad)) * RoundaboutRadius;
            r.AddPoint(Vector3.Lerp(entry, join, 0.55f));
            int steps = Mathf.Max(3, Mathf.CeilToInt((a1 - a0) / 15f));
            for (int k = 0; k <= steps; k++)
            {
                float a = Mathf.Lerp(a0, a1, k / (float)steps) * Mathf.Deg2Rad;
                r.AddPoint(c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RoundaboutRadius);
            }
            var leave = c + new Vector3(Mathf.Cos(a1 * Mathf.Deg2Rad), 0f, Mathf.Sin(a1 * Mathf.Deg2Rad)) * RoundaboutRadius;
            r.AddPoint(Vector3.Lerp(leave, exit, 0.45f));
            r.AddPoint(exit);
        }
    }
}
