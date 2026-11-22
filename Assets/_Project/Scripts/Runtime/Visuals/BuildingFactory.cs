using UnityEngine;

namespace Logistics
{
    /// <summary>Procedural architecture. All builders work in a local frame where +Z faces the street.</summary>
    public static class BuildingFactory
    {
        // City submesh layout shared by filler buildings and trees in decoration chunks.
        public const int CBark = 0, CFol1 = 1, CFol2 = 2, CFol3 = 3, CPlaster = 4, CBrick = 5, CRoofTiles = 6,
            CFlatRoof = 7, CGlass = 8, CConcrete = 9, CLot = 10, CAsphalt = 11, CSidewalk = 12, CSubCount = 13;

        public static Material[] CityMaterials(MaterialLibrary l) => new[]
        {
            l.bark, l.foliage[0], l.foliage[1], l.foliage[2], l.plaster, l.brick, l.roofTiles, l.flatRoof, l.glass, l.concrete, l.lot,
            l.asphalt, l.sidewalk
        };

        // ───────────────────────────── Depot ─────────────────────────────

        const int DLot = 0, DWall = 1, DRoof = 2, DConcrete = 3, DMetal = 4, DPaint = 5, DGlow = 6, DMarking = 7, DGlass = 8;

        public static GameObject BuildDepot(Depot d, World w, MaterialLibrary l, Transform parent)
        {
            var go = new GameObject($"Depot_{d.id}_{d.cargo}");
            go.transform.SetParent(parent, false);
            var center = d.WorldCenter(w.Grid);
            var rot = Quaternion.Euler(0f, d.facing.Yaw(), 0f);
            go.transform.SetPositionAndRotation(center, rot);
            var inv = Quaternion.Inverse(rot);
            var dockLocal = inv * (w.Grid.TileCenter(d.dockTile) - center);
            var parkLocal = inv * (w.Grid.TileCenter(d.ParkingTile) - center);

            var mb = new MeshBuilder(9);
            float sl = 1f / l.lotMeters, sw = 1f / l.warehouseWallMeters, sr = 1f / l.warehouseRoofMeters;
            const float H = 8f;
            // Lot with a low curb
            mb.Box(DLot, new Vector3(-H, -0.3f, -H), new Vector3(H, 0.08f, H), sl, MeshBuilder.Faces.Top | MeshBuilder.Faces.Sides);
            // Warehouse
            float wx = 7.3f, wz0 = -7.4f, wz1 = -0.9f, wh = 6.6f;
            mb.Box(DWall, new Vector3(-wx, 0f, wz0), new Vector3(wx, wh, wz1), sw, MeshBuilder.Faces.Sides);
            // low-pitch roof, ridge along X
            mb.GableRoof(DRoof, DWall, new Vector3(-wx, wh, wz0), new Vector3(wx, wh, wz1), 1.1f, 0.35f, sr);
            // Roll-up doors with concrete frames
            foreach (float x in new[] { -4.6f, 0f, 4.6f })
            {
                mb.Box(DMetal, new Vector3(x - 1.6f, 0.05f, wz1), new Vector3(x + 1.6f, 3.9f, wz1 + 0.06f), 0.5f,
                    MeshBuilder.Faces.North | MeshBuilder.Faces.Top);
                mb.Box(DConcrete, new Vector3(x - 1.85f, 3.9f, wz1), new Vector3(x + 1.85f, 4.15f, wz1 + 0.18f), 0.5f);
                for (float y = 0.4f; y < 3.8f; y += 0.42f)
                    mb.Box(DConcrete, new Vector3(x - 1.6f, y, wz1 + 0.06f), new Vector3(x + 1.6f, y + 0.05f, wz1 + 0.09f), 0.5f,
                        MeshBuilder.Faces.North | MeshBuilder.Faces.Top);
            }
            // Loading platform
            mb.Box(DConcrete, new Vector3(-wx, 0f, wz1), new Vector3(wx, 1.0f, wz1 + 1.3f), 0.5f,
                MeshBuilder.Faces.Top | MeshBuilder.Faces.North | MeshBuilder.Faces.East | MeshBuilder.Faces.West);
            // Cargo-color band and glowing roof sign
            mb.Box(DPaint, new Vector3(-wx - 0.02f, 4.7f, wz1 - 0.02f), new Vector3(wx + 0.02f, 5.5f, wz1 + 0.04f), 1f,
                MeshBuilder.Faces.North | MeshBuilder.Faces.East | MeshBuilder.Faces.West | MeshBuilder.Faces.Top);
            mb.Box(DPaint, new Vector3(-wx - 0.02f, 4.7f, wz0 - 0.04f), new Vector3(wx + 0.02f, 5.5f, wz0 + 0.02f), 1f,
                MeshBuilder.Faces.South);
            mb.Box(DMetal, new Vector3(-0.15f, wh + 0.4f, wz1 - 1.4f), new Vector3(0.15f, wh + 1.6f, wz1 - 1.1f), 0.5f);
            mb.Box(DMetal, new Vector3(-3.4f, wh + 0.4f, wz1 - 1.4f), new Vector3(-3.1f, wh + 1.6f, wz1 - 1.1f), 0.5f);
            mb.Box(DMetal, new Vector3(3.1f, wh + 0.4f, wz1 - 1.4f), new Vector3(3.4f, wh + 1.6f, wz1 - 1.1f), 0.5f);
            mb.Box(DGlow, new Vector3(-4.2f, wh + 1.5f, wz1 - 1.3f), new Vector3(4.2f, wh + 3.2f, wz1 - 0.95f), 1f);
            // Office annex with windows on the side of the yard opposite the dock
            float ax = dockLocal.x > 0 ? -1f : 1f;
            mb.Box(DWall, new Vector3(ax > 0 ? wx - 3.2f : -wx, 0f, wz1), new Vector3(ax > 0 ? wx : -wx + 3.2f, 3.4f, wz1 + 3.4f), sw,
                MeshBuilder.Faces.Sides | MeshBuilder.Faces.Top);
            float gx0 = ax > 0 ? wx - 2.9f : -wx + 0.3f;
            mb.Box(DGlass, new Vector3(gx0, 1.0f, wz1 + 3.4f), new Vector3(gx0 + 2.6f, 2.8f, wz1 + 3.45f), 1f, MeshBuilder.Faces.North);

            // Parking bays (painted)
            for (int i = -1; i <= 2; i++)
            {
                float x = parkLocal.x + (i - 0.5f) * 2.6f;
                mb.Flat(DMarking, x - 0.06f, parkLocal.z - 3.4f, x + 0.06f, parkLocal.z + 2.8f, 0.09f, 1f);
            }
            mb.Flat(DMarking, parkLocal.x - 4.0f, parkLocal.z - 3.46f, parkLocal.x + 4.0f, parkLocal.z - 3.34f, 0.09f, 1f);
            // Dock bay marking
            mb.Flat(DMarking, dockLocal.x - 3.3f, dockLocal.z - 0.06f + 3.8f, dockLocal.x + 3.3f, dockLocal.z + 0.06f + 3.8f, 0.09f, 1f);

            // Fence around back and sides; gate opening on the street side
            for (float t = -H + 0.3f; t <= H - 0.3f; t += 2f)
            {
                mb.Box(DMetal, new Vector3(t - 0.05f, 0f, -H + 0.2f), new Vector3(t + 0.05f, 1.9f, -H + 0.3f), 0.5f, MeshBuilder.Faces.Sides | MeshBuilder.Faces.Top);
                mb.Box(DMetal, new Vector3(-H + 0.2f, 0f, t - 0.05f), new Vector3(-H + 0.3f, 1.9f, t + 0.05f), 0.5f, MeshBuilder.Faces.Sides | MeshBuilder.Faces.Top);
                mb.Box(DMetal, new Vector3(H - 0.3f, 0f, t - 0.05f), new Vector3(H - 0.2f, 1.9f, t + 0.05f), 0.5f, MeshBuilder.Faces.Sides | MeshBuilder.Faces.Top);
            }
            mb.Box(DMetal, new Vector3(-H + 0.2f, 1.75f, -H + 0.22f), new Vector3(H - 0.2f, 1.85f, -H + 0.28f), 0.5f);
            mb.Box(DMetal, new Vector3(-H + 0.22f, 1.75f, -H + 0.2f), new Vector3(-H + 0.28f, 1.85f, H - 0.2f), 0.5f);
            mb.Box(DMetal, new Vector3(H - 0.28f, 1.75f, -H + 0.2f), new Vector3(H - 0.22f, 1.85f, H - 0.2f), 0.5f);

            Attach(go, mb.ToMesh(go.name), new[] { l.lot, l.warehouseWall, l.warehouseRoof, l.concrete, l.darkMetal, l.Paint(d.cargo), l.Glow(d.cargo), l.marking, l.glass });
            return go;
        }

