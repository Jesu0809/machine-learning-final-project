using System;
using NightShift.Core;
using NightShift.Monster;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>Which monster the Night scene uses.</summary>
    public enum MonsterKind
    {
        Random,
        Scripted,
        Trained
    }

    /// <summary>What the Night scene is showing.</summary>
    public enum NightPhase
    {
        Menu,
        Playing,
        Ended
    }

    /// <summary>
    /// Runs the playable night: owns the <see cref="GameState"/>, advances it in real time and asks
    /// the monster for a decision every <see cref="GameSettings.DecisionIntervalSeconds"/>.
    /// UI components only read the state from here and send the guard's input back.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class NightController : MonoBehaviour
    {
        [SerializeField] private GameSettings settings = new GameSettings();
        [SerializeField] private MonsterKind monsterKind = MonsterKind.Random;

        [Tooltip("Required for Monster Kind = Trained. Its Behavior Type must be Inference Only.")]
        [SerializeField] private MonsterAgent trainedMonster;

        [Tooltip("0 = a different random seed every night.")]
        [SerializeField] private int seed;

        private readonly System.Random seedSource = new System.Random();
        private float decisionTimer;
        private RoomId lastMonsterRoom;
        private RoomId lastCamera = RoomId.Stage;

        /// <summary>The current night.</summary>
        public GameState State { get; private set; }

        /// <summary>The policy moving the monster this night.</summary>
        public IMonsterPolicy Monster { get; private set; }

        public NightPhase Phase { get; private set; } = NightPhase.Menu;

        /// <summary>Unscaled time at which <see cref="Phase"/> last changed (for screen animations).</summary>
        public float PhaseStartedAt { get; private set; }

        /// <summary>Nights started since the scene was opened.</summary>
        public int NightNumber { get; private set; }

        /// <summary>Seconds until the monster's next decision.</summary>
        public float SecondsToNextDecision => decisionTimer;

        public MonsterKind SelectedMonster
        {
            get => monsterKind;
            set => monsterKind = value;
        }

        public bool HasTrainedModel => trainedMonster != null;

        /// <summary>Raised when the monster changes room (from, to).</summary>
        public event Action<RoomId, RoomId> MonsterMoved;

        /// <summary>Raised when the guard switches to another camera.</summary>
        public event Action<RoomId> CameraChanged;

        private void Awake()
        {
            EnsureState();
        }

        /// <summary>Creates the game state if it does not exist yet.</summary>
        public void EnsureState()
        {
            if (State == null)
            {
                State = new GameState(settings);
            }
        }

        private void Update()
        {
            HandleKeyboard();
            Advance(Time.deltaTime);
        }

        /// <summary>Starts a new night with the selected monster.</summary>
        public void StartNight()
        {
            EnsureState();
            State.Reset();
            Monster = CreatePolicy();
            Monster.OnNightStarted(State);
            decisionTimer = settings.DecisionIntervalSeconds;
            lastMonsterRoom = State.MonsterRoom;
            lastCamera = RoomId.Stage;
            NightNumber++;
            SetPhase(NightPhase.Playing);
        }

        /// <summary>Leaves the current night and shows the menu.</summary>
        public void GoToMenu()
        {
            SetPhase(NightPhase.Menu);
        }

        /// <summary>
        /// Advances the night by <paramref name="deltaSeconds"/>: time-based systems first, then a
        /// monster decision when the timer runs out. Public so the night can also be simulated
        /// without the real clock.
        /// </summary>
        public void Advance(float deltaSeconds)
        {
            if (Phase != NightPhase.Playing)
            {
                return;
            }

            State.Tick(deltaSeconds);

            if (!State.IsOver)
            {
                decisionTimer -= deltaSeconds;
                if (decisionTimer <= 0f)
                {
                    decisionTimer += settings.DecisionIntervalSeconds;
                    Monster.Decide(State);
                }
            }

            // The trained agent applies its action in a later physics step, so movement is detected
            // by watching the room instead of right after Decide().
            if (State.MonsterRoom != lastMonsterRoom)
            {
                RoomId from = lastMonsterRoom;
                lastMonsterRoom = State.MonsterRoom;
                MonsterMoved?.Invoke(from, lastMonsterRoom);
            }

            if (State.IsOver)
            {
                Monster.OnNightEnded(State, State.Outcome);
                SetPhase(NightPhase.Ended);
            }
        }

        // ---------- Guard actions (buttons and keys) ----------

        public void ViewCamera(RoomId room)
        {
            if (Phase == NightPhase.Playing && State.ViewCamera(room))
            {
                lastCamera = room;
                CameraChanged?.Invoke(room);
            }
        }

        public void CloseCameras()
        {
            if (Phase == NightPhase.Playing)
            {
                State.CloseCameras();
            }
        }

        /// <summary>Opens the cameras on the last room viewed, or closes them.</summary>
        public void ToggleCameras()
        {
            if (State.Cameras.IsOpen)
            {
                CloseCameras();
            }
            else
            {
                ViewCamera(lastCamera);
            }
        }

        public void ToggleDoor(DoorSide side)
        {
            if (Phase == NightPhase.Playing)
            {
                State.ToggleDoor(side);
            }
        }

        private void HandleKeyboard()
        {
            if (Phase != NightPhase.Playing)
            {
                return;
            }

            for (int camera = 0; camera < RoomGraph.ObservableRoomCount; camera++)
            {
                if (KeyboardInput.WasPressed(GameKey.Camera1 + camera))
                {
                    ViewCamera((RoomId)camera);
                }
            }
            if (KeyboardInput.WasPressed(GameKey.ToggleCameras))
            {
                ToggleCameras();
            }
            if (KeyboardInput.WasPressed(GameKey.LeftDoor))
            {
                ToggleDoor(DoorSide.Left);
            }
            if (KeyboardInput.WasPressed(GameKey.RightDoor))
            {
                ToggleDoor(DoorSide.Right);
            }
            if (KeyboardInput.WasPressed(GameKey.Back))
            {
                GoToMenu();
            }
        }

        private IMonsterPolicy CreatePolicy()
        {
            int policySeed = seed != 0 ? seed + NightNumber : seedSource.Next();
            switch (monsterKind)
            {
                case MonsterKind.Scripted:
                    return new ScriptedMonsterPolicy(policySeed);
                case MonsterKind.Trained when trainedMonster != null:
                    return trainedMonster;
                case MonsterKind.Trained:
                    Debug.LogWarning("No trained monster is assigned to NightController; using the random monster.");
                    return new RandomMonsterPolicy(policySeed);
                default:
                    return new RandomMonsterPolicy(policySeed);
            }
        }

        private void SetPhase(NightPhase phase)
        {
            Phase = phase;
            PhaseStartedAt = Time.unscaledTime;
        }
    }
}
