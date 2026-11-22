using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>One tile (or highway) stretch of a route.</summary>
    public struct RouteSpan
    {
        public TileCoord tile;
        public float sStart;
        public float sEnd;
        public float speedFactor;
        public bool highway;
        /// <summary>Intersection / dock tiles that need an exclusive (or capacity-limited) reservation.</summary>
        public int capacity;
    }

    /// <summary>A drivable polyline with arc-length parameterization and per-tile spans.</summary>
    public class Route
    {
        public readonly List<Vector3> points = new List<Vector3>();
        public readonly List<float> cum = new List<float>();
        public readonly List<RouteSpan> spans = new List<RouteSpan>();
        public readonly List<TileCoord> tiles = new List<TileCoord>();
        public float Length => cum.Count == 0 ? 0f : cum[cum.Count - 1];

        public void AddPoint(Vector3 p)
        {
            if (points.Count > 0)
            {
                float d = Vector3.Distance(points[points.Count - 1], p);
                if (d < 0.01f) return;
                cum.Add(cum[cum.Count - 1] + d);
            }
            else cum.Add(0f);
            points.Add(p);
        }

        int SegmentAt(float s)
        {
            int lo = 0, hi = cum.Count - 1;
            if (s <= 0f) return 0;
            if (s >= cum[hi]) return Mathf.Max(0, hi - 1);
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (cum[mid] <= s) lo = mid; else hi = mid;
            }
            return lo;
        }

        public Vector3 PositionAt(float s)
        {
            if (points.Count == 0) return Vector3.zero;
            if (points.Count == 1) return points[0];
            int i = SegmentAt(s);
            float seg = cum[i + 1] - cum[i];
            float t = seg > 1e-5f ? Mathf.Clamp01((s - cum[i]) / seg) : 0f;
            return Vector3.Lerp(points[i], points[i + 1], t);
        }

        public Vector3 TangentAt(float s)
        {
            if (points.Count < 2) return Vector3.forward;
            // Smooth over a short window so polyline kinks don't jitter the heading.
            var a = PositionAt(s - 1.2f);
            var b = PositionAt(s + 1.2f);
            var d = b - a;
            d.y = 0f;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        public int SpanIndexAt(float s)
        {
            for (int i = 0; i < spans.Count; i++)
                if (s < spans[i].sEnd) return i;
            return spans.Count - 1;
        }

        public float SpeedFactorAt(float s)
        {
            if (spans.Count == 0) return 1f;
            return spans[SpanIndexAt(s)].speedFactor;
        }

        public bool ContainsTile(TileCoord t)
        {
            foreach (var x in tiles) if (x == t) return true;
            return false;
        }
    }
}