        // ───────────────────────────── Store ─────────────────────────────

        const int SLot = 0, SWall = 1, SRoof = 2, SConcrete = 3, SMetal = 4, SPaint = 5, SGlow = 6, SGlass = 7;

        public static GameObject BuildStore(Store s, World w, MaterialLibrary l, Transform parent, out float roofHeight)
        {
            var go = new GameObject($"Store_{s.id}_{s.cargo}");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(w.Grid.TileCenter(s.origin), Quaternion.Euler(0f, s.facing.Yaw(), 0f));
            var rnd = new System.Random(s.id * 7919);
            bool brick = rnd.NextDouble() < 0.55;
            float H = 4.3f + (float)rnd.NextDouble() * 1.4f;
            roofHeight = H;
            var wall = brick ? l.brick : l.plaster;
            float swl = 1f / (brick ? l.brickMeters : l.plasterMeters);

            var mb = new MeshBuilder(8);
            mb.Box(SLot, new Vector3(-4f, -0.3f, -4f), new Vector3(4f, 0.08f, 4f), 1f / l.lotMeters, MeshBuilder.Faces.Top | MeshBuilder.Faces.Sides);
            float x = 3.6f, z0 = -3.8f, z1 = -1.0f;
            mb.Box(SWall, new Vector3(-x, 0f, z0), new Vector3(x, H, z1), swl, MeshBuilder.Faces.Sides);
            // Flat roof with parapet
            mb.Flat(SRoof, -x + 0.25f, z0 + 0.25f, x - 0.25f, z1 - 0.25f, H - 0.15f, 1f / l.flatRoofMeters);
            mb.Box(SConcrete, new Vector3(-x - 0.05f, H, z0 - 0.05f), new Vector3(x + 0.05f, H + 0.35f, z0 + 0.25f), 0.5f);
            mb.Box(SConcrete, new Vector3(-x - 0.05f, H, z1 - 0.25f), new Vector3(x + 0.05f, H + 0.35f, z1 + 0.05f), 0.5f);
            mb.Box(SConcrete, new Vector3(-x - 0.05f, H, z0), new Vector3(-x + 0.25f, H + 0.35f, z1), 0.5f);
            mb.Box(SConcrete, new Vector3(x - 0.25f, H, z0), new Vector3(x + 0.05f, H + 0.35f, z1), 0.5f);
            mb.Box(SWall, new Vector3(-x + 0.25f, H - 0.15f, z0 + 0.25f), new Vector3(x - 0.25f, H, z1 - 0.25f), swl, MeshBuilder.Faces.Sides);
            // Storefront glass, door, awning
            mb.Box(SGlass, new Vector3(-3.1f, 0.35f, z1), new Vector3(3.1f, 2.9f, z1 + 0.05f), 1f, MeshBuilder.Faces.North);
            for (float mx = -3.1f; mx <= 3.11f; mx += 1.55f)
                mb.Box(SMetal, new Vector3(mx - 0.05f, 0.3f, z1), new Vector3(mx + 0.05f, 2.95f, z1 + 0.09f), 0.5f, MeshBuilder.Faces.North | MeshBuilder.Faces.East | MeshBuilder.Faces.West);
            mb.Box(SMetal, new Vector3(-3.15f, 2.9f, z1), new Vector3(3.15f, 3.0f, z1 + 0.09f), 0.5f);
            mb.Box(SConcrete, new Vector3(-3.2f, 0f, z1), new Vector3(3.2f, 0.35f, z1 + 0.12f), 0.5f);
            mb.Quad(SPaint, new Vector3(-3.4f, 3.45f, z1), new Vector3(-3.4f, 2.95f, z1 + 1.1f),
                new Vector3(3.4f, 2.95f, z1 + 1.1f), new Vector3(3.4f, 3.45f, z1), 1f);
            mb.Quad(SPaint, new Vector3(3.4f, 3.45f, z1), new Vector3(3.4f, 2.95f, z1 + 1.1f),
                new Vector3(-3.4f, 2.95f, z1 + 1.1f), new Vector3(-3.4f, 3.45f, z1), 1f);
            mb.Box(SPaint, new Vector3(-3.4f, 2.65f, z1 + 1.05f), new Vector3(3.4f, 2.95f, z1 + 1.12f), 1f);
            // Upper-floor windows on the front if tall enough
            if (H > 5f)
                for (float wx = -2.6f; wx <= 2.61f; wx += 1.75f)
                    mb.Box(SGlass, new Vector3(wx - 0.45f, 3.7f, z1), new Vector3(wx + 0.45f, H - 0.5f, z1 + 0.04f), 1f, MeshBuilder.Faces.North);
            // Glowing sign on the parapet and a painted roof patch for top-down readability
            mb.Box(SGlow, new Vector3(-2.4f, H + 0.35f, z1 - 0.3f), new Vector3(2.4f, H + 1.35f, z1 - 0.05f), 1f);
            mb.Box(SMetal, new Vector3(-2.2f, H + 0.3f, z1 - 0.45f), new Vector3(-2.0f, H + 1.0f, z1 - 0.3f), 0.5f);
            mb.Box(SMetal, new Vector3(2.0f, H + 0.3f, z1 - 0.45f), new Vector3(2.2f, H + 1.0f, z1 - 0.3f), 0.5f);
            mb.Flat(SPaint, -2.8f, z0 + 0.6f, 2.8f, z1 - 1.0f, H - 0.13f, 1f);
            // Rooftop AC units
            mb.BeveledBox(SMetal, new Vector3(-2.6f, H - 0.15f, z0 + 0.5f), new Vector3(-1.5f, H + 0.7f, z0 + 1.3f), 0.08f, 0.5f);
            mb.BeveledBox(SMetal, new Vector3(1.2f, H - 0.15f, z0 + 0.5f), new Vector3(2.3f, H + 0.7f, z0 + 1.3f), 0.08f, 0.5f);
            // Bollards along the apron edge
            for (float bx = -3.5f; bx <= 3.51f; bx += 1.75f)
                mb.Cylinder(SMetal, new Vector3(bx, 0.08f, 3.7f), 0.1f, 0.8f, 8, 0.5f);

            Attach(go, mb.ToMesh(go.name), new[] { l.lot, wall, l.flatRoof, l.concrete, l.darkMetal, l.Paint(s.cargo), l.Glow(s.cargo), l.glass });
            return go;
        }

