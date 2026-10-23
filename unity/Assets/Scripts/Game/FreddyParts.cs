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
    public static class FreddyParts
    {
        /// <summary>Height of a spare head at scale 1 (the size of the old placeholder sphere).</summary>
        public const float HeadHeight = 0.6f;

        private static bool tried;
        private static Mesh headMesh;
        private static Material[] headMaterials;

        /// <summary>A spare head centered on <paramref name="localPosition"/>; null if the model is missing.</summary>
        public static GameObject Head(Transform parent, Vector3 localPosition, Quaternion localRotation, float scale)
        {
            if (!tried)
            {
                tried = true;
                Extract();
            }
            if (headMesh == null)
            {
                return null;
            }
            var go = new GameObject("SpareHead");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = headMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = headMaterials;
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
                headMesh = new Mesh { name = "FreddySpareHead" };
                headMesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                headMesh.SetVertices(vertices);
                headMesh.SetNormals(normals);
                headMesh.SetUVs(0, uvs);
                headMesh.subMeshCount = submeshes.Count;
                for (int i = 0; i < submeshes.Count; i++)
                {
                    headMesh.SetTriangles(submeshes[i], i);
                }
                headMesh.RecalculateBounds();
                headMaterials = materials.ToArray();
            }
            finally
            {
                Object.Destroy(model);
            }
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
}
