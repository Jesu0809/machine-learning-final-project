using System.IO;
using UnityEditor;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>
    /// Import settings that keep the web build small (GitHub rejects files over 100 MB, and every
    /// player downloads the whole build): the Sketchfab models' textures, the voices and the size
    /// check. Desktop builds keep their full quality; only the WebGL overrides shrink.
    /// </summary>
    public static class WebImport
    {
        /// <summary>
        /// The textures tools/prepare_models.py writes next to a model (Textures/&lt;model&gt;/NN_role):
        /// normal maps, linear data or color, compressed, and crunched to 1K for the web. Reimports the
        /// model when one changes, so glTFast picks up the compressed assets instead of embedding its own.
        /// </summary>
        public static void ModelTextures(string model)
        {
            string folder = "Assets/Art/Sketchfab/Textures/" + model;
            if (!Directory.Exists(folder))
            {
                return;
            }
            bool changed = false;
            foreach (string file in Directory.GetFiles(folder))
            {
                string path = file.Replace('\\', '/');
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                {
                    continue;
                }
                string role = Path.GetFileNameWithoutExtension(path);
                role = role.Substring(role.IndexOf('_') + 1);
                TextureImporterType type = role == "normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                bool srgb = role == "color" || role == "emission";
                bool dirty = importer.textureType != type || importer.sRGBTexture != srgb || importer.maxTextureSize != 2048
                             || importer.textureCompression != TextureImporterCompression.Compressed;
                importer.textureType = type;
                importer.sRGBTexture = srgb;
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.Compressed;
                dirty |= Crunch(importer, 1024, 60);
                if (dirty)
                {
                    importer.SaveAndReimport();
                    changed = true;
                }
            }
            if (changed)
            {
                AssetDatabase.ImportAsset("Assets/Art/Sketchfab/" + model + ".glb", ImportAssetOptions.ForceUpdate);
            }
        }

        /// <summary>The WebGL override of a texture: this size at most, crunch-compressed. True if it changed.</summary>
        public static bool Crunch(TextureImporter importer, int size, int quality)
        {
            TextureImporterPlatformSettings web = importer.GetPlatformTextureSettings("WebGL");
            if (web.overridden && web.maxTextureSize == size && web.crunchedCompression && web.compressionQuality == quality
                && web.textureCompression == TextureImporterCompression.Compressed)
            {
                return false;
            }
            web.overridden = true;
            web.maxTextureSize = size;
            web.format = TextureImporterFormat.Automatic;
            web.textureCompression = TextureImporterCompression.Compressed;
            web.crunchedCompression = true;
            web.compressionQuality = quality;
            importer.SetPlatformTextureSettings(web);
            return true;
        }

        /// <summary>
        /// The voices (Assets/Audio/Story), in the web build: AAC at a lower quality, and mono (they
        /// were synthesized mono). Saves about 9 MB. WebGL keeps its audio at 44.1 kHz whatever the
        /// importer asks, so the sample rate is left alone; music and effects keep Unity's settings.
        /// </summary>
        public static void Voices()
        {
            const float quality = 0.3f;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio/Story" }))
                {
                    if (!(AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is AudioImporter importer))
                    {
                        continue;
                    }
                    AudioImporterSampleSettings web = importer.GetOverrideSampleSettings("WebGL");
                    if (importer.ContainsSampleSettingsOverride("WebGL") && importer.forceToMono
                        && web.compressionFormat == AudioCompressionFormat.AAC && Mathf.Approximately(web.quality, quality))
                    {
                        continue;
                    }
                    web.compressionFormat = AudioCompressionFormat.AAC;
                    web.quality = quality;
                    importer.SetOverrideSampleSettings("WebGL", web);
                    importer.forceToMono = true;
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        /// <summary>
        /// After a WebGL build: the size of every file, and false if one is over the limit (GitHub
        /// rejects files over 100 MB; the repository's pre-push hook stops at 95).
        /// </summary>
        public static bool CheckSizes(string output, long limitBytes = 95L * 1024 * 1024)
        {
            bool fits = true;
            foreach (string file in Directory.GetFiles(output, "*", SearchOption.AllDirectories))
            {
                long size = new FileInfo(file).Length;
                bool over = size > limitBytes;
                fits &= !over;
                string line = "WebGL file " + Path.GetFileName(file) + ": " + (size / (1024f * 1024f)).ToString("F1") + " MB";
                if (over)
                {
                    Debug.LogError(line + " is over " + limitBytes / (1024 * 1024) + " MB: GitHub will refuse it.");
                }
                else
                {
                    Debug.Log(line);
                }
            }
            return fits;
        }
    }
}
