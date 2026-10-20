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

        /// <summary>Fuses the player must collect before the exit can be opened.</summary>
        public int FuseCount = 3;

        /// <summary>Player actions of loud work at the exit panel to open the door.</summary>
        public int ExitWorkTicks = 6;

        /// <summary>
        /// Player actions per Freddy decision. With 2, Freddy (one room per decision) walks as fast
        /// as a walking player, and a running player is faster than him.
        /// </summary>
        public int PlayerActionsPerTick = 2;

        /// <summary>Player actions a walk into the next room takes (running takes one).</summary>
        public int WalkTicks = 2;

        /// <summary>Player actions it takes to crawl through an air duct.</summary>
        public int CrawlActions = 4;

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
