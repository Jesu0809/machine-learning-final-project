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
                case Room.Manager: Manager(root, random); break;
                case Room.Lounge: Lounge(root, random); break;
                case Room.Parts: Parts(root, random); break;
                case Room.Maintenance: Maintenance(root, random); break;
                case Room.UpperHall:
                case Room.BackHall: Hall(room, root, random); break;
                case Room.Storage: Storage(root, random); break;
                case Room.Kitchen: Kitchen(root, random); break;
                case Room.DiningHall: Dining(root, random); break;
                case Room.Arcade: Arcade(root, random); break;
                case Room.Stage: Stage(root, random); break;
                case Room.PartyRoom: PartyRoom(root, random); break;
                case Room.ExitLobby: Lobby(root, random); break;
            }
            WallDecor(room, root, random);
        }

        // ------------------------------------------------------------------ rooms (local coordinates: floor at y = 0)

        private static void Office(Transform root, System.Random random)
        {
            Material wood = Wood();
            // The guard's desk along the west wall, facing the door.
            PlaceNear(Room.Office, new Vector2(-17.1f, 16.2f), 1.0f, p =>
            {
                BuildKit.Box("Desk", root, new Vector3(p.x - 0.1f, 0.4f, p.y), new Vector3(0.9f, 0.8f, 2.4f), wood);
                for (int i = 0; i < 2; i++)
                {
                    var monitor = BuildKit.Box("Monitor", root, new Vector3(p.x - 0.3f, 1.05f, p.y - 0.5f + i * 1.0f), new Vector3(0.45f, 0.45f, 0.55f), BuildKit.Plain("crt", new Color(0.18f, 0.17f, 0.15f), 0.3f));
                    BuildKit.Quad("Screen", monitor.transform, new Vector3(0.505f, 0.02f, 0f), new Vector2(0.75f, 0.8f), 90f, BuildKit.Glow("screenGreen", new Color(0.2f, 0.9f, 0.5f), 1.2f));
                }
                Chair(root, new Vector2(p.x + 0.8f, p.y), 270f);
                BuildKit.Box("Papers", root, new Vector3(p.x + 0.1f, 0.81f, p.y + 0.7f), new Vector3(0.3f, 0.02f, 0.4f), BuildKit.Plain("paper", new Color(0.85f, 0.83f, 0.75f)), 1f, false);
            });
            Place(Room.Office, new Vector2(-10.6f, 18.4f), 0.6f, () =>
                BuildKit.Box("FilingCabinet", root, new Vector3(-10.6f, 0.7f, 18.45f), new Vector3(0.7f, 1.4f, 0.5f), Metal(new Color(0.35f, 0.38f, 0.36f))));
            Sign(root, new Vector3(-14f, 2.3f, 18.77f), 180f, "SECURITY", new Color(0.8f, 0.8f, 0.7f), new Color(0.12f, 0.15f, 0.25f), 1.8f);
        }

        private static void Manager(Transform root, System.Random random)
        {
            Material wood = BuildKit.Surface("darkWood", () => ProceduralTextures.Planks(new Color(0.22f, 0.12f, 0.07f)), 0.45f);
            PlaceNear(Room.Manager, new Vector2(-4f, 17.6f), 1.0f, p =>
            {
                BuildKit.Box("ManagerDesk", root, new Vector3(p.x, 0.42f, p.y), new Vector3(2.0f, 0.84f, 0.9f), wood);
                Chair(root, new Vector2(p.x, p.y + 0.8f), 180f);
                BuildKit.Box("Lamp", root, new Vector3(p.x - 0.7f, 1.05f, p.y + 0.2f), new Vector3(0.18f, 0.4f, 0.18f), BuildKit.Glow("deskLamp", new Color(1f, 0.75f, 0.4f), 1.4f), 1f, false);
                BuildKit.Box("Ledger", root, new Vector3(p.x + 0.4f, 0.86f, p.y - 0.1f), new Vector3(0.45f, 0.04f, 0.32f), BuildKit.Plain("ledger", new Color(0.35f, 0.08f, 0.08f), 0.3f), 1f, false);
            });
            PlaceNear(Room.Manager, new Vector2(-7.4f, 14f), 0.5f, p =>
                BuildKit.Box("Safe", root, new Vector3(p.x, 0.45f, p.y), new Vector3(0.7f, 0.9f, 0.7f), Metal(new Color(0.2f, 0.22f, 0.22f))));
            Sign(root, new Vector3(-4f, 2.2f, 18.77f), 180f, "MANAGER", new Color(0.95f, 0.85f, 0.5f), new Color(0.25f, 0.08f, 0.05f), 1.6f);
            // Framed photos of the band on opening day.
            for (int i = 0; i < 3; i++)
            {
                BuildKit.Quad("Photo", root, new Vector3(-0.23f, 1.6f + (i % 2) * 0.25f, 17.2f + i * 0.6f), new Vector2(0.45f, 0.35f), 270f,
                              BuildKit.Picture("drawing" + (i + 1) % 4, ProceduralTextures.Drawing((i + 1) % 4)));
            }
        }

        private static void Lounge(Transform root, System.Random random)
        {
            Material couch = BuildKit.Surface("couch", () => ProceduralTextures.Grimy(new Color(0.35f, 0.12f, 0.08f), 0.3f), 0.15f);
            PlaceNear(Room.Lounge, new Vector2(6.5f, 18.1f), 1.0f, p =>
            {
                BuildKit.Box("CouchSeat", root, new Vector3(p.x, 0.25f, p.y + 0.1f), new Vector3(2.0f, 0.5f, 0.8f), couch);
                BuildKit.Box("CouchBack", root, new Vector3(p.x, 0.65f, p.y + 0.45f), new Vector3(2.0f, 0.8f, 0.2f), couch, 1f, false);
                BuildKit.Box("CoffeeTable", root, new Vector3(p.x, 0.25f, p.y - 0.9f), new Vector3(1.1f, 0.5f, 0.5f), Wood());
                BuildKit.Cylinder("Mug", root, new Vector3(p.x + 0.2f, 0.56f, p.y - 0.9f), 0.09f, 0.12f, BuildKit.Plain("mug", new Color(0.8f, 0.8f, 0.75f), 0.5f));
            });
            Place(Room.Lounge, new Vector2(2.6f, 18.4f), 0.6f, () =>
            {
                BuildKit.Box("VendingMachine", root, new Vector3(2.55f, 0.95f, 18.4f), new Vector3(0.9f, 1.9f, 0.8f), BuildKit.Plain("vending", new Color(0.6f, 0.08f, 0.1f), 0.5f));
                BuildKit.Quad("VendingGlass", root, new Vector3(3.01f, 1.15f, 18.4f), new Vector2(0.55f, 1f), 90f, BuildKit.Glow("vendingGlow", new Color(0.8f, 0.9f, 1f), 0.9f));
            });
            PlaceNear(Room.Lounge, new Vector2(3.6f, 13.6f), 0.6f, p =>
            {
                BuildKit.Box("TVStand", root, new Vector3(p.x, 0.35f, p.y), new Vector3(1.1f, 0.7f, 0.5f), Wood());
                BuildKit.Box("TV", root, new Vector3(p.x, 0.98f, p.y), new Vector3(0.8f, 0.6f, 0.5f), BuildKit.Plain("crt", new Color(0.18f, 0.17f, 0.15f), 0.3f));
                BuildKit.Quad("TVScreen", root, new Vector3(p.x, 0.98f, p.y + 0.26f), new Vector2(0.6f, 0.45f), 0f, BuildKit.Picture("cctv", ProceduralTextures.Static(64, 48), 0.9f));
            });
            Sign(root, new Vector3(6f, 2.3f, 18.77f), 180f, "STAFF ONLY", new Color(0.9f, 0.9f, 0.85f), new Color(0.15f, 0.15f, 0.18f), 1.8f);
        }

        private static void Parts(Transform root, System.Random random)
        {
            Material wood = Wood();
            // Workbenches with animatronic parts: endoskeletons, arms, rows of empty heads.
            PlaceNear(Room.Parts, new Vector2(-17.5f, 5.3f), 1.0f, p =>
            {
                BuildKit.Box("Workbench", root, new Vector3(p.x, 0.45f, p.y), new Vector3(2.2f, 0.9f, 0.9f), wood);
                Endoskeleton(root, new Vector3(p.x, 0.9f, p.y));
            });
            PlaceNear(Room.Parts, new Vector2(-17.5f, -2.5f), 1.0f, p =>
            {
                BuildKit.Box("Workbench", root, new Vector3(p.x, 0.45f, p.y), new Vector3(2.4f, 0.9f, 1.0f), wood);
                for (int i = 0; i < 3; i++)
                {
                    SpareHead(root, new Vector3(p.x - 0.8f + i * 0.8f, 1.2f, p.y), 180f + (i - 1) * 20f, 0.8f);
                }
            });
            PlaceNear(Room.Parts, new Vector2(-12.5f, 4.8f), 0.7f, p => Endoskeleton(root, new Vector3(p.x, 0.05f, p.y)));
            for (int i = 0; i < 4; i++)
            {
                BuildKit.Box("HeadShelf", root, new Vector3(-10.35f, 1.6f + (i % 2) * 0.6f, -2.5f + i * 0.4f), new Vector3(0.35f, 0.04f, 0.6f), Metal(new Color(0.3f, 0.32f, 0.33f)), 1f, false);
            }
            Sign(root, new Vector3(-15.5f, 2.4f, -3.77f), 0f, "PARTS & SERVICE", new Color(0.85f, 0.85f, 0.8f), new Color(0.2f, 0.22f, 0.2f), 2.6f);
        }

        private static void Maintenance(Transform root, System.Random random)
        {
            Material steel = Metal(new Color(0.4f, 0.42f, 0.42f));
            // The boiler and the generator: big humming shapes and pipes.
            Place(Room.Maintenance, new Vector2(16f, -2.8f), 1.3f, () =>
            {
                BuildKit.Cylinder("Boiler", root, new Vector3(16f, 1.1f, -2.8f), 1.5f, 2.2f, steel);
                BuildKit.Cylinder("Flue", root, new Vector3(16f, 2.7f, -2.8f), 0.35f, 1.2f, steel);
                BuildKit.Box("BoilerLight", root, new Vector3(16f, 1.3f, -2.03f), new Vector3(0.15f, 0.08f, 0.02f), BuildKit.Glow("boilerLight", new Color(1f, 0.25f, 0.1f), 2.5f), 1f, false);
            });
            PlaceNear(Room.Maintenance, new Vector2(15.5f, 5f), 1.0f, p =>
            {
                BuildKit.Box("Generator", root, new Vector3(p.x, 0.6f, p.y), new Vector3(2f, 1.2f, 1f), BuildKit.Plain("generator", new Color(0.55f, 0.45f, 0.1f), 0.4f, 0.4f));
                BuildKit.Box("Exhaust", root, new Vector3(p.x - 0.6f, 1.6f, p.y + 0.3f), new Vector3(0.2f, 0.8f, 0.2f), steel, 1f, false);
            });
            for (int i = 0; i < 2; i++)
            {
                PlaceNear(Room.Maintenance, new Vector2(11f + i * 1.2f, -3.3f), 0.5f, p => Crate(root, new Vector3(p.x, 0f, p.y), i * 25f));
            }
            for (int i = 0; i < 3; i++)
            {
                var pipe = BuildKit.Cylinder("Pipe", root, new Vector3(19.7f, 1.6f + i * 0.35f, 1f), 0.14f, 9f, BuildKit.Plain("pipe", new Color(0.32f, 0.3f, 0.27f), 0.5f, 0.6f));
                pipe.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            Sign(root, new Vector3(15f, 2.4f, 5.77f), 180f, "DANGER HIGH VOLTAGE", new Color(1f, 0.85f, 0.1f), new Color(0.12f, 0.1f, 0.05f), 2.6f);
        }

        private static void Hall(Room room, Transform root, System.Random random)
        {
            Rect area = PizzeriaLayout.Area(room);
            if (room == Room.BackHall)
            {
                // Garland of little flags along the top of the walls.
                for (float x = area.xMin + 0.5f; x < area.xMax - 0.5f; x += 0.6f)
                {
                    Material flag = BuildKit.Plain("flag" + ((int)(x * 10f) % PartyColors.Length + 5) % PartyColors.Length, PartyColors[Mathf.Abs((int)(x * 1.7f)) % PartyColors.Length]);
                    BuildKit.Box("Flag", root, new Vector3(x, 2.55f + Mathf.Sin(x * 2f) * 0.05f, area.yMax - 0.15f), new Vector3(0.25f, 0.25f, 0.01f), flag, 1f, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    BuildKit.Box("Flag", root, new Vector3(x, 2.55f + Mathf.Sin(x * 2f) * 0.05f, area.yMin + 0.15f), new Vector3(0.25f, 0.25f, 0.01f), flag, 1f, false).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                }
                // A wet floor sign nobody picked up.
                Place(room, new Vector2(-2f, 8.8f), 0.4f, () =>
                {
                    var sign = BuildKit.Box("WetFloor", root, new Vector3(-2f, 0.3f, 8.8f), new Vector3(0.35f, 0.6f, 0.05f), BuildKit.Plain("yellow", new Color(0.9f, 0.75f, 0.1f), 0.5f), 1f, false);
                    sign.transform.localRotation = Quaternion.Euler(10f, 30f, 0f);
                });
            }
            else
            {
                // Upstairs: the staff hall, with a strip of carpet and employee-of-the-month frames.
                BuildKit.Box("Runner", root, new Vector3(area.center.x, 0.005f, area.center.y), new Vector3(area.width - 1f, 0.01f, 1.2f),
                             BuildKit.Surface("carpet", () => ProceduralTextures.Carpet(), 0.05f), 2f, false);
            }
        }

        private static void Storage(Transform root, System.Random random)
        {
            // Free-standing racks in the middle of the warehouse, crates against the walls.
            for (int i = 0; i < 3; i++)
            {
                float z = 12.2f + i * 2.4f;
                Shelf(root, random, new Vector2(18.6f, z), 90f, Room.Storage);
            }
            PlaceNear(Room.Storage, new Vector2(20.8f, 10.6f), 0.5f, p => Crate(root, new Vector3(p.x, 0f, p.y), 15f));
            Place(Room.Storage, new Vector2(17.2f, 18.3f), 0.6f, () => Crate(root, new Vector3(17.2f, 0f, 18.3f), -10f));
            Sign(root, new Vector3(21.77f, 5.5f, 13.5f), 270f, "STORAGE", new Color(0.9f, 0.9f, 0.85f), new Color(0.2f, 0.2f, 0.22f), 2.2f);
        }

        private static void Kitchen(Transform root, System.Random random)
        {
            Material steel = Metal(new Color(0.55f, 0.57f, 0.6f));
            Place(Room.Kitchen, new Vector2(-16f, 5.55f), 1.4f, () =>
            {
                BuildKit.Box("Counter", root, new Vector3(-16f, 0.45f, 5.55f), new Vector3(2.6f, 0.9f, 0.7f), steel);
                for (int i = 0; i < 3; i++)
                {
                    BuildKit.Cylinder("Pan", root, new Vector3(-17f + i * 0.9f, 2.0f, 5.85f), 0.35f, 0.04f, BuildKit.Plain("pan", new Color(0.12f, 0.12f, 0.12f), 0.6f, 0.8f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                PizzaBoxes(root, new Vector3(-15.4f, 0.9f, 5.55f), 4);
            });
            Place(Room.Kitchen, new Vector2(-10.6f, -4f), 0.6f, () =>
            {
                BuildKit.Box("Fridge", root, new Vector3(-10.6f, 1.0f, -4f), new Vector3(0.75f, 2.0f, 0.8f), Metal(new Color(0.75f, 0.75f, 0.72f)));
            });
            Place(Room.Kitchen, new Vector2(-12.5f, -4.3f), 1.0f, () =>
            {
                BuildKit.Box("Stove", root, new Vector3(-12.5f, 0.45f, -4.4f), new Vector3(1.4f, 0.9f, 0.7f), BuildKit.Plain("stove", new Color(0.1f, 0.1f, 0.1f), 0.5f));
                BuildKit.Box("Hood", root, new Vector3(-12.5f, 2.3f, -4.5f), new Vector3(1.5f, 0.5f, 0.6f), steel, 1f, false);
            });
            PlaceNear(Room.Kitchen, new Vector2(-17f, 1.5f), 1.0f, p =>
            {
                BuildKit.Box("Island", root, new Vector3(p.x, 0.45f, p.y), new Vector3(1.8f, 0.9f, 0.9f), steel);
                PizzaBoxes(root, new Vector3(p.x + 0.3f, 0.9f, p.y), 2);
            });
        }

        private static void Dining(Transform root, System.Random random)
        {
            var tables = new[]
            {
                new Vector2(-3.8f, 1.6f), new Vector2(3.6f, 1.6f), new Vector2(-3.8f, -3.6f), new Vector2(3.6f, -3.6f),
                new Vector2(-1.6f, -7.6f), new Vector2(-4.4f, -1f)
            };
            foreach (Vector2 table in tables)
            {
                PlaceNear(Room.DiningHall, table, 1.1f, p => PartyTable(root, random, p), 2.5f);
            }
            // The atrium: balloons drifting up under the high ceiling, streamers across from the balcony.
            for (int i = 0; i < 18; i++)
            {
                var position = new Vector3(-6f + (float)random.NextDouble() * 12f, 5.5f + (float)random.NextDouble() * 1.8f, -9f + (float)random.NextDouble() * 12f);
                Balloon(root, position, PartyColors[i % PartyColors.Length]);
            }
            for (int i = 0; i < 4; i++)
            {
                Streamer(root, new Vector3(-6.8f, 7.2f, -8f + i * 3.3f), new Vector3(6.8f, 7.2f, -8f + i * 3.3f), PartyColors[i % PartyColors.Length]);
            }
            Sign(root, new Vector3(-6.77f, 5.6f, -2f), 90f, "LET'S PARTY!", new Color(1f, 0.85f, 0.2f), new Color(0.5f, 0.05f, 0.1f), 4.2f);
            Sign(root, new Vector3(5.2f, 7.1f, 5.77f), 180f, "STAFF ONLY UPSTAIRS", new Color(0.9f, 0.9f, 0.85f), new Color(0.2f, 0.05f, 0.05f), 2.4f);
        }

        private static void Arcade(Transform root, System.Random random)
        {
            Color[] screens = { new Color(0.1f, 0.8f, 1f), new Color(1f, 0.2f, 0.7f), new Color(0.3f, 1f, 0.3f), new Color(1f, 0.8f, 0.1f) };
            for (int i = 0; i < 4; i++)
            {
                var position = new Vector2(12.6f + i * 1.15f, 5.4f);
                Color color = screens[i % screens.Length];
                PlaceNear(Room.Arcade, position, 0.5f, p => ArcadeCabinet(root, p, 180f, color), 1.5f);
            }
            for (int i = 0; i < 3; i++)
            {
                var position = new Vector2(16.6f + i * 1.15f, -4.4f);
                Color color = screens[(i + 2) % screens.Length];
                PlaceNear(Room.Arcade, position, 0.5f, p => ArcadeCabinet(root, p, 0f, color), 1.5f);
            }
            // Neon strips along the walls, the only colorful light in the building.
            NeonStrip(root, new Vector3(15f, 3.1f, 5.74f), new Vector3(9f, 0.06f, 0.06f), new Color(1f, 0.15f, 0.7f));
            NeonStrip(root, new Vector3(19.74f, 3.1f, 0.5f), new Vector3(0.06f, 0.06f, 10f), new Color(0.1f, 0.85f, 1f));
            Sign(root, new Vector3(19.77f, 2.3f, -1.5f), 270f, "PRIZES", new Color(0.2f, 1f, 1f), new Color(0.1f, 0.02f, 0.15f), 1.8f);
        }

        private static void Stage(Transform root, System.Random random)
        {
            Material planks = BuildKit.Surface("stagePlanks", () => ProceduralTextures.Planks(new Color(0.45f, 0.28f, 0.15f)), 0.35f);
            // The show stage along the west wall, with a skirt, curtains and a star.
            BuildKit.Box("Stage", root, new Vector3(-19f, 0.35f, -13.5f), new Vector3(4f, 0.7f, 8f), planks, 1f);
            BuildKit.Box("Skirt", root, new Vector3(-16.97f, 0.35f, -13.5f), new Vector3(0.06f, 0.7f, 8f), BuildKit.Plain("skirt", new Color(0.35f, 0.05f, 0.08f)), 1f, false);
            Material curtain = BuildKit.Surface("curtain", () => ProceduralTextures.Grimy(new Color(0.42f, 0.05f, 0.2f)), 0.15f);
            // Folded curtains drawn to both sides (overlapping folds, no gaps), open in the middle.
            for (int i = 0; i < 16; i++)
            {
                float z = -18.7f + i * 0.45f;
                if (z > -16.2f && z < -10.8f)
                {
                    continue;
                }
                BuildKit.Box("Curtain", root, new Vector3(-17.3f + (i % 2) * 0.12f, 2.55f, z), new Vector3(0.18f, 4.5f, 0.55f), curtain, 1f, false);
            }
            BuildKit.Box("Valance", root, new Vector3(-17.3f, 4.65f, -13.5f), new Vector3(0.3f, 0.8f, 10.6f), curtain, 1f, false);
            Sign(root, new Vector3(-17.13f, 4.65f, -13.5f), 90f, "SHOWTIME", new Color(1f, 0.85f, 0.25f), new Color(0.2f, 0.02f, 0.12f), 2.6f);
            BuildKit.Cone("Star", root, new Vector3(-20.76f, 4.2f, -13.5f), 0.4f, 0.01f, BuildKit.Glow("star", new Color(1f, 0.8f, 0.2f), 1.5f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            // Two theatre spots aimed at the stage, with hard shadows.
            StageSpot(root, new Vector3(-12f, 4f, -16.5f), new Vector3(-19f, 0.7f, -13.5f), new Color(1f, 0.75f, 0.45f));
            StageSpot(root, new Vector3(-12f, 4f, -10.5f), new Vector3(-19f, 0.7f, -13.5f), new Color(0.8f, 0.45f, 1f));

            // Rows of chairs facing the stage (skipping Freddy's paths).
            for (int row = 0; row < 3; row++)
            {
                for (int seat = 0; seat < 6; seat++)
                {
                    var position = new Vector2(-14.5f + row * 1.4f, -17.4f + seat * 1.15f);
                    Place(Room.Stage, position, 0.35f, () => Chair(root, position, 270f));
                }
            }
        }

        private static void PartyRoom(Transform root, System.Random random)
        {
            PlaceNear(Room.PartyRoom, new Vector2(2.8f, -17.2f), 1.0f, p => PartyTable(root, random, p));
            PlaceNear(Room.PartyRoom, new Vector2(-3f, -13.6f), 1.0f, p => PartyTable(root, random, p));
            for (int i = 0; i < 10; i++)
            {
                var position = new Vector3(-5f + (float)random.NextDouble() * 10f, 2.5f + (float)random.NextDouble() * 0.6f, -18.5f + (float)random.NextDouble() * 6f);
                Balloon(root, position, PartyColors[i % PartyColors.Length]);
            }
            Streamer(root, new Vector3(-5.8f, 3.1f, -15.5f), new Vector3(5.8f, 3.1f, -15.5f), PartyColors[2]);
            Sign(root, new Vector3(0f, 2.4f, -18.77f), 0f, "PARTY ROOM", new Color(1f, 0.85f, 0.2f), new Color(0.5f, 0.05f, 0.1f), 2.6f);
        }

        private static void Lobby(Transform root, System.Random random)
        {
            Place(Room.ExitLobby, new Vector2(9.2f, -11f), 1.0f, () =>
            {
                BuildKit.Box("TicketBooth", root, new Vector3(9.2f, 0.6f, -11f), new Vector3(1.2f, 1.2f, 2.2f), BuildKit.Plain("booth", new Color(0.4f, 0.1f, 0.1f), 0.4f));
                BuildKit.Box("Glass", root, new Vector3(9.2f, 1.6f, -11f), new Vector3(1.2f, 0.8f, 0.05f), BuildKit.Plain("glass", new Color(0.3f, 0.35f, 0.4f), 0.9f), 1f, false);
            });
            Place(Room.ExitLobby, new Vector2(16.8f, -14f), 0.8f, () =>
                BuildKit.Box("Bench", root, new Vector3(17.4f, 0.25f, -14f), new Vector3(0.5f, 0.5f, 1.8f), Wood()));
        }

        // ------------------------------------------------------------------ pieces

        private static void PartyTable(Transform root, System.Random random, Vector2 center)
        {
            Material cloth = BuildKit.Surface("tablecloth", () => ProceduralTextures.Tiles(new Color(0.7f, 0.1f, 0.12f), new Color(0.88f, 0.85f, 0.78f), 6, true, 0.4f), 0.05f);
            BuildKit.Box("Table", root, new Vector3(center.x, 0.75f, center.y), new Vector3(2.0f, 0.06f, 0.9f), cloth);
            BuildKit.Box("Leg", root, new Vector3(center.x, 0.37f, center.y), new Vector3(0.15f, 0.74f, 0.15f), Metal(new Color(0.3f, 0.3f, 0.32f)));
            PartySetting(root, new Vector3(center.x, 0.78f, center.y));
            for (int i = 0; i < 3; i++)
            {
                Chair(root, new Vector2(center.x - 0.65f + i * 0.65f, center.y + 0.75f), 180f);
                Chair(root, new Vector2(center.x - 0.65f + i * 0.65f, center.y - 0.75f), 0f);
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

        private static void Chair(Transform root, Vector2 position, float yaw)
        {
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

        private static void Shelf(Transform root, System.Random random, Vector2 position, float yaw, Room room)
        {
            PlaceNear(room, position, 0.8f, p =>
            {
                var shelf = new GameObject("Shelf").transform;
                shelf.SetParent(root, false);
                shelf.localPosition = new Vector3(p.x, 0f, p.y);
                shelf.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Material metal = Metal(new Color(0.3f, 0.32f, 0.33f));
                for (int level = 0; level < 4; level++)
                {
                    BuildKit.Box("Board", shelf, new Vector3(0f, 0.15f + level * 0.6f, 0f), new Vector3(1.6f, 0.04f, 0.5f), metal, 1f, level == 0);
                    for (int b = 0; b < 3; b++)
                    {
                        if (random.NextDouble() < 0.35)
                        {
                            continue;
                        }
                        float w = 0.3f + (float)random.NextDouble() * 0.2f;
                        BuildKit.Box("Box", shelf, new Vector3(-0.5f + b * 0.5f, 0.17f + level * 0.6f + w * 0.5f, 0f), new Vector3(w, w, 0.4f), Cardboard(), 1f, false);
                    }
                }
                foreach (float x in new[] { -0.78f, 0.78f })
                {
                    foreach (float z in new[] { -0.23f, 0.23f })
                    {
                        BuildKit.Box("Post", shelf, new Vector3(x, 1.0f, z), new Vector3(0.04f, 2.0f, 0.04f), metal, 1f, false);
                    }
                }
            });
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
                if (hall || room == Room.Office || room == Room.Manager || room == Room.Lounge)
                {
                    position.y = 1.5f + (float)random.NextDouble() * 0.5f;
                    BuildKit.Quad("Drawing", root, position, new Vector2(0.55f, 0.55f), facing, BuildKit.Picture("drawing" + index % 4, ProceduralTextures.Drawing(index % 4)))
                        .transform.localRotation *= Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 10f - 5f);
                }
                else if (room == Room.Kitchen || room == Room.Storage || room == Room.Parts || room == Room.Maintenance)
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
            if (room == HuntMap.Exit && Vector2.Distance(point, new Vector2(PizzeriaLayout.ExitDoor.x + 1.2f, PizzeriaLayout.ExitDoor.z)) < distance + 1.6f)
            {
                return false;
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
        private static Material Metal(Color color) => BuildKit.Surface("metal" + color.GetHashCode(), () => ProceduralTextures.Grimy(color, 1f), 0.45f, 0.6f);
        private static Material Cardboard() => BuildKit.Surface("cardboard", () => ProceduralTextures.Grimy(new Color(0.55f, 0.42f, 0.25f), 0.2f), 0.1f);
    }
}
