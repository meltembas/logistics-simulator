using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>
    /// Moves trucks along their routes: speed limits per span, car-following along the route, and
    /// tile reservations for intersections and docks (reserved as a chain, with a don't-block-the-box
    /// check on the exit lane).
    /// </summary>
    public class TrafficSystem
    {
        readonly World w;
        public readonly List<TruckAgent> Trucks = new List<TruckAgent>();
        readonly Dictionary<TileCoord, List<TruckAgent>> holders = new Dictionary<TileCoord, List<TruckAgent>>();
        readonly Dictionary<TileCoord, int> occupancy = new Dictionary<TileCoord, int>();

        const float SampleStep = 1.5f;
        const float LookAhead = 24f;
        const float LaneRadius = 1.6f;

        public TrafficSystem(World world) { w = world; }

        public int OccupancyAt(TileCoord t) => occupancy.TryGetValue(t, out var c) ? c : 0;

        public bool IsTileInUse(TileCoord t)
        {
            foreach (var tr in Trucks)
            {
                if (tr.route == null || tr.State == TruckState.Parked || tr.State == TruckState.Parking) continue;
                if (tr.route.ContainsTile(t)) return true;
            }
            return false;
        }

        public int HolderCount(TileCoord t, TruckAgent except)
        {
            if (!holders.TryGetValue(t, out var list)) return 0;
            int c = 0;
            foreach (var x in list) if (x != except) c++;
            return c;
        }

        /// <summary>Reserve the run of reservation-needing spans starting at <paramref name="start"/>.</summary>
        public bool TryReserveChain(TruckAgent truck, int start)
        {
            var spans = truck.route.spans;
            if (start >= spans.Count || spans[start].capacity <= 0) return true;
            int end = start;
            while (end + 1 < spans.Count && spans[end + 1].capacity > 0) end++;

            for (int k = start; k <= end; k++)
            {
                if (truck.Holds(k)) continue;
                if (HolderCount(spans[k].tile, truck) >= spans[k].capacity) return false;
            }
            // Don't block the box: the lane after the chain must have room for us.
            if (end < spans.Count - 1)
            {
                float from = spans[end].sEnd;
                float to = Mathf.Min(truck.route.Length, from + w.Config.truckLength + w.Config.followGap);
                if (!IsLaneClear(truck, from, to)) return false;
            }
            for (int k = start; k <= end; k++)
            {
                if (truck.Holds(k)) continue;
                var tile = spans[k].tile;
                if (!holders.TryGetValue(tile, out var list)) holders[tile] = list = new List<TruckAgent>(2);
                list.Add(truck);
                truck.heldSpans.Add(k);
            }
            return true;
        }

        public void Release(TruckAgent truck, int spanIndex)
        {
            if (!truck.heldSpans.Remove(spanIndex)) return;
            var tile = truck.route.spans[spanIndex].tile;
            if (holders.TryGetValue(tile, out var list)) list.Remove(truck);
        }

        public void ReleaseAll(TruckAgent truck, TileCoord? keepTile = null)
        {
            for (int i = truck.heldSpans.Count - 1; i >= 0; i--)
            {
                int k = truck.heldSpans[i];
                var tile = truck.route.spans[k].tile;
                if (keepTile.HasValue && tile == keepTile.Value) continue;
                truck.heldSpans.RemoveAt(i);
                if (holders.TryGetValue(tile, out var list)) list.Remove(truck);
            }
        }

        /// <summary>Re-key held reservations onto a new route (used when a truck turns around in a dock).</summary>
        public void TransferHeld(TruckAgent truck, Route newRoute)
        {
            var oldRoute = truck.route;
            var tiles = new List<TileCoord>();
            foreach (var k in truck.heldSpans) tiles.Add(oldRoute.spans[k].tile);
            truck.heldSpans.Clear();
            truck.route = newRoute;
            for (int k = 0; k < newRoute.spans.Count; k++)
                if (tiles.Contains(newRoute.spans[k].tile) && newRoute.spans[k].capacity > 0 && !truck.heldSpans.Contains(k))
                    truck.heldSpans.Add(k);
            // tiles no longer on the route are released
            foreach (var t in tiles)
            {
                bool stillHeld = false;
                foreach (var k in truck.heldSpans) if (newRoute.spans[k].tile == t) stillHeld = true;
                if (!stillHeld && holders.TryGetValue(t, out var list)) list.Remove(truck);
            }
        }

        bool IsLaneClear(TruckAgent me, float from, float to)
        {
            for (float d = from; d <= to; d += SampleStep)
            {
                var p = me.route.PositionAt(d);
                var tan = me.route.TangentAt(d);
                foreach (var o in Trucks)
                {
                    if (o == me || !IsOnStreet(o)) continue;
                    if (Near(o.position, p) && Vector3.Dot(o.forward, tan) > 0.2f) return false;
                }
            }
            return true;
        }

        static bool IsOnStreet(TruckAgent o) =>
            o.State == TruckState.Outbound || o.State == TruckState.Returning || o.State == TruckState.Unloading;

        static bool Near(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z, dy = a.y - b.y;
            return dx * dx + dz * dz < LaneRadius * LaneRadius && Mathf.Abs(dy) < 2f;
        }

        float LeaderGap(TruckAgent me)
        {
            var r = me.route;
            for (float d = SampleStep; d <= LookAhead; d += SampleStep)
            {
                float sd = me.s + d;
                if (sd > r.Length) break;
                var p = r.PositionAt(sd);
                var tan = r.TangentAt(sd);
                foreach (var o in Trucks)
                {
                    if (o == me || !IsOnStreet(o)) continue;
                    if (Near(o.position, p) && Vector3.Dot(o.forward, tan) > 0.2f)
                        return d - w.Config.truckLength;
                }
            }
            return float.MaxValue;
        }

        public void Tick(float dt)
        {
            occupancy.Clear();
            foreach (var t in Trucks)
            {
                if (!t.OnRoad || t.route == null) continue;
                var tile = t.route.spans[t.route.SpanIndexAt(t.s)].tile;
                occupancy.TryGetValue(tile, out var c);
                occupancy[tile] = c + 1;
            }

            var cfg = w.Config;
            foreach (var t in Trucks)
            {
                switch (t.State)
                {
                    case TruckState.Departing:
                    case TruckState.Parking:
                        TickLerp(t, dt);
                        break;
                    case TruckState.Outbound:
                    case TruckState.Returning:
                        TickDrive(t, dt, cfg);
                        break;
                }
            }
        }

        void TickLerp(TruckAgent t, float dt)
        {
            t.timer += dt;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t.timer / t.lerpDuration));
            t.position = Vector3.Lerp(t.lerpFrom, t.lerpTo, k);
            var f = Vector3.Slerp(t.lerpFwdFrom, t.lerpFwdTo, k);
            if (f.sqrMagnitude > 1e-4f) t.forward = f.normalized;
            t.speed = 0f;
            if (t.timer >= t.lerpDuration) w.OnTruckLerpDone(t);
        }

        void TickDrive(TruckAgent t, float dt, GameConfig cfg)
        {
            var r = t.route;
            var spans = r.spans;
            int cur = r.SpanIndexAt(t.s);

            // Release spans our rear has cleared.
            for (int i = t.heldSpans.Count - 1; i >= 0; i--)
            {
                int k = t.heldSpans[i];
                if (k < spans.Count - 1 && t.s - cfg.truckLength * 0.55f > spans[k].sEnd) Release(t, k);
            }

            float stopDist = r.Length - t.s;

            // Reservations: find the next span needing one that we don't hold.
            for (int k = cur; k < spans.Count; k++)
            {
                if (spans[k].sStart - t.s > LookAhead) break;
                if (spans[k].capacity <= 0 || t.Holds(k)) continue;
                float brakeDist = t.speed * t.speed / (2f * cfg.truckBrake) + 3f;
                float distToEntry = spans[k].sStart - t.s;
                if (distToEntry <= brakeDist && !TryReserveChain(t, k))
                {
                    stopDist = Mathf.Min(stopDist, distToEntry - 0.6f);
                }
                break;
            }

            float gap = LeaderGap(t);
            if (gap < float.MaxValue) stopDist = Mathf.Min(stopDist, gap - cfg.followGap);

            // Speed limit: current span plus braking for slower spans ahead.
            float limit = cfg.truckSpeed * spans[cur].speedFactor;
            for (int k = cur + 1; k < spans.Count; k++)
            {
                float dist = spans[k].sStart - t.s;
                if (dist > 30f) break;
                float vk = cfg.truckSpeed * spans[k].speedFactor;
                limit = Mathf.Min(limit, Mathf.Sqrt(vk * vk + 2f * cfg.truckBrake * 0.45f * Mathf.Max(0f, dist)));
            }
            float vStop = Mathf.Sqrt(2f * cfg.truckBrake * 0.8f * Mathf.Max(0f, stopDist));
            float desired = Mathf.Min(limit, vStop);

            if (desired > t.speed) t.speed = Mathf.Min(desired, t.speed + cfg.truckAccel * dt);
            else t.speed = Mathf.Max(desired, t.speed - cfg.truckBrake * 1.5f * dt);
            t.braking = desired < t.speed - 0.3f || (t.speed < 0.5f && stopDist < 3f);
            t.waitTime = t.speed < 0.2f ? t.waitTime + dt : 0f;

            t.s = Mathf.Min(r.Length, t.s + t.speed * dt);
            t.position = r.PositionAt(t.s);
            t.forward = r.TangentAt(t.s);

            if (t.s >= r.Length - 0.05f) w.OnTruckArrived(t);
        }
    }
}
