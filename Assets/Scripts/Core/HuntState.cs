using System;

namespace NightShift.Hunt
{
    public enum HuntOutcome
    {
        InProgress,
        /// <summary>Freddy found the player.</summary>
        Caught,
        /// <summary>The player opened the exit with every fuse.</summary>
        Escaped,
        /// <summary>The night ran out with the player still inside.</summary>
        TimeUp
    }

    public enum PlayerActionKind
    {
        Wait,
        /// <summary>Quiet move to a neighboring room; takes <see cref="HuntSettings.WalkTicks"/> ticks.</summary>
        Walk,
        /// <summary>Loud move to a neighboring room in one tick.</summary>
        Run,
        /// <summary>Hide in a spot of the current room (silent).</summary>
        Hide,
        /// <summary>Pick up the fuse in the current room (loud).</summary>
        PickFuse,
        /// <summary>Work on the exit panel; needs every fuse (very loud).</summary>
        WorkExit
    }

    /// <summary>What the player does in one tick, from the simulated survivor or from the 3D game.</summary>
    public readonly struct PlayerAction
    {
        public readonly PlayerActionKind Kind;
        public readonly Room Target;
        public readonly int Spot;

        private PlayerAction(PlayerActionKind kind, Room target, int spot)
        {
            Kind = kind;
            Target = target;
            Spot = spot;
        }

        public static PlayerAction Wait() => new PlayerAction(PlayerActionKind.Wait, default, -1);
        public static PlayerAction Walk(Room target) => new PlayerAction(PlayerActionKind.Walk, target, -1);
        public static PlayerAction Run(Room target) => new PlayerAction(PlayerActionKind.Run, target, -1);
        public static PlayerAction Hide(int spot) => new PlayerAction(PlayerActionKind.Hide, default, spot);
        public static PlayerAction PickFuse() => new PlayerAction(PlayerActionKind.PickFuse, default, -1);
        public static PlayerAction WorkExit() => new PlayerAction(PlayerActionKind.WorkExit, default, -1);

        public override string ToString()
        {
            switch (Kind)
            {
                case PlayerActionKind.Walk:
                case PlayerActionKind.Run: return Kind + " " + Target;
                case PlayerActionKind.Hide: return "Hide " + Spot;
                default: return Kind.ToString();
            }
        }
    }

    /// <summary>Result of one of Freddy's actions, used for rewards and the AI inspector.</summary>
    public readonly struct FreddyStep
    {
        public readonly int Action;
        public readonly Room From;
        public readonly Room To;
        /// <summary>Distance to the player before and after the action (privileged; used only by the reward).</summary>
        public readonly int DistanceBefore;
        public readonly int DistanceAfter;
        public readonly bool FailedSearch;
        public readonly bool RepeatedSearch;
        public readonly bool SenseUsed;
        public readonly bool SenseFound;
        public readonly HuntOutcome Outcome;

        public FreddyStep(int action, Room from, Room to, int distanceBefore, int distanceAfter,
                          bool failedSearch, bool repeatedSearch, HuntOutcome outcome, bool senseUsed = false, bool senseFound = false)
        {
            SenseUsed = senseUsed;
            SenseFound = senseFound;
            Action = action;
            From = from;
            To = to;
            DistanceBefore = distanceBefore;
            DistanceAfter = distanceAfter;
            FailedSearch = failedSearch;
            RepeatedSearch = repeatedSearch;
            Outcome = outcome;
        }
    }

    /// <summary>
    /// The whole hunt on the room graph, without Unity. One tick is: the player acts
    /// <see cref="HuntSettings.PlayerActionsPerTick"/> times (<see cref="ApplyPlayerAction"/>),
    /// Freddy senses after each one, then Freddy acts once (<see cref="ApplyFreddyAction"/>). Training, evaluation and the 3D game all drive this
    /// same class. See docs/ml-design.md.
    /// </summary>
    public class HuntState
    {
        private readonly Random random;

        public HuntSettings Settings { get; }

        public int Tick { get; private set; }
        public HuntOutcome Outcome { get; private set; }
        public bool IsOver => Outcome != HuntOutcome.InProgress;

        /// <summary>False during the first <see cref="HuntSettings.FreddyWakeTicks"/>: Freddy does not act yet.</summary>
        public bool FreddyAwake => Tick >= Settings.FreddyWakeTicks;

        // ---- Ground truth (Freddy's observations never read these directly) ----

