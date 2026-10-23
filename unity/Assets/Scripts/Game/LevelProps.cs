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
                    GameObject crt = BuildKit.Prop("television_02", root, spot, 90f + ScreenFront + (i == 0 ? 8f : -8f), 1f, false);
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
                BuildKit.Box("Papers", root, new Vector3(p.x + 0.1f, 0.81f, p.y + 0.6f), new Vector3(0.3f, 0.02f, 0.4f), BuildKit.Plain("paper", new Color(0.85f, 0.83f, 0.75f)), 1f, false);
            });
            PlaceNear(Room.Office, new Vector2(-21.2f, -12.8f), 0.5f, p =>
                BuildKit.Box("FilingCabinet", root, new Vector3(p.x, 0.7f, p.y), new Vector3(0.5f, 1.4f, 0.7f), Metal(new Color(0.35f, 0.38f, 0.36f))));
            Sign(root, new Vector3(-19f, 2.3f, -15.77f), 0f, "SECURITY", new Color(0.8f, 0.8f, 0.7f), new Color(0.12f, 0.15f, 0.25f), 1.8f);
        }

        private static void Parts(Transform root, System.Random random)
        {
            Material wood = Wood();
            // Workbenches with animatronic parts: an endoskeleton, rows of empty heads.
            PlaceNear(Room.Parts, new Vector2(-12.9f, -8.9f), 0.6f, p =>
            {
                BuildKit.Box("Workbench", root, new Vector3(p.x, 0.45f, p.y), new Vector3(0.9f, 0.9f, 1.6f), wood);
                Endoskeleton(root, new Vector3(p.x, 0.9f, p.y - 0.3f));
            });
            PlaceNear(Room.Parts, new Vector2(-11.25f, -13.3f), 0.6f, p =>
            {
                BuildKit.Box("Workbench", root, new Vector3(p.x, 0.45f, p.y), new Vector3(1.8f, 0.9f, 0.7f), wood);
                for (int i = 0; i < 3; i++)
                {
                    SpareHead(root, new Vector3(p.x - 0.6f + i * 0.6f, 1.15f, p.y), (i - 1) * 20f, 0.6f);
                }
            });
            for (int i = 0; i < 3; i++)
            {
                BuildKit.Box("HeadShelf", root, new Vector3(-13.65f, 1.6f + (i % 2) * 0.6f, -8.8f + i * 0.45f), new Vector3(0.35f, 0.04f, 0.6f), Metal(new Color(0.3f, 0.32f, 0.33f)), 1f, false);
            }
            // Workshop tools: a drill press, a rolling tool chest, a toolbox left on the floor.
            PlaceNear(Room.Parts, new Vector2(-8.7f, -7.7f), 0.4f, p => BuildKit.Prop("old_drill_press", root, new Vector3(p.x, 0f, p.y), 225f), 1.5f);
            PlaceNear(Room.Parts, new Vector2(-13.4f, -12.2f), 0.4f, p => BuildKit.Prop("metal_tool_chest", root, new Vector3(p.x, 0f, p.y), 90f), 1.5f);
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
                Place(Room.Restrooms, new Vector2(-2.5f, z), 0.3f, () =>
                {
                    BuildKit.Box("Sink", root, new Vector3(-2.45f, 0.85f, z), new Vector3(0.5f, 0.15f, 0.6f), porcelain);
                    BuildKit.Box("Mirror", root, new Vector3(-2.22f, 1.6f, z), new Vector3(0.02f, 0.7f, 0.55f), mirror, 1f, false);
                });
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
            PlaceNear(Room.Kitchen, new Vector2(-12.4f, 7.6f), 0.5f, p =>
                BuildKit.Box("Fridge", root, new Vector3(p.x, 1.0f, p.y), new Vector3(0.75f, 2.0f, 0.8f), Metal(new Color(0.75f, 0.75f, 0.72f))));
            PlaceNear(Room.Kitchen, new Vector2(-7.75f, 9.3f), 0.6f, p =>
            {
                BuildKit.Box("Counter", root, new Vector3(p.x, 0.45f, p.y), new Vector3(1.6f, 0.9f, 0.7f), steel);
                PizzaBoxes(root, new Vector3(p.x + 0.3f, 0.9f, p.y), 4);
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
            BuildKit.Cone("Star", root, new Vector3(stage.center.x, 2.9f, stage.yMax - 0.22f), 0.4f, 0.01f, BuildKit.Glow("star", new Color(1f, 0.8f, 0.2f), 1.5f)).transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
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
            Color[] screens = { new Color(0.1f, 0.8f, 1f), new Color(1f, 0.2f, 0.7f), new Color(0.3f, 1f, 0.3f), new Color(1f, 0.8f, 0.1f) };
            for (int i = 0; i < 4; i++)
            {
                var position = new Vector2(17.6f + i * 1.1f, 9.4f);
                Color color = screens[i % screens.Length];
                PlaceNear(Room.Arcade, position, 0.5f, p => ArcadeCabinet(root, p, 180f, color), 1.5f);
            }
            PlaceNear(Room.Arcade, new Vector2(21f, 2.7f), 0.5f, p => ArcadeCabinet(root, p, 0f, screens[2]), 1f);
            // Neon strips along the walls, the most colorful light in the building.
            NeonStrip(root, new Vector3(19f, 3.1f, 9.74f), new Vector3(7f, 0.06f, 0.06f), new Color(1f, 0.15f, 0.7f));
            NeonStrip(root, new Vector3(22.74f, 3.1f, 6f), new Vector3(0.06f, 0.06f, 7f), new Color(0.1f, 0.85f, 1f));
            Sign(root, new Vector3(22.77f, 2.3f, 5.4f), 270f, "PRIZES", new Color(0.2f, 1f, 1f), new Color(0.1f, 0.02f, 0.15f), 1.8f);
        }

        private static void Lobby(Transform root, System.Random random)
        {
            PlaceNear(Room.ExitLobby, new Vector2(13.2f, -9.9f), 0.9f, p =>
            {
                BuildKit.Box("TicketBooth", root, new Vector3(p.x, 0.6f, p.y), new Vector3(2.0f, 1.2f, 1.0f), BuildKit.Plain("booth", new Color(0.4f, 0.1f, 0.1f), 0.4f));
                BuildKit.Box("Glass", root, new Vector3(p.x, 1.6f, p.y - 0.45f), new Vector3(2.0f, 0.8f, 0.05f), BuildKit.Plain("glass", new Color(0.3f, 0.35f, 0.4f), 0.9f), 1f, false);
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
            ExitSigns.Add((exit, ExitLamp(root, position, facing)));
        }

        /// <summary>A glowing EXIT box sign facing <paramref name="facing"/>, with a green light under it.</summary>
        public static GameObject ExitLamp(Transform root, Vector3 position, float facing)
        {
            Texture2D texture = ProceduralTextures.Sign("EXIT", new Color(0.55f, 1f, 0.6f), new Color(0.02f, 0.25f, 0.08f));
            var sign = BuildKit.Quad("ExitSign", root, position, new Vector2(0.9f, 0.9f * texture.height / texture.width), facing, BuildKit.Picture("exitGreen", texture, 3f));
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
            for (int i = 0; i < 3; i++)
            {
                Vector3 position = top + new Vector3(-0.6f + i * 0.6f, 0f, (i % 2) * 0.2f - 0.1f);
                BuildKit.Cylinder("Plate", parent, position + Vector3.up * 0.01f, 0.25f, 0.01f, BuildKit.Plain("plate", new Color(0.9f, 0.9f, 0.85f), 0.6f));
                BuildKit.Cone("PartyHat", parent, position + new Vector3(0.12f, 0.01f, 0.1f), 0.07f, 0.22f, BuildKit.Plain("hat" + i, PartyColors[i % PartyColors.Length], 0.3f));
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
            BuildKit.Sphere("Balloon", root, position, 0.35f, BuildKit.Plain("balloon" + color.GetHashCode(), color, 0.75f)).transform.localScale = new Vector3(0.33f, 0.4f, 0.33f);
            BuildKit.Box("String", root, position - Vector3.up * 0.6f, new Vector3(0.005f, 1f, 0.005f), BuildKit.Plain("string", new Color(0.8f, 0.8f, 0.8f)), 1f, false);
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

        private static void ArcadeCabinet(Transform root, Vector2 position, float yaw, Color screen)
        {
            var cabinet = new GameObject("ArcadeCabinet").transform;
            cabinet.SetParent(root, false);
            cabinet.localPosition = new Vector3(position.x, 0f, position.y);
            cabinet.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Material body = BuildKit.Plain("arcadeBody", new Color(0.08f, 0.06f, 0.15f), 0.4f);
            BuildKit.Box("Body", cabinet, new Vector3(0f, 0.9f, 0f), new Vector3(0.75f, 1.8f, 0.75f), body);
            var glass = BuildKit.Quad("Screen", cabinet, new Vector3(0f, 1.3f, 0.385f), new Vector2(0.55f, 0.42f), 0f, BuildKit.Glow("arcadeScreen" + screen.GetHashCode(), screen, 1.6f));
            glass.transform.localRotation = Quaternion.Euler(-12f, 180f, 0f);
            BuildKit.Box("Marquee", cabinet, new Vector3(0f, 1.95f, 0.2f), new Vector3(0.75f, 0.25f, 0.35f), BuildKit.Glow("marquee" + screen.GetHashCode(), screen * 0.8f, 2f), 1f, false);
            BuildKit.Box("Panel", cabinet, new Vector3(0f, 1.0f, 0.45f), new Vector3(0.7f, 0.08f, 0.25f), BuildKit.Plain("panelBlack", new Color(0.03f, 0.03f, 0.03f)), 1f, false);
            var light = new GameObject("ScreenLight").AddComponent<Light>();
            light.transform.SetParent(cabinet, false);
            light.transform.localPosition = new Vector3(0f, 1.3f, 0.9f);
            light.type = LightType.Point;
            light.range = 2.2f;
            light.intensity = 0.6f;
            light.color = screen;
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
        public static void Endoskeleton(Transform root, Vector3 bench) => LevelClutter.Endoskeleton(root, bench);

        public static void SpareHead(Transform root, Vector3 position, float yaw, float scale = 1f)
        {
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

        private static void PizzaBoxes(Transform root, Vector3 position, int count)
        {
            for (int i = 0; i < count; i++)
            {
                BuildKit.Box("PizzaBox", root, position + new Vector3(0f, 0.025f + i * 0.05f, 0f), new Vector3(0.45f, 0.045f, 0.45f), Cardboard(), 1f, false, i * 7f);
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
                var position = new Vector3(spotOnWall.x, 0f, spotOnWall.y);
                if (hall || room == Room.Office)
                {
                    position.y = 1.5f + (float)random.NextDouble() * 0.5f;
                    BuildKit.Quad("Drawing", root, position, new Vector2(0.55f, 0.55f), facing, BuildKit.Picture("drawing" + index % 4, ProceduralTextures.Drawing(index % 4)))
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
                    BuildKit.Quad("Poster", root, position, new Vector2(0.9f, 1.25f), facing,
                                  BuildKit.Picture("poster" + poster, ProceduralTextures.Poster(backs[poster], new Color(0.45f, 0.26f, 0.12f), titles[poster])));
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

        public static void Sign(Transform root, Vector3 position, float facing, string text, Color foreground, Color background, float width)
        {
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
        public static void PlaceNear(Room room, Vector2 desired, float radius, System.Action<Vector2> build, float maxShift = 3.5f)
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
                        return;
                    }
                }
            }
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-capture") >= 0)
            {
                Debug.Log("CAPTURE prop has no room in " + room + " near " + desired + " (radius " + radius + ")");
            }
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
}
