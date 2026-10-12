using System.Collections.Generic;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>Everything <see cref="LevelBuilder"/> created that the game needs to talk to.</summary>
    public class Level
    {
        public Transform Root;
        public readonly HidingSpot[][] Spots = new HidingSpot[HuntMap.RoomCount][];
        public readonly FusePickup[] Fuses = new FusePickup[HuntMap.RoomCount];
        public readonly List<FlickerLight>[] Lamps = new List<FlickerLight>[HuntMap.RoomCount];
        public ExitPanel Exit;

        public HidingSpot Spot(Room room, int index) => Spots[(int)room][index];
    }

    /// <summary>Look of each room: floor, lower wall, upper wall.</summary>
    internal struct RoomStyle
    {
        public Material Floor;
        public Material Wainscot;
        public Material Wall;
        public Material Ceiling;
    }

    /// <summary>
    /// Builds the pizzeria in code following <see cref="PizzeriaLayout"/>: rooms of different
    /// sizes, corridors, lamps with light shafts and dust, hiding spots, fuses, the exit, and the
    /// decoration of every room (see <see cref="LevelProps"/>). No hand-made level file is needed,
    /// so the 3D world always matches the graph Freddy's brain was trained on.
    /// </summary>
    public static class LevelBuilder
    {
        private const float WainscotHeight = 1.1f;

        public static Level Build(Transform parent, GameAssets assets)
        {
            BuildKit.Init(assets);
            var level = new Level { Root = new GameObject("Pizzeria").transform };
            level.Root.SetParent(parent, false);
            var random = new System.Random(1987);

            for (int i = 0; i < HuntMap.RoomCount; i++)
            {
                var room = (Room)i;
                Transform roomRoot = new GameObject(HuntMap.DisplayName(room)).transform;
                roomRoot.SetParent(level.Root, false);
                RoomStyle style = StyleOf(room);

                BuildShell(room, roomRoot, style);
                level.Lamps[i] = BuildLamps(room, roomRoot, random);
                LevelProps.Decorate(room, roomRoot, random);
                LevelClutter.Add(room, roomRoot, random);

                level.Spots[i] = new HidingSpot[HuntMap.SpotCount(room)];
                for (int spot = 0; spot < HuntMap.SpotCount(room); spot++)
                {
                    level.Spots[i][spot] = BuildSpot(room, spot, roomRoot);
                }
                if (PizzeriaLayout.HasFuseShelf(room))
                {
                    level.Fuses[i] = BuildFuse(room, roomRoot);
                }
            }
            BuildCorridors(level.Root);
            level.Exit = BuildExit(level.Root);
            return level;
        }

        // ------------------------------------------------------------------ styles

        private static RoomStyle StyleOf(Room room)
        {
            Material checkerFloor = BuildKit.Surface("floorChecker", () => ProceduralTextures.Tiles(new Color(0.82f, 0.8f, 0.74f), new Color(0.07f, 0.07f, 0.08f), 8, true, 0.35f), 0.45f);
            Material redBand = BuildKit.Surface("wainscotRed", () => ProceduralTextures.Tiles(new Color(0.55f, 0.07f, 0.08f), new Color(0.08f, 0.08f, 0.09f), 8, true), 0.3f);
            Material ceiling = BuildKit.Surface("ceiling", () => ProceduralTextures.CeilingTiles(), 0.05f);
            switch (room)
            {
                case Room.Kitchen:
                    Material white = BuildKit.Surface("kitchenTiles", () => ProceduralTextures.Tiles(new Color(0.85f, 0.86f, 0.84f), Color.white, 16, false, 0.45f), 0.6f);
                    return new RoomStyle
                    {
                        Floor = BuildKit.Surface("kitchenFloor", () => ProceduralTextures.Tiles(new Color(0.6f, 0.1f, 0.1f), new Color(0.85f, 0.83f, 0.78f), 8, true, 0.5f), 0.4f),
                        Wainscot = white, Wall = white, Ceiling = ceiling
                    };
                case Room.Storage:
                    Material concrete = BuildKit.Surface("concrete", () => ProceduralTextures.Concrete(new Color(0.42f, 0.42f, 0.4f)), 0.1f);
                    return new RoomStyle { Floor = concrete, Wainscot = concrete, Wall = BuildKit.Surface("concreteWall", () => ProceduralTextures.Concrete(new Color(0.36f, 0.37f, 0.38f)), 0.05f), Ceiling = concrete };
                case Room.Arcade:
                    return new RoomStyle
                    {
                        Floor = BuildKit.Surface("carpet", () => ProceduralTextures.Carpet(), 0.05f),
                        Wainscot = BuildKit.Surface("arcadeBand", () => ProceduralTextures.Tiles(new Color(0.12f, 0.08f, 0.22f), new Color(0.05f, 0.05f, 0.08f), 8, true), 0.3f),
                        Wall = BuildKit.Surface("arcadeWall", () => ProceduralTextures.Wallpaper(new Color(0.1f, 0.08f, 0.16f), new Color(0.2f, 0.12f, 0.32f)), 0.1f),
                        Ceiling = ceiling
                    };
                case Room.Stage:
                    return new RoomStyle
                    {
                        Floor = BuildKit.Surface("planks", () => ProceduralTextures.Planks(new Color(0.32f, 0.2f, 0.12f)), 0.35f),
                        Wainscot = redBand,
                        Wall = BuildKit.Surface("stageWall", () => ProceduralTextures.Wallpaper(new Color(0.18f, 0.06f, 0.14f), new Color(0.3f, 0.1f, 0.22f)), 0.1f),
                        Ceiling = BuildKit.Plain("stageCeiling", new Color(0.05f, 0.04f, 0.05f))
                    };
                case Room.Office:
                    return new RoomStyle
                    {
                        Floor = BuildKit.Surface("officeFloor", () => ProceduralTextures.Tiles(new Color(0.45f, 0.43f, 0.38f), new Color(0.35f, 0.33f, 0.3f), 4, true, 0.4f), 0.3f),
                        Wainscot = BuildKit.Surface("officeBand", () => ProceduralTextures.Grimy(new Color(0.25f, 0.28f, 0.3f)), 0.2f),
                        Wall = BuildKit.Surface("officeWall", () => ProceduralTextures.Wallpaper(new Color(0.42f, 0.42f, 0.36f), new Color(0.36f, 0.38f, 0.32f)), 0.1f),
                        Ceiling = ceiling
                    };
                default:
                    return new RoomStyle
                    {
                        Floor = checkerFloor,
                        Wainscot = redBand,
                        Wall = BuildKit.Surface("wallpaper", () => ProceduralTextures.Wallpaper(new Color(0.45f, 0.4f, 0.32f), new Color(0.55f, 0.18f, 0.15f)), 0.1f),
                        Ceiling = ceiling
                    };
            }
        }

        // ------------------------------------------------------------------ shell

        private static void BuildShell(Room room, Transform root, RoomStyle style)
        {
            Rect area = PizzeriaLayout.Area(room);
            float height = PizzeriaLayout.Height(room);
            var center = new Vector3(area.center.x, 0f, area.center.y);

            BuildKit.Box("Floor", root, center + new Vector3(0f, -0.05f, 0f), new Vector3(area.width, 0.1f, area.height), style.Floor, 2f);
            BuildKit.Box("Ceiling", root, center + new Vector3(0f, height + 0.05f, 0f), new Vector3(area.width, 0.1f, area.height), style.Ceiling, 2.5f, false);

            var doors = new List<Vector2>(PizzeriaLayout.DoorwaysOf(room));
            if (room == HuntMap.Exit)
            {
                doors.Add(new Vector2(PizzeriaLayout.ExitDoor.x, PizzeriaLayout.ExitDoor.z));
            }

            float t = PizzeriaLayout.WallThickness;
            // North and south walls run along x; west and east along z. Each sits just inside the room.
            Side(root, style, height, doors, area.xMin, area.xMax, area.yMax - t * 0.5f, true, area.yMax);
            Side(root, style, height, doors, area.xMin, area.xMax, area.yMin + t * 0.5f, true, area.yMin);
            Side(root, style, height, doors, area.yMin, area.yMax, area.xMin + t * 0.5f, false, area.xMin);
            Side(root, style, height, doors, area.yMin, area.yMax, area.xMax - t * 0.5f, false, area.xMax);
        }

        /// <summary>One wall from <paramref name="from"/> to <paramref name="to"/> with openings at the doorways on it.</summary>
        private static void Side(Transform root, RoomStyle style, float height, List<Vector2> doors,
                                 float from, float to, float at, bool alongX, float boundary)
        {
            var openings = new List<float>();
            foreach (Vector2 door in doors)
            {
                float across = alongX ? door.y : door.x;
                float along = alongX ? door.x : door.y;
                if (Mathf.Abs(across - boundary) < 0.05f && along > from && along < to)
                {
                    openings.Add(along);
                }
            }
            openings.Sort();

            float start = from;
            foreach (float opening in openings)
            {
                float doorStart = opening - PizzeriaLayout.DoorWidth * 0.5f;
                WallRun(root, style, height, start, doorStart, at, alongX);
                Doorway(root, style, height, opening, at, alongX);
                start = opening + PizzeriaLayout.DoorWidth * 0.5f;
            }
            WallRun(root, style, height, start, to, at, alongX);
        }

        private static void WallRun(Transform root, RoomStyle style, float height, float from, float to, float at, bool alongX)
        {
            float length = to - from;
            if (length <= 0.01f)
            {
                return;
            }
            float mid = (from + to) * 0.5f;
            float t = PizzeriaLayout.WallThickness;
            Vector3 Position(float y) => alongX ? new Vector3(mid, y, at) : new Vector3(at, y, mid);
            Vector3 Size(float h) => alongX ? new Vector3(length, h, t) : new Vector3(t, h, length);

            BuildKit.Box("Wainscot", root, Position(WainscotHeight * 0.5f), Size(WainscotHeight), style.Wainscot, 1f);
            BuildKit.Box("Wall", root, Position(WainscotHeight + (height - WainscotHeight) * 0.5f), Size(height - WainscotHeight), style.Wall, 1.6f);
            // Chair rail and baseboard catch the light and break the flat walls.
            Vector3 rail = alongX ? new Vector3(length, 0.08f, t + 0.06f) : new Vector3(t + 0.06f, 0.08f, length);
            BuildKit.Box("Rail", root, Position(WainscotHeight), rail, BuildKit.Plain("trim", new Color(0.16f, 0.1f, 0.07f), 0.4f), 1f, false);
            Vector3 baseboard = alongX ? new Vector3(length, 0.14f, t + 0.04f) : new Vector3(t + 0.04f, 0.14f, length);
            BuildKit.Box("Baseboard", root, Position(0.07f), baseboard, BuildKit.Plain("baseboard", new Color(0.06f, 0.05f, 0.05f), 0.3f), 1f, false);
        }

        private static void Doorway(Transform root, RoomStyle style, float height, float along, float at, bool alongX)
        {
            float t = PizzeriaLayout.WallThickness;
            float lintel = height - PizzeriaLayout.DoorHeight;
            Vector3 Position(float offset, float y) => alongX ? new Vector3(along + offset, y, at) : new Vector3(at, y, along + offset);
            Vector3 Size(float length, float h, float depth) => alongX ? new Vector3(length, h, depth) : new Vector3(depth, h, length);

            if (lintel > 0.01f)
            {
                BuildKit.Box("Lintel", root, Position(0f, PizzeriaLayout.DoorHeight + lintel * 0.5f), Size(PizzeriaLayout.DoorWidth, lintel, t), style.Wall, 1.6f);
            }
            Material frame = BuildKit.Plain("doorFrame", new Color(0.12f, 0.08f, 0.06f), 0.35f);
            float half = PizzeriaLayout.DoorWidth * 0.5f;
            BuildKit.Box("Frame", root, Position(-half + 0.05f, PizzeriaLayout.DoorHeight * 0.5f), Size(0.1f, PizzeriaLayout.DoorHeight, t + 0.1f), frame, 1f, false);
            BuildKit.Box("Frame", root, Position(half - 0.05f, PizzeriaLayout.DoorHeight * 0.5f), Size(0.1f, PizzeriaLayout.DoorHeight, t + 0.1f), frame, 1f, false);
            BuildKit.Box("Frame", root, Position(0f, PizzeriaLayout.DoorHeight - 0.05f), Size(PizzeriaLayout.DoorWidth, 0.1f, t + 0.1f), frame, 1f, false);
        }

        private static void BuildCorridors(Transform root)
        {
            Transform corridors = new GameObject("Corridors").transform;
            corridors.SetParent(root, false);
            Material floor = BuildKit.Surface("floorChecker", null);
            Material wall = BuildKit.Surface("wallpaper", null);
            Material band = BuildKit.Surface("wainscotRed", null);
            Material ceiling = BuildKit.Surface("ceiling", null);
            var style = new RoomStyle { Floor = floor, Wall = wall, Wainscot = band, Ceiling = ceiling };

            foreach ((Vector2 start, Vector2 end, float height) in PizzeriaLayout.Corridors())
            {
                Vector2 mid = (start + end) * 0.5f;
                float length = Vector2.Distance(start, end);
                bool alongX = Mathf.Abs(end.x - start.x) > Mathf.Abs(end.y - start.y);
                float width = PizzeriaLayout.CorridorWidth;
                Vector3 size = alongX ? new Vector3(length, 0.1f, width) : new Vector3(width, 0.1f, length);
                BuildKit.Box("CorridorFloor", corridors, new Vector3(mid.x, -0.05f, mid.y), size, floor, 2f);
                BuildKit.Box("CorridorCeiling", corridors, new Vector3(mid.x, height + 0.05f, mid.y), size, ceiling, 2.5f, false);
                float from = alongX ? Mathf.Min(start.x, end.x) : Mathf.Min(start.y, end.y);
                float to = alongX ? Mathf.Max(start.x, end.x) : Mathf.Max(start.y, end.y);
                float side = width * 0.5f + PizzeriaLayout.WallThickness * 0.5f;
                float across = alongX ? mid.y : mid.x;
                WallRun(corridors, style, height, from, to, across - side, alongX);
                WallRun(corridors, style, height, from, to, across + side, alongX);
            }
        }

        // ------------------------------------------------------------------ light

        private static List<FlickerLight> BuildLamps(Room room, Transform root, System.Random random)
        {
            var lamps = new List<FlickerLight>();
            Rect area = PizzeriaLayout.Area(room);
            float height = PizzeriaLayout.Height(room);
            bool hall = area.height < 4f || area.width < 4f;
            int columns = Mathf.Max(1, Mathf.RoundToInt(area.width / 7f));
            int rows = Mathf.Max(1, Mathf.RoundToInt(area.height / 7f));
            Color warm = room == HuntMap.Exit ? new Color(1f, 0.55f, 0.45f) : new Color(1f, 0.8f, 0.58f);
            bool shadows = room == Room.DiningHall || room == Room.Stage || room == Room.Office;

            for (int c = 0; c < columns; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    var position = new Vector3(area.xMin + area.width * (c + 0.5f) / columns, height, area.yMin + area.height * (r + 0.5f) / rows);
                    double roll = random.NextDouble();
                    bool dead = roll < 0.18 && columns * rows > 1;
                    float flicker = roll < 0.45 ? 0.6f : 0.15f;
                    lamps.Add(Lamp(root, position, warm, dead, flicker, shadows && (c + r) % 2 == 0, hall ? 0.8f : 1f));
                }
            }

            if (room == Room.WestHall || room == Room.EastHall)
            {
                // A red emergency light at one end of each hall.
                var emergency = new GameObject("EmergencyLight").AddComponent<Light>();
                emergency.transform.SetParent(root, false);
                emergency.transform.localPosition = new Vector3(area.xMax - 1f, height - 0.3f, area.center.y);
                emergency.type = LightType.Point;
                emergency.range = 7f;
                emergency.intensity = 1.6f;
                emergency.color = new Color(1f, 0.08f, 0.05f);
                BuildKit.Box("EmergencyLamp", root, emergency.transform.localPosition + Vector3.up * 0.2f, new Vector3(0.3f, 0.15f, 0.2f), BuildKit.Glow("emergency", new Color(1f, 0.1f, 0.05f), 3f), 1f, false);
            }
            return lamps;
        }

        /// <summary>A ceiling lamp: fixture, downward spot light, a faint light shaft and dust.</summary>
        private static FlickerLight Lamp(Transform root, Vector3 ceilingPoint, Color color, bool dead, float flickerChance, bool shadows, float strength)
        {
            var fixture = BuildKit.Box("Lamp", root, ceilingPoint - Vector3.up * 0.06f, new Vector3(1.3f, 0.08f, 0.35f),
                                       dead ? BuildKit.Plain("lampDead", new Color(0.3f, 0.3f, 0.28f), 0.5f) : BuildKit.Glow("lampOn", color, 2.2f), 1f, false);
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.transform.localPosition = ceilingPoint - Vector3.up * 0.25f;
            light.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            light.type = LightType.Spot;
            light.spotAngle = 140f;
            light.innerSpotAngle = 50f;
            light.range = ceilingPoint.y + 6f;
            light.intensity = (dead ? 0f : 8f) * strength;
            light.color = color;
            light.shadows = shadows && !dead ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.85f;

            GameObject shaftObject = null;
            if (!dead)
            {
                // Dust floating in the light under the lamp.
                Dust(root, ceilingPoint - Vector3.up * (ceilingPoint.y * 0.5f), ceilingPoint.y);
            }
            return FlickerLight.Attach(light, fixture.GetComponent<Renderer>(), dead ? 0f : flickerChance, shaftObject);
        }

        private static Texture2D shaftTexture;

        private static Texture2D ShaftTexture()
        {
            if (shaftTexture != null)
            {
                return shaftTexture;
            }
            // Bright near the lamp (v = 1), fading to nothing at the floor.
            shaftTexture = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float v = y / 63f;
                for (int x = 0; x < 4; x++)
                {
                    shaftTexture.SetPixel(x, y, new Color(1f, 1f, 1f, v * v));
                }
            }
            shaftTexture.Apply();
            return shaftTexture;
        }

        private static void Dust(Transform root, Vector3 center, float height)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = 9f;
            main.startSpeed = 0.03f;
            main.startSize = 0.012f;
            main.startColor = new Color(1f, 0.9f, 0.7f, 0.25f);
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 3f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.5f, height * 0.8f, 2.5f);
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.05f;
            noise.frequency = 0.3f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = BuildKit.Additive("dust", ProceduralTextures.Disc(16), new Color(1f, 0.9f, 0.75f, 0.6f));
            particles.Play();
        }

        // ------------------------------------------------------------------ hiding spots, fuses, exit

        private static HidingSpot BuildSpot(Room room, int index, Transform roomRoot)
        {
            SpotKind kind = PizzeriaLayout.SpotKindOf(room, index);
            var go = new GameObject("Spot " + index + " (" + kind + ")");
            go.transform.SetParent(roomRoot, false);
            go.transform.position = PizzeriaLayout.SpotPosition(room, index);
            go.transform.rotation = PizzeriaLayout.SpotFacing(room, index);
            Transform t = go.transform;
            Transform door;
            Vector3 hide;

            switch (kind)
            {
                case SpotKind.Locker:
                    Material locker = BuildKit.Surface("locker", () => ProceduralTextures.Grimy(new Color(0.2f, 0.3f, 0.34f), 1f), 0.4f, 0.5f);
                    BuildKit.Box("Back", t, new Vector3(0f, 1.0f, -0.3f), new Vector3(0.9f, 2.0f, 0.05f), locker);
                    BuildKit.Box("Left", t, new Vector3(-0.43f, 1.0f, 0f), new Vector3(0.05f, 2.0f, 0.6f), locker);
                    BuildKit.Box("Right", t, new Vector3(0.43f, 1.0f, 0f), new Vector3(0.05f, 2.0f, 0.6f), locker);
                    BuildKit.Box("Top", t, new Vector3(0f, 2.0f, 0f), new Vector3(0.9f, 0.05f, 0.6f), locker);
                    door = new GameObject("Hinge").transform;
                    door.SetParent(t, false);
                    door.localPosition = new Vector3(-0.43f, 0f, 0.3f);
                    // The door has wide vents at eye height: from inside you watch the room through them.
                    BuildKit.Box("DoorLow", door, new Vector3(0.43f, 0.66f, 0f), new Vector3(0.86f, 1.28f, 0.04f), locker);
                    BuildKit.Box("DoorHigh", door, new Vector3(0.43f, 1.92f, 0f), new Vector3(0.86f, 0.12f, 0.04f), locker);
                    BuildKit.Box("VentLeft", door, new Vector3(0.05f, 1.58f, 0f), new Vector3(0.1f, 0.56f, 0.04f), locker);
                    BuildKit.Box("VentRight", door, new Vector3(0.81f, 1.58f, 0f), new Vector3(0.1f, 0.56f, 0.04f), locker);
                    for (int slat = 0; slat < 7; slat++)
                    {
                        var piece = BuildKit.Box("Slat", door, new Vector3(0.43f, 1.33f + slat * 0.08f, 0f), new Vector3(0.66f, 0.018f, 0.045f), locker, 1f, false);
                        piece.transform.localRotation = Quaternion.Euler(-25f, 0f, 0f);
                    }
                    BuildKit.Box("Handle", door, new Vector3(0.78f, 1.0f, 0.04f), new Vector3(0.03f, 0.18f, 0.04f), BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f), 1f, false);
                    // Eyes right behind the vents.
                    hide = new Vector3(0f, 1.6f, 0.17f);
                    break;
                case SpotKind.Cabinet:
                    Material wood = BuildKit.Surface("cabinetWood", () => ProceduralTextures.Planks(new Color(0.38f, 0.25f, 0.15f)), 0.3f);
                    BuildKit.Box("Base", t, new Vector3(0f, 0.08f, -0.05f), new Vector3(1.4f, 0.16f, 0.7f), wood);
                    BuildKit.Box("Top", t, new Vector3(0f, 0.92f, -0.05f), new Vector3(1.45f, 0.06f, 0.75f), wood);
                    BuildKit.Box("Back", t, new Vector3(0f, 0.5f, -0.38f), new Vector3(1.4f, 0.9f, 0.05f), wood);
                    BuildKit.Box("Left", t, new Vector3(-0.68f, 0.5f, -0.05f), new Vector3(0.05f, 0.9f, 0.7f), wood);
                    BuildKit.Box("Right", t, new Vector3(0.68f, 0.5f, -0.05f), new Vector3(0.05f, 0.9f, 0.7f), wood);
                    door = new GameObject("Hinge").transform;
                    door.SetParent(t, false);
                    door.localPosition = new Vector3(-0.68f, 0f, 0.28f);
                    BuildKit.Box("Door", door, new Vector3(0.68f, 0.5f, 0f), new Vector3(1.34f, 0.78f, 0.04f), wood);
                    hide = new Vector3(0f, 0.55f, 0.12f);
                    break;
                default:
                    Material cloth = BuildKit.Surface("tablecloth", () => ProceduralTextures.Tiles(new Color(0.7f, 0.1f, 0.12f), new Color(0.88f, 0.85f, 0.78f), 6, true, 0.4f), 0.05f);
                    BuildKit.Box("Top", t, new Vector3(0f, 0.8f, 0f), new Vector3(1.6f, 0.06f, 1.1f), cloth);
                    BuildKit.Box("ClothBack", t, new Vector3(0f, 0.42f, -0.54f), new Vector3(1.6f, 0.76f, 0.02f), cloth);
                    BuildKit.Box("ClothLeft", t, new Vector3(-0.79f, 0.42f, 0f), new Vector3(0.02f, 0.76f, 1.1f), cloth);
                    BuildKit.Box("ClothRight", t, new Vector3(0.79f, 0.42f, 0f), new Vector3(0.02f, 0.76f, 1.1f), cloth);
                    door = new GameObject("Hinge").transform;
                    door.SetParent(t, false);
                    door.localPosition = new Vector3(-0.79f, 0f, 0.54f);
                    BuildKit.Box("ClothFront", door, new Vector3(0.79f, 0.42f, 0f), new Vector3(1.58f, 0.76f, 0.02f), cloth);
                    LevelProps.PartySetting(t, new Vector3(0f, 0.83f, 0f));
                    hide = new Vector3(0f, 0.45f, 0.3f);
                    break;
            }
            return HidingSpot.Attach(go, room, index, kind, door, hide, new Vector3(0f, 0f, 1.1f));
        }

        private static FusePickup BuildFuse(Room room, Transform roomRoot)
        {
            Vector3 position = PizzeriaLayout.FusePosition(room);
            Quaternion facing = PizzeriaLayout.FuseFacing(room);
            Material metal = BuildKit.Plain("metal", new Color(0.45f, 0.47f, 0.5f), 0.5f, 0.7f);
            var shelf = BuildKit.Box("FuseShelf", roomRoot, position - Vector3.up * 0.2f + facing * Vector3.forward * 0.1f, new Vector3(0.6f, 0.05f, 0.35f), metal);
            shelf.transform.rotation = facing;
            // A fuse box on the wall above the shelf, with a warning stripe.
            var box = BuildKit.Box("FuseBox", roomRoot, position + Vector3.up * 0.55f - facing * Vector3.forward * 0.02f, new Vector3(0.5f, 0.6f, 0.15f), BuildKit.Surface("fuseBox", () => ProceduralTextures.Grimy(new Color(0.5f, 0.45f, 0.2f), 1f), 0.3f, 0.3f), 1f, false);
            box.transform.rotation = facing;

            var fuse = new GameObject("Fuse");
            fuse.transform.SetParent(roomRoot, false);
            fuse.transform.position = position;
            BuildKit.Box("Body", fuse.transform, Vector3.zero, new Vector3(0.12f, 0.22f, 0.12f), BuildKit.Glow("fuse", new Color(1f, 0.72f, 0.15f), 2.5f), 1f, false);
            BuildKit.Box("CapTop", fuse.transform, new Vector3(0f, 0.13f, 0f), new Vector3(0.14f, 0.04f, 0.14f), metal, 1f, false);
            BuildKit.Box("CapBottom", fuse.transform, new Vector3(0f, -0.13f, 0f), new Vector3(0.14f, 0.04f, 0.14f), metal, 1f, false);
            var collider = fuse.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.4f, 0.5f, 0.4f);
            collider.isTrigger = true;

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(fuse.transform, false);
            glow.type = LightType.Point;
            glow.range = 2.5f;
            glow.color = new Color(1f, 0.75f, 0.2f);
            glow.intensity = 0.8f;
            FusePickup pickup = FusePickup.Attach(fuse, room, glow);
            fuse.SetActive(false);
            return pickup;
        }

        private static ExitPanel BuildExit(Transform root)
        {
            Vector3 door = PizzeriaLayout.ExitDoor;
            // The front doors of an old family restaurant: two glass doors in red frames, a brass
            // push bar, the logo on the glass and a CLOSED sign hanging inside. They swing out
            // when the power comes back.
            Material frameRed = BuildKit.Plain("frontDoorFrame", new Color(0.45f, 0.05f, 0.05f), 0.55f, 0.3f);
            Material glass = BuildKit.Stain("frontDoorGlass", Texture2D.whiteTexture, new Color(0.08f, 0.1f, 0.12f, 0.42f));
            Material brass = BuildKit.Plain("brass", new Color(0.62f, 0.48f, 0.24f), 0.75f, 1f);
            Material logo = BuildKit.Picture("doorLogo", ProceduralTextures.Sign("FREDDY'S", new Color(1f, 0.85f, 0.4f), new Color(0.35f, 0.04f, 0.04f)), 0f);
            Material closed = BuildKit.Picture("closedSign", ProceduralTextures.Sign("CLOSED", new Color(0.8f, 0.05f, 0.05f), new Color(0.92f, 0.9f, 0.84f)), 0f);
            float leafWidth = PizzeriaLayout.DoorWidth * 0.5f;
            float height = PizzeriaLayout.DoorHeight;
            var hinges = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                hinges[i] = new GameObject("FrontDoorHinge").transform;
                hinges[i].SetParent(root, false);
                hinges[i].localPosition = new Vector3(door.x + side * leafWidth, 0f, door.z);
                Transform h = hinges[i];
                float c = -side * leafWidth * 0.5f;   // leaf center, from the hinge
                BuildKit.Box("StileHinge", h, new Vector3(-side * 0.05f, height * 0.5f, 0f), new Vector3(0.1f, height, 0.07f), frameRed);
                BuildKit.Box("StileLock", h, new Vector3(-side * (leafWidth - 0.05f), height * 0.5f, 0f), new Vector3(0.1f, height, 0.07f), frameRed);
                BuildKit.Box("RailTop", h, new Vector3(c, height - 0.06f, 0f), new Vector3(leafWidth, 0.12f, 0.07f), frameRed);
                BuildKit.Box("RailBottom", h, new Vector3(c, 0.16f, 0f), new Vector3(leafWidth, 0.32f, 0.07f), frameRed);
                BuildKit.Box("Glass", h, new Vector3(c, height * 0.5f + 0.1f, 0f), new Vector3(leafWidth - 0.2f, height - 0.44f, 0.02f), glass);
                BuildKit.Box("PushBar", h, new Vector3(c, 1.05f, 0f), new Vector3(leafWidth - 0.3f, 0.05f, 0.16f), brass, 1f, false);
                BuildKit.Quad("LogoOut", h, new Vector3(c, 1.62f, -0.02f), new Vector2(0.62f, 0.12f), 180f, logo);
                BuildKit.Quad("LogoIn", h, new Vector3(c, 1.62f, 0.02f), new Vector2(0.62f, 0.12f), 0f, logo);
                if (i == 1)
                {
                    BuildKit.Quad("ClosedIn", h, new Vector3(c, 1.95f, 0.05f), new Vector2(0.5f, 0.16f), 0f, closed);
                    BuildKit.Quad("ClosedOut", h, new Vector3(c, 1.95f, 0.045f), new Vector2(0.5f, 0.16f), 180f, closed);
                }
            }

            var sign = BuildKit.Quad("ExitSign", root, new Vector3(door.x, PizzeriaLayout.DoorHeight + 0.35f, door.z + 0.13f), new Vector2(1.2f, 0.35f), 0f,
                                     BuildKit.Picture("exitSign", ProceduralTextures.Sign("EXIT", new Color(1f, 0.15f, 0.1f), new Color(0.08f, 0f, 0f)), 3f));
            var signLight = new GameObject("SignLight").AddComponent<Light>();
            signLight.transform.SetParent(sign.transform, false);
            signLight.transform.localPosition = new Vector3(0f, 0f, -1f);
            signLight.type = LightType.Point;
            signLight.range = 5f;
            signLight.intensity = 1.5f;
            signLight.color = new Color(1f, 0.12f, 0.08f);

            var panel = BuildKit.Box("ExitPanel", root, new Vector3(door.x + 2.2f, 1.35f, door.z + 0.15f), new Vector3(0.5f, 0.65f, 0.12f),
                                     BuildKit.Surface("fuseBox", null), 1f);
            var lamp = BuildKit.Box("PanelLamp", panel.transform, new Vector3(0f, 0.22f, 0.08f), new Vector3(0.3f, 0.08f, 0.04f), BuildKit.Glow("panelLamp", new Color(0.7f, 0f, 0f), 2f), 1f, false);
            lamp.GetComponent<Renderer>().material = new Material(lamp.GetComponent<Renderer>().sharedMaterial);
            for (int i = 0; i < 3; i++)
            {
                BuildKit.Box("Socket", panel.transform, new Vector3(-0.15f + i * 0.15f, -0.05f, 0.07f), new Vector3(0.08f, 0.16f, 0.03f), BuildKit.Plain("black", Color.black), 1f, false);
            }
            return ExitPanel.Attach(panel, hinges, lamp.GetComponent<Renderer>());
        }
    }
}
