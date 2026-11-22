using System.Collections.Generic;
using UnityEngine;

namespace Logistics
{
    public abstract class Building
    {
        public int id;
        public CargoType cargo;
        /// <summary>Lowest-x, lowest-y tile of the footprint.</summary>
        public TileCoord origin;
        public Vector2Int size;
        /// <summary>Footprint tile where trucks turn around and load/unload.</summary>
        public TileCoord dockTile;
        /// <summary>Direction from <see cref="dockTile"/> to <see cref="entrance"/>.</summary>
        public Dir facing;
        public TileCoord entrance;
        public float age;

        public IEnumerable<TileCoord> Tiles()
        {
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                yield return new TileCoord(origin.x + x, origin.y + y);
        }

        public bool Covers(TileCoord t) =>
            t.x >= origin.x && t.y >= origin.y && t.x < origin.x + size.x && t.y < origin.y + size.y;

        public Vector3 WorldCenter(GridMap map)
        {
            var a = map.TileCenter(origin);
            var b = map.TileCenter(new TileCoord(origin.x + size.x - 1, origin.y + size.y - 1));
            return (a + b) * 0.5f;
        }
    }

    public class Depot : Building
    {
        public readonly List<TruckAgent> trucks = new List<TruckAgent>();

        public int IdleTrucks
        {
            get
            {
                int c = 0;
                foreach (var t in trucks) if (t.State == TruckState.Parked) c++;
                return c;
            }
        }

        public TruckAgent FirstIdle()
        {
            foreach (var t in trucks) if (t.State == TruckState.Parked) return t;
            return null;
        }

        /// <summary>The front tile that is not the dock; idle trucks park here.</summary>
        public TileCoord ParkingTile
        {
            get
            {
                foreach (var t in Tiles())
                {
                    if (t == dockTile) continue;
                    // same row/column as dock along the facing axis
                    var toEntrance = t.Step(facing);
                    if (!Covers(toEntrance)) return t;
                }
                return dockTile;
            }
        }
    }

    public class Store : Building
    {
        /// <summary>Orders placed and not yet delivered (includes ones with a truck en route).</summary>
        public int undelivered;
        /// <summary>Orders with a truck assigned.</summary>
        public int assigned;
        public float orderTimer;
        /// <summary>0..1; reaching 1 ends the game.</summary>
        public float overflow;
        public int delivered;

        public int Unassigned => undelivered - assigned;
    }
}
