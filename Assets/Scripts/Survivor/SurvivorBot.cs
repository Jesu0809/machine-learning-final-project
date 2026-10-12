using System;

namespace NightShift.Hunt.Survivor
{
    /// <summary>Fixed survivor skills used in evaluation.</summary>
    public enum SurvivorPreset
    {
        Novice,
        Normal,
        Expert
    }

    /// <summary>Behavior parameters of the simulated survivor. See docs/ml-design.md, section 7.</summary>
    [Serializable]
    public class SurvivorSettings
    {
        /// <summary>The motion tracker shows Freddy's room when he is at most this many rooms away.</summary>
        public int TrackerRange = 2;

        /// <summary>Chance per tick that the survivor checks the tracker and reads it.</summary>
        public float TrackerUse = 0.8f;

        /// <summary>Actions the survivor remembers where the tracker last showed Freddy.</summary>
        public int TrackerMemoryTicks = 4;

        /// <summary>When Freddy is this close (rooms), the survivor hides or flees.</summary>
        public int DangerDistance = 1;

        /// <summary>In danger, chance of hiding (if the room has a spot) instead of running away.</summary>
        public float HideChance = 0.6f;

        /// <summary>Chance of running instead of walking when nothing is chasing.</summary>
        public float RunChance = 0.2f;

        /// <summary>Calm actions spent hidden after Freddy leaves, before coming out.</summary>
        public int Patience = 3;

        /// <summary>
        /// Actions the survivor will wait for a path that passes next to Freddy before risking it
        /// anyway (people do not freeze forever).
        /// </summary>
        public int MaxWaitActions = 6;

        /// <summary>Chance of choosing spot 0, the locker, in a room with more than one spot (the "where you hide" habit).</summary>
        public float Spot0Preference = 0.5f;

        public static SurvivorSettings FromPreset(SurvivorPreset preset)
        {
            switch (preset)
            {
                case SurvivorPreset.Novice:
                    return new SurvivorSettings { TrackerUse = 0.55f, HideChance = 0.75f, RunChance = 0.4f, Patience = 2, MaxWaitActions = 3 };
                case SurvivorPreset.Expert:
                    return new SurvivorSettings { TrackerUse = 0.95f, HideChance = 0.45f, RunChance = 0.08f, Patience = 3, MaxWaitActions = 8 };
                default:
                    return new SurvivorSettings();
            }
        }

        /// <summary>
        /// Random settings for one training night (domain randomization). The ranges cover the
        /// three presets, plus a random hiding habit.
        /// </summary>
        public static SurvivorSettings Randomized(Random random)
        {
            return new SurvivorSettings
            {
                TrackerUse = Range(random, 0.5f, 0.97f),
                HideChance = Range(random, 0.4f, 0.8f),
                RunChance = Range(random, 0.05f, 0.45f),
                Patience = random.Next(2, 6),
                MaxWaitActions = random.Next(3, 11),
                Spot0Preference = Range(random, 0.1f, 0.9f)
            };
        }

        private static float Range(Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }

    /// <summary>
    /// Replaces the human during training and evaluation. Like a player with a motion tracker, it
    /// goes for the nearest fuse and then the exit, avoiding the room where it last saw Freddy;
    /// when Freddy gets close it hides or runs away, and it waits before coming out again.
    /// It only knows what a player would know: the fuse rooms and its tracker readings.
    /// </summary>
    public class SurvivorBot
    {
        private readonly Random random;
        private readonly bool randomize;
        private readonly SurvivorSettings fixedSettings;

        private Room? knownFreddy;
        private int knownAge;
        private int calmTicks;
        private int waitStreak;
        private int actionsSinceLoud = 100;
        private Room? committedWalk;

        public SurvivorSettings Settings { get; private set; }

        /// <summary>Last action, for logs and tests.</summary>
        public PlayerAction LastAction { get; private set; }

        /// <summary>A bot with random settings every night (training).</summary>
        public SurvivorBot(int seed)
        {
            random = new Random(seed);
            randomize = true;
            Settings = SurvivorSettings.Randomized(random);
        }

        /// <summary>A bot with fixed settings (evaluation and experiments).</summary>
        public SurvivorBot(int seed, SurvivorSettings settings)
        {
            random = new Random(seed);
            fixedSettings = settings;
            Settings = settings;
        }

        public void OnNightStarted(HuntState state)
        {
            Settings = randomize ? SurvivorSettings.Randomized(random) : fixedSettings;
            knownFreddy = null;
            knownAge = 0;
            calmTicks = 0;
            waitStreak = 0;
            committedWalk = null;
            actionsSinceLoud = 100;
        }

        /// <summary>Chooses and applies this tick's action.</summary>
        public void Act(HuntState state)
        {
            LastAction = Choose(state);
            state.ApplyPlayerAction(LastAction);
            actionsSinceLoud = state.PlayerNoise >= 2 ? 0 : actionsSinceLoud + 1;
        }

