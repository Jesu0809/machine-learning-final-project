using System.Collections.Generic;
using System.IO;
using NightShift.Game;
using UnityEditor;
using UnityEngine;

namespace NightShift.EditorTools
{
    /// <summary>
    /// Imports the props downloaded by tools/fetch_models.py (Assets/Art/Models/name/): the FBX
    /// without its own materials, and one URP Lit material made from the packed textures, the same
    /// way as the surfaces. The level places them by name (see BuildKit.Prop).
    /// </summary>
    public static class PropImporter
    {
        public const string Folder = "Assets/Art/Models";

        [MenuItem("Night Shift/Import Models")]
        public static void ImportMenu()
        {
            Debug.Log("Models imported: " + ImportAll().Length);
        }

        public static GameAssets.Prop[] ImportAll()
        {
            var props = new List<GameAssets.Prop>();
            if (!Directory.Exists(Folder))
            {
                return props.ToArray();
            }
            foreach (string directory in Directory.GetDirectories(Folder))
            {
                string name = Path.GetFileName(directory);
                string path = Folder + "/" + name + "/" + name;
                if (!File.Exists(path + ".fbx") || !File.Exists(path + "_albedo.jpg"))
                {
                    continue;
                }
                var importer = (ModelImporter)AssetImporter.GetAtPath(path + ".fbx");
                if (importer != null && (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation || importer.importCameras))
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importAnimation = false;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
                Texture2D albedo = SurfaceImporter.Texture(path + "_albedo.jpg", TextureImporterType.Default, true);
                Texture2D normal = SurfaceImporter.Texture(path + "_normal.jpg", TextureImporterType.NormalMap, false);
                Texture2D mask = SurfaceImporter.Texture(path + "_mask.png", TextureImporterType.Default, false, 512);
                Material material = SurfaceImporter.Material(path + ".mat", name, albedo, normal, mask, 1f);
                props.Add(new GameAssets.Prop
                {
                    Name = name,
                    Model = AssetDatabase.LoadAssetAtPath<GameObject>(path + ".fbx"),
                    Material = material
                });
            }
            AssetDatabase.SaveAssets();
            return props.ToArray();
        }
    }
}