        public Room FreddyRoom { get; private set; }
        public Room PlayerRoom { get; private set; }
        public bool PlayerHidden { get; private set; }
        /// <summary>Spot the player hides in, or -1.</summary>
        public int PlayerSpot { get; private set; }
        /// <summary>Room the walking player is heading to, or null.</summary>
        public Room? PlayerWalkingTo { get; private set; }
        public int PlayerWalkProgress { get; private set; }
        public int FusesCollected { get; private set; }
        public int ExitProgress { get; private set; }
        /// <summary>Noise level of the player's last action: 0 silent, 1 walk, 2 run or pick up, 3 exit panel.</summary>
        public int PlayerNoise { get; private set; }

        /// <summary>Player actions applied in the current tick.</summary>
        public int PlayerActionsThisTick { get; private set; }

        /// <summary>True once the player has used all their actions of this tick; Freddy acts next.</summary>
        public bool FreddyTurn => PlayerActionsThisTick >= Settings.PlayerActionsPerTick;

        private readonly bool[] fuseInRoom = new bool[HuntMap.RoomCount];

        // ---- Freddy's knowledge (what the observations are built from) ----

        /// <summary>Fading memory of the noises heard from each room (0–1).</summary>
        public float[] NoiseMemory { get; } = new float[HuntMap.RoomCount];
        /// <summary>Room the noise came from this tick, or null if nothing was heard.</summary>
        public Room? HeardRoom { get; private set; }
        public bool PlayerSeen { get; private set; }
        public Room? LastSeenRoom { get; private set; }
        public int TicksSinceSeen { get; private set; }
        /// <summary>Ticks since Freddy last stood in each room.</summary>
        public int[] TicksSinceVisited { get; } = new int[HuntMap.RoomCount];
        /// <summary>
        /// The player's fear, 0–1, from the room graph: 1 − distance/4 to Freddy, +0.3 if he saw them
        /// in the last 4 decisions, −0.25 if they hide calmly two or more rooms away. "Sense" finds
        /// afraid players (as in Friday the 13th).
        /// </summary>
        public float PlayerFear { get; private set; }

        /// <summary>Decisions until "sense" can be used again (0 = ready).</summary>
        public int SenseCooldown { get; private set; }

        /// <summary>Room where the last successful "sense" found the player, or null.</summary>
        public Room? SensedRoom { get; private set; }
        public int TicksSinceSensed { get; private set; }
        public bool SenseReady => SenseCooldown == 0;

        /// <summary>Spots of the current room already searched since Freddy arrived.</summary>
        public bool[] SearchedHere { get; } = new bool[HuntMap.MaxSpots];

        // ---- Statistics for evaluation ----

        public int Searches { get; private set; }
        public int FailedSearches { get; private set; }
        public int TimesSeen { get; private set; }
        public int TimesHeard { get; private set; }
        public int SenseUses { get; private set; }
        public int SenseHits { get; private set; }

        public HuntState(HuntSettings settings, int seed)
        {
            Settings = settings ?? new HuntSettings();
            random = new Random(seed);
            Reset();
        }

        public bool FuseIn(Room room) => fuseInRoom[(int)room];

        /// <summary>Starts a new night with the fuses in random rooms.</summary>
        public void Reset()
        {
            Tick = 0;
            Outcome = HuntOutcome.InProgress;
            LastFreddyAction = -1;
            FreddyRoom = HuntMap.FreddyStart;
            PlayerRoom = HuntMap.PlayerStart;
            PlayerHidden = false;
            PlayerSpot = -1;
            PlayerWalkingTo = null;
            PlayerWalkProgress = 0;
            FusesCollected = 0;
            ExitProgress = 0;
            PlayerNoise = 0;
            PlayerActionsThisTick = 0;
            HeardRoom = null;
            PlayerSeen = false;
            LastSeenRoom = null;
            TicksSinceSeen = Settings.MemoryWindowTicks;
            Searches = FailedSearches = TimesSeen = TimesHeard = SenseUses = SenseHits = 0;
            PlayerFear = 0f;
            SenseCooldown = Math.Min(10, Settings.SenseCooldownTicks);
            SensedRoom = null;
            TicksSinceSensed = Settings.MemoryWindowTicks;
            Array.Clear(NoiseMemory, 0, NoiseMemory.Length);
            Array.Clear(SearchedHere, 0, SearchedHere.Length);
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                TicksSinceVisited[room] = Settings.MemoryWindowTicks;
            }
            TicksSinceVisited[(int)FreddyRoom] = 0;
            PlaceFuses();
        }

