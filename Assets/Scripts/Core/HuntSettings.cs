using System;

namespace NightShift.Hunt
{
    /// <summary>
    /// Tunable numbers of one hunt. Serializable so the same values can be edited in the Inspector
    /// of the game scene and of the training arena. See docs/ml-design.md, section 3.
    /// </summary>
    [Serializable]
    public class HuntSettings
    {
        /// <summary>
        /// Real seconds between two of Freddy's decisions in the 3D game (the RL time step). A room
        /// is 10 m wide, so one room per decision is a walking pace of about 3 m/s.
        /// </summary>
        public float DecisionIntervalSeconds = 3.5f;

        /// <summary>
        /// True in training and evaluation: catches follow the probabilistic rules of section 3 of
        /// docs/ml-design.md. False in the 3D game, where Freddy's body catches the player physically.
        /// </summary>
        public bool ResolveCatches = true;

        /// <summary>Decisions before the night ends with the player still inside (about 10 minutes).</summary>
        public int MaxTicks = 240;

        /// <summary>Ticks Freddy stays still on the stage at the start, so the player can get going.</summary>
        public int FreddyWakeTicks = 6;

        /// <summary>Chance that Freddy spots a player who is not hidden in his own room (rooms have corners).</summary>
        public float SightSameRoomChance = 0.75f;

        /// <summary>
        /// The night being played (1–5), which sets the objectives: night 1 three cells that fit any
        /// socket; night 2 each cell has its socket's color; night 3 adds the breaker; night 4 the
        /// keycard; night 5 the tape. Ignored when <see cref="RandomNight"/> is true.
        /// </summary>
        public int Night = 1;

        /// <summary>True in training and evaluation: every hunt plays a random night (1–5).</summary>
        public bool RandomNight = true;

        /// <summary>
        /// Each night three of <see cref="HuntMap.SocketCandidates"/> are live, at random (freddy_v12);
        /// false: always <see cref="HuntMap.DefaultSockets"/>.
        /// </summary>
        public bool RandomSockets = true;

        /// <summary>Power cells, one for each of tonight's live sockets (<see cref="HuntState.SocketRooms"/>). The player carries one at a time.</summary>
        public int CellCount = 3;

        /// <summary>Player actions flipping the tripped breakers back up (each flip is a loud clack).</summary>
        public int BreakerActions = 4;

        /// <summary>Chance that a keycard swipe is too fast or too slow: an error beep, try again.</summary>
        public float SwipeFailChance = 0.35f;

        /// <summary>Player actions pressing 1 to 9 on the tape box's keypad.</summary>
        public int KeypadActions = 5;

        /// <summary>Chance per keypad action of a wrong key: a buzz, and the sequence starts over.</summary>
        public float KeypadFailChance = 0.15f;

        /// <summary>True: each night the way out is a random one of <see cref="HuntMap.Exits"/>; false: always the front doors.</summary>
        public bool RandomExit = true;

        /// <summary>Player actions at the exit: pulling the lever and the doors swinging open (very loud).</summary>
        public int ExitWorkTicks = 2;

        /// <summary>
        /// Player actions per Freddy decision. With 2, Freddy (one room per decision) walks as fast
        /// as a walking player, and a running player is faster than him.
        /// </summary>
        public int PlayerActionsPerTick = 2;

        /// <summary>Player actions a walk into the next room takes (running takes one).</summary>
        public int WalkTicks = 2;

        /// <summary>
        /// Long connections take several decisions to walk (<see cref="HuntMap.Cost"/>), for Freddy
        /// and for the player, like distances in the 3D building. The 3D game turns it off: there
        /// Freddy's body really walks, and the brain only decides again when he arrives.
        /// </summary>
        public bool TravelTime = true;

        /// <summary>Player actions it takes to crawl through an air duct.</summary>
        public int CrawlActions = 4;

        /// <summary>
        /// Player actions after coming out of a duct before another one can be entered (14 ≈ 25 s in
        /// the 3D game; freddy_v15): the vents are an escape, not a way to stay out of reach forever.
        /// </summary>
        public int DuctCooldownActions = 14;

        /// <summary>Chance that searching the right spot finds the player (otherwise they held their breath).</summary>
        public float SearchFindChance = 0.8f;

        /// <summary>Decisions before Freddy can use "sense" again (about 1 minute 45 s in the 3D game).</summary>
        public int SenseCooldownTicks = 30;

        /// <summary>"Sense" reveals the player only if their fear is at least this (0–1).</summary>
        public float SenseFearThreshold = 0.5f;

        /// <summary>Farthest room distance at which Freddy hears each noise level (index = level 0–3).</summary>
        public int[] HearingRange = { -1, 0, 1, 2 };

        /// <summary>Chance that a heard noise is placed in a neighbor of the real room (sound is vague).</summary>
        public float NoiseMisplaceChance = 0.25f;

        /// <summary>Chance, per player action, that Freddy sees a player who is not hidden in a neighboring room.</summary>
        public float SightAdjacentChance = 0.2f;

        /// <summary>Seconds-like window that normalizes the "time since" observations.</summary>
        public int MemoryWindowTicks = 60;

        /// <summary>How much the noise memory fades every Freddy decision.</summary>
        public float NoiseDecay = 0.8f;
    }
}
