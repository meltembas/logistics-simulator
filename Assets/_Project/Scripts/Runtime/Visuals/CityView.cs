using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>Creates and owns the views for depots, stores and trucks as the world spawns them, plus feedback FX.</summary>
    public class CityView : MonoBehaviour
    {
        World world;
        MaterialLibrary lib;
        Camera cam;
        Transform buildingsRoot, trucksRoot, fxRoot;
        readonly Dictionary<Store, StoreView> stores = new Dictionary<Store, StoreView>();
        readonly List<Fx> fx = new List<Fx>();
        static Mesh fxRing;
        MaterialPropertyBlock mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        struct Fx
        {
            public Transform t;
            public MeshRenderer r;
            public float age, life, r0, r1;
            public Color color;
        }

        public void Bind(World w, MaterialLibrary materials, Camera camera)
        {
            Unbind();
            world = w;
            lib = materials;
            cam = camera;
            mpb = new MaterialPropertyBlock();
            foreach (Transform c in transform) Destroy(c.gameObject);
            stores.Clear();
            fx.Clear();
            buildingsRoot = NewRoot("Buildings");
            trucksRoot = NewRoot("Trucks");
            fxRoot = NewRoot("Fx");
            if (fxRing == null)
            {
                var mb = new MeshBuilder(1);
                mb.Ring(0, Vector3.zero, 0.86f, 1f, 40);
                fxRing = mb.ToMesh("FxRing", false);
            }
            w.DepotAdded += OnDepot;
            w.StoreAdded += OnStore;
            w.TruckAdded += OnTruck;
            w.Delivered += OnDelivered;
            w.Editor.RoadPlaced += OnRoadPlaced;
            w.Editor.RoadRemoved += OnRoadRemoved;
            foreach (var d in w.Depots) OnDepot(d);
            foreach (var s in w.Stores) OnStore(s);
            foreach (var t in w.Traffic.Trucks) OnTruck(t);
        }

        void Unbind()
        {
            if (world == null) return;
            world.DepotAdded -= OnDepot;
            world.StoreAdded -= OnStore;
            world.TruckAdded -= OnTruck;
            world.Delivered -= OnDelivered;
            world.Editor.RoadPlaced -= OnRoadPlaced;
            world.Editor.RoadRemoved -= OnRoadRemoved;
            world = null;
        }

        void OnDestroy() => Unbind();

        Transform NewRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        void OnDepot(Depot d)
        {
            DepotView.Create(d, world, lib, buildingsRoot);
            Burst(d.WorldCenter(world.Grid), CargoInfo.ColorOf(d.cargo), 4f, 14f, 1.1f);
        }

        void OnStore(Store s)
        {
            var v = StoreView.Create(s, world, lib, buildingsRoot);
            v.cam = cam;
            stores[s] = v;
            Burst(world.Grid.TileCenter(s.origin), CargoInfo.ColorOf(s.cargo), 2f, 9f, 0.9f);
        }

        void OnTruck(TruckAgent t) => TruckView.Create(t, lib, trucksRoot);

        void OnDelivered(Store s, TruckAgent t)
        {
            var p = world.Grid.TileCenter(s.origin);
            Burst(p, CargoInfo.ColorOf(s.cargo), 1.5f, 7.5f, 0.6f);
        }

        void OnRoadPlaced(TileCoord t) => Burst(world.Grid.TileCenter(t), Palette.Marking.WithAlpha(0.6f), 1.5f, 4.6f, 0.35f);
        void OnRoadRemoved(TileCoord t) => Burst(world.Grid.TileCenter(t), Palette.UiDanger.WithAlpha(0.6f), 4.5f, 1.5f, 0.3f);

        public void Burst(Vector3 pos, Color color, float r0, float r1, float life)
        {
            var go = new GameObject("FxRing");
            go.transform.SetParent(fxRoot, false);
            go.transform.position = pos + Vector3.up * 0.45f;
            go.AddComponent<MeshFilter>().sharedMesh = fxRing;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = lib.fx;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fx.Add(new Fx { t = go.transform, r = r, life = life, r0 = r0, r1 = r1, color = color });
            go.transform.localScale = Vector3.one * r0;
        }

        void LateUpdate()
        {
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                var f = fx[i];
                f.age += Time.deltaTime;
                float k = Mathf.Clamp01(f.age / f.life);
                float e = 1f - (1f - k) * (1f - k);
                f.t.localScale = Vector3.one * Mathf.Lerp(f.r0, f.r1, e);
                var c = f.color;
                c.a *= 1f - k;
                f.r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColor, c);
                f.r.SetPropertyBlock(mpb);
                if (k >= 1f)
                {
                    Destroy(f.t.gameObject);
                    fx.RemoveAt(i);
                }
                else fx[i] = f;
            }
        }
    }
}
