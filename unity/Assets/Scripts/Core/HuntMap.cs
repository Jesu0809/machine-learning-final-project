using System.Collections.Generic;

namespace NightShift.Hunt
{
    /// <summary>Rooms of the pizzeria. The numeric value is also Freddy's "go to room" action index.</summary>
    public enum Room
    {
        Office = 0,
        WestHall = 1,
        Storage = 2,
        Kitchen = 3,
        DiningHall = 4,
        Arcade = 5,
        Stage = 6,
        EastHall = 7,
        ExitLobby = 8
    }

    /// <summary>
    /// The room graph the hunt is played on. Freddy's brain decides on this graph; in the 3D game
    /// each room is a real space and the body walks between them with the NavMesh.
    /// See docs/ml-design.md, section 2.
    /// </summary>
    /// <remarks>
    /// Office ──── WestHall ──── Storage
    ///   │            │             │
    /// Kitchen ─── DiningHall ── Arcade
    ///   │            │             │
    /// Stage ───── EastHall ──── ExitLobby
    /// </remarks>
    public static class HuntMap
    {
        public const int RoomCount = 9;

        /// <summary>Most hiding spots in one room (the size of Freddy's "search spot" actions).</summary>
        public const int MaxSpots = 3;

        public const Room PlayerStart = Room.Office;
        public const Room FreddyStart = Room.Stage;
        public const Room Exit = Room.ExitLobby;

        private static readonly Room[][] Neighbors =
        {
            new[] { Room.WestHall, Room.Kitchen },                          // Office
            new[] { Room.Office, Room.Storage, Room.DiningHall },           // WestHall
            new[] { Room.WestHall, Room.Arcade },                           // Storage
            new[] { Room.Office, Room.DiningHall, Room.Stage },             // Kitchen
            new[] { Room.WestHall, Room.Kitchen, Room.Arcade, Room.EastHall }, // DiningHall
            new[] { Room.Storage, Room.DiningHall, Room.ExitLobby },        // Arcade
            new[] { Room.Kitchen, Room.EastHall },                          // Stage
            new[] { Room.DiningHall, Room.Stage, Room.ExitLobby },          // EastHall
            new[] { Room.Arcade, Room.EastHall }                            // ExitLobby
        };

        // Hiding spots per room: spot 0 is always a locker, then cabinets and under tables.
        // Halls and the lobby have only one.
        private static readonly int[] SpotCounts = { 2, 1, 3, 3, 3, 2, 2, 1, 1 };

        private static readonly string[] Names =
        {
            "Office", "West Hall", "Storage", "Kitchen", "Dining Hall", "Arcade", "Stage", "East Hall", "Exit Lobby"
        };

        private static readonly int[,] Distances = ComputeDistances();

        public static IReadOnlyList<Room> GetNeighbors(Room room) => Neighbors[(int)room];

        public static bool AreConnected(Room from, Room to) => System.Array.IndexOf(Neighbors[(int)from], to) >= 0;

        /// <summary>Number of hiding spots in <paramref name="room"/> (1 to <see cref="MaxSpots"/>).</summary>
        public static int SpotCount(Room room) => SpotCounts[(int)room];

        /// <summary>Fewest moves between two rooms.</summary>
        public static int Distance(Room from, Room to) => Distances[(int)from, (int)to];

        /// <summary>Longest shortest path in the map, used to normalize distances.</summary>
        public static int Diameter { get; } = ComputeDiameter();

        public static string DisplayName(Room room) => Names[(int)room];

        /// <summary>
        /// First room on a shortest path from <paramref name="from"/> to <paramref name="to"/> that
        /// avoids <paramref name="avoid"/> when possible. Returns <paramref name="from"/> if already there.
        /// </summary>
        public static Room NextStep(Room from, Room to, Room? avoid = null)
        {
            if (from == to)
            {
                return from;
            }
            Room best = from;
            int bestScore = int.MaxValue;
            foreach (Room next in Neighbors[(int)from])
            {
                int score = Distance(next, to) * 10;
                if (avoid.HasValue && next == avoid.Value)
                {
                    score += 1000;
                }
                else if (avoid.HasValue && Distance(next, avoid.Value) == 1)
                {
                    // Prefer not to pass next to the danger either, if it costs nothing extra.
                    score += 5;
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    best = next;
                }
            }
            return best;
        }

        private static int[,] ComputeDistances()
        {
            var result = new int[RoomCount, RoomCount];
            for (int start = 0; start < RoomCount; start++)
            {
                for (int i = 0; i < RoomCount; i++)
                {
                    result[start, i] = -1;
                }
                var queue = new Queue<int>();
                queue.Enqueue(start);
                result[start, start] = 0;
                while (queue.Count > 0)
                {
                    int room = queue.Dequeue();
                    foreach (Room next in Neighbors[room])
                    {
                        if (result[start, (int)next] < 0)
                        {
                            result[start, (int)next] = result[start, room] + 1;
                            queue.Enqueue((int)next);
                        }
                    }
                }
            }
            return result;
        }

        private static int ComputeDiameter()
        {
            int max = 0;
            for (int a = 0; a < RoomCount; a++)
            {
                for (int b = 0; b < RoomCount; b++)
                {
                    max = System.Math.Max(max, Distances[a, b]);
                }
            }
            return max;
        }
    }
}
