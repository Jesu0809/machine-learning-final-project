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

    /// <summary>
    /// Physical layout of the pizzeria: where each room of <see cref="HuntMap"/> is in the 3D world.
    /// The rooms keep exactly the connections of the graph the brain was trained on, but they have
    /// different sizes and heights and are joined by long halls and short corridors, so the building
    /// does not read as a grid. Inside a room Freddy walks straight lines between the room's hub,
    /// its doorways and its hiding spots; props are kept off those lines.
    /// </summary>
    /// <remarks>
    /// Top view (north up, 1 character ≈ 2 m):
    /// <code>
    ///   [ Office ]==== North Hall ====[ Storage ]
    ///       ||             ||              ||
    ///  [ Kitchen ][    Dining Hall    ][ Arcade ]
    ///       ||             ||              ||
    /// [ Show Stage ]==== South Hall ===[ Exit Lobby ]=> EXIT
    /// </code>
    /// </remarks>
    public static class PizzeriaLayout
    {
        public const float DoorWidth = 2.2f;
        public const float DoorHeight = 2.6f;
        public const float WallThickness = 0.2f;
        public const float CorridorWidth = 2.6f;

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
            public Vector2[] Points;  // from the From side to the To side
        }

        private static readonly RoomShape[] Rooms =
        {
            Shape(-16f, 8f, 8f, 7f, 3.2f),        // Office
            Shape(-8f, 9.5f, 16f, 3f, 3.0f),      // North Hall
            Shape(8f, 8f, 8f, 8f, 3.4f),          // Storage
            Shape(-19f, -5f, 10f, 11f, 3.4f),     // Kitchen
            Shape(-9f, -7f, 18f, 14f, 4.2f),      // Dining Hall
            Shape(9f, -6f, 10f, 12f, 3.6f),       // Arcade
            Shape(-21f, -19f, 13f, 11f, 5.2f),    // Show Stage
            Shape(-8f, -12.5f, 16f, 3f, 3.0f),    // South Hall
            Shape(8f, -17f, 10f, 9f, 3.6f)        // Exit Lobby
        };

        private static readonly Passage[] Passages =
        {
            Link(Room.Office, Room.WestHall, -8f, 11f),
            Link(Room.WestHall, Room.Storage, 8f, 11f),
            Link(Room.Office, Room.Kitchen, -12f, 8f, -12f, 6f),
            Link(Room.WestHall, Room.DiningHall, 0f, 9.5f, 0f, 7f),
            Link(Room.Storage, Room.Arcade, 12.5f, 8f, 12.5f, 6f),
            Link(Room.Kitchen, Room.DiningHall, -9f, 0f),
            Link(Room.DiningHall, Room.Arcade, 9f, 0f),
            Link(Room.Kitchen, Room.Stage, -14f, -5f, -14f, -8f),
            Link(Room.DiningHall, Room.EastHall, 0f, -7f, 0f, -9.5f),
            Link(Room.Arcade, Room.ExitLobby, 13.5f, -6f, 13.5f, -8f),
            Link(Room.Stage, Room.EastHall, -8f, -11f),
            Link(Room.EastHall, Room.ExitLobby, 8f, -11f)
        };

        private static readonly Spot[][] Spots =
        {
            new[] { S(SpotKind.Locker, -15.4f, 14.4f, 135f), S(SpotKind.Table, -9.4f, 13.7f, 225f) },
            new[] { S(SpotKind.Locker, -4.5f, 12.1f, 180f) },
            new[] { S(SpotKind.Locker, 15.4f, 15.4f, 225f), S(SpotKind.Cabinet, 10f, 15.4f, 180f), S(SpotKind.Cabinet, 15.4f, 10.5f, 270f) },
            new[] { S(SpotKind.Locker, -18.4f, 5.4f, 135f), S(SpotKind.Cabinet, -18.4f, -1f, 90f), S(SpotKind.Table, -17.2f, -3.8f, 45f) },
            new[] { S(SpotKind.Locker, -8.4f, 6.4f, 135f), S(SpotKind.Table, 6.4f, -5.4f, 315f), S(SpotKind.Table, -6.4f, -5.4f, 45f) },
            new[] { S(SpotKind.Locker, 18.4f, 5.4f, 225f), S(SpotKind.Cabinet, 18.4f, -4.4f, 270f) },
            new[] { S(SpotKind.Locker, -8.6f, -18.4f, 315f), S(SpotKind.Cabinet, -12.5f, -18.4f, 0f) },
            new[] { S(SpotKind.Locker, 4.5f, -12.1f, 0f) },
            new[] { S(SpotKind.Locker, 17.4f, -8.6f, 225f) }
        };

        // Wall shelves for the fuses (rooms that can hold one), with the direction the shelf faces.
        private static readonly Dictionary<Room, (Vector2 position, float facing)> FuseShelves =
            new Dictionary<Room, (Vector2, float)>
            {
                { Room.WestHall, (new Vector2(2.6f, 12.3f), 180f) },
                { Room.Storage, (new Vector2(15.65f, 13f), 270f) },
                { Room.Kitchen, (new Vector2(-18.65f, 2.6f), 90f) },
                { Room.DiningHall, (new Vector2(8.65f, 4.6f), 270f) },
                { Room.Arcade, (new Vector2(18.65f, 1.6f), 270f) },
                { Room.EastHall, (new Vector2(-3f, -12.3f), 0f) }
            };

        public static readonly Vector3 PlayerStart = new Vector3(-12f, 0.05f, 11.5f);
        public const float PlayerFacing = 90f;
        public static readonly Vector3 FreddyStart = new Vector3(-19f, 0.7f, -13.5f);
        public const float FreddyFacing = 90f;

        /// <summary>The exit door, in the south wall of the Exit Lobby.</summary>
        public static readonly Vector3 ExitDoor = new Vector3(13f, 0f, -17f);

        /// <summary>The player has escaped once south of this line.</summary>
        public const float ExitLineZ = -17.8f;

        /// <summary>Area that contains the whole building (for the maps in the UI).</summary>
        public static readonly Rect Bounds = new Rect(-22f, -20f, 42f, 37f);

        public static Rect Area(Room room) => Rooms[(int)room].Area;
        public static float Height(Room room) => Rooms[(int)room].Height;

        /// <summary>The room's hub on the floor: where Freddy walks to when the brain picks the room.</summary>
        public static Vector3 Center(Room room)
        {
            Vector2 hub = Rooms[(int)room].Hub;
            return new Vector3(hub.x, 0f, hub.y);
        }

        /// <summary>The room that contains <paramref name="position"/>; in a corridor, the nearest room.</summary>
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

        /// <summary>Waypoints through the doorway (and corridor) from <paramref name="from"/> into <paramref name="to"/>.</summary>
        public static IReadOnlyList<Vector3> PassageBetween(Room from, Room to)
        {
            foreach (Passage passage in Passages)
            {
                if (passage.From == from && passage.To == to)
                {
                    return ToWorld(passage.Points, false);
                }
                if (passage.From == to && passage.To == from)
                {
                    return ToWorld(passage.Points, true);
                }
            }
            return new[] { Center(to) };
        }

        /// <summary>One doorway per connection (on the first room's wall), with the axis of its wall.</summary>
        public static IEnumerable<(Room from, Room to, Vector3 door, bool alongX)> Doorways()
        {
            foreach (Passage passage in Passages)
            {
                Vector2 point = passage.Points[0];
                Rect area = Area(passage.From);
                bool alongX = Mathf.Abs(point.y - area.yMax) < 0.05f || Mathf.Abs(point.y - area.yMin) < 0.05f;
                yield return (passage.From, passage.To, new Vector3(point.x, 0f, point.y), alongX);
            }
        }

        /// <summary>Doorway points that lie on the walls of <paramref name="room"/>.</summary>
        public static IEnumerable<Vector2> DoorwaysOf(Room room)
        {
            foreach (Passage passage in Passages)
            {
                if (passage.From == room)
                {
                    yield return passage.Points[0];
                }
                else if (passage.To == room)
                {
                    yield return passage.Points[passage.Points.Length - 1];
                }
            }
        }

        /// <summary>Corridors between two doorways (passages with two points), as (start, end) on the floor.</summary>
        public static IEnumerable<(Vector2 start, Vector2 end, float height)> Corridors()
        {
            foreach (Passage passage in Passages)
            {
                if (passage.Points.Length == 2)
                {
                    float height = Mathf.Min(Height(passage.From), Height(passage.To));
                    yield return (passage.Points[0], passage.Points[1], Mathf.Min(height, 3f));
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
        /// The straight lines Freddy walks inside a room (hub to every doorway and to every spot);
        /// props must stay clear of them.
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
            if (room == Room.Stage)
            {
                yield return (hub, new Vector2(FreddyStart.x, FreddyStart.z));
            }
            if (room == HuntMap.Exit)
            {
                yield return (hub, new Vector2(ExitDoor.x, ExitDoor.z));
            }
        }

        // ------------------------------------------------------------------ table helpers

        private static RoomShape Shape(float west, float south, float width, float depth, float height)
        {
            var area = new Rect(west, south, width, depth);
            return new RoomShape { Area = area, Height = height, Hub = area.center };
        }

        private static Passage Link(Room from, Room to, float x, float z)
        {
            return new Passage { From = from, To = to, Points = new[] { new Vector2(x, z) } };
        }

        private static Passage Link(Room from, Room to, float x1, float z1, float x2, float z2)
        {
            return new Passage { From = from, To = to, Points = new[] { new Vector2(x1, z1), new Vector2(x2, z2) } };
        }

        private static Spot S(SpotKind kind, float x, float z, float facing)
        {
            return new Spot { Kind = kind, Position = new Vector2(x, z), Facing = facing };
        }

        private static IReadOnlyList<Vector3> ToWorld(Vector2[] points, bool reverse)
        {
            var result = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 point = points[reverse ? points.Length - 1 - i : i];
                result[i] = new Vector3(point.x, 0f, point.y);
            }
            return result;
        }
    }
}
