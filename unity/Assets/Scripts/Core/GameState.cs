using System;

namespace NightShift.Core
{
    /// <summary>How a night ended.</summary>
    public enum NightOutcome
    {
        InProgress,
        /// <summary>The monster got into the office. Success for the agent.</summary>
        MonsterEntered,
        /// <summary>The guard reached 6 AM. Failure for the agent.</summary>
        GuardSurvived,
        /// <summary>Safety limit of decisions reached. Neither side wins; the episode is cut.</summary>
        StepLimitReached
    }

    /// <summary>What happened when the monster applied an action. Used to compute rewards.</summary>
    public enum MoveResult
    {
        Stayed,
        MovedCloser,
        MovedAway,
        /// <summary>Moved between two rooms at the same distance from the office (Hall ↔ Kitchen).</summary>
        MovedSideways,
        /// <summary>Tried to enter through a closed door and was sent back to Hall.</summary>
        BlockedByDoor,
        EnteredOffice
    }

    /// <summary>Full record of one monster decision.</summary>
    public readonly struct MonsterStep
    {
        public MonsterStep(int action, RoomId from, RoomId to, MoveResult result, bool seenWhileMoving, bool wasIllegal)
        {
            Action = action;
            From = from;
            To = to;
            Result = result;
            SeenWhileMoving = seenWhileMoving;
            WasIllegal = wasIllegal;
        }

        /// <summary>Action actually applied (an illegal request is replaced by "stay").</summary>
        public int Action { get; }
        public RoomId From { get; }
        public RoomId To { get; }
        public MoveResult Result { get; }

        /// <summary>The monster changed room while the guard was watching the room it left or the room it reached.</summary>
        public bool SeenWhileMoving { get; }

        /// <summary>The requested action was illegal. Never happens with action masking on.</summary>
        public bool WasIllegal { get; }
    }

    /// <summary>
    /// One complete night: room graph, doors, power, cameras, clock and the monster's position.
    /// Pure C# with no Unity dependencies, so the same class runs in the playable scene (driven by
    /// a human through the UI) and in the training arena (driven by PlayerBot, many in parallel).
    /// </summary>
    /// <remarks>
    /// Order inside one monster tick (docs/ml-design.md, section 5):
    /// 1. <see cref="Tick"/>: clock, power and camera counters advance; power outage; 6 AM check.
    /// 2. The guard acts (<see cref="ViewCamera"/>, <see cref="CloseCameras"/>, <see cref="ToggleDoor"/>).
    /// 3. The monster acts (<see cref="ApplyMonsterAction"/>).
    /// A human can act at any moment between ticks; the bot acts once per tick, before the monster.
    /// </remarks>
    public class GameState
    {
        public GameState(GameSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Power = new PowerSystem(settings);
            Doors = new DoorSystem();
            Cameras = new CameraSystem(settings);
            Clock = new NightClock(settings);
            Reset();
        }

        public GameSettings Settings { get; }
        public PowerSystem Power { get; }
        public DoorSystem Doors { get; }
        public CameraSystem Cameras { get; }
        public NightClock Clock { get; }

        /// <summary>Room the monster is in.</summary>
        public RoomId MonsterRoom { get; private set; }

        /// <summary>Current result of the night.</summary>
        public NightOutcome Outcome { get; private set; }

        /// <summary>True once the night has ended for any reason.</summary>
        public bool IsOver => Outcome != NightOutcome.InProgress;

        /// <summary>Monster decisions taken this night.</summary>
        public int StepCount { get; private set; }

        /// <summary>Times the monster hit a closed door this night (evaluation metric).</summary>
        public int DoorBlocks { get; private set; }

        /// <summary>Illegal actions requested this night. Must stay at zero when masking works.</summary>
        public int IllegalActions { get; private set; }

        /// <summary>Simulated second at which the monster entered, or null if it has not.</summary>
        public float? EnteredAtSeconds { get; private set; }

        /// <summary>Simulated second at which the power ran out, or null if it has not.</summary>
        public float? PowerOutageAtSeconds { get; private set; }

        /// <summary>Raised once when the night ends.</summary>
        public event Action<NightOutcome> NightEnded;

        /// <summary>Raised once when the power runs out.</summary>
        public event Action PowerOutage;

        /// <summary>Starts a new night: full power, open doors, cameras closed, 12 AM, monster on Stage.</summary>
        public void Reset()
        {
            Power.Reset();
            Doors.Reset();
            Cameras.Reset();
            Clock.Reset();
            MonsterRoom = RoomGraph.StartRoom;
            Outcome = NightOutcome.InProgress;
            StepCount = 0;
            DoorBlocks = 0;
            IllegalActions = 0;
            EnteredAtSeconds = null;
            PowerOutageAtSeconds = null;
        }

