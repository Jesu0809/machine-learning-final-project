namespace NightShift.Hunt
{
    /// <summary>
    /// Freddy's discrete actions, one branch of <see cref="Count"/> (18). Actions 0–13 mean "go to
    /// that room" (choosing the current room means stay and listen); 14–16 search hiding spot 0, 1
    /// or 2 of the current room; 17 is "sense", which reveals the player's room if they are afraid
    /// (it has a long cooldown). Illegal actions are masked. See docs/ml-design.md, section 4.
    /// </summary>
    public static class FreddyActions
    {
        public const int SearchSpot0 = HuntMap.RoomCount;
        public const int Sense = HuntMap.RoomCount + HuntMap.MaxSpots;
        public const int Count = Sense + 1;

        public static int GoTo(Room room) => (int)room;

        public static int Search(int spot) => SearchSpot0 + spot;

        public static bool IsMove(int action) => action >= 0 && action < HuntMap.RoomCount;

        public static bool IsSearch(int action) => action >= SearchSpot0 && action < Sense;

        public static bool IsSense(int action) => action == Sense;

        public static int SpotOf(int action) => action - SearchSpot0;

        /// <summary>True if Freddy may take <paramref name="action"/> while standing in <paramref name="room"/>.</summary>
        public static bool IsLegal(Room room, int action)
        {
            if (IsMove(action))
            {
                return action == (int)room || HuntMap.AreConnected(room, (Room)action);
            }
            if (IsSense(action))
            {
                return true; // whether it has cooled down is checked by HuntState
            }
            return IsSearch(action) && SpotOf(action) < HuntMap.SpotCount(room);
        }

        public static string Describe(int action)
        {
            if (IsSense(action))
            {
                return "sense";
            }
            return IsSearch(action) ? "search spot " + SpotOf(action) : "go to " + HuntMap.DisplayName((Room)action);
        }
    }
}
