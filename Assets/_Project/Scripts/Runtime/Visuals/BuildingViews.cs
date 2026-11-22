using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>Rises a freshly placed building out of the ground with a small overshoot.</summary>
    public class SpawnRise : MonoBehaviour
    {
        float t;
        const float Duration = 0.7f;

        void Awake() => transform.localScale = new Vector3(1f, 0.01f, 1f);

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Duration);
            float y = k < 1f ? 1f + Mathf.Sin(k * Mathf.PI) * 0.12f * (1f - k) : 1f;
            y *= Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 1.6f));
            transform.localScale = new Vector3(1f, Mathf.Max(0.01f, y), 1f);
            if (k >= 1f) { transform.localScale = Vector3.one; Destroy(this); }
        }
    }

    /// <summary>Store visuals plus its order pins and overflow ring.</summary>
    public class StoreView : MonoBehaviour
    {
        [System.NonSerialized] public Store store;
        MaterialLibrary lib;
        float roofHeight;
        Transform pinsRoot;
        readonly List<Transform> pins = new List<Transform>();
        readonly List<float> pinAge = new List<float>();
        MeshFilter ringFill;
        MeshRenderer ringFillR, ringBaseR;
        MaterialPropertyBlock mpb;
        float shownFill = -1f;
        static Mesh pinMesh, ringMesh, badgeMesh;
        Transform badge;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public Camera cam;

        public static StoreView Create(Store s, World w, MaterialLibrary l, Transform parent)
        {
            var root = new GameObject($"StoreView_{s.id}");
            root.transform.SetParent(parent, false);
            root.transform.position = w.Grid.TileCenter(s.origin);
            var v = root.AddComponent<StoreView>();
            v.store = s;
            v.lib = l;
            var building = BuildingFactory.BuildStore(s, w, l, root.transform, out v.roofHeight);
            building.AddComponent<SpawnRise>();
            v.BuildIndicators();
            return v;
        }

        void BuildIndicators()
        {
            mpb = new MaterialPropertyBlock();
            if (pinMesh == null)
            {
                var mb = new MeshBuilder(1);
                mb.BeveledBox(0, new Vector3(-0.45f, -0.45f, -0.45f), new Vector3(0.45f, 0.45f, 0.45f), 0.1f, 1f);
                pinMesh = mb.ToMesh("Pin");
            }
            if (ringMesh == null)
            {
                var mb = new MeshBuilder(1);
                mb.Ring(0, Vector3.zero, 4.7f, 5.3f, 48);
                ringMesh = mb.ToMesh("RingBase", false);
            }
            pinsRoot = new GameObject("Pins").transform;
            pinsRoot.SetParent(transform, false);
            pinsRoot.localPosition = new Vector3(0f, roofHeight + 2.8f, 0f);

            // Dark rounded badge behind the order crates: reads as the UI layer, not part of the building.
            if (badgeMesh == null)
            {
                var mb = new MeshBuilder(1);
                const int seg = 6;
                float r = 0.5f;
                var outline = new System.Collections.Generic.List<Vector3>();
                foreach (var c in new[] { new Vector2(0.5f - r, 0.5f - r), new Vector2(-0.5f + r, 0.5f - r), new Vector2(-0.5f + r, -0.5f + r), new Vector2(0.5f - r, -0.5f + r) })
                {
                    int q = outline.Count / (seg + 1);
                    for (int i = 0; i <= seg; i++)
                    {
                        float a = (q * 90f + i * 90f / seg) * Mathf.Deg2Rad;
                        outline.Add(new Vector3(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r, 0f));
                    }
                }
                for (int i = 0; i < outline.Count; i++)
                    mb.Triangle(0, Vector3.zero, outline[(i + 1) % outline.Count], outline[i], 1f);
                badgeMesh = mb.ToMesh("Badge", false);
            }
            badge = new GameObject("Badge").transform;
            badge.SetParent(pinsRoot, false);
            badge.localPosition = new Vector3(0f, 0f, 0.55f);
            badge.gameObject.AddComponent<MeshFilter>().sharedMesh = badgeMesh;
            var br = badge.gameObject.AddComponent<MeshRenderer>();
            br.sharedMaterial = lib.ringBase;
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var rb = new GameObject("RingBase");
            rb.transform.SetParent(transform, false);
            rb.transform.localPosition = new Vector3(0f, 0.36f, 0f);
            rb.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            ringBaseR = rb.AddComponent<MeshRenderer>();
            ringBaseR.sharedMaterial = lib.ringBase;
            ringBaseR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var rf = new GameObject("RingFill");
            rf.transform.SetParent(transform, false);
            rf.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            ringFill = rf.AddComponent<MeshFilter>();
            ringFill.sharedMesh = new Mesh { name = "RingFill" };
            ringFillR = rf.AddComponent<MeshRenderer>();
            ringFillR.sharedMaterial = lib.ringFill;
            ringFillR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void PopDelivered()
        {
            // the oldest pin flies up and fades (handled by removing; FxView adds the burst)
        }

        void LateUpdate()
        {
            if (store == null) return;
            // Pins: one per undelivered order (cap 8). Assigned ones sit smaller — a truck is on its way.
            int want = Mathf.Min(8, store.undelivered);
            while (pins.Count < want)
            {
                var p = new GameObject("Pin");
                p.transform.SetParent(pinsRoot, false);
                p.AddComponent<MeshFilter>().sharedMesh = pinMesh;
                var r = p.AddComponent<MeshRenderer>();
                r.sharedMaterial = lib.Glow(store.cargo);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pins.Add(p.transform);
                pinAge.Add(0f);
            }
            while (pins.Count > want)
            {
                Destroy(pins[0].gameObject);
                pins.RemoveAt(0);
                pinAge.RemoveAt(0);
            }

            float camDist = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : 100f;
            float scale = Mathf.Clamp(camDist / 95f, 1f, 2.4f);
            pinsRoot.localScale = Vector3.one * scale;
            if (cam != null)
            {
                var toCam = cam.transform.position - pinsRoot.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.01f) pinsRoot.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
            }

            float spacing = 1.15f;
            int n = pins.Count;
            badge.gameObject.SetActive(n > 0);
            if (n > 0) badge.localScale = new Vector3(n * spacing + 0.45f, 1.55f, 1f);
            for (int i = 0; i < n; i++)
            {
                pinAge[i] += Time.deltaTime;
                float age = pinAge[i];
                float pop = age < 0.35f ? Mathf.Sin(Mathf.Clamp01(age / 0.35f) * Mathf.PI * 0.75f) * 1.25f : 1f;
                bool assigned = i < store.assigned;
                float s = pop * (assigned ? 0.72f : 1f);
                var x = (i - (n - 1) * 0.5f) * spacing;
                float bob = Mathf.Sin(Time.time * 2.2f + i * 0.7f) * 0.08f;
                pins[i].localPosition = new Vector3(x, bob, 0f);
                pins[i].localScale = Vector3.one * s;
                pins[i].localRotation = Quaternion.Euler(0f, 18f, 0f);
            }

            // Overflow ring
            float fill = store.overflow;
            bool showRing = fill > 0.001f;
            ringBaseR.enabled = showRing;
            ringFillR.enabled = showRing;
            if (showRing)
            {
                if (Mathf.Abs(fill - shownFill) > 0.004f)
                {
                    shownFill = fill;
                    var mb = new MeshBuilder(1);
                    mb.Ring(0, Vector3.zero, 4.7f, 5.3f, 48, fill);
                    var m = mb.ToMesh("RingFill", false);
                    ringFill.sharedMesh.Clear();
                    ringFill.sharedMesh.SetVertices(m.vertices);
                    ringFill.sharedMesh.SetNormals(m.normals);
                    ringFill.sharedMesh.SetTriangles(m.triangles, 0);
                    ringFill.sharedMesh.RecalculateBounds();
                    Destroy(m);
                }
                var c = Color.Lerp(Palette.UiAccent, Palette.UiDanger, Mathf.SmoothStep(0.3f, 0.8f, fill));
                if (fill > 0.6f) c.a = 0.75f + 0.25f * Mathf.Sin(Time.time * 9f);
                ringFillR.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColor, c);
                ringFillR.SetPropertyBlock(mpb);
            }
        }
    }

    public class DepotView : MonoBehaviour
    {
        [System.NonSerialized] public Depot depot;

        public static DepotView Create(Depot d, World w, MaterialLibrary l, Transform parent)
        {
            var root = new GameObject($"DepotView_{d.id}");
            root.transform.SetParent(parent, false);
            var v = root.AddComponent<DepotView>();
            v.depot = d;
            var b = BuildingFactory.BuildDepot(d, w, l, root.transform);
            b.AddComponent<SpawnRise>();
            return v;
        }
    }
}
