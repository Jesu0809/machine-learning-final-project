using NightShift.Hunt;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightShift.Game
{
    /// <summary>
    /// What makes the building Bellwether's Winter Circus: striped canopies, sawdust, pennants, the
    /// midway's games, trunks, costumes and the clown dummies. Added on top of the rooms by
    /// <see cref="LevelProps"/>; anything with a collider is placed clear of the walking lines, so the
    /// map stays the one the brain was trained on.
    /// </summary>
    public static class CircusDressing
    {
        private static readonly Color Scarlet = new Color(0.52f, 0.06f, 0.07f);
        private static readonly Color Cream = new Color(0.66f, 0.58f, 0.44f);
        private static readonly Color Navy = new Color(0.07f, 0.1f, 0.24f);
        private static readonly Color Gold = new Color(0.82f, 0.6f, 0.16f);

        private static Material Red => BuildKit.Pbr("canvas_red", Color.white) ?? BuildKit.Surface("tentRed", () => ProceduralTextures.Grimy(Scarlet, 0.5f), 0.1f);
        private static Material Pale => BuildKit.Pbr("canvas_cream", new Color(0.58f, 0.56f, 0.52f)) ?? BuildKit.Surface("tentCream", () => ProceduralTextures.Grimy(Cream, 0.5f), 0.1f);
        private static Material Blue => BuildKit.Pbr("canvas_navy", Color.white) ?? BuildKit.Plain("circusNavy", Navy, 0.3f);
        private static Material Brass => BuildKit.Plain("circusBrass", Gold, 0.55f, 0.8f);
        private static Material Timber => BuildKit.Pbr("distressed_planks", new Color(0.62f, 0.46f, 0.36f)) ?? BuildKit.Surface("circusTimber", () => ProceduralTextures.Planks(new Color(0.34f, 0.2f, 0.11f)), 0.3f);

        public static void Add(Room room, Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(room);
            float height = PizzeriaLayout.Height(room);
            switch (room)
            {
                case Room.DiningHall:
                    Canopy(root, area, height - 1.1f, height - 0.08f, 18);
                    Sawdust(root, new Vector2(area.center.x, area.center.y - 0.6f), Mathf.Min(area.width, area.height) * 0.47f);
                    Trapeze(root, new Vector3(area.center.x - 2.2f, height - 0.9f, area.center.y));
                    Trapeze(root, new Vector3(area.center.x + 2.4f, height - 0.8f, area.center.y + 1f));
                    for (int i = 0; i < 4; i++)
                    {
                        float z = area.yMin + 1.6f + i * 1.9f;
                        Put(room, new Vector2(area.xMin + 0.75f, z), 0.5f, p => Bleacher(root, p, 90f));
                        Put(room, new Vector2(area.xMax - 0.75f, z), 0.5f, p => Bleacher(root, p, 270f));
                    }
                    Put(room, new Vector2(area.center.x - 3f, area.yMin + 2f), 0.4f, p => RingTub(root, p, 0));
                    Put(room, new Vector2(area.center.x + 3f, area.yMin + 2f), 0.4f, p => RingTub(root, p, 1));
                    Put(room, new Vector2(area.center.x - 3.6f, area.center.y + 1.2f), 0.4f, p => RingTub(root, p, 2));
                    Dummy(room, root, new Vector2(area.xMin + 0.8f, area.yMin + 0.8f), 40f, 0);
                    break;
                case Room.Arcade:
                    Canopy(root, area, height - 0.9f, height - 0.08f, 14);
                    Put(room, new Vector2(area.xMax - 0.7f, area.yMin + 1.4f), 0.55f, p => HighStriker(root, p, 270f));
                    Put(room, new Vector2(area.xMin + 1.6f, area.yMin + 1.4f), 0.75f, p => RingToss(root, p, 45f, random));
                    Put(room, new Vector2(area.xMax - 2.2f, area.yMax - 0.8f), 0.6f, p => ShootingGallery(root, p, 180f, random));
                    Put(room, new Vector2(area.xMin + 1.4f, area.yMax - 0.8f), 0.55f, p => FortuneBooth(root, p, 180f));
                    Put(room, new Vector2(area.xMin + 0.7f, area.center.y), 0.5f, p => BalloonBoard(root, p, 90f, random));
                    Bulbs(root, new Vector3(area.xMin + 0.5f, 2.9f, area.yMin + 0.26f), new Vector3(area.xMax - 0.5f, 2.9f, area.yMin + 0.26f), 12);
                    Bulbs(root, new Vector3(area.xMin + 0.5f, 2.9f, area.yMax - 0.26f), new Vector3(area.xMax - 0.5f, 2.9f, area.yMax - 0.26f), 12);
                    break;
                case Room.Kitchen:
                    Put(room, new Vector2(area.xMin + 2.2f, area.yMax - 0.8f), 0.7f, p => ConcessionCounter(root, p, 180f, random));
                    Put(room, new Vector2(area.xMax - 2.4f, area.yMax - 0.8f), 0.7f, p => ConcessionCounter(root, p, 180f, random));
                    Put(room, new Vector2(area.center.x + 1.4f, area.yMax - 1.6f), 0.7f, p => PopcornCart(root, p, 180f));
                    Put(room, new Vector2(area.xMin + 1f, area.center.y), 0.45f, p => CottonCandy(root, p));
                    Put(room, new Vector2(area.xMax - 0.8f, area.center.y), 0.35f, p => BuildKit.Prop("Barrel_02", root, new Vector3(p.x, 0f, p.y), 30f, 0.9f));
                    Bunting(root, new Vector3(area.xMin + 0.4f, 2.8f, area.center.y), new Vector3(area.xMax - 0.4f, 2.8f, area.center.y), 22);
                    break;
                case Room.Storage:
                    for (int i = 0; i < 2; i++)
                    {
                        int index = i;
                        Put(room, new Vector2(area.xMin + 0.9f, area.yMax - 0.9f - i * 1.2f), 0.35f, p => Trunk(root, p, 90f + index * 9f, index, random));
                    }
                    Put(room, new Vector2(area.center.x + 1f, area.yMax - 1.3f), 0.32f, p => HayBales(root, p));
                    Put(room, new Vector2(area.xMax - 0.9f, area.center.y), 0.45f, p => CostumeRack(root, p, 90f, random));
                    break;
                case Room.Parts:
                    Masks(root, area, random);
                    Put(room, new Vector2(area.xMax - 0.9f, area.yMax - 0.9f), 0.25f, p => Unicycle(root, p));
                    Put(room, new Vector2(area.xMin + 1f, area.center.y), 0.3f, p => PaintCans(root, p, random));
                    Dummy(room, root, new Vector2(area.xMin + 0.8f, area.yMin + 0.8f), 60f, 1);
                    break;
                case Room.Restrooms:
                    for (int i = 0; i < 3; i++)
                    {
                        Vanity(root, new Vector3(area.xMax - 0.24f, 1.55f, area.yMin + 1.4f + i * 1.7f), 270f);
                        Put(room, new Vector2(area.xMax - 0.7f, area.yMin + 1.4f + i * 1.7f), 0.25f, p => Stool(root, p));
                    }
                    Put(room, new Vector2(area.xMin + 1f, area.center.y), 0.45f, p => CostumeRack(root, p, 90f, random));
                    Put(room, new Vector2(area.xMin + 1f, area.yMin + 1f), 0.5f, p => FoldingScreen(root, p, 35f));
                    Put(room, new Vector2(area.center.x, area.yMax - 0.7f), 0.3f, p => WigStand(root, p));
                    Dummy(room, root, new Vector2(area.xMin + 0.8f, area.yMax - 0.8f), 130f, 2);
                    break;
                case Room.BackHall:
                    Put(room, new Vector2(area.xMin + 6f, area.yMax - 0.45f), 0.3f, p => Trunk(root, p, 12f, 1, random));
                    Put(room, new Vector2(area.xMin + 11f, area.yMin + 0.45f), 0.3f, p => Sandbags(root, p));
                    Put(room, new Vector2(area.xMin + 15f, area.yMax - 0.45f), 0.3f, p => Sandbags(root, p));
                    PaintedFlat(root, new Vector3(area.xMin + 9f, 1.4f, area.yMax - 0.22f), 180f, 0);
                    PaintedFlat(root, new Vector3(area.xMin + 18f, 1.4f, area.yMin + 0.22f), 0f, 1);
                    Dummy(room, root, new Vector2(area.center.x + 2.5f, area.yMax - 0.5f), 180f, 3);
                    break;
                case Room.MainHall:
                    Bunting(root, new Vector3(area.xMin + 0.4f, 2.75f, area.yMin + 0.3f), new Vector3(area.xMax - 0.4f, 2.75f, area.yMin + 0.3f), 34);
                    Bunting(root, new Vector3(area.xMin + 0.4f, 2.75f, area.yMax - 0.3f), new Vector3(area.xMax - 0.4f, 2.75f, area.yMax - 0.3f), 34);
                    for (int i = 0; i < 3; i++)
                    {
                        FunhouseMirror(root, new Vector3(area.xMin + 5f + i * 6f, 1.25f, area.yMax - 0.24f), 180f, i);
                    }
                    Dummy(room, root, new Vector2(area.xMax - 2.2f, area.yMax - 0.5f), 180f, 4);
                    break;
                case Room.PartyRoom:
                    Canopy(root, area, height - 0.8f, height - 0.08f, 12, Blue, Pale);
                    Bunting(root, new Vector3(area.xMin + 0.4f, 2.5f, area.yMin + 0.4f), new Vector3(area.xMax - 0.4f, 2.5f, area.yMax - 0.4f), 16);
                    break;
                case Room.ExitLobby:
                    Bulbs(root, new Vector3(area.xMin + 0.5f, 2.85f, area.yMax - 0.26f), new Vector3(area.xMax - 0.5f, 2.85f, area.yMax - 0.26f), 10);
                    Dummy(room, root, new Vector2(area.xMax - 0.8f, area.yMax - 0.8f), 220f, 5);
                    break;
                case Room.Banquet:
                    Bunting(root, new Vector3(area.xMin + 0.5f, 2.9f, area.center.y), new Vector3(area.xMax - 0.5f, 2.9f, area.center.y), 40);
                    Put(room, new Vector2(area.center.x - 5f, area.yMax - 1.3f), 0.8f, p => Cannon(root, p, 160f));
                    Dummy(room, root, new Vector2(area.xMin + 0.8f, area.yMin + 0.8f), 70f, 6);
                    break;
                case Room.Veranda:
                    for (int i = 0; i < 4; i++)
                    {
                        Put(room, new Vector2(area.xMin + 1f + i * 1.9f, area.yMax - 0.7f), 0.4f, p => Planter(root, p, random));
                    }
                    break;
            }
        }

        /// <summary>Places a prop as near as it fits to where it is wanted, clear of the walking lines, doors and spots.</summary>
        private static void Put(Room room, Vector2 desired, float radius, System.Action<Vector2> build)
        {
            if (!LevelProps.PlaceNear(room, desired, radius, build, 9f) && !Application.isPlaying)
            {
                Debug.Log("CIRCUS no room in " + room + " for a prop near " + desired);
            }
        }

        /// <summary>
        /// A spare head of Pockets, its power off, centered on <paramref name="position"/>. At scale 1
        /// it is the size of his own. Without collider.
        /// </summary>
        public static Transform ClownHead(Transform parent, Vector3 position, float scale)
        {
            var head = new GameObject("ClownHead").transform;
            head.SetParent(parent, false);
            head.localPosition = position;
            head.localScale = Vector3.one * scale;
            GameAssets assets = GameAssets.Load();
            if (assets != null && assets.PropHeadMesh != null)
            {
                head.gameObject.AddComponent<MeshFilter>().sharedMesh = assets.PropHeadMesh;
                head.gameObject.AddComponent<MeshRenderer>().sharedMaterial = assets.PropMaterial;
            }
            else
            {
                BuildKit.Sphere("Face", head, Vector3.zero, 0.42f, BuildKit.Plain("clownWhite", new Color(0.78f, 0.75f, 0.68f), 0.45f));
            }
            return head;
        }

        /// <summary>
        /// A retired body of his on a steel stand, life size: from across a dark room you cannot tell
        /// which one it is, except for the pole. It watches the player on the nights the rules say so.
        /// </summary>
        private static void Dummy(Room room, Transform root, Vector2 desired, float yaw, int variant)
        {
            Put(room, desired, 0.42f, p =>
            {
                var dummy = new GameObject("ClownDummy").transform;
                dummy.SetParent(root, false);
                dummy.localPosition = new Vector3(p.x, 0f, p.y);
                dummy.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Material steel = BuildKit.Pbr("rusty_paint", new Color(0.5f, 0.46f, 0.42f)) ?? BuildKit.Plain("chrome", new Color(0.4f, 0.4f, 0.42f), 0.5f, 1f);
                BuildKit.Cylinder("Base", dummy, new Vector3(0f, 0.015f, -0.05f), 0.56f, 0.03f, steel);
                BuildKit.Cylinder("Pole", dummy, new Vector3(0f, 0.8f, -0.2f), 0.035f, 1.6f, steel);
                GameAssets assets = GameAssets.Load();
                if (assets != null && assets.PropDummyMesh != null)
                {
                    var body = new GameObject("Body");
                    body.transform.SetParent(dummy, false);
                    body.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                    body.AddComponent<MeshFilter>().sharedMesh = assets.PropDummyMesh;
                    body.AddComponent<MeshRenderer>().sharedMaterial = FadedProp(assets.PropMaterial, variant);
                }
                // The heads hang at a different angle on every stand.
                ClownHead(dummy, new Vector3(0f, 1.91f, 0.01f), 1f).localRotation = Quaternion.Euler(10f + variant * 3f, variant * 11f - 30f, variant * 7f - 18f);
                var collider = dummy.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.95f, 0f);
                collider.size = new Vector3(0.55f, 1.9f, 0.55f);
                dummy.gameObject.AddComponent<ClownDummy>();
            });
        }

        private static readonly System.Collections.Generic.Dictionary<int, Material> Faded = new System.Collections.Generic.Dictionary<int, Material>();

        /// <summary>The same paint, more or less rotted: no two bodies have been left in the same damp.</summary>
        private static Material FadedProp(Material source, int variant)
        {
            int kind = variant % 3;
            if (source == null || kind == 0)
            {
                return source;
            }
            if (!Faded.TryGetValue(kind, out Material material) || material == null)
            {
                material = new Material(source) { name = "PropFaded" + kind };
                material.SetColor("_BaseColor", kind == 1 ? new Color(0.62f, 0.6f, 0.58f) : new Color(0.72f, 0.66f, 0.62f));
                Faded[kind] = material;
            }
            return material;
        }

        /// <summary>A striped fabric roof under the ceiling, rising to the middle of the area. Casts no shadow, so the lamps still light the room.</summary>
        /// <summary>
        /// A striped tent roof over the room: panels of canvas sewn at a center pole. The cloth sags
        /// between the ribs and curves down to the eaves. Lights nothing; casts no shadow.
        /// </summary>
        public static GameObject Canopy(Transform root, Rect area, float edge, float top, int stripes, Material first = null, Material second = null, float uvScale = 1f)
        {
            var go = new GameObject("Canopy");
            go.transform.SetParent(root, false);
            int panels = stripes * 2;
            const int rings = 6;
            const int across = 6;
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var a = new System.Collections.Generic.List<int>();
            var b = new System.Collections.Generic.List<int>();
            var middle = new Vector2(area.center.x, area.center.y);
            float sag = (top - edge) * 0.12f;
            for (int i = 0; i < panels; i++)
            {
                Vector3 from = OnEdge(area, edge, i * 2f * Mathf.PI / panels);
                Vector3 to = OnEdge(area, edge, (i + 1) * 2f * Mathf.PI / panels);
                int start = vertices.Count;
                for (int r = 0; r <= rings; r++)
                {
                    float along = r / (float)rings;
                    for (int s = 0; s <= across; s++)
                    {
                        float side = s / (float)across;
                        Vector2 rim = Vector2.Lerp(new Vector2(from.x, from.z), new Vector2(to.x, to.z), side);
                        Vector2 flat = Vector2.Lerp(middle, rim, along);
                        float y = Mathf.Lerp(top, edge, Mathf.Pow(along, 0.8f)) - sag * Mathf.Sin(side * Mathf.PI) * Mathf.Sin(along * Mathf.PI);
                        vertices.Add(new Vector3(flat.x, y, flat.y));
                        uvs.Add(flat * uvScale);
                    }
                }
                System.Collections.Generic.List<int> target = i % 2 == 0 ? a : b;
                for (int r = 0; r < rings; r++)
                {
                    for (int s = 0; s < across; s++)
                    {
                        int v = start + r * (across + 1) + s;
                        int next = v + across + 1;
                        target.Add(v); target.Add(next); target.Add(v + 1);
                        target.Add(v + 1); target.Add(next); target.Add(next + 1);
                    }
                }
            }
            go.AddComponent<MeshFilter>().sharedMesh = CircusSet.DoubleSided("Canopy", vertices, uvs, new[] { a, b });
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { first != null ? first : Red, second != null ? second : Pale };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static Vector3 OnEdge(Rect area, float y, float angle)
        {
            float dx = Mathf.Cos(angle);
            float dz = Mathf.Sin(angle);
            float reach = Mathf.Min(Mathf.Abs(dx) > 0.001f ? area.width * 0.5f / Mathf.Abs(dx) : 1e6f,
                                    Mathf.Abs(dz) > 0.001f ? area.height * 0.5f / Mathf.Abs(dz) : 1e6f);
            return new Vector3(area.center.x + dx * reach, y, area.center.y + dz * reach);
        }

        /// <summary>The ring: a flat circle of sawdust with a painted curb, nothing to trip on.</summary>
        private static void Sawdust(Transform root, Vector2 center, float radius)
        {
            const int sides = 40;
            var go = new GameObject("Sawdust");
            go.transform.SetParent(root, false);
            var vertices = new Vector3[sides * 3];
            var uvs = new Vector2[sides * 3];
            var triangles = new int[sides * 3];
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * 2f * Mathf.PI / sides;
                float a1 = (i + 1) * 2f * Mathf.PI / sides;
                vertices[i * 3] = new Vector3(center.x, 0.012f, center.y);
                vertices[i * 3 + 1] = new Vector3(center.x + Mathf.Sin(a0) * radius, 0.012f, center.y + Mathf.Cos(a0) * radius);
                vertices[i * 3 + 2] = new Vector3(center.x + Mathf.Sin(a1) * radius, 0.012f, center.y + Mathf.Cos(a1) * radius);
                for (int k = 0; k < 3; k++)
                {
                    uvs[i * 3 + k] = new Vector2(vertices[i * 3 + k].x, vertices[i * 3 + k].z);
                    triangles[i * 3 + k] = i * 3 + k;
                }
            }
            var mesh = new Mesh { name = "Sawdust", vertices = vertices, uv = uvs, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mesh.RecalculateTangents();
            go.AddComponent<MeshRenderer>().sharedMaterial = BuildKit.Pbr("raked_dirt", new Color(0.82f, 0.68f, 0.5f)) ?? BuildKit.Surface("sawdust", () => ProceduralTextures.Grimy(new Color(0.42f, 0.31f, 0.17f), 1.4f), 0.03f);
            for (int i = 0; i < 28; i++)
            {
                float angle = i * 2f * Mathf.PI / 28f;
                var at = new Vector3(center.x + Mathf.Sin(angle) * radius, 0.016f, center.y + Mathf.Cos(angle) * radius);
                float length = 2f * Mathf.PI * radius / 28f;
                BuildKit.Box("Curb", root, at, new Vector3(length, 0.008f, 0.32f), i % 2 == 0 ? Red : Pale, 1f, false, angle * Mathf.Rad2Deg);
            }
        }

        private static void Trapeze(Transform root, Vector3 top)
        {
            Material rope = BuildKit.Plain("rope", new Color(0.34f, 0.27f, 0.17f), 0.05f);
            float drop = 1.3f;
            foreach (float side in new[] { -0.4f, 0.4f })
            {
                BuildKit.Cylinder("Rope", root, top + new Vector3(side, -drop * 0.5f, 0f), 0.022f, drop, rope);
            }
            BuildKit.Cylinder("Bar", root, top + new Vector3(0f, -drop, 0f), 0.04f, 0.85f, Brass).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        /// <summary>A line of pennants, red, cream and navy, from one point to another, sagging a little.</summary>
        private static void Bunting(Transform root, Vector3 from, Vector3 to, int count)
        {
            Material[] colors = { Red, Pale, Blue };
            Material rope = BuildKit.Plain("rope", new Color(0.34f, 0.27f, 0.17f), 0.05f);
            Vector3 along = to - from;
            float yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                float sag = Mathf.Sin(t * count / 6f * Mathf.PI);
                Vector3 at = from + along * t + Vector3.down * Mathf.Abs(sag) * 0.16f;
                GameObject flag = BuildKit.Cone("Pennant", root, at + Vector3.down * 0.17f, 0.11f, 0.34f, colors[i % 3]);
                flag.transform.localRotation = Quaternion.Euler(180f, yaw, 0f);
                flag.transform.localScale = new Vector3(0.11f, 0.17f, 0.012f);
                BuildKit.Box("Line", root, at, new Vector3(0.012f, 0.012f, along.magnitude / count * 1.06f), rope, 1f, false, yaw);
            }
        }

        /// <summary>A row of glowing bulbs along a wall.</summary>
        public static void Bulbs(Transform root, Vector3 from, Vector3 to, int count, float sag = 0.16f)
        {
            Material on = BuildKit.Glow("marqueeBulb", new Color(1f, 0.72f, 0.32f), 2.2f);
            Material dim = BuildKit.Glow("marqueeBulbDim", new Color(1f, 0.5f, 0.22f), 0.8f);
            Material dead = BuildKit.Plain("deadBulb", new Color(0.2f, 0.18f, 0.14f), 0.7f);
            Material wire = BuildKit.Plain("clownBlack", new Color(0.02f, 0.02f, 0.02f), 0.5f);
            Vector3 At(float t) => Vector3.Lerp(from, to, t) - Vector3.up * sag * 4f * t * (1f - t);
            int pieces = Mathf.Max(count, 1) * 2;
            for (int i = 0; i < pieces; i++)
            {
                Vector3 a = At(i / (float)pieces);
                Vector3 b = At((i + 1) / (float)pieces);
                var piece = BuildKit.Cylinder("Wire", root, (a + b) * 0.5f, 0.012f, Vector3.Distance(a, b), wire);
                piece.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 at = At(count > 1 ? i / (count - 1f) : 0.5f) - Vector3.up * 0.06f;
                BuildKit.Sphere("Bulb", root, at, 0.075f, i % 5 == 3 ? dead : i % 7 == 2 ? dim : on);
            }
        }

        /// <summary>The high striker: a tall painted tower with a scale and a bell, a mallet leaning on it.</summary>
        private static void HighStriker(Transform root, Vector2 p, float yaw)
        {
            var t = new GameObject("HighStriker").transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(p.x, 0f, p.y);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            BuildKit.Box("Tower", t, new Vector3(0f, 1.7f, 0f), new Vector3(0.45f, 3.4f, 0.14f), Pale);
            string[] bands = { "#", "#", "#", "#", "#", "#", "#" };
            for (int i = 0; i < bands.Length; i++)
            {
                float k = i / 6f;
                Color color = Color.Lerp(new Color(0.2f, 0.5f, 0.15f), new Color(0.7f, 0.05f, 0.05f), k);
                BuildKit.Box("Band", t, new Vector3(0f, 0.6f + i * 0.4f, 0.075f), new Vector3(0.38f, 0.3f, 0.01f), BuildKit.Plain("strikerBand" + i, color, 0.4f), 1f, false);
            }
            BuildKit.Sphere("Bell", t, new Vector3(0f, 3.5f, 0.05f), 0.3f, Brass);
            BuildKit.Box("Base", t, new Vector3(0f, 0.15f, 0.25f), new Vector3(0.6f, 0.3f, 0.6f), Red, 1f, false);
            BuildKit.Cylinder("Pad", t, new Vector3(0f, 0.33f, 0.25f), 0.2f, 0.06f, BuildKit.Plain("clownBlack", new Color(0.02f, 0.02f, 0.02f), 0.5f));
            BuildKit.Cylinder("Mallet", t, new Vector3(0.4f, 0.55f, 0.1f), 0.05f, 1.1f, Timber).transform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            BuildKit.Cylinder("MalletHead", t, new Vector3(0.53f, 1.1f, 0.1f), 0.2f, 0.3f, Red).transform.localRotation = Quaternion.Euler(0f, 0f, 76f);
            Texture2D title = BuildKit.SignTexture("title_high_striker");
            if (title != null)
            {
                BuildKit.WallSign(t, title, new Vector3(0f, 3.15f, 0.09f), 0f, 0.9f, 1.2f);
            }
        }

        /// <summary>The ring toss: a low platform of bottles under a striped awning, rings scattered around.</summary>
        private static void RingToss(Transform root, Vector2 p, float yaw, System.Random random)
        {
            var t = new GameObject("RingToss").transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(p.x, 0f, p.y);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            BuildKit.Box("Platform", t, new Vector3(0f, 0.4f, 0f), new Vector3(1.6f, 0.8f, 1.2f), Red);
            for (int x = 0; x < 5; x++)
            {
                for (int z = 0; z < 4; z++)
                {
                    Vector3 at = new Vector3(-0.56f + x * 0.28f, 0.8f, -0.39f + z * 0.26f);
                    BuildKit.Cylinder("Bottle", t, at + Vector3.up * 0.1f, 0.075f, 0.2f, BuildKit.Plain("bottleGlass", new Color(0.05f, 0.2f, 0.1f), 0.85f));
                    BuildKit.Cylinder("Neck", t, at + Vector3.up * 0.25f, 0.03f, 0.1f, BuildKit.Plain("bottleGlass", new Color(0.05f, 0.2f, 0.1f), 0.85f));
                }
            }
            foreach (Vector2 post in new[] { new Vector2(-0.8f, -0.6f), new Vector2(0.8f, -0.6f), new Vector2(-0.8f, 0.6f), new Vector2(0.8f, 0.6f) })
            {
                BuildKit.Cylinder("Post", t, new Vector3(post.x, 1.15f, post.y), 0.05f, 2.3f, Brass);
            }
            for (int i = 0; i < 6; i++)
            {
                BuildKit.Box("Awning", t, new Vector3(-0.72f + i * 0.29f, 2.35f, 0f), new Vector3(0.29f, 0.04f, 1.4f), i % 2 == 0 ? Red : Pale, 1f, false);
            }
            Texture2D title = BuildKit.SignTexture("title_ring_toss");
            if (title != null)
            {
                BuildKit.WallSign(t, title, new Vector3(0f, 2.15f, 0.72f), 0f, 1.3f, 1.2f);
            }
        }

        private static void PopcornCart(Transform root, Vector2 p, float yaw)
        {
            var t = new GameObject("PopcornCart").transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(p.x, 0f, p.y);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            BuildKit.Box("Base", t, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.7f, 0.6f), Red);
            BuildKit.Box("Glass", t, new Vector3(0f, 1.2f, 0f), new Vector3(0.9f, 0.7f, 0.5f), BuildKit.Stain("cartGlass", Texture2D.whiteTexture, new Color(0.7f, 0.65f, 0.4f, 0.25f)), 1f, false);
            BuildKit.Box("Roof", t, new Vector3(0f, 1.6f, 0f), new Vector3(1.05f, 0.1f, 0.65f), Pale, 1f, false);
            BuildKit.Cylinder("Kettle", t, new Vector3(0f, 1.25f, 0f), 0.24f, 0.2f, Brass);
            var random = new System.Random(12);
            Material popped = BuildKit.Plain("popcorn", new Color(0.75f, 0.65f, 0.38f), 0.15f);
            for (int i = 0; i < 26; i++)
            {
                BuildKit.Sphere("Kernel", t, new Vector3(-0.38f + (float)random.NextDouble() * 0.76f, 0.9f + (float)random.NextDouble() * 0.12f, -0.18f + (float)random.NextDouble() * 0.36f), 0.07f, popped);
            }
            foreach (float side in new[] { -0.5f, 0.5f })
            {
                BuildKit.Cylinder("Wheel", t, new Vector3(side * 1.05f, 0.18f, 0f), 0.36f, 0.04f, Blue).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            Texture2D title = BuildKit.SignTexture("title_popcorn");
            if (title != null)
            {
                BuildKit.WallSign(t, title, new Vector3(0f, 1.82f, 0.33f), 0f, 0.95f, 1.2f);
            }
        }

        private static void Trunk(Transform root, Vector2 p, float yaw, int index, System.Random random)
        {
            var t = new GameObject("Trunk").transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(p.x, 0f, p.y);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Material skin = index % 2 == 0 ? Red : Blue;
            BuildKit.Box("Body", t, new Vector3(0f, 0.3f, 0f), new Vector3(0.95f, 0.6f, 0.55f), skin);
            foreach (float side in new[] { -0.33f, 0.33f })
            {
                BuildKit.Box("Strap", t, new Vector3(side, 0.3f, 0f), new Vector3(0.07f, 0.62f, 0.57f), Timber, 1f, false);
            }
            BuildKit.Box("Lock", t, new Vector3(0f, 0.45f, 0.285f), new Vector3(0.1f, 0.1f, 0.02f), Brass, 1f, false);
            if (index == 0 || index == 2)
            {
                // A second one stacked on top, a little askew.
                BuildKit.Box("Top", t, new Vector3(0.04f, 0.8f, 0f), new Vector3(0.78f, 0.4f, 0.46f), index == 0 ? Blue : Pale, 1f, false, 6f + (float)random.NextDouble() * 10f);
            }
        }

        private static void CostumeRack(Transform root, Vector2 p, float yaw, System.Random random)
        {
            var t = new GameObject("CostumeRack").transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(p.x, 0f, p.y);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            BuildKit.Cylinder("Rail", t, new Vector3(0f, 1.75f, 0f), 0.04f, 1.5f, Brass).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (float side in new[] { -0.75f, 0.75f })
            {
                BuildKit.Cylinder("Upright", t, new Vector3(side, 0.87f, 0f), 0.05f, 1.75f, Brass);
                BuildKit.Box("Foot", t, new Vector3(side, 0.03f, 0f), new Vector3(0.1f, 0.06f, 0.6f), Timber, 1f, false);
            }
            Material[] cloth = { Red, Pale, Blue, BuildKit.Plain("costumeGreen", new Color(0.1f, 0.26f, 0.13f), 0.1f) };
            for (int i = 0; i < 7; i++)
            {
                float length = 0.85f + (float)random.NextDouble() * 0.45f;
                BuildKit.Box("Costume", t, new Vector3(-0.6f + i * 0.2f, 1.7f - length * 0.5f, 0f), new Vector3(0.14f, length, 0.42f), cloth[random.Next(cloth.Length)], 1f, false);
            }
            var collider = t.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.9f, 0f);
            collider.size = new Vector3(1.6f, 1.8f, 0.5f);
        }

        /// <summary>A row of masks on the north wall of the workshop, the faces looking into the room.</summary>
        private static void Masks(Transform root, Rect area, System.Random random)
        {
            for (int i = 0; i < 5; i++)
            {
                Transform mask = ClownHead(root, new Vector3(area.xMin + 1.3f + i * 0.85f, 1.95f + (i % 2) * 0.16f, area.yMax - 0.32f), 0.55f);
                mask.localRotation = Quaternion.Euler((float)random.NextDouble() * 10f, 180f, (float)random.NextDouble() * 24f - 12f);
            }
        }

        private static void Unicycle(Transform root, Vector2 p)
        {
            var t = new GameObject("Unicycle").transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(p.x, 0f, p.y);
            BuildKit.Cylinder("Wheel", t, new Vector3(0f, 0.33f, 0f), 0.66f, 0.03f, Blue).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            BuildKit.Cylinder("Fork", t, new Vector3(0f, 0.75f, 0f), 0.05f, 0.9f, Brass);
            BuildKit.Box("Seat", t, new Vector3(0f, 1.22f, 0f), new Vector3(0.12f, 0.05f, 0.26f), Red, 1f, false);
        }

        /// <summary>A dressing-room mirror framed in bulbs, half of them dead.</summary>
        private static void Vanity(Transform root, Vector3 center, float facing)
        {
            Quaternion turn = Quaternion.Euler(0f, facing, 0f);
            BuildKit.Box("Frame", root, center, new Vector3(1.05f, 1.05f, 0.05f), Timber, 1f, false, facing);
            BuildKit.Box("Mirror", root, center + turn * Vector3.forward * 0.03f, new Vector3(0.82f, 0.82f, 0.01f), BuildKit.Plain("mirror", new Color(0.4f, 0.43f, 0.45f), 0.95f, 1f), 1f, false, facing);
            Vector3 right = turn * Vector3.right * 0.48f;
            Vector3 up = Vector3.up * 0.48f;
            Vector3 front = turn * Vector3.forward * 0.05f;
            Bulbs(root, center - right - up + front, center - right + up + front, 6);
            Bulbs(root, center + right - up + front, center + right + up + front, 6);
        }

        private static void Planter(Transform root, Vector2 p, System.Random random)
        {
            BuildKit.Box("Planter", root, new Vector3(p.x, 0.3f, p.y), new Vector3(0.7f, 0.6f, 0.7f), Timber);
            // Nobody has watered them since 1987: bare branches.
            Material dead = BuildKit.Plain("deadBranch", new Color(0.09f, 0.07f, 0.05f), 0.08f);
            for (int i = 0; i < 9; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float lean = 10f + (float)random.NextDouble() * 35f;
                float length = 0.7f + (float)random.NextDouble() * 1.1f;
                var branch = BuildKit.Cylinder("Branch", root, new Vector3(p.x, 0.6f, p.y), 0.018f + (float)random.NextDouble() * 0.02f, length, dead);
                branch.transform.localRotation = Quaternion.Euler(Mathf.Cos(angle) * lean, 0f, Mathf.Sin(angle) * lean);
                branch.transform.localPosition += branch.transform.up * (length * 0.5f);
            }
        }

        /// <summary>A section of wooden grandstand against a wall: three rising benches, facing the ring.</summary>
        private static void Bleacher(Transform root, Vector2 p, float yaw)
        {
            var t = new GameObject("Bleacher").transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(p.x, 0f, p.y);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            for (int row = 0; row < 3; row++)
            {
                BuildKit.Box("Bench", t, new Vector3(0f, 0.4f + row * 0.4f, -0.45f * row + 0.3f), new Vector3(1.8f, 0.07f, 0.4f), Timber, 1f, row == 0);
                BuildKit.Box("Riser", t, new Vector3(0f, 0.2f + row * 0.2f, -0.45f * row + 0.1f), new Vector3(1.8f, 0.4f + row * 0.4f, 0.05f), Blue, 1f, false);
            }
        }

        /// <summary>The painted tub an animal sits on in the ring.</summary>
        private static void RingTub(Transform root, Vector2 p, int variant)
        {
            Material paint = variant % 2 == 0 ? Red : Blue;
            BuildKit.Cylinder("Tub", root, new Vector3(p.x, 0.3f, p.y), 0.72f, 0.6f, paint);
            BuildKit.Cylinder("Rim", root, new Vector3(p.x, 0.61f, p.y), 0.78f, 0.04f, Brass);
            BuildKit.Cylinder("Foot", root, new Vector3(p.x, 0.02f, p.y), 0.78f, 0.04f, Brass);
            BuildKit.Star("Star", root, new Vector3(p.x, 0.32f, p.y - 0.37f), 0.14f, 0.06f, 0.012f, Brass);
        }

        /// <summary>A shooting gallery: a booth with tin ducks on a rail under a striped awning.</summary>
        private static void ShootingGallery(Transform root, Vector2 p, float yaw, System.Random random)
        {
            Transform t = Group("ShootingGallery", root, p, yaw);
            BuildKit.Box("Counter", t, new Vector3(0f, 0.5f, 0.25f), new Vector3(1.9f, 1f, 0.3f), Red);
            BuildKit.Box("Back", t, new Vector3(0f, 1.3f, -0.45f), new Vector3(1.9f, 2.4f, 0.06f), Blue, 1f, false);
            Awning(t, 2.5f, 1.9f, 1f);
            for (int row = 0; row < 2; row++)
            {
                BuildKit.Box("Rail", t, new Vector3(0f, 1.3f + row * 0.55f, -0.4f), new Vector3(1.7f, 0.03f, 0.04f), Brass, 1f, false);
                for (int i = 0; i < 6; i++)
                {
                    Vector3 at = new Vector3(-0.72f + i * 0.29f + row * 0.1f, 1.42f + row * 0.55f, -0.39f);
                    bool down = random.NextDouble() < 0.25;
                    GameObject duck = BuildKit.Sphere("Duck", t, at, 0.14f, down ? Blue : BuildKit.Plain("tinDuck", new Color(0.72f, 0.56f, 0.12f), 0.5f, 0.6f));
                    duck.transform.localScale = new Vector3(0.18f, 0.13f, 0.05f);
                    if (!down)
                    {
                        BuildKit.Sphere("DuckHead", t, at + new Vector3(0.07f, 0.08f, 0f), 0.08f, BuildKit.Plain("tinDuck", new Color(0.72f, 0.56f, 0.12f), 0.5f, 0.6f));
                    }
                }
            }
            for (int i = 0; i < 2; i++)
            {
                BuildKit.Cylinder("Rifle", t, new Vector3(-0.5f + i, 1.03f, 0.25f), 0.03f, 0.7f, Timber).transform.localRotation = Quaternion.Euler(90f, i * 12f, 0f);
            }
        }

        /// <summary>A fortune-teller booth: a tall glass case with a dummy in a turban behind it.</summary>
        private static void FortuneBooth(Transform root, Vector2 p, float yaw)
        {
            Transform t = Group("FortuneBooth", root, p, yaw);
            BuildKit.Box("Base", t, new Vector3(0f, 0.45f, 0f), new Vector3(1f, 0.9f, 0.8f), Blue);
            foreach (Vector2 c in new[] { new Vector2(-0.47f, -0.37f), new Vector2(0.47f, -0.37f), new Vector2(-0.47f, 0.37f), new Vector2(0.47f, 0.37f) })
            {
                BuildKit.Cylinder("Post", t, new Vector3(c.x, 1.45f, c.y), 0.05f, 1.1f, Brass);
            }
            BuildKit.Box("Glass", t, new Vector3(0f, 1.45f, 0f), new Vector3(0.94f, 1.1f, 0.74f), BuildKit.Stain("cartGlass", Texture2D.whiteTexture, new Color(0.7f, 0.65f, 0.4f, 0.2f)), 1f, false);
            BuildKit.Box("Crown", t, new Vector3(0f, 2.1f, 0f), new Vector3(1.12f, 0.22f, 0.9f), Red, 1f, false);
            BuildKit.Sphere("Crystal", t, new Vector3(0f, 1.04f, 0.18f), 0.2f, BuildKit.Glow("crystal", new Color(0.4f, 0.75f, 0.8f), 1.1f));
            BuildKit.Cylinder("Shoulders", t, new Vector3(0f, 1.15f, -0.1f), 0.42f, 0.42f, Red);
            ClownHead(t, new Vector3(0f, 1.58f, -0.1f), 0.72f);
        }

        /// <summary>The balloon-dart board on a wall: some balloons still whole.</summary>
        private static void BalloonBoard(Transform root, Vector2 p, float yaw, System.Random random)
        {
            Transform t = Group("BalloonBoard", root, p, yaw);
            BuildKit.Box("Stand", t, new Vector3(0f, 0.4f, 0f), new Vector3(1.8f, 0.8f, 0.4f), Timber);
            BuildKit.Box("Board", t, new Vector3(0f, 1.6f, -0.1f), new Vector3(1.8f, 1.6f, 0.06f), Pale, 1f, false);
            Color[] colors = { new Color(0.6f, 0.05f, 0.06f), new Color(0.08f, 0.14f, 0.5f), new Color(0.7f, 0.5f, 0.08f), new Color(0.08f, 0.35f, 0.15f) };
            for (int x = 0; x < 6; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    if (random.NextDouble() < 0.3)
                    {
                        continue;
                    }
                    BuildKit.Sphere("Balloon", t, new Vector3(-0.72f + x * 0.29f, 1.07f + y * 0.34f, -0.0f), 0.24f, BuildKit.Plain("dartBalloon" + (x + y) % 4, colors[(x + y) % 4], 0.8f));
                }
            }
        }

        /// <summary>A wooden concession stand: a striped front, jars of candy, an awning.</summary>
        private static void ConcessionCounter(Transform root, Vector2 p, float yaw, System.Random random)
        {
            Transform t = Group("ConcessionStand", root, p, yaw);
            BuildKit.Box("Front", t, new Vector3(0f, 0.5f, 0f), new Vector3(1.9f, 1f, 0.6f), BuildKit.Surface("wallConcessions", () => ProceduralTextures.Stripes(new Color(0.56f, 0.38f, 0.1f), new Color(0.46f, 0.05f, 0.06f), 5), 0.08f));
            BuildKit.Box("Top", t, new Vector3(0f, 1.02f, 0f), new Vector3(2f, 0.05f, 0.7f), Timber, 1f, false);
            foreach (float side in new[] { -0.95f, 0.95f })
            {
                BuildKit.Cylinder("Post", t, new Vector3(side, 1.55f, 0.28f), 0.06f, 1.1f, Timber);
                BuildKit.Cylinder("Post", t, new Vector3(side, 1.55f, -0.28f), 0.06f, 1.1f, Timber);
            }
            Awning(t, 2.2f, 2.1f, 0.9f);
            Material glass = BuildKit.Stain("cartGlass", Texture2D.whiteTexture, new Color(0.7f, 0.65f, 0.4f, 0.25f));
            Color[] candy = { new Color(0.65f, 0.1f, 0.2f), new Color(0.75f, 0.55f, 0.1f), new Color(0.1f, 0.4f, 0.3f), new Color(0.35f, 0.1f, 0.5f) };
            for (int i = 0; i < 5; i++)
            {
                Vector3 at = new Vector3(-0.72f + i * 0.36f, 1.17f, 0.05f);
                BuildKit.Cylinder("Candy", t, at + Vector3.down * 0.03f, 0.18f, 0.18f, BuildKit.Plain("candy" + i % 4, candy[i % 4], 0.6f));
                BuildKit.Cylinder("Jar", t, at, 0.22f, 0.26f, glass);
                BuildKit.Cylinder("JarLid", t, at + Vector3.up * 0.15f, 0.2f, 0.03f, Brass);
            }
        }

        private static void CottonCandy(Transform root, Vector2 p)
        {
            BuildKit.Cylinder("Stand", root, new Vector3(p.x, 0.4f, p.y), 0.5f, 0.8f, Red);
            BuildKit.Cylinder("Bowl", root, new Vector3(p.x, 0.88f, p.y), 0.82f, 0.16f, Brass);
            Material floss = BuildKit.Plain("cottonCandy", new Color(0.62f, 0.25f, 0.38f), 0.05f);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 2.1f;
                Vector3 at = new Vector3(p.x + Mathf.Cos(angle) * 0.22f, 1.28f, p.y + Mathf.Sin(angle) * 0.22f);
                BuildKit.Cylinder("Stick", root, at + Vector3.down * 0.18f, 0.015f, 0.38f, Pale);
                BuildKit.Sphere("Floss", root, at, 0.24f, floss);
            }
        }

        /// <summary>The human cannonball's cannon, on two wagon wheels.</summary>
        private static void Cannon(Transform root, Vector2 p, float yaw)
        {
            Transform t = Group("Cannon", root, p, yaw);
            GameObject barrel = BuildKit.Cylinder("Barrel", t, new Vector3(0f, 1.05f, 0.1f), 0.62f, 2.4f, Red);
            barrel.transform.localRotation = Quaternion.Euler(62f, 0f, 0f);
            GameObject mouth = BuildKit.Cylinder("Mouth", t, new Vector3(0f, 1.6f, 1.15f), 0.72f, 0.12f, Brass);
            mouth.transform.localRotation = Quaternion.Euler(62f, 0f, 0f);
            foreach (float side in new[] { -0.5f, 0.5f })
            {
                BuildKit.Cylinder("Wheel", t, new Vector3(side, 0.5f, -0.1f), 1f, 0.06f, Timber).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                BuildKit.Cylinder("Hub", t, new Vector3(side, 0.5f, -0.1f), 0.2f, 0.1f, Brass).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            var collider = t.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.7f, 0f);
            collider.size = new Vector3(1.1f, 1.4f, 1.6f);
        }

        private static void HayBales(Transform root, Vector2 p)
        {
            Material hay = BuildKit.Surface("hay", () => ProceduralTextures.Planks(new Color(0.42f, 0.32f, 0.12f)), 0.05f);
            BuildKit.Box("Bale", root, new Vector3(p.x, 0.25f, p.y), new Vector3(0.9f, 0.5f, 0.5f), hay);
            BuildKit.Box("Bale", root, new Vector3(p.x + 0.08f, 0.75f, p.y), new Vector3(0.9f, 0.5f, 0.5f), hay, 1f, false, 14f);
        }

        private static void PaintCans(Transform root, Vector2 p, System.Random random)
        {
            Color[] colors = { new Color(0.5f, 0.05f, 0.05f), new Color(0.08f, 0.12f, 0.4f), new Color(0.7f, 0.55f, 0.15f) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = new Vector3(p.x + (i % 2) * 0.24f, 0.11f, p.y + (i / 2) * 0.24f);
                BuildKit.Cylinder("Can", root, at, 0.2f, 0.22f, BuildKit.Plain("paintCan", new Color(0.45f, 0.45f, 0.42f), 0.5f, 0.7f));
                BuildKit.Cylinder("Paint", root, at + Vector3.up * 0.112f, 0.18f, 0.005f, BuildKit.Plain("paint" + i % 3, colors[i % 3], 0.8f));
            }
        }

        private static void Stool(Transform root, Vector2 p)
        {
            BuildKit.Cylinder("Seat", root, new Vector3(p.x, 0.5f, p.y), 0.36f, 0.05f, Red);
            BuildKit.Cylinder("Leg", root, new Vector3(p.x, 0.25f, p.y), 0.05f, 0.5f, Brass);
            BuildKit.Cylinder("Foot", root, new Vector3(p.x, 0.02f, p.y), 0.32f, 0.03f, Brass);
        }

        private static void FoldingScreen(Transform root, Vector2 p, float yaw)
        {
            Transform t = Group("FoldingScreen", root, p, yaw);
            for (int i = 0; i < 3; i++)
            {
                BuildKit.Box("Panel", t, new Vector3(-0.56f + i * 0.56f, 0.95f, (i % 2) * 0.16f), new Vector3(0.56f, 1.9f, 0.03f), i % 2 == 0 ? Red : Pale, 1f, false, (i - 1) * 22f);
            }
            var collider = t.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.95f, 0.08f);
            collider.size = new Vector3(1.6f, 1.9f, 0.3f);
        }

        private static void WigStand(Transform root, Vector2 p)
        {
            BuildKit.Cylinder("Pole", root, new Vector3(p.x, 0.65f, p.y), 0.04f, 1.3f, Timber);
            BuildKit.Cylinder("Foot", root, new Vector3(p.x, 0.02f, p.y), 0.3f, 0.03f, Timber);
            BuildKit.Sphere("Block", root, new Vector3(p.x, 1.4f, p.y), 0.26f, Pale);
            Material wig = BuildKit.Plain("wigOrange", new Color(0.55f, 0.18f, 0.03f), 0.05f);
            foreach (Vector3 off in new[] { new Vector3(-0.14f, 0.08f, 0f), new Vector3(0.14f, 0.08f, 0f), new Vector3(0f, 0.15f, 0f), new Vector3(0f, 0.08f, -0.14f) })
            {
                BuildKit.Sphere("Curl", root, new Vector3(p.x, 1.4f, p.y) + off, 0.2f, wig);
            }
        }

        private static void Sandbags(Transform root, Vector2 p)
        {
            Material canvas = BuildKit.Plain("sandbag", new Color(0.36f, 0.3f, 0.2f), 0.05f);
            for (int i = 0; i < 3; i++)
            {
                GameObject bag = BuildKit.Sphere("Sandbag", root, new Vector3(p.x - 0.15f + i * 0.15f, 0.12f + (i == 1 ? 0.14f : 0f), p.y), 0.4f, canvas);
                bag.transform.localScale = new Vector3(0.42f, 0.22f, 0.3f);
            }
        }

        /// <summary>A flat of painted scenery leaning against a wall.</summary>
        private static void PaintedFlat(Transform root, Vector3 at, float facing, int variant)
        {
            Texture2D picture = BuildKit.SignTexture("poster_" + (5 + variant));
            BuildKit.Box("FlatFrame", root, at, new Vector3(2f, 2.7f, 0.05f), Timber, 1f, false, facing);
            if (picture != null)
            {
                Vector3 front = Quaternion.Euler(0f, facing, 0f) * Vector3.forward * 0.03f;
                BuildKit.Quad("FlatPicture", root, at + front, new Vector2(1.86f, 2.48f), facing, BuildKit.Picture("sign:" + picture.name, picture, 0.08f));
            }
        }

        /// <summary>A funhouse mirror on the wall in a bulb frame, warped three different ways.</summary>
        private static void FunhouseMirror(Transform root, Vector3 at, float facing, int variant)
        {
            Quaternion turn = Quaternion.Euler(0f, facing, 0f);
            BuildKit.Box("MirrorFrame", root, at, new Vector3(1.1f, 2.1f, 0.06f), Brass, 1f, false, facing);
            GameObject glass = BuildKit.Sphere("MirrorGlass", root, at + turn * Vector3.forward * 0.03f, 1f, BuildKit.Plain("mirror", new Color(0.4f, 0.43f, 0.45f), 0.95f, 1f));
            glass.transform.localRotation = turn;
            glass.transform.localScale = new Vector3(variant == 1 ? 1.05f : 0.92f, variant == 2 ? 1.96f : 1.86f, 0.05f + variant * 0.04f);
            Vector3 up = Vector3.up * 1.12f;
            Vector3 right = turn * Vector3.right * 0.58f;
            Bulbs(root, at + up - right, at + up + right, 6);
        }

        /// <summary>The clown of the menu: a dummy in the ring's spotlight (see HuntDirector), its head moves by itself.</summary>
        public static Transform MenuFigure(Transform root, Vector3 floor, float yaw, GameObject prefab = null)
        {
            var dummy = new GameObject("MenuClown").transform;
            dummy.SetParent(root, false);
            dummy.position = floor;
            dummy.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (prefab != null && prefab.GetComponent<PocketsRig>() != null)
            {
                Object.Instantiate(prefab, dummy, false).name = "Pockets";
            }
            else
            {
                BuildKit.Cylinder("Legs", dummy, new Vector3(0f, 0.45f, 0f), 0.42f, 0.9f, Blue);
                BuildKit.Cylinder("Body", dummy, new Vector3(0f, 1.25f, 0f), 0.62f, 0.8f, Red);
                BuildKit.Sphere("Belly", dummy, new Vector3(0f, 1f, 0f), 0.68f, Red);
                for (int i = 0; i < 3; i++)
                {
                    BuildKit.Sphere("Button", dummy, new Vector3(0f, 1.45f - i * 0.24f, 0.31f), 0.09f, Brass);
                }
                BuildKit.Cylinder("Ruff", dummy, new Vector3(0f, 1.68f, 0f), 0.78f, 0.07f, Pale);
                foreach (float side in new[] { -1f, 1f })
                {
                    GameObject arm = BuildKit.Cylinder("Arm", dummy, new Vector3(side * 0.4f, 1.22f, 0.05f), 0.15f, 0.72f, Red);
                    arm.transform.localRotation = Quaternion.Euler(-10f, 0f, side * 12f);
                    BuildKit.Sphere("Glove", dummy, new Vector3(side * 0.48f, 0.82f, 0.12f), 0.2f, BuildKit.Plain("clownWhite", new Color(0.78f, 0.75f, 0.68f), 0.45f));
                }
                Transform head = ClownHead(dummy, new Vector3(0f, 1.95f, 0f), 1.2f);
                dummy.gameObject.AddComponent<MenuClown>().Head = head;
            }
            // His own light from below, the only warm thing on the screen, and a red edge behind him.
            var under = new GameObject("MenuLight").AddComponent<Light>();
            under.transform.SetParent(dummy, false);
            under.transform.localPosition = new Vector3(0.1f, 0.4f, 1.6f);
            under.type = LightType.Point;
            under.range = 6f;
            under.intensity = 7f;
            under.color = new Color(1f, 0.72f, 0.45f);
            var edge = new GameObject("MenuEdge").AddComponent<Light>();
            edge.transform.SetParent(dummy, false);
            edge.transform.localPosition = new Vector3(-1.4f, 2.6f, -0.9f);
            edge.type = LightType.Point;
            edge.range = 5f;
            edge.intensity = 8f;
            edge.color = new Color(1f, 0.12f, 0.08f);
            // One hard light on the face: on the home screen it has to be seen from across the room.
            var face = new GameObject("MenuFace").AddComponent<Light>();
            face.transform.SetParent(dummy, false);
            face.transform.localPosition = new Vector3(0.55f, 0.95f, 2.1f);
            face.transform.LookAt(dummy.TransformPoint(new Vector3(0f, 1.82f, 0f)));
            face.type = LightType.Spot;
            face.spotAngle = 34f;
            face.innerSpotAngle = 12f;
            face.range = 7f;
            face.intensity = 44f;
            face.color = new Color(1f, 0.84f, 0.66f);
            return dummy;
        }

        private static Transform Group(string name, Transform root, Vector2 p, float yaw)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            t.localPosition = new Vector3(p.x, 0f, p.y);
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        private static void Awning(Transform t, float height, float width, float depth)
        {
            int stripes = Mathf.RoundToInt(width / 0.3f);
            for (int i = 0; i < stripes; i++)
            {
                BuildKit.Box("Awning", t, new Vector3(-width * 0.5f + (i + 0.5f) * width / stripes, height, 0f), new Vector3(width / stripes, 0.04f, depth), i % 2 == 0 ? Red : Pale, 1f, false)
                    .transform.localRotation = Quaternion.Euler(-14f, 0f, 0f);
            }
        }
    }

    /// <summary>Stares at the player from the menu, and every few seconds jerks its head to one side.</summary>
    public class MenuClown : MonoBehaviour
    {
        public Transform Head;
        private float nextTwitch;
        private Quaternion wanted = Quaternion.identity;

        private void Update()
        {
            if (Time.unscaledTime > nextTwitch)
            {
                nextTwitch = Time.unscaledTime + Random.Range(2.5f, 7f);
                wanted = Quaternion.Euler(Random.Range(-6f, 10f), Random.Range(-24f, 24f), Random.Range(-28f, 28f));
            }
            Head.localRotation = Quaternion.Slerp(Head.localRotation, wanted, Time.unscaledDeltaTime * 16f);
        }
    }
}
