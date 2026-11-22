using UnityEngine;

namespace Logistics
{
    /// <summary>Ground heightfield (grass + carved riverbed), water surface, and the dimmed out-of-bounds overlay.</summary>
    public class TerrainView : MonoBehaviour
    {
        public const float Margin = 96f;
        const float Step = 2f;
        const float RiverDepth = 1.9f;
        public const float WaterLevel = -0.45f;

        World world;
        MaterialLibrary lib;
        GameObject ground, water, overlay, border;
        RectInt shownBounds;
        Rect animBounds;
        Mesh overlayMesh, borderMesh;

        public void Build(World w, MaterialLibrary materials)
        {
            world = w;
            lib = materials;
            foreach (Transform c in transform) Destroy(c.gameObject);
            BuildGround();
            BuildWater();
            overlay = MakeChild("OutOfBounds", lib.overlay, out var mfO);
            overlayMesh = new Mesh { name = "OutOfBounds" };
            mfO.sharedMesh = overlayMesh;
            border = MakeChild("BoundsBorder", lib.fx, out var mfB);
            borderMesh = new Mesh { name = "BoundsBorder" };
            mfB.sharedMesh = borderMesh;
            var b = w.Grid.ActiveBounds;
            animBounds = new Rect(b.x, b.y, b.width, b.height);
            shownBounds = b;
            RebuildOverlay();
        }

        GameObject MakeChild(string name, Material m, out MeshFilter mf)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        bool WaterAt(float px, float pz)
        {
            var g = world.Grid;
            float ts = g.TileSize;
            // Outside the map the edge rows/columns continue, so the river runs off the map instead of ending.
            int tx = Mathf.Clamp(Mathf.FloorToInt(px / ts), 0, g.Width - 1);
            int ty = Mathf.Clamp(Mathf.FloorToInt(pz / ts), 0, g.Height - 1);
            return g.IsWater(new TileCoord(tx, ty));
        }

