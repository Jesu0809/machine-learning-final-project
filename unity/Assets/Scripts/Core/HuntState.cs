using System;

namespace NightShift.Hunt
{
    public enum HuntOutcome
    {
        InProgress,
        /// <summary>Freddy found the player.</summary>
        Caught,
        /// <summary>The player did every objective of the night and opened the exit.</summary>
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
        /// <summary>Pick up the power cell in the current room, if the hands are free (loud).</summary>
        PickCell,
        /// <summary>Put the carried cell into the socket of the current room, if it fits (loud clunk).</summary>
        InsertCell,
        /// <summary>
        /// Work on the night's task in the current room: flip a breaker, take the keycard, swipe it,
        /// press the tape box's keypad. Some of it is loud; a failed swipe or a wrong key beeps.
        /// </summary>
        Work,
        /// <summary>Pull the exit lever; needs every objective done (very loud).</summary>
        WorkExit,
        /// <summary>
        /// Crawl through an air duct to another room (see <see cref="HuntMap.Ducts"/>): silent after
        /// the grate rattles, unseen, and takes <see cref="HuntSettings.CrawlActions"/> actions.
        /// Freddy cannot follow.
        /// </summary>
        Crawl
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
        public static PlayerAction PickCell() => new PlayerAction(PlayerActionKind.PickCell, default, -1);
        public static PlayerAction InsertCell() => new PlayerAction(PlayerActionKind.InsertCell, default, -1);
        public static PlayerAction Work() => new PlayerAction(PlayerActionKind.Work, default, -1);
        public static PlayerAction WorkExit() => new PlayerAction(PlayerActionKind.WorkExit, default, -1);
        public static PlayerAction Crawl(Room target) => new PlayerAction(PlayerActionKind.Crawl, target, -1);

