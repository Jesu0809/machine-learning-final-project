using System;

namespace NightShift.Hunt.Brain
{
    /// <summary>
    /// Upper baseline: a hand-written hunter with the same information as the trained agent.
    /// Chase what it sees, investigate what it hears (searching the spots there), otherwise patrol
    /// the room it has not visited for the longest time. Nothing in it is learned.
    /// </summary>
    public class ScriptedFreddyPolicy : IFreddyPolicy
    {
        private const float NoiseThreshold = 0.15f;
        private const int RecentSightTicks = 6;

        private readonly Random random;

        // Rooms already investigated, with the tick: their old noise is ignored until a new one.
        private readonly int[] investigatedAt = new int[HuntMap.RoomCount];
        private Room? patrolTarget;

        public ScriptedFreddyPolicy(int seed)
        {
            random = new Random(seed);
        }

        public string PolicyName => "scripted";

        public void OnNightStarted(HuntState state)
        {
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                investigatedAt[room] = -1;
            }
            patrolTarget = null;
        }

        public void Decide(HuntState state)
        {
            state.ApplyFreddyAction(Choose(state));
        }

        public void OnNightEnded(HuntState state, HuntOutcome outcome)
        {
        }

        private int Choose(HuntState state)
        {
            Room here = state.FreddyRoom;
            if (state.HeardRoom.HasValue)
            {
                // A new noise makes that room interesting again.
                investigatedAt[(int)state.HeardRoom.Value] = -1;
            }
            if (state.PlayerSeen && state.LastSeenRoom.HasValue)
            {
                investigatedAt[(int)state.LastSeenRoom.Value] = -1;
            }
            if (state.TicksSinceSensed == 0 && state.SensedRoom.HasValue)
            {
                // A fresh "sense" result makes that room interesting again.
                investigatedAt[(int)state.SensedRoom.Value] = -1;
            }

            // 1. Chase the player it just saw, or check the place it was seen recently.
            if (state.LastSeenRoom.HasValue && state.TicksSinceSeen <= RecentSightTicks
                && investigatedAt[(int)state.LastSeenRoom.Value] < 0)
            {
                int action = Investigate(state, state.LastSeenRoom.Value);
                if (action >= 0)
                {
                    return action;
                }
            }

            // 2. Go to the loudest room it has not investigated yet.
            Room? loudest = null;
            float best = NoiseThreshold;
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                if (state.NoiseMemory[room] > best && investigatedAt[room] < 0)
                {
                    best = state.NoiseMemory[room];
                    loudest = (Room)room;
                }
            }
            if (loudest.HasValue)
            {
                int action = Investigate(state, loudest.Value);
                if (action >= 0)
                {
                    return action;
                }
            }

            // 3. Go where "sense" found the player, or use it when he has no other lead.
            if (state.SensedRoom.HasValue && state.TicksSinceSensed <= RecentSightTicks && investigatedAt[(int)state.SensedRoom.Value] < 0)
            {
                int action = Investigate(state, state.SensedRoom.Value);
                if (action >= 0)
                {
                    return action;
                }
            }
            if (state.SenseReady && state.TicksSinceSeen > RecentSightTicks)
            {
                if (state.TicksSinceSensed == 0 && state.SensedRoom.HasValue)
                {
                    investigatedAt[(int)state.SensedRoom.Value] = -1;
                }
                return FreddyActions.Sense;
            }

            // 4. Patrol toward the stalest room, searching one spot on the way now and then.
            if (!patrolTarget.HasValue || patrolTarget.Value == here)
            {
                patrolTarget = Stalest(state);
            }
            if (random.NextDouble() < 0.2)
            {
                int spot = UnsearchedSpot(state);
                if (spot >= 0)
                {
                    return FreddyActions.Search(spot);
                }
            }
            return FreddyActions.GoTo(HuntMap.NextStep(here, patrolTarget.Value));
        }

        /// <summary>Walks to <paramref name="room"/> and searches its spots; -1 once all are searched.</summary>
        private int Investigate(HuntState state, Room room)
        {
            if (state.FreddyRoom != room)
            {
                return FreddyActions.GoTo(HuntMap.NextStep(state.FreddyRoom, room));
            }
            int spot = UnsearchedSpot(state);
            if (spot >= 0)
            {
                return FreddyActions.Search(spot);
            }
            investigatedAt[(int)room] = state.Tick;
            return -1;
        }

        private int UnsearchedSpot(HuntState state)
        {
            int count = HuntMap.SpotCount(state.FreddyRoom);
            int start = random.Next(count);
            for (int i = 0; i < count; i++)
            {
                int spot = (start + i) % count;
                if (!state.SearchedHere[spot])
                {
                    return spot;
                }
            }
            return -1;
        }

        private Room Stalest(HuntState state)
        {
            Room best = state.FreddyRoom;
            int bestScore = -1;
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                if (room == (int)state.FreddyRoom)
                {
                    continue;
                }
                int score = state.TicksSinceVisited[room] * 10 + random.Next(10);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = (Room)room;
                }
            }
            return best;
        }
    }
}
