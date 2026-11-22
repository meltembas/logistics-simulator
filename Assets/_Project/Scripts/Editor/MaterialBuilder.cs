using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Logistics.EditorTools
{
    /// <summary>
    /// Builds every material the game uses (Tools ▸ Logistics ▸ 1 Build Materials). PBR maps come from the
    /// Poly Haven downloads; each ARM map (AO/Rough/Metal) is repacked into URP's mask layout
    /// (R = metallic, G = occlusion, A = smoothness). All tints come from <see cref="Palette"/>.
    /// </summary>
    public static class MaterialBuilder
    {
        const string PH = "Assets/ThirdParty/PolyHaven";
        const string MatDir = "Assets/_Project/Materials";
        const string TexDir = "Assets/_Project/Textures";
        public const string LibraryPath = "Assets/_Project/Data/MaterialLibrary.asset";

        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");
        static Shader Unlit => Shader.Find("Universal Render Pipeline/Unlit");

        [MenuItem("Tools/Logistics/1 Build Materials")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(TexDir);
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));

            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<MaterialLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            lib.asphalt = Pbr("Asphalt", "asphalt_02", Color.Lerp(Palette.White, Palette.Asphalt, 0.25f), 1f);
            lib.sidewalk = Pbr("Sidewalk", "concrete_pavement", Color.Lerp(Palette.White, Palette.Concrete, 0.2f), 1f);
            lib.lot = Pbr("Lot", "concrete_floor_02", Color.Lerp(Palette.White, Palette.Concrete, 0.15f), 1f);
            lib.grass = Pbr("Grass", "aerial_grass_rock", Palette.GrassTint, 0.8f);
            AddMacroVariation(lib.grass, 15f / 140f);
            lib.riverbed = Pbr("Riverbed", "aerial_rocks_02", Color.Lerp(Palette.White, Palette.Shadow, 0.35f), 1f);
            lib.warehouseWall = Pbr("WarehouseWall", "factory_wall", Color.Lerp(Palette.White, Palette.Concrete, 0.1f), 1f);
            lib.warehouseRoof = Pbr("WarehouseRoof", "box_profile_metal_sheet", Color.Lerp(Palette.White, Palette.Concrete, 0.2f), 1f);
            lib.brick = Pbr("Brick", "red_brick_03", Color.Lerp(Palette.White, Palette.Facade, 0.15f), 1f);
            lib.plaster = Pbr("Plaster", "beige_wall_001", Palette.FacadeTint, 1f);
            lib.roofTiles = Pbr("RoofTiles", "grey_roof_01", Palette.RoofTint, 1f);
            lib.flatRoof = Pbr("FlatRoof", "tarred_gravel", Color.Lerp(Palette.White, Palette.Concrete, 0.1f), 1f);

            lib.concrete = Plain("Concrete", Palette.Concrete, 0.25f, 0f);
            lib.marking = Plain("Marking", Palette.Marking, 0.35f, 0f);
            lib.glass = Plain("Glass", Palette.Glass, 0.95f, 0.6f);
            lib.rubber = Plain("Rubber", Palette.Rubber, 0.25f, 0f);
            lib.chassis = Plain("Chassis", Palette.Chassis, 0.4f, 0.4f);
            lib.cabPaint = Plain("CabPaint", Palette.CabPaint, 0.78f, 0.15f);
            lib.darkMetal = Plain("DarkMetal", Palette.Chassis, 0.55f, 0.6f);
            lib.bark = Plain("Bark", Palette.Bark, 0.15f, 0f);
            lib.foliage = new[]
            {
                Foliage("Foliage1", Palette.Foliage1), Foliage("Foliage2", Palette.Foliage2), Foliage("Foliage3", Palette.Foliage3)
            };
            lib.headlight = Emissive("Headlight", Palette.Headlight, 1.6f);
            lib.brakeOff = Emissive("BrakeOff", Palette.BrakeLight, 0.25f);
            lib.brakeOn = Emissive("BrakeOn", Palette.BrakeLight, 4f);
            lib.cargoPaint = new Material[CargoInfo.Count];
            lib.cargoGlow = new Material[CargoInfo.Count];
            for (int i = 0; i < CargoInfo.Count; i++)
            {
                var c = (CargoType)i;
                lib.cargoPaint[i] = Plain("Paint_" + c, CargoInfo.ColorOf(c), 0.7f, 0.1f);
                lib.cargoGlow[i] = Emissive("Glow_" + c, CargoInfo.ColorOf(c), 1.15f);
            }
            lib.water = Water();

            lib.overlay = Overlay("Overlay", Palette.OutOfBounds);
            lib.ghost = Overlay("Ghost", Palette.GhostOk);
            lib.ringBase = Overlay("RingBase", Palette.UiPanel.WithAlpha(0.55f));
            lib.ringFill = Overlay("RingFill", Palette.UiAccent);
            lib.fx = Overlay("Fx", Palette.Marking.WithAlpha(0.8f));

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Logistics] Materials built: " + LibraryPath);
        }

        static Material GetOrCreate(string name, Shader shader)
        {
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            return m;
        }

        static Material Pbr(string name, string id, Color tint, float smoothnessScale)
        {
            var m = GetOrCreate(name, Lit);
            var diff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PH}/{id}/{id}_diff_2k.jpg");
            var nor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PH}/{id}/{id}_nor_gl_2k.jpg");
            var mask = BuildMask(id);
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BaseMap", diff);
            if (nor != null)
            {
                m.SetTexture("_BumpMap", nor);
                m.SetFloat("_BumpScale", 1f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (mask != null)
            {
                m.SetTexture("_MetallicGlossMap", mask);
                m.SetTexture("_OcclusionMap", mask);
                m.SetFloat("_OcclusionStrength", 1f);
                m.SetFloat("_Smoothness", smoothnessScale);
                m.SetFloat("_Metallic", 0f);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.EnableKeyword("_OCCLUSIONMAP");
            }
            m.SetFloat("_EnvironmentReflections", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D BuildMask(string id)
        {
            var outPath = $"{TexDir}/{id}_mask.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            if (existing != null) return existing;
            var src = $"{PH}/{id}/{id}_arm_2k.jpg";
            if (!File.Exists(src)) return null;
            var arm = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            arm.LoadImage(File.ReadAllBytes(src));
            int w = arm.width / 2, h = arm.height / 2;
            var px = arm.GetPixels32();
            var outPx = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int r = 0, g = 0, b = 0;
                for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                {
                    var c = px[(y * 2 + dy) * arm.width + x * 2 + dx];
                    r += c.r; g += c.g; b += c.b;
                }
                byte ao = (byte)(r / 4), rough = (byte)(g / 4), metal = (byte)(b / 4);
                outPx[y * w + x] = new Color32(metal, ao, 0, (byte)(255 - rough));
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(outPx);
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            Object.DestroyImmediate(arm);
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(outPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        /// <summary>Large-scale ×2 detail map (tileable fBm) so wide lawns don't read as one repeating texture.</summary>
        static void AddMacroVariation(Material m, float tiling)
        {
            var path = $"{TexDir}/macro_variation_v2.png";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                const int size = 256;
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size, n = 0f, amp = 0.5f, total = 0f;
                    for (int o = 0; o < 4; o++)
                    {
                        float f = 3f * (1 << o);
                        // Seamless: blend four offset copies so opposite edges match.
                        float N(float a, float b) => Mathf.PerlinNoise(a * f + 17.3f * o, b * f + 41.1f * o);
                        float top = Mathf.Lerp(N(u, v), N(u - 1f, v), u);
                        float bottom = Mathf.Lerp(N(u, v - 1f), N(u - 1f, v - 1f), u);
                        n += amp * Mathf.Lerp(top, bottom, v);
                        total += amp;
                        amp *= 0.5f;
                    }
                    n /= total;
                    float k = Mathf.Lerp(0.45f, 0.62f, Mathf.SmoothStep(0.25f, 0.75f, n));
                    // dryer patches lean warm, lush patches lean green (still from the grass family)
                    float warm = Mathf.Clamp01((0.5f - n) * 2f);
                    px[y * size + x] = new Color(k * (1f + warm * 0.12f), k, k * (1f - warm * 0.1f), 1f);
                }
                var t = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
                t.SetPixels32(px);
                t.Apply();
                File.WriteAllBytes(path, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            m.SetTexture("_DetailAlbedoMap", tex);
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(tiling, tiling));
            m.SetFloat("_DetailAlbedoMapScale", 1f);
            m.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(m);
        }

        static Material Plain(string name, Color color, float smoothness, float metallic)
        {
            var m = GetOrCreate(name, Lit);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetTexture("_BaseMap", null);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Foliage(string name, Color color)
        {
            // Leafy breakup: reuse the grass albedo at a small scale, tinted toward the foliage tone.
            var m = GetOrCreate(name, Lit);
            m.SetColor("_BaseColor", new Color(color.r * 1.25f, color.g * 1.5f, color.b * 1.2f));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PH}/aerial_grass_rock/aerial_grass_rock_diff_2k.jpg"));
            var nor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PH}/aerial_grass_rock/aerial_grass_rock_nor_gl_2k.jpg");
            m.SetTexture("_BumpMap", nor);
            m.SetFloat("_BumpScale", 1.5f);
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Emissive(string name, Color color, float intensity)
        {
            var m = GetOrCreate(name, Lit);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", 0.6f);
            m.SetFloat("_Metallic", 0f);
            m.SetColor("_EmissionColor", color.linear * intensity);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void MakeTransparent(Material m, bool preserveSpecular = false)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            // URP Lit "Preserve Specular Lighting": alpha is applied to diffuse in the shader so
            // sun glints on transparent surfaces (water) are not faded — matches the editor's own validation.
            m.SetFloat("_BlendModePreserveSpecular", preserveSpecular ? 1f : 0f);
            if (preserveSpecular) m.EnableKeyword("_ALPHAPREMULTIPLY_ON"); else m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetFloat("_SrcBlend", preserveSpecular ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
        }

        static Material Overlay(string name, Color color)
        {
            var m = GetOrCreate(name, Unlit);
            m.SetColor("_BaseColor", color);
            MakeTransparent(m);
            m.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Water()
        {
            var m = GetOrCreate("Water", Lit);
            m.SetColor("_BaseColor", Palette.Water);
            m.SetFloat("_Smoothness", 0.84f);
            m.SetFloat("_Metallic", 0.05f);
            var normal = BuildWaterNormal();
            m.SetTexture("_BumpMap", normal);
            m.SetFloat("_BumpScale", 0.6f);
            m.EnableKeyword("_NORMALMAP");
            m.SetTextureScale("_BaseMap", new Vector2(1f, 1f));
            MakeTransparent(m, preserveSpecular: true);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Tileable ripple normal map from summed periodic noise.</summary>
        static Texture2D BuildWaterNormal()
        {
            var path = $"{TexDir}/water_normal.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 512;
            var height = new float[size * size];
            var rnd = new System.Random(7);
            var waves = new (float fx, float fy, float amp, float phase)[14];
            for (int i = 0; i < waves.Length; i++)
                waves[i] = (rnd.Next(-9, 10), rnd.Next(-9, 10), 1f / (1 + i * 0.6f), (float)rnd.NextDouble() * 6.28f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size, hsum = 0f;
                foreach (var wv in waves) hsum += Mathf.Sin((u * wv.fx + v * wv.fy) * Mathf.PI * 2f + wv.phase) * wv.amp;
                height[y * size + x] = hsum;
            }
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float hl = height[y * size + (x - 1 + size) % size], hr = height[y * size + (x + 1) % size];
                float hd = height[((y - 1 + size) % size) * size + x], hu = height[((y + 1) % size) * size + x];
                var n = new Vector3((hl - hr) * 0.6f, (hd - hu) * 0.6f, 1f).normalized;
                px[y * size + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255), (byte)((n.y * 0.5f + 0.5f) * 255), (byte)((n.z * 0.5f + 0.5f) * 255), 255);
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
