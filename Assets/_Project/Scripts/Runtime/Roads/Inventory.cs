using System;

namespace Logistics
{
    public enum ItemKind { Road, Bridge, Roundabout, Highway }

    public class Inventory
    {
        public int Roads;
        public int Bridges;
        public int Roundabouts;
        public int Highways;

        public event Action Changed;

        public int Get(ItemKind k)
        {
            switch (k)
            {
                case ItemKind.Road: return Roads;
                case ItemKind.Bridge: return Bridges;
                case ItemKind.Roundabout: return Roundabouts;
                default: return Highways;
            }
        }

        public void Add(ItemKind k, int amount)
        {
            switch (k)
            {
                case ItemKind.Road: Roads += amount; break;
                case ItemKind.Bridge: Bridges += amount; break;
                case ItemKind.Roundabout: Roundabouts += amount; break;
                default: Highways += amount; break;
            }
            Changed?.Invoke();
        }

        public bool TrySpend(ItemKind k, int amount = 1)
        {
            if (Get(k) < amount) return false;
            Add(k, -amount);
            return true;
        }
    }
}
