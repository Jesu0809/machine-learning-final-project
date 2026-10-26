using System.Collections.Generic;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>Kind of hiding spot; decides its shape and how the view looks from inside.</summary>
    public enum SpotKind
    {
        Locker,
        Cabinet,
        Table
    }

    /// <summary>An air duct only the player fits through: a tube from a grate in one room to a grate in another.</summary>
    public class DuctPath
    {
        public Room A;
        public Room B;
        /// <summary>Where the player stands in each room to use the grate.</summary>
        public Vector3 StandA;
        public Vector3 StandB;
        /// <summary>The tube: from the grate in A to the grate in B, axis-aligned segments.</summary>
        public Vector3[] Points;
        /// <summary>True if the grate sits in the floor (a crawlspace) rather than in a wall.</summary>
        public bool FloorGrateA;
        public bool FloorGrateB;
        /// <summary>True if the grate at B is on a duct standing in the open (not set in a wall).</summary>
        public bool OpenGrateB;
    }

    /// <summary>
    /// Physical layout of the pizzeria: where each room of <see cref="HuntMap"/> is in the 3D world.
    /// One floor in two wings joined by the north wing: the staff wing to the west (the security
    /// office where the night starts, parts, restrooms, the back hall, the kitchen and the
    /// storage) and the public wing to the east (the dining hall with Freddy's stage, the
    /// arcade, the main hall, the party room and the exit lobby). The north wing is two dining
    /// rooms with tables and windows, the banquet room off the kitchen and the veranda above the
    /// arcade; its tables are hiding spots to slide under. Air ducts give the player
    /// crawl-only shortcuts across. Rooms keep exactly the connections of the graph the brain was
    /// trained on, and corridors are as long as their cost in it. Inside a room Freddy walks
    /// straight lines between the room's hub, its doorways and its hiding spots; props are kept
    /// off those lines.
    /// </summary>
    /// <remarks>
    /// Top view (north up):
    /// <code>
    ///                [======= Banquet Room =======]===[ Veranda ]
    ///                       ||                          ||
    ///  [Storage]=[ Kitchen ]      [ Dining Hall (stage) ]=[ Arcade ]
    ///                 ||                                    ||
    ///  [=========== Back Hall ===========]   [======= Main Hall =======]
    ///     ||        ||          ||                    ||
    ///  [Office]=[ Parts ]   [Restrooms]           [ Party ]=[ Exit Lobby ]
    ///                                                            EXIT
    /// </code>
    /// </remarks>
    public static class PizzeriaLayout
    {
        public const float DoorWidth = 2.2f;
        public const float DoorHeight = 2.6f;
        public const float WallThickness = 0.2f;
        public const float CorridorWidth = 2.6f;

        /// <summary>Side of the square air ducts.</summary>
        public const float DuctSize = 1.15f;

        /// <summary>Depth of the crawlspace ducts under the floor.</summary>
        private const float Under = -0.75f;

        private struct RoomShape
        {
            public Rect Area;      // x = west, y = south, in meters
            public float Height;
            public Vector2 Hub;
        }

        private struct Spot
        {
            public SpotKind Kind;
            public Vector2 Position;
            public float Facing;   // yaw in degrees, 0 = north
        }

        private struct Passage
        {
            public Room From;
            public Room To;
            public Vector3[] Points;  // from the doorway of From to the doorway of To
        }

        // Order matches the Room enum.
        private static readonly RoomShape[] Rooms =
        {
            Shape(-22f, -16f, 6f, 7f, 3.2f),          // Office
            Shape(-14f, -14f, 6f, 7f, 3.2f),          // Parts & Service
            Shape(-7f, -14f, 5f, 7f, 3.2f),           // Restrooms
            Shape(-22f, -3f, 19f, 3f, 3.2f),          // Back Hall
            Shape(-22f, 2f, 6f, 8f, 3.6f),            // Storage
            Shape(-14f, 2f, 9f, 8f, 3.4f),            // Kitchen
            Shape(2f, 2f, 11f, 10f, 4.4f),            // Dining Hall (the show stage on its north wall)
            Shape(4f, -3f, 19f, 3f, 3.4f),            // Main Hall
            Shape(4f, -14f, 6f, 7f, 3.4f),            // Party Room
            Shape(15f, 2f, 8f, 8f, 3.6f),             // Arcade
            Shape(12f, -17f, 8f, 8f, 3.6f),           // Exit Lobby
            Shape(-16f, 15f, 25f, 7f, 3.4f),          // Banquet Room (north of the kitchen)
            Shape(12f, 15f, 10f, 7f, 3.4f)            // Veranda (north of the arcade)
        };

        private static readonly Passage[] Passages =
        {
            // Staff wing.
            Link(Room.Office, Room.Parts, -16f, -11.5f, -14f, -11.5f),
            Link(Room.Office, Room.BackHall, -19f, -9f, -19f, -3f),
            Link(Room.Parts, Room.BackHall, -11f, -7f, -11f, -3f),
            Link(Room.Restrooms, Room.BackHall, -4.5f, -7f, -4.5f, -3f),
            Link(Room.BackHall, Room.Kitchen, -9.5f, 0f, -9.5f, 2f),
            Link(Room.Storage, Room.Kitchen, -16f, 6f, -14f, 6f),
            // Between the wings: only the north wing.
            Link(Room.Kitchen, Room.Banquet, -6.3f, 10f, -6.3f, 15f),
            Link(Room.Banquet, Room.Veranda, 9f, 18.5f, 12f, 18.5f),
            Link(Room.Veranda, Room.Arcade, 18f, 15f, 18f, 10f),
            // Public wing.
            Link(Room.DiningHall, Room.Arcade, 13f, 6f, 15f, 6f),
            Link(Room.MainHall, Room.Arcade, 19f, 0f, 19f, 2f),
            Link(Room.MainHall, Room.PartyRoom, 7f, -3f, 7f, -7f),
            Link(Room.PartyRoom, Room.ExitLobby, 10f, -10.5f, 12f, -10.5f)
        };

        private static readonly DuctPath[] DuctList =
        {
            // Office → Storage: a crawlspace under the back hall, north along the west wall.
            new DuctPath
            {
                A = Room.Office, B = Room.Storage,
                StandA = new Vector3(-20.3f, 0f, -11.5f), StandB = new Vector3(-20.3f, 0f, 3.5f),
                Points = new[] { new Vector3(-21f, 0f, -11.5f), new Vector3(-21f, Under, -11.5f), new Vector3(-21f, Under, 3.5f), new Vector3(-21f, 0f, 3.5f) },
                FloorGrateA = true, FloorGrateB = true
            },
            // Restrooms → Party Room: straight through the walls between the wings, at knee height.
            new DuctPath
            {
                A = Room.Restrooms, B = Room.PartyRoom,
                StandA = new Vector3(-2.8f, 0f, -12f), StandB = new Vector3(4.8f, 0f, -12f),
                Points = new[] { new Vector3(-2f, 0.6f, -12f), new Vector3(4f, 0.6f, -12f) }
            },
            // Kitchen → Arcade: under the dining hall and the stage.
            new DuctPath
            {
                A = Room.Kitchen, B = Room.Arcade,
                StandA = new Vector3(-6.7f, 0f, 8.5f), StandB = new Vector3(16.7f, 0f, 8.5f),
                Points = new[] { new Vector3(-6f, 0f, 8.5f), new Vector3(-6f, Under, 8.5f), new Vector3(16f, Under, 8.5f), new Vector3(16f, 0f, 8.5f) },
                FloorGrateA = true, FloorGrateB = true
            },
            // Parts → Dining Hall: under the restrooms and the main hall.
            new DuctPath
            {
                A = Room.Parts, B = Room.DiningHall,
                StandA = new Vector3(-9.9f, 0f, -8.5f), StandB = new Vector3(3.8f, 0f, 2.9f),
                Points = new[] { new Vector3(-9.2f, 0f, -8.5f), new Vector3(-9.2f, Under, -8.5f), new Vector3(3f, Under, -8.5f), new Vector3(3f, Under, 2.9f), new Vector3(3f, 0f, 2.9f) },
                FloorGrateA = true, FloorGrateB = true
            }
        };

        private static readonly Spot[][] Spots =
        {
            new[] { S(SpotKind.Locker, -21.4f, -15.4f, 45f), S(SpotKind.Table, -17.6f, -15.2f, 0f) },                                       // Office
            new[] { S(SpotKind.Locker, -13.4f, -13.4f, 45f), S(SpotKind.Cabinet, -8.6f, -12.6f, 270f) },                                    // Parts
            new[] { S(SpotKind.Locker, -6.4f, -13.4f, 45f), S(SpotKind.Cabinet, -6.6f, -9.6f, 90f) },                                       // Restrooms
            new[] { S(SpotKind.Locker, -15f, -0.4f, 180f) },                                                                                 // Back Hall
            new[] { S(SpotKind.Locker, -21.4f, 9.4f, 135f), S(SpotKind.Cabinet, -16.6f, 9f, 270f), S(SpotKind.Cabinet, -17.8f, 2.6f, 0f) },  // Storage
            new[] { S(SpotKind.Locker, -13.4f, 2.6f, 45f), S(SpotKind.Cabinet, -9.5f, 9.6f, 180f), S(SpotKind.Table, -7.2f, 3.4f, 315f) },   // Kitchen
            new[] { S(SpotKind.Locker, 12.4f, 2.6f, 315f), S(SpotKind.Table, 5.6f, 4.2f, 0f), S(SpotKind.Table, 10.2f, 8.4f, 180f) },        // Dining Hall
            new[] { S(SpotKind.Locker, 12f, -0.4f, 180f) },                                                                                  // Main Hall
            new[] { S(SpotKind.Locker, 9.4f, -13.4f, 315f), S(SpotKind.Table, 7.6f, -13.2f, 0f) },                                          // Party Room
            new[] { S(SpotKind.Locker, 22.4f, 9.4f, 225f), S(SpotKind.Cabinet, 22.6f, 4f, 270f) },                                          // Arcade
            new[] { S(SpotKind.Locker, 19.4f, -9.6f, 225f) },                                                                                // Exit Lobby
            new[] { S(SpotKind.Table, -12f, 17.2f, 90f), S(SpotKind.Table, -3.5f, 20.5f, 180f), S(SpotKind.Table, 3f, 16.4f, 0f) },        // Banquet Room
            new[] { S(SpotKind.Table, 20.2f, 20.4f, 180f), S(SpotKind.Table, 14.2f, 20.4f, 180f) }                                          // Veranda
        };

        // Wall shelves for the fuses (rooms that can hold one), with the direction the shelf faces.
        private static readonly Dictionary<Room, (Vector2 position, float facing)> FuseShelves =
            new Dictionary<Room, (Vector2, float)>
            {
                { Room.Parts, (new Vector2(-8.35f, -10f), 270f) },
                { Room.Restrooms, (new Vector2(-4.5f, -13.65f), 0f) },
                { Room.BackHall, (new Vector2(-21.65f, -1.5f), 90f) },
                { Room.Storage, (new Vector2(-19f, 9.65f), 180f) },
                { Room.Kitchen, (new Vector2(-5.35f, 3.5f), 270f) },
                { Room.DiningHall, (new Vector2(12.65f, 8.6f), 270f) },
                { Room.MainHall, (new Vector2(15f, -2.65f), 0f) },
                { Room.PartyRoom, (new Vector2(4.35f, -9f), 90f) },
                { Room.Arcade, (new Vector2(22.65f, 7f), 270f) }
            };

        public static readonly Vector3 PlayerStart = new Vector3(-18f, 0.05f, -12.5f);
        public const float PlayerFacing = 0f;
        /// <summary>Freddy's place on the show stage, at the north wall of the dining hall.</summary>
        public static readonly Vector3 FreddyStart = new Vector3(7.5f, 0.7f, 10.8f);
        public const float FreddyFacing = 180f;

        /// <summary>The show stage: its footprint on the dining hall floor and its height.</summary>
        public static readonly Rect StageArea = Rect.MinMaxRect(4f, 9.8f, 11f, 12f);
        public const float StageHeight = 0.7f;

        /// <summary>The front doors, in the south wall of the Exit Lobby.</summary>
        public static readonly Vector3 ExitDoor = new Vector3(16f, 0f, -17f);

        /// <summary>The loading door, in the west wall of the Storage (the other possible way out).</summary>
        public static readonly Vector3 BackExitDoor = new Vector3(-22f, 0f, 6.5f);

        /// <summary>The door out of the building in an exit room (<see cref="HuntMap.Exits"/>).</summary>
        public static Vector3 ExitDoorOf(Room exit) => exit == Room.Storage ? BackExitDoor : ExitDoor;

        /// <summary>Unit vector from that door into the building.</summary>
        public static Vector3 ExitInward(Room exit) => exit == Room.Storage ? Vector3.right : Vector3.forward;

        /// <summary>Where the exit panel hangs on the wall next to the door.</summary>
        public static Vector3 ExitPanelOf(Room exit)
        {
            Vector3 inward = ExitInward(exit);
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            return ExitDoorOf(exit) + side * 2.2f + inward * 0.15f + Vector3.up * 1.35f;
        }

        /// <summary>True once the player is outside, past the door of <paramref name="exit"/>.</summary>
        public static bool IsOutside(Room exit, Vector3 position)
        {
            Vector3 door = ExitDoorOf(exit);
            return Vector3.Dot(position - door, ExitInward(exit)) < -0.8f;
        }

        /// <summary>Area that contains the whole building (for the maps in the UI).</summary>
        public static readonly Rect Bounds = new Rect(-23f, -19f, 47f, 42f);

        public static Rect Area(Room room) => Rooms[(int)room].Area;
        public static float Height(Room room) => Rooms[(int)room].Height;

        public static IReadOnlyList<DuctPath> Ducts => DuctList;

        /// <summary>The room's hub: where Freddy walks to when the brain picks the room.</summary>
        public static Vector3 Center(Room room)
        {
            Vector2 hub = Rooms[(int)room].Hub;
            return new Vector3(hub.x, 0f, hub.y);
        }

        /// <summary>The room that contains <paramref name="position"/>; in a corridor or a duct, the nearest room.</summary>
        public static Room RoomAt(Vector3 position)
        {
            var point = new Vector2(position.x, position.z);
            Room best = Room.Office;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Rooms.Length; i++)
            {
                Rect area = Rooms[i].Area;
                float dx = Mathf.Max(area.xMin - point.x, 0f, point.x - area.xMax);
                float dz = Mathf.Max(area.yMin - point.y, 0f, point.y - area.yMax);
                float distance = dx * dx + dz * dz;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (Room)i;
                }
            }
            return best;
        }

        /// <summary>Waypoints through the doorways and the corridor from <paramref name="from"/> into <paramref name="to"/>.</summary>
        public static IReadOnlyList<Vector3> PassageBetween(Room from, Room to)
        {
            foreach (Passage passage in Passages)
            {
                if (passage.From == from && passage.To == to)
                {
                    return Ordered(passage.Points, false);
                }
                if (passage.From == to && passage.To == from)
                {
                    return Ordered(passage.Points, true);
                }
            }
            return new[] { Center(to) };
        }

        /// <summary>The doorway of the passage toward <paramref name="neighbor"/> on <paramref name="room"/>'s wall.</summary>
        public static Vector3 DoorOf(Room room, Room neighbor)
        {
            foreach (Vector3 point in PassageBetween(room, neighbor))
            {
                if (OnWall(room, point))
                {
                    return point;
                }
            }
            return Center(room);
        }

        /// <summary>
        /// One door per connection (at its first doorway), with the axis of its wall and the
        /// direction from the doorway into its corridor (the side the open leaves rest on).
        /// </summary>
        public static IEnumerable<(Room from, Room to, Vector3 door, bool alongX, Vector3 corridor)> Doorways()
        {
            foreach (Passage passage in Passages)
            {
                Vector3 point = passage.Points[0];
                yield return (passage.From, passage.To, point, AlongX(passage.From, point), Outward(passage.From, point));
            }
        }

        /// <summary>Doorway points on the walls of <paramref name="room"/>, the exit door included.</summary>
        public static IEnumerable<Vector2> DoorwaysOf(Room room)
        {
            foreach (Passage passage in Passages)
            {
                if (passage.From != room && passage.To != room)
                {
                    continue;
                }
                foreach (Vector3 point in passage.Points)
                {
                    if (OnWall(room, point))
                    {
                        yield return new Vector2(point.x, point.z);
                    }
                }
            }
            foreach (Room exit in HuntMap.Exits)
            {
                if (room == exit)
                {
                    Vector3 door = ExitDoorOf(exit);
                    yield return new Vector2(door.x, door.z);
                }
            }
        }

        /// <summary>Unit vector from a doorway on <paramref name="room"/>'s wall out of the room.</summary>
        public static Vector3 Outward(Room room, Vector3 door)
        {
            Vector2 center = Area(room).center;
            return AlongX(room, door) ? new Vector3(0f, 0f, Mathf.Sign(door.z - center.y)) : new Vector3(Mathf.Sign(door.x - center.x), 0f, 0f);
        }

        /// <summary>The corridors: from the doorway of one room to the doorway of the next.</summary>
        public static IEnumerable<(Vector3 start, Vector3 end, float height)> Corridors()
        {
            foreach (Passage passage in Passages)
            {
                Vector3 a = passage.Points[0];
                Vector3 b = passage.Points[passage.Points.Length - 1];
                if (Vector3.Distance(a, b) > 0.05f)
                {
                    yield return (a, b, Mathf.Min(Height(passage.From), Height(passage.To), 3f));
                }
            }
        }

        public static SpotKind SpotKindOf(Room room, int spot) => Spots[(int)room][spot].Kind;

        /// <summary>Where the hiding spot stands, on the floor.</summary>
        public static Vector3 SpotPosition(Room room, int spot)
        {
            Vector2 position = Spots[(int)room][spot].Position;
            return new Vector3(position.x, 0f, position.y);
        }

        /// <summary>Rotation of a spot: its open side faces the room.</summary>
        public static Quaternion SpotFacing(Room room, int spot) => Quaternion.Euler(0f, Spots[(int)room][spot].Facing, 0f);

        /// <summary>Where Freddy stands to search the spot: in front of its open side.</summary>
        public static Vector3 SpotApproach(Room room, int spot)
        {
            return SpotPosition(room, spot) + SpotFacing(room, spot) * Vector3.forward * 1.5f;
        }

        public static bool HasFuseShelf(Room room) => FuseShelves.ContainsKey(room);

        /// <summary>Where the fuse of a room floats, above its wall shelf.</summary>
        public static Vector3 FusePosition(Room room)
        {
            (Vector2 position, float _) = FuseShelves[room];
            return new Vector3(position.x, 1.15f, position.y);
        }

        public static Quaternion FuseFacing(Room room) => Quaternion.Euler(0f, FuseShelves[room].facing, 0f);

        /// <summary>
        /// The straight lines Freddy (or the player) walks inside a room: hub to every doorway, spot
        /// and vent; props must stay clear of them.
        /// </summary>
        public static IEnumerable<(Vector2 a, Vector2 b)> WalkLines(Room room)
        {
            Vector2 hub = Rooms[(int)room].Hub;
            foreach (Vector2 door in DoorwaysOf(room))
            {
                yield return (hub, door);
            }
            for (int spot = 0; spot < Spots[(int)room].Length; spot++)
            {
                Vector3 approach = SpotApproach(room, spot);
                yield return (hub, new Vector2(approach.x, approach.z));
            }
            foreach (DuctPath duct in DuctList)
            {
                if (duct.A == room)
                {
                    yield return (hub, new Vector2(duct.StandA.x, duct.StandA.z));
                }
                if (duct.B == room)
                {
                    yield return (hub, new Vector2(duct.StandB.x, duct.StandB.z));
                }
            }
            if (room == HuntMap.FreddyStart)
            {
                yield return (hub, new Vector2(FreddyStart.x, FreddyStart.z));
            }
        }

        /// <summary>True if <paramref name="point"/> lies on one of the walls of <paramref name="room"/>.</summary>
        private static bool OnWall(Room room, Vector3 point)
        {
            Rect a = Rooms[(int)room].Area;
            bool onX = (Mathf.Abs(point.x - a.xMin) < 0.05f || Mathf.Abs(point.x - a.xMax) < 0.05f) && point.z > a.yMin && point.z < a.yMax;
            bool onZ = (Mathf.Abs(point.z - a.yMin) < 0.05f || Mathf.Abs(point.z - a.yMax) < 0.05f) && point.x > a.xMin && point.x < a.xMax;
            return onX || onZ;
        }

        /// <summary>True if the doorway lies on a north or south wall (the wall runs along x).</summary>
        public static bool AlongX(Room room, Vector3 point)
        {
            Rect area = Area(room);
            return Mathf.Abs(point.z - area.yMax) < 0.05f || Mathf.Abs(point.z - area.yMin) < 0.05f;
        }

        // ------------------------------------------------------------------ table helpers

        private static RoomShape Shape(float west, float south, float width, float depth, float height)
        {
            var area = new Rect(west, south, width, depth);
            return new RoomShape { Area = area, Height = height, Hub = area.center };
        }

        private static Passage Link(Room from, Room to, float x1, float z1, float x2, float z2)
        {
            return new Passage { From = from, To = to, Points = new[] { new Vector3(x1, 0f, z1), new Vector3(x2, 0f, z2) } };
        }

        private static Spot S(SpotKind kind, float x, float z, float facing)
        {
            return new Spot { Kind = kind, Position = new Vector2(x, z), Facing = facing };
        }

        private static IReadOnlyList<Vector3> Ordered(Vector3[] points, bool reverse)
        {
            var result = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                result[i] = points[reverse ? points.Length - 1 - i : i];
            }
            return result;
        }
    }
}
