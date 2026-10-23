using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>
    /// Turns the PBR surfaces downloaded by tools/fetch_surfaces.py (Assets/Art/Surfaces/name/) into
    /// URP Lit materials: base color, normal map, and one mask with metallic (R), ambient occlusion
    /// (G) and smoothness (A). Each material tiles once per the real size of its texture, because
    /// the level's boxes have their UVs in meters. The web build gets 1K textures, the desktop 2K.
    /// </summary>
    public static class SurfaceImporter
    {
        public const string Folder = "Assets/Art/Surfaces";

        [MenuItem("Night Shift/Import Surfaces")]
        public static void ImportMenu()
        {
            Material[] materials = ImportAll();
            Debug.Log("Surfaces imported: " + materials.Length);
        }

        /// <summary>Imports every surface folder and returns its materials (saved next to the textures).</summary>
        public static Material[] ImportAll()
        {
            var materials = new List<Material>();
            if (!Directory.Exists(Folder))
            {
                return materials.ToArray();
            }
            Dictionary<string, float> sizes = ReadSizes();
            foreach (string directory in Directory.GetDirectories(Folder))
            {
                string name = Path.GetFileName(directory);
                string path = Folder + "/" + name + "/" + name;
                if (!File.Exists(path + "_albedo.jpg"))
                {
                    continue;
                }
                Texture2D albedo = Texture(path + "_albedo.jpg", TextureImporterType.Default, true);
                Texture2D normal = Texture(path + "_normal.jpg", TextureImporterType.NormalMap, false);
                Texture2D mask = Texture(path + "_mask.png", TextureImporterType.Default, false);
                float meters = sizes.TryGetValue(name, out float size) ? size : 2f;
                materials.Add(Material(path + ".mat", name, albedo, normal, mask, meters));
            }
            AssetDatabase.SaveAssets();
            return materials.ToArray();
        }

        private static Material Material(string path, string name, Texture2D albedo, Texture2D normal, Texture2D mask, float meters)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = name;
            material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", mask);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.SetFloat("_SmoothnessTextureChannel", 0f);   // smoothness in the mask's alpha
            material.SetFloat("_Smoothness", 1f);                 // scales the mask's smoothness
            material.SetFloat("_Metallic", 1f);
            material.SetTexture("_OcclusionMap", mask);           // occlusion is read from G
            material.EnableKeyword("_OCCLUSIONMAP");
            material.SetFloat("_OcclusionStrength", 1f);
            Vector2 scale = Vector2.one / Mathf.Max(0.1f, meters);
            material.SetTextureScale("_BaseMap", scale);
            material.mainTextureScale = scale;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D Texture(string path, TextureImporterType type, bool srgb)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                bool changed = importer.textureType != type || importer.sRGBTexture != srgb || importer.maxTextureSize != 2048;
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.textureCompression = TextureImporterCompression.Compressed;
                // The web build downloads every texture: 1K there.
                TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings("WebGL");
                if (!web.overridden || web.maxTextureSize != 1024)
                {
                    web.overridden = true;
                    web.maxTextureSize = 1024;
                    web.format = TextureImporterFormat.Automatic;
                    importer.SetPlatformTextureSettings(web);
                    changed = true;
                }
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Real size of each texture in meters, from surfaces.json.</summary>
        private static Dictionary<string, float> ReadSizes()
        {
            var sizes = new Dictionary<string, float>();
            string file = Folder + "/surfaces.json";
            if (!File.Exists(file))
            {
                return sizes;
            }
            // { "name": { "meters": 1.2, "source": "..." }, ... }: a tiny reader is enough.
            string text = File.ReadAllText(file);
            int index = 0;
            while ((index = text.IndexOf("\"meters\":", index, System.StringComparison.Ordinal)) >= 0)
            {
                int nameEnd = text.LastIndexOf("\": {", index, System.StringComparison.Ordinal);
                int nameStart = text.LastIndexOf('"', nameEnd - 1) + 1;
                string name = text.Substring(nameStart, nameEnd - nameStart);
                int valueStart = index + "\"meters\":".Length;
                int valueEnd = text.IndexOfAny(new[] { ',', '}' }, valueStart);
                if (float.TryParse(text.Substring(valueStart, valueEnd - valueStart).Trim(), System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float meters))
                {
                    sizes[name] = meters;
                }
                index = valueEnd;
            }
            return sizes;
        }
    }
}
