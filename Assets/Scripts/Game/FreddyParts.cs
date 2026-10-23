using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Spare parts cut from Freddy's own model, so the heads on the workshop benches and shelves
    /// are really his (same fur, hat and face), not spheres. The skinned mesh is baked once in its
    /// rest pose; the triangles above the neck become the head, the eyes get a dead material
    /// instead of the glow, and the mesh is centered and scaled to <see cref="HeadHeight"/>.
    /// </summary>
    /// <summary>How a spare head is left: with or without its hat, dead or with an eye still on.</summary>
    [System.Flags]
    public enum HeadStyle
    {
        Default = 0,
        NoHat = 1,
        LiveEye = 2,
    }

    public static class FreddyParts
    {
        /// <summary>Height of a spare head at scale 1 (the size of the old placeholder sphere).</summary>
        public const float HeadHeight = 0.6f;

        private static bool tried;
        private static Mesh headMesh;
        private static Mesh hatlessMesh;
        private static Mesh hatMesh;
        private static Material[] headMaterials;
        private static Material[] hatlessMaterials;
        private static Material[] liveMaterials;
        private static Material[] hatMaterials;

        /// <summary>A spare head centered on <paramref name="localPosition"/>; null if the model is missing.</summary>
        public static GameObject Head(Transform parent, Vector3 localPosition, Quaternion localRotation, float scale, HeadStyle style = HeadStyle.Default)
        {
            Ensure();
            if (headMesh == null)
            {
                return null;
            }
            bool hatless = (style & HeadStyle.NoHat) != 0 && hatlessMesh != null;
            var go = Part("SpareHead", parent, localPosition, localRotation, scale, hatless ? hatlessMesh : headMesh, hatless ? hatlessMaterials : headMaterials);
            if ((style & HeadStyle.LiveEye) != 0)
            {
                go.AddComponent<EyeFlicker>().SetUp(go.GetComponent<Renderer>(), hatless ? hatlessMaterials : headMaterials, liveMaterials, headMaterials);
            }
            return go;
        }

        /// <summary>The top hat on its own (fallen off a head); null if the model is missing.</summary>
        public static GameObject Hat(Transform parent, Vector3 localPosition, Quaternion localRotation, float scale)
        {
            Ensure();
            if (hatMesh == null)
            {
                return null;
            }
            return Part("SpareHat", parent, localPosition, localRotation, scale, hatMesh, hatMaterials);
        }

        private static void Ensure()
        {
            if (!tried)
            {
                tried = true;
                Extract();
            }
        }

        private static GameObject Part(string name, Transform parent, Vector3 position, Quaternion rotation, float scale, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        private static void Extract()
        {
            GameAssets assets = GameAssets.Load();
            if (assets == null || assets.FreddyPrefab == null)
            {
                Log("no Freddy prefab");
                return;
            }
            GameObject model = Object.Instantiate(assets.FreddyPrefab, new Vector3(0f, -200f, 0f), Quaternion.identity);
            try
            {
                Animator animator = model.GetComponentInChildren<Animator>();
                Transform headBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
                Transform neckBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Neck) : null;
                if (headBone == null)
                {
                    Log("no head bone (animator " + (animator != null) + ")");
                    return;
                }
                // The cut: a little below the head bone, between it and the neck.
                float cut = neckBone != null ? Mathf.Lerp(neckBone.position.y, headBone.position.y, 0.6f) : headBone.position.y - 0.05f;

                var vertices = new List<Vector3>();
                var normals = new List<Vector3>();
                var uvs = new List<Vector2>();
                var submeshes = new List<List<int>>();
                var materials = new List<Material>();
                var originals = new List<Material>();
                foreach (SkinnedMeshRenderer skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var baked = new Mesh();
                    // Baked without the renderer's scale (the bones already size a skinned mesh):
                    // only its position and rotation put it in the world.
                    skin.BakeMesh(baked, false);
                    Matrix4x4 toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                    Vector3[] v = baked.vertices;
                    Vector3[] n = baked.normals;
                    Vector2[] uv = baked.uv;
                    var world = new Vector3[v.Length];
                    float low = float.MaxValue, high = float.MinValue;
                    for (int i = 0; i < v.Length; i++)
                    {
                        world[i] = toWorld.MultiplyPoint3x4(v[i]);
                        low = Mathf.Min(low, world[i].y);
                        high = Mathf.Max(high, world[i].y);
                    }
                    Log(skin.name + " (scale " + skin.transform.lossyScale.x.ToString("0.000") + "): " + v.Length + " vertices, y " + low.ToString("0.00") + " to " + high.ToString("0.00") + ", cut " + cut.ToString("0.00"));
                    Material[] shared = skin.sharedMaterials;
                    for (int sub = 0; sub < baked.subMeshCount; sub++)
                    {
                        int[] triangles = baked.GetTriangles(sub);
                        var kept = new List<int>();
                        var remap = new Dictionary<int, int>();
                        for (int t = 0; t < triangles.Length; t += 3)
                        {
                            int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                            if (world[a].y < cut || world[b].y < cut || world[c].y < cut)
                            {
                                continue;
                            }
                            foreach (int index in new[] { a, b, c })
                            {
                                if (!remap.TryGetValue(index, out int mapped))
                                {
                                    mapped = vertices.Count;
                                    remap[index] = mapped;
                                    vertices.Add(world[index] - headBone.position);
                                    normals.Add(toWorld.MultiplyVector(n.Length > index ? n[index] : Vector3.up).normalized);
                                    uvs.Add(uv.Length > index ? uv[index] : Vector2.zero);
                                }
                                kept.Add(mapped);
                            }
                        }
                        if (kept.Count == 0)
                        {
                            continue;
                        }
                        submeshes.Add(kept);
                        originals.Add(sub < shared.Length ? shared[sub] : null);
                        materials.Add(DeadEyes(sub < shared.Length ? shared[sub] : null));
                        Log("part " + (sub < shared.Length && shared[sub] != null ? shared[sub].name : "?") + ": " + kept.Count / 3 + " triangles");
                    }
                    Object.Destroy(baked);
                }
                if (vertices.Count == 0)
                {
                    return;
                }

                // Centered on its bounds and scaled to the placeholder's size.
                var bounds = new Bounds(vertices[0], Vector3.zero);
                foreach (Vector3 vertex in vertices)
                {
                    bounds.Encapsulate(vertex);
                }
                float size = HeadHeight / Mathf.Max(0.01f, bounds.size.y);
                for (int i = 0; i < vertices.Count; i++)
                {
                    vertices[i] = (vertices[i] - bounds.center) * size;
                }
                // Which parts are the hat (its material is shared with the bow, which is below the cut).
                var all = new List<int>();
                var withoutHat = new List<int>();
                var hatOnly = new List<int>();
                for (int i = 0; i < submeshes.Count; i++)
                {
                    all.Add(i);
                    bool hat = originals[i] != null && originals[i].name.StartsWith("Hat");
                    (hat ? hatOnly : withoutHat).Add(i);
                }
                headMesh = Build("FreddySpareHead", vertices, normals, uvs, submeshes, all, Vector3.zero);
                headMaterials = materials.ToArray();
                liveMaterials = originals.ToArray();
                if (hatOnly.Count > 0 && withoutHat.Count > 0)
                {
                    hatlessMesh = Build("FreddySpareHeadNoHat", vertices, normals, uvs, submeshes, withoutHat, Vector3.zero);
                    hatlessMaterials = Pick(materials, withoutHat);
                    var hatBounds = new Bounds();
                    bool firstHat = true;
                    foreach (int sub in hatOnly)
                    {
                        foreach (int index in submeshes[sub])
                        {
                            if (firstHat)
                            {
                                hatBounds = new Bounds(vertices[index], Vector3.zero);
                                firstHat = false;
                            }
                            hatBounds.Encapsulate(vertices[index]);
                        }
                    }
                    hatMesh = Build("FreddySpareHat", vertices, normals, uvs, submeshes, hatOnly, hatBounds.center);
                    hatMaterials = Pick(materials, hatOnly);
                }
            }
            finally
            {
                Object.Destroy(model);
            }
        }

        private static Mesh Build(string name, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<List<int>> submeshes, List<int> parts, Vector3 center)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            var shifted = new List<Vector3>(vertices.Count);
            foreach (Vector3 vertex in vertices)
            {
                shifted.Add(vertex - center);
            }
            mesh.SetVertices(shifted);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = parts.Count;
            for (int i = 0; i < parts.Count; i++)
            {
                mesh.SetTriangles(submeshes[parts[i]], i);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material[] Pick(List<Material> materials, List<int> parts)
        {
            var picked = new Material[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                picked[i] = materials[parts[i]];
            }
            return picked;
        }

        private static void Log(string text)
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-capture") >= 0)
            {
                Debug.Log("CAPTURE spare head " + text);
            }
        }

        /// <summary>The same material, except glowing eyes go dark: a spare head is switched off.</summary>
        private static Material DeadEyes(Material material)
        {
            if (material == null || !material.IsKeywordEnabled("_EMISSION"))
            {
                return material;
            }
            var dead = new Material(material) { name = material.name + " (off)" };
            dead.SetColor("_EmissionColor", Color.black);
            dead.DisableKeyword("_EMISSION");
            dead.SetColor("_BaseColor", new Color(0.25f, 0.27f, 0.3f));
            return dead;
        }
    }

    /// <summary>
    /// A spare head with one circuit still alive: its eyes come on and off in short, uneven bursts,
    /// throwing a little light on whatever is next to it.
    /// </summary>
    public class EyeFlicker : MonoBehaviour
    {
        private Renderer target;
        private Material[] dead;
        private Material[] live;
        private Light glow;
        private float nextChange;
        private bool on;

        public void SetUp(Renderer renderer, Material[] deadMaterials, Material[] liveMaterials, Material[] fullDead)
        {
            target = renderer;
            dead = deadMaterials;
            // Same slots as the head shown: swap only the eye materials that were switched off.
            live = (Material[])deadMaterials.Clone();
            for (int i = 0; i < live.Length; i++)
            {
                int full = System.Array.IndexOf(fullDead, deadMaterials[i]);
                if (full >= 0 && full < liveMaterials.Length && liveMaterials[full] != null && liveMaterials[full].IsKeywordEnabled("_EMISSION"))
                {
                    live[i] = liveMaterials[full];
                }
            }
            glow = new GameObject("EyeLight").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.05f, 0.3f);
            glow.type = LightType.Point;
            glow.range = 1.2f;
            glow.intensity = 0.6f;
            glow.color = new Color(0.8f, 0.85f, 1f);
            glow.shadows = LightShadows.None;
        }

        private void Update()
        {
            if (Time.time < nextChange || target == null)
            {
                return;
            }
            on = !on;
            // Mostly dark; on for a blink, sometimes a stutter of blinks.
            nextChange = Time.time + (on ? Random.Range(0.05f, 0.4f) : Random.Range(0.1f, 3.5f));
            target.sharedMaterials = on ? live : dead;
            glow.enabled = on;
        }
    }
}
