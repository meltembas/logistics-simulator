using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    /// <summary>Box truck mesh (cab + cargo body in the cargo color), shared per cargo type.</summary>
    public static class TruckFactory
    {
        public const int Cab = 0, Body = 1, Glass = 2, Rubber = 3, Chassis = 4, Head = 5, Brake = 6, Stripe = 7, Metal = 8;
        static readonly Dictionary<int, Mesh> cache = new Dictionary<int, Mesh>();

        public static Mesh GetMesh()
        {
            if (cache.TryGetValue(0, out var m) && m != null) return m;
            m = Build();
            cache[0] = m;
            return m;
        }

        public static Material[] Materials(MaterialLibrary l, CargoType c, bool braking) => new[]
        {
            l.cabPaint, l.Paint(c), l.glass, l.rubber, l.chassis, l.headlight, braking ? l.brakeOn : l.brakeOff, l.marking, l.darkMetal
        };

        static Mesh Build()
        {
            var mb = new MeshBuilder(9);
            // Chassis and bumpers
            mb.Box(Chassis, new Vector3(-1.02f, 0.42f, -3.2f), new Vector3(1.02f, 0.86f, 3.15f), 1f);
            mb.Box(Chassis, new Vector3(-1.15f, 0.55f, 3.15f), new Vector3(1.15f, 0.88f, 3.38f), 1f);
            mb.Box(Chassis, new Vector3(-1.15f, 0.5f, -3.38f), new Vector3(1.15f, 0.8f, -3.2f), 1f);
            // Cab
            mb.BeveledBox(Cab, new Vector3(-1.17f, 0.86f, 1.55f), new Vector3(1.17f, 2.85f, 3.25f), 0.14f, 1f);
            mb.Box(Metal, new Vector3(-0.9f, 0.92f, 3.25f), new Vector3(0.9f, 1.6f, 3.29f), 1f, MeshBuilder.Faces.North);
            mb.Quad(Glass, new Vector3(-1.02f, 1.75f, 3.27f), new Vector3(-0.98f, 2.66f, 3.12f),
                new Vector3(0.98f, 2.66f, 3.12f), new Vector3(1.02f, 1.75f, 3.27f), 1f);
            mb.Box(Glass, new Vector3(1.17f, 1.8f, 2.05f), new Vector3(1.19f, 2.6f, 3.05f), 1f, MeshBuilder.Faces.East);
            mb.Box(Glass, new Vector3(-1.19f, 1.8f, 2.05f), new Vector3(-1.17f, 2.6f, 3.05f), 1f, MeshBuilder.Faces.West);
            mb.Box(Metal, new Vector3(1.17f, 2.1f, 2.95f), new Vector3(1.42f, 2.55f, 3.02f), 1f);
            mb.Box(Metal, new Vector3(-1.42f, 2.1f, 2.95f), new Vector3(-1.17f, 2.55f, 3.02f), 1f);
            mb.Box(Head, new Vector3(-1.0f, 1.02f, 3.26f), new Vector3(-0.62f, 1.3f, 3.31f), 1f, MeshBuilder.Faces.North);
            mb.Box(Head, new Vector3(0.62f, 1.02f, 3.26f), new Vector3(1.0f, 1.3f, 3.31f), 1f, MeshBuilder.Faces.North);
            // Cargo body
            mb.BeveledBox(Body, new Vector3(-1.22f, 0.86f, -3.3f), new Vector3(1.22f, 3.3f, 1.45f), 0.08f, 1f);
            mb.Box(Stripe, new Vector3(1.22f, 1.25f, -3.2f), new Vector3(1.235f, 1.5f, 1.35f), 1f, MeshBuilder.Faces.East);
            mb.Box(Stripe, new Vector3(-1.235f, 1.25f, -3.2f), new Vector3(-1.22f, 1.5f, 1.35f), 1f, MeshBuilder.Faces.West);
            mb.Box(Metal, new Vector3(-0.03f, 0.95f, -3.315f), new Vector3(0.03f, 3.2f, -3.3f), 1f, MeshBuilder.Faces.South);
            mb.Box(Brake, new Vector3(-1.1f, 0.95f, -3.33f), new Vector3(-0.75f, 1.2f, -3.3f), 1f, MeshBuilder.Faces.South | MeshBuilder.Faces.Top);
            mb.Box(Brake, new Vector3(0.75f, 0.95f, -3.33f), new Vector3(1.1f, 1.2f, -3.3f), 1f, MeshBuilder.Faces.South | MeshBuilder.Faces.Top);
            // Wheels (axis along X)
            foreach (float z in new[] { 2.3f, -1.55f, -2.6f })
            foreach (float x in new[] { -1.0f, 1.0f })
            {
                mb.Push(Matrix4x4.TRS(new Vector3(x, 0.5f, z), Quaternion.Euler(0f, 0f, 90f), Vector3.one));
                mb.Cylinder(Rubber, new Vector3(0f, -0.19f, 0f), 0.5f, 0.38f, 14, 1f, true, true);
                mb.Pop();
            }
            return mb.ToMesh("Truck");
        }
    }

    /// <summary>Renders one <see cref="TruckAgent"/>: pose, ramp pitch, body roll, brake lights.</summary>
    public class TruckView : MonoBehaviour
    {
        [System.NonSerialized] public TruckAgent agent;
        MaterialLibrary lib;
        Transform body;
        MeshRenderer mr;
        Material[] matsOff, matsOn;
        bool brakeShown;
        float roll, pitch, prevSpeed, prevYaw;
        Quaternion shownRot;
        bool initialized;

        public static TruckView Create(TruckAgent a, MaterialLibrary l, Transform parent)
        {
            var go = new GameObject($"Truck_{a.id}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<TruckView>();
            v.agent = a;
            v.lib = l;
            var b = new GameObject("Body");
            b.transform.SetParent(go.transform, false);
            v.body = b.transform;
            b.transform.localScale = Vector3.one * 0.86f;
            b.AddComponent<MeshFilter>().sharedMesh = TruckFactory.GetMesh();
            v.mr = b.AddComponent<MeshRenderer>();
            v.matsOff = TruckFactory.Materials(l, a.depot.cargo, false);
            v.matsOn = TruckFactory.Materials(l, a.depot.cargo, true);
            v.mr.sharedMaterials = v.matsOff;
            v.Snap();
            return v;
        }

        void Snap()
        {
            transform.position = agent.position;
            shownRot = Quaternion.LookRotation(agent.forward, Vector3.up);
            transform.rotation = shownRot;
            prevYaw = shownRot.eulerAngles.y;
            initialized = true;
        }

        void LateUpdate()
        {
            if (agent == null) return;
            bool visible = agent.Visible;
            if (mr.enabled != visible) mr.enabled = visible;
            if (!visible) return;
            if (!initialized) Snap();

            float dt = Mathf.Max(1e-4f, Time.deltaTime);
            var fwd = agent.forward;
            float rampPitch = 0f;
            if (agent.OnRoad && agent.route != null)
            {
                float y0 = agent.route.PositionAt(agent.s - 1.6f).y, y1 = agent.route.PositionAt(agent.s + 1.6f).y;
                rampPitch = -Mathf.Atan2(y1 - y0, 3.2f) * Mathf.Rad2Deg;
            }
            var target = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(rampPitch, 0f, 0f);
            shownRot = Quaternion.Slerp(shownRot, target, 1f - Mathf.Exp(-dt * 18f));
            transform.SetPositionAndRotation(agent.position, shownRot);

            float yaw = shownRot.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(prevYaw, yaw) / dt;
            prevYaw = yaw;
            float accel = (agent.speed - prevSpeed) / dt;
            prevSpeed = agent.speed;
            float targetRoll = Mathf.Clamp(-yawRate * agent.speed * 0.0016f, -4.5f, 4.5f);
            float targetPitch = Mathf.Clamp(-accel * 0.35f, -2.5f, 2.5f);
            roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-dt * 6f));
            pitch = Mathf.Lerp(pitch, targetPitch, 1f - Mathf.Exp(-dt * 6f));
            body.localRotation = Quaternion.Euler(pitch, 0f, roll);

            bool brake = agent.braking || agent.State == TruckState.Unloading;
            if (brake != brakeShown)
            {
                brakeShown = brake;
                mr.sharedMaterials = brake ? matsOn : matsOff;
            }
        }
    }
}