        /// <summary>
        /// Advances time-based systems (clock, power, camera counters). Handles the power outage
        /// and the end of the night. Does not move the monster. Does nothing once the night is over.
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "Time cannot go backwards.");
            }
            if (IsOver)
            {
                return;
            }

            Clock.Tick(deltaSeconds);
            Power.Tick(deltaSeconds, Doors.ClosedCount, Cameras.IsOpen);
            Cameras.Tick(deltaSeconds);

            if (Power.IsDepleted && !Doors.IsLocked)
            {
                Doors.ForceOpenAndLock();
                Cameras.Shutdown();
                PowerOutageAtSeconds = Clock.ElapsedSeconds;
                PowerOutage?.Invoke();
            }

            if (Clock.IsOver)
            {
                End(NightOutcome.GuardSurvived);
            }
        }

        // ---------- Guard actions ----------

        /// <summary>Shows <paramref name="room"/> on the camera panel.</summary>
        /// <returns>False if the night is over or the cameras are down.</returns>
        public bool ViewCamera(RoomId room)
        {
            return !IsOver && Cameras.View(room);
        }

        /// <summary>Closes the camera panel and returns to the office view.</summary>
        public void CloseCameras()
        {
            if (!IsOver)
            {
                Cameras.Close();
            }
        }

        /// <summary>Opens or closes a door.</summary>
        /// <returns>False if the night is over or the doors are locked open by an outage.</returns>
        public bool ToggleDoor(DoorSide side)
        {
            return !IsOver && Doors.Toggle(side);
        }

        /// <summary>Sets a door to a specific state (used by the bot).</summary>
        /// <returns>True if the door changed state.</returns>
        public bool SetDoorClosed(DoorSide side, bool closed)
        {
            return !IsOver && Doors.SetClosed(side, closed);
        }

        // ---------- Monster actions ----------

        /// <summary>True if <paramref name="action"/> is legal from the monster's room. Used for action masking.</summary>
        public bool IsLegalAction(int action)
        {
            return MonsterActions.IsLegal(MonsterRoom, action);
        }

        /// <summary>
        /// Applies one monster decision. <paramref name="action"/> is the target room index
        /// (docs/ml-design.md, section 4). Illegal actions are counted and treated as "stay".
        /// </summary>
        /// <returns>What happened, so the caller can assign the reward.</returns>
        /// <exception cref="InvalidOperationException">If the night is already over.</exception>
        public MonsterStep ApplyMonsterAction(int action)
        {
            if (IsOver)
            {
                throw new InvalidOperationException("The night is over; start a new one with Reset().");
            }

            StepCount++;
            RoomId from = MonsterRoom;

            bool wasIllegal = !MonsterActions.IsLegal(from, action);
            if (wasIllegal)
            {
                IllegalActions++;
                action = MonsterActions.Stay(from);
            }

            RoomId target = (RoomId)action;
            MoveResult result;
            bool seen;

            if (target == from)
            {
                result = MoveResult.Stayed;
                seen = false;
            }
            else if (target == RoomId.Office)
            {
                if (Doors.IsClosed(RoomGraph.DoorFor(from)))
                {
                    MonsterRoom = RoomGraph.PushBackRoom;
                    DoorBlocks++;
                    result = MoveResult.BlockedByDoor;
                    seen = Cameras.IsWatching(from) || Cameras.IsWatching(MonsterRoom);
                }
                else
                {
                    MonsterRoom = RoomId.Office;
                    EnteredAtSeconds = Clock.ElapsedSeconds;
                    result = MoveResult.EnteredOffice;
                    seen = Cameras.IsWatching(from);
                }
            }
            else
            {
                MonsterRoom = target;
                int before = RoomGraph.DistanceToOffice(from);
                int after = RoomGraph.DistanceToOffice(target);
                result = after < before ? MoveResult.MovedCloser
                       : after > before ? MoveResult.MovedAway
                       : MoveResult.MovedSideways;
                seen = Cameras.IsWatching(from) || Cameras.IsWatching(target);
            }

            var step = new MonsterStep(action, from, MonsterRoom, result, seen, wasIllegal);

            if (result == MoveResult.EnteredOffice)
            {
                End(NightOutcome.MonsterEntered);
            }
            else if (StepCount >= Settings.MaxStepsPerEpisode)
            {
                End(NightOutcome.StepLimitReached);
            }

            return step;
        }

        private void End(NightOutcome outcome)
        {
            if (IsOver)
            {
                return;
            }
            Outcome = outcome;
            NightEnded?.Invoke(outcome);
        }
    }
}