        public override string ToString()
        {
            switch (Kind)
            {
                case PlayerActionKind.Walk:
                case PlayerActionKind.Run:
                case PlayerActionKind.Crawl: return Kind + " " + Target;
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

        /// <summary>He stayed or sensed in the room where the player hides, instead of searching (privileged; reward only).</summary>
        public readonly bool WaitedOutsideHiding;

        public FreddyStep(int action, Room from, Room to, int distanceBefore, int distanceAfter,
                          bool failedSearch, bool repeatedSearch, HuntOutcome outcome, bool senseUsed = false, bool senseFound = false,
                          bool waitedOutsideHiding = false)
        {
            WaitedOutsideHiding = waitedOutsideHiding;
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

        /// <summary>Decisions Freddy still spends walking a long connection (he cannot act meanwhile).</summary>
        public int FreddyTravelLeft { get; private set; }

        /// <summary>True when the policy should decide this tick: awake and not in the middle of a long walk.</summary>
        public bool FreddyCanDecide => FreddyAwake && FreddyTravelLeft == 0;

        // ---- Ground truth (Freddy's observations never read these directly) ----

        public Room FreddyRoom { get; private set; }

        /// <summary>Tonight's way out (the player knows it; Freddy does not observe it).</summary>
        public Room ExitRoom { get; private set; } = HuntMap.Exit;
        public Room PlayerRoom { get; private set; }
        public bool PlayerHidden { get; private set; }
        /// <summary>Spot the player hides in, or -1.</summary>
        public int PlayerSpot { get; private set; }
        /// <summary>Room the walking player is heading to, or null.</summary>
        public Room? PlayerWalkingTo { get; private set; }
        public int PlayerWalkProgress { get; private set; }
        /// <summary>Room at the other end of the duct the player is crawling through, or null.</summary>
        public Room? PlayerCrawlingTo { get; private set; }
        public int PlayerCrawlProgress { get; private set; }
        public bool PlayerInDuct => PlayerCrawlingTo.HasValue;
        public int ExitProgress { get; private set; }

        // ---- The night's objectives ----

        /// <summary>The night being played (1–5): it decides the objectives (see <see cref="HuntSettings.Night"/>).</summary>
        public int Night { get; private set; } = 1;

        /// <summary>Index of the cell the player carries, or -1 (one at a time).</summary>
        public int CarriedCell { get; private set; } = -1;

        /// <summary>Cells already in their socket.</summary>
        public int CellsInserted { get; private set; }

        public int BreakerProgress { get; private set; }
        public bool BreakerDone { get; private set; }
        public bool CardTaken { get; private set; }
        public bool CardSwiped { get; private set; }
        public int KeypadProgress { get; private set; }
        public bool TapeTaken { get; private set; }

        public bool NeedsBreaker => Night >= 3;
        public bool NeedsCard => Night >= 4;
        public bool NeedsTape => Night >= 5;

        /// <summary>Objectives of tonight: one per cell, the breaker, taking and swiping the card, the tape.</summary>
        public int ObjectivesTotal => Settings.CellCount + (NeedsBreaker ? 1 : 0) + (NeedsCard ? 2 : 0) + (NeedsTape ? 1 : 0);

        public int ObjectivesDone => CellsInserted + (BreakerDone ? 1 : 0) + (CardTaken ? 1 : 0) + (CardSwiped ? 1 : 0) + (TapeTaken ? 1 : 0);

        /// <summary>How close the player is to getting out, 0–1 (the generators light up the map, so Freddy knows).</summary>
        public float Progress => ObjectivesTotal == 0 ? 1f : ObjectivesDone / (float)ObjectivesTotal;

        /// <summary>True once every objective is done: the exit lever works.</summary>
        public bool ExitUnlocked => ObjectivesDone >= ObjectivesTotal;
        /// <summary>Noise level of the player's last action: 0 silent, 1 walk, 2 run, pick up or a beep, 3 exit lever.</summary>
        public int PlayerNoise { get; private set; }

        /// <summary>Player actions applied in the current tick.</summary>
        public int PlayerActionsThisTick { get; private set; }

        /// <summary>True once the player has used all their actions of this tick; Freddy acts next.</summary>
        public bool FreddyTurn => PlayerActionsThisTick >= Settings.PlayerActionsPerTick;

        // Where each cell waits (null once picked up), the socket it fits (-1: any), the sockets filled.
        private readonly Room?[] cellRoom = new Room?[MaxCells];
        private readonly int[] cellSocket = new int[MaxCells];
        private readonly bool[] socketPowered = new bool[MaxCells];
        private const int MaxCells = 3;

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
        public int DuctUses { get; private set; }

        public HuntState(HuntSettings settings, int seed)
        {
            Settings = settings ?? new HuntSettings();
            random = new Random(seed);
            Reset();
        }

        /// <summary>Index of the cell waiting in <paramref name="room"/>, or -1.</summary>
        public int CellAt(Room room)
        {
            for (int i = 0; i < CellCount; i++)
            {
                if (cellRoom[i] == room)
                {
                    return i;
                }
            }
            return -1;
        }

        public bool CellIn(Room room) => CellAt(room) >= 0;

        /// <summary>Room where cell <paramref name="cell"/> waits, or null once it was picked up.</summary>
        public Room? CellRoom(int cell) => cellRoom[cell];

        /// <summary>The socket cell <paramref name="cell"/> fits (index in <see cref="HuntMap.SocketRooms"/>), or -1 for any.</summary>
        public int CellSocket(int cell) => cellSocket[cell];

        public bool SocketPowered(int socket) => socketPowered[socket];

        public bool CellFits(int cell, int socket) => cell >= 0 && socket >= 0 && !socketPowered[socket] && (cellSocket[cell] < 0 || cellSocket[cell] == socket);

        private int CellCount => Math.Min(MaxCells, Settings.CellCount);

        /// <summary>True if <see cref="PlayerActionKind.Work"/> in <paramref name="room"/> would do something now.</summary>
        public bool HasWorkIn(Room room)
        {
            return (room == HuntMap.BreakerRoom && NeedsBreaker && !BreakerDone)
                || (room == HuntMap.CardRoom && NeedsCard && !CardTaken)
                || (room == HuntMap.ReaderRoom && CardTaken && !CardSwiped)
                || (room == HuntMap.TapeRoom && NeedsTape && !TapeTaken);
        }

        /// <summary>Starts a new night: its objectives, and the cells in random rooms.</summary>
        public void Reset()
        {
            Tick = 0;
            Outcome = HuntOutcome.InProgress;
            LastFreddyAction = -1;
            FreddyRoom = HuntMap.FreddyStart;
            PlayerRoom = HuntMap.PlayerStart;
            ExitRoom = Settings.RandomExit ? HuntMap.Exits[random.Next(HuntMap.Exits.Count)] : HuntMap.Exit;
            PlayerHidden = false;
            PlayerSpot = -1;
            PlayerWalkingTo = null;
            PlayerWalkProgress = 0;
            PlayerCrawlingTo = null;
            PlayerCrawlProgress = 0;
            FreddyTravelLeft = 0;
            ExitProgress = 0;
            Night = Settings.RandomNight ? random.Next(1, 6) : Math.Max(1, Math.Min(5, Settings.Night));
            CarriedCell = -1;
            CellsInserted = 0;
            BreakerProgress = KeypadProgress = 0;
            BreakerDone = CardTaken = CardSwiped = TapeTaken = false;
            PlayerNoise = 0;
            PlayerActionsThisTick = 0;
            HeardRoom = null;
            PlayerSeen = false;
            LastSeenRoom = null;
            TicksSinceSeen = Settings.MemoryWindowTicks;
            Searches = FailedSearches = TimesSeen = TimesHeard = SenseUses = SenseHits = DuctUses = 0;
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
            PlaceCells();
        }

        /// <summary>
        /// One cell per socket, each on a shelf in a different room: never a socket room or tonight's
        /// exit, and at least two rooms away from the socket it fits (any socket on night 1), so
        /// every cell is a trip across the building.
        /// </summary>
        private void PlaceCells()
        {
            Array.Clear(socketPowered, 0, socketPowered.Length);
            bool colored = Night >= 2;
            var candidates = HuntMap.CellRooms;
            for (int i = 0; i < MaxCells; i++)
            {
                cellRoom[i] = null;
                cellSocket[i] = colored ? i : -1;
            }
            for (int i = 0; i < CellCount; i++)
            {
                for (int attempt = 0; attempt < 60; attempt++)
                {
                    Room room = candidates[random.Next(candidates.Count)];
                    bool strict = attempt < 40;
                    if (HuntMap.SocketIn(room) >= 0 || room == ExitRoom || CellIn(room) || (strict && SocketDistance(room, cellSocket[i]) < 2))
                    {
                        continue;
                    }
                    cellRoom[i] = room;
                    break;
                }
            }
        }

        /// <summary>Rooms between <paramref name="room"/> and socket <paramref name="socket"/> (the nearest socket if -1).</summary>
        private static int SocketDistance(Room room, int socket)
        {
            if (socket >= 0)
            {
                return HuntMap.Distance(room, HuntMap.SocketRooms[socket]);
            }
            int best = int.MaxValue;
            foreach (Room s in HuntMap.SocketRooms)
            {
                best = Math.Min(best, HuntMap.Distance(room, s));
            }
            return best;
        }

        /// <summary>Puts the cells in given rooms (tests and hand-made scenarios).</summary>
        public void SetCells(params Room[] rooms)
        {
            for (int i = 0; i < MaxCells; i++)
            {
                cellRoom[i] = i < rooms.Length ? rooms[i] : (Room?)null;
            }
        }

        /// <summary>Sets tonight's way out (tests and the 3D game, which picks it with the night).</summary>
        public void SetExit(Room exit)
        {
            ExitRoom = exit;
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
            PlayerCrawlingTo = null;
            PlayerCrawlProgress = 0;
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
            if (PlayerInDuct)
            {
                // Inside a duct there is nothing to do but keep crawling: silent and out of sight.
                PlayerCrawlProgress++;
                if (PlayerCrawlProgress >= Settings.CrawlActions)
                {
                    Room exit = PlayerCrawlingTo.Value;
                    PlayerCrawlingTo = null;
                    PlayerCrawlProgress = 0;
                    LeaveHidingSpot();
                    MovePlayer(exit);
                    PlayerNoise = 1; // the grate at the other end
                }
                Sense();
                return;
            }
            switch (action.Kind)
            {
                case PlayerActionKind.Crawl:
                    if (!HuntMap.DuctBetween(PlayerRoom, action.Target))
                    {
                        break;
                    }
                    // Into the vent: the grate rattles, then the player is gone from sight.
                    PlayerNoise = 1;
                    PlayerHidden = true;
                    PlayerSpot = -1;
                    PlayerCrawlingTo = action.Target;
                    PlayerCrawlProgress = 0;
                    DuctUses++;
                    break;

                case PlayerActionKind.Walk:
                    if (!HuntMap.AreConnected(PlayerRoom, action.Target))
                    {
                        break;
                    }
                    LeaveHidingSpot();
                    PlayerNoise = 1;
                    Advance(action.Target, 1);
                    break;

                case PlayerActionKind.Run:
                    if (!HuntMap.AreConnected(PlayerRoom, action.Target))
                    {
                        break;
                    }
                    LeaveHidingSpot();
                    PlayerNoise = 2;
                    // Running covers ground twice as fast as walking.
                    Advance(action.Target, Settings.WalkTicks);
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

                case PlayerActionKind.PickCell:
                    if (CarriedCell < 0 && CellAt(PlayerRoom) >= 0)
                    {
                        LeaveHidingSpot();
                        CarriedCell = CellAt(PlayerRoom);
                        cellRoom[CarriedCell] = null;
                        PlayerNoise = 2;
                    }
                    break;

                case PlayerActionKind.InsertCell:
                    if (CellFits(CarriedCell, HuntMap.SocketIn(PlayerRoom)))
                    {
                        LeaveHidingSpot();
                        socketPowered[HuntMap.SocketIn(PlayerRoom)] = true;
                        CarriedCell = -1;
                        CellsInserted++;
                        PlayerNoise = 2;
                    }
                    break;

                case PlayerActionKind.Work:
                    if (HasWorkIn(PlayerRoom))
                    {
                        LeaveHidingSpot();
                        PlayerNoise = DoWork(PlayerRoom);
                    }
                    break;

                case PlayerActionKind.WorkExit:
                    if (PlayerRoom == ExitRoom && ExitUnlocked)
                    {
                        LeaveHidingSpot();
                        ExitProgress++;
                        PlayerNoise = 3;
                    }
                    break;
            }

            if (action.Kind != PlayerActionKind.Walk && action.Kind != PlayerActionKind.Run)
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

        /// <summary>One action of the night's task in <paramref name="room"/>; returns the noise it made.</summary>
        private int DoWork(Room room)
        {
            if (room == HuntMap.BreakerRoom && NeedsBreaker && !BreakerDone)
            {
                BreakerProgress++;
                BreakerDone = BreakerProgress >= Settings.BreakerActions;
                return 2;
            }
            if (room == HuntMap.CardRoom && NeedsCard && !CardTaken)
            {
                CardTaken = true;
                return 1;
            }
            if (room == HuntMap.ReaderRoom && CardTaken && !CardSwiped)
            {
                // Too fast, too slow: the reader beeps and the card has to go through again.
                if (random.NextDouble() < Settings.SwipeFailChance)
                {
                    return 2;
                }
                CardSwiped = true;
                return 1;
            }
            if (room == HuntMap.TapeRoom && NeedsTape && !TapeTaken)
            {
                if (random.NextDouble() < Settings.KeypadFailChance)
                {
                    KeypadProgress = 0;
                    return 2;
                }
                KeypadProgress++;
                TapeTaken = KeypadProgress >= Settings.KeypadActions;
                return 1;
            }
            return 0;
        }

        /// <summary>Progress toward a neighboring room; the player arrives when the connection's length is covered.</summary>
        private void Advance(Room target, int steps)
        {
            if (PlayerWalkingTo != target)
            {
                PlayerWalkingTo = target;
                PlayerWalkProgress = 0;
            }
            PlayerWalkProgress += steps;
            int length = Settings.WalkTicks * (Settings.TravelTime ? HuntMap.Cost(PlayerRoom, target) : 1);
            if (PlayerWalkProgress >= length)
            {
                MovePlayer(target);
            }
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

        /// <summary>The 3D player picked up the cell waiting in <paramref name="room"/>; returns its index, or -1.</summary>
        public int TakeCell(Room room)
        {
            int cell = CellAt(room);
            if (cell < 0 || CarriedCell >= 0)
            {
                return -1;
            }
            cellRoom[cell] = null;
            CarriedCell = cell;
            return cell;
        }

        /// <summary>The 3D player put the carried cell into socket <paramref name="socket"/>. False if it does not fit.</summary>
        public bool InsertCarriedCell(int socket)
        {
            if (!CellFits(CarriedCell, socket))
            {
                return false;
            }
            socketPowered[socket] = true;
            CarriedCell = -1;
            CellsInserted++;
            return true;
        }

        /// <summary>The 3D player finished one of the night's tasks (the minigames are played in the game, not here).</summary>
        public void CompleteBreaker() => BreakerDone = true;
        public void CompleteCardTaken() => CardTaken = true;
        public void CompleteCardSwipe() => CardSwiped = true;
        public void CompleteTape() => TapeTaken = true;

        /// <summary>The 3D game sets tonight's number before <see cref="Reset"/> (training draws it at random).</summary>
        public void SetNight(int night)
        {
            Settings.Night = night;
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
                if (FreddyTravelLeft > 0)
                {
                    FreddyTravelLeft--;
                }
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
            // Standing (or sensing) next to the spot the player hides in, instead of opening it.
            bool waitedOutsideHiding = PlayerHidden && PlayerRoom == FreddyRoom
                                       && (FreddyActions.IsSense(action) || action == FreddyActions.GoTo(FreddyRoom));

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
                    // A long connection keeps him walking for its extra decisions (see FreddyCanDecide).
                    FreddyTravelLeft = Settings.TravelTime ? HuntMap.Cost(FreddyRoom, target) - 1 : 0;
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
            return new FreddyStep(action, from, FreddyRoom, distanceBefore, distanceAfter, failedSearch, repeatedSearch, Outcome, senseUsed, senseFound, waitedOutsideHiding);
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
