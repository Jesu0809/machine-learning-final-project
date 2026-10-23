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
                if (BuildKit.Prop("wall_clock", root, new Vector3(area.center.x + 1.2f, 2.5f, area.yMax - inset + 0.01f), 180f, 1.3f, false) == null)
                {
                    var clock = BuildKit.Cylinder("Clock", root, new Vector3(area.center.x + 1.2f, 2.5f, area.yMax - inset), 0.4f, 0.05f, BuildKit.Plain("clockFace", new Color(0.85f, 0.82f, 0.75f), 0.4f));
                    clock.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    BuildKit.Box("Hand", root, new Vector3(area.center.x + 1.2f, 2.55f, area.yMax - inset - 0.03f), new Vector3(0.02f, 0.14f, 0.01f), BuildKit.Plain("black", Color.black), 1f, false);
                    BuildKit.Box("Hand", root, new Vector3(area.center.x + 1.25f, 2.5f, area.yMax - inset - 0.03f), new Vector3(0.1f, 0.02f, 0.01f), BuildKit.Plain("black", Color.black), 1f, false);
                }
            }
            SecurityCamera(room, root, area);
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
            // A small red light that blinks: it is recording.
            var led = BuildKit.Box("RecordLed", camera.transform, new Vector3(0f, 0.08f, 0.1f), new Vector3(0.015f, 0.015f, 0.015f), BuildKit.Glow("recordLed", new Color(1f, 0.05f, 0.05f), 6f), 1f, false);
            led.AddComponent<Blinker>();
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
                BuildKit.Cylinder("Puddle", root, new Vector3(position.x, 0.003f, position.y), 1.1f + (float)random.NextDouble(), 0.002f, puddle).transform.localScale = new Vector3(1.6f, 0.001f, 1f);
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
                    // The camera monitors: a wall of screens with static, over the desk.
                    Material frame = BuildKit.Plain("crt", new Color(0.18f, 0.17f, 0.15f), 0.3f);
                    Material screen = BuildKit.Picture("cctv", ProceduralTextures.Static(64, 48), 0.9f);
                    for (int r = 0; r < 2; r++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            var position = new Vector3(-21.65f, 1.55f + r * 0.5f, -11.3f + c * 0.55f);
                            BuildKit.Box("Monitor", root, position, new Vector3(0.45f, 0.45f, 0.5f), frame, 1f, false);
                            BuildKit.Quad("Screen", root, position + Vector3.right * 0.23f, new Vector2(0.38f, 0.3f), 90f, screen);
                        }
                    }
                    BuildKit.Box("MonitorShelf", root, new Vector3(-21.65f, 1.3f, -10.75f), new Vector3(0.6f, 0.05f, 1.8f), frame, 1f, false);
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
                            BuildKit.Box("Gift", root, new Vector3(gift.x, 0.2f, gift.y), new Vector3(0.4f, 0.4f, 0.4f), BuildKit.Plain("gift" + i, Party[i % Party.Length], 0.4f), 1f, false, i * 17f));
                    }
                    LevelProps.Sign(root, new Vector3(4.23f, 2.5f, -11f), 90f, "HAPPY BIRTHDAY", new Color(0.3f, 0.85f, 1f), new Color(0.08f, 0.05f, 0.25f), 3.4f);
                    break;

                case Room.Storage:
                    // A row of spare masks watching the door.
                    for (int i = 0; i < 3; i++)
                    {
                        float z = 2.9f + i * 0.7f;
                        BuildKit.Box("MaskShelf", root, new Vector3(-16.4f, 1.85f, z), new Vector3(0.35f, 0.04f, 0.65f), BuildKit.Plain("pipe", new Color(0.32f, 0.3f, 0.27f), 0.5f, 0.6f), 1f, false);
                        LevelProps.SpareHead(root, new Vector3(-16.45f, 2.15f, z), 270f + (i - 1) * 15f);
                    }
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
                        BuildKit.Box("ClawBase", root, new Vector3(p.x, 0.5f, p.y), new Vector3(0.8f, 1f, 0.8f), BuildKit.Plain("clawBase", new Color(0.7f, 0.1f, 0.4f), 0.4f));
                        BuildKit.Box("ClawGlass", root, new Vector3(p.x, 1.45f, p.y), new Vector3(0.78f, 0.9f, 0.78f), BuildKit.Plain("glass", new Color(0.3f, 0.35f, 0.4f), 0.9f), 1f, false);
                        for (int i = 0; i < 4; i++)
                        {
                            LevelProps.SpareHead(root, new Vector3(p.x - 0.15f + (i % 2) * 0.3f, 1.12f, p.y - 0.15f + (i / 2) * 0.3f), i * 40f, 0.3f);
                        }
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
                    LevelProps.Place(room, new Vector2(19.4f, -16.4f), 0.5f, () =>
                    {
                        BuildKit.Box("LostAndFound", root, new Vector3(19.4f, 0.3f, -16.4f), new Vector3(0.7f, 0.6f, 0.5f), BuildKit.Surface("cardboard", () => ProceduralTextures.Grimy(new Color(0.55f, 0.42f, 0.25f), 0.2f), 0.1f));
                        LevelProps.SpareHead(root, new Vector3(19.4f, 0.75f, -16.4f), 200f, 0.35f);
                    });
                    break;
            }
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
