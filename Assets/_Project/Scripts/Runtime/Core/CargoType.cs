using UnityEngine;

namespace Logistics
{
    public enum CargoType { Food = 0, Electronics = 1, Fuel = 2, Textile = 3, Construction = 4 }

    public static class CargoInfo
    {
        public const int Count = 5;

        public static Color ColorOf(CargoType t)
        {
            switch (t)
            {
                case CargoType.Food: return Palette.Food;
                case CargoType.Electronics: return Palette.Electronics;
                case CargoType.Fuel: return Palette.Fuel;
                case CargoType.Textile: return Palette.Textile;
                default: return Palette.Construction;
            }
        }

        public static string NameOf(CargoType t)
        {
            switch (t)
            {
                case CargoType.Food: return "Gıda";
                case CargoType.Electronics: return "Elektronik";
                case CargoType.Fuel: return "Yakıt";
                case CargoType.Textile: return "Tekstil";
                default: return "İnşaat";
            }
        }
    }
}