        /// <summary>Riverbed depth from the fraction of water in a small disc — rounds the tile staircase into banks.</summary>
        float Height(float px, float pz)
        {
            const float r = 4.6f;
            int water = 0, total = 0;
            for (int i = -2; i <= 2; i++)
            for (int j = -2; j <= 2; j++)
            {
                float dx = i * r * 0.5f, dz = j * r * 0.5f;
                if (dx * dx + dz * dz > r * r * 1.01f) continue;
                total++;
                if (WaterAt(px + dx, pz + dz)) water++;
            }
            if (water == 0) return 0f;
            float f = water / (float)total;
            f += (Mathf.PerlinNoise(px * 0.08f, pz * 0.08f) - 0.5f) * 0.18f;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.9f, f));
            return -RiverDepth * k;
        }

        void BuildGround()
        {
            var g = world.Grid;
            float w = g.Width * g.TileSize, h = g.Height * g.TileSize;
            float x0 = -Margin, z0 = -Margin, x1 = w + Margin, z1 = h + Margin;
            int nx = Mathf.CeilToInt((x1 - x0) / Step), nz = Mathf.CeilToInt((z1 - z0) / Step);
            var heights = new float[(nx + 1) * (nz + 1)];
            for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
                heights[j * (nx + 1) + i] = Height(x0 + i * Step, z0 + j * Step);

            var mb = new MeshBuilder(2);
            float sg = 1f / lib.grassMeters, sr = 1f / lib.riverbedMeters;
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float ax = x0 + i * Step, az = z0 + j * Step;
                float h00 = heights[j * (nx + 1) + i], h10 = heights[j * (nx + 1) + i + 1];
                float h01 = heights[(j + 1) * (nx + 1) + i], h11 = heights[(j + 1) * (nx + 1) + i + 1];
                bool wet = Mathf.Min(Mathf.Min(h00, h10), Mathf.Min(h01, h11)) < -0.3f;
                int sub = wet ? 1 : 0;
                mb.Quad(sub, new Vector3(ax, h00, az), new Vector3(ax, h01, az + Step),
                    new Vector3(ax + Step, h11, az + Step), new Vector3(ax + Step, h10, az), wet ? sr : sg);
            }
            ground = new GameObject("Ground");
            ground.transform.SetParent(transform, false);
            var mesh = mb.ToMesh("Ground");
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = ground.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { lib.grass, lib.riverbed };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void BuildWater()
        {
            // One sheet under the whole world; the terrain hides it everywhere except the carved river.
            var g = world.Grid;
            var mb = new MeshBuilder(1);
            float w = g.Width * g.TileSize, h = g.Height * g.TileSize;
            mb.Flat(0, -Margin, -Margin, w + Margin, h + Margin, WaterLevel, 1f / 6f);
            water = new GameObject("Water");
            water.transform.SetParent(transform, false);
            water.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("Water");
            var mr = water.AddComponent<MeshRenderer>();
            mr.sharedMaterial = lib.water;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            water.AddComponent<WaterSurface>();
        }

        void Update()
        {
            if (world == null) return;
            var target = world.Grid.ActiveBounds;
            var goal = new Rect(target.x, target.y, target.width, target.height);
            if (animBounds != goal)
            {
                float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2.5f);
                animBounds = new Rect(
                    Mathf.Lerp(animBounds.x, goal.x, k), Mathf.Lerp(animBounds.y, goal.y, k),
                    Mathf.Lerp(animBounds.width, goal.width, k), Mathf.Lerp(animBounds.height, goal.height, k));
                if (Mathf.Abs(animBounds.x - goal.x) + Mathf.Abs(animBounds.width - goal.width) +
                    Mathf.Abs(animBounds.y - goal.y) + Mathf.Abs(animBounds.height - goal.height) < 0.01f)
                    animBounds = goal;
                RebuildOverlay();
            }
        }

        void RebuildOverlay()
        {
            var g = world.Grid;
            float ts = g.TileSize;
            float ax0 = animBounds.xMin * ts, az0 = animBounds.yMin * ts, ax1 = animBounds.xMax * ts, az1 = animBounds.yMax * ts;
            float X0 = -Margin, Z0 = -Margin, X1 = g.Width * ts + Margin, Z1 = g.Height * ts + Margin;
            const float y = 0.3f;
            var mb = new MeshBuilder(1);
            mb.Flat(0, X0, Z0, X1, az0, y, 1f);
            mb.Flat(0, X0, az1, X1, Z1, y, 1f);
            mb.Flat(0, X0, az0, ax0, az1, y, 1f);
            mb.Flat(0, ax1, az0, X1, az1, y, 1f);
            overlayMesh.Clear();
            var tmp = mb.ToMesh("tmp", false);
            overlayMesh.SetVertices(tmp.vertices);
            overlayMesh.SetNormals(tmp.normals);
            overlayMesh.SetUVs(0, tmp.uv);
            overlayMesh.SetTriangles(tmp.triangles, 0);
            overlayMesh.RecalculateBounds();
            Destroy(tmp);

            // Dashed border at ground level
            var bb = new MeshBuilder(1);
            const float by = 0.34f, wdt = 0.35f, dash = 3f, gap = 2f;
            void Edge(Vector3 a, Vector3 b)
            {
                var d = b - a;
                float len = d.magnitude;
                var dir = d / Mathf.Max(0.001f, len);
                var side = new Vector3(-dir.z, 0f, dir.x) * wdt * 0.5f;
                for (float s = 0f; s < len; s += dash + gap)
                {
                    var p0 = a + dir * s;
                    var p1 = a + dir * Mathf.Min(len, s + dash);
                    bb.Quad(0, p0 - side, p1 - side, p1 + side, p0 + side, 1f);
                }
            }
            Edge(new Vector3(ax0, by, az0), new Vector3(ax1, by, az0));
            Edge(new Vector3(ax1, by, az0), new Vector3(ax1, by, az1));
            Edge(new Vector3(ax1, by, az1), new Vector3(ax0, by, az1));
            Edge(new Vector3(ax0, by, az1), new Vector3(ax0, by, az0));
            var tb = bb.ToMesh("tmp", false);
            borderMesh.Clear();
            borderMesh.SetVertices(tb.vertices);
            borderMesh.SetNormals(tb.normals);
            borderMesh.SetUVs(0, tb.uv);
            borderMesh.SetTriangles(tb.triangles, 0);
            borderMesh.RecalculateBounds();
            Destroy(tb);
        }
    }
}
