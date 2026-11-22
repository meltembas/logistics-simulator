using UnityEngine;

namespace Logistics
{
    public enum TerrainKind : byte { Ground = 0, Water = 1 }

    /// <summary>What sits on a tile. Roads live in <see cref="RoadNetwork"/>; this tracks exclusivity.</summary>
    public enum Occupant : byte { None = 0, Road = 1, Building = 2, Filler = 3 }

    /// <summary>Pure tile data for the play field. Tile (0,0) spans world [0,ts]x[0,ts].</summary>
    public class GridMap
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float TileSize;

        readonly TerrainKind[] terrain;
        readonly Occupant[] occupant;
        readonly bool[] tree;

        public RectInt ActiveBounds;
        public int Version { get; private set; }
        /// <summary>Tiles whose tree visuals changed (consumed by the decoration view).</summary>
        public readonly System.Collections.Generic.HashSet<TileCoord> DirtyTreeTiles = new System.Collections.Generic.HashSet<TileCoord>();

        public GridMap(int width, int height, float tileSize)
        {
            Width = width;
            Height = height;
            TileSize = tileSize;
            terrain = new TerrainKind[width * height];
            occupant = new Occupant[width * height];
            tree = new bool[width * height];
            ActiveBounds = new RectInt(0, 0, width, height);
        }

        int Idx(TileCoord t) => t.y * Width + t.x;

        public bool InMap(TileCoord t) => t.x >= 0 && t.y >= 0 && t.x < Width && t.y < Height;
        public bool InActive(TileCoord t) => InMap(t) && ActiveBounds.Contains(new Vector2Int(t.x, t.y));

        public TerrainKind Terrain(TileCoord t) => InMap(t) ? terrain[Idx(t)] : TerrainKind.Ground;
        public bool IsWater(TileCoord t) => InMap(t) && terrain[Idx(t)] == TerrainKind.Water;
        public Occupant OccupantAt(TileCoord t) => InMap(t) ? occupant[Idx(t)] : Occupant.Filler;
        public bool HasTree(TileCoord t) => InMap(t) && tree[Idx(t)];

        public void SetTerrain(TileCoord t, TerrainKind k) { terrain[Idx(t)] = k; Version++; }

        public void SetOccupant(TileCoord t, Occupant o)
        {
            int i = Idx(t);
            occupant[i] = o;
            if (o != Occupant.None && tree[i]) { tree[i] = false; DirtyTreeTiles.Add(t); }
            Version++;
        }

        public void SetTree(TileCoord t, bool v) { tree[Idx(t)] = v; DirtyTreeTiles.Add(t); Version++; }

        public bool IsFree(TileCoord t) => InMap(t) && occupant[Idx(t)] == Occupant.None;

        /// <summary>Free ground tile inside the active area (trees are cleared automatically).</summary>
        public bool IsBuildableGround(TileCoord t) => InActive(t) && IsFree(t) && !IsWater(t);

        public Vector3 TileCenter(TileCoord t) => new Vector3((t.x + 0.5f) * TileSize, 0f, (t.y + 0.5f) * TileSize);

        public TileCoord WorldToTile(Vector3 p) =>
            new TileCoord(Mathf.FloorToInt(p.x / TileSize), Mathf.FloorToInt(p.z / TileSize));

        public Vector3 WorldSize => new Vector3(Width * TileSize, 0f, Height * TileSize);
        public Vector3 WorldCenter => new Vector3(Width * TileSize * 0.5f, 0f, Height * TileSize * 0.5f);

        public Bounds ActiveWorldBounds
        {
            get
            {
                var min = new Vector3(ActiveBounds.xMin * TileSize, 0f, ActiveBounds.yMin * TileSize);
                var max = new Vector3(ActiveBounds.xMax * TileSize, 0f, ActiveBounds.yMax * TileSize);
                var b = new Bounds();
                b.SetMinMax(min, max);
                return b;
            }
        }
    }
}
