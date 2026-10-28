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
            Sign(root, new Vector3(-19f, 2.3f, -15.77f), 0f, "SECURITY", new Color(0.8f, 0.8f, 0.7f), new Color(0.12f, 0.15f, 0.25f), 1.8f);
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
            PlaceNear(Room.Parts, new Vector2(-8.7f, -7.7f), 0.4f, p => BuildKit.Prop("old_drill_press", root, new Vector3(p.x, 0f, p.y), 225f), 1.5f);
            PlaceNear(Room.Parts, new Vector2(-13.4f, -12.2f), 0.4f, p => BuildKit.Prop("metal_tool_chest", root, new Vector3(p.x, 0f, p.y), 90f), 3f);
            PlaceNear(Room.Parts, new Vector2(-9.5f, -13.5f), 0.25f, p => BuildKit.Prop("metal_toolbox", root, new Vector3(p.x, 0f, p.y), 20f), 1.5f);
            Sign(root, new Vector3(-11f, 2.4f, -13.77f), 0f, "PARTS & SERVICE", new Color(0.85f, 0.85f, 0.8f), new Color(0.2f, 0.22f, 0.2f), 2.6f);
        }

        private static void Restrooms(Transform root, System.Random random)
        {
            Material porcelain = BuildKit.Plain("porcelain", new Color(0.85f, 0.86f, 0.84f), 0.7f);
            Material mirror = BuildKit.Plain("mirror", new Color(0.25f, 0.28f, 0.3f), 0.95f, 0.9f);
            // Sinks and mirrors along the east wall, a row of stall doors along the west.
            for (int i = 0; i < 2; i++)
            {
                float z = -8.4f - i * 1.3f;
                Place(Room.Restrooms, new Vector2(-2.5f, z), 0.3f, () => Washbasin(root, new Vector3(-2.2f, 0f, z), 270f, porcelain, mirror));
            }
            Material stall = BuildKit.Plain("stall", new Color(0.32f, 0.4f, 0.42f), 0.4f, 0.3f);
            for (int i = 0; i < 3; i++)
            {
                BuildKit.Box("StallWall", root, new Vector3(-6.4f, 1.05f, -11.4f + i * 1.0f), new Vector3(1.0f, 1.9f, 0.05f), stall, 1f, false);
            }
            // Cleaning things the janitor left behind.
            Place(Room.Restrooms, new Vector2(-5.9f, -12.2f), 0.2f, () => BuildKit.Prop("plunger", root, new Vector3(-5.9f, 0f, -12.2f), 30f, 1f, false));
            Place(Room.Restrooms, new Vector2(-2.6f, -12.9f), 0.2f, () => BuildKit.Prop("multi_cleaner_5_litre", root, new Vector3(-2.6f, 0f, -12.9f), 200f, 1f, false));
            Place(Room.Restrooms, new Vector2(-2.5f, -11.3f), 0.25f, () =>
            {
                GameObject broom = BuildKit.Prop("plastic_broom", root, new Vector3(-2.55f, 0f, -11.3f), 90f, 1f, false);
                if (broom != null)
                {
                    broom.transform.localRotation *= Quaternion.Euler(0f, 0f, -12f);   // leaning on the wall
                }
            });
            Sign(root, new Vector3(-2.23f, 2.3f, -10.5f), 270f, "OUT OF ORDER", new Color(0.9f, 0.9f, 0.85f), new Color(0.35f, 0.05f, 0.05f), 1.8f);
        }

        private static void BackHall(Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(Room.BackHall);
            // Garland of little flags along the top of the walls.
            for (float x = area.xMin + 0.5f; x < area.xMax - 0.5f; x += 0.6f)
            {
                Material flag = BuildKit.Plain("flag" + ((int)(x * 10f) % PartyColors.Length + 5) % PartyColors.Length, PartyColors[Mathf.Abs((int)(x * 1.7f)) % PartyColors.Length]);
                BuildKit.Box("Flag", root, new Vector3(x, 2.55f + Mathf.Sin(x * 2f) * 0.05f, area.yMax - 0.15f), new Vector3(0.25f, 0.25f, 0.01f), flag, 1f, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                BuildKit.Box("Flag", root, new Vector3(x, 2.55f + Mathf.Sin(x * 2f) * 0.05f, area.yMin + 0.15f), new Vector3(0.25f, 0.25f, 0.01f), flag, 1f, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            // A wet floor sign nobody picked up.
            Place(Room.BackHall, new Vector2(-13f, -0.6f), 0.3f, () =>
            {
                if (BuildKit.Prop("WetFloorSign_01", root, new Vector3(-13f, 0f, -0.6f), 30f, 1f, false) == null)
                {
                    var sign = BuildKit.Box("WetFloor", root, new Vector3(-13f, 0.3f, -0.6f), new Vector3(0.35f, 0.6f, 0.05f), BuildKit.Plain("yellow", new Color(0.9f, 0.75f, 0.1f), 0.5f), 1f, false);
                    sign.transform.localRotation = Quaternion.Euler(10f, 30f, 0f);
                }
            });
            Directory(root, "map_back_hall", new Vector3(-17f, 1.65f, -0.2f), 180f);
            Sign(root, new Vector3(-21.77f, 2.6f, -1.5f), 90f, "STAFF ONLY", new Color(0.9f, 0.9f, 0.85f), new Color(0.15f, 0.15f, 0.18f), 1.8f);
        }

        private static void Storage(Transform root, System.Random random)
        {
            // Wall shelves of boxes above the cabinet (the floor is taken by the loading door's way in).
            Material metal = Metal(new Color(0.3f, 0.32f, 0.33f));
            for (int level = 0; level < 2; level++)
            {
                float y = 1.8f + level * 0.6f;
                BuildKit.Box("WallShelf", root, new Vector3(-18.2f, y, 2.42f), new Vector3(2.6f, 0.04f, 0.4f), metal, 1f, false);
                for (int b = 0; b < 4; b++)
                {
                    if (random.NextDouble() < 0.3)
                    {
                        continue;
                    }
                    float w = 0.3f + (float)random.NextDouble() * 0.15f;
                    BuildKit.Box("Box", root, new Vector3(-19.2f + b * 0.65f, y + 0.02f + w * 0.5f, 2.45f), new Vector3(w, w, 0.32f), Cardboard(), 1f, false);
                }
            }
            PlaceNear(Room.Storage, new Vector2(-16.75f, 3.75f), 0.5f, p =>
            {
                if (BuildKit.Prop("cardboard_box_01", root, new Vector3(p.x, 0f, p.y), 15f) != null)
                {
                    BuildKit.Prop("cardboard_box_01", root, new Vector3(p.x + 0.05f, 0.34f, p.y - 0.02f), 40f);
                    BuildKit.Prop("plastic_crate_02", root, new Vector3(p.x - 0.1f, 0.68f, p.y), -10f, 1f, false);
                }
                else
                {
                    Crate(root, new Vector3(p.x, 0f, p.y), 15f);
                }
            });
            // The backup generator that never got connected.
            PlaceNear(Room.Storage, new Vector2(-21.1f, 3.2f), 0.4f, p => BuildKit.Prop("portable_generator", root, new Vector3(p.x, 0f, p.y), 180f), 3f);
            Sign(root, new Vector3(-21.77f, 2.6f, 4.5f), 90f, "STORAGE", new Color(0.9f, 0.9f, 0.85f), new Color(0.2f, 0.2f, 0.22f), 2.0f);
        }

        private static void Kitchen(Transform root, System.Random random)
        {
            Material steel = Metal(new Color(0.55f, 0.57f, 0.6f));
            PlaceNear(Room.Kitchen, new Vector2(-12.6f, 9.3f), 0.6f, p =>
            {
                BuildKit.Box("Hood", root, new Vector3(p.x, 2.3f, p.y - 0.05f), new Vector3(1.5f, 0.5f, 0.6f), steel, 1f, false);
                if (BuildKit.Prop("electric_stove", root, new Vector3(p.x - 0.35f, 0f, p.y), 180f + DeskFront) != null)
                {
                    BuildKit.Prop("electric_stove", root, new Vector3(p.x + 0.35f, 0f, p.y), 180f + DeskFront);
                    BuildKit.Prop("pot_enamel_01", root, new Vector3(p.x - 0.4f, 0.86f, p.y - 0.05f), 30f, 1.3f, false);
                }
                else
                {
                    BuildKit.Box("Stove", root, new Vector3(p.x, 0.45f, p.y), new Vector3(1.4f, 0.9f, 0.7f), BuildKit.Plain("stove", new Color(0.1f, 0.1f, 0.1f), 0.5f));
                    for (int i = 0; i < 2; i++)
                    {
                        BuildKit.Cylinder("Pan", root, new Vector3(p.x - 0.35f + i * 0.7f, 0.92f, p.y), 0.25f, 0.04f, BuildKit.Plain("pan", new Color(0.12f, 0.12f, 0.12f), 0.6f, 0.8f));
                    }
                }
            });
            // The fridge in the row along the north wall, next to the stoves (not out in the room).
            if (!PlaceNear(Room.Kitchen, new Vector2(-11.1f, 9.45f), 0.45f, p => Fridge(root, new Vector3(p.x, 0f, p.y), 180f), 0.5f))
            {
                PlaceNear(Room.Kitchen, new Vector2(-13.35f, 8.4f), 0.45f, p => Fridge(root, new Vector3(p.x, 0f, p.y), 90f), 0.8f);
            }
            PlaceNear(Room.Kitchen, new Vector2(-7.75f, 9.3f), 0.6f, p =>
            {
                Counter(root, new Vector3(p.x, 0f, p.y), 180f, steel);
                PizzaBoxes(root, new Vector3(p.x + 0.3f, 0.92f, p.y), 4);
            });
        }

        private static void Dining(Transform root, System.Random random)
        {
            foreach (Vector2 table in new[] { new Vector2(4.25f, 8.25f), new Vector2(8.25f, 3.4f) })
            {
                PlaceNear(Room.DiningHall, table, 1.1f, p => PartyTable(root, random, p), 2.0f);
            }
            Stage(root);
            // Balloons drifting under the ceiling, streamers across the room.
            for (int i = 0; i < 14; i++)
            {
                var position = new Vector3(2.8f + (float)random.NextDouble() * 9.4f, 3.3f + (float)random.NextDouble() * 0.7f, 2.8f + (float)random.NextDouble() * 6.5f);
                Balloon(root, position, PartyColors[i % PartyColors.Length]);
            }
            for (int i = 0; i < 3; i++)
            {
                Streamer(root, new Vector3(2.2f, 4.1f, 3.5f + i * 2.5f), new Vector3(12.8f, 4.1f, 3.5f + i * 2.5f), PartyColors[i % PartyColors.Length]);
            }
            Sign(root, new Vector3(7.5f, 3.6f, 2.23f), 0f, "LET'S PARTY!", new Color(1f, 0.85f, 0.2f), new Color(0.5f, 0.05f, 0.1f), 3.4f);
        }

        /// <summary>The show stage along the north wall of the dining hall, where Freddy sleeps.</summary>
        private static void Stage(Transform root)
        {
            Rect stage = PizzeriaLayout.StageArea;
            float top = PizzeriaLayout.StageHeight;
            Material planks = BuildKit.Surface("stagePlanks", () => ProceduralTextures.Planks(new Color(0.45f, 0.28f, 0.15f)), 0.35f);
            BuildKit.Box("Stage", root, new Vector3(stage.center.x, top * 0.5f, stage.center.y), new Vector3(stage.width, top, stage.height), planks, 1f);
            BuildKit.Box("Skirt", root, new Vector3(stage.center.x, top * 0.5f, stage.yMin - 0.03f), new Vector3(stage.width, top, 0.06f), BuildKit.Plain("skirt", new Color(0.35f, 0.05f, 0.08f)), 1f, false);
            // Folded curtains drawn to both sides (overlapping folds), open in the middle.
            Material curtain = BuildKit.Surface("curtain", () => ProceduralTextures.Grimy(new Color(0.42f, 0.05f, 0.2f)), 0.15f);
            float height = PizzeriaLayout.Height(Room.DiningHall);
            for (int i = 0; i < 16; i++)
            {
                float x = stage.xMin + 0.2f + i * 0.45f;
                if (x > stage.center.x - 2.2f && x < stage.center.x + 2.2f)
                {
                    continue;
                }
                BuildKit.Box("Curtain", root, new Vector3(x, (height - 0.1f) * 0.5f, stage.yMax - 0.35f - (i % 2) * 0.12f), new Vector3(0.55f, height - 0.1f, 0.18f), curtain, 1f, false);
            }
            BuildKit.Box("Valance", root, new Vector3(stage.center.x, height - 0.4f, stage.yMax - 0.3f), new Vector3(stage.width + 0.6f, 0.7f, 0.3f), curtain, 1f, false);
            Sign(root, new Vector3(stage.center.x, height - 0.4f, stage.yMax - 0.47f), 180f, "SHOWTIME", new Color(1f, 0.85f, 0.25f), new Color(0.2f, 0.02f, 0.12f), 2.6f);
            BuildKit.Star("Star", root, new Vector3(stage.center.x, 2.9f, stage.yMax - 0.24f), 0.42f, 0.18f, 0.06f, BuildKit.Glow("star", new Color(1f, 0.8f, 0.2f), 1.2f));
            // Two theatre spots on the ceiling aimed at Freddy, with hard shadows.
            Vector3 freddy = PizzeriaLayout.FreddyStart;
            StageSpot(root, new Vector3(4.2f, height - 0.3f, 4.5f), freddy, new Color(1f, 0.75f, 0.45f));
            StageSpot(root, new Vector3(10.8f, height - 0.3f, 4.5f), freddy, new Color(0.8f, 0.45f, 1f));
        }

        private static void MainHall(Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(Room.MainHall);
            // A strip of carpet down the hall and a bench by the wall.
            BuildKit.Box("Runner", root, new Vector3(area.center.x, 0.005f, area.center.y), new Vector3(area.width - 1f, 0.01f, 1.2f),
                         BuildKit.Surface("carpet", () => ProceduralTextures.Carpet(), 0.05f), 2f, false);
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
            for (int i = 0; i < 8; i++)
            {
                var position = new Vector3(4.6f + (float)random.NextDouble() * 4.8f, 2.6f + (float)random.NextDouble() * 0.5f, -13.4f + (float)random.NextDouble() * 5.8f);
                Balloon(root, position, PartyColors[i % PartyColors.Length]);
            }
            Streamer(root, new Vector3(4.2f, 3.1f, -10.5f), new Vector3(9.8f, 3.1f, -10.5f), PartyColors[2]);
            PlaceNear(Room.PartyRoom, new Vector2(9.3f, -8.9f), 0.3f, p => BuildKit.Prop("boombox", root, new Vector3(p.x, 0f, p.y), 200f, 1f, false), 1f);
            Sign(root, new Vector3(7f, 2.4f, -13.77f), 0f, "PARTY ROOM", new Color(1f, 0.85f, 0.2f), new Color(0.5f, 0.05f, 0.1f), 2.6f);
        }

        private static void Arcade(Transform root, System.Random random)
        {
            // The north wall has the door to the veranda now: one cabinet on each side of it, one
            // against the west wall past the dining hall door.
            (Vector2 at, float yaw)[] cabinets = { (new Vector2(15.9f, 9.4f), 180f), (new Vector2(20.2f, 9.4f), 180f), (new Vector2(15.6f, 8.2f), 90f) };
            for (int i = 0; i < cabinets.Length; i++)
            {
                int game = i;
                float yaw = cabinets[i].yaw;
                PlaceNear(Room.Arcade, cabinets[i].at, 0.5f, p => ArcadeCabinet.Build(root, p, yaw, game), 0.8f);
            }
            PlaceNear(Room.Arcade, new Vector2(21f, 2.7f), 0.5f, p => ArcadeCabinet.Build(root, p, 0f, 2), 1f);
            // Neon strips along the walls, the most colorful light in the building.
            NeonStrip(root, new Vector3(19f, 3.1f, 9.74f), new Vector3(7f, 0.06f, 0.06f), new Color(1f, 0.15f, 0.7f));
            NeonStrip(root, new Vector3(22.74f, 3.1f, 6f), new Vector3(0.06f, 0.06f, 7f), new Color(0.1f, 0.85f, 1f));
            Sign(root, new Vector3(22.77f, 2.3f, 5.4f), 270f, "PRIZES", new Color(0.2f, 1f, 1f), new Color(0.1f, 0.02f, 0.15f), 1.8f);
        }

        /// <summary>The banquet room off the kitchen: long rows of windows and laid tables, some of them to hide under.</summary>
        private static void Banquet(Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(Room.Banquet);
            // The real windows of the north wall (PizzeriaLayout.Windows); the west wall has the inside of the building behind it.
            RealWindows(root, Room.Banquet);
            foreach (Vector2 table in new[] { new Vector2(-8.5f, 20f), new Vector2(1.5f, 20.2f), new Vector2(5.5f, 19.6f) })
            {
                PlaceNear(Room.Banquet, table, 1.5f, p => PartyTable(root, random, p), 2.5f);
            }
            for (int i = 0; i < 6; i++)
            {
                var position = new Vector3(-14f + (float)random.NextDouble() * 22f, 2.6f + (float)random.NextDouble() * 0.5f, 15.5f + (float)random.NextDouble() * 6f);
                Balloon(root, position, PartyColors[i % PartyColors.Length]);
            }
            Streamer(root, new Vector3(-15.5f, 3.1f, 16.2f), new Vector3(8.5f, 3.1f, 16.2f), PartyColors[0]);
            Sign(root, new Vector3(-11f, 2.6f, area.yMin + 0.23f), 0f, "BANQUET ROOM", new Color(1f, 0.85f, 0.5f), new Color(0.3f, 0.1f, 0.05f), 2.8f);
        }

        /// <summary>The veranda above the arcade: windows onto the back lot, a table with its chairs pushed in.</summary>
        private static void Veranda(Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(Room.Veranda);
            RealWindows(root, Room.Veranda);
            // The keycard's pedestal stands against the north wall (see KeycardPedestal): keep it clear.
            Occupied.Add((new Vector2(PizzeriaLayout.CardPedestal.x, PizzeriaLayout.CardPedestal.z), 0.7f));
            PlaceNear(Room.Veranda, new Vector2(14.2f, 16.5f), 1.5f, p => PartyTable(root, random, p), 1.5f);
            Sign(root, new Vector3(14.6f, 2.6f, area.yMin + 0.23f), 0f, "VERANDA", new Color(0.6f, 1f, 0.75f), new Color(0.05f, 0.2f, 0.12f), 2.2f);
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

        private static void PartyTable(Transform root, System.Random random, Vector2 center)
        {
            // The scanned diner table with its checkered cloth, a little smaller than life.
            float side = 0.75f;
            float top = 0.78f;
            if (BuildKit.Prop("dining_table", root, new Vector3(center.x, 0f, center.y), 0f, 0.85f) != null)
            {
                side = 0.85f;
                top = 0.75f;
            }
            else
            {
                Material cloth = BuildKit.Surface("tablecloth", () => ProceduralTextures.Tiles(new Color(0.7f, 0.1f, 0.12f), new Color(0.88f, 0.85f, 0.78f), 6, true, 0.4f), 0.05f);
                BuildKit.Box("Table", root, new Vector3(center.x, 0.75f, center.y), new Vector3(2.0f, 0.06f, 0.9f), cloth);
                BuildKit.Box("Leg", root, new Vector3(center.x, 0.37f, center.y), new Vector3(0.15f, 0.74f, 0.15f), Metal(new Color(0.3f, 0.3f, 0.32f)));
            }
            PartySetting(root, new Vector3(center.x, top, center.y));
            for (int i = 0; i < 3; i++)
            {
                float jitter = (float)random.NextDouble() * 24f - 12f;
                Chair(root, new Vector2(center.x - 0.65f + i * 0.65f, center.y + side), 180f + jitter);
                Chair(root, new Vector2(center.x - 0.65f + i * 0.65f, center.y - side), jitter);
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
            if (BuildKit.Prop("plastic_monobloc_chair_01", root, new Vector3(position.x, 0f, position.y), yaw + ChairFront, 1f, false,
                              new Color(1f, 0.55f, 0.5f)) != null)
            {
                return;
            }
            var chair = new GameObject("Chair").transform;
            chair.SetParent(root, false);
            chair.localPosition = new Vector3(position.x, 0f, position.y);
            chair.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Material plastic = BuildKit.Plain("chairPlastic", new Color(0.55f, 0.1f, 0.1f), 0.4f);
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

        private static void Streamer(Transform root, Vector3 from, Vector3 to, Color color)
        {
            const int segments = 12;
            Material material = BuildKit.Plain("streamer" + color.GetHashCode(), color, 0.3f);
            for (int i = 0; i < segments; i++)
            {
                float t = (i + 0.5f) / segments;
                Vector3 position = Vector3.Lerp(from, to, t) - Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.4f;
                var piece = BuildKit.Box("Streamer", root, position, new Vector3((to - from).magnitude / segments, 0.02f, 0.12f), material, 1f, false);
                piece.transform.localRotation = Quaternion.Euler(i * 30f, 0f, Mathf.Cos(t * Mathf.PI) * 12f);
            }
        }

        private static void NeonStrip(Transform root, Vector3 position, Vector3 size, Color color)
        {
            BuildKit.Box("Neon", root, position, size, BuildKit.Glow("neon" + color.GetHashCode(), color, 4f), 1f, false);
            var light = new GameObject("NeonLight").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = position - (size.x > size.z ? Vector3.forward : Vector3.right) * 0.6f;
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 0.9f;
            light.color = color;
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

        private static void Crate(Transform root, Vector3 position, float yaw)
        {
            BuildKit.Box("Crate", root, position + Vector3.up * 0.35f, new Vector3(0.7f, 0.7f, 0.7f), Cardboard(), 1f, true, yaw);
            BuildKit.Box("Crate", root, position + new Vector3(0.1f, 0.9f, 0f), new Vector3(0.45f, 0.4f, 0.45f), Cardboard(), 1f, false, yaw + 20f);
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
            float half = FreddyParts.HeadHeight * scale * 0.5f;
            Material steel = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            GameObject onStand = FreddyParts.Head(root, new Vector3(p.x - 0.45f, top + 0.27f + half, p.y), Quaternion.Euler(-8f, 12f, 5f), scale);
            if (onStand == null)
            {
                return false;
            }
            BuildKit.Cylinder("StandBase", root, new Vector3(p.x - 0.45f, top + 0.01f, p.y), 0.16f, 0.02f, steel);
            BuildKit.Cylinder("StandPole", root, new Vector3(p.x - 0.45f, top + 0.14f, p.y), 0.025f, 0.26f, steel);
            NeckWires(onStand.transform);

            FreddyParts.Head(root, new Vector3(p.x - 0.08f, top + half * 0.95f, p.y - 0.02f), Quaternion.Euler(0f, 35f, 82f), scale, HeadStyle.NoHat | HeadStyle.LiveEye);
            FreddyParts.Hat(root, new Vector3(p.x - 0.02f, top + 0.05f, p.y + 0.22f), Quaternion.Euler(0f, 25f, 100f), scale);

            GameObject turned = FreddyParts.Head(root, new Vector3(p.x + 0.3f, top + half, p.y - 0.05f), Quaternion.Euler(0f, 195f, -4f), scale);
            NeckWires(turned.transform);
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
                var start = new Vector3(-0.07f + i * 0.045f, -FreddyParts.HeadHeight * 0.45f, -0.02f + (i % 2) * 0.05f);
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
            GameObject repaired = FreddyParts.Head(root, top + new Vector3(0.05f, 0.16f, -0.25f), Quaternion.Euler(0f, 70f, 80f), 0.55f, HeadStyle.NoHat);
            if (repaired != null)
            {
                NeckWires(repaired.transform);
            }
            else
            {
                LevelClutter.Endoskeleton(root, top + new Vector3(0f, 0f, -0.3f));
                return;
            }
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
            // Cut from Freddy's own model when it is there; the spheres are the fallback.
            if (FreddyParts.Head(root, position, Quaternion.Euler(0f, yaw, 15f), scale) != null)
            {
                return;
            }
            var head = new GameObject("SpareHead").transform;
            head.SetParent(root, false);
            head.localPosition = position;
            head.localRotation = Quaternion.Euler(0f, yaw, 15f);
            head.localScale = Vector3.one * scale;
            Material fur = BuildKit.Plain("fur", new Color(0.35f, 0.2f, 0.1f), 0.2f);
            BuildKit.Sphere("Head", head, Vector3.zero, 0.6f, fur);
            BuildKit.Sphere("Snout", head, new Vector3(0f, -0.1f, 0.25f), 0.3f, BuildKit.Plain("snout", new Color(0.55f, 0.38f, 0.22f), 0.2f));
            BuildKit.Sphere("EarL", head, new Vector3(-0.25f, 0.27f, 0f), 0.2f, fur);
            BuildKit.Sphere("EarR", head, new Vector3(0.25f, 0.27f, 0f), 0.2f, fur);
            BuildKit.Sphere("EyeL", head, new Vector3(-0.12f, 0.07f, 0.27f), 0.09f, BuildKit.Glow("deadEye", new Color(0.6f, 0.65f, 0.7f), 0.4f));
            BuildKit.Sphere("EyeR", head, new Vector3(0.12f, 0.07f, 0.27f), 0.09f, BuildKit.Glow("deadEye", new Color(0.6f, 0.65f, 0.7f), 0.4f));
            BuildKit.Cylinder("Hat", head, new Vector3(0f, 0.38f, 0f), 0.2f, 0.18f, BuildKit.Plain("black", Color.black));
        }

        /// <summary>
        /// An old enamel fridge (no CC0 model exists): body on a kick plate with a grille, freezer
        /// and fridge doors with chrome handles, a badge, notes held by magnets, and its hum.
        /// Its front looks toward <paramref name="yaw"/>.
        /// </summary>
        private static void Fridge(Transform root, Vector3 position, float yaw)
        {
            var fridge = new GameObject("Fridge").transform;
            fridge.SetParent(root, false);
            fridge.localPosition = position;
            fridge.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Material enamel = BuildKit.Pbr("metal_worn", new Color(1f, 0.97f, 0.88f)) ?? BuildKit.Plain("fridgeEnamel", new Color(0.8f, 0.78f, 0.7f), 0.4f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Material dark = BuildKit.Plain("fridgeDark", new Color(0.04f, 0.04f, 0.045f), 0.3f);
            Material paper = BuildKit.Plain("paper", new Color(0.85f, 0.83f, 0.75f));

            BuildKit.Box("Body", fridge, new Vector3(0f, 1.0f, -0.03f), new Vector3(0.75f, 1.84f, 0.7f), enamel);
            BuildKit.Box("Kick", fridge, new Vector3(0f, 0.04f, 0f), new Vector3(0.7f, 0.08f, 0.66f), dark, 1f, false);
            for (int slat = 0; slat < 4; slat++)
            {
                BuildKit.Box("Grille", fridge, new Vector3(0f, 0.025f + slat * 0.015f, 0.335f), new Vector3(0.6f, 0.006f, 0.01f), chrome, 1f, false);
            }
            // Doors stand proud of the body, a dark seam between them.
            BuildKit.Box("FreezerDoor", fridge, new Vector3(0f, 1.62f, 0.345f), new Vector3(0.74f, 0.54f, 0.05f), enamel, 1f, false);
            BuildKit.Box("FridgeDoor", fridge, new Vector3(0f, 0.75f, 0.345f), new Vector3(0.74f, 1.16f, 0.05f), enamel, 1f, false);
            BuildKit.Box("Seam", fridge, new Vector3(0f, 1.34f, 0.335f), new Vector3(0.74f, 0.02f, 0.04f), dark, 1f, false);
            foreach ((float low, float high) in new[] { (1.42f, 1.78f), (0.9f, 1.28f) })
            {
                float x = 0.3f;
                BuildKit.Box("Handle", fridge, new Vector3(x, (low + high) * 0.5f, 0.4f), new Vector3(0.025f, high - low, 0.025f), chrome, 1f, false);
                BuildKit.Box("HandleFoot", fridge, new Vector3(x, low, 0.383f), new Vector3(0.03f, 0.03f, 0.04f), chrome, 1f, false);
                BuildKit.Box("HandleFoot", fridge, new Vector3(x, high, 0.383f), new Vector3(0.03f, 0.03f, 0.04f), chrome, 1f, false);
            }
            BuildKit.Box("Badge", fridge, new Vector3(-0.2f, 1.82f, 0.372f), new Vector3(0.14f, 0.025f, 0.006f), chrome, 1f, false);
            // Notes under magnets: a schedule, a warning, a child's drawing.
            Color[] magnets = { new Color(0.8f, 0.1f, 0.1f), new Color(0.15f, 0.35f, 0.85f), new Color(0.95f, 0.8f, 0.15f) };
            Vector3[] notes = { new Vector3(-0.12f, 1.15f, 0f), new Vector3(0.1f, 0.95f, 0f), new Vector3(-0.15f, 0.7f, 0f) };
            for (int i = 0; i < notes.Length; i++)
            {
                var note = BuildKit.Box("Note", fridge, notes[i] + new Vector3(0f, 0f, 0.372f), new Vector3(0.15f, 0.2f, 0.002f), paper, 1f, false);
                note.transform.localRotation = Quaternion.Euler(0f, 0f, (i - 1) * 6f);
                BuildKit.Cylinder("Magnet", fridge, notes[i] + new Vector3(0f, 0.08f, 0.377f), 0.03f, 0.012f, BuildKit.Plain("magnet" + i, magnets[i], 0.5f))
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            var hum = fridge.gameObject.AddComponent<AudioSource>();
            hum.clip = GameAudio.LampHum();
            hum.loop = true;
            hum.spatialBlend = 1f;
            hum.rolloffMode = AudioRolloffMode.Linear;
            hum.minDistance = 0.8f;
            hum.maxDistance = 5f;
            hum.volume = 0.05f;
            hum.pitch = 0.45f;
            hum.Play();
        }

        /// <summary>A steel prep counter: drawers over two doors, a recessed kick, a backsplash.</summary>
        private static void Counter(Transform root, Vector3 position, float yaw, Material steel)
        {
            var counter = new GameObject("Counter").transform;
            counter.SetParent(root, false);
            counter.localPosition = position;
            counter.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Material top = BuildKit.Plain("stainless", new Color(0.62f, 0.64f, 0.66f), 0.55f, 0.9f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Material dark = BuildKit.Plain("fridgeDark", new Color(0.04f, 0.04f, 0.045f), 0.3f);

            BuildKit.Box("Cabinet", counter, new Vector3(0f, 0.49f, -0.02f), new Vector3(1.6f, 0.82f, 0.62f), steel);
            BuildKit.Box("Kick", counter, new Vector3(0f, 0.04f, -0.04f), new Vector3(1.56f, 0.08f, 0.56f), dark, 1f, false);
            BuildKit.Box("Top", counter, new Vector3(0f, 0.92f, 0f), new Vector3(1.66f, 0.04f, 0.72f), top, 1f, false);
            BuildKit.Box("Backsplash", counter, new Vector3(0f, 1.08f, -0.35f), new Vector3(1.66f, 0.28f, 0.02f), top, 1f, false);
            // Seams and handles: three drawers, two doors.
            BuildKit.Box("Seam", counter, new Vector3(0f, 0.7f, 0.292f), new Vector3(1.56f, 0.012f, 0.008f), dark, 1f, false);
            for (int i = 0; i < 2; i++)
            {
                BuildKit.Box("Seam", counter, new Vector3(-0.27f + i * 0.54f, 0.79f, 0.292f), new Vector3(0.012f, 0.17f, 0.008f), dark, 1f, false);
            }
            BuildKit.Box("Seam", counter, new Vector3(0f, 0.4f, 0.292f), new Vector3(0.012f, 0.58f, 0.008f), dark, 1f, false);
            for (int i = 0; i < 3; i++)
            {
                BuildKit.Box("DrawerHandle", counter, new Vector3(-0.54f + i * 0.54f, 0.8f, 0.31f), new Vector3(0.16f, 0.015f, 0.02f), chrome, 1f, false);
            }
            for (int i = 0; i < 2; i++)
            {
                BuildKit.Box("DoorHandle", counter, new Vector3(i == 0 ? -0.08f : 0.08f, 0.5f, 0.31f), new Vector3(0.015f, 0.16f, 0.02f), chrome, 1f, false);
            }
        }

        /// <summary>A wall-hung washbasin with its taps, the pipes under it and a mirror over it.</summary>
        private static void Washbasin(Transform root, Vector3 wall, float yaw, Material porcelain, Material mirror)
        {
            var basin = new GameObject("Washbasin").transform;
            basin.SetParent(root, false);
            basin.localPosition = wall;
            basin.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Material dark = BuildKit.Plain("drain", new Color(0.08f, 0.08f, 0.08f), 0.6f);
            BuildKit.Box("Basin", basin, new Vector3(0f, 0.82f, 0.27f), new Vector3(0.55f, 0.16f, 0.45f), porcelain);
            BuildKit.Box("Bowl", basin, new Vector3(0f, 0.905f, 0.29f), new Vector3(0.4f, 0.01f, 0.3f), BuildKit.Plain("bowl", new Color(0.62f, 0.64f, 0.62f), 0.8f), 1f, false);
            BuildKit.Cylinder("Drain", basin, new Vector3(0f, 0.912f, 0.29f), 0.04f, 0.002f, dark);
            BuildKit.Cylinder("Tap", basin, new Vector3(0f, 0.97f, 0.1f), 0.03f, 0.12f, chrome);
            BuildKit.Cylinder("Spout", basin, new Vector3(0f, 1.02f, 0.16f), 0.025f, 0.12f, chrome).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            foreach (float x in new[] { -0.12f, 0.12f })
            {
                BuildKit.Cylinder("Knob", basin, new Vector3(x, 0.95f, 0.1f), 0.05f, 0.03f, chrome);
            }
            BuildKit.Cylinder("Trap", basin, new Vector3(0f, 0.6f, 0.2f), 0.045f, 0.32f, chrome);
            BuildKit.Cylinder("TrapBend", basin, new Vector3(0f, 0.45f, 0.11f), 0.045f, 0.2f, chrome).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // The mirror: a steel frame, the glass a little dirty.
            BuildKit.Box("MirrorFrame", basin, new Vector3(0f, 1.6f, 0.012f), new Vector3(0.6f, 0.75f, 0.02f), chrome, 1f, false);
            BuildKit.Box("Mirror", basin, new Vector3(0f, 1.6f, 0.024f), new Vector3(0.55f, 0.7f, 0.006f), mirror, 1f, false);
            BuildKit.Box("Shelf", basin, new Vector3(0f, 1.2f, 0.06f), new Vector3(0.5f, 0.02f, 0.1f), porcelain, 1f, false);
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
            Material stripes = BuildKit.Surface("wainscotRed", () => ProceduralTextures.Tiles(new Color(0.6f, 0.08f, 0.08f), new Color(0.9f, 0.86f, 0.75f), 8, true, 0.3f), 0.3f);
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

        private static void PizzaBoxes(Transform root, Vector3 position, int count)
        {
            // A stack of take-away boxes with the printed lid on top of each.
            Texture2D print = BuildKit.SignTexture("pizza_box_top");
            for (int i = 0; i < count; i++)
            {
                var box = new GameObject("PizzaBox").transform;
                box.SetParent(root, false);
                box.localPosition = position + new Vector3(0f, 0.025f + i * 0.05f, 0f);
                box.localRotation = Quaternion.Euler(0f, i * 7f, 0f);
                BuildKit.Box("Body", box, Vector3.zero, new Vector3(0.45f, 0.045f, 0.45f), BuildKit.Plain("pizzaBoxSide", new Color(0.78f, 0.74f, 0.66f), 0.15f), 1f, false);
                if (print != null)
                {
                    BuildKit.Quad("Lid", box, new Vector3(0f, 0.0231f, 0f), new Vector2(0.44f, 0.44f), 0f, BuildKit.Picture("sign:" + print.name, print))
                        .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
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
                if (hall || room == Room.Office)
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
                else if (room == Room.Kitchen || room == Room.Storage || room == Room.Parts || room == Room.Restrooms)
                {
                    continue;
                }
                else
                {
                    position.y = 1.95f;
                    string[] titles = { "FREDDY", "SMILE", "PIZZA!", "FUN" };
                    Color[] backs = { new Color(0.75f, 0.45f, 0.15f), new Color(0.2f, 0.35f, 0.6f), new Color(0.55f, 0.15f, 0.2f), new Color(0.2f, 0.5f, 0.3f) };
                    int poster = index % 4;
                    Texture2D drawnPoster = BuildKit.SignTexture("poster_" + index % 5);
                    if (drawnPoster != null)
                    {
                        Vector3 off = Quaternion.Euler(0f, facing, 0f) * Vector3.forward * 0.01f;
                        BuildKit.Quad("Poster", root, position + off, new Vector2(0.84f, 1.12f), facing, BuildKit.Picture("sign:" + drawnPoster.name, drawnPoster, 0.08f))
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
            { "SHOWTIME", ("title_showtime", 1.6f) },
            { "LET'S PARTY!", ("title_lets_party", 1.6f) },
            { "WELCOME", ("title_welcome", 1.6f) },
            { "PARTY ROOM", ("title_party_room", 1.6f) },
            { "PRIZES", ("title_prizes", 1.6f) },
            { "TICKETS", ("title_tickets", 1.6f) },
            { "BANQUET ROOM", ("title_banquet_room", 1.6f) },
            { "VERANDA", ("title_veranda", 1.6f) },
            { "HAPPY BIRTHDAY", ("title_happy_birthday", 1.4f) },
            { "SECURITY", ("title_security", 0.3f) },
            { "PARTS & SERVICE", ("title_parts_service", 0.3f) },
            { "STORAGE", ("title_storage", 0.3f) },
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
            int socket = HuntMap.SocketIn(room);
            if (socket >= 0)
            {
                Vector3 mount = PizzeriaLayout.SocketPosition(socket);
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
