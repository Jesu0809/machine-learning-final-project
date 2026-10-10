using System.Collections.Generic;

namespace NightShift.Core
{
    /// <summary>Rooms of the map. The numeric value is also the monster's action index.</summary>
    public enum RoomId
    {
        Stage = 0,
        Hall = 1,
        WestCorridor = 2,
        Kitchen = 3,
        EastCorridor = 4,
        Office = 5
    }

    /// <summary>The two office doors.</summary>
    public enum DoorSide
    {
        Left,
        Right
    }

    /// <summary>
    /// Fixed graph of rooms the monster moves on. The monster jumps between connected rooms;
    /// there is no physical navigation. See docs/ml-design.md, section 2.
    /// </summary>
    /// <remarks>
    /// Stage ─ Hall ─ WestCorridor ─[left door]─ Office
    ///          │
    ///        Kitchen ─ EastCorridor ─[right door]─ Office
    /// </remarks>
    public static class RoomGraph
    {
        /// <summary>Number of rooms the monster can stand in (Office excluded).</summary>
        public const int ObservableRoomCount = 5;

        /// <summary>Total number of rooms, Office included.</summary>
        public const int RoomCount = 6;

        /// <summary>Room where the monster starts every night.</summary>
        public const RoomId StartRoom = RoomId.Stage;

        /// <summary>Room the monster is sent back to after hitting a closed door.</summary>
        public const RoomId PushBackRoom = RoomId.Hall;

        private static readonly RoomId[][] Neighbors =
        {
            new[] { RoomId.Hall },                                            // Stage
            new[] { RoomId.Stage, RoomId.WestCorridor, RoomId.Kitchen },      // Hall
            new[] { RoomId.Hall, RoomId.Office },                             // WestCorridor
            new[] { RoomId.Hall, RoomId.EastCorridor },                       // Kitchen
            new[] { RoomId.Kitchen, RoomId.Office },                          // EastCorridor
            new RoomId[0]                                                     // Office (terminal)
        };

        // Shortest-path distance (in moves) from each room to the Office.
        private static readonly int[] DistanceToOfficeTable = { 3, 2, 1, 2, 1, 0 };

        /// <summary>Rooms directly connected to <paramref name="room"/>.</summary>
        public static IReadOnlyList<RoomId> GetNeighbors(RoomId room) => Neighbors[(int)room];

        /// <summary>True if the monster can go from <paramref name="from"/> to <paramref name="to"/> in one move.</summary>
        public static bool AreConnected(RoomId from, RoomId to)
        {
            return System.Array.IndexOf(Neighbors[(int)from], to) >= 0;
        }

        /// <summary>Number of moves from <paramref name="room"/> to the Office.</summary>
        public static int DistanceToOffice(RoomId room) => DistanceToOfficeTable[(int)room];

        /// <summary>True for the two corridors next to a door.</summary>
        public static bool IsDoorRoom(RoomId room) => room == RoomId.WestCorridor || room == RoomId.EastCorridor;

        /// <summary>The door between a corridor and the Office.</summary>
        /// <exception cref="System.ArgumentException">If <paramref name="room"/> is not a door room.</exception>
        public static DoorSide DoorFor(RoomId room)
        {
            switch (room)
            {
                case RoomId.WestCorridor: return DoorSide.Left;
                case RoomId.EastCorridor: return DoorSide.Right;
                default: throw new System.ArgumentException($"{room} is not next to a door.", nameof(room));
            }
        }
    }
}