        private void PlaceFuses()
        {
            Array.Clear(fuseInRoom, 0, fuseInRoom.Length);
            // Never in the start rooms or at the exit, so the player always has to cross the map.
            int placed = 0;
            while (placed < Settings.FuseCount)
            {
                var room = (Room)random.Next(HuntMap.RoomCount);
                if (room == HuntMap.PlayerStart || room == HuntMap.FreddyStart || room == HuntMap.Exit || fuseInRoom[(int)room])
                {
                    continue;
                }
                fuseInRoom[(int)room] = true;
                placed++;
            }
        }

        /// <summary>Puts a fuse in a specific room (tests and hand-made scenarios).</summary>
        public void SetFuses(params Room[] rooms)
        {
            Array.Clear(fuseInRoom, 0, fuseInRoom.Length);
            foreach (Room room in rooms)
            {
                fuseInRoom[(int)room] = true;
            }
        }

        /// <summary>Puts the player and Freddy in given rooms (tests and the 3D game's sync).</summary>
        public void Place(Room player, Room freddy)
        {
            PlayerRoom = player;
            FreddyRoom = freddy;
            PlayerHidden = false;
            PlayerSpot = -1;
            PlayerWalkingTo = null;
            PlayerWalkProgress = 0;
        }

        // =====================================================================
        // Player phase
        // =====================================================================

        /// <summary>
        /// Applies one player action, then lets Freddy hear and look. After
        /// <see cref="HuntSettings.PlayerActionsPerTick"/> of these (see <see cref="FreddyTurn"/>)
        /// comes exactly one <see cref="ApplyFreddyAction"/>, unless the night ended.
        /// </summary>
        public void ApplyPlayerAction(PlayerAction action)
        {
            if (IsOver)
            {
                return;
            }

            if (PlayerActionsThisTick == 0)
            {
                // First action of the tick: what Freddy perceived last tick is no longer "now".
                HeardRoom = null;
                PlayerSeen = false;
            }
            PlayerActionsThisTick++;
            PlayerNoise = 0;
            switch (action.Kind)
            {
                case PlayerActionKind.Walk:
                    if (!HuntMap.AreConnected(PlayerRoom, action.Target))
                    {
                        break;
                    }
                    LeaveHidingSpot();
                    PlayerNoise = 1;
                    if (PlayerWalkingTo != action.Target)
                    {
                        PlayerWalkingTo = action.Target;
                        PlayerWalkProgress = 0;
                    }
                    PlayerWalkProgress++;
                    if (PlayerWalkProgress >= Settings.WalkTicks)
                    {
                        MovePlayer(action.Target);
                    }
                    break;

                case PlayerActionKind.Run:
                    if (!HuntMap.AreConnected(PlayerRoom, action.Target))
                    {
                        break;
                    }
                    LeaveHidingSpot();
                    PlayerNoise = 2;
                    MovePlayer(action.Target);
                    break;

                case PlayerActionKind.Hide:
                    if (action.Spot >= 0 && action.Spot < HuntMap.SpotCount(PlayerRoom))
                    {
                        PlayerHidden = true;
                        PlayerSpot = action.Spot;
                        PlayerWalkingTo = null;
                        PlayerWalkProgress = 0;
                    }
                    break;

                case PlayerActionKind.PickFuse:
                    if (fuseInRoom[(int)PlayerRoom])
                    {
                        LeaveHidingSpot();
                        fuseInRoom[(int)PlayerRoom] = false;
                        FusesCollected++;
                        PlayerNoise = 2;
                    }
                    break;

                case PlayerActionKind.WorkExit:
                    if (PlayerRoom == HuntMap.Exit && FusesCollected >= Settings.FuseCount)
                    {
                        LeaveHidingSpot();
                        ExitProgress++;
                        PlayerNoise = 3;
                    }
                    break;
            }

            if (action.Kind != PlayerActionKind.Walk)
            {
                PlayerWalkingTo = null;
                PlayerWalkProgress = 0;
            }

            if (ExitProgress >= Settings.ExitWorkTicks)
            {
                Outcome = HuntOutcome.Escaped;
                return;
            }

            // Walking into Freddy's room in plain sight (a sleeping Freddy notices too).
            if (SpotsPlayer())
            {
                Outcome = HuntOutcome.Caught;
                return;
            }

            Sense();
        }

