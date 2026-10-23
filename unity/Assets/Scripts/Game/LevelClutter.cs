using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The second layer of detail that makes the building feel used and abandoned: signs over the
    /// doorways, pipes, vents and cables on the ceiling, things on the walls, trash, stains and
    /// puddles on the floor, and a set piece per room. Everything on the floor goes through
    /// <see cref="LevelProps.Place"/>, so Freddy's walking lines stay free.
    /// </summary>
    public static class LevelClutter
    {
        private static readonly Color[] Party =
        {
            new Color(0.85f, 0.15f, 0.15f), new Color(0.15f, 0.4f, 0.85f), new Color(0.95f, 0.75f, 0.1f),
            new Color(0.2f, 0.7f, 0.3f), new Color(0.7f, 0.2f, 0.7f)
        };

        public static void Add(Room room, Transform root, System.Random random)
        {
            DoorSigns(room, root);
            Ceiling(room, root, random);
            Walls(room, root, random);
            Floor(room, root, random);
            SetPiece(room, root, random);
        }

        // ------------------------------------------------------------------ doorway signs

        /// <summary>Over every doorway, on the room side: the name of the room it leads to.</summary>
        private static void DoorSigns(Room room, Transform root)
        {
            Rect area = PizzeriaLayout.Area(room);
            foreach (Room neighbor in HuntMap.GetNeighbors(room))
            {
                Vector3 door = PizzeriaLayout.DoorOf(room, neighbor);
                // Direction from the doorway into this room (doorways sit on one of the four walls).
                Vector3 into;
                if (Mathf.Abs(door.z - area.yMax) < 0.05f) into = Vector3.back;
                else if (Mathf.Abs(door.z - area.yMin) < 0.05f) into = Vector3.forward;
                else if (Mathf.Abs(door.x - area.xMin) < 0.05f) into = Vector3.right;
                else into = Vector3.left;
                float facing = Mathf.Atan2(into.x, into.z) * Mathf.Rad2Deg;
                // The drawn plate: icon and name, in the colors of the wing the door leads to.
                string key = "door_" + System.Text.RegularExpressions.Regex.Replace(HuntMap.DisplayName(neighbor).ToLowerInvariant(), "[^a-z]+", "_").Trim('_');
                Texture2D plate = BuildKit.SignTexture(key);
                if (plate != null)
                {
                    BuildKit.WallSign(root, plate, door + into * (PizzeriaLayout.WallThickness + 0.04f) + Vector3.up * (PizzeriaLayout.DoorHeight + 0.24f), facing, 1.15f, 0.3f);
                    continue;
                }
                string name = HuntMap.DisplayName(neighbor).ToUpperInvariant();
                Texture2D texture = ProceduralTextures.Sign(name, new Color(0.95f, 0.9f, 0.7f), new Color(0.12f, 0.05f, 0.05f), 3, 5);
                float width = Mathf.Min(2.0f, 0.13f * name.Length + 0.2f);
                BuildKit.Quad("DoorSign", root, door + into * (PizzeriaLayout.WallThickness + 0.03f) + Vector3.up * (PizzeriaLayout.DoorHeight + 0.25f),
                              new Vector2(width, width * texture.height / texture.width), facing, BuildKit.Picture("doorSign" + name, texture, 0.35f));
            }
        }

        // ------------------------------------------------------------------ ceiling

        private static void Ceiling(Room room, Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(room);
            float height = PizzeriaLayout.Height(room);
            Material pipe = BuildKit.Plain("pipe", new Color(0.32f, 0.3f, 0.27f), 0.5f, 0.6f);
            Material duct = BuildKit.Surface("duct", () => ProceduralTextures.Grimy(new Color(0.5f, 0.52f, 0.54f), 2f), 0.5f, 0.7f);

            // Two pipes running along the north wall, just under the ceiling.
            for (int i = 0; i < 2; i++)
            {
                var run = BuildKit.Cylinder("Pipe", root, new Vector3(area.center.x, height - 0.18f - i * 0.16f, area.yMax - 0.35f - i * 0.12f), 0.09f - i * 0.03f, area.width - 0.4f, pipe);
                run.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            // A ventilation duct across big rooms, with a grille.
            if (area.width > 9f)
            {
                BuildKit.Box("Duct", root, new Vector3(area.center.x, height - 0.3f, area.yMin + area.height * 0.3f), new Vector3(area.width - 0.4f, 0.45f, 0.6f), duct, 1f, false);
            }
            BuildKit.Box("Vent", root, new Vector3(area.xMin + 1.5f, height - 0.02f, area.center.y), new Vector3(0.6f, 0.04f, 0.6f), BuildKit.Plain("vent", new Color(0.15f, 0.15f, 0.15f), 0.4f, 0.6f), 1f, false);

            // Loose cables and a fallen ceiling tile here and there.
            for (int i = 0; i < 2; i++)
            {
                if (random.NextDouble() < 0.5)
                {
                    continue;
                }
                var x = (float)(area.xMin + 1f + random.NextDouble() * (area.width - 2f));
                var z = (float)(area.yMin + 1f + random.NextDouble() * (area.height - 2f));
                float length = 0.6f + (float)random.NextDouble() * 0.9f;
                var cable = BuildKit.Box("Cable", root, new Vector3(x, height - length * 0.5f, z), new Vector3(0.02f, length, 0.02f), BuildKit.Plain("cable", new Color(0.05f, 0.05f, 0.05f), 0.3f), 1f, false);
                cable.transform.localRotation = Quaternion.Euler((float)random.NextDouble() * 20f - 10f, 0f, (float)random.NextDouble() * 20f - 10f);
            }
            if (random.NextDouble() < 0.6 && area.width > 4f && area.height > 4f)
            {
                var tile = BuildKit.Box("FallenTile", root, new Vector3(area.xMin + area.width * 0.7f, height - 0.35f, area.yMin + area.height * 0.65f), new Vector3(1.2f, 0.03f, 0.6f),
                                        BuildKit.Surface("ceiling", () => ProceduralTextures.CeilingTiles(), 0.05f), 1f, false);
                tile.transform.localRotation = Quaternion.Euler(35f, 20f, 0f);
            }
        }

        // ------------------------------------------------------------------ walls

        private static void Walls(Room room, Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(room);
            float inset = PizzeriaLayout.WallThickness + 0.06f;
            // A fire extinguisher by the first doorway, a clock, an electrical panel.
            foreach (Vector2 door in PizzeriaLayout.DoorwaysOf(room))
            {
                Vector3 position = WallPoint(area, door, 1.6f, inset);
                if (position != Vector3.zero && LevelProps.ClearOfOpenings(room, new Vector2(position.x, position.z), 0.2f))
                {
                    Extinguisher(root, position);
                }
                break;
            }
            if (area.height > 4f)
            {
                GameObject wallClock = BuildKit.Prop("wall_clock", root, new Vector3(area.center.x + 1.2f, 2.5f, area.yMax - inset + 0.01f), 180f, 1.3f, false);
                if (wallClock != null)
                {
                    // Its glass mirrored the room so much the face could not be read.
                    BuildKit.Matte(wallClock, 0.35f);
                }
                else
                {
                    var clock = BuildKit.Cylinder("Clock", root, new Vector3(area.center.x + 1.2f, 2.5f, area.yMax - inset), 0.4f, 0.05f, BuildKit.Plain("clockFace", new Color(0.85f, 0.82f, 0.75f), 0.4f));
                    clock.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    BuildKit.Box("Hand", root, new Vector3(area.center.x + 1.2f, 2.55f, area.yMax - inset - 0.03f), new Vector3(0.02f, 0.14f, 0.01f), BuildKit.Plain("black", Color.black), 1f, false);
                    BuildKit.Box("Hand", root, new Vector3(area.center.x + 1.25f, 2.5f, area.yMax - inset - 0.03f), new Vector3(0.1f, 0.02f, 0.01f), BuildKit.Plain("black", Color.black), 1f, false);
                }
            }
            SecurityCamera(room, root, area);
            Stains(room, root, area, random);
            var panelPoint = new Vector2(area.xMin + 0.3f, area.yMin + area.height * 0.35f);
            if (LevelProps.ClearOfOpenings(room, panelPoint, 0.5f))
            {
                BuildKit.Box("ElectricalPanel", root, new Vector3(area.xMin + inset, 1.6f, panelPoint.y), new Vector3(0.1f, 0.7f, 0.5f), BuildKit.Plain("panelGrey", new Color(0.4f, 0.42f, 0.4f), 0.4f, 0.5f), 1f, false);
                BuildKit.Box("Warning", root, new Vector3(area.xMin + inset + 0.055f, 1.75f, panelPoint.y), new Vector3(0.01f, 0.12f, 0.2f), BuildKit.Plain("yellow", new Color(0.9f, 0.75f, 0.1f), 0.5f), 1f, false);
            }
            // A cork board with notes in staff rooms.
            if (room == Room.Office || room == Room.Kitchen || room == Room.Parts)
            {
                var board = new Vector2(area.xMax - 0.3f, area.yMin + area.height * 0.5f);
                if (LevelProps.ClearOfOpenings(room, board, 0.7f))
                {
                    BuildKit.Box("CorkBoard", root, new Vector3(area.xMax - inset, 1.7f, board.y), new Vector3(0.04f, 0.8f, 1.2f), BuildKit.Plain("cork", new Color(0.55f, 0.38f, 0.22f), 0.1f), 1f, false);
                    for (int i = 0; i < 5; i++)
                    {
                        BuildKit.Box("Note", root, new Vector3(area.xMax - inset - 0.03f, 1.5f + (i % 2) * 0.35f, board.y - 0.45f + i * 0.22f),
                                     new Vector3(0.01f, 0.2f, 0.16f), BuildKit.Plain("paper", new Color(0.85f, 0.83f, 0.75f)), 1f, false);
                    }
                }
            }
        }

        /// <summary>A point on the wall next to a doorway, <paramref name="offset"/> meters to its side.</summary>
        private static Vector3 WallPoint(Rect area, Vector2 door, float offset, float inset)
        {
            if (Mathf.Abs(door.y - area.yMax) < 0.05f) return new Vector3(door.x + offset, 0f, area.yMax - inset);
            if (Mathf.Abs(door.y - area.yMin) < 0.05f) return new Vector3(door.x + offset, 0f, area.yMin + inset);
            if (Mathf.Abs(door.x - area.xMin) < 0.05f) return new Vector3(area.xMin + inset, 0f, door.y + offset);
            if (Mathf.Abs(door.x - area.xMax) < 0.05f) return new Vector3(area.xMax - inset, 0f, door.y + offset);
            return Vector3.zero;
        }

        /// <summary>
        /// A security camera high in a corner of the room, aimed at its middle: the pizzeria still
        /// watches the guard, and the cameras are what the office monitors show.
        /// </summary>
        private static void SecurityCamera(Room room, Transform root, Rect area)
        {
            if (area.width < 4f || area.height < 4f)
            {
                return;
            }
            float height = PizzeriaLayout.Height(room);
            var corner = new Vector3(area.xMax - 0.35f, height - 0.35f, area.yMax - 0.35f);
            var middle = new Vector3(area.center.x, 1f, area.center.y);
            Vector3 look = middle - corner;
            float yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            GameObject camera = BuildKit.Prop("security_camera_01", root, corner, yaw + LevelProps.ScreenFront, 1.2f, false);
            if (camera == null)
            {
                return;
            }
            // What it sees goes to the office monitors.
            Vector3 lens = corner + look.normalized * 0.3f + Vector3.down * 0.12f;
            SecurityFeeds.RegisterCamera(room, lens, Quaternion.LookRotation(middle - lens));
            // A small red light that blinks: it is recording.
            var led = BuildKit.Box("RecordLed", camera.transform, new Vector3(0f, 0.08f, 0.1f), new Vector3(0.015f, 0.015f, 0.015f), BuildKit.Glow("recordLed", new Color(1f, 0.05f, 0.05f), 6f), 1f, false);
            led.AddComponent<Blinker>();
        }

        /// <summary>
        /// Years of a closed building on the walls: water running down from the ceiling, mold
        /// creeping up from the floor in the damp rooms, and here and there something written.
        /// </summary>
        private static void Stains(Room room, Transform root, Rect area, System.Random random)
        {
            float height = PizzeriaLayout.Height(room);
            int leaks = area.width * area.height > 30f ? 3 : 1;
            int placed = 0;
            for (int attempt = 0; attempt < 12 && placed < leaks; attempt++)
            {
                Texture2D leak = BuildKit.SignTexture("decal_leak_" + random.Next(3));
                if (leak == null)
                {
                    return;
                }
                float width = 1.1f + (float)random.NextDouble() * 0.8f;
                float tall = Mathf.Min(height - 0.1f, width * 2f);
                if (WallDecal(room, root, area, random, BuildKit.Decal(leak.name, leak, Color.white), width, tall, height - tall * 0.5f - 0.02f, 0.7f, placed))
                {
                    placed++;
                }
            }
            bool damp = room == Room.Restrooms || room == Room.Kitchen || room == Room.Storage || room == Room.Parts || room == Room.BackHall;
            if (damp)
            {
                Texture2D mold = BuildKit.SignTexture("decal_mold_" + random.Next(2));
                if (mold != null)
                {
                    for (int attempt = 0; attempt < 8; attempt++)
                    {
                        if (WallDecal(room, root, area, random, BuildKit.Decal(mold.name, mold, Color.white), 2.2f, 1.1f, 0.55f, 1.2f, 10))
                        {
                            break;
                        }
                    }
                }
            }
            // Written on the walls, in a few places only.
            (string name, Vector3 position, float facing, float width)? writing = room switch
            {
                Room.BackHall => ("decal_graffiti_1", new Vector3(-15f, 1.45f, -2.79f), 0f, 1.7f),
                Room.MainHall => ("decal_graffiti_0", new Vector3(15.5f, 1.5f, -0.21f), 180f, 1.6f),
                Room.Restrooms => ("decal_graffiti_2", new Vector3(-5.0f, 1.55f, -13.79f), 0f, 1.0f),
                Room.PartyRoom => ("decal_handprints", new Vector3(5.2f, 0.9f, -7.21f), 180f, 1.6f),
                _ => null
            };
            if (writing.HasValue)
            {
                Texture2D texture = BuildKit.SignTexture(writing.Value.name);
                if (texture != null)
                {
                    float w = writing.Value.width;
                    BuildKit.Quad("Graffiti", root, writing.Value.position, new Vector2(w, w * texture.height / texture.width), writing.Value.facing,
                                  BuildKit.Decal(texture.name, texture, Color.white));
                }
            }
        }

        /// <summary>A decal flat on a random wall of the room, clear of doors, spots and the fuse shelf.</summary>
        private static bool WallDecal(Room room, Transform root, Rect area, System.Random random, Material material, float width, float tall, float y, float clearance, int layer)
        {
            int side = random.Next(4);
            bool alongX = side < 2;
            float span = alongX ? area.width : area.height;
            if (span < width + 0.6f)
            {
                return false;
            }
            float along = (alongX ? area.xMin : area.yMin) + width * 0.5f + 0.3f + (float)random.NextDouble() * (span - width - 0.6f);
            float inset = PizzeriaLayout.WallThickness + 0.004f + layer * 0.0015f;
            Vector3 position;
            float facing;
            switch (side)
            {
                case 0: position = new Vector3(along, y, area.yMax - inset); facing = 180f; break;
                case 1: position = new Vector3(along, y, area.yMin + inset); facing = 0f; break;
                case 2: position = new Vector3(area.xMin + inset, y, along); facing = 90f; break;
                default: position = new Vector3(area.xMax - inset, y, along); facing = 270f; break;
            }
            if (!LevelProps.ClearOfOpenings(room, new Vector2(position.x, position.z), clearance + width * 0.3f))
            {
                return false;
            }
            BuildKit.Quad("Stain", root, position, new Vector2(width, tall), facing, material);
            return true;
        }

        private static void Extinguisher(Transform root, Vector3 position)
        {
            Material red = BuildKit.Plain("extinguisher", new Color(0.75f, 0.06f, 0.05f), 0.6f);
            BuildKit.Cylinder("Extinguisher", root, position + Vector3.up * 0.55f, 0.16f, 0.5f, red);
            BuildKit.Cylinder("Nozzle", root, position + Vector3.up * 0.85f, 0.06f, 0.1f, BuildKit.Plain("black", Color.black));
        }

        // ------------------------------------------------------------------ floor

        private static void Floor(Room room, Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(room);
            int litter = Mathf.RoundToInt(area.width * area.height * 0.18f);
            Material paper = BuildKit.Plain("paper", new Color(0.85f, 0.83f, 0.75f));
            Material cup = BuildKit.Plain("cup", new Color(0.8f, 0.15f, 0.15f), 0.3f);
            for (int i = 0; i < litter; i++)
            {
                var point = new Vector2((float)(area.xMin + 0.4f + random.NextDouble() * (area.width - 0.8f)), (float)(area.yMin + 0.4f + random.NextDouble() * (area.height - 0.8f)));
                double kind = random.NextDouble();
                var position = new Vector3(point.x, 0f, point.y);
                float yaw = (float)random.NextDouble() * 360f;
                if (kind < 0.35)
                {
                    BuildKit.Sphere("CrumpledPaper", root, position + Vector3.up * 0.05f, 0.1f, paper);
                }
                else if (kind < 0.55)
                {
                    BuildKit.Box("Napkin", root, position + Vector3.up * 0.003f, new Vector3(0.2f, 0.004f, 0.2f), paper, 1f, false, yaw);
                }
                else if (kind < 0.7)
                {
                    var c = BuildKit.Cylinder("Cup", root, position + Vector3.up * 0.05f, 0.08f, 0.14f, cup);
                    c.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
                }
                else
                {
                    Material confetti = BuildKit.Plain("confetti" + (int)(kind * 50) % 5, Party[(int)(kind * 50) % 5], 0.3f);
                    BuildKit.Box("Confetti", root, position + Vector3.up * 0.002f, new Vector3(0.05f, 0.002f, 0.03f), confetti, 1f, false, yaw);
                }
            }

            // Stains and puddles under leaking pipes.
            Material stain = BuildKit.Stain("stain", ProceduralTextures.Stain(7), new Color(0.12f, 0.08f, 0.04f, 0.8f));
            Material puddle = BuildKit.Plain("puddle", new Color(0.02f, 0.02f, 0.025f), 0.95f);
            for (int i = 0; i < 2 + (int)(area.width * area.height / 40f); i++)
            {
                var position = new Vector3((float)(area.xMin + 1f + random.NextDouble() * (area.width - 2f)), 0.006f, (float)(area.yMin + 1f + random.NextDouble() * (area.height - 2f)));
                float size = 0.8f + (float)random.NextDouble() * 1.6f;
                var quad = BuildKit.Quad("Stain", root, position, new Vector2(size, size), 0f, stain);
                quad.transform.localRotation = Quaternion.Euler(90f, (float)random.NextDouble() * 360f, 0f);
            }
            if (random.NextDouble() < 0.6)
            {
                var position = new Vector2((float)(area.xMin + 1.5f + random.NextDouble() * (area.width - 3f)), (float)(area.yMin + 1.5f + random.NextDouble() * (area.height - 3f)));
                Texture2D wet = BuildKit.SignTexture("decal_puddle");
                if (wet != null)
                {
                    // A shallow, glossy puddle: it mirrors the lamps (the room's reflections do the rest).
                    float size = 1.2f + (float)random.NextDouble();
                    Material water = BuildKit.Decal("puddle", wet, new Color(0.35f, 0.38f, 0.4f, 1f));
                    water.SetFloat("_Smoothness", 0.97f);
                    var quad = BuildKit.Quad("Puddle", root, new Vector3(position.x, 0.004f, position.y), new Vector2(size * 1.5f, size), 0f, water);
                    quad.transform.localRotation = Quaternion.Euler(90f, (float)random.NextDouble() * 360f, 0f);
                }
                else
                {
                    BuildKit.Cylinder("Puddle", root, new Vector3(position.x, 0.003f, position.y), 1.1f + (float)random.NextDouble(), 0.002f, puddle).transform.localScale = new Vector3(1.6f, 0.001f, 1f);
                }
            }

            // Bigger things that need room.
            for (int i = 0; i < 3; i++)
            {
                var point = new Vector2((float)(area.xMin + 0.8f + random.NextDouble() * (area.width - 1.6f)), (float)(area.yMin + 0.8f + random.NextDouble() * (area.height - 1.6f)));
                double kind = random.NextDouble();
                if (kind < 0.35)
                {
                    LevelProps.Place(room, point, 0.4f, () => TippedChair(root, point, (float)random.NextDouble() * 360f));
                }
                else if (kind < 0.65)
                {
                    LevelProps.Place(room, point, 0.35f, () => TrashBin(root, point));
                }
                else
                {
                    LevelProps.Place(room, point, 0.45f, () => BuildKit.Box("Box", root, new Vector3(point.x, 0.22f, point.y), new Vector3(0.5f, 0.44f, 0.5f),
                                                                           BuildKit.Surface("cardboard", () => ProceduralTextures.Grimy(new Color(0.55f, 0.42f, 0.25f), 0.2f), 0.1f), 1f, true, (float)random.NextDouble() * 90f));
                }
            }
        }

        private static void TippedChair(Transform root, Vector2 point, float yaw)
        {
            var chair = new GameObject("TippedChair").transform;
            chair.SetParent(root, false);
            chair.localPosition = new Vector3(point.x, 0.21f, point.y);
            chair.localRotation = Quaternion.Euler(0f, yaw, 90f);
            Material plastic = BuildKit.Plain("chairPlastic", new Color(0.55f, 0.1f, 0.1f), 0.4f);
            BuildKit.Box("Seat", chair, new Vector3(0f, 0.24f, 0f), new Vector3(0.42f, 0.05f, 0.42f), plastic, 1f, false);
            BuildKit.Box("Back", chair, new Vector3(0f, 0.5f, -0.19f), new Vector3(0.42f, 0.5f, 0.04f), plastic, 1f, false);
        }

        private static void TrashBin(Transform root, Vector2 point)
        {
            if (BuildKit.Prop("Barrel_02", root, new Vector3(point.x, 0f, point.y), point.x * 37f, 0.9f) != null)
            {
                return;
            }
            BuildKit.Cylinder("TrashBin", root, new Vector3(point.x, 0.4f, point.y), 0.5f, 0.8f, BuildKit.Plain("bin", new Color(0.18f, 0.25f, 0.2f), 0.3f, 0.3f));
            BuildKit.Sphere("Trash", root, new Vector3(point.x, 0.82f, point.y), 0.35f, BuildKit.Plain("trashBag", new Color(0.05f, 0.05f, 0.05f), 0.7f));
        }

        // ------------------------------------------------------------------ set pieces

        private static void SetPiece(Room room, Transform root, System.Random random)
        {
            switch (room)
            {
                case Room.Office:
                    // The camera monitors: a steel rack on the wall over the desk, two shelves of
                    // old CRTs showing the security cameras live (see SecurityFeeds).
                    Material rack = BuildKit.Pbr("metal_worn", new Color(0.45f, 0.47f, 0.5f)) ?? BuildKit.Plain("crt", new Color(0.18f, 0.17f, 0.15f), 0.3f);
                    for (int r = 0; r < 2; r++)
                    {
                        float shelf = 1.3f + r * 0.5f;
                        BuildKit.Box("MonitorShelf", root, new Vector3(-21.55f, shelf, -10.75f), new Vector3(0.5f, 0.04f, 1.8f), rack, 1f, false);
                        for (int c = 0; c < 3; c++)
                        {
                            LevelProps.Monitor(root, new Vector3(-21.58f, shelf + 0.02f, -11.3f + c * 0.55f), 90f + (c - 1) * 6f);
                        }
                    }
                    foreach (float z in new[] { -11.67f, -9.83f })
                    {
                        BuildKit.Box("RackPost", root, new Vector3(-21.55f, 1.55f, z), new Vector3(0.04f, 0.6f, 0.04f), rack, 1f, false);
                    }
                    break;

                case Room.PartyRoom:
                    // The birthday cake on the table by the south wall.
                    Vector3 cakeSpot = PizzeriaLayout.SpotPosition(room, 1) + new Vector3(0.45f, 0.83f, -0.2f);
                    if (BuildKit.Prop("strawberry_chocolate_cake", root, cakeSpot, 0f, 1.6f, false) == null)
                    {
                        Cake(root, cakeSpot);
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        var gift = new Vector2(9.3f + (i % 2) * 0.45f, -7.7f - (i / 2) * 0.45f);
                        LevelProps.Place(room, gift, 0.3f, () =>
                            Gift(root, new Vector3(gift.x, 0f, gift.y), new Vector3(0.4f - (i % 2) * 0.08f, 0.4f - (i / 2) * 0.12f, 0.4f), i, i * 17f));
                    }
                    LevelProps.Sign(root, new Vector3(4.23f, 2.5f, -11f), 90f, "HAPPY BIRTHDAY", new Color(0.3f, 0.85f, 1f), new Color(0.08f, 0.05f, 0.25f), 3.4f);
                    break;

                case Room.Storage:
                    // A row of spare heads watching the door: one looks straight at you, one has an
                    // eye that still flickers, one lost its hat (it is on the floor below).
                    Quaternion[] poses = { Quaternion.Euler(0f, 262f, 8f), Quaternion.Euler(-6f, 252f, 22f), Quaternion.Euler(4f, 292f, -6f) };
                    HeadStyle[] styles = { HeadStyle.Default, HeadStyle.LiveEye, HeadStyle.NoHat };
                    for (int i = 0; i < 3; i++)
                    {
                        float z = 2.9f + i * 0.7f;
                        BuildKit.Box("MaskShelf", root, new Vector3(-16.4f, 1.85f, z), new Vector3(0.35f, 0.04f, 0.65f), BuildKit.Plain("pipe", new Color(0.32f, 0.3f, 0.27f), 0.5f, 0.6f), 1f, false);
                        GameObject head = FreddyParts.Head(root, new Vector3(-16.45f, 2.17f, z), poses[i], 1f, styles[i]);
                        if (head == null)
                        {
                            LevelProps.SpareHead(root, new Vector3(-16.45f, 2.15f, z), 270f + (i - 1) * 15f);
                        }
                        else if (i == 0)
                        {
                            LevelProps.NeckWires(head.transform);
                        }
                    }
                    FreddyParts.Hat(root, new Vector3(-16.75f, 0.09f, 4.6f), Quaternion.Euler(0f, 40f, 95f), 1f);
                    break;

                case Room.Arcade:
                    // Ball pit in the corner and a claw machine full of little Freddys.
                    LevelProps.PlaceNear(room, new Vector2(16.6f, 3.4f), 1.0f, p =>
                    {
                        Material rim = BuildKit.Plain("pitRim", new Color(0.15f, 0.35f, 0.75f), 0.4f);
                        BuildKit.Box("PitWall", root, new Vector3(p.x, 0.3f, p.y + 1f), new Vector3(2f, 0.6f, 0.1f), rim);
                        BuildKit.Box("PitWall", root, new Vector3(p.x + 0.95f, 0.3f, p.y), new Vector3(0.1f, 0.6f, 2f), rim);
                        for (int i = 0; i < 70; i++)
                        {
                            var ball = new Vector3(p.x - 0.9f + (float)random.NextDouble() * 1.8f, 0.08f + (float)random.NextDouble() * 0.35f, p.y - 0.9f + (float)random.NextDouble() * 1.8f);
                            BuildKit.Sphere("Ball", root, ball, 0.14f, BuildKit.Plain("ball" + i % 5, Party[i % Party.Length], 0.5f));
                        }
                    });
                    LevelProps.PlaceNear(room, new Vector2(22.1f, 9.3f), 0.5f, p =>
                    {
                        ClawMachine(root, p);
                    });
                    break;

                case Room.DiningHall:
                    // Speakers at both ends of the stage and Freddy's microphone.
                    Rect stage = PizzeriaLayout.StageArea;
                    float top = PizzeriaLayout.StageHeight;
                    Material speaker = BuildKit.Plain("speaker", new Color(0.05f, 0.05f, 0.05f), 0.3f);
                    BuildKit.Box("Speaker", root, new Vector3(stage.xMin + 0.4f, top + 0.6f, stage.yMax - 0.9f), new Vector3(0.6f, 1.2f, 0.6f), speaker);
                    BuildKit.Box("Speaker", root, new Vector3(stage.xMax - 0.4f, top + 0.6f, stage.yMax - 0.9f), new Vector3(0.6f, 1.2f, 0.6f), speaker);
                    BuildKit.Cylinder("MicStand", root, new Vector3(stage.center.x - 0.9f, top + 0.6f, stage.yMin + 0.4f), 0.03f, 1.2f, BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f));
                    BuildKit.Sphere("Mic", root, new Vector3(stage.center.x - 0.9f, top + 1.25f, stage.yMin + 0.4f), 0.08f, BuildKit.Plain("black", Color.black));
                    break;

                case Room.ExitLobby:
                    for (int i = 0; i < 3; i++)
                    {
                        var post = new Vector2(13.4f + i * 1.2f, -14.6f);
                        LevelProps.Place(room, post, 0.2f, () => BuildKit.Cylinder("Stanchion", root, new Vector3(post.x, 0.45f, post.y), 0.08f, 0.9f, BuildKit.Plain("brass", new Color(0.8f, 0.6f, 0.25f), 0.7f, 0.9f)));
                    }
                    LevelProps.PlaceNear(room, new Vector2(12.8f, -12.6f), 0.45f, spot =>
                    {
                        var box = new Vector3(spot.x, 0f, spot.y);
                        // An open cardboard box, LOST & FOUND in marker, a small head looking out of it.
                        Material cardboard = BuildKit.Surface("cardboard", () => ProceduralTextures.Grimy(new Color(0.55f, 0.42f, 0.25f), 0.2f), 0.1f);
                        foreach ((Vector3 at, Vector3 size) in new[]
                        {
                            (new Vector3(0f, 0.02f, 0f), new Vector3(0.7f, 0.04f, 0.5f)), (new Vector3(-0.34f, 0.3f, 0f), new Vector3(0.02f, 0.6f, 0.5f)),
                            (new Vector3(0.34f, 0.3f, 0f), new Vector3(0.02f, 0.6f, 0.5f)), (new Vector3(0f, 0.3f, -0.24f), new Vector3(0.7f, 0.6f, 0.02f)),
                            (new Vector3(0f, 0.3f, 0.24f), new Vector3(0.7f, 0.6f, 0.02f)),
                        })
                        {
                            BuildKit.Box("LostAndFound", root, box + at, size, cardboard);
                        }
                        foreach (float side in new[] { -1f, 1f })
                        {
                            BuildKit.Box("Flap", root, box + new Vector3(side * 0.47f, 0.62f, 0f), new Vector3(0.26f, 0.02f, 0.5f), cardboard, 1f, false)
                                .transform.localRotation = Quaternion.Euler(0f, 0f, side * -35f);
                        }
                        Texture2D label = BuildKit.SignTexture("decal_lost_found");
                        if (label != null)
                        {
                            BuildKit.Quad("Label", root, box + new Vector3(0.355f, 0.35f, 0f), new Vector2(0.4f, 0.2f), 90f, BuildKit.Decal("lostFound", label, Color.white));
                        }
                        LevelProps.SpareHead(root, box + new Vector3(0f, 0.6f, 0f), 200f, 0.35f);
                    }, 2.5f);
                    break;
            }
        }

        /// <summary>
        /// A claw machine: a painted metal base with the prize chute, a glass case on four posts with
        /// little Freddy heads piled inside, the claw hanging from its gantry, and a lit header.
        /// </summary>
        private static void ClawMachine(Transform root, Vector2 p)
        {
            var machine = new GameObject("ClawMachine").transform;
            machine.SetParent(root, false);
            machine.localPosition = new Vector3(p.x, 0f, p.y);
            machine.localRotation = Quaternion.Euler(0f, 225f, 0f);
            Material body = BuildKit.Pbr("metal_red", new Color(1f, 0.55f, 0.85f)) ?? BuildKit.Plain("clawBase", new Color(0.7f, 0.1f, 0.4f), 0.4f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Material black = BuildKit.Plain("arcadeBlack", new Color(0.015f, 0.015f, 0.02f), 0.85f);
            Material glass = BuildKit.Decal("clawGlass", Texture2D.whiteTexture, new Color(0.6f, 0.75f, 0.85f, 0.18f));
            glass.SetFloat("_Smoothness", 0.98f);
            BuildKit.Box("Base", machine, new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.9f, 0.8f), body);
            BuildKit.Box("Chute", machine, new Vector3(0.22f, 0.3f, 0.41f), new Vector3(0.24f, 0.24f, 0.02f), black, 1f, false);
            BuildKit.Box("CoinSlot", machine, new Vector3(-0.22f, 0.6f, 0.41f), new Vector3(0.06f, 0.1f, 0.01f), BuildKit.Glow("coinSlot", new Color(1f, 0.45f, 0.1f), 2.5f), 1f, false);
            BuildKit.Box("ControlShelf", machine, new Vector3(0f, 0.92f, 0.36f), new Vector3(0.8f, 0.04f, 0.16f), black, 1f, false);
            BuildKit.Cylinder("Stick", machine, new Vector3(-0.15f, 0.98f, 0.36f), 0.02f, 0.1f, chrome);
            BuildKit.Sphere("StickBall", machine, new Vector3(-0.15f, 1.04f, 0.36f), 0.05f, BuildKit.Plain("joystickBall", new Color(0.75f, 0.05f, 0.05f), 0.8f));
            BuildKit.Cylinder("Button", machine, new Vector3(0.15f, 0.95f, 0.36f), 0.07f, 0.02f, BuildKit.Glow("arcadeButton0", new Color(1f, 0.1f, 0.1f), 0.6f));
            // Glass case on four corner posts.
            foreach (Vector2 corner in new[] { new Vector2(-0.38f, -0.38f), new Vector2(0.38f, -0.38f), new Vector2(-0.38f, 0.38f), new Vector2(0.38f, 0.38f) })
            {
                BuildKit.Box("Post", machine, new Vector3(corner.x, 1.4f, corner.y), new Vector3(0.04f, 1f, 0.04f), chrome, 1f, false);
            }
            BuildKit.Quad("GlassFront", machine, new Vector3(0f, 1.4f, 0.39f), new Vector2(0.74f, 0.96f), 0f, glass);
            BuildKit.Quad("GlassBack", machine, new Vector3(0f, 1.4f, -0.39f), new Vector2(0.74f, 0.96f), 180f, glass);
            BuildKit.Quad("GlassLeft", machine, new Vector3(-0.39f, 1.4f, 0f), new Vector2(0.74f, 0.96f), 270f, glass);
            BuildKit.Quad("GlassRight", machine, new Vector3(0.39f, 1.4f, 0f), new Vector2(0.74f, 0.96f), 90f, glass);
            // The prizes: a heap of little Freddy heads.
            for (int i = 0; i < 9; i++)
            {
                LevelProps.SpareHead(machine, new Vector3(-0.22f + (i % 3) * 0.22f, 0.98f + (i / 3) * 0.06f, -0.2f + (i / 3) * 0.16f), i * 47f, 0.26f);
            }
            // Gantry and claw.
            BuildKit.Box("Gantry", machine, new Vector3(0f, 1.86f, 0f), new Vector3(0.74f, 0.03f, 0.05f), chrome, 1f, false);
            BuildKit.Box("Cable", machine, new Vector3(0.1f, 1.7f, 0f), new Vector3(0.008f, 0.3f, 0.008f), black, 1f, false);
            for (int k = 0; k < 3; k++)
            {
                var finger = BuildKit.Box("Claw", machine, new Vector3(0.1f, 1.52f, 0f), new Vector3(0.015f, 0.12f, 0.015f), chrome, 1f, false);
                finger.transform.localRotation = Quaternion.Euler(0f, k * 120f, 25f);
            }
            // Header with the lit name.
            BuildKit.Box("Header", machine, new Vector3(0f, 2.02f, 0f), new Vector3(0.82f, 0.3f, 0.82f), body);
            Texture2D title = BuildKit.SignTexture("title_prize_claw");
            if (title != null)
            {
                BuildKit.Quad("HeaderSign", machine, new Vector3(0f, 2.02f, 0.415f), new Vector2(0.78f, 0.195f), 0f, BuildKit.Picture("sign:" + title.name, title, 2f));
            }
            var glow = new GameObject("CaseLight").AddComponent<Light>();
            glow.transform.SetParent(machine, false);
            glow.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            glow.type = LightType.Point;
            glow.range = 1.6f;
            glow.intensity = 1.2f;
            glow.color = new Color(1f, 0.55f, 0.85f);
        }

        private static void Cake(Transform root, Vector3 top)
        {
            BuildKit.Cylinder("Cake", root, top + Vector3.up * 0.1f, 0.4f, 0.2f, BuildKit.Plain("cake", new Color(0.9f, 0.75f, 0.8f), 0.3f));
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 72f * Mathf.Deg2Rad;
                Vector3 candle = top + new Vector3(Mathf.Cos(angle) * 0.12f, 0.27f, Mathf.Sin(angle) * 0.12f);
                BuildKit.Cylinder("Candle", root, candle, 0.02f, 0.12f, BuildKit.Plain("candle", new Color(0.3f, 0.5f, 0.9f)));
            }
        }

        /// <summary>A wrapped present: patterned paper, a ribbon both ways and a bow on top.</summary>
        private static void Gift(Transform root, Vector3 floor, Vector3 size, int index, float yaw)
        {
            var gift = new GameObject("Gift").transform;
            gift.SetParent(root, false);
            gift.localPosition = floor;
            gift.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Texture2D paper = BuildKit.SignTexture("wrap_" + index % 3);
            Material wrap = paper != null ? BuildKit.Picture("sign:" + paper.name, paper) : BuildKit.Plain("gift" + index, Party[index % Party.Length], 0.4f);
            Color[] ribbons = { new Color(0.95f, 0.85f, 0.2f), new Color(0.9f, 0.15f, 0.2f), new Color(0.2f, 0.4f, 0.9f) };
            Material ribbon = BuildKit.Plain("ribbon" + index % 3, ribbons[index % 3], 0.6f);
            BuildKit.Box("Box", gift, new Vector3(0f, size.y * 0.5f, 0f), size, wrap, 0.25f, true);
            BuildKit.Box("RibbonX", gift, new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x + 0.006f, size.y + 0.006f, 0.04f), ribbon, 1f, false);
            BuildKit.Box("RibbonZ", gift, new Vector3(0f, size.y * 0.5f, 0f), new Vector3(0.04f, size.y + 0.006f, size.z + 0.006f), ribbon, 1f, false);
            foreach (float side in new[] { -1f, 1f })
            {
                BuildKit.Sphere("Bow", gift, new Vector3(side * 0.05f, size.y + 0.03f, 0f), 1f, ribbon).transform.localScale = new Vector3(0.09f, 0.05f, 0.04f);
            }
            BuildKit.Sphere("Knot", gift, new Vector3(0f, size.y + 0.02f, 0f), 0.035f, ribbon);
        }

        public static void Endoskeleton(Transform root, Vector3 bench)
        {
            Material metal = BuildKit.Plain("endo", new Color(0.4f, 0.4f, 0.42f), 0.6f, 0.9f);
            BuildKit.Box("Torso", root, bench + new Vector3(0f, 0.12f, 0f), new Vector3(0.35f, 0.2f, 0.7f), metal, 1f, false);
            BuildKit.Sphere("Skull", root, bench + new Vector3(0f, 0.15f, 0.55f), 0.28f, metal);
            BuildKit.Sphere("Eye", root, bench + new Vector3(-0.07f, 0.24f, 0.65f), 0.07f, BuildKit.Glow("endoEye", new Color(0.9f, 0.9f, 1f), 0.8f));
            BuildKit.Sphere("Eye", root, bench + new Vector3(0.07f, 0.24f, 0.65f), 0.07f, BuildKit.Glow("endoEye", new Color(0.9f, 0.9f, 1f), 0.8f));
            for (int i = 0; i < 2; i++)
            {
                var arm = BuildKit.Cylinder("Arm", root, bench + new Vector3(i == 0 ? -0.3f : 0.3f, 0.06f, 0.05f), 0.05f, 0.6f, metal);
                arm.transform.localRotation = Quaternion.Euler(90f, 0f, i == 0 ? 15f : -15f);
            }
        }
    }

    /// <summary>Blinks its renderer on and off (the record light of the security cameras).</summary>
    public class Blinker : MonoBehaviour
    {
        private Renderer target;
        private float offset;

        private void Awake()
        {
            target = GetComponent<Renderer>();
            offset = Random.value * 2f;
        }

        private void Update()
        {
            if (target != null)
            {
                target.enabled = (Time.time + offset) % 1.6f < 0.8f;
            }
        }
    }
}
