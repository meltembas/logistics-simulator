using UnityEngine;

namespace Logistics
{
    [CreateAssetMenu(menuName = "Logistics/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Map")]
        public int mapWidth = 36;
        public int mapHeight = 26;
        public float tileSize = 8f;
        public Vector2Int startBoundsSize = new Vector2Int(14, 10);
        public int boundsGrowPerWeek = 2;
        public float treeCoverage = 0.22f;
        public float fillerBuildingChance = 0.05f;

        [Header("Time")]
        public float weekSeconds = 75f;

        [Header("Inventory")]
        public int startRoadTiles = 40;
        public int weeklyRoadTiles = 20;
        public int startBridges = 1;
        public int startRoundabouts = 1;
        public int startHighways = 0;
        public int bonusRoadTiles = 15;
        public int maxBridgeSpan = 4;
        public int maxHighwayLength = 14;

        [Header("Trucks")]
        public float truckSpeed = 11f;
        public float truckAccel = 9f;
        public float truckBrake = 18f;
        public float cornerSpeedFactor = 0.6f;
        public float highwaySpeedMultiplier = 1.8f;
        public float unloadSeconds = 1.2f;
        public float laneOffset = 1.6f;
        public float truckLength = 5.8f;
        public float followGap = 2.2f;
        public int trucksPerDepot = 1;
        [Tooltip("Each new store brings one truck to the nearest depot of its cargo (like houses bringing cars).")]
        public int trucksPerStore = 1;
        public int maxTrucksPerDepot = 8;

        [Header("Demand")]
        public float orderIntervalStart = 20f;
        public float orderIntervalMin = 8f;
        public float orderIntervalWeeklyGrowth = 0.1f;
        public int overflowThreshold = 5;
        public float overflowSeconds = 50f;
        public float overflowRecoverRate = 1.5f;

        [Header("Spawning")]
        public float storeSpawnIntervalStart = 24f;
        public float storeSpawnIntervalMin = 10f;
        public int[] cargoUnlockWeeks = { 0, 2, 4, 6, 8 };
        public int initialStores = 2;
        public int maxStoresPerDepot = 5;

        public static GameConfig CreateDefault()
        {
            var c = CreateInstance<GameConfig>();
            c.name = "GameConfig (default)";
            return c;
        }
    }
}
