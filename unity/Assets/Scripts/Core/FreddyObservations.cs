using System;

namespace NightShift.Hunt
{
    /// <summary>
    /// Builds Freddy's observation vector from what Freddy knows: never the player's real room or
    /// hiding spot. Every value is in [0, 1]. See docs/ml-design.md, section 5.
    /// </summary>
    /// <remarks>
    /// Layout (54 values):
    ///   0–8   own room, one-hot
    ///   9–17  noise memory per room
    ///   18–26 last room where the player was seen, one-hot (all zero if never seen)
    ///   27    ticks since the player was seen, normalized
    ///   28–36 ticks since Freddy visited each room, normalized
    ///   37    player seen right now
    ///   38    fuses collected, normalized (the generators light up the map, so Freddy knows)
    ///   39    night progress
    ///   40–42 spots of the current room already searched
    ///   43    "sense" readiness (1 = ready, falls to 0 right after using it)
    ///   44–52 room where "sense" last found the player, one-hot (all zero if never)
    ///   53    ticks since that, normalized
    /// </remarks>
    public static class FreddyObservations
    {
        public const int Size = 54;

        private const int RoomOffset = 0;
        private const int NoiseOffset = 9;
        private const int LastSeenOffset = 18;
        private const int SinceSeenIndex = 27;
        private const int VisitedOffset = 28;
        private const int SeenNowIndex = 37;
        private const int FusesIndex = 38;
        private const int ProgressIndex = 39;
        private const int SearchedOffset = 40;
        private const int SenseReadyIndex = 43;
        private const int SensedOffset = 44;
        private const int SinceSensedIndex = 53;

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
