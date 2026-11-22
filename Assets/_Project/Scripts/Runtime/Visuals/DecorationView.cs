using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>
    /// Trees and the existing city: filler buildings on the play field (rebuilt per chunk when trees are
    /// cleared), plus a static ring of streets, blocks and trees around the map so the world never ends in a void.
    /// </summary>
    public class DecorationView : MonoBehaviour
    {
        const int Chunk = 8;
        World world;
        MaterialLibrary lib;
        readonly Dictionary<Vector2Int, MeshFilter> chunks = new Dictionary<Vector2Int, MeshFilter>();
        Material[] cityMats;

        public void Build(World w, MaterialLibrary materials)
        {
            world = w;
            lib = materials;
            cityMats = BuildingFactory.CityMaterials(lib);
            foreach (Transform c in transform) Destroy(c.gameObject);
            chunks.Clear();
            for (int y = 0; y < w.Grid.Height; y += Chunk)
            for (int x = 0; x < w.Grid.Width; x += Chunk)
                RebuildChunk(new Vector2Int(x / Chunk, y / Chunk));
            w.Grid.DirtyTreeTiles.Clear();
            BuildOuterRing();
        }

        void LateUpdate()
        {
            if (world == null || world.Grid.DirtyTreeTiles.Count == 0) return;
            var dirty = new HashSet<Vector2Int>();
            foreach (var t in world.Grid.DirtyTreeTiles) dirty.Add(new Vector2Int(t.x / Chunk, t.y / Chunk));
            world.Grid.DirtyTreeTiles.Clear();
            foreach (var c in dirty) RebuildChunk(c);
        }

        int TileSeed(int x, int y) => world.Seed * 92821 + x * 73856093 + y * 19349663;

        void RebuildChunk(Vector2Int c)
        {
            var g = world.Grid;
            var mb = new MeshBuilder(BuildingFactory.CSubCount);
            for (int y = c.y * Chunk; y < (c.y + 1) * Chunk; y++)
            for (int x = c.x * Chunk; x < (c.x + 1) * Chunk; x++)
            {
                var t = new TileCoord(x, y);
                if (!g.InMap(t)) continue;
                var center = g.TileCenter(t);
                if (g.OccupantAt(t) == Occupant.Filler)
                    BuildingFactory.AddFiller(mb, center, g.TileSize, TileSeed(x, y), lib, false);
                else if (g.HasTree(t))
                    AddTreeTile(mb, center, g.TileSize, TileSeed(x, y));
            }
            if (!chunks.TryGetValue(c, out var mf))
            {
                var go = new GameObject($"Deco_{c.x}_{c.y}");
                go.transform.SetParent(transform, false);
                mf = go.AddComponent<MeshFilter>();
                go.AddComponent<MeshRenderer>().sharedMaterials = cityMats;
                chunks[c] = mf;
            }
            if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
            mf.sharedMesh = mb.IsEmpty ? null : mb.ToMesh($"Deco_{c.x}_{c.y}");
        }

        static void AddTreeTile(MeshBuilder mb, Vector3 center, float tile, int seed)
        {
            var rnd = new System.Random(seed);
            int n = 2 + rnd.Next(3);
            for (int i = 0; i < n; i++)
            {
                var off = new Vector3((float)(rnd.NextDouble() - 0.5) * tile * 0.7f, 0f, (float)(rnd.NextDouble() - 0.5) * tile * 0.7f);
                BuildingFactory.AddTree(mb, center + off, rnd.Next());
            }
        }

        void BuildOuterRing()
        {
            var g = world.Grid;
            int ring = Mathf.CeilToInt(TerrainView.Margin / g.TileSize);
            float ts = g.TileSize;
            var groups = new Dictionary<Vector2Int, MeshBuilder>();
            MeshBuilder Get(int x, int y)
            {
                var key = new Vector2Int(Mathf.FloorToInt(x / 12f), Mathf.FloorToInt(y / 12f));
                if (!groups.TryGetValue(key, out var b)) groups[key] = b = new MeshBuilder(BuildingFactory.CSubCount);
                return b;
            }

            for (int y = -ring; y < g.Height + ring; y++)
            for (int x = -ring; x < g.Width + ring; x++)
            {
                if (x >= 0 && y >= 0 && x < g.Width && y < g.Height) continue;
                // The river leaves the map through the top and bottom edges; keep its continuation clear.
                bool water = x >= 0 && x < g.Width && (y < 0 || y >= g.Height)
                             && g.IsWater(new TileCoord(x, Mathf.Clamp(y, 0, g.Height - 1)));
                if (water) continue;

                var center = new Vector3((x + 0.5f) * ts, 0f, (y + 0.5f) * ts);
                int dist = Mathf.Max(Mathf.Max(-x, x - (g.Width - 1)), Mathf.Max(-y, y - (g.Height - 1)));
                var mb = Get(x, y);
                bool street = dist >= 2 && (Mod(x, 5) == 0 || Mod(y, 5) == 0);
                if (street)
                {
                    bool ns = Mod(x, 5) == 0, ew = Mod(y, 5) == 0;
                    AddOuterStreet(mb, center, ts, ns, ew);
                    continue;
                }
                var rnd = new System.Random(TileSeed(x + 1000, y + 1000));
                double r = rnd.NextDouble();
                float density = Mathf.Clamp01((dist - 1) / 5f);
                if (r < 0.25 + 0.4 * density) BuildingFactory.AddFiller(mb, center, ts, rnd.Next(), lib, density > 0.4f);
                else if (r < 0.75) AddTreeTile(mb, center, ts, rnd.Next());
            }
            foreach (var kv in groups)
            {
                if (kv.Value.IsEmpty) continue;
                var go = new GameObject($"OuterCity_{kv.Key.x}_{kv.Key.y}");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = kv.Value.ToMesh(go.name);
                go.AddComponent<MeshRenderer>().sharedMaterials = cityMats;
            }
        }

        static int Mod(int a, int m) => ((a % m) + m) % m;

        void AddOuterStreet(MeshBuilder mb, Vector3 c, float ts, bool ns, bool ew)
        {
            float h = ts * 0.5f, a = RoadView.HalfRoad;
            float sa = 1f / lib.asphaltMeters, ss = 1f / lib.sidewalkMeters;
            mb.Push(Matrix4x4.Translate(c));
            mb.Flat(BuildingFactory.CAsphalt, -a, -a, a, a, RoadView.RoadY, sa);
            if (ns)
            {
                mb.Flat(BuildingFactory.CAsphalt, -a, a, a, h, RoadView.RoadY, sa);
                mb.Flat(BuildingFactory.CAsphalt, -a, -h, a, -a, RoadView.RoadY, sa);
            }
            else
            {
                mb.Box(BuildingFactory.CSidewalk, new Vector3(-a, -0.3f, a), new Vector3(a, RoadView.SidewalkY, h), ss, MeshBuilder.Faces.Top | MeshBuilder.Faces.Sides);
                mb.Box(BuildingFactory.CSidewalk, new Vector3(-a, -0.3f, -h), new Vector3(a, RoadView.SidewalkY, -a), ss, MeshBuilder.Faces.Top | MeshBuilder.Faces.Sides);
            }
            if (ew)
            {
                mb.Flat(BuildingFactory.CAsphalt, a, -a, h, a, RoadView.RoadY, sa);
                mb.Flat(BuildingFactory.CAsphalt, -h, -a, -a, a, RoadView.RoadY, sa);
            }
            else
            {
                mb.Box(BuildingFactory.CSidewalk, new Vector3(a, -0.3f, -a), new Vector3(h, RoadView.SidewalkY, a), ss, MeshBuilder.Faces.Top | MeshBuilder.Faces.Sides);
                mb.Box(BuildingFactory.CSidewalk, new Vector3(-h, -0.3f, -a), new Vector3(-a, RoadView.SidewalkY, a), ss, MeshBuilder.Faces.Top | MeshBuilder.Faces.Sides);
            }
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                float x0 = sx < 0 ? -h : a, x1 = sx < 0 ? -a : h, z0 = sz < 0 ? -h : a, z1 = sz < 0 ? -a : h;
                mb.Box(BuildingFactory.CSidewalk, new Vector3(x0, -0.3f, z0), new Vector3(x1, RoadView.SidewalkY, z1), ss, MeshBuilder.Faces.Top | MeshBuilder.Faces.Sides);
            }
            mb.Pop();
        }
    }
}