        private void LeaveHidingSpot()
        {
            PlayerHidden = false;
            PlayerSpot = -1;
        }

        private void MovePlayer(Room room)
        {
            // Work done on the exit panel is kept: the player can leave and come back.
            PlayerRoom = room;
            PlayerWalkingTo = null;
            PlayerWalkProgress = 0;
        }

        /// <summary>True if the player is in Freddy's room, not hidden, and Freddy sees them this tick.</summary>
        private bool SpotsPlayer()
        {
            return Settings.ResolveCatches && PlayerRoom == FreddyRoom && !PlayerHidden && random.NextDouble() < Settings.SightSameRoomChance;
        }

        /// <summary>Freddy's hearing and sight after one player action; updates only what Freddy knows.</summary>
        private void Sense()
        {
            UpdateFear();
            int distance = HuntMap.Distance(FreddyRoom, PlayerRoom);
            if (PlayerNoise > 0 && distance <= Settings.HearingRange[PlayerNoise])
            {
                Room source = PlayerRoom;
                if (distance > 0 && random.NextDouble() < Settings.NoiseMisplaceChance)
                {
                    var neighbors = HuntMap.GetNeighbors(PlayerRoom);
                    source = neighbors[random.Next(neighbors.Count)];
                }
                HeardRoom = source;
                NoiseMemory[(int)source] = Math.Max(NoiseMemory[(int)source], PlayerNoise / 3f);
                TimesHeard++;
            }

            if (!PlayerHidden && distance == 1 && random.NextDouble() < Settings.SightAdjacentChance)
            {
                PlayerSeen = true;
                LastSeenRoom = PlayerRoom;
                TicksSinceSeen = 0;
                TimesSeen++;
            }
        }

        // =====================================================================
        // 3D game bridge: the real player is measured, not simulated
        // =====================================================================

        /// <summary>
        /// One player action measured in the 3D game: where the player is, whether they hide and how
        /// much noise they made since the last call. Replaces <see cref="ApplyPlayerAction"/> there.
        /// </summary>
        public void ObservePlayer(Room room, bool hidden, int spot, int noise)
        {
            if (IsOver)
            {
                return;
            }
            if (PlayerActionsThisTick == 0)
            {
                HeardRoom = null;
                PlayerSeen = false;
            }
            PlayerActionsThisTick++;
            PlayerRoom = room;
            PlayerHidden = hidden;
            PlayerSpot = hidden ? spot : -1;
            PlayerNoise = Math.Max(0, Math.Min(3, noise));
            Sense();
        }

        /// <summary>
        /// A noise that is not the player (a thrown party horn): Freddy hears it with the same
        /// rules as any other noise. True if it reached him. His brain cannot tell it from the player.
        /// </summary>
        public bool HearNoiseAt(Room source, int level)
        {
            level = Math.Max(0, Math.Min(3, level));
            if (IsOver || level == 0 || HuntMap.Distance(FreddyRoom, source) > Settings.HearingRange[level])
            {
                return false;
            }
            HeardRoom = source;
            NoiseMemory[(int)source] = Math.Max(NoiseMemory[(int)source], level / 3f);
            TimesHeard++;
            return true;
        }

        /// <summary>Freddy's eyes in the 3D game saw the player (raycast), wherever Freddy is.</summary>
        public void ReportSighting(Room room)
        {
            PlayerSeen = true;
            LastSeenRoom = room;
            TicksSinceSeen = 0;
            TimesSeen++;
        }

        /// <summary>Puts Freddy's brain in the room where his body really is (after a chase in the 3D game).</summary>
        public void PlaceFreddy(Room room)
        {
            if (room != FreddyRoom)
            {
                FreddyRoom = room;
                Array.Clear(SearchedHere, 0, SearchedHere.Length);
            }
            TicksSinceVisited[(int)room] = 0;
        }

        /// <summary>The 3D player picked up the fuse of <paramref name="room"/>. False if there was none.</summary>
        public bool CollectFuse(Room room)
        {
            if (!fuseInRoom[(int)room])
            {
                return false;
            }
            fuseInRoom[(int)room] = false;
            FusesCollected++;
            return true;
        }

        /// <summary>Ends the night from the 3D game (the body caught the player, or the door opened).</summary>
        public void End(HuntOutcome outcome)
        {
            if (!IsOver && outcome != HuntOutcome.InProgress)
            {
                Outcome = outcome;
            }
        }

