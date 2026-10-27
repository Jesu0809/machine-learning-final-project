using System.Collections.Generic;

namespace NightShift.Hunt
{
    /// <summary>Rooms of the pizzeria. The numeric value is also Freddy's "go to room" action index.</summary>
    public enum Room
    {
        // Staff wing (west).
        Office = 0,
        Parts = 1,
        Restrooms = 2,
        BackHall = 3,
        Storage = 4,
        Kitchen = 5,
        // Public wing (east).
        DiningHall = 6,
        MainHall = 7,
        PartyRoom = 8,
        Arcade = 9,
        ExitLobby = 10,
        // The north wing: two dining rooms with tables and windows, the only way between the wings.
        Banquet = 11,
        Veranda = 12
    }

    /// <summary>
    /// The room graph the hunt is played on: one floor in two wings, the staff wing and the public
    /// wing, each strung along its own hall; the wings meet only through the north wing, two
    /// dining rooms with tables and windows (the banquet room off the kitchen, the veranda above
    /// the arcade). Air ducts cross between the wings, and only the player fits through them. Moving along a connection takes as many decisions as its length needs
    /// (<see cref="Cost"/>), so the simulation runs at the pace of the 3D building.
    /// See docs/ml-design.md, section 2.
    /// </summary>
    /// <remarks>
    /// <code>
    ///   STAFF WING                                PUBLIC WING
    ///                  Banquet ═══════════════ Veranda
    ///                     ║                       ║
    ///   Storage ── Kitchen          DiningHall ── Arcade
    ///      │          ║                              ║   ║
    ///   ═══ BackHall ═╝                MainHall ═════╝   ║
    ///    │     │     │                    │              ║
    ///  Office─Parts Restrooms         PartyRoom ──── ExitLobby (front exit)
    ///
    ///   Exits: the front doors (Exit Lobby) or the loading door (Storage), one of them each night.
    ///
    ///   Ducts (player only): Office–Storage, Restrooms–PartyRoom, Kitchen–Arcade, Parts–DiningHall.
    /// </code>
    /// </remarks>
    public static class HuntMap
    {
        public const int RoomCount = 13;

        /// <summary>Most hiding spots in one room (the size of Freddy's "search spot" actions).</summary>
        public const int MaxSpots = 3;

        public const Room PlayerStart = Room.Office;
        public const Room FreddyStart = Room.DiningHall;
        /// <summary>The front doors. Each night the way out is this one or the loading door (<see cref="Exits"/>).</summary>
        public const Room Exit = Room.ExitLobby;

        /// <summary>Rooms with a door out of the building; <see cref="HuntState.ExitRoom"/> picks tonight's.</summary>
        public static IReadOnlyList<Room> Exits { get; } = new[] { Room.ExitLobby, Room.Storage };

        /// <summary>
        /// The three power sockets, always in the same rooms. A socket takes one power cell; from
        /// night 2 each cell has the color of its socket: index 0 yellow (Parts), 1 blue (Banquet
        /// Room), 2 green (Party Room).
        /// </summary>
        public static IReadOnlyList<Room> SocketRooms { get; } = new[] { Room.Parts, Room.Banquet, Room.PartyRoom };

        /// <summary>The breaker box (night 3 on).</summary>
        public const Room BreakerRoom = Room.Storage;

        /// <summary>The keycard's lit pedestal (night 4 on).</summary>
        public const Room CardRoom = Room.Veranda;

        /// <summary>The card reader the keycard is swiped through.</summary>
        public const Room ReaderRoom = Room.Office;

        /// <summary>The keypad box that holds the tape (night 5).</summary>
        public const Room TapeRoom = Room.ExitLobby;

        /// <summary>Index of the socket in <paramref name="room"/>, or -1.</summary>
        public static int SocketIn(Room room)
        {
            for (int i = 0; i < SocketRooms.Count; i++)
            {
                if (SocketRooms[i] == room)
                {
                    return i;
                }
            }
            return -1;
        }

