using System.Collections.Generic;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The set dressing that makes the circus look lived in and abandoned: real cloth with folds,
    /// old furniture, and what somebody wrote on the walls. Nothing here has a collider unless it was
    /// placed clear of the walking lines, so the map stays the one the brain was trained on.
    /// </summary>
    public static class CircusSet
    {
        private static Material Canvas(string name, Color tint, Color fallback) =>
            BuildKit.Pbr(name, tint) ?? BuildKit.Plain("set:" + name, fallback, 0.08f);

        private static Material Velvet(Color tint) => BuildKit.Pbr("velvet", tint) ?? BuildKit.Plain("circusRedVelvet", new Color(0.46f, 0.05f, 0.06f), 0.1f);

        public static void Add(Room room, Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(room);
            float height = PizzeriaLayout.Height(room);
            Material red = Velvet(new Color(0.62f, 0.07f, 0.09f));
            switch (room)
            {
                case Room.DiningHall:
                    Valance(room, root, height - 1.1f, 0.62f, Canvas("canvas_ring", Color.white, new Color(0.46f, 0.05f, 0.06f)), 9);
                    DoorCurtains(room, root, red);
                    Prop(room, root, "wooden_stool_01", new Vector2(area.center.x - 1.3f, area.center.y - 0.4f), 0.32f, 20f, 1.25f);
                    Prop(room, root, "wooden_stool_01", new Vector2(area.center.x + 1.5f, area.center.y - 1.2f), 0.32f, 200f, 1.25f);
                    Prop(room, root, "wine_barrel_01", new Vector2(area.xMax - 1f, area.yMin + 1f), 0.45f, 40f);
                    Prop(room, root, "wooden_ladder", new Vector2(area.xMin + 0.8f, area.center.y + 1.4f), 0.5f, 90f, 1.6f);
                    break;
                case Room.Arcade:
                    Valance(room, root, height - 0.9f, 0.5f, Canvas("canvas_midway", Color.white, new Color(0.06f, 0.09f, 0.24f)), 8);
                    DoorCurtains(room, root, Velvet(new Color(0.16f, 0.2f, 0.5f)));
                    Stack(room, root, new Vector2(area.xMax - 1.2f, area.center.y + 0.6f));
                    Prop(room, root, "wooden_bucket_01", new Vector2(area.center.x, area.yMin + 0.8f), 0.25f, 10f);
                    break;
                case Room.PartyRoom:
                    Valance(room, root, height - 0.8f, 0.45f, Canvas("canvas_birthday", Color.white, new Color(0.06f, 0.09f, 0.24f)), 7);
                    DoorCurtains(room, root, red);
                    Prop(room, root, "folding_wooden_stool", new Vector2(area.xMin + 1.2f, area.yMax - 1.2f), 0.3f, 30f);
                    Prop(room, root, "folding_wooden_stool", new Vector2(area.xMax - 1.4f, area.yMin + 1.4f), 0.3f, 130f);
                    break;
                case Room.Kitchen:
                    Stack(room, root, new Vector2(area.xMin + 3f, area.yMin + 0.9f));
                    Prop(room, root, "wine_barrel_01", new Vector2(area.xMax - 0.8f, area.yMin + 0.9f), 0.45f, 120f);
                    Prop(room, root, "wooden_bucket_01", new Vector2(area.xMax - 1.6f, area.yMin + 0.8f), 0.25f, 200f);
                    Scrawl(room, root, random, "decal_scrawl_3", 2.2f);
                    break;
                case Room.Storage:
                    Prop(room, root, "spinning_wheel_01", new Vector2(area.center.x + 1.4f, area.yMin + 0.7f), 0.32f, 35f);
                    Scrawl(room, root, random, "decal_scrawl_2", 2f);
                    break;
                case Room.Parts:
                    HorseHead(room, root, new Vector2(area.center.x + 0.6f, area.yMax - 0.9f));
                    Prop(room, root, "wooden_ladder", new Vector2(area.xMin + 0.8f, area.yMax - 1f), 0.5f, 110f, 1.6f);
                    Scrawl(room, root, random, "decal_scrawl_4", 2.2f);
                    break;
                case Room.Restrooms:
                    Prop(room, root, "ArmChair_01", new Vector2(area.center.x, area.yMin + 0.7f), 0.38f, 20f);
                    Prop(room, root, "vintage_crutches_01", new Vector2(area.xMin + 0.7f, area.yMin + 2.4f), 0.2f, 90f);
                    Prop(room, root, "wheelchair_01", new Vector2(area.center.x + 0.6f, area.yMax - 0.8f), 0.38f, 200f);
                    foreach (float z in new[] { area.yMin + 0.75f, area.yMin + 2.45f, area.yMin + 4.15f })
                    {
                        if (BuildKit.Prop("ornate_mirror_01", root, new Vector3(area.xMax - 0.26f, 1.2f, z), 270f, 1.05f, false) == null)
                        {
                            break;
                        }
                    }
                    break;
                case Room.BackHall:
                    Prop(room, root, "wheelchair_01", new Vector2(area.xMax - 3.4f, area.yMax - 0.5f), 0.36f, 190f);
                    Stack(room, root, new Vector2(area.xMin + 3f, area.yMin + 0.55f));
                    Prop(room, root, "vintage_crutches_01", new Vector2(area.center.x - 2f, area.yMin + 0.32f), 0.2f, 0f);
                    ShoePrints(root, new Vector3(area.xMin + 7f, 0.014f, area.center.y), 90f);
                    break;
                case Room.MainHall:
                    DoorCurtains(room, root, red);
                    Valance(room, root, height - 0.02f, 0.34f, red, 6);
                    Prop(room, root, "marble_bust_01", new Vector2(area.xMin + 2f, area.yMin + 0.45f), 0.25f, 0f, 1.5f);
                    ShoePrints(root, new Vector3(area.center.x + 2f, 0.022f, area.center.y + 0.1f), 90f);
                    ShoePrints(root, new Vector3(area.center.x + 5f, 0.022f, area.center.y - 0.1f), 90f);
                    break;
                case Room.ExitLobby:
                    DoorCurtains(room, root, Velvet(new Color(0.34f, 0.08f, 0.32f)));
                    Valance(room, root, height - 0.02f, 0.34f, Velvet(new Color(0.34f, 0.08f, 0.32f)), 6);
                    Rocking(room, root, new Vector2(area.xMin + 1.1f, area.yMax - 1.2f), 145f);
                    Stack(room, root, new Vector2(area.xMax - 1.4f, area.yMin + 0.9f));
                    Scrawl(room, root, random, "decal_scrawl_5", 3f);
                    break;
                case Room.Banquet:
                    DoorCurtains(room, root, red);
                    Valance(room, root, height - 0.02f, 0.34f, red, 8);
                    Prop(room, root, "painted_wooden_sofa", new Vector2(area.center.x + 3f, area.yMin + 0.75f), 0.95f, 0f);
                    Prop(room, root, "ArmChair_01", new Vector2(area.xMax - 2f, area.yMin + 0.8f), 0.55f, 330f);
                    Prop(room, root, "marble_bust_01", new Vector2(area.xMin + 1.2f, area.yMax - 0.5f), 0.25f, 180f, 1.5f);
                    Scrawl(room, root, random, "decal_scrawl_0", 2.6f);
                    break;
                case Room.Veranda:
                    Prop(room, root, "gothic_statue", new Vector2(area.xMax - 1.4f, area.center.y + 0.3f), 0.85f, 250f, 0.95f);
                    Rocking(room, root, new Vector2(area.xMin + 1.6f, area.yMin + 1.4f), 40f);
                    Prop(room, root, "wooden_bucket_01", new Vector2(area.center.x + 0.4f, area.yMin + 0.9f), 0.25f, 40f);
                    break;
            }
        }

        private static void Prop(Room room, Transform root, string name, Vector2 desired, float radius, float yaw, float scale = 1f)
        {
            if (!BuildKit.HasProp(name))
            {
                return;
            }
            // Nothing white survives years of damp.
            Color? aged = name.StartsWith("painted") ? new Color(0.46f, 0.42f, 0.37f) : (Color?)null;
            bool placed = LevelProps.PlaceNear(room, desired, radius, p => BuildKit.Prop(name, root, new Vector3(p.x, 0f, p.y), yaw, scale, true, aged), 5f);
            if (!placed && !Application.isPlaying)
            {
                Debug.Log("CIRCUS no room in " + room + " for " + name + " near " + desired);
            }
        }

        /// <summary>Show crates piled on top of each other.</summary>
        private static void Stack(Room room, Transform root, Vector2 desired)
        {
            if (!BuildKit.HasProp("wooden_crate_02"))
            {
                return;
            }
            LevelProps.PlaceNear(room, desired, 0.7f, p =>
            {
                BuildKit.Prop("wooden_crate_02", root, new Vector3(p.x, 0f, p.y), 12f);
                BuildKit.Prop("wooden_crate_01", root, new Vector3(p.x + 0.05f, 0.46f, p.y - 0.03f), -9f, 1f, false);
            }, 5f);
        }

        /// <summary>The head of a carousel horse left on a stool, staring at the door.</summary>
        private static void HorseHead(Room room, Transform root, Vector2 desired)
        {
            if (!BuildKit.HasProp("horse_head"))
            {
                return;
            }
            LevelProps.PlaceNear(room, desired, 0.32f, p =>
            {
                BuildKit.Prop("wooden_stool_01", root, new Vector3(p.x, 0f, p.y), 15f, 1.4f);
                BuildKit.Prop("horse_head", root, new Vector3(p.x, 0.61f, p.y), 170f, 1.6f, false);
            }, 4f);
        }

        /// <summary>A rocking chair. It rocks. Nobody is in it.</summary>
        private static void Rocking(Room room, Transform root, Vector2 desired, float yaw)
        {
            if (!BuildKit.HasProp("Rockingchair_01"))
            {
                return;
            }
            bool placed = LevelProps.PlaceNear(room, desired, 0.55f, p =>
            {
                var pivot = new GameObject("RockingChair").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(p.x, 0f, p.y);
                pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
                BuildKit.Prop("Rockingchair_01", pivot, Vector3.zero, 0f, 1f, false, new Color(0.46f, 0.42f, 0.37f));
                pivot.gameObject.AddComponent<RockingProp>();
            }, 4f);
            if (!placed && !Application.isPlaying)
            {
                Debug.Log("CIRCUS no room in " + room + " for the rocking chair near " + desired);
            }
        }

        private static void Scrawl(Room room, Transform root, System.Random random, string name, float width)
        {
            Texture2D texture = BuildKit.SignTexture(name);
            if (texture == null)
            {
                return;
            }
            Material material = BuildKit.Decal(name, texture, Color.white);
            float tall = width * texture.height / texture.width;
            for (int attempt = 0; attempt < 10; attempt++)
            {
                if (LevelClutter.WallDecal(room, root, PizzeriaLayout.Area(room), random, material, width, tall, 1.55f, 0.9f, 20))
                {
                    return;
                }
            }
        }

        /// <summary>Greasepaint footprints as big as a clown's shoes, walking along the floor.</summary>
        private static void ShoePrints(Transform root, Vector3 at, float yaw)
        {
            Texture2D texture = BuildKit.SignTexture("decal_shoeprints");
            if (texture != null)
            {
                BuildKit.Quad("ShoePrints", root, at, new Vector2(0.75f, 3f), 0f, BuildKit.Decal("decal_shoeprints", texture, Color.white))
                    .transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
            }
        }

        /// <summary>A scalloped cloth border along the top of the walls, broken at each doorway.</summary>
        private static void Valance(Room room, Transform root, float top, float drop, Material material, int seed)
        {
            foreach ((float from, float to, float at, bool alongX) in WallSpans(room))
            {
                float length = to - from;
                if (length < 0.7f)
                {
                    continue;
                }
                float middle = (from + to) * 0.5f;
                Vector3 position = alongX ? new Vector3(middle, top, at) : new Vector3(at, top, middle);
                Drape("Valance", root, position, alongX ? 0f : 90f, length, drop, material, Mathf.RoundToInt(length * 2.5f), 0.045f, seed + (int)(at * 7f), Mathf.Max(1, Mathf.RoundToInt(length / 0.8f)));
            }
        }

        /// <summary>The stretches of wall of a room between its doorways, just inside the room.</summary>
        private static IEnumerable<(float from, float to, float at, bool alongX)> WallSpans(Room room)
        {
            Rect area = PizzeriaLayout.Area(room);
            float inset = PizzeriaLayout.WallThickness + 0.05f;
            var walls = new[]
            {
                (area.xMin, area.xMax, area.yMax, area.yMax - inset, true),
                (area.xMin, area.xMax, area.yMin, area.yMin + inset, true),
                (area.yMin, area.yMax, area.xMin, area.xMin + inset, false),
                (area.yMin, area.yMax, area.xMax, area.xMax - inset, false),
            };
            foreach ((float from, float to, float boundary, float at, bool alongX) in walls)
            {
                var cuts = new List<float>();
                foreach (Vector2 door in PizzeriaLayout.DoorwaysOf(room))
                {
                    if (Mathf.Abs((alongX ? door.y : door.x) - boundary) < 0.05f)
                    {
                        cuts.Add(alongX ? door.x : door.y);
                    }
                }
                cuts.Sort();
                float start = from + 0.1f;
                foreach (float cut in cuts)
                {
                    yield return (start, cut - PizzeriaLayout.DoorWidth * 0.5f - 0.1f, at, alongX);
                    start = cut + PizzeriaLayout.DoorWidth * 0.5f + 0.1f;
                }
                yield return (start, to - 0.1f, at, alongX);
            }
        }

        /// <summary>Heavy curtains drawn to the sides of each doorway of the public rooms.</summary>
        private static void DoorCurtains(Room room, Transform root, Material material)
        {
            Rect area = PizzeriaLayout.Area(room);
            float inset = PizzeriaLayout.WallThickness + 0.09f;
            int seed = 0;
            foreach (Vector2 door in PizzeriaLayout.DoorwaysOf(room))
            {
                bool alongX = Mathf.Abs(door.y - area.yMin) < 0.05f || Mathf.Abs(door.y - area.yMax) < 0.05f;
                bool onMax = alongX ? Mathf.Abs(door.y - area.yMax) < 0.05f : Mathf.Abs(door.x - area.xMax) < 0.05f;
                float at = alongX ? (onMax ? door.y - inset : door.y + inset) : (onMax ? door.x - inset : door.x + inset);
                foreach (float side in new[] { -1f, 1f })
                {
                    float along = (alongX ? door.x : door.y) + side * (PizzeriaLayout.DoorWidth * 0.5f + 0.32f);
                    float min = alongX ? area.xMin : area.yMin;
                    float max = alongX ? area.xMax : area.yMax;
                    if (along < min + 0.45f || along > max - 0.45f)
                    {
                        continue;
                    }
                    Vector3 top = alongX ? new Vector3(along, PizzeriaLayout.DoorHeight + 0.22f, at) : new Vector3(at, PizzeriaLayout.DoorHeight + 0.22f, along);
                    if (!ClearOfSpots(room, new Vector2(top.x, top.z)))
                    {
                        continue;
                    }
                    Drape("DoorCurtain", root, top, alongX ? 0f : 90f, 0.6f, PizzeriaLayout.DoorHeight + 0.19f, material, 4, 0.06f, 300 + seed++);
                }
            }
        }

        private static bool ClearOfSpots(Room room, Vector2 point)
        {
            for (int spot = 0; spot < HuntMap.SpotCount(room); spot++)
            {
                Vector3 position = PizzeriaLayout.SpotPosition(room, spot);
                if (Vector2.Distance(point, new Vector2(position.x, position.z)) < 1.15f)
                {
                    return false;
                }
            }
            if (PizzeriaLayout.HasFuseShelf(room))
            {
                Vector3 shelf = PizzeriaLayout.FusePosition(room);
                return Vector2.Distance(point, new Vector2(shelf.x, shelf.z)) > 1f;
            }
            return true;
        }

        /// <summary>
        /// A piece of hanging cloth: folds that deepen toward the hem and an uneven hem, or scallops.
        /// It hangs from <paramref name="top"/>, along its local x. Seen from both sides; no collider.
        /// </summary>
        public static GameObject Drape(string name, Transform parent, Vector3 top, float yaw, float width, float drop, Material material, int folds, float depth, int seed, int scallops = 0)
        {
            var rng = new System.Random(seed);
            float phase = (float)rng.NextDouble() * 6.28f;
            float phaseB = (float)rng.NextDouble() * 6.28f;
            int across = Mathf.Clamp(folds * 8, 12, 220);
            const int down = 6;
            var vertices = new List<Vector3>((across + 1) * (down + 1));
            var uvs = new List<Vector2>(vertices.Capacity);
            var triangles = new List<int>(across * down * 6);
            for (int i = 0; i <= across; i++)
            {
                float u = i / (float)across;
                float hem = scallops > 0 ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Mathf.PI * scallops * u)) : 1f - 0.05f * Mathf.PerlinNoise(u * 9f, seed * 0.13f);
                for (int j = 0; j <= down; j++)
                {
                    float t = j / (float)down;
                    float amount = depth * (0.4f + 0.6f * t);
                    float wave = Mathf.Sin(u * folds * 6.2832f + phase) + 0.35f * Mathf.Sin(u * folds * 14.3f + phaseB + t);
                    float y = -drop * t * hem;
                    vertices.Add(new Vector3((u - 0.5f) * width, y, wave * amount));
                    uvs.Add(new Vector2(u * width, y));
                }
            }
            for (int i = 0; i < across; i++)
            {
                for (int j = 0; j < down; j++)
                {
                    int a = i * (down + 1) + j;
                    int b = a + down + 1;
                    triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
                }
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = top;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = DoubleSided(name, vertices, uvs, new[] { triangles });
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>A mesh seen from both faces, with normals and tangents for the scanned materials.</summary>
        public static Mesh DoubleSided(string name, List<Vector3> vertices, List<Vector2> uvs, IList<List<int>> submeshes)
        {
            var front = new Mesh { name = name };
            front.SetVertices(vertices);
            front.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++)
            {
                front.SetTriangles(submeshes[s], s);
            }
            front.RecalculateNormals();
            Vector3[] normals = front.normals;
            int count = vertices.Count;
            var allVertices = new List<Vector3>(vertices);
            allVertices.AddRange(vertices);
            var allUvs = new List<Vector2>(uvs);
            allUvs.AddRange(uvs);
            var allNormals = new List<Vector3>(normals);
            foreach (Vector3 normal in normals)
            {
                allNormals.Add(-normal);
            }
            var mesh = new Mesh { name = name };
            if (allVertices.Count > 60000)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            mesh.SetVertices(allVertices);
            mesh.SetUVs(0, allUvs);
            mesh.SetNormals(allNormals);
            mesh.subMeshCount = submeshes.Count;
            for (int s = 0; s < submeshes.Count; s++)
            {
                var both = new List<int>(submeshes[s]);
                for (int t = 0; t < submeshes[s].Count; t += 3)
                {
                    both.Add(submeshes[s][t] + count);
                    both.Add(submeshes[s][t + 2] + count);
                    both.Add(submeshes[s][t + 1] + count);
                }
                mesh.SetTriangles(both, s);
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            Object.DestroyImmediate(front);
            return mesh;
        }
    }

    /// <summary>Tips its object slowly back and forth around the floor, as if somebody had just got up.</summary>
    public class RockingProp : MonoBehaviour
    {
        private Quaternion rest;
        private float offset;

        private void Awake()
        {
            rest = transform.localRotation;
            offset = Random.value * 10f;
        }

        private void Update()
        {
            transform.localRotation = rest * Quaternion.Euler(Mathf.Sin((Time.time + offset) * 1.35f) * 4.5f, 0f, 0f);
        }
    }
}
