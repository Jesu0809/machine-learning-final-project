using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>
    /// Imports the signs and posters drawn by tools/generate_signs.py (Assets/Art/Signs): sharp,
    /// mip-mapped, filtered at an angle, smaller and crunched on the web.
    /// </summary>
    public static class SignImporter
    {
        public const string Folder = "Assets/Art/Signs";

        public static Texture2D[] ImportAll()
        {
            var textures = new List<Texture2D>();
            if (!Directory.Exists(Folder))
            {
                return textures.ToArray();
            }
            foreach (string file in Directory.GetFiles(Folder, "*.png"))
            {
                string path = file.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer != null)
                {
                    string name = Path.GetFileName(path);
                    int webSize = name.StartsWith("map_") ? 1024 : 512;
                    // Fences, wrapping paper and rain on glass tile; everything with see-through parts keeps clean edges.
                    TextureWrapMode wrap = name.StartsWith("fence_") || name.StartsWith("wrap_") || name.StartsWith("glass_") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    bool transparent = name.StartsWith("decal_") || name.StartsWith("cctv_") || name.StartsWith("fence_") || name.StartsWith("glass_") || name.StartsWith("vhs_osd_");
                    bool changed = importer.textureType != TextureImporterType.Default || importer.wrapMode != wrap || importer.anisoLevel != 8
                                   || importer.alphaIsTransparency != transparent;
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.mipmapEnabled = true;
                    importer.wrapMode = wrap;
                    importer.anisoLevel = 8;
                    importer.npotScale = TextureImporterNPOTScale.ToNearest;
                    importer.alphaIsTransparency = transparent;
                    TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings("WebGL");
                    if (!web.overridden || web.maxTextureSize != webSize || !web.crunchedCompression)
                    {
                        web.overridden = true;
                        web.maxTextureSize = webSize;
                        web.format = TextureImporterFormat.Automatic;
                        web.textureCompression = TextureImporterCompression.Compressed;
                        web.crunchedCompression = true;
                        web.compressionQuality = 70;
                        importer.SetPlatformTextureSettings(web);
                        changed = true;
                    }
                    if (changed)
                    {
                        importer.SaveAndReimport();
                    }
                }
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    textures.Add(texture);
                }
            }
            return textures.ToArray();
        }
    }
}
