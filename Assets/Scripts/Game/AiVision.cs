using System.Collections.Generic;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The AI inspector drawn in the world (Tab): the path Freddy is about to walk, his view cone on
    /// the floor, and a ripple in every room where his brain heard a noise. It shows in 3D what the
    /// inspector panel shows as numbers.
    /// </summary>
    public class AiVision : MonoBehaviour
    {
        private const float RippleSeconds = 1.6f;

        private class Wave
        {
            public Transform Ring;
            public Material Material;
            public float Age;
            public float Size;
        }

        private HuntDirector director;
        private LineRenderer pathLine;
        private GameObject viewCone;
        private Material coneMaterial;
        private readonly List<Wave> ripples = new List<Wave>();

        public static AiVision Instance { get; private set; }

        public static AiVision Create(HuntDirector director)
        {
            var go = new GameObject("AiVision");
            go.transform.SetParent(director.transform, false);
            Instance = go.AddComponent<AiVision>();
            Instance.director = director;
            Instance.Build();
            return Instance;
        }

        private bool Visible => director.UI != null && director.UI.InspectorVisible
                                && (director.Phase == GamePhase.Playing || director.Phase == GamePhase.Paused);

        private void Build()
        {
            pathLine = gameObject.AddComponent<LineRenderer>();
            pathLine.sharedMaterial = BuildKit.Additive("aiPath", ProceduralTextures.Disc(8), new Color(0.3f, 0.9f, 1f, 0.9f));
            pathLine.widthMultiplier = 0.12f;
            pathLine.numCornerVertices = 2;
            pathLine.alignment = LineAlignment.TransformZ;
            pathLine.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            pathLine.useWorldSpace = true;
            pathLine.enabled = false;

            viewCone = new GameObject("ViewCone");
            viewCone.transform.SetParent(director.Freddy.transform, false);
            viewCone.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            viewCone.AddComponent<MeshFilter>().sharedMesh = FanMesh(60f, 24);
            coneMaterial = new Material(BuildKit.Additive("aiCone", FadeTexture(), new Color(1f, 0.15f, 0.1f, 0.35f)));
            viewCone.AddComponent<MeshRenderer>().sharedMaterial = coneMaterial;
            viewCone.SetActive(false);
        }

        /// <summary>A noise the brain just heard, shown as a ripple on the floor of that room.</summary>
        public void Ripple(Room room, int noise)
        {
            Wave ripple = null;
            foreach (Wave candidate in ripples)
            {
                if (candidate.Age >= RippleSeconds)
                {
                    ripple = candidate;
                    break;
                }
            }
            if (ripple == null)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "NoiseRipple";
                quad.transform.SetParent(transform, false);
                quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var material = new Material(BuildKit.Additive("aiRipple", ProceduralTextures.Ring(128, 0.05f), Color.white));
                quad.GetComponent<Renderer>().sharedMaterial = material;
                ripple = new Wave { Ring = quad.transform, Material = material };
                ripples.Add(ripple);
            }
            ripple.Ring.position = PizzeriaLayout.Center(room) + Vector3.up * 0.06f;
            ripple.Age = 0f;
            ripple.Size = 2f + noise * 2.5f;
            ripple.Ring.gameObject.SetActive(Visible);
        }

        private void LateUpdate()
        {
            bool visible = Visible;
            pathLine.enabled = visible;
            viewCone.SetActive(visible);

            if (visible)
            {
                IReadOnlyList<Vector3> path = director.Freddy.PlannedPath;
                pathLine.positionCount = path.Count + 1;
                pathLine.SetPosition(0, Flat(director.Freddy.transform.position));
                for (int i = 0; i < path.Count; i++)
                {
                    pathLine.SetPosition(i + 1, Flat(path[i]));
                }
                float range = director.Player.Flashlight.enabled ? FreddyBody.SightRangeFlashlight : FreddyBody.SightRange;
                viewCone.transform.localScale = new Vector3(range, 1f, range) / director.Freddy.transform.lossyScale.x;
                coneMaterial.SetColor("_BaseColor", director.Freddy.SeesPlayer ? new Color(1f, 0.1f, 0.05f, 0.7f) : new Color(1f, 0.6f, 0.2f, 0.25f));
            }

            foreach (Wave ripple in ripples)
            {
                if (ripple.Age >= RippleSeconds)
                {
                    ripple.Ring.gameObject.SetActive(false);
                    continue;
                }
                ripple.Age += Time.deltaTime;
                float k = ripple.Age / RippleSeconds;
                ripple.Ring.localScale = Vector3.one * Mathf.Lerp(0.5f, ripple.Size, k);
                ripple.Material.SetColor("_BaseColor", new Color(1f, 0.25f, 0.15f, (1f - k) * 0.9f));
                ripple.Ring.gameObject.SetActive(visible);
            }
        }

        /// <summary>Just above the floor under the point (the floors and the stairs are at different heights).</summary>
        private static Vector3 Flat(Vector3 point) => point + Vector3.up * 0.07f;

        /// <summary>A flat fan on the floor, one unit long, opening <paramref name="halfAngle"/> to each side.</summary>
        private static Mesh FanMesh(float halfAngle, int segments)
        {
            var vertices = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0f) };
            var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float angle = Mathf.Lerp(-halfAngle, halfAngle, (float)i / segments) * Mathf.Deg2Rad;
                vertices.Add(new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)));
                uvs.Add(new Vector2((float)i / segments, 1f));
                if (i > 0)
                {
                    triangles.AddRange(new[] { 0, i, i + 1 });
                }
            }
            var mesh = new Mesh { name = "ViewCone" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Opaque near Freddy, fading out at the end of his sight.</summary>
        private static Texture2D FadeTexture()
        {
            var texture = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float a = 1f - y / 63f;
                for (int x = 0; x < 4; x++)
                {
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
