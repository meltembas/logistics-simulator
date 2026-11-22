using System;
using UnityEngine;

namespace Logistics
{
    /// <summary>Four map directions. N = +Z, E = +X, S = -Z, W = -X.</summary>
    public enum Dir : byte { N = 0, E = 1, S = 2, W = 3 }

    public static class DirUtil
    {
        public static readonly Dir[] All = { Dir.N, Dir.E, Dir.S, Dir.W };

        public static Dir Opposite(this Dir d) => (Dir)(((int)d + 2) & 3);
        public static int Bit(this Dir d) => 1 << (int)d;

        public static Vector2Int Offset(this Dir d)
        {
            switch (d)
            {
                case Dir.N: return new Vector2Int(0, 1);
                case Dir.E: return new Vector2Int(1, 0);
                case Dir.S: return new Vector2Int(0, -1);
                default: return new Vector2Int(-1, 0);
            }
        }

        public static Vector3 Vec(this Dir d)
        {
            var o = d.Offset();
            return new Vector3(o.x, 0f, o.y);
        }

        public static float Yaw(this Dir d) => (int)d * 90f;

        public static int PopCount(int mask)
        {
            int c = 0;
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) c++;
            return c;
        }

        /// <summary>Direction from a to b when they are 4-neighbors; null otherwise.</summary>
        public static Dir? Between(TileCoord a, TileCoord b)
        {
            int dx = b.x - a.x, dy = b.y - a.y;
            if (dx == 0 && dy == 1) return Dir.N;
            if (dx == 1 && dy == 0) return Dir.E;
            if (dx == 0 && dy == -1) return Dir.S;
            if (dx == -1 && dy == 0) return Dir.W;
            return null;
        }
    }

    [Serializable]
    public struct TileCoord : IEquatable<TileCoord>
    {
        public int x;
        public int y;

        public TileCoord(int x, int y) { this.x = x; this.y = y; }

        public TileCoord Step(Dir d)
        {
            var o = d.Offset();
            return new TileCoord(x + o.x, y + o.y);
        }

        public int Manhattan(TileCoord o) => Math.Abs(x - o.x) + Math.Abs(y - o.y);
        public float Distance(TileCoord o) => Mathf.Sqrt((x - o.x) * (x - o.x) + (y - o.y) * (y - o.y));

        public bool Equals(TileCoord o) => x == o.x && y == o.y;
        public override bool Equals(object obj) => obj is TileCoord o && Equals(o);
        public override int GetHashCode() => (x * 73856093) ^ (y * 19349663);
        public static bool operator ==(TileCoord a, TileCoord b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(TileCoord a, TileCoord b) => !(a == b);
        public override string ToString() => $"({x},{y})";
    }
}
