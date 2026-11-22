using UnityEngine;

namespace Logistics
{
    /// <summary>
    /// Art bible — single source for every color in the project.
    /// Style: photoreal miniature city (PBR surfaces, HDRI sky, light tilt-shift); gameplay info
    /// (order icons, cargo colors, UI) sits on top as a clean, flat-colored layer.
    /// Mood: afternoon sun (~40° elevation, warm key), blue sky bounce keeps shadows alive.
    /// </summary>
    public static class Palette
    {
        // Surfaces (used as tints over PBR textures)
        public static readonly Color Grass = Hex(0x5F7A45);
        public static readonly Color Asphalt = Hex(0x3B3D40);
        public static readonly Color Concrete = Hex(0xB9B4AA);
        public static readonly Color Facade = Hex(0xC9B79C);
        public static readonly Color Shadow = Hex(0x3A3428);

        /// <summary>Neutral (no tint) — the base that surface tints lerp from.</summary>
        public static readonly Color White = Hex(0xFFFFFF);

        // Derived surface tones (all from the bible family)
        public static readonly Color GrassTint = new Color(Grass.r * 1.5f, Grass.g * 1.62f, Grass.b * 1.35f);
        public static readonly Color FacadeTint = Color.Lerp(White, Facade, 0.25f);
        public static readonly Color RoofTint = Color.Lerp(White, Shadow, 0.15f);
        public static readonly Color Water = Hex(0x1F4650, 0.86f);
        public static readonly Color Glass = Hex(0x1B2733);
        public static readonly Color Rubber = Hex(0x1A1A1C);
        public static readonly Color Chassis = Hex(0x2A2C30);
        public static readonly Color CabPaint = Hex(0xEDEBE6);
        public static readonly Color Marking = Hex(0xE8E4DA);
        public static readonly Color Bark = Hex(0x4A3A2A);
        public static readonly Color Foliage1 = Hex(0x4C6B35);
        public static readonly Color Foliage2 = Hex(0x5E7D3E);
        public static readonly Color Foliage3 = Hex(0x3F5C33);
        public static readonly Color Headlight = Hex(0xFFF1DA);
        public static readonly Color BrakeLight = Hex(0xE0301E);
        public static readonly Color Haze = Hex(0xB9CAD6);

        // Light
        public static readonly Color SunKey = Hex(0xFFF1DA);

        // Cargo colors (Okabe-Ito, colorblind-safe)
        public static readonly Color Food = Hex(0x009E73);
        public static readonly Color Electronics = Hex(0x0072B2);
        public static readonly Color Fuel = Hex(0xD55E00);
        public static readonly Color Textile = Hex(0xCC79A7);
        public static readonly Color Construction = Hex(0xF0E442);

        // UI
        public static readonly Color UiPanel = Hex(0x1E2329, 0.88f);
        public static readonly Color UiText = Hex(0xF3F1EC);
        public static readonly Color UiAccent = Hex(0xFFB547);
        public static readonly Color UiDanger = Hex(0xE5484D);
        public static readonly Color UiMuted = Hex(0x8A9099);

        // Overlays
        public static readonly Color OutOfBounds = Hex(0x3A3428, 0.38f);
        public static readonly Color GhostOk = Hex(0xFFB547, 0.28f);
        public static readonly Color GhostBad = Hex(0xE5484D, 0.32f);

        public static Color Hex(uint rgb, float a = 1f)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
        }

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }
    }
}