        // ───────────────────────────── Filler city ─────────────────────────────

        /// <summary>Adds a house or apartment block centered at <paramref name="center"/> into a city chunk builder.</summary>
        public static void AddFiller(MeshBuilder mb, Vector3 center, float tile, int seed, MaterialLibrary l, bool dense)
        {
            var rnd = new System.Random(seed);
            float yaw = rnd.Next(4) * 90f;
            mb.Push(Matrix4x4.TRS(center, Quaternion.Euler(0f, yaw, 0f), Vector3.one));
            float half = tile * 0.5f;
            double roll = rnd.NextDouble();
            // yard / pad
            mb.Box(CLot, new Vector3(-half + 0.3f, -0.3f, -half + 0.3f), new Vector3(half - 0.3f, 0.05f, half - 0.3f), 1f / l.lotMeters,
                MeshBuilder.Faces.Top | MeshBuilder.Faces.Sides);
            if (roll < (dense ? 0.35 : 0.6)) AddHouse(mb, rnd, l);
            else AddApartment(mb, rnd, l, dense);
            mb.Pop();
        }

        static void AddHouse(MeshBuilder mb, System.Random rnd, MaterialLibrary l)
        {
            float w = 2.6f + (float)rnd.NextDouble() * 0.6f, d = 2.2f + (float)rnd.NextDouble() * 0.5f;
            int floors = rnd.NextDouble() < 0.5 ? 1 : 2;
            float h = floors * 2.9f + 0.3f;
            bool brick = rnd.NextDouble() < 0.4;
            int wallSub = brick ? CBrick : CPlaster;
            float swl = 1f / (brick ? l.brickMeters : l.plasterMeters);
            float oz = -0.6f;
            mb.Box(wallSub, new Vector3(-w, 0f, -d + oz), new Vector3(w, h, d + oz), swl, MeshBuilder.Faces.Sides);
            mb.GableRoof(CRoofTiles, wallSub, new Vector3(-w, h, -d + oz), new Vector3(w, h, d + oz), 1.9f, 0.45f, 1f / l.roofTilesMeters);
            for (int f = 0; f < floors; f++)
            {
                float y0 = 0.9f + f * 2.9f;
                for (float x = -w + 0.9f; x <= w - 0.8f; x += 1.6f)
                {
                    mb.Box(CGlass, new Vector3(x - 0.4f, y0, d + oz), new Vector3(x + 0.4f, y0 + 1.2f, d + oz + 0.04f), 1f, MeshBuilder.Faces.North);
                    mb.Box(CGlass, new Vector3(x - 0.4f, y0, -d + oz - 0.04f), new Vector3(x + 0.4f, y0 + 1.2f, -d + oz), 1f, MeshBuilder.Faces.South);
                    mb.Box(CConcrete, new Vector3(x - 0.5f, y0 - 0.1f, d + oz), new Vector3(x + 0.5f, y0, d + oz + 0.12f), 0.5f);
                }
            }
            // hedge / garden tree
            AddTree(mb, new Vector3(w + 0.9f, 0f, 2.6f), rnd.Next(), 0.6f);
        }

