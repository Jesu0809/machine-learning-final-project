using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightShift.Game
{
    /// <summary>
    /// Meshes and materials for the level built in code. Boxes get UVs in meters, so one material
    /// tiles the same on a small shelf and on a long wall. Materials are copies of templates saved in
    /// the project (see <see cref="GameAssets.MaterialTemplates"/>), so builds keep the shader variants.
    /// </summary>
    public static class BuildKit
    {
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly Dictionary<Vector4, Mesh> Boxes = new Dictionary<Vector4, Mesh>();
        /// <summary>Scanned surfaces: their textures cover a real size, so boxes using them get UVs in plain meters.</summary>
        private static readonly HashSet<Material> InMeters = new HashSet<Material>();
        private static GameAssets assets;
        private static Mesh cone;

        public static void Init(GameAssets gameAssets)
        {
            assets = gameAssets;
            Materials.Clear();
            Boxes.Clear();
            InMeters.Clear();
        }

        // ------------------------------------------------------------------ materials

        /// <summary>
        /// Lit surface with base color and normal map. If <see cref="SurfaceLibrary"/> maps the key
        /// to a scanned PBR surface, that one is used; otherwise the textures are generated in code
        /// (only the first time).
        /// </summary>
        public static Material Surface(string key, System.Func<Surface> make, float smoothness = 0.2f, float metallic = 0f)
        {
            if (SurfaceLibrary.TryGet(key, out string scanned, out Color tint))
            {
                Material pbr = Pbr(scanned, tint);
                if (pbr != null)
                {
                    return pbr;
                }
            }
            return Cached(key, () =>
            {
                Surface surface = make();
                Material material = FromTemplate("LitNormal", "Universal Render Pipeline/Lit", "_NORMALMAP");
                material.SetTexture("_BaseMap", surface.Albedo);
                material.SetTexture("_BumpMap", surface.Normal);
                material.SetFloat("_BumpScale", 1f);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", metallic);
                return material;
            });
        }

        /// <summary>A scanned PBR surface (Assets/Art/Surfaces) tinted by <paramref name="tint"/>; null if it was not imported.</summary>
        public static Material Pbr(string name, Color tint)
        {
            string key = "pbr:" + name + ":" + ColorUtility.ToHtmlStringRGB(tint);
            if (Materials.TryGetValue(key, out Material cached))
            {
                return cached;
            }
            if (assets == null || assets.Surfaces == null)
            {
                return null;
            }
            foreach (Material source in assets.Surfaces)
            {
                if (source != null && source.name == name)
                {
                    var material = new Material(source) { name = key };
                    material.SetColor("_BaseColor", tint);
                    Materials[key] = material;
                    InMeters.Add(material);
                    return material;
                }
            }
            return null;
        }

        public static Material Plain(string key, Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            return Cached(key, () =>
            {
                Material material = FromTemplate("LitPlain", "Universal Render Pipeline/Lit");
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", smoothness);
                material.SetFloat("_Metallic", metallic);
                return material;
            });
        }

        /// <summary>Lit material with a texture (posters, signs) and optional glow.</summary>
        public static Material Picture(string key, Texture2D texture, float glow = 0f)
        {
            return Cached(key, () =>
            {
                Material material = glow > 0f
                    ? FromTemplate("LitEmission", "Universal Render Pipeline/Lit", "_EMISSION")
                    : FromTemplate("LitPlain", "Universal Render Pipeline/Lit");
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 0.1f);
                if (glow > 0f)
                {
                    material.SetTexture("_EmissionMap", texture);
                    material.SetColor("_EmissionColor", Color.white * glow);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                return material;
            });
        }

        public static Material Glow(string key, Color color, float strength)
        {
            return Cached(key, () =>
            {
                Material material = FromTemplate("LitEmission", "Universal Render Pipeline/Lit", "_EMISSION");
                material.SetColor("_BaseColor", color * 0.5f);
                material.SetColor("_EmissionColor", color * strength);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                return material;
            });
        }

        /// <summary>Additive, unlit, double-sided (fake light shafts, ripples, the AI vision overlay).</summary>
        public static Material Additive(string key, Texture2D texture, Color color)
        {
            return Cached(key, () =>
            {
                Material material = FromTemplate("UnlitAdditive", "Universal Render Pipeline/Unlit");
                if (material.shader.name == "Universal Render Pipeline/Unlit" && !material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                {
                    MakeAdditive(material);
                }
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", color);
                return material;
            });
        }

        /// <summary>Alpha-blended, unlit decal (stains on the floor).</summary>
        public static Material Stain(string key, Texture2D texture, Color color)
        {
            return Cached(key, () =>
            {
                Material material = FromTemplate("UnlitAlpha", "Universal Render Pipeline/Unlit");
                if (!material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                {
                    MakeAlpha(material);
                }
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", color);
                return material;
            });
        }

        /// <summary>
        /// A lit, alpha-blended decal (water stains, mold, graffiti): it takes the light of the room
        /// and the flashlight like the wall under it, so it never glows in the dark.
        /// </summary>
        public static Material Decal(string key, Texture2D texture, Color tint)
        {
            return Cached("decal:" + key + ColorUtility.ToHtmlStringRGBA(tint), () =>
            {
                Material material = FromTemplate("LitAlpha", "Universal Render Pipeline/Lit");
                if (!material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                {
                    MakeLitAlpha(material);
                }
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", tint);
                material.SetFloat("_Smoothness", 0.15f);
                return material;
            });
        }

        /// <summary>Sets up a Lit material as alpha-blended transparent (also used for the editor template).</summary>
        public static void MakeLitAlpha(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent - 20;
        }

        /// <summary>Sets up an Unlit material as alpha-blended transparent (also used for the editor template).</summary>
        public static void MakeAlpha(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent - 10;
        }

        /// <summary>Sets up an Unlit material as transparent additive (also used by the editor to make the template).</summary>
        public static void MakeAdditive(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static Material Cached(string key, System.Func<Material> create)
        {
            if (!Materials.TryGetValue(key, out Material material))
            {
                material = create();
                material.name = key;
                Materials[key] = material;
            }
            return material;
        }

        private static Material FromTemplate(string template, string shader, string keyword = null)
        {
            if (assets != null && assets.MaterialTemplates != null)
            {
                foreach (Material candidate in assets.MaterialTemplates)
                {
                    if (candidate != null && candidate.name == template)
                    {
                        return new Material(candidate);
                    }
                }
            }
            var material = new Material(Shader.Find(shader));
            if (keyword != null)
            {
                material.EnableKeyword(keyword);
            }
            return material;
        }

        // ------------------------------------------------------------------ scanned props

        /// <summary>True if the scanned prop <paramref name="name"/> was imported.</summary>
        public static bool HasProp(string name) => FindProp(name) != null;

        /// <summary>
        /// Places a scanned 3D prop (Assets/Art/Models) standing on <paramref name="localPosition"/>,
        /// turned <paramref name="yaw"/> degrees, optionally scaled, with a box collider around it.
        /// Returns null if the prop was not imported, so callers can fall back to boxes.
        /// </summary>
        public static GameObject Prop(string name, Transform parent, Vector3 localPosition, float yaw = 0f, float scale = 1f, bool collider = true, Color? tint = null)
        {
            GameAssets.Prop prop = FindProp(name);
            if (prop == null || prop.Model == null)
            {
                return null;
            }
            GameObject go = Object.Instantiate(prop.Model, parent, false);
            go.name = name;
            // Models exported from Blender keep their own root rotation and scale (z-up to y-up,
            // centimeters): turn and scale on top of them instead of replacing them.
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * prop.Model.transform.localRotation;
            go.transform.localScale = prop.Model.transform.localScale * scale;
            Material material = prop.Material;
            if (tint.HasValue && material != null)
            {
                string key = "prop:" + name + ":" + ColorUtility.ToHtmlStringRGB(tint.Value);
                if (!Materials.TryGetValue(key, out material))
                {
                    material = new Material(prop.Material) { name = key };
                    material.SetColor("_BaseColor", tint.Value);
                    Materials[key] = material;
                }
            }
            var bounds = new Bounds();
            bool first = true;
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                if (material != null)
                {
                    var materials = new Material[renderer.sharedMaterials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        materials[i] = material;
                    }
                    renderer.sharedMaterials = materials;
                }
                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            if (collider && !first)
            {
                // Collider in the prop's own space (it is only rotated about y).
                // On a child that only carries the yaw, so the box stays axis-aligned with the room.
                var holder = new GameObject("Collider");
                holder.transform.SetParent(parent, false);
                holder.transform.localPosition = localPosition;
                holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                holder.transform.SetParent(go.transform, true);
                var box = holder.AddComponent<BoxCollider>();
                Vector3 size = Quaternion.Inverse(holder.transform.rotation) * bounds.size;
                box.center = holder.transform.InverseTransformPoint(bounds.center);
                Vector3 lossy = holder.transform.lossyScale;
                box.size = new Vector3(Mathf.Abs(size.x) / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
                                       Mathf.Abs(size.y) / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
                                       Mathf.Abs(size.z) / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));
            }
            return go;
        }

        /// <summary>A drawn sign or poster from Assets/Art/Signs, or null.</summary>
        public static Texture2D SignTexture(string name)
        {
            if (assets == null || assets.Signs == null)
            {
                return null;
            }
            foreach (Texture2D texture in assets.Signs)
            {
                if (texture != null && texture.name == name)
                {
                    return texture;
                }
            }
            return null;
        }

        /// <summary>
        /// Hangs a drawn sign on a wall: the picture on a thin dark backing (so it has an edge and a
        /// shadow), <paramref name="width"/> meters wide, facing <paramref name="facing"/>. Signs that
        /// light up get <paramref name="glow"/> &gt; 1; plates that only catch the light stay near 0.3.
        /// </summary>
        public static GameObject WallSign(Transform parent, Texture2D texture, Vector3 localPosition, float facing, float width, float glow)
        {
            float height = width * texture.height / texture.width;
            var sign = new GameObject("Sign " + texture.name);
            sign.transform.SetParent(parent, false);
            sign.transform.localPosition = localPosition;
            sign.transform.localRotation = Quaternion.Euler(0f, facing, 0f);
            Box("Backing", sign.transform, new Vector3(0f, 0f, -0.015f), new Vector3(width + 0.04f, height + 0.04f, 0.03f),
                Plain("signBacking", new Color(0.05f, 0.05f, 0.05f), 0.3f, 0.4f), 1f, false);
            Quad("Face", sign.transform, new Vector3(0f, 0f, 0.002f), new Vector2(width, height), 0f, Picture("sign:" + texture.name, texture, glow));
            return sign;
        }

        private static GameAssets.Prop FindProp(string name)
        {
            if (assets == null || assets.Props == null)
            {
                return null;
            }
            foreach (GameAssets.Prop prop in assets.Props)
            {
                if (prop != null && prop.Name == name)
                {
                    return prop;
                }
            }
            return null;
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>A box with its UVs in meters divided by <paramref name="tile"/>.</summary>
        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material,
                                     float tile = 1f, bool collider = true, float yaw = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, InMeters.Contains(material) ? 1f : tile);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (collider)
            {
                go.AddComponent<BoxCollider>().size = size;
            }
            return go;
        }

        /// <summary>A cone (apex up) of the given radius and height, without collider.</summary>
        public static GameObject Cone(string name, Transform parent, Vector3 localPosition, float radius, float height, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(radius, height, radius);
            go.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        public static GameObject Sphere(string name, Transform parent, Vector3 localPosition, float diameter, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * diameter;
            go.GetComponent<Renderer>().sharedMaterial = material;
            DestroyCollider(go);
            return go;
        }

        public static GameObject Cylinder(string name, Transform parent, Vector3 localPosition, float diameter, float height, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = material;
            DestroyCollider(go);
            return go;
        }

        /// <summary>A flat picture (poster, sign) on a wall; facing is the direction it looks at.</summary>
        public static GameObject Quad(string name, Transform parent, Vector3 localPosition, Vector2 size, float facing, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            // A Quad faces -Z; rotate it so its front looks toward "facing".
            go.transform.localRotation = Quaternion.Euler(0f, facing + 180f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.GetComponent<Renderer>().sharedMaterial = material;
            DestroyCollider(go);
            return go;
        }

        private static void DestroyCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static Mesh BoxMesh(Vector3 size, float tile)
        {
            var key = new Vector4(size.x, size.y, size.z, tile);
            if (Boxes.TryGetValue(key, out Mesh cached))
            {
                return cached;
            }
            Vector3 h = size * 0.5f;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            void Face(Vector3 normal, Vector3 right, Vector3 up, float width, float height)
            {
                int start = vertices.Count;
                Vector3 center = Vector3.Scale(normal, h);
                Vector3 r = right * (width * 0.5f);
                Vector3 u = up * (height * 0.5f);
                vertices.Add(center - r - u);
                vertices.Add(center + r - u);
                vertices.Add(center + r + u);
                vertices.Add(center - r + u);
                for (int i = 0; i < 4; i++)
                {
                    normals.Add(normal);
                }
                uvs.Add(new Vector2(0f, 0f));
                uvs.Add(new Vector2(width / tile, 0f));
                uvs.Add(new Vector2(width / tile, height / tile));
                uvs.Add(new Vector2(0f, height / tile));
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
            Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);
            Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
            Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
            Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
            Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);

            var mesh = new Mesh { name = "Box" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            Boxes[key] = mesh;
            return mesh;
        }

        private static Mesh ConeMesh()
        {
            if (cone != null)
            {
                return cone;
            }
            const int segments = 20;
            var vertices = new List<Vector3> { Vector3.up };
            var uvs = new List<Vector2> { new Vector2(0.5f, 1f) };
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
                uvs.Add(new Vector2((float)i / segments, 0f));
            }
            for (int i = 1; i <= segments; i++)
            {
                triangles.AddRange(new[] { 0, i + 1, i });
            }
            cone = new Mesh { name = "Cone" };
            cone.SetVertices(vertices);
            cone.SetUVs(0, uvs);
            cone.SetTriangles(triangles, 0);
            cone.RecalculateNormals();
            cone.RecalculateBounds();
            return cone;
        }
    }
}
