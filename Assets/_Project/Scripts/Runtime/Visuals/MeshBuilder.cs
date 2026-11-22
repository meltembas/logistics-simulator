using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Logistics
{
    /// <summary>
    /// Small procedural mesh kit with planar world-scale UVs (so tiled PBR textures keep their real size)
    /// and multiple submeshes (one per material). Front faces are clockwise, as Unity expects.
    /// </summary>
    public class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int>[] subs;

        /// <summary>Transform applied to everything added (push/pop with <see cref="Push"/>).</summary>
        public Matrix4x4 M = Matrix4x4.identity;
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();

        public MeshBuilder(int submeshCount)
        {
            subs = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++) subs[i] = new List<int>();
        }

        public int VertexCount => verts.Count;
        public bool IsEmpty => verts.Count == 0;

        public void Push(Matrix4x4 local)
        {
            stack.Push(M);
            M = M * local;
        }

        public void Push(Vector3 pos, Quaternion rot) => Push(Matrix4x4.TRS(pos, rot, Vector3.one));
        public void Pop() { M = stack.Pop(); }

        Vector2 PlanarUV(Vector3 p, Vector3 n, float s)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az) return new Vector2(p.x * s, p.z * s);
            if (ax >= az) return new Vector2(p.z * s, p.y * s);
            return new Vector2(p.x * s, p.y * s);
        }

        /// <summary>Quad a→b→c→d clockwise as seen from the front.</summary>
        public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float uvScale)
        {
            a = M.MultiplyPoint3x4(a); b = M.MultiplyPoint3x4(b);
            c = M.MultiplyPoint3x4(c); d = M.MultiplyPoint3x4(d);
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f) n = Vector3.Cross(c - a, d - a);
            n.Normalize();
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            for (int k = 0; k < 4; k++) normals.Add(n);
            uvs.Add(PlanarUV(a, n, uvScale)); uvs.Add(PlanarUV(b, n, uvScale));
            uvs.Add(PlanarUV(c, n, uvScale)); uvs.Add(PlanarUV(d, n, uvScale));
            var s = subs[sub];
            s.Add(i); s.Add(i + 1); s.Add(i + 2);
            s.Add(i); s.Add(i + 2); s.Add(i + 3);
        }

        public void Triangle(int sub, Vector3 a, Vector3 b, Vector3 c, float uvScale)
        {
            a = M.MultiplyPoint3x4(a); b = M.MultiplyPoint3x4(b); c = M.MultiplyPoint3x4(c);
            var n = Vector3.Cross(b - a, c - a).normalized;
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            uvs.Add(PlanarUV(a, n, uvScale)); uvs.Add(PlanarUV(b, n, uvScale)); uvs.Add(PlanarUV(c, n, uvScale));
            subs[sub].Add(i); subs[sub].Add(i + 1); subs[sub].Add(i + 2);
        }

        /// <summary>Horizontal rectangle facing up at height y.</summary>
        public void Flat(int sub, float x0, float z0, float x1, float z1, float y, float uvScale)
        {
            Quad(sub, new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), uvScale);
        }

        [System.Flags]
        public enum Faces { None = 0, Top = 1, Bottom = 2, North = 4, South = 8, East = 16, West = 32, All = 63, Sides = 60 }

        /// <summary>Axis-aligned box (in the current transform) from min to max corner.</summary>
        public void Box(int sub, Vector3 min, Vector3 max, float uvScale, Faces faces = Faces.All)
        {
            var p000 = new Vector3(min.x, min.y, min.z); var p100 = new Vector3(max.x, min.y, min.z);
            var p010 = new Vector3(min.x, max.y, min.z); var p110 = new Vector3(max.x, max.y, min.z);
            var p001 = new Vector3(min.x, min.y, max.z); var p101 = new Vector3(max.x, min.y, max.z);
            var p011 = new Vector3(min.x, max.y, max.z); var p111 = new Vector3(max.x, max.y, max.z);
            if ((faces & Faces.Top) != 0) Quad(sub, p010, p011, p111, p110, uvScale);
            if ((faces & Faces.Bottom) != 0) Quad(sub, p000, p100, p101, p001, uvScale);
            if ((faces & Faces.South) != 0) Quad(sub, p000, p010, p110, p100, uvScale);
            if ((faces & Faces.North) != 0) Quad(sub, p101, p111, p011, p001, uvScale);
            if ((faces & Faces.West) != 0) Quad(sub, p001, p011, p010, p000, uvScale);
            if ((faces & Faces.East) != 0) Quad(sub, p100, p110, p111, p101, uvScale);
        }

        public void BoxCentered(int sub, Vector3 center, Vector3 size, float uvScale, Faces faces = Faces.All)
        {
            Box(sub, center - size * 0.5f, center + size * 0.5f, uvScale, faces);
        }

        /// <summary>Box with a chamfered top edge band — catches light like a manufactured edge.</summary>
        public void BeveledBox(int sub, Vector3 min, Vector3 max, float bevel, float uvScale)
        {
            float b = Mathf.Min(bevel, (max.x - min.x) * 0.45f, (max.z - min.z) * 0.45f, (max.y - min.y) * 0.45f);
            Box(sub, min, new Vector3(max.x, max.y - b, max.z), uvScale, Faces.Sides | Faces.Bottom);
            float y0 = max.y - b, y1 = max.y;
            var a0 = new Vector3(min.x, y0, min.z); var a1 = new Vector3(max.x, y0, min.z);
            var a2 = new Vector3(max.x, y0, max.z); var a3 = new Vector3(min.x, y0, max.z);
            var t0 = new Vector3(min.x + b, y1, min.z + b); var t1 = new Vector3(max.x - b, y1, min.z + b);
            var t2 = new Vector3(max.x - b, y1, max.z - b); var t3 = new Vector3(min.x + b, y1, max.z - b);
            Quad(sub, t0, t3, t2, t1, uvScale);
            Quad(sub, a0, t0, t1, a1, uvScale);
            Quad(sub, a1, t1, t2, a2, uvScale);
            Quad(sub, a2, t2, t3, a3, uvScale);
            Quad(sub, a3, t3, t0, a0, uvScale);
        }

        /// <summary>Vertical cylinder along +Y with smooth side normals.</summary>
        public void Cylinder(int sub, Vector3 baseCenter, float radius, float height, int segments, float uvScale, bool top = true, bool bottom = false)
        {
            int start = verts.Count;
            var nm = M;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var p0 = nm.MultiplyPoint3x4(baseCenter + dir * radius);
                var p1 = nm.MultiplyPoint3x4(baseCenter + dir * radius + Vector3.up * height);
                var n = nm.MultiplyVector(dir).normalized;
                float u = i / (float)segments * radius * Mathf.PI * 2f * uvScale;
                verts.Add(p0); normals.Add(n); uvs.Add(new Vector2(u, 0f));
                verts.Add(p1); normals.Add(n); uvs.Add(new Vector2(u, height * uvScale));
            }
            var s = subs[sub];
            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;
                s.Add(a); s.Add(a + 1); s.Add(a + 3);
                s.Add(a); s.Add(a + 3); s.Add(a + 2);
            }
            if (top) Disc(sub, baseCenter + Vector3.up * height, radius, segments, uvScale, true);
            if (bottom) Disc(sub, baseCenter, radius, segments, uvScale, false);
        }

        public void Disc(int sub, Vector3 center, float radius, int segments, float uvScale, bool up)
        {
            var c = center;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                var p0 = c + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var p1 = c + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                if (up) Triangle(sub, c, p1, p0, uvScale);
                else Triangle(sub, c, p0, p1, uvScale);
            }
        }

        /// <summary>Upright cone with smooth-ish side normals (conifer layers, bollard caps).</summary>
        public void Cone(int sub, Vector3 baseCenter, float radius, float height, int segments, float uvScale)
        {
            var tip = baseCenter + Vector3.up * height;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                var p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                Triangle(sub, p0, tip, p1, uvScale);
            }
            Disc(sub, baseCenter, radius, segments, uvScale, false);
        }

        /// <summary>Flat ring (annulus) facing up, optionally partial (fill 0..1 clockwise from +Z).</summary>
        public void Ring(int sub, Vector3 center, float r0, float r1, int segments, float fill = 1f)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(segments * Mathf.Clamp01(fill)));
            float total = Mathf.Clamp01(fill) * Mathf.PI * 2f;
            for (int i = 0; i < count; i++)
            {
                float a0 = Mathf.PI * 0.5f - total * i / count;
                float a1 = Mathf.PI * 0.5f - total * (i + 1) / count;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                Quad(sub, center + d0 * r0, center + d0 * r1, center + d1 * r1, center + d1 * r0, 1f);
            }
        }

        /// <summary>Gable roof prism over a rectangle, ridge along local X.</summary>
        public void GableRoof(int sub, int gableSub, Vector3 min, Vector3 max, float ridgeHeight, float overhang, float uvScale)
        {
            float x0 = min.x - overhang, x1 = max.x + overhang;
            float z0 = min.z - overhang, z1 = max.z + overhang;
            float zm = (min.z + max.z) * 0.5f;
            float y = min.y, yr = min.y + ridgeHeight;
            var a = new Vector3(x0, y, z0); var b = new Vector3(x1, y, z0);
            var c = new Vector3(x1, y, z1); var d = new Vector3(x0, y, z1);
            var r0 = new Vector3(x0, yr, zm); var r1 = new Vector3(x1, yr, zm);
            Quad(sub, a, r0, r1, b, uvScale);
            Quad(sub, c, r1, r0, d, uvScale);
            // gable ends (walls)
            Triangle(gableSub, new Vector3(min.x, y, min.z), new Vector3(min.x, y, max.z), new Vector3(min.x, yr - overhang * 0.5f, zm), uvScale);
            Triangle(gableSub, new Vector3(max.x, y, max.z), new Vector3(max.x, y, min.z), new Vector3(max.x, yr - overhang * 0.5f, zm), uvScale);
            // soffit
            Quad(sub, a, b, c, d, uvScale);
        }

        /// <summary>Noise-displaced blob (subdivided octahedron) — used for tree canopies.</summary>
        public void Blob(int sub, Vector3 center, Vector3 radii, float noise, int seed)
        {
            var rnd = new System.Random(seed);
            float ox = (float)rnd.NextDouble() * 50f, oy = (float)rnd.NextDouble() * 50f;
            Vector3[] baseV = { Vector3.up, Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.down };
            int[,] faces = { { 0, 1, 2 }, { 0, 2, 3 }, { 0, 3, 4 }, { 0, 4, 1 }, { 5, 2, 1 }, { 5, 3, 2 }, { 5, 4, 3 }, { 5, 1, 4 } };
            for (int f = 0; f < 8; f++)
            {
                var a = baseV[faces[f, 0]]; var b = baseV[faces[f, 1]]; var c = baseV[faces[f, 2]];
                const int div = 3;
                for (int i = 0; i < div; i++)
                for (int j = 0; j < div - i; j++)
                {
                    var p0 = Tri(a, b, c, i, j, div); var p1 = Tri(a, b, c, i + 1, j, div); var p2 = Tri(a, b, c, i, j + 1, div);
                    EmitBlobTri(sub, center, radii, noise, ox, oy, p0, p1, p2);
                    if (i + j < div - 1)
                    {
                        var p3 = Tri(a, b, c, i + 1, j + 1, div);
                        EmitBlobTri(sub, center, radii, noise, ox, oy, p1, p3, p2);
                    }
                }
            }
        }

        static Vector3 Tri(Vector3 a, Vector3 b, Vector3 c, int i, int j, int div)
        {
            float u = i / (float)div, v = j / (float)div;
            return (a + (b - a) * u + (c - a) * v).normalized;
        }

        void EmitBlobTri(int sub, Vector3 center, Vector3 radii, float noise, float ox, float oy, Vector3 d0, Vector3 d1, Vector3 d2)
        {
            Vector3 P(Vector3 d)
            {
                float n = Mathf.PerlinNoise(d.x * 2.1f + ox, d.z * 2.1f + d.y * 1.3f + oy) - 0.5f;
                var p = Vector3.Scale(d, radii) * (1f + n * noise);
                return center + p;
            }
            var a = M.MultiplyPoint3x4(P(d0)); var b = M.MultiplyPoint3x4(P(d1)); var c = M.MultiplyPoint3x4(P(d2));
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            // smooth-ish normals from the sphere direction, slightly faceted by face normal
            var fn = Vector3.Cross(b - a, c - a).normalized;
            normals.Add(Vector3.Lerp(M.MultiplyVector(d0).normalized, fn, 0.35f).normalized);
            normals.Add(Vector3.Lerp(M.MultiplyVector(d1).normalized, fn, 0.35f).normalized);
            normals.Add(Vector3.Lerp(M.MultiplyVector(d2).normalized, fn, 0.35f).normalized);
            uvs.Add(new Vector2(a.x * 0.35f, a.z * 0.35f + a.y * 0.2f));
            uvs.Add(new Vector2(b.x * 0.35f, b.z * 0.35f + b.y * 0.2f));
            uvs.Add(new Vector2(c.x * 0.35f, c.z * 0.35f + c.y * 0.2f));
            subs[sub].Add(i); subs[sub].Add(i + 1); subs[sub].Add(i + 2);
        }

        /// <summary>Raw triangle-strip helper for ribbons (e.g. highway decks): left/right edge points.</summary>
        public void Ribbon(int sub, IList<Vector3> left, IList<Vector3> right, float uvScale, bool flip = false)
        {
            for (int i = 0; i < left.Count - 1; i++)
            {
                if (!flip) Quad(sub, left[i], left[i + 1], right[i + 1], right[i], uvScale);
                else Quad(sub, right[i], right[i + 1], left[i + 1], left[i], uvScale);
            }
        }

        public Mesh ToMesh(string name, bool tangents = true)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            int used = 0;
            foreach (var s in subs) if (s.Count > 0) used++;
            mesh.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) mesh.SetTriangles(subs[i], i, false);
            mesh.RecalculateBounds();
            if (tangents && verts.Count > 0) mesh.RecalculateTangents();
            return mesh;
        }

        public void Clear()
        {
            verts.Clear(); normals.Clear(); uvs.Clear();
            foreach (var s in subs) s.Clear();
            M = Matrix4x4.identity;
            stack.Clear();
        }
    }
}