        static void AddApartment(MeshBuilder mb, System.Random rnd, MaterialLibrary l, bool dense)
        {
            float w = 3.0f + (float)rnd.NextDouble() * 0.5f, d = 3.0f + (float)rnd.NextDouble() * 0.5f;
            int floors = dense ? 3 + rnd.Next(5) : 2 + rnd.Next(3);
            float fh = 3f;
            float h = floors * fh + 0.4f;
            bool brick = rnd.NextDouble() < 0.45;
            int wallSub = brick ? CBrick : CPlaster;
            float swl = 1f / (brick ? l.brickMeters : l.plasterMeters);
            mb.Box(wallSub, new Vector3(-w, 0f, -d), new Vector3(w, h, d), swl, MeshBuilder.Faces.Sides);
            mb.Flat(CFlatRoof, -w + 0.2f, -d + 0.2f, w - 0.2f, d - 0.2f, h - 0.1f, 1f / l.flatRoofMeters);
            mb.Box(CConcrete, new Vector3(-w - 0.05f, h - 0.1f, -d - 0.05f), new Vector3(w + 0.05f, h + 0.3f, -d + 0.2f), 0.5f);
            mb.Box(CConcrete, new Vector3(-w - 0.05f, h - 0.1f, d - 0.2f), new Vector3(w + 0.05f, h + 0.3f, d + 0.05f), 0.5f);
            mb.Box(CConcrete, new Vector3(-w - 0.05f, h - 0.1f, -d), new Vector3(-w + 0.2f, h + 0.3f, d), 0.5f);
            mb.Box(CConcrete, new Vector3(w - 0.2f, h - 0.1f, -d), new Vector3(w + 0.05f, h + 0.3f, d), 0.5f);
            mb.BeveledBox(CConcrete, new Vector3(-1.1f, h - 0.1f, -0.9f), new Vector3(0.9f, h + 1.6f, 0.9f), 0.1f, 0.5f);
            for (int f = 0; f < floors; f++)
            {
                float y0 = 0.8f + f * fh;
                for (float x = -w + 0.8f; x <= w - 0.7f; x += 1.5f)
                {
                    mb.Box(CGlass, new Vector3(x - 0.45f, y0, d), new Vector3(x + 0.45f, y0 + 1.55f, d + 0.04f), 1f, MeshBuilder.Faces.North);
                    mb.Box(CGlass, new Vector3(x - 0.45f, y0, -d - 0.04f), new Vector3(x + 0.45f, y0 + 1.55f, -d), 1f, MeshBuilder.Faces.South);
                    mb.Box(CGlass, new Vector3(w, y0, x - 0.45f), new Vector3(w + 0.04f, y0 + 1.55f, x + 0.45f), 1f, MeshBuilder.Faces.East);
                    mb.Box(CGlass, new Vector3(-w - 0.04f, y0, x - 0.45f), new Vector3(-w, y0 + 1.55f, x + 0.45f), 1f, MeshBuilder.Faces.West);
                }
                if (f > 0)
                    mb.Box(CConcrete, new Vector3(-w - 0.06f, f * fh + 0.05f, -d - 0.06f), new Vector3(w + 0.06f, f * fh + 0.25f, d + 0.06f), 0.5f, MeshBuilder.Faces.Sides);
            }
        }