        private static readonly Room[][] Neighbors =
        {
            new[] { Room.Parts, Room.BackHall },                                       // Office
            new[] { Room.Office, Room.BackHall },                                      // Parts
            new[] { Room.BackHall },                                                   // Restrooms
            new[] { Room.Office, Room.Parts, Room.Restrooms, Room.Kitchen },           // BackHall
            new[] { Room.Kitchen },                                                    // Storage
            new[] { Room.BackHall, Room.Storage, Room.Banquet },                       // Kitchen
            new[] { Room.Arcade },                                                     // DiningHall
            new[] { Room.PartyRoom, Room.Arcade },                                     // MainHall
            new[] { Room.MainHall, Room.ExitLobby },                                   // PartyRoom
            new[] { Room.DiningHall, Room.MainHall, Room.Veranda },                    // Arcade
            new[] { Room.PartyRoom },                                                  // ExitLobby
            new[] { Room.Kitchen, Room.Veranda },                                      // Banquet
            new[] { Room.Banquet, Room.Arcade }                                        // Veranda
        };

        /// <summary>Air ducts: crawl-only shortcuts for the player. Freddy is too big for them.</summary>
        private static readonly (Room a, Room b)[] DuctList =
        {
            (Room.Office, Room.Storage),
            (Room.Restrooms, Room.PartyRoom),
            (Room.Kitchen, Room.Arcade),
            (Room.Parts, Room.DiningHall)
        };

        /// <summary>
        /// Decisions it takes to walk a connection (hub to hub in the 3D building, at Freddy's
        /// walking pace of about 10 m per decision). Every connection not listed costs one.
        /// </summary>
        private static readonly (Room a, Room b, int cost)[] LongConnections =
        {
            (Room.Office, Room.BackHall, 2),
            (Room.MainHall, Room.Arcade, 2),
            (Room.Kitchen, Room.Banquet, 2),
            (Room.Banquet, Room.Veranda, 2)
        };

        // Hiding spots per room: lockers, cabinets and under tables (the banquet room and the veranda only have tables).
        private static readonly int[] SpotCounts = { 2, 2, 2, 1, 3, 3, 3, 1, 2, 2, 1, 3, 2 };

        /// <summary>Rooms with a wall shelf where the night's power cells can wait.</summary>
        private static readonly Room[] CellRoomList =
        {
            Room.Restrooms, Room.BackHall, Room.Storage, Room.Kitchen,
            Room.DiningHall, Room.MainHall, Room.Arcade, Room.Veranda
        };

        private static readonly string[] Names =
        {
            "Office", "Parts & Service", "Restrooms", "Back Hall", "Storage", "Kitchen",
            "Dining Hall", "Main Hall", "Party Room", "Arcade", "Exit Lobby", "Banquet Room", "Veranda"
        };

        private static readonly int[,] Distances = ComputeDistances();

        public static IReadOnlyList<Room> GetNeighbors(Room room) => Neighbors[(int)room];

        public static bool AreConnected(Room from, Room to) => System.Array.IndexOf(Neighbors[(int)from], to) >= 0;

        /// <summary>The air ducts, as pairs of rooms.</summary>
        public static IReadOnlyList<(Room a, Room b)> Ducts => DuctList;

        /// <summary>Rooms the player can reach from <paramref name="room"/> through a duct.</summary>
        public static IEnumerable<Room> DuctsFrom(Room room)
        {
            foreach ((Room a, Room b) in DuctList)
            {
                if (a == room)
                {
                    yield return b;
                }
                else if (b == room)
                {
                    yield return a;
                }
            }
        }

        public static bool DuctBetween(Room a, Room b)
        {
            foreach ((Room x, Room y) in DuctList)
            {
                if ((x == a && y == b) || (x == b && y == a))
                {
                    return true;
                }
            }
            return false;
        }

        public static IReadOnlyList<Room> CellRooms => CellRoomList;

        /// <summary>True for the staff wing (west), false for the public wing (east).</summary>
        public static bool IsStaffWing(Room room) => room <= Room.Kitchen;  // the north wing counts as public

        /// <summary>Decisions it takes to walk from <paramref name="from"/> to its neighbor <paramref name="to"/> (1 or more).</summary>
        public static int Cost(Room from, Room to)
        {
            foreach ((Room a, Room b, int cost) in LongConnections)
            {
                if ((a == from && b == to) || (a == to && b == from))
                {
                    return cost;
                }
            }
            return 1;
        }

        /// <summary>Number of hiding spots in <paramref name="room"/> (1 to <see cref="MaxSpots"/>).</summary>
        public static int SpotCount(Room room) => SpotCounts[(int)room];

        /// <summary>Fewest walking moves between two rooms (ducts do not count: Freddy cannot use them).</summary>
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
