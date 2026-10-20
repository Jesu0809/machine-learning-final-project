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

    /// <summary>A staircase inside a tall room, from its floor up to a doorway onto the upper floor.</summary>
    public struct Staircase
    {
        public Room Room;
        public Vector3 Bottom;     // first step, on the floor
        public Vector3 Top;        // last step, at the upper floor
        public Rect Landing;       // platform at the top, in front of the upper doorway
        public float Width;
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
    /// Two floors: the public restaurant downstairs, laid out as a ring of rooms around a
    /// double-height dining hall, and the staff floor upstairs. Two staircases (in the dining hall
    /// and in the storage) join them, and air ducts give the player crawl-only shortcuts, some of
    /// them dropping from one floor to the other. Rooms keep exactly the connections of the graph
    /// the brain was trained on. Inside a room Freddy walks straight lines between the room's hub,
    /// its doorways, its stairs and its hiding spots; props are kept off those lines.
    /// </summary>
    /// <remarks>
    /// Top view (north up). Ground floor:
    /// <code>
    ///                          [=========== Back Hall ===========][ Storage  ]
    ///   [ Kitchen  ]     [   Dining Hall (stairs up)   ]   [ Arcade ]  (stairs up)
    /// [  Show Stage  ][ Party Room ][ Exit Lobby ]=> EXIT
    /// </code>
    /// Upper floor (4.4 m up):
    /// <code>
    /// [ Office ][ Manager ][ Lounge ]
    ///   [========= Upper Hall =========]
    /// [  Parts  ]                  [ Maintenance ]
    /// </code>
    /// </remarks>
    public static class PizzeriaLayout
    {
        public const float DoorWidth = 2.2f;
        public const float DoorHeight = 2.6f;
        public const float WallThickness = 0.2f;
        public const float CorridorWidth = 2.6f;

        /// <summary>Height of the upper floor above the ground floor.</summary>
        public const float UpperFloor = 4.4f;

        /// <summary>Side of the square air ducts.</summary>
        public const float DuctSize = 1.15f;

        private struct RoomShape
        {
            public Rect Area;      // x = west, y = south, in meters
            public float Elevation;
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
            public Vector3[] Points;  // from the From side to the To side; y is the floor height
        }

        private const float U = UpperFloor;

        // Order matches the Room enum.
        private static readonly RoomShape[] Rooms =
        {
            Shape(-18f, 13f, 8f, 6f, U, 3.0f),        // Office
            Shape(-13f, 8f, 26f, 3f, U, 3.0f),        // Upper Hall
            Shape(-8f, 13f, 8f, 6f, U, 3.0f),         // Manager
            Shape(2f, 13f, 8f, 6f, U, 3.0f),          // Lounge
            Shape(-21f, -4f, 11f, 10f, U, 3.2f),      // Parts & Service
            Shape(10f, -4f, 10f, 10f, U, 3.2f),       // Maintenance
            Shape(-13f, 8f, 26f, 3f, 0f, 3.2f),       // Back Hall
            Shape(-21f, -5f, 11f, 11f, 0f, 3.4f),     // Kitchen
            Shape(-7f, -10f, 14f, 16f, 0f, 8.0f),     // Dining Hall (double height, stairs inside)
            Shape(10f, -5f, 10f, 11f, 0f, 3.6f),      // Arcade
            Shape(14f, 8f, 8f, 11f, 0f, 8.0f),        // Storage (double height, stairs inside)
            Shape(-21f, -19f, 13f, 11f, 0f, 4.2f),    // Show Stage
            Shape(-6f, -19f, 12f, 7f, 0f, 3.4f),      // Party Room
            Shape(8f, -17f, 10f, 9f, 0f, 3.6f)        // Exit Lobby
        };

        private static readonly Staircase[] StairList =
        {
            // Along the dining hall's north wall, rising east to a landing at the upper hall's door.
            new Staircase { Room = Room.DiningHall, Bottom = new Vector3(-4.5f, 0f, 5f), Top = new Vector3(3.5f, U, 5f),
                            Landing = Rect.MinMaxRect(3.5f, 3.9f, 6.8f, 5.8f), Width = 1.6f },
            // Metal stairs along the storage's west wall, rising south to the upper hall's door.
            new Staircase { Room = Room.Storage, Bottom = new Vector3(15f, 0f, 18.2f), Top = new Vector3(15f, U, 11.2f),
                            Landing = Rect.MinMaxRect(14.2f, 8.2f, 16.6f, 11.2f), Width = 1.6f }
        };

        private static readonly Passage[] Passages =
        {
            // Ground floor: a ring around the dining hall.
            Link(Room.Kitchen, Room.BackHall, -11.5f, 6f, -11.5f, 8f, 0f),
            Link(Room.BackHall, Room.Storage, 13f, 9.5f, 14f, 9.5f, 0f),
            Link(Room.Storage, Room.Arcade, 17f, 8f, 17f, 6f, 0f),
            Link(Room.Arcade, Room.ExitLobby, 14f, -5f, 14f, -8f, 0f),
            Link(Room.ExitLobby, Room.PartyRoom, 8f, -14.5f, 6f, -14.5f, 0f),
            Link(Room.PartyRoom, Room.Stage, -6f, -15.5f, -8f, -15.5f, 0f),
            Link(Room.Stage, Room.Kitchen, -15f, -8f, -15f, -5f, 0f),
            Link(Room.Kitchen, Room.DiningHall, -10f, 1f, -7f, 1f, 0f),
            Link(Room.DiningHall, Room.Arcade, 7f, 1f, 10f, 1f, 0f),
            Link(Room.DiningHall, Room.PartyRoom, 0f, -10f, 0f, -12f, 0f),
            // Up the stairs.
            new Passage
            {
                From = Room.DiningHall, To = Room.UpperHall,
                Points = new[] { new Vector3(-5.5f, 0f, 5f), new Vector3(-4.5f, 0f, 5f), new Vector3(3.5f, U, 5f), new Vector3(5f, U, 5f), new Vector3(5f, U, 6f), new Vector3(5f, U, 8f) }
            },
            new Passage
            {
                From = Room.Storage, To = Room.UpperHall,
                Points = new[] { new Vector3(15f, 0f, 18.6f), new Vector3(15f, 0f, 18.2f), new Vector3(15f, U, 11.2f), new Vector3(15f, U, 9.5f), new Vector3(14f, U, 9.5f), new Vector3(13f, U, 9.5f) }
            },
            // Upper floor.
            Link(Room.UpperHall, Room.Parts, -11.5f, 8f, -11.5f, 6f, U),
            Link(Room.UpperHall, Room.Maintenance, 11.5f, 8f, 11.5f, 6f, U),
            Link(Room.UpperHall, Room.Manager, -4f, 11f, -4f, 13f, U),
            Link(Room.UpperHall, Room.Lounge, 6f, 11f, 6f, 13f, U),
            Link(Room.Manager, Room.Office, -8f, 16f, -10f, 16f, U),
            Link(Room.Manager, Room.Lounge, 0f, 16f, 2f, 16f, U)
        };

        private static readonly DuctPath[] DuctList =
        {
            // Office → Parts: through the wall void beside the upper hall.
            new DuctPath
            {
                A = Room.Office, B = Room.Parts,
                StandA = new Vector3(-15f, U, 13.8f), StandB = new Vector3(-15f, U, 5.2f),
                Points = new[] { new Vector3(-15f, U + 0.6f, 13f), new Vector3(-15f, U + 0.6f, 6f) }
            },
            // Parts → Stage: out over the stage, then a drop down a shaft onto the show stage itself.
            new DuctPath
            {
                A = Room.Parts, B = Room.Stage,
                StandA = new Vector3(-19.5f, U, -3.2f), StandB = new Vector3(-19.5f, 0.7f, -10.4f),
                Points = new[] { new Vector3(-19.5f, U + 0.6f, -4f), new Vector3(-19.5f, U + 0.6f, -8.7f), new Vector3(-19.5f, 1.3f, -8.7f), new Vector3(-19.5f, 1.3f, -9.3f) },
                OpenGrateB = true
            },
            // Lounge → Arcade: down a shaft behind the halls, then under the floor to a grate in the arcade.
            new DuctPath
            {
                A = Room.Lounge, B = Room.Arcade,
                StandA = new Vector3(9f, U, 13.8f), StandB = new Vector3(11.8f, 0f, 2.2f),
                Points = new[] { new Vector3(9f, U + 0.6f, 13f), new Vector3(9f, U + 0.6f, 12f), new Vector3(9f, -0.75f, 12f), new Vector3(9f, -0.75f, 3f), new Vector3(11.2f, -0.75f, 3f), new Vector3(11.2f, 0f, 3f) },
                FloorGrateB = true
            },
            // Party Room → Kitchen: a crawlspace under the show stage.
            new DuctPath
            {
                A = Room.PartyRoom, B = Room.Kitchen,
                StandA = new Vector3(-4.3f, 0f, -13.2f), StandB = new Vector3(-14f, 0f, -1.2f),
                Points = new[] { new Vector3(-5f, 0f, -13.2f), new Vector3(-5f, -0.75f, -13.2f), new Vector3(-14f, -0.75f, -13.2f), new Vector3(-14f, -0.75f, -2f), new Vector3(-14f, 0f, -2f) },
                FloorGrateA = true, FloorGrateB = true
            }
        };

        private static readonly Spot[][] Spots =
        {
            new[] { S(SpotKind.Locker, -17.4f, 18.4f, 135f), S(SpotKind.Table, -12.5f, 17.8f, 180f) },                                        // Office
            new[] { S(SpotKind.Locker, 0.5f, 10.6f, 180f) },                                                                                   // Upper Hall
            new[] { S(SpotKind.Locker, -7.4f, 18.4f, 135f), S(SpotKind.Cabinet, -1.5f, 18.6f, 180f) },                                        // Manager
            new[] { S(SpotKind.Locker, 9.4f, 18.4f, 225f), S(SpotKind.Table, 4.5f, 17.8f, 180f) },                                            // Lounge
            new[] { S(SpotKind.Locker, -20.4f, 5.4f, 135f), S(SpotKind.Cabinet, -20.6f, 0f, 90f), S(SpotKind.Cabinet, -13f, -3.6f, 0f) },     // Parts
            new[] { S(SpotKind.Locker, 19.4f, 5.4f, 225f), S(SpotKind.Cabinet, 19.6f, -1f, 270f) },                                           // Maintenance
            new[] { S(SpotKind.Locker, 4.5f, 10.6f, 180f) },                                                                                   // Back Hall
            new[] { S(SpotKind.Locker, -20.4f, 5.4f, 135f), S(SpotKind.Cabinet, -20.6f, -1f, 90f), S(SpotKind.Table, -18f, -3.8f, 45f) },     // Kitchen
            new[] { S(SpotKind.Locker, -6.4f, -9.4f, 45f), S(SpotKind.Table, 5.4f, -8.4f, 315f), S(SpotKind.Table, 5.4f, -2f, 270f) },        // Dining Hall
            new[] { S(SpotKind.Locker, 19.4f, 5.4f, 225f), S(SpotKind.Cabinet, 19.6f, -3f, 270f) },                                           // Arcade
            new[] { S(SpotKind.Locker, 21.4f, 18.4f, 225f), S(SpotKind.Cabinet, 21.6f, 13f, 270f), S(SpotKind.Cabinet, 20.2f, 8.4f, 0f) },      // Storage
            new[] { S(SpotKind.Locker, -8.6f, -18.4f, 315f), S(SpotKind.Cabinet, -12.5f, -18.4f, 0f) },                                       // Stage
            new[] { S(SpotKind.Locker, 5.4f, -18.4f, 315f), S(SpotKind.Table, -3.5f, -17.8f, 0f), S(SpotKind.Table, 3f, -12.8f, 180f) },      // Party Room
            new[] { S(SpotKind.Locker, 17.4f, -8.6f, 225f) }                                                                                  // Exit Lobby
        };

        // Wall shelves for the fuses (rooms that can hold one), with the direction the shelf faces.
        private static readonly Dictionary<Room, (Vector2 position, float facing)> FuseShelves =
            new Dictionary<Room, (Vector2, float)>
            {
                { Room.UpperHall, (new Vector2(-8f, 10.65f), 180f) },
                { Room.Manager, (new Vector2(-6.5f, 13.35f), 0f) },
                { Room.Lounge, (new Vector2(9.65f, 15.5f), 270f) },
                { Room.Parts, (new Vector2(-10.35f, 1f), 270f) },
                { Room.Maintenance, (new Vector2(10.35f, 1f), 90f) },
                { Room.BackHall, (new Vector2(-6f, 8.35f), 0f) },
                { Room.Kitchen, (new Vector2(-20.65f, 2.6f), 90f) },
                { Room.DiningHall, (new Vector2(6.65f, -6f), 270f) },
                { Room.Arcade, (new Vector2(19.65f, 1.6f), 270f) },
                { Room.Storage, (new Vector2(21.65f, 16f), 270f) },
                { Room.PartyRoom, (new Vector2(1f, -18.65f), 0f) }
            };

        public static readonly Vector3 PlayerStart = new Vector3(-11.6f, U + 0.05f, 15f);
        public const float PlayerFacing = 270f;
        public static readonly Vector3 FreddyStart = new Vector3(-19f, 0.7f, -13.5f);
        public const float FreddyFacing = 90f;

        /// <summary>The exit door, in the south wall of the Exit Lobby.</summary>
        public static readonly Vector3 ExitDoor = new Vector3(13f, 0f, -17f);

        /// <summary>The player has escaped once south of this line.</summary>
        public const float ExitLineZ = -17.8f;

        /// <summary>Area that contains the whole building, per floor (for the maps in the UI).</summary>
        public static readonly Rect Bounds = new Rect(-22f, -20f, 45f, 40f);

        public static Rect Area(Room room) => Rooms[(int)room].Area;
        public static float Height(Room room) => Rooms[(int)room].Height;

        /// <summary>Height of the room's floor (0 downstairs, <see cref="UpperFloor"/> upstairs).</summary>
        public static float Elevation(Room room) => Rooms[(int)room].Elevation;

        public static IReadOnlyList<Staircase> Stairs => StairList;
        public static IReadOnlyList<DuctPath> Ducts => DuctList;

        /// <summary>The room's hub on its floor: where Freddy walks to when the brain picks the room.</summary>
        public static Vector3 Center(Room room)
        {
            Vector2 hub = Rooms[(int)room].Hub;
            return new Vector3(hub.x, Elevation(room), hub.y);
        }

        /// <summary>A world point turned into the room's own space (its floor is y = 0).</summary>
        public static Vector3 Local(Room room, Vector3 world) => world - Vector3.up * Elevation(room);

        /// <summary>
        /// The room that contains <paramref name="position"/>; in a corridor, a staircase or a duct,
        /// the nearest room on that level.
        /// </summary>
        public static Room RoomAt(Vector3 position)
        {
            var point = new Vector2(position.x, position.z);
            Room best = Room.Office;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Rooms.Length; i++)
            {
                RoomShape shape = Rooms[i];
                if (position.y < shape.Elevation - 1.5f || position.y > shape.Elevation + shape.Height + 0.3f)
                {
                    continue;
                }
                Rect area = shape.Area;
                float dx = Mathf.Max(area.xMin - point.x, 0f, point.x - area.xMax);
                float dz = Mathf.Max(area.yMin - point.y, 0f, point.y - area.yMax);
                // Same footprint: prefer the room whose floor is closest below.
                float distance = dx * dx + dz * dz + Mathf.Abs(position.y - shape.Elevation) * 0.01f;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (Room)i;
                }
            }
            return best;
        }

        /// <summary>Waypoints through the doorways, corridors and stairs from <paramref name="from"/> into <paramref name="to"/>.</summary>
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

        /// <summary>The doorway of the passage toward <paramref name="neighbor"/> on <paramref name="room"/>'s wall (world space).</summary>
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
                for (int i = 0; i < passage.Points.Length; i++)
                {
                    Vector3 point = passage.Points[i];
                    Room owner = OnWall(passage.From, point) ? passage.From : OnWall(passage.To, point) ? passage.To : (Room)(-1);
                    if ((int)owner < 0)
                    {
                        continue;
                    }
                    // Into the corridor: away from the room whose wall the door is on.
                    Vector2 center = Area(owner).center;
                    var outward = new Vector3(point.x - center.x, 0f, point.z - center.y);
                    outward = AlongX(owner, point) ? new Vector3(0f, 0f, Mathf.Sign(outward.z)) : new Vector3(Mathf.Sign(outward.x), 0f, 0f);
                    yield return (passage.From, passage.To, point, AlongX(owner, point), outward);
                    break;
                }
            }
        }

        /// <summary>Doorway points on the walls of <paramref name="room"/> (any height), for keeping props clear.</summary>
        public static IEnumerable<Vector2> DoorwaysOf(Room room)
        {
            foreach ((Vector2 point, float _) in DoorOpeningsOf(room))
            {
                yield return point;
            }
        }

        /// <summary>Openings in the walls of <paramref name="room"/>, with the height of their sill above the room's floor.</summary>
        public static IEnumerable<(Vector2 point, float sill)> DoorOpeningsOf(Room room)
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
                        yield return (new Vector2(point.x, point.z), point.y - Elevation(room));
                    }
                }
            }
            if (room == HuntMap.Exit)
            {
                yield return (new Vector2(ExitDoor.x, ExitDoor.z), 0f);
            }
        }

        /// <summary>Corridors between two doorways (consecutive passage points on walls, at the same height).</summary>
        public static IEnumerable<(Vector3 start, Vector3 end, float height)> Corridors()
        {
            foreach (Passage passage in Passages)
            {
                for (int i = 0; i + 1 < passage.Points.Length; i++)
                {
                    Vector3 a = passage.Points[i];
                    Vector3 b = passage.Points[i + 1];
                    bool aWall = OnWall(passage.From, a) || OnWall(passage.To, a);
                    bool bWall = OnWall(passage.From, b) || OnWall(passage.To, b);
                    if (aWall && bWall && Mathf.Abs(a.y - b.y) < 0.01f && Vector3.Distance(a, b) > 0.05f)
                    {
                        float height = Mathf.Min(Height(passage.From), Height(passage.To), 3f);
                        yield return (a, b, height);
                    }
                }
            }
        }

        public static SpotKind SpotKindOf(Room room, int spot) => Spots[(int)room][spot].Kind;

        /// <summary>Where the hiding spot stands, on the room's floor.</summary>
        public static Vector3 SpotPosition(Room room, int spot)
        {
            Vector2 position = Spots[(int)room][spot].Position;
            return new Vector3(position.x, Elevation(room), position.y);
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
            return new Vector3(position.x, Elevation(room) + 1.15f, position.y);
        }

        public static Quaternion FuseFacing(Room room) => Quaternion.Euler(0f, FuseShelves[room].facing, 0f);

        /// <summary>
        /// The straight lines Freddy (or the player) walks inside a room: hub to every doorway, spot,
        /// staircase and vent; props must stay clear of them.
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
            foreach (Staircase stairs in StairList)
            {
                if (stairs.Room == room)
                {
                    yield return (hub, new Vector2(stairs.Bottom.x, stairs.Bottom.z));
                    yield return (new Vector2(stairs.Bottom.x, stairs.Bottom.z), new Vector2(stairs.Top.x, stairs.Top.z));
                }
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
            if (room == Room.Stage)
            {
                yield return (hub, new Vector2(FreddyStart.x, FreddyStart.z));
            }
            if (room == HuntMap.Exit)
            {
                yield return (hub, new Vector2(ExitDoor.x, ExitDoor.z));
            }
        }

        /// <summary>True if <paramref name="point"/> lies on one of the walls of <paramref name="room"/>, within its height.</summary>
        private static bool OnWall(Room room, Vector3 point)
        {
            RoomShape shape = Rooms[(int)room];
            if (point.y < shape.Elevation - 0.01f || point.y > shape.Elevation + shape.Height - DoorHeight + 0.01f)
            {
                return false;
            }
            Rect a = shape.Area;
            bool onX = (Mathf.Abs(point.x - a.xMin) < 0.05f || Mathf.Abs(point.x - a.xMax) < 0.05f) && point.z > a.yMin && point.z < a.yMax;
            bool onZ = (Mathf.Abs(point.z - a.yMin) < 0.05f || Mathf.Abs(point.z - a.yMax) < 0.05f) && point.x > a.xMin && point.x < a.xMax;
            return onX || onZ;
        }

        /// <summary>True if the doorway lies on a north or south wall (the wall runs along x).</summary>
        private static bool AlongX(Room room, Vector3 point)
        {
            Rect area = Area(room);
            return Mathf.Abs(point.z - area.yMax) < 0.05f || Mathf.Abs(point.z - area.yMin) < 0.05f;
        }

        // ------------------------------------------------------------------ table helpers

        private static RoomShape Shape(float west, float south, float width, float depth, float elevation, float height)
        {
            var area = new Rect(west, south, width, depth);
            return new RoomShape { Area = area, Elevation = elevation, Height = height, Hub = area.center };
        }

        private static Passage Link(Room from, Room to, float x1, float z1, float x2, float z2, float y)
        {
            return new Passage { From = from, To = to, Points = new[] { new Vector3(x1, y, z1), new Vector3(x2, y, z2) } };
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
