using System;

namespace NightShift.Hunt
{
    /// <summary>
    /// Builds Freddy's observation vector from what Freddy knows: never the player's real room or
    /// hiding spot. Every value is in [0, 1]. See docs/ml-design.md, section 5.
    /// </summary>
    /// <remarks>
    /// Layout (R = <see cref="HuntMap.RoomCount"/> = 13, so 5R + 9 = 74 values):
    ///   own room, one-hot (R)
    ///   noise memory per room (R)
    ///   last room where the player was seen, one-hot, all zero if never seen (R)
    ///   ticks since the player was seen, normalized (1)
    ///   ticks since Freddy visited each room, normalized (R)
    ///   player seen right now (1)
    ///   fuses collected, normalized (1) (the generators light up the map, so Freddy knows)
    ///   night progress (1)
    ///   spots of the current room already searched (MaxSpots = 3)
    ///   "sense" readiness, 1 = ready, falls to 0 right after using it (1)
    ///   room where "sense" last found the player, one-hot, all zero if never (R)
    ///   ticks since that, normalized (1)
    /// </remarks>
    public static class FreddyObservations
    {
        private const int R = HuntMap.RoomCount;

        public const int Size = 5 * R + 6 + HuntMap.MaxSpots;

        private const int RoomOffset = 0;
        private const int NoiseOffset = R;
        private const int LastSeenOffset = 2 * R;
        private const int SinceSeenIndex = 3 * R;
        private const int VisitedOffset = 3 * R + 1;
        private const int SeenNowIndex = 4 * R + 1;
        private const int FusesIndex = 4 * R + 2;
        private const int ProgressIndex = 4 * R + 3;
        private const int SearchedOffset = 4 * R + 4;
        private const int SenseReadyIndex = SearchedOffset + HuntMap.MaxSpots;
        private const int SensedOffset = SenseReadyIndex + 1;
        private const int SinceSensedIndex = SensedOffset + R;

        public static void Write(HuntState state, float[] output)
        {
            if (output.Length < Size)
            {
                throw new ArgumentException("Output needs " + Size + " values.", nameof(output));
            }
            Array.Clear(output, 0, Size);
            float window = Math.Max(1, state.Settings.MemoryWindowTicks);

            output[RoomOffset + (int)state.FreddyRoom] = 1f;
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                output[NoiseOffset + room] = Clamp01(state.NoiseMemory[room]);
                output[VisitedOffset + room] = Clamp01(state.TicksSinceVisited[room] / window);
            }
            if (state.LastSeenRoom.HasValue)
            {
                output[LastSeenOffset + (int)state.LastSeenRoom.Value] = 1f;
            }
            output[SinceSeenIndex] = Clamp01(state.TicksSinceSeen / window);
            output[SeenNowIndex] = state.PlayerSeen ? 1f : 0f;
            output[FusesIndex] = Clamp01(state.FusesCollected / (float)Math.Max(1, state.Settings.FuseCount));
            output[ProgressIndex] = Clamp01(state.Tick / (float)Math.Max(1, state.Settings.MaxTicks));
            for (int spot = 0; spot < HuntMap.MaxSpots; spot++)
            {
                output[SearchedOffset + spot] = state.SearchedHere[spot] ? 1f : 0f;
            }
            output[SenseReadyIndex] = 1f - Clamp01(state.SenseCooldown / (float)Math.Max(1, state.Settings.SenseCooldownTicks));
            if (state.SensedRoom.HasValue)
            {
                output[SensedOffset + (int)state.SensedRoom.Value] = 1f;
            }
            output[SinceSensedIndex] = Clamp01(state.TicksSinceSensed / window);
        }

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
