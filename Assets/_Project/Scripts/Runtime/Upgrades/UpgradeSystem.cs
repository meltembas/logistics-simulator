using System.Collections.Generic;

namespace Logistics
{
    public enum UpgradeCard { Bridge, Roundabout, Highway, ExtraTruck, MoreRoads }

    public static class UpgradeSystem
    {
        public static string Title(UpgradeCard c)
        {
            switch (c)
            {
                case UpgradeCard.Bridge: return "Köprü";
                case UpgradeCard.Roundabout: return "Göbek Kavşak";
                case UpgradeCard.Highway: return "Otoyol";
                case UpgradeCard.ExtraTruck: return "Ek Kamyon";
                default: return "+15 Yol";
            }
        }

        public static string Description(UpgradeCard c, GameConfig cfg)
        {
            switch (c)
            {
                case UpgradeCard.Bridge: return $"Nehri {cfg.maxBridgeSpan} karoya kadar geç.";
                case UpgradeCard.Roundabout: return "Kavşakta aynı anda üç kamyona yol verir.";
                case UpgradeCard.Highway: return "İki yolu engellerin üstünden 1,8× hızla bağlar.";
                case UpgradeCard.ExtraTruck: return "En yoğun depoya bir kamyon daha.";
                default: return $"{cfg.bonusRoadTiles} yol karosu daha.";
            }
        }

        /// <summary>Two distinct cards, weighted toward what the player is short of.</summary>
        public static UpgradeCard[] Offer(World w)
        {
            var pool = new List<(UpgradeCard card, float weight)>
            {
                (UpgradeCard.ExtraTruck, 1.3f),
                (UpgradeCard.MoreRoads, w.Inventory.Roads < 15 ? 1.6f : 0.8f),
                (UpgradeCard.Roundabout, w.Week >= 2 ? 1f : 0.4f),
                (UpgradeCard.Bridge, w.Inventory.Bridges == 0 ? 1.2f : 0.6f),
                (UpgradeCard.Highway, w.Week >= 3 ? 0.9f : 0.2f),
            };
            var result = new UpgradeCard[2];
            for (int k = 0; k < 2; k++)
            {
                float total = 0f;
                foreach (var p in pool) total += p.weight;
                float r = (float)w.Rng.NextDouble() * total;
                int pick = 0;
                for (int i = 0; i < pool.Count; i++)
                {
                    r -= pool[i].weight;
                    if (r <= 0f) { pick = i; break; }
                }
                result[k] = pool[pick].card;
                pool.RemoveAt(pick);
            }
            return result;
        }

        public static void Apply(World w, UpgradeCard c)
        {
            switch (c)
            {
                case UpgradeCard.Bridge: w.Inventory.Add(ItemKind.Bridge, 1); break;
                case UpgradeCard.Roundabout: w.Inventory.Add(ItemKind.Roundabout, 1); break;
                case UpgradeCard.Highway: w.Inventory.Add(ItemKind.Highway, 1); break;
                case UpgradeCard.ExtraTruck: w.GrantExtraTruck(); break;
                default: w.Inventory.Add(ItemKind.Road, w.Config.bonusRoadTiles); break;
            }
        }
    }
}