        private PlayerAction Choose(HuntState state)
        {
            ReadTracker(state);
            Room here = state.PlayerRoom;
            // Freddy may have come one room closer for every tick (two player actions) since the
            // last tracker reading.
            int danger = knownFreddy.HasValue ? HuntMap.Distance(here, knownFreddy.Value) - knownAge / 2 : int.MaxValue;
            bool inDanger = danger <= Settings.DangerDistance;

            if (state.PlayerHidden)
            {
                calmTicks = inDanger ? 0 : calmTicks + 1;
                if (calmTicks < Settings.Patience)
                {
                    return PlayerAction.Wait();
                }
            }
            else if (inDanger)
            {
                committedWalk = null;
                calmTicks = 0;
                int spots = HuntMap.SpotCount(here);
                Room flee = FleeRoom(here, knownFreddy.Value);
                // Freddy goes straight to where he heard a noise, so after a loud action, or in a
                // room with a single spot, get away first if there is somewhere safer to go.
                bool fleeFirst = (actionsSinceLoud < 3 || spots == 1)
                                 && HuntMap.Distance(flee, knownFreddy.Value) > HuntMap.Distance(here, knownFreddy.Value);
                if (!fleeFirst && random.NextDouble() < Settings.HideChance)
                {
                    return PlayerAction.Hide(ChooseSpot(spots));
                }
                return PlayerAction.Run(flee);
            }

            // Finish a walk already started, unless it now leads next to Freddy.
            if (committedWalk.HasValue && state.PlayerWalkingTo == committedWalk && !Blocked(committedWalk.Value))
            {
                return PlayerAction.Walk(committedWalk.Value);
            }
            committedWalk = null;

            // Freddy is close but not here: lay low for a while instead of walking around.
            if (!state.PlayerHidden && danger <= Settings.DangerDistance + 1 && calmTicks < Settings.Patience)
            {
                calmTicks++;
                return PlayerAction.Wait();
            }

            if (state.FuseIn(here))
            {
                return PlayerAction.PickFuse();
            }
            bool allFuses = state.FusesCollected >= state.Settings.FuseCount;
            if (allFuses && here == HuntMap.Exit)
            {
                return PlayerAction.WorkExit();
            }

            Room goal = allFuses ? HuntMap.Exit : NearestFuse(state);
            Room next = HuntMap.NextStep(here, goal, knownFreddy);
            if (next == here || Blocked(next))
            {
                // The way on passes next to Freddy: wait for him to move on.
                waitStreak++;
                return PlayerAction.Wait();
            }
            waitStreak = 0;
            if (random.NextDouble() < Settings.RunChance)
            {
                return PlayerAction.Run(next);
            }
            committedWalk = next;
            return PlayerAction.Walk(next);
        }

        /// <summary>
        /// True if the survivor will not step into <paramref name="room"/>: Freddy's room never, a room
        /// next to him only until the survivor has waited too long.
        /// </summary>
        private bool Blocked(Room room)
        {
            if (knownFreddy.HasValue && room == knownFreddy.Value)
            {
                return true;
            }
            return waitStreak < Settings.MaxWaitActions && NearFreddy(room);
        }

        /// <summary>True if <paramref name="room"/> is Freddy's last known room or next to it.</summary>
        private bool NearFreddy(Room room)
        {
            return knownFreddy.HasValue && HuntMap.Distance(room, knownFreddy.Value) <= 1;
        }

        private void ReadTracker(HuntState state)
        {
            knownAge++;
            if (knownAge > Settings.TrackerMemoryTicks)
            {
                knownFreddy = null;
            }
            if (random.NextDouble() < Settings.TrackerUse
                && HuntMap.Distance(state.PlayerRoom, state.FreddyRoom) <= Settings.TrackerRange)
            {
                knownFreddy = state.FreddyRoom;
                knownAge = 0;
            }
        }

        private int ChooseSpot(int spots)
        {
            if (spots == 1 || random.NextDouble() < Settings.Spot0Preference)
            {
                return 0;
            }
            return 1 + random.Next(spots - 1);
        }

        private Room FleeRoom(Room here, Room freddy)
        {
            Room best = here;
            int bestScore = int.MinValue;
            foreach (Room next in HuntMap.GetNeighbors(here))
            {
                int score = HuntMap.Distance(next, freddy) * 10 + random.Next(5);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = next;
                }
            }
            return best;
        }

        private Room NearestFuse(HuntState state)
        {
            Room best = HuntMap.Exit;
            int bestDistance = int.MaxValue;
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                if (!state.FuseIn((Room)room))
                {
                    continue;
                }
                int distance = HuntMap.Distance(state.PlayerRoom, (Room)room);
                if (knownFreddy.HasValue && HuntMap.Distance((Room)room, knownFreddy.Value) <= 1)
                {
                    // Leave the fuse next to Freddy for later if there is another one.
                    distance += 4;
                }
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = (Room)room;
                }
            }
            return best;
        }
    }
}
