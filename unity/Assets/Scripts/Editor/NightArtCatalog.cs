using System.IO;
using NightShift.UI;
using UnityEditor;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>Import settings for everything under Assets/Art and Assets/Audio.</summary>
    public class NightArtImportSettings : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Art/"))
            {
                return;
            }
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = assetPath.EndsWith(".png");
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Audio/"))
            {
                return;
            }
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
        }
    }

    /// <summary>Builds Assets/Resources/NightArt.asset from the files in Assets/Art and Assets/Audio.</summary>
    public static class NightArtCatalog
    {
        private const string CatalogPath = "Assets/Resources/NightArt.asset";

        private static readonly string[] CameraNames =
        {
            "cam1-stage", "cam2-main-hall", "cam3-west-hall", "cam4-kitchen", "cam5-east-hall"
        };

        [MenuItem("Night Shift/Rebuild Art Catalog")]
        public static void Rebuild()
        {
            // Re-import so the settings above apply to files that were already imported.
            AssetDatabase.ImportAsset("Assets/Art", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Audio", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

            Directory.CreateDirectory("Assets/Resources");
            var art = AssetDatabase.LoadAssetAtPath<NightArt>(CatalogPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<NightArt>();
                AssetDatabase.CreateAsset(art, CatalogPath);
            }

            art.cameraEmpty = new Sprite[CameraNames.Length];
            art.cameraMonster = new Sprite[CameraNames.Length];
            for (int i = 0; i < CameraNames.Length; i++)
            {
                art.cameraEmpty[i] = Sprite("Cameras/" + CameraNames[i] + ".jpg");
                art.cameraMonster[i] = Sprite("Cameras/" + CameraNames[i] + "-monster.jpg");
            }

            art.office = Sprite("Office/office-wide.jpg");
            art.officePowerOut = Sprite("Office/office-power-out.jpg");
            art.leftDoorClosed = Sprite("Office/office-door-left-closed.png");
            art.rightDoorClosed = Sprite("Office/office-door-right-closed.png");
            art.monsterLeftDoor = Sprite("Office/office-monster-left-door.png");
            art.monsterRightDoor = Sprite("Office/office-monster-right-door.png");

            art.monsterIdle = new[]
            {
                Sprite("Monster/monster-idle-1.png"), Sprite("Monster/monster-idle-2.png"), Sprite("Monster/monster-idle-3.png")
            };
            art.jumpscare = new[]
            {
                Sprite("Jumpscare/jumpscare-1-face.jpg"), Sprite("Jumpscare/jumpscare-2-beak-open.jpg"), Sprite("Jumpscare/jumpscare-3-scream.jpg")
            };

            art.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/VT323-Regular.ttf");

            art.staticLoop = Clip("static-loop.ogg");
            art.cameraSwitch = Clip("camera-switch.ogg");
            art.ambientHum = Clip("ambient-hum-loop.ogg");
            art.doorSlide = Clip("door-slide.ogg");
            art.doorSlam = Clip("door-slam.ogg");
            art.powerOut = Clip("power-out.ogg");
            art.scream = Clip("jumpscare-scream.ogg");

            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            Debug.Log("Night art catalog rebuilt at " + CatalogPath);
        }

        private static Sprite Sprite(string relativePath)
        {
            string path = "Assets/Art/" + relativePath;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError("Missing sprite: " + path);
            }
            return sprite;
        }

        private static AudioClip Clip(string fileName)
        {
            string path = "Assets/Audio/" + fileName;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogError("Missing audio clip: " + path);
            }
            return clip;
        }
    }
}