        /// <summary>Broadleaf (clustered noisy canopy) or conifer (stacked cones) tree.</summary>
        public static void AddTree(MeshBuilder mb, Vector3 basePos, int seed, float scale = 1f)
        {
            var rnd = new System.Random(seed);
            float s = scale * (0.75f + (float)rnd.NextDouble() * 0.6f);
            if (rnd.NextDouble() < 0.28)
            {
                float th = 1.2f * s;
                mb.Cylinder(CBark, basePos, 0.2f * s, th + 1f, 6, 0.5f, false);
                int layers = 3 + rnd.Next(2);
                float y = th, r = 2.1f * s;
                for (int i = 0; i < layers; i++)
                {
                    mb.Cone(CFol3, basePos + Vector3.up * y, r, 2.6f * s, 9, 0.35f);
                    y += 1.35f * s;
                    r *= 0.74f;
                }
                return;
            }
            float trunkH = 2.0f * s;
            mb.Cylinder(CBark, basePos, 0.2f * s, trunkH + 0.8f * s, 7, 0.5f, false);
            int fol = CFol1 + rnd.Next(2);
            int blobs = 4 + rnd.Next(3);
            var crown = basePos + Vector3.up * (trunkH + 1.7f * s);
            for (int i = 0; i < blobs; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float d = (i == 0 ? 0f : 0.9f + (float)rnd.NextDouble() * 0.6f) * s;
                var off = new Vector3(Mathf.Cos(a) * d, (float)(rnd.NextDouble() - 0.3) * 1.3f * s, Mathf.Sin(a) * d);
                float rr = (i == 0 ? 1.7f : 1.05f + (float)rnd.NextDouble() * 0.45f) * s;
                mb.Blob(fol, crown + off, new Vector3(rr, rr * 0.85f, rr), 0.5f, rnd.Next());
            }
        }

        static void Attach(GameObject go, Mesh mesh, Material[] mats)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        }
    }
}
