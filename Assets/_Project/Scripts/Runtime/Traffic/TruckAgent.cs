using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    public enum TruckState { Parked, Departing, Outbound, Unloading, Returning, Parking }

    /// <summary>Pure simulation state of one delivery truck. Movement is driven by <see cref="TrafficSystem"/>.</summary>
    public class TruckAgent
    {
        public int id;
        public Depot depot;
        public Store target;
        public TruckState State = TruckState.Parked;
        public Route route;
        public float s;
        public float speed;
        public Vector3 position;
        public Vector3 forward = Vector3.forward;
        public bool braking;
        public float timer;
        public int parkSlot;
        public float waitTime;

        /// <summary>Indices of route spans this truck currently holds a reservation on.</summary>
        public readonly List<int> heldSpans = new List<int>();

        // Short lerp between a parking slot and the dock (Departing / Parking states)
        public Vector3 lerpFrom, lerpTo;
        public Vector3 lerpFwdFrom, lerpFwdTo;
        public float lerpDuration = 1.1f;

        public bool OnRoad => State == TruckState.Outbound || State == TruckState.Returning;
        public bool Visible => State != TruckState.Parked || parkSlot < 3;

        public bool Holds(int spanIndex) => heldSpans.Contains(spanIndex);
    }
}
