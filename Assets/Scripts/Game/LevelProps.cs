using System.Collections.Generic;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The decoration of every room. Floor props are only placed where they do not block the lines
    /// Freddy walks (<see cref="PizzeriaLayout.WalkLines"/>), the doorways, the hiding spots or the fuse.
    /// </summary>
    public static class LevelProps
    {
        private static readonly Color[] PartyColors =
        {
            new Color(0.85f, 0.15f, 0.15f), new Color(0.15f, 0.4f, 0.85f), new Color(0.95f, 0.75f, 0.1f),
            new Color(0.2f, 0.7f, 0.3f), new Color(0.7f, 0.2f, 0.7f)
        };

        public static void Decorate(Room room, Transform root, System.Random random)
        {
            switch (room)
            {
                case Room.Office: Office(root, random); break;
                case Room.Parts: Parts(root, random); break;
                case Room.Restrooms: Restrooms(root, random); break;
                case Room.BackHall: BackHall(root, random); break;
                case Room.Storage: Storage(root, random); break;
                case Room.Kitchen: Kitchen(root, random); break;
                case Room.DiningHall: Dining(root, random); break;
                case Room.MainHall: MainHall(root, random); break;
                case Room.PartyRoom: PartyRoom(root, random); break;
                case Room.Arcade: Arcade(root, random); break;
                case Room.ExitLobby: Lobby(root, random); break;
                case Room.Banquet: Banquet(root, random); break;
                case Room.Veranda: Veranda(root, random); break;
            }
            foreach (Room exit in HuntMap.Exits)
            {
                ExitSign(room, exit, root);
            }
            WallDecor(room, root, random);
            CircusDressing.Add(room, root, random);
            CircusSet.Add(room, root, random);
        }

        /// <summary>A free spot on the floor of the room for tonight's toys and boxes: away from walls and props.</summary>
        public static bool OpenFloorPoint(Room room, out Vector3 floor)
        {
            Rect area = PizzeriaLayout.Area(room);
            for (int attempt = 0; attempt < 24; attempt++)
            {
                floor = new Vector3(Random.Range(area.xMin + 0.9f, area.xMax - 0.9f), 0f, Random.Range(area.yMin + 0.9f, area.yMax - 0.9f));
                if (!Physics.CheckSphere(floor + Vector3.up * 0.5f, 0.45f, ~0, QueryTriggerInteraction.Ignore))
                {
                    return true;
                }
            }
            floor = default;
            return false;
        }

        // ------------------------------------------------------------------ rooms

        private static void Office(Transform root, System.Random random)
        {
            Material wood = Wood();
            // The guard's desk in the corner by the west wall, under the camera monitors.
            PlaceNear(Room.Office, new Vector2(-21f, -10.25f), 0.6f, p =>
            {
                // The guard's metal desk along the wall, two old CRT monitors facing the room.
                bool scanned = BuildKit.Prop("metal_office_desk", root, new Vector3(p.x, 0f, p.y), 90f + DeskFront, 0.9f) != null;
                if (!scanned)
                {
                    BuildKit.Box("Desk", root, new Vector3(p.x, 0.4f, p.y), new Vector3(0.9f, 0.8f, 1.8f), wood);
                }
                float deskTop = scanned ? 0.71f : 0.8f;
                for (int i = 0; i < 2; i++)
                {
                    var spot = new Vector3(p.x - 0.1f, deskTop, p.y - 0.45f + i * 0.9f);
                    GameObject crt = Monitor(root, spot, 90f + (i == 0 ? 8f : -8f));
                    if (crt == null)
                    {
                        var monitor = BuildKit.Box("Monitor", root, spot + Vector3.up * 0.25f, new Vector3(0.45f, 0.45f, 0.55f), BuildKit.Plain("crt", new Color(0.18f, 0.17f, 0.15f), 0.3f));
                        BuildKit.Quad("Screen", monitor.transform, new Vector3(0.505f, 0.02f, 0f), new Vector2(0.75f, 0.8f), 90f, BuildKit.Glow("screenGreen", new Color(0.2f, 0.9f, 0.5f), 1.2f));
                    }
                }
                if (BuildKit.Prop("SchoolChair_01", root, new Vector3(p.x + 0.85f, 0f, p.y), 270f + ChairFront + 15f, 1f, false) == null)
                {
                    Chair(root, new Vector2(p.x + 0.8f, p.y), 270f);
                }
                // A Freddy plush at the end of the desk, looking at whoever sits there (see Plush).
                OfficePlushPose = new Pose(new Vector3(p.x + 0.05f, deskTop, p.y + 0.78f), Quaternion.Euler(0f, 90f + 25f, 0f));
                // The guard's seat, facing the monitors (see OfficeSeat).
                OfficeChairPose = new Pose(new Vector3(p.x + 0.85f, 0f, p.y), Quaternion.Euler(0f, 270f, 0f));
                BuildKit.Box("Papers", root, new Vector3(p.x + 0.1f, deskTop + 0.01f, p.y + 0.35f), new Vector3(0.3f, 0.02f, 0.4f), BuildKit.Plain("paper", new Color(0.85f, 0.83f, 0.75f)), 1f, false);
            });
            PlaceNear(Room.Office, new Vector2(-21.2f, -12.8f), 0.5f, p => FilingCabinet(root, new Vector3(p.x, 0f, p.y), 90f));
            Sign(root, new Vector3(-19f, 2.3f, -15.77f), 0f, "WATCH OFFICE", new Color(0.8f, 0.8f, 0.7f), new Color(0.12f, 0.15f, 0.25f), 1.8f);
        }

        private static void Parts(Transform root, System.Random random)
        {
            Material wood = Wood();
            // Workbenches with animatronic parts: an endoskeleton, rows of empty heads.
            // The scanned workbench is 0.83 m high; the boxes they replace (no model imported) 0.9 m.
            PlaceNear(Room.Parts, new Vector2(-12.9f, -8.9f), 0.6f, p =>
            {
                bool scanned = BuildKit.Prop("WoodenTable_03", root, new Vector3(p.x, 0f, p.y), 90f) != null;
                if (!scanned)
                {
                    BuildKit.Box("Workbench", root, new Vector3(p.x, 0.45f, p.y), new Vector3(0.9f, 0.9f, 1.6f), wood);
                }
                RepairJob(root, new Vector3(p.x, scanned ? 0.83f : 0.9f, p.y));
                BuildKit.Prop("metal_stool_01", root, new Vector3(p.x + 0.75f, 0f, p.y + 0.35f), 25f, 1f, false);
            });
            PlaceNear(Room.Parts, new Vector2(-11.25f, -13.3f), 0.6f, p =>
            {
                bool scanned = BuildKit.Prop("WoodenTable_03", root, new Vector3(p.x, 0f, p.y), 0f) != null;
                if (!scanned)
                {
                    BuildKit.Box("Workbench", root, new Vector3(p.x, 0.45f, p.y), new Vector3(1.8f, 0.9f, 0.7f), wood);
                }
                float top = scanned ? 0.83f : 0.9f;
                if (!HeadBench(root, p, top))
                {
                    float spacing = scanned ? 0.4f : 0.6f;
                    for (int i = 0; i < 3; i++)
                    {
                        SpareHead(root, new Vector3(p.x - spacing + i * spacing - (scanned ? 0.12f : 0f), top + 0.25f, p.y), (i - 1) * 20f, 0.6f);
                    }
                }
                if (scanned)
                {
                    BuildKit.Prop("bench_vice_01", root, new Vector3(p.x + 0.55f, top, p.y + 0.12f), 180f, 1f, false);
                }
            });
            for (int i = 0; i < 3; i++)
            {
                BuildKit.Box("HeadShelf", root, new Vector3(-13.65f, 1.6f + (i % 2) * 0.6f, -8.8f + i * 0.45f), new Vector3(0.35f, 0.04f, 0.6f), Metal(new Color(0.3f, 0.32f, 0.33f)), 1f, false);
            }
            // Workshop tools: a drill press, a rolling tool chest, a toolbox left on the floor.
            PlaceNear(Room.Parts, new Vector2(-9.5f, -13.5f), 0.25f, p => BuildKit.Prop("metal_toolbox", root, new Vector3(p.x, 0f, p.y), 20f), 1.5f);
            Sign(root, new Vector3(-11f, 2.4f, -13.77f), 0f, "PROP WORKSHOP", new Color(0.85f, 0.85f, 0.8f), new Color(0.2f, 0.22f, 0.2f), 2.6f);
        }

        private static void Restrooms(Transform root, System.Random random)
        {
            // The dressing rooms: see CircusDressing (mirrors, costumes, wig stands, a folding screen).
        }

        private static void BackHall(Transform root, System.Random random)
        {
            Directory(root, "map_back_hall", new Vector3(-17f, 1.65f, -0.2f), 180f);
            Sign(root, new Vector3(-21.77f, 2.6f, -1.5f), 90f, "STAFF ONLY", new Color(0.9f, 0.9f, 0.85f), new Color(0.15f, 0.15f, 0.18f), 1.8f);
        }

        private static void Storage(Transform root, System.Random random)
        {
            // Trunks, crates, straw and the cannon: see CircusDressing.
            Sign(root, new Vector3(-21.77f, 2.6f, 4.5f), 90f, "TRUNK STORE", new Color(0.9f, 0.9f, 0.85f), new Color(0.2f, 0.2f, 0.22f), 2.0f);
        }

        private static void Kitchen(Transform root, System.Random random)
        {
            // The concession stands: see CircusDressing.
            Sign(root, new Vector3(PizzeriaLayout.Area(Room.Kitchen).center.x, 2.7f, PizzeriaLayout.Area(Room.Kitchen).yMax - 0.23f), 180f, "POPCORN", new Color(1f, 0.85f, 0.4f), new Color(0.3f, 0.05f, 0.05f), 2.4f);
        }

        private static void Dining(Transform root, System.Random random)
        {
            Stage(root);
            Sign(root, new Vector3(7.5f, 3.6f, 2.23f), 0f, "STEP RIGHT UP!", new Color(1f, 0.85f, 0.2f), new Color(0.5f, 0.05f, 0.1f), 3.4f);
        }

        /// <summary>The show stage along the north wall of the dining hall, where Freddy sleeps.</summary>
        private static void Stage(Transform root)
        {
            Rect stage = PizzeriaLayout.StageArea;
            float top = PizzeriaLayout.StageHeight;
            Material planks = BuildKit.Surface("circusStage", () => ProceduralTextures.Stripes(new Color(0.46f, 0.05f, 0.06f), new Color(0.72f, 0.55f, 0.16f), 8), 0.3f);
            BuildKit.Box("Stage", root, new Vector3(stage.center.x, top * 0.5f, stage.center.y), new Vector3(stage.width, top, stage.height), planks, 1f);
            BuildKit.Box("Skirt", root, new Vector3(stage.center.x, top * 0.5f, stage.yMin - 0.03f), new Vector3(stage.width, top, 0.06f), BuildKit.Plain("skirt", new Color(0.35f, 0.05f, 0.08f)), 1f, false);
            // Folded curtains drawn to both sides (overlapping folds), open in the middle.
            Material curtain = BuildKit.Pbr("velvet", new Color(0.8f, 0.12f, 0.14f)) ?? BuildKit.Plain("circusRedVelvet", new Color(0.46f, 0.05f, 0.06f), 0.1f);
            float height = PizzeriaLayout.Height(Room.DiningHall);
            float curtainWidth = stage.width * 0.5f - 2.2f;
            foreach (float side in new[] { -1f, 1f })
            {
                CircusSet.Drape("Curtain", root, new Vector3(stage.center.x + side * (2.2f + curtainWidth * 0.5f), height - 0.1f, stage.yMax - 0.4f), 0f, curtainWidth, height - 0.1f, curtain, Mathf.RoundToInt(curtainWidth * 3.2f), 0.13f, side > 0f ? 11 : 12);
            }
            CircusSet.Drape("Valance", root, new Vector3(stage.center.x, height - 0.04f, stage.yMax - 0.28f), 0f, stage.width + 0.6f, 0.82f, curtain, 22, 0.08f, 13, 9);
            Sign(root, new Vector3(stage.center.x, height - 0.4f, stage.yMax - 0.47f), 180f, "SHOWTIME", new Color(1f, 0.85f, 0.25f), new Color(0.2f, 0.02f, 0.12f), 2.6f);
            BuildKit.Star("Star", root, new Vector3(stage.center.x + 2.6f, 2.9f, stage.yMax - 0.24f), 0.42f, 0.18f, 0.06f, BuildKit.Glow("star", new Color(1f, 0.8f, 0.2f), 1.2f));
            // Two theatre spots on the ceiling aimed at Freddy, with hard shadows.
            Vector3 freddy = PizzeriaLayout.FreddyStart;
            StageSpot(root, new Vector3(4.2f, height - 0.3f, 4.5f), freddy, new Color(1f, 0.75f, 0.45f));
            StageSpot(root, new Vector3(10.8f, height - 0.3f, 4.5f), freddy, new Color(1f, 0.55f, 0.35f));
        }

        private static void MainHall(Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(Room.MainHall);
            // A strip of carpet down the hall and a bench by the wall.
            BuildKit.Box("Runner", root, new Vector3(area.center.x, 0.005f, area.center.y), new Vector3(area.width - 1f, 0.01f, 1.2f),
                         BuildKit.Pbr("dirty_carpet", new Color(0.72f, 0.16f, 0.17f)) ?? BuildKit.Plain("circusRedVelvet", new Color(0.46f, 0.05f, 0.06f), 0.1f), 2f, false);
            PlaceNear(Room.MainHall, new Vector2(22.3f, -1.5f), 0.4f, p =>
            {
                if (BuildKit.Prop("painted_wooden_bench", root, new Vector3(p.x, 0f, p.y), 90f) == null)
                {
                    BuildKit.Box("Bench", root, new Vector3(p.x, 0.25f, p.y), new Vector3(0.5f, 0.5f, 1.6f), Wood());
                }
            }, 1.5f);
            Directory(root, "map_main_hall", new Vector3(9f, 1.65f, -0.2f), 180f);
            Sign(root, new Vector3(22.77f, 2.2f, -1.5f), 270f, "WELCOME", new Color(1f, 0.85f, 0.2f), new Color(0.5f, 0.05f, 0.1f), 2.0f);
        }

        private static void PartyRoom(Transform root, System.Random random)
        {
            for (int i = 0; i < 5; i++)
            {
                var position = new Vector3(4.6f + (float)random.NextDouble() * 4.8f, 2.6f + (float)random.NextDouble() * 0.5f, -13.4f + (float)random.NextDouble() * 5.8f);
                Balloon(root, position, PartyColors[i % PartyColors.Length]);
            }
            Sign(root, new Vector3(7f, 2.4f, -13.77f), 0f, "BIRTHDAY TENT", new Color(1f, 0.85f, 0.2f), new Color(0.5f, 0.05f, 0.1f), 2.6f);
        }

        private static void Arcade(Transform root, System.Random random)
        {
            // The booths of the midway: see CircusDressing.
            Sign(root, new Vector3(22.77f, 2.3f, 5.4f), 270f, "PRIZES", new Color(0.2f, 1f, 1f), new Color(0.1f, 0.02f, 0.15f), 1.8f);
        }

        /// <summary>The banquet room off the kitchen: long rows of windows and laid tables, some of them to hide under.</summary>
        private static void Banquet(Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(Room.Banquet);
            // The real windows of the north wall (PizzeriaLayout.Windows); the west wall has the inside of the building behind it.
            RealWindows(root, Room.Banquet);
            foreach (Vector2 table in new[] { new Vector2(-8.5f, 20f), new Vector2(-3.5f, 19f), new Vector2(1.5f, 20.2f), new Vector2(5.5f, 19.6f), new Vector2(-12f, 18f) })
            {
                PlaceNear(Room.Banquet, table, 0.8f, p => CabaretTable(root, random, p), 2.5f);
            }
            Sign(root, new Vector3(-11f, 2.6f, area.yMin + 0.23f), 0f, "RINGSIDE LOUNGE", new Color(1f, 0.85f, 0.5f), new Color(0.3f, 0.1f, 0.05f), 2.8f);
        }

        /// <summary>The veranda above the arcade: windows onto the back lot, a table with its chairs pushed in.</summary>
        private static void Veranda(Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(Room.Veranda);
            RealWindows(root, Room.Veranda);
            // The keycard's pedestal stands against the north wall (see KeycardPedestal): keep it clear.
            Occupied.Add((new Vector2(PizzeriaLayout.CardPedestal.x, PizzeriaLayout.CardPedestal.z), 0.7f));
            PlaceNear(Room.Veranda, new Vector2(14.2f, 16.5f), 0.8f, p => CabaretTable(root, random, p), 1.5f);
            Sign(root, new Vector3(14.6f, 2.6f, area.yMin + 0.23f), 0f, "WINTER GARDEN", new Color(0.6f, 1f, 0.75f), new Color(0.05f, 0.2f, 0.12f), 2.2f);
        }

        private const float WindowInset = PizzeriaLayout.WallThickness + 0.02f;
        // One RunningWater animates the rain material every window shares.
        private static bool rainRunning;

        /// <summary>A new level is being built.</summary>
        public static void BeginLevel() => rainRunning = false;

        /// <summary>The frames, glass and curtains of a room's real windows (the holes are cut by LevelBuilder).</summary>
        private static void RealWindows(Transform root, Room room)
        {
            Rect area = PizzeriaLayout.Area(room);
            foreach ((Room r, float x, float width, float sill, float top) in PizzeriaLayout.Windows)
            {
                if (r == room)
                {
                    Window(root, new Vector3(x, 0f, area.yMax - WindowInset), 180f, width, top - sill);
                }
            }
        }

        /// <summary>
        /// A tall window set into a wall, looking out on the night: a frame with a cross, cold glass
        /// that glows faintly, a sill and a curtain on each side. <paramref name="at"/> is on the wall
        /// (y ignored), <paramref name="facing"/> the direction the window looks into the room.
        /// </summary>
        private static void Window(Transform root, Vector3 at, float facing, float width, float height)
        {
            Quaternion turn = Quaternion.Euler(0f, facing, 0f);
            Vector3 P(float x, float y, float depth) => new Vector3(at.x, 0f, at.z) + turn * new Vector3(x, y, depth);
            float bottom = 0.9f;
            float middle = bottom + height * 0.5f;
            Material frame = BuildKit.Plain("windowFrame", new Color(0.1f, 0.07f, 0.05f), 0.35f);
            Material cloth = BuildKit.Plain("windowCurtain", new Color(0.36f, 0.06f, 0.1f), 0.1f);
            // Real glass in a real hole in the wall (see PizzeriaLayout.Windows): faintly tinted,
            // glossy enough to catch the room's lights, the back lot behind it. Rain runs down it.
            Material glass = BuildKit.Decal("windowGlass", Texture2D.whiteTexture, new Color(0.55f, 0.62f, 0.7f, 0.12f));
            glass.SetFloat("_Smoothness", 0.96f);
            BuildKit.Quad("WindowGlass", root, P(0f, middle, -0.1f), new Vector2(width, height), facing, glass);
            Texture2D rain = BuildKit.SignTexture("glass_rain");
            if (rain != null)
            {
                Material water = BuildKit.Decal("glassRain", rain, new Color(0.85f, 0.9f, 1f, 0.75f));
                water.SetFloat("_Smoothness", 0.98f);
                GameObject wet = BuildKit.Quad("RainOnGlass", root, P(0f, middle, -0.095f), new Vector2(width, height), facing, water);
                if (!rainRunning)
                {
                    rainRunning = true;
                    wet.AddComponent<RunningWater>().Set(water);
                }
            }
            BuildKit.Box("GlassMullionV", root, P(0f, middle, -0.1f), new Vector3(0.04f, height, 0.05f), frame, 1f, false, facing);
            BuildKit.Box("GlassMullionH", root, P(0f, middle, -0.1f), new Vector3(width, 0.04f, 0.05f), frame, 1f, false, facing);
            BuildKit.Box("FrameTop", root, P(0f, bottom + height, 0.02f), new Vector3(width + 0.12f, 0.08f, 0.09f), frame, 1f, false, facing);
            BuildKit.Box("FrameBottom", root, P(0f, bottom, 0.02f), new Vector3(width + 0.12f, 0.08f, 0.09f), frame, 1f, false, facing);
            BuildKit.Box("FrameLeft", root, P(-width * 0.5f, middle, 0.02f), new Vector3(0.08f, height, 0.09f), frame, 1f, false, facing);
            BuildKit.Box("FrameRight", root, P(width * 0.5f, middle, 0.02f), new Vector3(0.08f, height, 0.09f), frame, 1f, false, facing);
            BuildKit.Box("Sill", root, P(0f, bottom - 0.06f, 0.1f), new Vector3(width + 0.3f, 0.06f, 0.24f), frame, 1f, false, facing);
            foreach (float side in new[] { -1f, 1f })
            {
                BuildKit.Box("Curtain", root, P(side * (width * 0.5f + 0.2f), bottom + height * 0.5f + 0.15f, 0.1f), new Vector3(0.4f, height + 0.5f, 0.12f), cloth, 1f, false, facing);
            }
        }

        private static void Lobby(Transform root, System.Random random)
        {
            PlaceNear(Room.ExitLobby, new Vector2(13.2f, -9.9f), 0.9f, p =>
            {
                TicketBooth(root, new Vector3(p.x, 0f, p.y));
            });
            PlaceNear(Room.ExitLobby, new Vector2(19.5f, -13.5f), 0.6f, p =>
            {
                if (BuildKit.Prop("painted_wooden_bench", root, new Vector3(p.x, 0f, p.y), 270f) == null)
                {
                    BuildKit.Box("Bench", root, new Vector3(p.x, 0.25f, p.y), new Vector3(0.5f, 0.5f, 1.8f), Wood());
                }
            });
        }

        /// <summary>EXIT signs built for each exit; the director switches on tonight's.</summary>
        public static readonly List<(Room exit, GameObject sign)> ExitSigns = new List<(Room, GameObject)>();

        /// <summary>
        /// A green EXIT sign with its own light beside the door that leads one room closer to
        /// <paramref name="exit"/>, so the escape route reads from anywhere in the building.
        /// </summary>
        private static void ExitSign(Room room, Room exit, Transform root)
        {
            if (room == exit)
            {
                return;
            }
            Room next = HuntMap.NextStep(room, exit);
            Vector3 door = PizzeriaLayout.DoorOf(room, next);
            Vector3 into = -PizzeriaLayout.Outward(room, door);
            float facing = Mathf.Atan2(into.x, into.z) * Mathf.Rad2Deg;
            // Beside the door at eye level, on the side toward the middle of the wall.
            Vector3 along = Vector3.Cross(Vector3.up, into);
            Vector2 middle = PizzeriaLayout.Area(room).center;
            if (Vector3.Dot(along, new Vector3(middle.x - door.x, 0f, middle.y - door.z)) < 0f)
            {
                along = -along;
            }
            Vector3 position = door + into * (PizzeriaLayout.WallThickness + 0.05f) + along * (PizzeriaLayout.DoorWidth * 0.5f + 0.7f) + Vector3.up * 2.2f;
            // The arrow points at the door: seen from the room, is the door to the sign's left or right?
            Vector3 viewerRight = Vector3.Cross(Vector3.up, -into);
            bool doorOnRight = Vector3.Dot(-along, viewerRight) > 0f;
            ExitSigns.Add((exit, ExitLamp(root, position, facing, doorOnRight)));
        }

        /// <summary>A glowing EXIT box sign facing <paramref name="facing"/>, with a green light under it.</summary>
        public static GameObject ExitLamp(Transform root, Vector3 position, float facing, bool arrowRight = true)
        {
            Texture2D drawn = BuildKit.SignTexture(arrowRight ? "exit_right" : "exit_left");
            GameObject sign;
            if (drawn != null)
            {
                // A lit box sign: the green face glows on a dark housing.
                sign = BuildKit.WallSign(root, drawn, position, facing, 0.95f, 2.2f);
            }
            else
            {
                Texture2D texture = ProceduralTextures.Sign("EXIT", new Color(0.55f, 1f, 0.6f), new Color(0.02f, 0.25f, 0.08f));
                sign = BuildKit.Quad("ExitSign", root, position, new Vector2(0.9f, 0.9f * texture.height / texture.width), facing, BuildKit.Picture("exitGreen", texture, 3f));
            }
            var light = new GameObject("ExitLight").AddComponent<Light>();
            light.transform.SetParent(sign.transform, false);
            light.transform.localPosition = new Vector3(0f, -0.3f, -0.5f);
            light.type = LightType.Point;
            light.range = 4f;
            light.intensity = 0.8f;
            light.color = new Color(0.3f, 1f, 0.45f);
            return sign;
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>A small round table on one leg with a little lamp and two wooden chairs: the lounge by the ring.</summary>
        private static void CabaretTable(Transform root, System.Random random, Vector2 center)
        {
            Material wood = Wood();
            Material brass = BuildKit.Plain("circusBrass", new Color(0.82f, 0.6f, 0.16f), 0.55f, 0.8f);
            if (BuildKit.Prop("round_wooden_table_01", root, new Vector3(center.x, 0f, center.y), (float)random.NextDouble() * 360f, 0.74f) != null)
            {
                BuildKit.Cylinder("LampBase", root, new Vector3(center.x, 0.82f, center.y), 0.035f, 0.14f, brass);
                BuildKit.Cone("LampShade", root, new Vector3(center.x, 0.88f, center.y), 0.09f, 0.12f, BuildKit.Plain("circusShade", new Color(0.45f, 0.06f, 0.07f), 0.2f));
                foreach (float side in new[] { -1f, 1f })
                {
                    float jitter = (float)random.NextDouble() * 40f - 20f;
                    // One of them has been knocked over.
                    GameObject chair = BuildKit.Prop("painted_wooden_chair_01", root, new Vector3(center.x + side * 0.82f, 0f, center.y), (side > 0f ? 270f : 90f) + jitter, 1f, false, new Color(0.46f, 0.42f, 0.37f));
                    if (chair != null && side > 0f && random.NextDouble() < 0.3)
                    {
                        chair.transform.localPosition += new Vector3(0.2f, 0.19f, 0f);
                        chair.transform.localRotation = Quaternion.Euler(0f, 0f, -82f) * chair.transform.localRotation;
                    }
                }
                return;
            }
            BuildKit.Cylinder("TableFoot", root, new Vector3(center.x, 0.02f, center.y), 0.5f, 0.04f, brass);
            BuildKit.Cylinder("TableLeg", root, new Vector3(center.x, 0.36f, center.y), 0.07f, 0.7f, brass);
            BuildKit.Cylinder("TableTop", root, new Vector3(center.x, 0.73f, center.y), 0.85f, 0.04f, wood);
            BuildKit.Cylinder("LampBase", root, new Vector3(center.x, 0.82f, center.y), 0.035f, 0.14f, brass);
            BuildKit.Cone("LampShade", root, new Vector3(center.x, 0.88f, center.y), 0.09f, 0.12f, BuildKit.Plain("circusShade", new Color(0.45f, 0.06f, 0.07f), 0.2f));
            foreach (float side in new[] { -1f, 1f })
            {
                float jitter = (float)random.NextDouble() * 24f - 12f;
                Chair(root, new Vector2(center.x + side * 0.72f, center.y), side > 0f ? 270f + jitter : 90f + jitter);
            }
        }

        /// <summary>Party hats and plates on a table top.</summary>
        public static void PartySetting(Transform parent, Vector3 top)
        {
            // Paper plates with a printed rim, a paper cup each, party hats with a pompom.
            Texture2D plate = BuildKit.SignTexture("decal_paper_plate");
            Material cupRed = BuildKit.Plain("cupRed", new Color(0.75f, 0.1f, 0.12f), 0.35f);
            Material cupRim = BuildKit.Plain("cupRim", new Color(0.92f, 0.9f, 0.86f), 0.3f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 position = top + new Vector3(-0.6f + i * 0.6f, 0f, (i % 2) * 0.2f - 0.1f);
                if (plate != null)
                {
                    BuildKit.Quad("Plate", parent, position + Vector3.up * 0.004f, new Vector2(0.26f, 0.26f), 0f, BuildKit.Decal("paperPlate", plate, Color.white))
                        .transform.localRotation = Quaternion.Euler(90f, i * 40f, 0f);
                }
                else
                {
                    BuildKit.Cylinder("Plate", parent, position + Vector3.up * 0.01f, 0.25f, 0.01f, BuildKit.Plain("plate", new Color(0.9f, 0.9f, 0.85f), 0.6f));
                }
                Vector3 cup = position + new Vector3(-0.18f, 0.055f, -0.12f);
                BuildKit.Cylinder("Cup", parent, cup, 0.07f, 0.11f, cupRed);
                BuildKit.Cylinder("CupRim", parent, cup + Vector3.up * 0.05f, 0.074f, 0.012f, cupRim);
                Vector3 hat = position + new Vector3(0.12f, 0.01f, 0.1f);
                BuildKit.Cone("PartyHat", parent, hat, 0.07f, 0.22f, BuildKit.Plain("hat" + i, PartyColors[i % PartyColors.Length], 0.3f))
                    .transform.localRotation = Quaternion.Euler(0f, 0f, i == 1 ? 70f : 0f);
                if (i != 1)
                {
                    BuildKit.Sphere("Pompom", parent, hat + Vector3.up * 0.225f, 0.05f, BuildKit.Plain("pompom" + i, PartyColors[(i + 2) % PartyColors.Length], 0.1f));
                }
            }
        }

        /// <summary>Which way each scanned model faces by default (degrees to add so it faces +z).</summary>
        internal const float ChairFront = 0f;
        internal const float DeskFront = 0f;
        internal const float ScreenFront = 0f;

        private static void Chair(Transform root, Vector2 position, float yaw)
        {
            var chair = new GameObject("Chair").transform;
            chair.SetParent(root, false);
            chair.localPosition = new Vector3(position.x, 0f, position.y);
            chair.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Material plastic = Wood();
            BuildKit.Box("Seat", chair, new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.05f, 0.42f), plastic, 1f, false);
            BuildKit.Box("Back", chair, new Vector3(0f, 0.72f, -0.19f), new Vector3(0.42f, 0.5f, 0.04f), plastic, 1f, false);
            Material legs = Metal(new Color(0.25f, 0.25f, 0.27f));
            for (int i = 0; i < 4; i++)
            {
                BuildKit.Box("Leg", chair, new Vector3(i % 2 == 0 ? -0.18f : 0.18f, 0.22f, i < 2 ? -0.18f : 0.18f), new Vector3(0.03f, 0.45f, 0.03f), legs, 1f, false);
            }
        }

        private static void Balloon(Transform root, Vector3 position, Color color)
        {
            // Shiny latex, a knot, a string that hangs in a curve; it drifts a little.
            var balloon = new GameObject("Balloon").transform;
            balloon.SetParent(root, false);
            balloon.localPosition = position;
            Material latex = BuildKit.Plain("balloon" + color.GetHashCode(), color * 0.9f, 0.88f);
            BuildKit.Sphere("Body", balloon, Vector3.zero, 1f, latex).transform.localScale = new Vector3(0.32f, 0.39f, 0.32f);
            BuildKit.Cone("Knot", balloon, new Vector3(0f, -0.215f, 0f), 0.025f, 0.035f, latex).transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            Material twine = BuildKit.Plain("string", new Color(0.8f, 0.8f, 0.8f));
            BuildKit.Box("String", balloon, new Vector3(0.02f, -0.5f, 0f), new Vector3(0.004f, 0.56f, 0.004f), twine, 1f, false).transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            BuildKit.Box("String", balloon, new Vector3(0.02f, -1.02f, 0f), new Vector3(0.004f, 0.5f, 0.004f), twine, 1f, false).transform.localRotation = Quaternion.Euler(0f, 0f, -5f);
            balloon.gameObject.AddComponent<Drift>();
        }

        private static void StageSpot(Transform root, Vector3 position, Vector3 target, Color color)
        {
            BuildKit.Cylinder("SpotHousing", root, position, 0.3f, 0.45f, BuildKit.Plain("spotHousing", new Color(0.08f, 0.08f, 0.08f), 0.5f, 0.7f))
                .transform.localRotation = Quaternion.LookRotation(target - position) * Quaternion.Euler(90f, 0f, 0f);
            var spot = new GameObject("StageSpot").AddComponent<Light>();
            spot.transform.SetParent(root, false);
            spot.transform.localPosition = position;
            spot.transform.localRotation = Quaternion.LookRotation(target - position);
            spot.type = LightType.Spot;
            spot.spotAngle = 32f;
            spot.innerSpotAngle = 18f;
            spot.range = 14f;
            spot.intensity = 9f;
            spot.color = color;
            spot.shadows = LightShadows.Soft;
        }

        /// <summary>A spare animatronic head: the kind of thing you should not find in a storage room.</summary>
        /// <summary>A bare endoskeleton lying on a bench.</summary>
        // Where the picture tube is on the front of the scanned CRT (television_02), as fractions
        // of the set's width and height measured from its lower left corner, seen from the front.
        public static Vector2 TvScreenCenter = new Vector2(0.5f, 0.6f);
        public static Vector2 TvScreenSize = new Vector2(0.62f, 0.5f);
        // Just in front of the tube: its glass bulges out past the bezel and would hide a picture behind it.
        private const float TvScreenOffset = 0.002f;

        /// <summary>
        /// An old CRT monitor standing at <paramref name="position"/>, its screen toward
        /// <paramref name="yaw"/>, showing one of the security cameras. Null without the model.
        /// </summary>
        public static GameObject Monitor(Transform root, Vector3 position, float yaw)
        {
            GameObject tv = TvSet(root, position, yaw, out GameObject screen);
            if (screen != null)
            {
                SecurityFeeds.RegisterScreen(screen.GetComponent<Renderer>());
            }
            return tv;
        }

        /// <summary>
        /// An old television set facing <paramref name="yaw"/>, and a quad laid on its glass to show
        /// a picture (null, and no screen, if the model is missing).
        /// </summary>
        public static GameObject TvSet(Transform root, Vector3 position, float yaw, out GameObject screen)
        {
            screen = null;
            GameObject tv = BuildKit.Prop("television_02", root, position, yaw + ScreenFront, 1f, false);
            if (tv == null)
            {
                return null;
            }
            var frame = new GameObject("MonitorFrame").transform;
            frame.SetParent(root, false);
            frame.localPosition = position;
            frame.localRotation = Quaternion.Euler(0f, yaw, 0f);
            // The set's real box in the frame (its pivot is not in the middle of the model).
            var box = new Bounds();
            bool first = true;
            foreach (MeshFilter filter in tv.GetComponentsInChildren<MeshFilter>())
            {
                Bounds mesh = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3((corner & 1) == 0 ? mesh.min.x : mesh.max.x, (corner & 2) == 0 ? mesh.min.y : mesh.max.y, (corner & 4) == 0 ? mesh.min.z : mesh.max.z);
                    Vector3 point = frame.InverseTransformPoint(filter.transform.TransformPoint(local));
                    if (first)
                    {
                        box = new Bounds(point, Vector3.zero);
                        first = false;
                    }
                    box.Encapsulate(point);
                }
            }
            // Seen from the front (looking down -z), the set's left edge is at +x.
            var center = new Vector3(box.max.x - TvScreenCenter.x * box.size.x, box.min.y + TvScreenCenter.y * box.size.y, box.max.z + TvScreenOffset);
            var size = new Vector2(TvScreenSize.x * box.size.x, TvScreenSize.y * box.size.y);
            // Better: the glass itself, measured on the model when it was imported.
            GameAssets assets = GameAssets.Load();
            if (assets != null && assets.TvScreen.size.sqrMagnitude > 0f)
            {
                Bounds glass = assets.TvScreen;
                var onFrame = new Bounds();
                for (int corner = 0; corner < 8; corner++)
                {
                    var local = new Vector3((corner & 1) == 0 ? glass.min.x : glass.max.x, (corner & 2) == 0 ? glass.min.y : glass.max.y, (corner & 4) == 0 ? glass.min.z : glass.max.z);
                    Vector3 point = frame.InverseTransformPoint(tv.transform.TransformPoint(local));
                    if (corner == 0)
                    {
                        onFrame = new Bounds(point, Vector3.zero);
                    }
                    onFrame.Encapsulate(point);
                }
                // A hair inside the bezel, a hair in front of the glass.
                center = new Vector3(onFrame.center.x, onFrame.center.y, onFrame.max.z + TvScreenOffset);
                size = new Vector2(onFrame.size.x * 0.97f, onFrame.size.y * 0.97f);
            }
            screen = BuildKit.Quad("Screen", frame, center, size, 0f, BuildKit.Glow("screenGreen", new Color(0.2f, 0.9f, 0.5f), 1.2f));
            return tv;
        }

        /// <summary>
        /// Three heads left on the workbench, each its own story: one on a test stand with its
        /// wires hanging out, one knocked on its side with its hat off and an eye that still
        /// flickers, one turned to face the wall with a DO NOT REACTIVATE tag. False without the model.
        /// </summary>
        private static bool HeadBench(Transform root, Vector2 p, float top)
        {
            const float scale = 0.55f;
            float half = 0.14f;
            Material steel = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Transform onStand = CircusDressing.ClownHead(root, new Vector3(p.x - 0.45f, top + 0.27f + half, p.y), scale);
            onStand.localRotation = Quaternion.Euler(-8f, 12f, 5f);
            BuildKit.Cylinder("StandBase", root, new Vector3(p.x - 0.45f, top + 0.01f, p.y), 0.16f, 0.02f, steel);
            BuildKit.Cylinder("StandPole", root, new Vector3(p.x - 0.45f, top + 0.14f, p.y), 0.025f, 0.26f, steel);
            NeckWires(onStand);

            CircusDressing.ClownHead(root, new Vector3(p.x - 0.08f, top + half * 0.95f, p.y - 0.02f), scale).localRotation = Quaternion.Euler(0f, 35f, 82f);

            Transform turned = CircusDressing.ClownHead(root, new Vector3(p.x + 0.3f, top + half, p.y - 0.05f), scale);
            turned.localRotation = Quaternion.Euler(0f, 195f, -4f);
            NeckWires(turned);
            Texture2D tag = BuildKit.SignTexture("decal_tag_reactivate");
            if (tag != null)
            {
                BuildKit.Quad("RepairTag", root, new Vector3(p.x + 0.33f, top + 0.08f, p.y + 0.14f), new Vector2(0.13f, 0.065f), 0f, BuildKit.Decal("repairTag", tag, Color.white))
                    .transform.localRotation *= Quaternion.Euler(0f, 0f, -12f);
            }
            return true;
        }

        /// <summary>Wires hanging out of a spare head's neck (in the head's own space, before its scale).</summary>
        public static void NeckWires(Transform head)
        {
            Color[] colors = { new Color(0.6f, 0.05f, 0.05f), new Color(0.05f, 0.05f, 0.05f), new Color(0.75f, 0.6f, 0.05f), new Color(0.1f, 0.25f, 0.6f) };
            for (int i = 0; i < colors.Length; i++)
            {
                Material insulation = BuildKit.Plain("wire" + i, colors[i], 0.45f);
                var start = new Vector3(-0.07f + i * 0.045f, -0.23f, -0.02f + (i % 2) * 0.05f);
                var upper = BuildKit.Cylinder("Wire", head, start + new Vector3(0f, -0.07f, 0f), 0.018f, 0.14f, insulation);
                upper.transform.localRotation = Quaternion.Euler((i - 1.5f) * 9f, 0f, (i % 2 == 0 ? 1f : -1f) * 12f);
                var lower = BuildKit.Cylinder("Wire", head, start + new Vector3((i % 2 == 0 ? 0.03f : -0.03f), -0.19f, (i - 1.5f) * 0.02f), 0.018f, 0.12f, insulation);
                lower.transform.localRotation = Quaternion.Euler((i - 1.5f) * -14f, 0f, (i % 2 == 0 ? -1f : 1f) * 18f);
                BuildKit.Cylinder("WireEnd", head, start + new Vector3((i % 2 == 0 ? 0.045f : -0.045f), -0.255f, (i - 1.5f) * 0.03f), 0.014f, 0.02f, BuildKit.Plain("copper", new Color(0.72f, 0.45f, 0.2f), 0.7f, 1f));
            }
        }

        /// <summary>
        /// A head being repaired on the bench: lying on its side, the open toolbox next to it,
        /// screws and a screwdriver. The boxes of the old endoskeleton are the fallback.
        /// </summary>
        private static void RepairJob(Transform root, Vector3 top)
        {
            Transform repaired = CircusDressing.ClownHead(root, top + new Vector3(0.05f, 0.16f, -0.25f), 0.65f);
            repaired.localRotation = Quaternion.Euler(0f, 70f, 80f);
            NeckWires(repaired);
            BuildKit.Prop("metal_toolbox", root, top + new Vector3(0.05f, 0f, 0.35f), 15f, 0.8f, false);
            Material steel = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            var shaft = BuildKit.Cylinder("Screwdriver", root, top + new Vector3(0.25f, 0.01f, -0.05f), 0.008f, 0.16f, steel);
            shaft.transform.localRotation = Quaternion.Euler(90f, 30f, 0f);
            var grip = BuildKit.Cylinder("ScrewdriverGrip", root, top + new Vector3(0.21f, 0.013f, -0.12f), 0.026f, 0.1f, BuildKit.Plain("gripYellow", new Color(0.8f, 0.6f, 0.05f), 0.5f));
            grip.transform.localRotation = Quaternion.Euler(90f, 30f, 0f);
            for (int i = 0; i < 5; i++)
            {
                BuildKit.Cylinder("Screw", root, top + new Vector3(-0.15f + i * 0.045f, 0.004f, 0.08f + (i % 2) * 0.03f), 0.012f, 0.008f, steel);
            }
        }

        public static void SpareHead(Transform root, Vector3 position, float yaw, float scale = 1f)
        {
            CircusDressing.ClownHead(root, position, scale * 1.1f).localRotation = Quaternion.Euler(0f, yaw, 15f);
        }

        /// <summary>Where the tape box sits: on the ticket booth's counter, its keypad toward the entrance.</summary>
        /// <summary>Where the plush sits on the office desk.</summary>
        public static Pose OfficePlushPose { get; private set; } = new Pose(new Vector3(-20.95f, 0.71f, -9.55f), Quaternion.Euler(0f, 115f, 0f));

        /// <summary>Where the guard's chair stands in the office, facing the desk.</summary>
        public static Pose OfficeChairPose { get; private set; } = new Pose(new Vector3(-20f, 0f, -10.25f), Quaternion.Euler(0f, 270f, 0f));

        public static Pose TapeBoxPose { get; private set; } = new Pose(new Vector3(13f, 1.145f, -10.6f), Quaternion.Euler(0f, 180f, 0f));

        /// <summary>The ticket booth by the entrance: striped counter, glass window, TICKETS on top.</summary>
        private static void TicketBooth(Transform root, Vector3 position)
        {
            var booth = new GameObject("TicketBooth").transform;
            booth.SetParent(root, false);
            booth.localPosition = position;
            booth.localRotation = Quaternion.Euler(0f, 180f, 0f);   // the window looks at the entrance
            Material stripes = BuildKit.Surface("circusBooth", () => ProceduralTextures.Stripes(new Color(0.46f, 0.05f, 0.06f), new Color(0.62f, 0.54f, 0.38f), 6), 0.1f);
            Material wood = Wood();
            Material brass = BuildKit.Plain("brass", new Color(0.62f, 0.48f, 0.24f), 0.75f, 1f);
            Material glass = BuildKit.Decal("boothGlass", Texture2D.whiteTexture, new Color(0.55f, 0.65f, 0.7f, 0.15f));
            BuildKit.Box("Counter", booth, new Vector3(0f, 0.55f, 0f), new Vector3(2.0f, 1.1f, 1.0f), stripes);
            BuildKit.Box("CounterTop", booth, new Vector3(0f, 1.12f, 0.03f), new Vector3(2.1f, 0.05f, 1.1f), wood);
            // The window stands back from the counter's edge: the front of the counter is the
            // customers' side, where the tape box waits on night 5.
            foreach (float x in new[] { -0.98f, 0.98f })
            {
                BuildKit.Box("Post", booth, new Vector3(x, 1.6f, -0.08f), new Vector3(0.06f, 0.9f, 0.06f), brass, 1f, false);
            }
            BuildKit.Quad("Window", booth, new Vector3(0f, 1.6f, -0.08f), new Vector2(1.9f, 0.9f), 0f, glass);
            BuildKit.Box("WindowSlot", booth, new Vector3(0f, 1.18f, -0.06f), new Vector3(0.4f, 0.06f, 0.04f), brass, 1f, false);
            TapeBoxPose = new Pose(booth.TransformPoint(new Vector3(-0.45f, 1.145f, 0.3f)), booth.rotation);
            BuildKit.Box("Roof", booth, new Vector3(0f, 2.08f, 0.05f), new Vector3(2.2f, 0.08f, 1.2f), wood, 1f, false);
            BuildKit.Cylinder("TicketRoll", booth, new Vector3(0.6f, 1.2f, 0.3f), 0.16f, 0.12f, BuildKit.Plain("ticketRed", new Color(0.75f, 0.12f, 0.1f), 0.3f))
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            BuildKit.Sphere("Bell", booth, new Vector3(-0.5f, 1.17f, 0.35f), 0.08f, brass).transform.localScale = new Vector3(0.09f, 0.05f, 0.09f);
            Texture2D title = BuildKit.SignTexture("title_tickets");
            if (title != null)
            {
                BuildKit.WallSign(booth, title, new Vector3(0f, 2.35f, 0.5f), 0f, 1.6f, 1.6f);
            }
        }

        /// <summary>A four-drawer steel filing cabinet; its front looks toward <paramref name="yaw"/>.</summary>
        private static void FilingCabinet(Transform root, Vector3 position, float yaw)
        {
            var cabinet = new GameObject("FilingCabinet").transform;
            cabinet.SetParent(root, false);
            cabinet.localPosition = position;
            cabinet.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Material steel = BuildKit.Pbr("metal_worn", new Color(0.62f, 0.68f, 0.64f)) ?? Metal(new Color(0.35f, 0.38f, 0.36f));
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Material card = BuildKit.Plain("labelCard", new Color(0.88f, 0.86f, 0.78f), 0.1f);
            BuildKit.Box("Body", cabinet, new Vector3(0f, 0.7f, -0.02f), new Vector3(0.5f, 1.4f, 0.66f), steel);
            for (int d = 0; d < 4; d++)
            {
                float y = 0.2f + d * 0.335f;
                // The third drawer is pulled out a little: someone left in a hurry.
                float open = d == 2 ? 0.12f : 0f;
                BuildKit.Box("Drawer", cabinet, new Vector3(0f, y, 0.32f + open), new Vector3(0.46f, 0.31f, 0.03f), steel, 1f, false);
                BuildKit.Box("Handle", cabinet, new Vector3(0f, y + 0.02f, 0.345f + open), new Vector3(0.14f, 0.025f, 0.025f), chrome, 1f, false);
                BuildKit.Box("LabelHolder", cabinet, new Vector3(0f, y + 0.09f, 0.337f + open), new Vector3(0.09f, 0.04f, 0.004f), card, 1f, false);
                if (open > 0f)
                {
                    BuildKit.Box("DrawerSide", cabinet, new Vector3(0f, y - 0.05f, 0.25f), new Vector3(0.44f, 0.2f, 0.14f), steel, 1f, false);
                    BuildKit.Box("Folders", cabinet, new Vector3(0f, y + 0.02f, 0.25f), new Vector3(0.4f, 0.22f, 0.12f), BuildKit.Plain("folders", new Color(0.8f, 0.68f, 0.42f), 0.1f), 1f, false);
                }
            }
        }

        // ------------------------------------------------------------------ walls

        /// <summary>Posters, crayon drawings and a clock along the walls, away from doors and spots.</summary>
        private static void WallDecor(Room room, Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(room);
            bool hall = area.height < 4f;
            const float inset = PizzeriaLayout.WallThickness + 0.02f;
            int index = 0;
            foreach ((Vector2 point, float facing) in WallPoints(area, hall ? 1.6f : 2.6f))
            {
                Vector2 into = new Vector2(Mathf.Sin(facing * Mathf.Deg2Rad), Mathf.Cos(facing * Mathf.Deg2Rad));
                Vector2 spotOnWall = point + into * inset;
                if (!ClearOfOpenings(room, spotOnWall, 1.4f) || random.NextDouble() < 0.35)
                {
                    continue;
                }
                // The north wall of the north wing is windows: no posters on it.
                if ((room == Room.Banquet || room == Room.Veranda) && Mathf.Abs(Mathf.DeltaAngle(facing, 180f)) < 1f)
                {
                    continue;
                }
                var position = new Vector3(spotOnWall.x, 0f, spotOnWall.y);
                if ((hall || room == Room.Office) && index % 4 == 3)
                {
                    position.y = 1.5f + (float)random.NextDouble() * 0.5f;
                    // A child's crayon drawing taped to the wall (the pixel drawings are the fallback).
                    Texture2D crayon = BuildKit.SignTexture("drawing_" + index % 6);
                    Material drawing = crayon != null ? BuildKit.Picture("sign:" + crayon.name, crayon)
                                                      : BuildKit.Picture("drawing" + index % 4, ProceduralTextures.Drawing(index % 4));
                    Vector2 size = crayon != null ? new Vector2(0.6f, 0.45f) : new Vector2(0.55f, 0.55f);
                    BuildKit.Quad("Drawing", root, position, size, facing, drawing)
                        .transform.localRotation *= Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 10f - 5f);
                }
                else if (room == Room.Storage && index % 2 == 1)
                {
                    index++;
                    continue;
                }
                else
                {
                    position.y = 1.95f;
                    string[] titles = { "POCKETS", "SMILE", "POPCORN", "FUN" };
                    Color[] backs = { new Color(0.75f, 0.45f, 0.15f), new Color(0.2f, 0.35f, 0.6f), new Color(0.55f, 0.15f, 0.2f), new Color(0.2f, 0.5f, 0.3f) };
                    int poster = index % 4;
                    Texture2D drawnPoster = BuildKit.SignTexture("poster_" + index % 8);
                    if (drawnPoster != null)
                    {
                        Vector3 off = Quaternion.Euler(0f, facing, 0f) * Vector3.forward * 0.01f;
                        BuildKit.Quad("Poster", root, position + off, new Vector2(1.14f, 1.52f), facing, BuildKit.Picture("sign:" + drawnPoster.name, drawnPoster, 0.08f))
                            .transform.localRotation *= Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 4f - 2f);
                    }
                    else
                    {
                        BuildKit.Quad("Poster", root, position, new Vector2(0.9f, 1.25f), facing,
                                      BuildKit.Picture("poster" + poster, ProceduralTextures.Poster(backs[poster], new Color(0.45f, 0.26f, 0.12f), titles[poster])));
                    }
                }
                index++;
            }
        }

        /// <summary>Points along the inside of the four walls, with the direction that faces into the room.</summary>
        private static IEnumerable<(Vector2 point, float facing)> WallPoints(Rect area, float spacing)
        {
            for (float x = area.xMin + 1.2f; x <= area.xMax - 1.2f; x += spacing)
            {
                yield return (new Vector2(x, area.yMax), 180f);
                yield return (new Vector2(x, area.yMin), 0f);
            }
            for (float z = area.yMin + 1.2f; z <= area.yMax - 1.2f; z += spacing)
            {
                yield return (new Vector2(area.xMin, z), 90f);
                yield return (new Vector2(area.xMax, z), 270f);
            }
        }

        /// <summary>
        /// A "YOU ARE HERE" board with the plan of the building and both exits, lit by a small
        /// lamp above it so the player can always read where they are.
        /// </summary>
        private static void Directory(Transform root, string name, Vector3 position, float facing)
        {
            Texture2D map = BuildKit.SignTexture(name);
            if (map == null)
            {
                return;
            }
            Vector3 outward = Quaternion.Euler(0f, facing, 0f) * Vector3.forward;
            BuildKit.WallSign(root, map, position + outward * 0.03f, facing, 1.4f, 0.35f);
            var lamp = new GameObject("DirectoryLamp").AddComponent<Light>();
            lamp.transform.SetParent(root, false);
            lamp.transform.localPosition = position + outward * 0.45f + Vector3.up * 0.75f;
            lamp.transform.localRotation = Quaternion.LookRotation(position - lamp.transform.localPosition);
            lamp.type = LightType.Spot;
            lamp.spotAngle = 80f;
            lamp.range = 3f;
            lamp.intensity = 2.5f;
            lamp.color = new Color(1f, 0.88f, 0.7f);
            BuildKit.Box("DirectoryLampHousing", root, lamp.transform.localPosition, new Vector3(0.5f, 0.06f, 0.12f),
                         BuildKit.Plain("lampHousingDark", new Color(0.12f, 0.12f, 0.12f), 0.5f, 0.6f), 1f, false);
        }

        /// <summary>The drawn wall sign for each text (tools/generate_signs.py), and whether it lights up.</summary>
        private static readonly Dictionary<string, (string texture, float glow)> DrawnSigns = new Dictionary<string, (string, float)>
        {
            { "SHOWTIME", ("title_showtime", 0.45f) },
            { "STEP RIGHT UP!", ("title_step_right_up", 0.45f) },
            { "WELCOME", ("title_welcome", 0.45f) },
            { "BIRTHDAY TENT", ("title_birthday_tent", 0.45f) },
            { "PRIZES", ("title_prizes", 0.45f) },
            { "TICKETS", ("title_tickets", 0.45f) },
            { "RINGSIDE LOUNGE", ("title_ringside_lounge", 0.45f) },
            { "WINTER GARDEN", ("title_winter_garden", 0.45f) },
            { "HAPPY BIRTHDAY", ("title_happy_birthday", 0.45f) },
            { "WATCH OFFICE", ("title_watch_office", 0.3f) },
            { "PROP WORKSHOP", ("title_prop_workshop", 0.3f) },
            { "TRUNK STORE", ("title_trunk_store", 0.3f) },
            { "STAFF ONLY", ("title_staff_only", 0.3f) },
            { "OUT OF ORDER", ("title_out_of_order", 0.35f) },
        };

        public static void Sign(Transform root, Vector3 position, float facing, string text, Color foreground, Color background, float width)
        {
            if (DrawnSigns.TryGetValue(text, out (string texture, float glow) drawn))
            {
                Texture2D picture = BuildKit.SignTexture(drawn.texture);
                if (picture != null)
                {
                    // Off the wall by the backing's thickness.
                    Vector3 off = Quaternion.Euler(0f, facing, 0f) * Vector3.forward * 0.03f;
                    BuildKit.WallSign(root, picture, position + off, facing, width, drawn.glow);
                    return;
                }
            }
            Texture2D texture = ProceduralTextures.Sign(text, foreground, background);
            float height = width * texture.height / texture.width;
            BuildKit.Quad("Sign", root, position, new Vector2(width, height), facing, BuildKit.Picture("sign" + text, texture, 0.8f));
        }

        // ------------------------------------------------------------------ placement rules

        /// <summary>Props already placed in the room being built (so they do not overlap).</summary>
        private static readonly List<(Vector2 center, float radius)> Occupied = new List<(Vector2, float)>();

        /// <summary>Starts a new room: forgets the props of the previous one.</summary>
        public static void BeginRoom() => Occupied.Clear();

        public static void Place(Room room, Vector2 center, float radius, System.Action build)
        {
            if (Clear(room, center, radius))
            {
                Occupied.Add((center, radius));
                build();
            }
            else if (radius >= 0.6f && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-capture") >= 0)
            {
                Debug.Log("CAPTURE prop skipped in " + room + " at " + center + " (radius " + radius + ")");
            }
        }

        /// <summary>
        /// Places a prop at <paramref name="desired"/>, or at the nearest free point within
        /// <paramref name="maxShift"/> meters (rings around it), and builds it there. Rooms are cut by
        /// Freddy's walking lines, so big furniture needs to find its gap.
        /// </summary>
        public static bool PlaceNear(Room room, Vector2 desired, float radius, System.Action<Vector2> build, float maxShift = 3.5f)
        {
            Rect area = PizzeriaLayout.Area(room);
            for (float ring = 0f; ring <= maxShift; ring += 0.35f)
            {
                int steps = ring < 0.01f ? 1 : Mathf.Max(8, Mathf.RoundToInt(ring * 6f));
                for (int i = 0; i < steps; i++)
                {
                    float angle = i * Mathf.PI * 2f / steps;
                    var point = desired + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ring;
                    float margin = radius * 0.7f + PizzeriaLayout.WallThickness;
                    if (point.x < area.xMin + margin || point.x > area.xMax - margin || point.y < area.yMin + margin || point.y > area.yMax - margin)
                    {
                        continue;
                    }
                    if (Clear(room, point, radius))
                    {
                        Occupied.Add((point, radius));
                        build(point);
                        return true;
                    }
                }
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-capture") >= 0)
            {
                Debug.Log("CAPTURE prop has no room in " + room + " near " + desired + " (radius " + radius + ")");
            }
            return false;
        }

        /// <summary>True if a prop of this radius at <paramref name="center"/> leaves Freddy's lines, doors, spots and other props free.</summary>
        private static bool Clear(Room room, Vector2 center, float radius)
        {
            foreach ((Vector2 other, float otherRadius) in Occupied)
            {
                if (Vector2.Distance(center, other) < radius + otherRadius)
                {
                    return false;
                }
            }
            foreach ((Vector2 a, Vector2 b) in PizzeriaLayout.WalkLines(room))
            {
                if (DistanceToSegment(center, a, b) < radius + 0.6f)
                {
                    return false;
                }
            }
            return ClearOfOpenings(room, center, radius + 0.4f);
        }

        public static bool ClearOfOpenings(Room room, Vector2 point, float distance)
        {
            foreach (Vector2 door in PizzeriaLayout.DoorwaysOf(room))
            {
                if (Vector2.Distance(point, door) < distance + PizzeriaLayout.DoorWidth * 0.5f)
                {
                    return false;
                }
            }
            for (int spot = 0; spot < HuntMap.SpotCount(room); spot++)
            {
                Vector3 position = PizzeriaLayout.SpotPosition(room, spot);
                if (Vector2.Distance(point, new Vector2(position.x, position.z)) < distance + 0.6f)
                {
                    return false;
                }
            }
            if (HuntMap.HasSocket(room))
            {
                Vector3 mount = PizzeriaLayout.SocketPosition(room);
                if (Vector2.Distance(point, new Vector2(mount.x, mount.z)) < distance + 0.6f)
                {
                    return false;
                }
            }
            if (PizzeriaLayout.HasFuseShelf(room))
            {
                Vector3 fuse = PizzeriaLayout.FusePosition(room);
                if (Vector2.Distance(point, new Vector2(fuse.x, fuse.z)) < distance + 0.4f)
                {
                    return false;
                }
            }
            foreach (Room exit in HuntMap.Exits)
            {
                Vector3 panel = PizzeriaLayout.ExitPanelOf(exit);
                if (room == exit && Vector2.Distance(point, new Vector2(panel.x, panel.z)) < distance + 0.8f)
                {
                    return false;
                }
            }
            return true;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------ materials

        private static Material Wood() => BuildKit.Surface("propWood", () => ProceduralTextures.Planks(new Color(0.35f, 0.22f, 0.13f)), 0.3f);
        private static Material Metal(Color color) => BuildKit.Pbr("metal_worn", color * 2.2f)
                                                       ?? BuildKit.Surface("metal" + color.GetHashCode(), () => ProceduralTextures.Grimy(color, 1f), 0.45f, 0.6f);
        private static Material Cardboard() => BuildKit.Surface("cardboard", () => ProceduralTextures.Grimy(new Color(0.55f, 0.42f, 0.25f), 0.2f), 0.1f);
    }

    /// <summary>A balloon drifting in the air the vents move: a slow sway and bob.</summary>
    public class Drift : MonoBehaviour
    {
        private Vector3 home;
        private float phase;

        private void Start()
        {
            home = transform.localPosition;
            phase = Random.value * 10f;
        }

        private void Update()
        {
            float t = Time.time * 0.4f + phase;
            transform.localPosition = home + new Vector3(Mathf.Sin(t) * 0.04f, Mathf.Sin(t * 1.3f) * 0.03f, Mathf.Cos(t * 0.8f) * 0.04f);
            transform.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 4f, t * 6f, Mathf.Cos(t) * 4f);
        }
    }

    /// <summary>Water running down the windows: the rain layer's texture slides down slowly, unevenly.</summary>
    public class RunningWater : MonoBehaviour
    {
        private Material water;
        private float offset;

        public void Set(Material material) => water = material;

        private void Update()
        {
            if (water == null)
            {
                return;
            }
            // Drops run in spurts, not at a constant speed.
            offset += Time.deltaTime * (0.035f + 0.025f * Mathf.PerlinNoise(Time.time * 0.6f, 0.3f));
            water.SetTextureOffset("_BaseMap", new Vector2(0f, offset % 1f));
        }
    }
}
