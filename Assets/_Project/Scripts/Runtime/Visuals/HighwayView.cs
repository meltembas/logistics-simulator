using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>Elevated highway decks on piers, following the same curve trucks drive.</summary>
    public class HighwayView : MonoBehaviour
    {
        World world;
        MaterialLibrary lib;
        MeshFilter mf;
        const float HalfWidth = 3.6f;
        const int Samples = 40;

        public void Init(World w, MaterialLibrary materials)
        {
            world = w;
            lib = materials;
            mf = gameObject.AddComponent<MeshFilter>();
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { lib.asphalt, lib.concrete, lib.marking };
        }

        public void Rebuild()
        {
            var mb = new MeshBuilder(3);
            foreach (var h in world.Roads.Highways) BuildOne(mb, h);
            if (mf.sharedMesh != null) Destroy(mf.sharedMesh);
            mf.sharedMesh = mb.IsEmpty ? null : mb.ToMesh("Highways");
        }

        void BuildOne(MeshBuilder mb, Highway h)
        {
            var a = world.Grid.TileCenter(h.a);
            var b = world.Grid.TileCenter(h.b);
            RouteBuilder.HighwayControls(a, b, out var p1, out var p2);
            var center = new List<Vector3>();
            for (int i = 0; i <= Samples; i++)
                center.Add(RouteBuilder.Bezier(a, p1, p2, b, i / (float)Samples) + Vector3.up * RoadView.RoadY);

            var topL = new List<Vector3>(); var topR = new List<Vector3>();
            var botL = new List<Vector3>(); var botR = new List<Vector3>();
            var parL = new List<Vector3>(); var parR = new List<Vector3>();
            var parLi = new List<Vector3>(); var parRi = new List<Vector3>();
            var parLb = new List<Vector3>(); var parRb = new List<Vector3>();
            for (int i = 0; i <= Samples; i++)
            {
                var p = center[i];
                var t = (center[Mathf.Min(Samples, i + 1)] - center[Mathf.Max(0, i - 1)]);
                t.y = 0f;
                t.Normalize();
                var r = RouteBuilder.Right(t);
                float thick = Mathf.Lerp(0.15f, 0.9f, Mathf.Clamp01(p.y / 2f));
                topL.Add(p - r * HalfWidth); topR.Add(p + r * HalfWidth);
                botL.Add(p - r * HalfWidth + Vector3.down * thick); botR.Add(p + r * HalfWidth + Vector3.down * thick);
                float ph = Mathf.Lerp(0f, 0.85f, Mathf.Clamp01(p.y / 1.5f));
                parL.Add(p - r * HalfWidth + Vector3.up * ph); parR.Add(p + r * HalfWidth + Vector3.up * ph);
                parLi.Add(p - r * (HalfWidth - 0.3f) + Vector3.up * ph); parRi.Add(p + r * (HalfWidth - 0.3f) + Vector3.up * ph);
                parLb.Add(p - r * (HalfWidth - 0.3f)); parRb.Add(p + r * (HalfWidth - 0.3f));
            }
            float sa = 1f / lib.asphaltMeters;
            mb.Ribbon(0, topL, topR, sa);
            mb.Ribbon(1, botL, botR, 0.5f, true);
            // deck sides
            for (int i = 0; i < Samples; i++)
            {
                mb.Quad(1, botR[i], parR[i], parR[i + 1], botR[i + 1], 0.5f);
                mb.Quad(1, botL[i + 1], parL[i + 1], parL[i], botL[i], 0.5f);
                // parapet tops and inner faces
                mb.Quad(1, parL[i], parL[i + 1], parLi[i + 1], parLi[i], 0.5f);
                mb.Quad(1, parRi[i], parRi[i + 1], parR[i + 1], parR[i], 0.5f);
                mb.Quad(1, parLb[i], parLi[i], parLi[i + 1], parLb[i + 1], 0.5f);
                mb.Quad(1, parRb[i + 1], parRi[i + 1], parRi[i], parRb[i], 0.5f);
            }
            // dashed centre line
            for (int i = 0; i < Samples; i += 2)
            {
                var c0 = center[i] + Vector3.up * 0.01f; var c1 = center[i + 1] + Vector3.up * 0.01f;
                var t = c1 - c0; t.y = 0f; t.Normalize();
                var r = RouteBuilder.Right(t) * 0.08f;
                mb.Quad(2, c0 - r, c1 - r, c1 + r, c0 + r, 1f);
            }
            // piers
            for (int i = 4; i < Samples - 3; i += 5)
            {
                var p = center[i];
                if (p.y < 2.2f) continue;
                var tile = world.Grid.WorldToTile(p);
                float groundY = world.Grid.IsWater(tile) ? -1.9f : 0f;
                float top = p.y - 0.9f;
                mb.Cylinder(1, new Vector3(p.x, groundY, p.z), 0.65f, top - groundY, 14, 0.5f, false);
                mb.BoxCentered(1, new Vector3(p.x, top - 0.2f, p.z), new Vector3(2.6f, 0.4f, 2.6f), 0.5f);
            }
        }
    }
}
