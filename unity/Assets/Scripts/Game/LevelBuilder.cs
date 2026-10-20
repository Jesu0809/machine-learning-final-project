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
        public readonly List<VentGrate> Vents = new List<VentGrate>();

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
                LevelProps.BeginRoom();
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
            BuildCorridors(level, random);
            BuildDucts(level);
            level.Exit = BuildExit(level.Root);
            return level;
        }

        // ------------------------------------------------------------------ styles

        private static RoomStyle StyleOf(Room room)
        {
            Material checkerFloor = BuildKit.Surface("floorChecker", () => ProceduralTextures.Tiles(new Color(0.82f, 0.8f, 0.74f), new Color(0.07f, 0.07f, 0.08f), 8, true, 0.35f), 0.45f);
            Material redBand = BuildKit.Surface("wainscotRed", () => ProceduralTextures.Tiles(new Color(0.55f, 0.07f, 0.08f), new Color(0.08f, 0.08f, 0.09f), 8, true), 0.3f);
            Material ceiling = BuildKit.Surface("ceiling", () => ProceduralTextures.CeilingTiles(), 0.05f);
            Material officeFloor = BuildKit.Surface("officeFloor", () => ProceduralTextures.Tiles(new Color(0.45f, 0.43f, 0.38f), new Color(0.35f, 0.33f, 0.3f), 4, true, 0.4f), 0.3f);
            Material officeBand = BuildKit.Surface("officeBand", () => ProceduralTextures.Grimy(new Color(0.25f, 0.28f, 0.3f)), 0.2f);
            Material concrete = BuildKit.Surface("concrete", () => ProceduralTextures.Concrete(new Color(0.42f, 0.42f, 0.4f)), 0.1f);
            Material concreteWall = BuildKit.Surface("concreteWall", () => ProceduralTextures.Concrete(new Color(0.36f, 0.37f, 0.38f)), 0.05f);
            switch (room)
            {
                case Room.Kitchen:
                    Material white = BuildKit.Surface("kitchenTiles", () => ProceduralTextures.Tiles(new Color(0.85f, 0.86f, 0.84f), Color.white, 16, false, 0.45f), 0.6f);
                    return new RoomStyle
                    {
                        Floor = BuildKit.Surface("kitchenFloor", () => ProceduralTextures.Tiles(new Color(0.6f, 0.1f, 0.1f), new Color(0.85f, 0.83f, 0.78f), 8, true, 0.5f), 0.4f),
                        Wainscot = white, Wall = white, Ceiling = ceiling
                    };
                case Room.Restrooms:
                    Material blue = BuildKit.Surface("restroomTiles", () => ProceduralTextures.Tiles(new Color(0.55f, 0.68f, 0.72f), new Color(0.8f, 0.84f, 0.84f), 16, false, 0.4f), 0.6f);
                    return new RoomStyle
                    {
                        Floor = BuildKit.Surface("restroomFloor", () => ProceduralTextures.Tiles(new Color(0.75f, 0.76f, 0.74f), new Color(0.3f, 0.4f, 0.45f), 12, true, 0.5f), 0.5f),
                        Wainscot = blue, Wall = BuildKit.Surface("restroomWall", () => ProceduralTextures.Grimy(new Color(0.62f, 0.66f, 0.64f), 0.8f), 0.2f), Ceiling = ceiling
                    };
                case Room.Storage:
                    return new RoomStyle { Floor = concrete, Wainscot = concrete, Wall = concreteWall, Ceiling = concrete };
                case Room.Parts:
                    // Staff-only workroom: bare concrete, steel-grey walls.
                    return new RoomStyle
                    {
                        Floor = concrete,
                        Wainscot = BuildKit.Surface("workBand", () => ProceduralTextures.Grimy(new Color(0.3f, 0.32f, 0.3f), 1.2f), 0.25f, 0.2f),
                        Wall = concreteWall,
                        Ceiling = concrete
                    };
                case Room.Arcade:
                    return new RoomStyle
                    {
                        Floor = BuildKit.Surface("carpet", () => ProceduralTextures.Carpet(), 0.05f),
                        Wainscot = BuildKit.Surface("arcadeBand", () => ProceduralTextures.Tiles(new Color(0.12f, 0.08f, 0.22f), new Color(0.05f, 0.05f, 0.08f), 8, true), 0.3f),
                        Wall = BuildKit.Surface("arcadeWall", () => ProceduralTextures.Wallpaper(new Color(0.1f, 0.08f, 0.16f), new Color(0.2f, 0.12f, 0.32f)), 0.1f),
                        Ceiling = ceiling
                    };
                case Room.Office:
                case Room.BackHall:
                    // The staff wing: grey-green offices and halls, so it never looks like the restaurant.
                    return new RoomStyle
                    {
                        Floor = officeFloor,
                        Wainscot = officeBand,
                        Wall = BuildKit.Surface("officeWall", () => ProceduralTextures.Wallpaper(new Color(0.42f, 0.42f, 0.36f), new Color(0.36f, 0.38f, 0.32f)), 0.1f),
                        Ceiling = ceiling
                    };
                default:
                    // The public wing: checkered floor, red band, party wallpaper.
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
                WallRun(root, style, height, start, opening - PizzeriaLayout.DoorWidth * 0.5f, at, alongX);
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
            float top = PizzeriaLayout.DoorHeight;
            float lintel = height - top;
            Vector3 Position(float offset, float y) => alongX ? new Vector3(along + offset, y, at) : new Vector3(at, y, along + offset);
            Vector3 Size(float length, float h, float depth) => alongX ? new Vector3(length, h, depth) : new Vector3(depth, h, length);

            if (lintel > 0.01f)
            {
                BuildKit.Box("Lintel", root, Position(0f, top + lintel * 0.5f), Size(PizzeriaLayout.DoorWidth, lintel, t), style.Wall, 1.6f);
            }
            Material frame = BuildKit.Plain("doorFrame", new Color(0.12f, 0.08f, 0.06f), 0.35f);
            float half = PizzeriaLayout.DoorWidth * 0.5f;
            float mid = PizzeriaLayout.DoorHeight * 0.5f;
            BuildKit.Box("Frame", root, Position(-half + 0.05f, mid), Size(0.1f, PizzeriaLayout.DoorHeight, t + 0.1f), frame, 1f, false);
            BuildKit.Box("Frame", root, Position(half - 0.05f, mid), Size(0.1f, PizzeriaLayout.DoorHeight, t + 0.1f), frame, 1f, false);
            BuildKit.Box("Frame", root, Position(0f, top - 0.05f), Size(PizzeriaLayout.DoorWidth, 0.1f, t + 0.1f), frame, 1f, false);
        }

        /// <summary>
        /// The corridors between doorways, dressed like the wing they start in, with a ceiling lamp
        /// in the long ones (it goes dark with the room the corridor starts from).
        /// </summary>
        private static void BuildCorridors(Level level, System.Random random)
        {
            Transform corridors = new GameObject("Corridors").transform;
            corridors.SetParent(level.Root, false);

            foreach ((Vector3 start3, Vector3 end3, float height) in PizzeriaLayout.Corridors())
            {
                Room owner = PizzeriaLayout.RoomAt(start3);
                RoomStyle style = StyleOf(HuntMap.IsStaffWing(owner) ? Room.BackHall : Room.MainHall);
                Material floor = style.Floor;
                Material ceiling = style.Ceiling;
                Transform segment = new GameObject("Corridor").transform;
                segment.SetParent(corridors, false);
                var start = new Vector2(start3.x, start3.z);
                var end = new Vector2(end3.x, end3.z);
                Vector2 mid = (start + end) * 0.5f;
                float length = Vector2.Distance(start, end);
                bool alongX = Mathf.Abs(end.x - start.x) > Mathf.Abs(end.y - start.y);
                float width = PizzeriaLayout.CorridorWidth;
                Vector3 size = alongX ? new Vector3(length, 0.1f, width) : new Vector3(width, 0.1f, length);
                BuildKit.Box("CorridorFloor", segment, new Vector3(mid.x, -0.05f, mid.y), size, floor, 2f);
                BuildKit.Box("CorridorCeiling", segment, new Vector3(mid.x, height + 0.05f, mid.y), size, ceiling, 2.5f, false);
                float from = alongX ? Mathf.Min(start.x, end.x) : Mathf.Min(start.y, end.y);
                float to = alongX ? Mathf.Max(start.x, end.x) : Mathf.Max(start.y, end.y);
                float side = width * 0.5f + PizzeriaLayout.WallThickness * 0.5f;
                float across = alongX ? mid.y : mid.x;
                WallRun(segment, style, height, from, to, across - side, alongX);
                WallRun(segment, style, height, from, to, across + side, alongX);
                if (length > 4f)
                {
                    bool dead = random.NextDouble() < 0.3;
                    level.Lamps[(int)owner].Add(Lamp(segment, new Vector3(mid.x, height, mid.y), new Color(1f, 0.8f, 0.58f), dead, 0.5f, false, 0.7f));
                }
            }
        }

        // ------------------------------------------------------------------ air ducts

        /// <summary>
        /// The air ducts: square metal tubes along each path (segments plus closed corner boxes),
        /// with a grate at each end that the player can crawl into.
        /// </summary>
        private static void BuildDucts(Level level)
        {
            Transform parent = new GameObject("Ducts").transform;
            parent.SetParent(level.Root, false);
            Material metal = BuildKit.Surface("ductInside", () => ProceduralTextures.Grimy(new Color(0.42f, 0.44f, 0.46f), 2.5f), 0.55f, 0.7f);
            const float size = PizzeriaLayout.DuctSize;
            const float skin = 0.06f;

            foreach (DuctPath duct in PizzeriaLayout.Ducts)
            {
                Vector3[] p = duct.Points;
                for (int i = 0; i + 1 < p.Length; i++)
                {
                    Vector3 a = p[i];
                    Vector3 b = p[i + 1];
                    Vector3 axis = (b - a).normalized;
                    // Interior joints get their own corner box, so segments stop half a duct short of them.
                    if (i > 0)
                    {
                        a += axis * (size * 0.5f);
                    }
                    if (i + 2 < p.Length)
                    {
                        b -= axis * (size * 0.5f);
                    }
                    float length = Vector3.Distance(a, b);
                    if (length < 0.01f)
                    {
                        continue;
                    }
                    Vector3 center = (a + b) * 0.5f;
                    foreach (Vector3 normal in Perpendiculars(axis))
                    {
                        Vector3 slab = Abs(axis) * length + (Vector3.one - Abs(axis) - Abs(normal)) * size + Abs(normal) * skin;
                        BuildKit.Box("DuctWall", parent, center + normal * (size * 0.5f), slab, metal, 1f, false);
                    }
                }
                for (int i = 1; i + 1 < p.Length; i++)
                {
                    Vector3 comeFrom = (p[i - 1] - p[i]).normalized;
                    Vector3 goTo = (p[i + 1] - p[i]).normalized;
                    foreach (Vector3 normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
                    {
                        if (Vector3.Dot(normal, comeFrom) > 0.9f || Vector3.Dot(normal, goTo) > 0.9f)
                        {
                            continue;
                        }
                        Vector3 slab = (Vector3.one - Abs(normal)) * size + Abs(normal) * skin;
                        BuildKit.Box("DuctCorner", parent, p[i] + normal * (size * 0.5f), slab, metal, 1f, false);
                    }
                }
                level.Vents.Add(Grate(parent, duct, true));
                level.Vents.Add(Grate(parent, duct, false));
            }
        }

        /// <summary>The grate at one end of a duct, facing the room, and the trigger the player uses.</summary>
        private static VentGrate Grate(Transform parent, DuctPath duct, bool endA)
        {
            Vector3[] p = duct.Points;
            Vector3 point = endA ? p[0] : p[p.Length - 1];
            Vector3 inward = ((endA ? p[1] : p[p.Length - 2]) - point).normalized;   // from the room into the duct
            bool floor = endA ? duct.FloorGrateA : duct.FloorGrateB;
            bool open = !endA && duct.OpenGrateB;
            Vector3 face = floor ? point + Vector3.up * 0.035f
                         : open ? point - inward * 0.03f
                         : point - inward * (PizzeriaLayout.WallThickness + 0.03f);
            Quaternion rotation = Quaternion.LookRotation(-inward, floor ? Vector3.forward : Vector3.up);

            var grate = new GameObject("VentGrate");
            grate.transform.SetParent(parent, false);
            grate.transform.SetPositionAndRotation(face, rotation);
            Material frame = BuildKit.Plain("grateFrame", new Color(0.25f, 0.26f, 0.27f), 0.5f, 0.8f);
            const float size = PizzeriaLayout.DuctSize;
            BuildKit.Box("Hole", grate.transform, new Vector3(0f, 0f, -0.02f), new Vector3(size - 0.1f, size - 0.1f, 0.01f), BuildKit.Plain("black", Color.black), 1f, false);
            BuildKit.Box("FrameTop", grate.transform, new Vector3(0f, size * 0.5f - 0.04f, 0f), new Vector3(size, 0.08f, 0.04f), frame, 1f, false);
            BuildKit.Box("FrameBottom", grate.transform, new Vector3(0f, -size * 0.5f + 0.04f, 0f), new Vector3(size, 0.08f, 0.04f), frame, 1f, false);
            BuildKit.Box("FrameLeft", grate.transform, new Vector3(-size * 0.5f + 0.04f, 0f, 0f), new Vector3(0.08f, size, 0.04f), frame, 1f, false);
            BuildKit.Box("FrameRight", grate.transform, new Vector3(size * 0.5f - 0.04f, 0f, 0f), new Vector3(0.08f, size, 0.04f), frame, 1f, false);
            for (int i = 0; i < 7; i++)
            {
                var slat = BuildKit.Box("Slat", grate.transform, new Vector3(0f, -size * 0.38f + i * size * 0.127f, 0f), new Vector3(size - 0.12f, 0.025f, 0.05f), frame, 1f, false);
                slat.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            }
            var trigger = grate.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0f, 0.3f);
            trigger.size = new Vector3(size + 0.3f, size + 0.3f, 0.6f);
            return VentGrate.Attach(grate, duct, endA);
        }

        /// <summary>The four directions across a duct running along <paramref name="axis"/>.</summary>
        private static IEnumerable<Vector3> Perpendiculars(Vector3 axis)
        {
            foreach (Vector3 normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                if (Mathf.Abs(Vector3.Dot(normal, axis)) < 0.1f)
                {
                    yield return normal;
                }
            }
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

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
            bool shadows = room == Room.DiningHall || room == Room.Office || room == Room.Storage || room == Room.MainHall;

            for (int c = 0; c < columns; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    var position = new Vector3(area.xMin + area.width * (c + 0.5f) / columns, height, area.yMin + area.height * (r + 0.5f) / rows);
                    double roll = random.NextDouble();
                    bool dead = roll < 0.28 && columns * rows > 1;   // more than a quarter of the bulbs are gone
                    float flicker = roll < 0.45 ? 0.6f : 0.15f;
                    lamps.Add(Lamp(root, position, warm, dead, flicker, shadows && (c + r) % 2 == 0, hall ? 0.8f : 1f));
                }
            }

            if (room == Room.BackHall || room == Room.MainHall)
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
            light.intensity = (dead ? 0f : 7f) * strength;
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

            // The fuse: a heavy power cell, its amber core glowing through the casing, terminals on top,
            // a hazard band, a pulsing ring of light on the shelf and sparks rising from it. In the dark
            // it is the brightest thing in the room.
            var fuse = new GameObject("Fuse");
            fuse.transform.SetParent(roomRoot, false);
            fuse.transform.position = position;
            Material casing = BuildKit.Plain("cellCasing", new Color(0.12f, 0.12f, 0.13f), 0.75f, 0.9f);
            Material core = BuildKit.Glow("cellCore", new Color(1f, 0.62f, 0.12f), 6f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Material hazard = BuildKit.Picture("cellHazard", ProceduralTextures.HazardStripes(), 0f);
            BuildKit.Cylinder("Core", fuse.transform, Vector3.zero, 0.15f, 0.3f, core);
            for (int i = 0; i < 4; i++)
            {
                // Four casing ribs; the core shows between them.
                var rib = BuildKit.Box("Rib", fuse.transform, Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0f, 0f, 0.075f), new Vector3(0.06f, 0.3f, 0.03f), casing, 1f, false);
                rib.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            }
            BuildKit.Cylinder("CapTop", fuse.transform, new Vector3(0f, 0.17f, 0f), 0.18f, 0.05f, casing);
            BuildKit.Cylinder("CapBottom", fuse.transform, new Vector3(0f, -0.17f, 0f), 0.18f, 0.05f, casing);
            BuildKit.Cylinder("Hazard", fuse.transform, new Vector3(0f, -0.12f, 0f), 0.165f, 0.04f, hazard);
            BuildKit.Cylinder("TerminalPlus", fuse.transform, new Vector3(0.035f, 0.21f, 0f), 0.035f, 0.04f, chrome);
            BuildKit.Cylinder("TerminalMinus", fuse.transform, new Vector3(-0.035f, 0.205f, 0f), 0.03f, 0.03f, chrome);
            var halo = BuildKit.Quad("Halo", roomRoot, position - Vector3.up * 0.17f + facing * Vector3.forward * 0.1f, new Vector2(0.7f, 0.7f), 0f,
                                     BuildKit.Additive("cellHalo", ProceduralTextures.Ring(128, 0.12f), new Color(1f, 0.6f, 0.15f, 0.8f)));
            halo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Sparks(fuse.transform);
            var collider = fuse.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.4f, 0.5f, 0.4f);
            collider.isTrigger = true;

            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(fuse.transform, false);
            glow.type = LightType.Point;
            glow.range = 4f;
            glow.color = new Color(1f, 0.62f, 0.18f);
            glow.intensity = 1.6f;
            FusePickup pickup = FusePickup.Attach(fuse, room, glow);
            fuse.SetActive(false);
            return pickup;
        }

        /// <summary>A few sparks drifting up from a power cell.</summary>
        private static void Sparks(Transform parent)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            var sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop();
            ParticleSystem.MainModule main = sparks.main;
            main.loop = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 0.25f;
            main.startSize = 0.02f;
            main.startColor = new Color(1f, 0.75f, 0.3f, 0.9f);
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 12f;
            ParticleSystem.ShapeModule shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = BuildKit.Additive("spark", ProceduralTextures.Disc(16), new Color(1f, 0.7f, 0.3f, 1f));
            sparks.Play();
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