        // =====================================================================
        // Freddy phase
        // =====================================================================

        public bool IsLegalFreddyAction(int action)
        {
            return FreddyActions.IsLegal(FreddyRoom, action) && (!FreddyActions.IsSense(action) || SenseReady);
        }

        private void UpdateFear()
        {
            int distance = HuntMap.Distance(FreddyRoom, PlayerRoom);
            float fear = 1f - distance / 4f;
            if (TicksSinceSeen <= 4)
            {
                fear += 0.3f;
            }
            if (PlayerHidden && distance >= 2)
            {
                fear -= 0.25f;
            }
            PlayerFear = Math.Max(0f, Math.Min(1f, fear));
        }

        /// <summary>Freddy's last applied action, or -1 at the start of the night.</summary>
        public int LastFreddyAction { get; private set; } = -1;

        /// <summary>Ends the tick while Freddy is still asleep (<see cref="FreddyAwake"/> is false).</summary>
        public void SkipFreddyTurn()
        {
            if (!IsOver)
            {
                AdvanceClock();
            }
        }

        /// <summary>
        /// Applies Freddy's action and advances the clock one tick. An illegal action is treated as
        /// "stay" (it should never happen: illegal actions are masked).
        /// </summary>
        public FreddyStep ApplyFreddyAction(int action)
        {
            if (IsOver)
            {
                return new FreddyStep(action, FreddyRoom, FreddyRoom, 0, 0, false, false, Outcome);
            }
            if (!IsLegalFreddyAction(action))
            {
                action = FreddyActions.GoTo(FreddyRoom);
            }

            LastFreddyAction = action;
            Room from = FreddyRoom;
            int distanceBefore = HuntMap.Distance(FreddyRoom, PlayerRoom);
            bool failedSearch = false;
            bool repeatedSearch = false;
            bool senseUsed = false;
            bool senseFound = false;

            if (FreddyActions.IsSense(action))
            {
                senseUsed = true;
                SenseUses++;
                SenseCooldown = Settings.SenseCooldownTicks;
                UpdateFear();
                if (PlayerFear >= Settings.SenseFearThreshold)
                {
                    senseFound = true;
                    SenseHits++;
                    SensedRoom = PlayerRoom;
                    TicksSinceSensed = 0;
                }
            }
            else if (FreddyActions.IsMove(action))
            {
                var target = (Room)action;
                if (target != FreddyRoom)
                {
                    FreddyRoom = target;
                    Array.Clear(SearchedHere, 0, SearchedHere.Length);
                }
                if (SpotsPlayer())
                {
                    Outcome = HuntOutcome.Caught;
                }
            }
            else
            {
                int spot = FreddyActions.SpotOf(action);
                Searches++;
                repeatedSearch = SearchedHere[spot];
                SearchedHere[spot] = true;
                if (Settings.ResolveCatches && PlayerHidden && PlayerRoom == FreddyRoom && PlayerSpot == spot
                    && random.NextDouble() < Settings.SearchFindChance)
                {
                    Outcome = HuntOutcome.Caught;
                }
                else
                {
                    failedSearch = true;
                    FailedSearches++;
                }
            }

            AdvanceClock();
            int distanceAfter = HuntMap.Distance(FreddyRoom, PlayerRoom);
            return new FreddyStep(action, from, FreddyRoom, distanceBefore, distanceAfter, failedSearch, repeatedSearch, Outcome, senseUsed, senseFound);
        }

        private void AdvanceClock()
        {
            Tick++;
            PlayerActionsThisTick = 0;
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                NoiseMemory[room] *= Settings.NoiseDecay;
            }
            TicksSinceSeen = Math.Min(TicksSinceSeen + 1, Settings.MemoryWindowTicks);
            TicksSinceSensed = Math.Min(TicksSinceSensed + 1, Settings.MemoryWindowTicks);
            SenseCooldown = Math.Max(0, SenseCooldown - 1);
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                TicksSinceVisited[room] = Math.Min(TicksSinceVisited[room] + 1, Settings.MemoryWindowTicks);
            }
            TicksSinceVisited[(int)FreddyRoom] = 0;

            if (!IsOver && Tick >= Settings.MaxTicks)
            {
                Outcome = HuntOutcome.TimeUp;
            }
        }
    }
}
