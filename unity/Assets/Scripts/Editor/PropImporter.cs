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
                if (importer != null && (importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation || importer.importCameras
                                         || importer.meshCompression != ModelImporterMeshCompression.High))
                {
                    // Small on disk and on the web: compressed meshes, no materials of their own.
                    importer.meshCompression = ModelImporterMeshCompression.High;
                    importer.optimizeMeshPolygons = true;
                    importer.optimizeMeshVertices = true;
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

        /// <summary>
        /// The bounds of a set's screen glass in the model's space: the triangles whose texture is
        /// the glass, the dark square in the top right corner of television_02's atlas.
        /// </summary>
        public static Bounds MeasureScreen(GameAssets.Prop[] props, string name)
        {
            GameAssets.Prop tv = System.Array.Find(props, p => p.Name == name);
            if (tv == null || tv.Model == null)
            {
                return new Bounds();
            }
            Transform root = tv.Model.transform;
            var bounds = new Bounds();
            int found = 0;
            foreach (MeshFilter filter in tv.Model.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }
                Vector3[] vertices = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                for (int i = 0; i < vertices.Length && i < uvs.Length; i++)
                {
                    if (uvs[i].x < 0.68f || uvs[i].y < 0.765f)
                    {
                        continue;
                    }
                    Vector3 point = root.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
                    if (found++ == 0)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                    }
                    bounds.Encapsulate(point);
                }
            }
            Debug.Log("Screen of " + name + ": " + found + " vertices, center " + bounds.center.ToString("F3") + ", size " + bounds.size.ToString("F3"));
            return found > 0 ? bounds : new Bounds();
        }
    }
}
