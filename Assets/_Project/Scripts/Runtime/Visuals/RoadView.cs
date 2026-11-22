using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>
    /// Auto-tiled road meshes, rebuilt per 8×8 chunk when tiles change. Each tile is asphalt with
    /// raised sidewalks on unconnected sides, dashed centre lines, zebra crossings at intersections,
    /// a planted island on roundabouts, and decks/parapets/piers on bridges.
    /// </summary>
    public class RoadView : MonoBehaviour
    {
        public const int ChunkSize = 8;
        public const float RoadY = 0.06f;
        public const float SidewalkY = 0.2f;
        public const float HalfRoad = 3.2f;

        const int SubAsphalt = 0, SubSidewalk = 1, SubMarking = 2, SubConcrete = 3, SubGrass = 4, SubMetal = 5;

        World world;
        MaterialLibrary lib;
        readonly Dictionary<Vector2Int, MeshFilter> chunks = new Dictionary<Vector2Int, MeshFilter>();
        readonly HashSet<Vector2Int> dirtyChunks = new HashSet<Vector2Int>();
        readonly MeshBuilder mb = new MeshBuilder(6);
        HighwayView highways;
        int highwayVersion = -1;

        public void Build(World w, MaterialLibrary materials)
        {
            world = w;
            lib = materials;
            foreach (Transform c in transform) Destroy(c.gameObject);
            chunks.Clear();
            var hw = new GameObject("Highways");
            hw.transform.SetParent(transform, false);
            highways = hw.AddComponent<HighwayView>();
            highways.Init(w, materials);
            for (int y = 0; y < w.Grid.Height; y += ChunkSize)
            for (int x = 0; x < w.Grid.Width; x += ChunkSize)
                dirtyChunks.Add(new Vector2Int(x / ChunkSize, y / ChunkSize));
            w.Roads.DirtyTiles.Clear();
            Flush();
        }

        void LateUpdate()
        {
            if (world == null) return;
            if (world.Roads.DirtyTiles.Count > 0)
            {
                foreach (var t in world.Roads.DirtyTiles)
                    if (world.Grid.InMap(t)) dirtyChunks.Add(new Vector2Int(t.x / ChunkSize, t.y / ChunkSize));
                world.Roads.DirtyTiles.Clear();
            }
            Flush();
            int hv = HighwaySignature();
            if (hv != highwayVersion)
            {
                highwayVersion = hv;
                highways.Rebuild();
            }
        }

        int HighwaySignature()
        {
            int h = world.Roads.Highways.Count * 7919;
            foreach (var x in world.Roads.Highways) h = h * 31 + x.id;
            return h;
        }

        void Flush()
        {
            if (dirtyChunks.Count == 0) return;
            foreach (var c in dirtyChunks) RebuildChunk(c);
            dirtyChunks.Clear();
        }

        void RebuildChunk(Vector2Int c)
        {
            mb.Clear();
            for (int y = c.y * ChunkSize; y < (c.y + 1) * ChunkSize; y++)
            for (int x = c.x * ChunkSize; x < (c.x + 1) * ChunkSize; x++)
            {
                var t = new TileCoord(x, y);
                if (world.Grid.InMap(t) && world.Roads.IsRoad(t)) BuildTile(t);
            }
            if (!chunks.TryGetValue(c, out var mf))
            {
                var go = new GameObject($"Roads_{c.x}_{c.y}");
                go.transform.SetParent(transform, false);
                mf = go.AddComponent<MeshFilter>();
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = new[] { lib.asphalt, lib.sidewalk, lib.marking, lib.concrete, lib.grass, lib.darkMetal };
                chunks[c] = mf;
            }
            if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
            mf.sharedMesh = mb.IsEmpty ? null : mb.ToMesh($"Roads_{c.x}_{c.y}");
        }

        void AddLamp(TileCoord t, int conn, float h)
        {
            // Pick a corner sidewalk; the arm reaches over the road toward the tile centre.
            int corner = (t.x * 3 + t.y * 5) & 3;
            float sx = (corner & 1) == 0 ? -1f : 1f, sz = (corner & 2) == 0 ? -1f : 1f;
            var basePos = new Vector3(sx * (h - 0.45f), SidewalkY, sz * (h - 0.45f));
            mb.Cylinder(SubMetal, basePos, 0.09f, 5.6f, 6, 1f, false);
            var dir = new Vector3(-sx, 0f, -sz).normalized;
            var top = basePos + Vector3.up * 5.6f;
            var head = top + dir * 1.5f;
            var side = Vector3.Cross(dir, Vector3.up) * 0.05f;
            mb.Quad(SubMetal, top - side, top + side + Vector3.up * 0.08f, head + side + Vector3.up * 0.08f, head - side, 1f);
            mb.Quad(SubMetal, head - side, head + side + Vector3.up * 0.08f, top + side + Vector3.up * 0.08f, top - side, 1f);
            mb.BoxCentered(SubMetal, head + Vector3.down * 0.05f, new Vector3(0.45f, 0.14f, 0.45f), 1f);
        }

        static void Rect(Dir d, float near, float far, float halfWidth, out float x0, out float z0, out float x1, out float z1)
        {
            switch (d)
            {
                case Dir.N: x0 = -halfWidth; x1 = halfWidth; z0 = near; z1 = far; break;
                case Dir.S: x0 = -halfWidth; x1 = halfWidth; z0 = -far; z1 = -near; break;
                case Dir.E: x0 = near; x1 = far; z0 = -halfWidth; z1 = halfWidth; break;
                default: x0 = -far; x1 = -near; z0 = -halfWidth; z1 = halfWidth; break;
            }
        }

        void BuildTile(TileCoord t)
        {
            var roads = world.Roads;
            float h = world.Grid.TileSize * 0.5f;
            float a = HalfRoad;
            float sa = 1f / lib.asphaltMeters, ss = 1f / lib.sidewalkMeters;
            int mask = roads.Mask(t);
            var bside = roads.BuildingSide(t);
            int conn = mask | (bside.HasValue ? bside.Value.Bit() : 0);
            bool bridge = roads.IsBridge(t);
            bool roundabout = roads.IsRoundabout(t);
            float bottom = bridge ? -0.55f : -0.7f;

            mb.Push(Matrix4x4.Translate(world.Grid.TileCenter(t)));

            // Asphalt
            mb.Flat(SubAsphalt, -a, -a, a, a, RoadY, sa);
            foreach (var d in DirUtil.All)
            {
                Rect(d, a, h, a, out var x0, out var z0, out var x1, out var z1);
                if ((conn & d.Bit()) != 0)
                    mb.Flat(SubAsphalt, x0, z0, x1, z1, RoadY, sa);
                else
                    mb.Box(SubSidewalk, new Vector3(x0, bottom, z0), new Vector3(x1, SidewalkY, z1), ss,
                        MeshBuilder.Faces.Top | MeshBuilder.Faces.Sides);
            }
            // Corner sidewalks
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                float x0 = sx < 0 ? -h : a, x1 = sx < 0 ? -a : h;
                float z0 = sz < 0 ? -h : a, z1 = sz < 0 ? -a : h;
                mb.Box(SubSidewalk, new Vector3(x0, bottom, z0), new Vector3(x1, SidewalkY, z1), ss,
                    MeshBuilder.Faces.Top | MeshBuilder.Faces.Sides);
            }

            // Markings
            int roadConn = DirUtil.PopCount(mask);
            bool intersection = roads.IsIntersection(t);
            const float my = RoadY + 0.008f;
            if (roundabout)
            {
                mb.Cylinder(SubConcrete, new Vector3(0f, RoadY, 0f), 1.55f, 0.18f, 24, 1f, false);
                mb.Disc(SubGrass, new Vector3(0f, RoadY + 0.2f, 0f), 1.5f, 24, 1f / lib.grassMeters, true);
                // dashed circulation ring
                for (int i = 0; i < 16; i += 1)
                {
                    float a0 = i / 16f * Mathf.PI * 2f, a1 = (i + 0.5f) / 16f * Mathf.PI * 2f;
                    var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                    var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                    // angles increase counter-clockwise, so start from p1 to keep the quad facing up
                    mb.Quad(SubMarking, p1 * 3.0f + Vector3.up * my, p1 * 3.14f + Vector3.up * my,
                        p0 * 3.14f + Vector3.up * my, p0 * 3.0f + Vector3.up * my, 1f);
                }
            }
            else if (intersection)
            {
                foreach (var d in DirUtil.All)
                {
                    if ((mask & d.Bit()) == 0) continue;
                    for (float lat = -2.7f; lat <= 2.71f; lat += 0.9f)
                    {
                        Rect(d, 2.2f, 3.6f, 0.22f, out var x0, out var z0, out var x1, out var z1);
                        var off = (d == Dir.N || d == Dir.S) ? new Vector3(lat, 0f, 0f) : new Vector3(0f, 0f, lat);
                        mb.Flat(SubMarking, x0 + off.x, z0 + off.z, x1 + off.x, z1 + off.z, my, 1f);
                    }
                }
            }
            else
            {
                foreach (var d in DirUtil.All)
                {
                    if ((mask & d.Bit()) == 0) continue;
                    for (float s = 0.3f; s < h; s += 2f)
                    {
                        Rect(d, s, Mathf.Min(h, s + 1.1f), 0.075f, out var x0, out var z0, out var x1, out var z1);
                        mb.Flat(SubMarking, x0, z0, x1, z1, my, 1f);
                    }
                }
                if (roadConn == 0 && bside.HasValue)
                {
                    // lone driveway tile: a short stop bar
                    Rect(bside.Value.Opposite(), 0.2f, 0.45f, a - 0.3f, out var x0, out var z0, out var x1, out var z1);
                    mb.Flat(SubMarking, x0, z0, x1, z1, my, 1f);
                }
            }

            if (!bridge && !roundabout && roadConn <= 2 && ((t.x * 7 + t.y * 13) % 3 == 0))
                AddLamp(t, conn, h);

            if (bridge)
            {
                var span = roads.Bridges[roads.BridgeIdAt(t)];
                var axis = span.axis;
                // Deck slab under the asphalt
                mb.Box(SubConcrete, new Vector3(-a, -0.55f, -a), new Vector3(a, RoadY - 0.002f, a), 0.5f,
                    MeshBuilder.Faces.Bottom);
                foreach (var d in DirUtil.All)
                {
                    if ((conn & d.Bit()) != 0) continue;
                    Rect(d, h - 0.28f, h, h, out var x0, out var z0, out var x1, out var z1);
                    mb.Box(SubConcrete, new Vector3(x0, SidewalkY, z0), new Vector3(x1, 1.05f, z1), 0.5f);
                }
                // Piers across the axis
                var lateral = (axis == Dir.N || axis == Dir.S) ? Vector3.right : Vector3.forward;
                foreach (float k in new[] { -2.3f, 2.3f })
                    mb.Cylinder(SubConcrete, lateral * k + Vector3.up * -2.6f, 0.45f, 2.06f, 12, 0.5f, false);
            }

            mb.Pop();
        }
    }
}
