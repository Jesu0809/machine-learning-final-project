using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightShift.Game
{
    /// <summary>
    /// The browser build's own profile. WebGL sends every draw call through JavaScript, and most
    /// players have a high-density screen, so in the browser the game keeps to Low and Medium,
    /// renders at most <see cref="MaxRenderWidth"/> pixels across, has no ambient occlusion or
    /// shadows, and merges the circus's still geometry into a few hundred meshes. The desktop build
    /// never takes this path.
    /// </summary>
    public static class WebPerformance
    {
        /// <summary>Widest internal resolution in the browser, in pixels.</summary>
        public const int MaxRenderWidth = 1600;

        /// <summary>Side of the cells the still geometry is merged in: small enough that each merged
        /// mesh keeps the lamps of its own corner of the room.</summary>
        private const float CellSize = 6f;

        public static bool Active => Application.platform == RuntimePlatform.WebGLPlayer;

        /// <summary>The render scale wanted, lowered so the picture is never wider than the cap.</summary>
        public static float RenderScale(float wanted)
        {
            return Mathf.Min(wanted, MaxRenderWidth / (float)Mathf.Max(1, Screen.width));
        }

        /// <summary>
        /// Merges the meshes under <paramref name="root"/> that never move or change into one mesh
        /// per room, material and cell, and hides the originals (their colliders stay). Left alone:
        /// anything under a script, under one of <paramref name="keep"/> (things moved or switched
        /// by others), glowing or see-through materials, materials of one object only, and meshes
        /// that cannot be read. Returns how many renderers were merged.
        /// </summary>
        public static int MergeStill(Transform root, IEnumerable<Transform> keep)
        {
            var kept = new HashSet<Transform>(keep);
            var groups = new Dictionary<(Transform, Material, Vector3Int, ShadowCastingMode, bool, int), List<MeshRenderer>>();
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || renderer.sharedMaterials.Length != 1)
                {
                    continue;
                }
                Material material = renderer.sharedMaterial;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (material == null || mesh == null || !mesh.isReadable || mesh.subMeshCount != 1
                    || material.renderQueue >= (int)RenderQueue.GeometryLast || material.IsKeywordEnabled("_EMISSION"))
                {
                    continue;
                }
                Transform room = RoomOf(renderer.transform, root, kept);
                if (room == null)
                {
                    continue;
                }
                Vector3 center = renderer.bounds.center;
                var cell = Vector3Int.FloorToInt(center / CellSize);
                var key = (room, material, cell, renderer.shadowCastingMode, renderer.receiveShadows, renderer.gameObject.layer);
                if (!groups.TryGetValue(key, out List<MeshRenderer> list))
                {
                    groups[key] = list = new List<MeshRenderer>();
                }
                list.Add(renderer);
            }

            var merged = new GameObject("Merged").transform;
            merged.SetParent(root, true);
            int count = 0;
            foreach (var pair in groups)
            {
                List<MeshRenderer> renderers = pair.Value;
                if (renderers.Count < 2)
                {
                    continue;
                }
                var parts = new CombineInstance[renderers.Count];
                long vertices = 0;
                for (int i = 0; i < renderers.Count; i++)
                {
                    Mesh mesh = renderers[i].GetComponent<MeshFilter>().sharedMesh;
                    vertices += mesh.vertexCount;
                    parts[i] = new CombineInstance { mesh = mesh, subMeshIndex = 0, transform = renderers[i].localToWorldMatrix };
                }
                var combined = new Mesh { name = "Merged " + pair.Key.Item2.name };
                combined.indexFormat = vertices > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                combined.CombineMeshes(parts, true, true, false);
                combined.UploadMeshData(true);

                var go = new GameObject(combined.name);
                go.layer = pair.Key.Item6;
                go.transform.SetParent(merged, false);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                go.AddComponent<MeshFilter>().sharedMesh = combined;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Key.Item2;
                renderer.shadowCastingMode = pair.Key.Item4;
                renderer.receiveShadows = pair.Key.Item5;
                renderer.lightProbeUsage = renderers[0].lightProbeUsage;
                renderer.reflectionProbeUsage = renderers[0].reflectionProbeUsage;
                foreach (MeshRenderer original in renderers)
                {
                    original.enabled = false;
                }
                count += renderers.Count;
            }
            return count;
        }

        /// <summary>The room (child of the root) a renderer belongs to, or null if anything between them
        /// runs a script or is kept apart.</summary>
        private static Transform RoomOf(Transform transform, Transform root, HashSet<Transform> kept)
        {
            Transform room = null;
            for (Transform t = transform; t != null && t != root; t = t.parent)
            {
                if (kept.Contains(t) || t.GetComponent<MonoBehaviour>() != null || t.GetComponent<Light>() != null)
                {
                    return null;
                }
                room = t;
            }
            return room;
        }
    }
}
