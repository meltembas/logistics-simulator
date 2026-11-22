using UnityEditor;
using UnityEngine;

namespace Logistics.EditorTools
{
    /// <summary>Import settings for downloaded PBR maps and generated textures, applied on first import.</summary>
    public class TextureImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            var path = assetPath.Replace('\\', '/');
            bool thirdParty = path.StartsWith("Assets/ThirdParty/PolyHaven/");
            bool generated = path.StartsWith("Assets/_Project/Textures/");
            if (!thirdParty && !generated) return;

            var ti = (TextureImporter)assetImporter;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 8;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.textureCompression = TextureImporterCompression.Compressed;

            if (path.EndsWith(".hdr") || path.EndsWith(".exr"))
            {
                ti.textureShape = TextureImporterShape.Texture2D;
                ti.sRGBTexture = false;
                ti.maxTextureSize = 2048;
                ti.wrapModeU = TextureWrapMode.Repeat;
                ti.wrapModeV = TextureWrapMode.Clamp;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            ti.maxTextureSize = 2048;
            if (path.Contains("_nor_gl") || path.EndsWith("_normal.png"))
            {
                ti.textureType = TextureImporterType.NormalMap;
                ti.sRGBTexture = false;
            }
            else if (path.Contains("macro_variation"))
            {
                // URP's Detail ×2 multiplies in linear space, so mid-grey must stay 0.5 (no sRGB decode).
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                ti.maxTextureSize = 256;
            }
            else if (path.Contains("_arm_") || path.EndsWith("_mask.png"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                ti.maxTextureSize = 1024;
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
            }
        }
    }
}
