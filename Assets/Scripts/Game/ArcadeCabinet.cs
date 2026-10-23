using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// An upright arcade cabinet built in code with the classic profile: a kick plate and coin door
    /// at the bottom, a sloped control panel with a joystick and buttons, a screen leaning back
    /// inside its bezel, and a lit marquee on top. The side panels are cut from that profile and
    /// carry the game's side art; the marquee, screen and art come from tools/generate_signs.py.
    /// </summary>
    public static class ArcadeCabinet
    {
        // The side profile (z forward = front of the cabinet, y up), in meters.
        private static readonly Vector2[] Profile =
        {
            new Vector2(-0.36f, 0f), new Vector2(0.34f, 0f), new Vector2(0.34f, 0.86f), new Vector2(0.46f, 0.92f),
            new Vector2(0.44f, 1.0f), new Vector2(0.26f, 1.04f), new Vector2(0.16f, 1.56f), new Vector2(0.28f, 1.6f),
            new Vector2(0.28f, 1.86f), new Vector2(-0.36f, 1.86f)
        };

        private const float Width = 0.72f;
        private const float SideThickness = 0.03f;
        private static Mesh sideMesh;

        public static GameObject Build(Transform parent, Vector2 position, float yaw, int game)
        {
            game = Mathf.Abs(game) % 4;
            var root = new GameObject("ArcadeCabinet");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(position.x, 0f, position.y);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = root.transform;

            Texture2D sideArt = BuildKit.SignTexture("arcade_side_" + game);
            Texture2D marqueeArt = BuildKit.SignTexture("arcade_marquee_" + game);
            Texture2D screenArt = BuildKit.SignTexture("arcade_screen_" + game);
            Material body = BuildKit.Plain("arcadeCabinetBody", new Color(0.05f, 0.045f, 0.06f), 0.45f);
            Material side = sideArt != null ? BuildKit.Picture("sign:" + sideArt.name, sideArt, 0.05f) : body;
            Material black = BuildKit.Plain("arcadeBlack", new Color(0.015f, 0.015f, 0.02f), 0.85f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Material trim = BuildKit.Plain("arcadeTrim", new Color(0.55f, 0.48f, 0.3f), 0.6f, 0.8f);

            // The two side panels.
            foreach (float x in new[] { -Width * 0.5f, Width * 0.5f - SideThickness })
            {
                var panel = new GameObject("Side");
                panel.transform.SetParent(t, false);
                panel.transform.localPosition = new Vector3(x, 0f, 0f);
                panel.AddComponent<MeshFilter>().sharedMesh = SideMesh();
                panel.AddComponent<MeshRenderer>().sharedMaterial = side;
            }
            float inner = Width - SideThickness * 2f;

            // Kick plate and coin door.
            BuildKit.Box("KickPlate", t, new Vector3(0f, 0.08f, 0.33f), new Vector3(inner, 0.16f, 0.02f), black, 1f, false);
            BuildKit.Box("Front", t, new Vector3(0f, 0.51f, 0.335f), new Vector3(inner, 0.7f, 0.02f), body, 1f, false);
            BuildKit.Box("CoinDoor", t, new Vector3(0f, 0.5f, 0.35f), new Vector3(0.3f, 0.36f, 0.015f), chrome, 1f, false);
            Material coin = BuildKit.Glow("coinSlot", new Color(1f, 0.45f, 0.1f), 2.5f);
            for (int i = 0; i < 2; i++)
            {
                float x = -0.07f + i * 0.14f;
                BuildKit.Box("CoinSlot", t, new Vector3(x, 0.58f, 0.36f), new Vector3(0.05f, 0.08f, 0.01f), coin, 1f, false);
                BuildKit.Box("CoinReturn", t, new Vector3(x, 0.42f, 0.36f), new Vector3(0.05f, 0.04f, 0.01f), black, 1f, false);
            }

            // Control panel: sloped, with a joystick and six buttons.
            var controls = new GameObject("ControlPanel").transform;
            controls.SetParent(t, false);
            controls.localPosition = new Vector3(0f, 0.96f, 0.36f);
            controls.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            BuildKit.Box("PanelTop", controls, Vector3.zero, new Vector3(inner, 0.03f, 0.2f), black, 1f, false);
            BuildKit.Box("PanelTrim", controls, new Vector3(0f, 0f, 0.1f), new Vector3(inner, 0.06f, 0.015f), trim, 1f, false);
            BuildKit.Cylinder("JoystickBase", controls, new Vector3(-0.17f, 0.02f, 0f), 0.06f, 0.01f, chrome);
            BuildKit.Cylinder("JoystickShaft", controls, new Vector3(-0.17f, 0.06f, 0f), 0.015f, 0.08f, chrome);
            BuildKit.Sphere("JoystickBall", controls, new Vector3(-0.17f, 0.11f, 0f), 0.045f, BuildKit.Plain("joystickBall", new Color(0.75f, 0.05f, 0.05f), 0.8f));
            Color[] buttons = { new Color(1f, 0.1f, 0.1f), new Color(1f, 0.85f, 0.1f), new Color(0.1f, 0.5f, 1f) };
            for (int row = 0; row < 2; row++)
            {
                for (int i = 0; i < 3; i++)
                {
                    var button = BuildKit.Cylinder("Button", controls, new Vector3(0.0f + i * 0.075f, 0.02f, -0.04f + row * 0.07f), 0.04f, 0.012f,
                                                   BuildKit.Glow("arcadeButton" + i, buttons[i], 0.6f));
                    button.transform.localPosition += Vector3.right * (row * 0.02f);
                }
            }

            // The screen, leaning back inside a black bezel.
            var screenFrame = new GameObject("Screen").transform;
            screenFrame.SetParent(t, false);
            screenFrame.localPosition = new Vector3(0f, 1.3f, 0.21f);
            screenFrame.localRotation = Quaternion.Euler(-11f, 0f, 0f);
            BuildKit.Box("Bezel", screenFrame, Vector3.zero, new Vector3(inner, 0.5f, 0.02f), black, 1f, false);
            Material screen = screenArt != null ? BuildKit.Picture("sign:" + screenArt.name, screenArt, 1.6f) : BuildKit.Glow("arcadeScreenPlain", new Color(0.2f, 0.6f, 1f), 1.2f);
            GameObject face = BuildKit.Quad("ScreenFace", screenFrame, new Vector3(0f, 0f, 0.012f), new Vector2(0.46f, 0.345f), 0f, screen);
            face.AddComponent<ScreenFlicker>();
            var light = new GameObject("ScreenLight").AddComponent<Light>();
            light.transform.SetParent(screenFrame, false);
            light.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            light.type = LightType.Point;
            light.range = 2.5f;
            light.intensity = 0.7f;
            light.color = new Color(0.55f, 0.65f, 1f);

            // Marquee: the game's name, lit from inside.
            Material marquee = marqueeArt != null ? BuildKit.Picture("sign:" + marqueeArt.name, marqueeArt, 2.2f) : BuildKit.Glow("arcadeMarqueePlain", new Color(1f, 0.6f, 0.2f), 2f);
            BuildKit.Box("MarqueeBox", t, new Vector3(0f, 1.73f, 0.2f), new Vector3(inner, 0.25f, 0.16f), body, 1f, false);
            BuildKit.Quad("Marquee", t, new Vector3(0f, 1.73f, 0.285f), new Vector2(inner - 0.02f, 0.2f), 0f, marquee);
            BuildKit.Box("Top", t, new Vector3(0f, 1.85f, -0.04f), new Vector3(inner, 0.02f, 0.64f), body, 1f, false);
            BuildKit.Box("Back", t, new Vector3(0f, 0.93f, -0.355f), new Vector3(inner, 1.86f, 0.02f), body, 1f, false);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.93f, 0.05f);
            collider.size = new Vector3(Width, 1.86f, 0.8f);
            return root;
        }

        /// <summary>A side panel: the profile extruded by the panel's thickness, UVs from the profile.</summary>
        private static Mesh SideMesh()
        {
            if (sideMesh != null)
            {
                return sideMesh;
            }
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            List<int> caps = Triangulate(Profile);
            float minZ = -0.36f;
            float depth = 0.82f;
            float height = 1.86f;
            // Outer (x = 0) and inner (x = thickness) faces.
            for (int face = 0; face < 2; face++)
            {
                int start = vertices.Count;
                float x = face == 0 ? 0f : SideThickness;
                foreach (Vector2 p in Profile)
                {
                    vertices.Add(new Vector3(x, p.y, p.x));
                    uvs.Add(new Vector2((p.x - minZ) / depth, p.y / height));
                }
                for (int i = 0; i < caps.Count; i += 3)
                {
                    if (face == 0)
                    {
                        triangles.AddRange(new[] { start + caps[i], start + caps[i + 2], start + caps[i + 1] });
                    }
                    else
                    {
                        triangles.AddRange(new[] { start + caps[i], start + caps[i + 1], start + caps[i + 2] });
                    }
                }
            }
            // The edge strip all around.
            for (int i = 0; i < Profile.Length; i++)
            {
                Vector2 a = Profile[i];
                Vector2 b = Profile[(i + 1) % Profile.Length];
                int start = vertices.Count;
                vertices.Add(new Vector3(0f, a.y, a.x));
                vertices.Add(new Vector3(SideThickness, a.y, a.x));
                vertices.Add(new Vector3(SideThickness, b.y, b.x));
                vertices.Add(new Vector3(0f, b.y, b.x));
                for (int k = 0; k < 4; k++)
                {
                    uvs.Add(new Vector2(0.02f, 0.02f));
                }
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            sideMesh = new Mesh { name = "ArcadeSide" };
            sideMesh.SetVertices(vertices);
            sideMesh.SetUVs(0, uvs);
            sideMesh.SetTriangles(triangles, 0);
            sideMesh.RecalculateNormals();
            sideMesh.RecalculateBounds();
            return sideMesh;
        }

        /// <summary>Ear clipping of a simple polygon (counter-clockwise or clockwise).</summary>
        private static List<int> Triangulate(Vector2[] polygon)
        {
            var result = new List<int>();
            var indices = new List<int>();
            for (int i = 0; i < polygon.Length; i++)
            {
                indices.Add(i);
            }
            float area = 0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                area += a.x * b.y - b.x * a.y;
            }
            bool counterClockwise = area > 0f;
            int guard = 0;
            while (indices.Count > 3 && guard++ < 1000)
            {
                for (int i = 0; i < indices.Count; i++)
                {
                    int prev = indices[(i + indices.Count - 1) % indices.Count];
                    int cur = indices[i];
                    int next = indices[(i + 1) % indices.Count];
                    Vector2 a = polygon[prev];
                    Vector2 b = polygon[cur];
                    Vector2 c = polygon[next];
                    float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                    if (counterClockwise ? cross <= 0f : cross >= 0f)
                    {
                        continue;
                    }
                    bool empty = true;
                    foreach (int other in indices)
                    {
                        if (other != prev && other != cur && other != next && Inside(polygon[other], a, b, c))
                        {
                            empty = false;
                            break;
                        }
                    }
                    if (!empty)
                    {
                        continue;
                    }
                    result.AddRange(new[] { prev, cur, next });
                    indices.RemoveAt(i);
                    break;
                }
            }
            if (indices.Count == 3)
            {
                result.AddRange(new[] { indices[0], indices[1], indices[2] });
            }
            if (!counterClockwise)
            {
                for (int i = 0; i < result.Count; i += 3)
                {
                    (result[i + 1], result[i + 2]) = (result[i + 2], result[i + 1]);
                }
            }
            return result;
        }

        private static bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(negative && positive);
        }
    }

    /// <summary>A CRT screen: a slight brightness flicker and now and then a rolling dip.</summary>
    public class ScreenFlicker : MonoBehaviour
    {
        private Material material;
        private Color baseEmission;
        private float seed;

        private void Awake()
        {
            var renderer = GetComponent<Renderer>();
            material = renderer.material;
            baseEmission = material.GetColor("_EmissionColor");
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float t = Time.time + seed;
            float flicker = 0.92f + 0.08f * Mathf.PerlinNoise(t * 9f, 0f);
            float roll = Mathf.Repeat(t * 0.13f, 1f) < 0.03f ? 0.6f : 1f;
            material.SetColor("_EmissionColor", baseEmission * flicker * roll);
        }
    }
}
