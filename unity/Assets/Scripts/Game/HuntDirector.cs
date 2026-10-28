using System.Collections;
using System.Collections.Generic;
using NightShift.Hunt;
using NightShift.Hunt.Brain;
using NightShift.UI;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightShift.Game
{
    public enum GamePhase
    {
        Menu,
        Playing,
        Paused,
        Caught,
        Escaped,
        TimeUp
    }

    /// <summary>One of the brain's decisions, kept for the AI inspector and the end-of-night replay.</summary>
    public struct DecisionRecord
    {
        public float Time;
        public int Action;
        public string Text;
        /// <summary>Probability the policy gave to the chosen action (-1 if unknown).</summary>
        public float Confidence;
        public Room FreddyRoom;
        public Room? Heard;
        public Room? LastSeen;
        public Vector3 Position;
    }

    /// <summary>
    /// Entry point of the game scene. Builds the pizzeria, the player, Freddy's body and brain and
    /// the UI, and connects the 3D world to the hunt the brain was trained on:
    /// <list type="number">
    /// <item>Twice per decision interval it measures the real player (room, hidden or not, noise)
    /// and feeds it to <see cref="HuntState.ObservePlayer"/>; Freddy hears with the same rules as in
    /// training.</item>
    /// <item>When the body is ready, it asks the policy (the trained .onnx model through
    /// ML-Agents, or the scripted baseline if there is no model) for the next action, and records
    /// how likely the policy found every option (<see cref="PolicyProbe"/>).</item>
    /// <item>The body carries the action out; what it sees is reported back to the brain.</item>
    /// </list>
    /// It also runs the five nights: the story intro, each night's phone call, the objectives and
    /// what gets harder from night to night.
    /// </summary>
    public class HuntDirector : MonoBehaviour
    {
        private const float TrailInterval = 0.5f;
        public const int LastNight = 5;

        private HuntSettings settings;
        private float sampleTimer;
        private bool decisionPending;
        private int decisionTick;
        private float trackerTimer;
        private float heartbeatTimer;
        private float trailTimer;
        private PolicyProbe probe;
        private readonly float[] observations = new float[FreddyObservations.Size];

        private PhoneCall call;
        private IntroCinematic intro;
        private bool wasInCall;
        private bool itemsRevealed;
        private BreakerBox breakerBox;
        private KeycardPedestal cardPedestal;
        private CardReader cardReader;
        private TapeBox tapeBox;
        private readonly List<LurePickup> lures = new List<LurePickup>();
        private readonly List<NotePickup> notes = new List<NotePickup>();
        private bool pointerWasLocked;

        private Volume volume;
        private ChromaticAberration chromatic;
        private LensDistortion distortion;
        private FilmGrain grain;
        private Vignette vignette;
        private ColorAdjustments colorAdjustments;
        private const float BaseExposure = 0.9f;
        private Light jumpscareLight;

        public GameAssets Assets { get; private set; }
        public GameAudio Audio { get; private set; }
        public Level Level { get; private set; }
        public PlayerController Player { get; private set; }
        public FreddyBody Freddy { get; private set; }
        public HuntState State { get; private set; }
        public IFreddyPolicy Policy { get; private set; }
        public FreddyAgent Agent { get; private set; }
        public GameUI UI { get; private set; }

        public GamePhase Phase { get; private set; } = GamePhase.Menu;
        public bool Playing => Phase == GamePhase.Playing;

        /// <summary>"trained with ML-Agents (freddy_v3)" or "scripted baseline …", shown in the menu and the inspector.</summary>
        public string BrainDescription { get; private set; }

        public int CellsNeeded => settings.CellCount;

        /// <summary>Seconds of the night played so far (the clock on the HUD).</summary>
        public float NightSeconds { get; private set; }

        public float NightLengthSeconds => settings.MaxTicks * settings.DecisionIntervalSeconds;

        /// <summary>Room where the brain heard the last noise, for the inspector.</summary>
        public Room? LastHeard { get; private set; }

        public string LastDecision { get; private set; } = "-";

        /// <summary>Probability of each action at the last decision (all 0 if there is no probe).</summary>
        public float[] Probabilities { get; } = new float[FreddyActions.Count];
        public bool[] LegalActions { get; } = new bool[FreddyActions.Count];
        public bool HasProbabilities { get; private set; }

        /// <summary>The action chosen at the decision the probabilities belong to (-1 while it is pending).</summary>
        public int ProbedChoice { get; private set; } = -1;

        /// <summary>Freddy's room when the last decision was made (actions are relative to it).</summary>
        public Room DecisionRoom { get; private set; }

        /// <summary>The brain's decisions this night, oldest first.</summary>
        public List<DecisionRecord> Decisions { get; } = new List<DecisionRecord>();

        /// <summary>Freddy's and the player's positions every half second (the end-of-night replay).</summary>
        public List<Vector3> FreddyTrail { get; } = new List<Vector3>();
        public List<Vector3> PlayerTrail { get; } = new List<Vector3>();

        /// <summary>The night being played (1–5). Each night Freddy decides more often and walks faster.</summary>
        public int Night { get; private set; } = 1;

        /// <summary>Best night reached, remembered between sessions.</summary>
        public static int BestNight
        {
            get => PlayerPrefs.GetInt("NightShift.BestNight", 1);
            private set { PlayerPrefs.SetInt("NightShift.BestNight", value); PlayerPrefs.Save(); }
        }

        /// <summary>Time the night began (the title card fades from it).</summary>
        public float NightStartedAt { get; private set; }

        /// <summary>The steps between the player and the exit tonight.</summary>
        public Objectives ObjectiveList { get; } = new Objectives();

        /// <summary>Motion tracker range in meters; it loses range on the last nights.</summary>
        public float TrackerRange { get; private set; } = 25f;

        /// <summary>The street, the front of the building and the car (seen in the intro).</summary>
        public Exterior Exterior { get; private set; }

        /// <summary>The tape, after the last night (see EndingCinematic).</summary>
        public EndingCinematic Ending { get; private set; }

        /// <summary>True while the intro cinematic plays.</summary>
        public bool InIntro => intro != null && intro.Playing;

        /// <summary>The part of the intro playing ("tape", "cards", "drive", "foot", "door", "inside") and seconds into it.</summary>
        public (string part, float time) IntroPart => intro != null ? (intro.Part, intro.PartTime) : ("", 0f);

        /// <summary>
        /// True while the phone call plays and the night has not started (nights 1–4): Freddy sleeps,
        /// the objectives are hidden and the building is free to explore. On night 5 he hunts during the call.
        /// </summary>
        public bool InCall => call != null && call.Active && Night < LastNight;

        public string CallSubtitle => call != null ? call.Subtitle : "";

        /// <summary>Time of the last "sense" and whether it found the player (for the screen effect).</summary>
        public float LastSenseAt { get; private set; } = -100f;
        public bool LastSenseFound { get; private set; }

        /// <summary>How the night ended, in words, for the end screen.</summary>
        public string EndReason { get; private set; } = "";

        /// <summary>True while the browser has not captured the mouse yet (the HUD asks for a click).</summary>
        public bool NeedsClickToLook { get; private set; }

        /// <summary>Editor only: when true, pressing Play goes straight into night 1 (no menu, no intro). Set to false for the normal flow.</summary>
        public static readonly bool QuickStartInEditor = true;

        public void SkipCall() => call.Skip();

        public void SkipIntro() => intro.Skip();

        private void Awake()
        {
            Assets = GameAssets.Load();
            if (Assets == null)
            {
                Assets = ScriptableObject.CreateInstance<GameAssets>();
                Debug.LogWarning("Resources/GameAssets.asset is missing: run Night Shift/Create Game Scene.");
            }
            // In 3D the body really walks; the brain decides again when he arrives.
            settings = new HuntSettings { ResolveCatches = false, TravelTime = false, RandomNight = false };
            State = new HuntState(settings, System.Environment.TickCount);
            if (Assets.PolicyWeights != null)
            {
                probe = PolicyProbe.FromBytes(Assets.PolicyWeights.bytes);
                if (probe.InputSize != FreddyObservations.Size || probe.OutputSize != FreddyActions.Count)
                {
                    // Weights of a brain trained on another map: no probabilities rather than wrong ones.
                    probe = null;
                }
            }

            SetUpAtmosphere();
            Audio = GameAudio.Create(transform, Assets);
            GraphicsQuality.Load(Assets.AmbientOcclusion);
            Level = LevelBuilder.Build(transform, Assets);
            Player = PlayerController.Create(this, PizzeriaLayout.PlayerStart, PizzeriaLayout.PlayerFacing);
            Freddy = FreddyBody.Create(this, Assets.FreddyPrefab, PizzeriaLayout.FreddyStart, PizzeriaLayout.FreddyFacing);
            SecurityFeeds.Create(this);
            Material doorSteel = BuildKit.Surface("doorSteel", () => ProceduralTextures.SteelPanel(new Color(0.42f, 0.09f, 0.08f)), 0.4f, 0.35f);
            foreach ((Room a, Room b, Vector3 door, bool alongX, Vector3 corridor) in PizzeriaLayout.Doorways())
            {
                Door.Create(this, Level.Root, a, b, door, alongX, corridor, doorSteel);
            }
            OfficeSeat.Create(this, Level.Root, LevelProps.OfficeChairPose);
            Plush.Create(this, Level.Root, LevelProps.OfficePlushPose);
            AiVision.Create(this);
            call = PhoneCall.Create(transform, Audio, Assets);
            Ending = EndingCinematic.Create(this);
            Exterior = Exterior.Build(transform, Assets);
            intro = IntroCinematic.Create(this, Exterior);
            // The night's tasks: the three power sockets, the breaker box on the storage's south
            // wall, the keycard on its pedestal in the veranda, the reader on the office wall and
            // the tape box on the ticket booth's counter.
            for (int socket = 0; socket < Level.Sockets.Length; socket++)
            {
                Level.Sockets[socket] = PowerSocket.Build(this, Level.Root, socket);
            }
            breakerBox = BreakerBox.Create(this, Level.Root, new Vector3(-20.4f, 1.4f, 2.32f), 0f);
            cardPedestal = KeycardPedestal.Create(this, Level.Root, PizzeriaLayout.CardPedestal);
            cardReader = CardReader.Create(this, Level.Root, PizzeriaLayout.ReaderMount, PizzeriaLayout.ReaderFacing);
            tapeBox = TapeBox.Create(this, Level.Root, LevelProps.TapeBoxPose.position, LevelProps.TapeBoxPose.rotation);
            GraphicsQuality.Apply();
            CreateBrain();
            UI = GameUI.Create(this);
#if UNITY_EDITOR
            if (QuickStartInEditor)
            {
                // Testing shortcut: skip the menu and the intro and drop straight into night 1.
                StartNight(1);
                CaptureMode.AttachIfRequested(this);
                ExploreMode.AttachIfRequested(this);
                return;
            }
#endif
            ShowMenu();
            CaptureMode.AttachIfRequested(this);
            ExploreMode.AttachIfRequested(this);
        }

        private void CreateBrain()
        {
            // The exported weights travel with the model: if they do not fit this map, neither does the model.
            bool modelFits = Assets.PolicyWeights == null || probe != null;
            if (Assets.FreddyModel != null && modelFits)
            {
                var brain = new GameObject("FreddyBrain");
                brain.transform.SetParent(transform, false);
                Agent = FreddyBrain.Add(brain, BehaviorType.InferenceOnly, Assets.FreddyModel);
                Policy = Agent;
                BrainDescription = "trained with ML-Agents (" + Assets.FreddyModel.name + ")";
            }
            else
            {
                Policy = new ScriptedFreddyPolicy(System.Environment.TickCount);
                BrainDescription = Assets.FreddyModel != null
                    ? "scripted baseline (the model " + Assets.FreddyModel.name + " was trained on another map)"
                    : "scripted baseline (no trained model found)";
                probe = null;
            }
        }

        // ------------------------------------------------------------------ phases

        public void ShowMenu()
        {
            StopAllCoroutines();
            ResetEffects();
            Phase = GamePhase.Menu;
            Time.timeScale = 1f;
            Player.Controllable = false;
            Player.CameraLocked = true;
            // The menu looks at Freddy, asleep on the stage, from the audience.
            Freddy.ResetTo(PizzeriaLayout.FreddyStart, PizzeriaLayout.FreddyFacing);
            Player.Head.position = new Vector3(5.4f, 1.55f, 4.2f);
            // Aim a little to his side so he stands right of the menu buttons.
            Player.Head.LookAt(PizzeriaLayout.FreddyStart + new Vector3(-1.8f, 1.3f, 0f));
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Audio.PlayMenuMusic(true);
            Audio.PlayAmbient(false);
            UI.ShowMenu();
        }

        /// <summary>Plays the story intro, then night 1.</summary>
        public void PlayStory()
        {
            if (Assets.IntroLines == null || Assets.IntroLines.Length == 0)
            {
                StartNight(1);
                return;
            }
            Audio.PlayMenuMusic(false);
            intro.Play();
        }

        public void StartNight() => StartNight(1);

        public void RetryNight() => StartNight(Night);

        public void NextNight() => StartNight(Mathf.Min(Night + 1, LastNight));

        /// <summary>
        /// Starts night <paramref name="night"/>. The trained brain is the same every night; what
        /// changes is the pace and the building: a decision every 3.5 s on night 1 down to 2.3 s on
        /// night 5, Freddy 7% faster per night, "sense" recharging faster, dead lights, a shorter
        /// tracker, fewer party horns, more objectives, and on night 5 he is awake from the start.
        /// </summary>
        public void StartNight(int night)
        {
            Night = Mathf.Clamp(night, 1, LastNight);
            settings.DecisionIntervalSeconds = 3.5f - (Night - 1) * 0.3f;
            Freddy.NightSpeed = 1f + (Night - 1) * 0.07f;
            settings.SenseCooldownTicks = Night >= 5 ? 16 : Night >= 4 ? 20 : 30;
            settings.FreddyWakeTicks = Night >= 5 ? 0 : 6;
            TrackerRange = Night >= 5 ? 15f : Night >= 4 ? 18f : 25f;
            FlickerLight.KillLights(Night >= 4 ? 0.5f : Night >= 3 ? 0.3f : 0f, Night * 97);
            NightStartedAt = Time.time;

            StopAllCoroutines();
            ResetEffects();
            settings.Night = Night;
            State.Reset();
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                if (Level.Cells[room] != null)
                {
                    Level.Cells[room].gameObject.SetActive(false);
                }
            }
            foreach (PowerSocket socket in Level.Sockets)
            {
                socket.ResetSocket(Night >= 2);
            }
            TonightExit = State.ExitRoom;
            foreach (ExitPanel panel in new[] { Level.Exit, Level.BackExit })
            {
                panel.ResetPanel();
                panel.Active = panel.Room == State.ExitRoom;
            }
            foreach ((Room exit, GameObject sign) in Level.ExitSigns)
            {
                sign.SetActive(exit == State.ExitRoom);
            }
            ObjectiveList.Begin(State);
            breakerBox.ResetTask(Night * 97 + System.Environment.TickCount % 1000);
            cardPedestal.ResetTask(State.NeedsCard);
            cardReader.ResetTask();
            tapeBox.ResetTask();
            PlaceNotes();
            Player.ResetTo(PizzeriaLayout.PlayerStart, PizzeriaLayout.PlayerFacing);
            Player.ClearHands();
            Player.CameraLocked = false;
            Player.Controllable = true;
            Freddy.ResetTo(PizzeriaLayout.FreddyStart, PizzeriaLayout.FreddyFacing);
            Policy.OnNightStarted(State);
            PlaceLures();
            Door.ResetAll();
            call.Begin(Night);
            wasInCall = InCall;
            itemsRevealed = false;
            if (InCall)
            {
                // The guard listens to the call in the office: its doors are locked until it ends.
                Door.LockRoom(Room.Office, true);
            }
            if (!InCall)
            {
                RevealItems();
            }
            if (State.FreddyAwake && !InCall)
            {
                // Night 5: awake from the first second. Without this his body stayed asleep on the
                // stage (it was only woken when the sleeping ticks ran out, and there were none).
                Freddy.WakeUp();
                LastDecision = "awake";
            }

            LastSenseAt = -100f;
            sampleTimer = 0f;
            trailTimer = 0f;
            decisionPending = false;
            NightSeconds = 0f;
            sixAM = false;
            lastHour = 0;
            Player.Fatigue = 0f;
            LastHeard = null;
            LastDecision = "asleep on the stage";
            HasProbabilities = false;
            Decisions.Clear();
            FreddyTrail.Clear();
            PlayerTrail.Clear();
            EndReason = "";
            Phase = GamePhase.Playing;
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            Audio.PlayMenuMusic(false);
            Audio.PlayAmbient(true);
            UI.ShowHud();
        }

        /// <summary>
        /// Developer explore mode (see ExploreMode.cs): night 1's building with every objective item
        /// out, no phone call and Freddy asleep on the stage. The loop in Update stops here.
        /// </summary>
        public void BeginExplore()
        {
            StartNight(1);
            call.Skip();
            wasInCall = false;
            Door.LockRoom(Room.Office, false);
            RevealItems();
            LastDecision = "asleep (explore mode)";
        }

        /// <summary>
        /// Eyes getting used to the dark (0–1), while hidden with the flashlight off: the picture
        /// brightens by up to 1.5 stops, so the room outside the vents can be watched.
        /// </summary>
        public void SetEyeAdaptation(float amount)
        {
            colorAdjustments.postExposure.Override(BaseExposure + 1.5f * Mathf.Clamp01(amount));
        }

        /// <summary>The night's task objects (the capture tour drives them).</summary>
        public BreakerBox Breaker => breakerBox;
        public KeycardPedestal CardPedestal => cardPedestal;
        public CardReader Reader => cardReader;
        public TapeBox Tape => tapeBox;

        public void SetPaused(bool paused)
        {
            if (paused && Phase == GamePhase.Playing)
            {
                Phase = GamePhase.Paused;
                Time.timeScale = 0f;
                Player.Controllable = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                UI.ShowPause();
            }
            else if (!paused && Phase == GamePhase.Paused)
            {
                Phase = GamePhase.Playing;
                Time.timeScale = 1f;
                Player.Controllable = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                Audio.PlayAmbient(true);
                UI.ShowHud();
            }
        }

        public void RefreshSound()
        {
            Audio.PlayMenuMusic(Phase == GamePhase.Menu && !InIntro);
            Audio.PlayAmbient(Phase == GamePhase.Playing);
        }

        private void OnApplicationFocus(bool focused)
        {
            // Alt-tab or clicking another window pauses the night (and frees the mouse).
            if (!focused && Phase == GamePhase.Playing && !Application.isEditor && !CaptureMode.Active)
            {
                SetPaused(true);
            }
        }

        // ------------------------------------------------------------------ the loop

        private void Update()
        {
            Controls.Poll();
            if (Controls.PausePressed && !InIntro && Player.InFocus)
            {
                // Esc at a panel steps back from it instead of pausing.
                Player.EndFocus();
            }
            else if (Controls.PausePressed && !InIntro)
            {
                if (Phase == GamePhase.Playing)
                {
                    SetPaused(true);
                }
                else if (Phase == GamePhase.Paused)
                {
                    SetPaused(false);
                }
            }
            if (Controls.InspectorPressed && (Phase == GamePhase.Playing || Phase == GamePhase.Paused))
            {
                UI.ToggleInspector();
            }

            FlickerLight.SetFreddyPosition(Phase == GamePhase.Menu ? new Vector3(9999f, 0f, 9999f) : Freddy.transform.position);
            UpdatePointerLock();
            if (Phase != GamePhase.Playing || ExploreMode.Active)
            {
                return;
            }

            if (Controls.SkipPressed)
            {
                call.Skip();
            }
            if (InCall)
            {
                // The call: the building is free to explore, the night has not started.
                return;
            }
            if (!itemsRevealed)
            {
                RevealItems();
            }
            if (wasInCall)
            {
                wasInCall = false;
                Door.LockRoom(Room.Office, false);
                while (!State.FreddyAwake && !State.IsOver)
                {
                    State.SkipFreddyTurn();
                }
                Freddy.WakeUp();
                LastDecision = "woke up";
                Audio.Sting();
                UI.Toast("The line goes dead. Something on the stage just moved.");
            }

            NightSeconds += Time.deltaTime;
            UpdateFatigue();
            if (!sixAM && NightSeconds >= NightLengthSeconds)
            {
                StartCoroutine(SixAM());
            }
            if (sixAM)
            {
                // The 6 AM sequence drives everything from here.
                return;
            }
            float actionInterval = settings.DecisionIntervalSeconds / settings.PlayerActionsPerTick;
            sampleTimer += Time.deltaTime;
            if (sampleTimer >= actionInterval && !State.FreddyTurn)
            {
                sampleTimer = 0f;
                SamplePlayer();
            }

            if (State.FreddyTurn && !decisionPending)
            {
                if (!State.FreddyAwake)
                {
                    State.SkipFreddyTurn();
                    if (State.FreddyAwake)
                    {
                        Freddy.WakeUp();
                        LastDecision = "woke up";
                        Audio.Sting();
                        UI.Toast("Something on the stage just moved.");
                    }
                }
                else if (Freddy.IsAsleep)
                {
                    // The brain is awake but the body is not (safety net): wake it, decide next turn.
                    Freddy.WakeUp();
                    State.SkipFreddyTurn();
                }
                else if (Freddy.Ready)
                {
                    State.PlaceFreddy(Freddy.Room);
                    DecisionRoom = State.FreddyRoom;
                    ProbeDecision();
                    decisionPending = true;
                    decisionTick = State.Tick;
                    Policy.Decide(State);
                }
            }

            // The agent answers on the next Academy step; baselines answer at once.
            if (decisionPending && State.Tick != decisionTick)
            {
                decisionPending = false;
                int action = State.LastFreddyAction;
                ProbedChoice = action;
                LastDecision = Describe(action);
                Decisions.Add(new DecisionRecord
                {
                    Time = NightSeconds,
                    Action = action,
                    Text = LastDecision,
                    Confidence = HasProbabilities ? Probabilities[action] : -1f,
                    FreddyRoom = Freddy.Room,
                    Heard = LastHeard,
                    LastSeen = State.LastSeenRoom,
                    Position = Freddy.transform.position
                });
                Freddy.Execute(action, settings.DecisionIntervalSeconds * 0.8f);
                if (FreddyActions.IsSense(action))
                {
                    OnSense();
                }
            }

            trailTimer -= Time.deltaTime;
            if (trailTimer <= 0f)
            {
                trailTimer = TrailInterval;
                FreddyTrail.Add(Freddy.transform.position);
                PlayerTrail.Add(Player.transform.position);
            }

            UpdateTracker();
            CheckEnd();
        }

        /// <summary>
        /// In a browser the mouse can only be captured after a click, and the browser releases it on
        /// Esc or when the pointer escapes. Ask for a click, and pause when the capture is lost.
        /// </summary>
        private void UpdatePointerLock()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer || CaptureMode.Active)
            {
                NeedsClickToLook = false;
                return;
            }
            if (Player.InFocus)
            {
                // Working a panel: the pointer is free on purpose.
                NeedsClickToLook = false;
                pointerWasLocked = false;
                return;
            }
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            if (Controls.UsingGamepad)
            {
                // A gamepad does not need the mouse captured.
                NeedsClickToLook = false;
                Player.Controllable = Phase == GamePhase.Playing;
                pointerWasLocked = locked;
                return;
            }
            if (Phase == GamePhase.Playing)
            {
                if (pointerWasLocked && !locked)
                {
                    SetPaused(true);
                }
                else if (!locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
            NeedsClickToLook = Phase == GamePhase.Playing && !locked;
            Player.Controllable = Phase == GamePhase.Playing && locked;
            pointerWasLocked = locked;
        }

        /// <summary>The brain's view of every option right now, from the same weights as the model.</summary>
        private void ProbeDecision()
        {
            if (probe == null)
            {
                HasProbabilities = false;
                return;
            }
            FreddyObservations.Write(State, observations);
            for (int action = 0; action < FreddyActions.Count; action++)
            {
                LegalActions[action] = State.IsLegalFreddyAction(action);
            }
            probe.Evaluate(observations, LegalActions, Probabilities);
            HasProbabilities = true;
            ProbedChoice = -1;
        }

        /// <summary>An action in words, naming the room or the kind of hiding spot.</summary>
        public string Describe(int action)
        {
            if (action < 0)
            {
                return "-";
            }
            if (FreddyActions.IsSense(action))
            {
                return "sense your fear";
            }
            if (FreddyActions.IsSearch(action))
            {
                int spot = FreddyActions.SpotOf(action);
                if (spot >= HuntMap.SpotCount(DecisionRoom))
                {
                    return "search spot " + spot;
                }
                SpotKind kind = PizzeriaLayout.SpotKindOf(DecisionRoom, spot);
                string what = kind == SpotKind.Locker ? "the locker" : kind == SpotKind.Cabinet ? "cabinet" : "under table";
                if (kind != SpotKind.Locker)
                {
                    // Several cabinets or tables in the room: say which one.
                    int same = 0;
                    int index = 0;
                    for (int other = 0; other < HuntMap.SpotCount(DecisionRoom); other++)
                    {
                        if (PizzeriaLayout.SpotKindOf(DecisionRoom, other) == kind)
                        {
                            same++;
                            index += other < spot ? 1 : 0;
                        }
                    }
                    if (same > 1)
                    {
                        what += " " + (char)('A' + index);
                    }
                }
                return "search " + what;
            }
            var room = (Room)action;
            return room == DecisionRoom ? "stay and listen" : "go to " + HuntMap.DisplayName(room);
        }

        /// <summary>Tonight's way out (the front doors or the loading door), for the texts.</summary>
        public static Room TonightExit { get; private set; } = HuntMap.Exit;

        /// <summary>The panel of tonight's exit.</summary>
        public ExitPanel ActiveExit => Level.ExitOf(State.ExitRoom);

        /// <summary>A step of the objectives, in words.</summary>
        public static string Describe(ObjectiveStep step)
        {
            switch (step)
            {
                case ObjectiveStep.Cells: return "power the three sockets";
                case ObjectiveStep.Breaker: return "reset the breakers in the Storage";
                case ObjectiveStep.Keycard: return "take the keycard from the Veranda and swipe it in the Office";
                case ObjectiveStep.Tape: return "open the tape box in the Exit Lobby";
                default: return TonightExit == HuntMap.Exit ? "pull the lever at the front doors" : "pull the lever at the loading door";
            }
        }

        private void SamplePlayer()
        {
            HidingSpot spot = Player.HiddenIn;
            int noise = Player.ConsumeNoise();
            if (Player.Crawling)
            {
                // In a duct: out of sight and out of reach, like in the simulation; the brain
                // keeps the room the player crawled in from until they come out.
                Room inside = PizzeriaLayout.RoomAt(Player.HeadPosition);
                State.ObservePlayer(inside, true, -1, noise);
            }
            else
            {
                Room room = PizzeriaLayout.RoomAt(Player.transform.position);
                State.ObservePlayer(spot != null ? spot.Room : room, spot != null, spot != null ? spot.Index : -1, noise);
            }
            if (State.HeardRoom.HasValue)
            {
                LastHeard = State.HeardRoom;
                AiVision.Instance.Ripple(State.HeardRoom.Value, noise);
            }
        }

        private void CheckEnd()
        {
            if (ActiveExit.Open && PizzeriaLayout.IsOutside(State.ExitRoom, Player.transform.position))
            {
                EndReason = Night >= LastNight ? "You got out with the tape." : "You powered the exit and got out.";
                Finish(HuntOutcome.Escaped);
            }
            else if (State.Outcome == HuntOutcome.TimeUp && !sixAM)
            {
                StartCoroutine(SixAM());
            }
        }

        /// <summary>Motion tracker: faster beeps and a heartbeat as Freddy gets closer.</summary>
        private void UpdateTracker()
        {
            float distance = Vector3.Distance(Player.transform.position, Freddy.transform.position);
            if (distance > TrackerRange)
            {
                return;
            }
            trackerTimer -= Time.deltaTime;
            if (trackerTimer <= 0f)
            {
                trackerTimer = Mathf.Lerp(0.25f, 1.6f, distance / TrackerRange);
                Audio.Beep(Mathf.Lerp(1.3f, 0.9f, distance / TrackerRange));
            }
            if (distance < 10f)
            {
                heartbeatTimer -= Time.deltaTime;
                if (heartbeatTimer <= 0f)
                {
                    heartbeatTimer = Mathf.Lerp(0.42f, 1.1f, distance / 10f);
                    Audio.Heartbeat(Mathf.Lerp(0.9f, 0.3f, distance / 10f));
                }
            }
        }

        // ------------------------------------------------------------------ objectives and items

        /// <summary>
        /// The fuses and party horns appear only when the call ends: exploring during the call
        /// teaches the building, not where tonight's objectives are.
        /// </summary>
        private void RevealItems()
        {
            itemsRevealed = true;
            for (int cell = 0; cell < settings.CellCount; cell++)
            {
                Room? room = State.CellRoom(cell);
                if (room.HasValue && Level.Cells[(int)room.Value] != null)
                {
                    Level.Cells[(int)room.Value].SetCell(cell, State.CellSocket(cell));
                    Level.Cells[(int)room.Value].gameObject.SetActive(true);
                }
            }
            foreach (LurePickup lure in lures)
            {
                lure.gameObject.SetActive(true);
            }
        }

        /// <summary>Party horns in random rooms each night (fewer every night), in front of a hiding spot.</summary>
        private void PlaceLures()
        {
            foreach (LurePickup lure in lures)
            {
                Destroy(lure.gameObject);
            }
            lures.Clear();
            Color[] colors = { new Color(1f, 0.3f, 0.3f), new Color(0.3f, 0.6f, 1f), new Color(1f, 0.85f, 0.2f), new Color(0.4f, 1f, 0.4f) };
            var rooms = new List<Room>();
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                if ((Room)room != HuntMap.Exit && (Room)room != HuntMap.FreddyStart)
                {
                    rooms.Add((Room)room);
                }
            }
            int count = Mathf.Max(1, 5 - Night);
            for (int i = 0; i < count && rooms.Count > 0; i++)
            {
                Room room = rooms[Random.Range(0, rooms.Count)];
                rooms.Remove(room);
                Vector3 spot = PizzeriaLayout.SpotApproach(room, 0);
                Vector3 side = PizzeriaLayout.SpotFacing(room, 0) * Vector3.right * 0.7f;
                LurePickup lure = LurePickup.Create(Level.Root, spot + side + Vector3.up * 0.05f, colors[i]);
                lure.gameObject.SetActive(false);
                lures.Add(lure);
            }
        }

        /// <summary>The lab notes of this night (two per night, in story order), on the floor of random rooms.</summary>
        private void PlaceNotes()
        {
            foreach (NotePickup note in notes)
            {
                Destroy(note.gameObject);
            }
            notes.Clear();
            int first = (Night - 1) * 2;
            var rooms = new List<Room>
            {
                Room.Office, Room.Parts, Room.Restrooms, Room.BackHall, Room.Kitchen,
                Room.DiningHall, Room.MainHall, Room.Arcade, Room.Storage, Room.PartyRoom, Room.Banquet, Room.Veranda
            };
            for (int i = first; i < first + 2 && i < StoryData.Notes.Length; i++)
            {
                Room room = rooms[Random.Range(0, rooms.Count)];
                rooms.Remove(room);
                int spot = HuntMap.SpotCount(room) - 1;
                Vector3 position = PizzeriaLayout.SpotApproach(room, spot) - PizzeriaLayout.SpotFacing(room, spot) * Vector3.right * 0.8f;
                notes.Add(NotePickup.Create(this, Level.Root, position + Vector3.up * 0.01f, Random.Range(0f, 360f), StoryData.Notes[i]));
            }
        }

        /// <summary>A thrown party horn blew: a loud noise in that room, for Freddy's ears only.</summary>
        public void OnLureBlown(Vector3 position)
        {
            Audio.PlayAt(Audio.PartyHorn, position, 1f);
            Room room = PizzeriaLayout.RoomAt(position);
            if (Phase == GamePhase.Playing && !InCall && State.HearNoiseAt(room, 3))
            {
                LastHeard = room;
                AiVision.Instance.Ripple(room, 3);
                UI.Toast("Freddy's brain heard a noise in the " + HuntMap.DisplayName(room) + ".");
            }
        }

        /// <summary>Freddy used "sense": a whisper and a red pulse, and the result in words.</summary>
        private void OnSense()
        {
            LastSenseAt = Time.time;
            LastSenseFound = State.TicksSinceSensed <= 1 && State.SensedRoom.HasValue;
            Audio.SenseWhisper();
            UI.Toast(LastSenseFound
                ? "FREDDY SENSED YOU. He knows where you are."
                : "You feel something searching for you... it found nothing. Stay calm.");
        }

        // ------------------------------------------------------------------ events from the world

        public void OnCellTaken(CellPickup cell)
        {
            State.TakeCell(cell.Room);
            Player.MakeNoise(2);
            Audio.PlayAt(Assets.FusePickup, cell.transform.position, 0.8f, 1.4f);
            UI.Toast(Night >= 2
                ? "The " + CellColors.Name(cell.Socket) + " cell. Its socket is in the " + HuntMap.DisplayName(HuntMap.SocketRooms[cell.Socket]) + "."
                : "A power cell. Any socket takes it: Parts & Service, Banquet Room or Party Room.");
        }

        public void OnCellInserted(PowerSocket socket)
        {
            State.InsertCarriedCell(socket.Index);
            if (State.HearNoiseAt(PizzeriaLayout.RoomAt(socket.transform.position), 2))
            {
                LastHeard = PizzeriaLayout.RoomAt(socket.transform.position);
            }
            UI.Toast(State.CellsInserted >= CellsNeeded ? "All three sockets have power." : "Socket powered: " + State.CellsInserted + " of " + CellsNeeded + ". He is getting faster.");
            AnnounceExitIfReady();
        }

        public void OnBreakerRestored(BreakerBox box)
        {
            State.CompleteBreaker();
            Audio.PlayAt(Assets.FusePickup, box.transform.position, 1f, 0.8f);
            FlickerLight.Blackout(0.6f);
            // When the lights come back, they come back too bright, all over the building.
            FlickerLight.Surge(box.transform.position, 60f, 1.5f, 0.6f);
            UI.Toast("Power restored. The whole building heard that.");
            // The breaker is loud: the brain hears it like any other noise.
            Room room = PizzeriaLayout.RoomAt(box.transform.position);
            if (State.HearNoiseAt(room, 3))
            {
                LastHeard = room;
            }
            AnnounceExitIfReady();
        }

        public void OnCardTaken()
        {
            State.CompleteCardTaken();
            Player.MakeNoise(1);
            Audio.Click();
            UI.Toast("The staff keycard. Swipe it through the reader in the Office.");
        }

        public void OnCardSwiped()
        {
            State.CompleteCardSwipe();
            UI.Toast("Card accepted.");
            AnnounceExitIfReady();
        }

        public void OnTapeTaken()
        {
            State.CompleteTape();
            Audio.Click();
            UI.Toast("You have the tape.");
            AnnounceExitIfReady();
        }

        private void AnnounceExitIfReady()
        {
            if (State.ExitUnlocked)
            {
                UI.Toast("Everything is done. Now " + Describe(ObjectiveStep.Exit) + ". He will hear it.");
            }
        }

        public void OnExitOpened()
        {
            Audio.PlayAt(Assets.ExitDoor, ActiveExit.transform.position, 1f);
            UI.Toast("The door is open. RUN!");
        }

        public void OnFreddySawPlayer()
        {
            if (Phase == GamePhase.Playing)
            {
                State.ReportSighting(PizzeriaLayout.RoomAt(Player.transform.position));
            }
        }

        public void OnFreddyLostPlayer()
        {
            State.PlaceFreddy(Freddy.Room);
        }

        public void OnSpotSearched(HidingSpot spot, bool playerWasInside)
        {
            if (playerWasInside)
            {
                UI.Toast("He didn't notice you...");
            }
        }

        /// <summary>The body caught the player. <paramref name="how"/> explains it on the end screen.</summary>
        public void OnCaught(string how)
        {
            if (Phase != GamePhase.Playing)
            {
                return;
            }
            Phase = GamePhase.Caught;
            EndReason = how;
            State.End(HuntOutcome.Caught);
            Policy.OnNightEnded(State, HuntOutcome.Caught);
            StartCoroutine(Jumpscare());
        }

        private void Finish(HuntOutcome outcome)
        {
            State.End(outcome);
            Policy.OnNightEnded(State, outcome);
            Phase = outcome == HuntOutcome.Escaped ? GamePhase.Escaped : GamePhase.TimeUp;
            if (outcome == HuntOutcome.Escaped && Night + 1 > BestNight)
            {
                BestNight = Mathf.Min(Night + 1, LastNight);
            }
            Player.Controllable = false;
            Audio.PlayAmbient(false);
            if (outcome == HuntOutcome.Escaped && Night >= LastNight && Ending != null)
            {
                // Out with the tape: it is played at home before the end screen.
                Player.EndFocus();
                StartCoroutine(EndWithTape(outcome));
                return;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UI.ShowEnd(outcome);
        }

        /// <summary>Test helper (capture tour): a second before 6 AM.</summary>
        public void SkipToDawnForTest() => NightSeconds = NightLengthSeconds - 1f;

        /// <summary>Test helper (capture tour): the guard gets out on the last night, as if through the door.</summary>
        public void EscapeForTest()
        {
            EndReason = "You got out with the tape.";
            Finish(HuntOutcome.Escaped);
        }

        private IEnumerator EndWithTape(HuntOutcome outcome)
        {
            yield return Ending.Play();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UI.ShowEnd(outcome);
        }

        // ------------------------------------------------------------------ jumpscare

        /// <summary>
        /// Lights die, Freddy's face fills the screen in red light while the camera shakes and the
        /// lens warps, he screams over static, and the camera falls to the floor.
        /// </summary>
        // ------------------------------------------------------------------ fatigue and 6 AM

        private bool sixAM;
        private int lastHour;

        private static readonly string[] HourLines =
        {
            "",
            "1 AM. Your legs are starting to feel it.",
            "2 AM. Running takes more out of you now.",
            "3 AM. Your breath is getting short.",
            "4 AM. Everything is heavy. Pick your runs.",
            "5 AM. One more hour. You can barely run."
        };

        /// <summary>The guard tires with every hour (and a little with every night); a word at each hour.</summary>
        private void UpdateFatigue()
        {
            float progress = Mathf.Clamp01(NightSeconds / NightLengthSeconds);
            Player.Fatigue = Mathf.Clamp01(progress * 0.75f + (Night - 1) * 0.05f);
            int hour = Mathf.FloorToInt(progress * 6f);
            if (hour != lastHour && hour >= 1 && hour < HourLines.Length)
            {
                lastHour = hour;
                UI.Toast(HourLines[hour]);
                Audio.PlayAt(GameAudio.Bell(), Player.transform.position + Vector3.up * 3f, 0.25f, 1.2f);
            }
        }

        /// <summary>
        /// Six in the morning with the guard still inside: the bell, the lights die, heavy steps
        /// behind. The guard turns, sees him, runs for it; a door slams shut ahead; he is there.
        /// </summary>
        private IEnumerator SixAM()
        {
            sixAM = true;
            Player.EndFocus();
            if (Player.HiddenIn != null)
            {
                Player.HiddenIn.Use(Player);
            }
            Player.Controllable = false;
            UI.Toast("6:00 AM. The doors are locking.");
            for (int i = 0; i < 3; i++)
            {
                Audio.PlayAt(GameAudio.Bell(), Player.transform.position + Vector3.up * 3f, 0.6f, 1f);
                if (i == 1)
                {
                    FlickerLight.Blackout(14f);
                    Audio.PlayAmbient(false);
                }
                yield return new WaitForSeconds(0.9f);
            }
            // Steps behind, coming closer.
            Vector3 forward = Player.transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            for (int i = 0; i < 3; i++)
            {
                Audio.PlayAt(Assets.LockerDoor, Player.transform.position - forward * (6f - i * 1.5f), 0.6f + i * 0.15f, 0.35f);
                yield return new WaitForSeconds(0.55f);
            }
            // He is behind: the guard turns around and sees him.
            Vector3 behind = Player.transform.position - forward * 4.5f;
            Freddy.ResetTo(new Vector3(behind.x, Freddy.transform.position.y, behind.z), Quaternion.LookRotation(forward).eulerAngles.y);
            Freddy.WakeUp();
            // The scene moves him from here on, not his hunt.
            Freddy.ScriptedWalk = 0f;
            Freddy.FlashEyes(3f);
            for (float t = 0f; t < 0.45f; t += Time.deltaTime)
            {
                Player.LookToward(Freddy.EyePosition, 14f);
                yield return null;
            }
            Audio.Sting();
            Player.Jolt(1.2f);
            yield return new WaitForSeconds(0.6f);
            // Run.
            for (float t = 0f; t < 1.6f; t += Time.deltaTime)
            {
                Player.LookToward(Player.transform.position + forward * 6f + Vector3.up * 1.5f, 10f);
                Player.ScriptedRun(forward, PlayerController.RunSpeed * 0.9f);
                Vector3 follow = Player.transform.position - forward * Mathf.Lerp(4.5f, 2.2f, t / 1.6f);
                Freddy.transform.position = new Vector3(follow.x, Freddy.transform.position.y, follow.z);
                Freddy.ScriptedWalk = FreddyBody.ChaseSpeed;
                yield return null;
            }
            // A door slams shut ahead.
            Audio.PlayAt(Assets.LockerDoor, Player.transform.position + forward * 2.5f, 1f, 0.5f);
            Player.Jolt(1.6f);
            yield return new WaitForSeconds(0.45f);
            Vector3 close = Player.transform.position - forward * 1.3f;
            Freddy.transform.position = new Vector3(close.x, Freddy.transform.position.y, close.z);
            Freddy.transform.rotation = Quaternion.LookRotation(forward);
            Freddy.Grab("6 AM came and you were still inside. The doors locked, the lights went out, and he found you in the dark.");
        }

        private IEnumerator Jumpscare()
        {
            Player.EndFocus();
            Player.Controllable = false;
            Player.CameraLocked = true;
            if (Player.HiddenIn != null)
            {
                Player.HiddenIn.Rattle(4f);
            }
            Vector3 toPlayer = Player.HeadPosition - Freddy.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                Freddy.transform.rotation = Quaternion.LookRotation(toPlayer.normalized);
            }
            Transform head = Freddy.Head;
            Camera view = Player.View;

            // A beat first: the view whips to him, silence, his eyes flare. Then the scream.
            Freddy.FlashEyes(3f);
            Audio.PlayAmbient(false);
            Quaternion from = Player.Head.rotation;
            for (float w = 0f; w < 0.22f; w += Time.deltaTime)
            {
                Vector3 face0 = head.position + Vector3.up * 0.05f;
                Player.Head.rotation = Quaternion.Slerp(from, Quaternion.LookRotation(face0 - Player.Head.position), TaskKit.Ease(w / 0.22f));
                yield return null;
            }
            yield return new WaitForSeconds(0.18f);

            FlickerLight.Blackout(3f);
            Player.Flashlight.enabled = false;
            jumpscareLight.enabled = true;
            Audio.PlayAmbient(false);
            Audio.Scream();
            Audio.StaticBurst();
            Audio.Impact();
            UI.ShowJumpscare();

            const float lunge = 1.5f;
            float t = 0f;
            float seed = Random.value * 100f;
            while (t < lunge)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.16f);
                float calm = 1f - Mathf.Clamp01((t - 0.3f) / (lunge - 0.3f));
                Vector3 face = head.position + Vector3.up * 0.05f;
                Vector3 forward = Freddy.transform.forward;
                Vector3 shake = new Vector3(Mathf.PerlinNoise(seed, t * 25f) - 0.5f, Mathf.PerlinNoise(seed + 5f, t * 25f) - 0.5f, 0f) * 0.12f * calm;
                Player.Head.position = face + forward * Mathf.Lerp(1.6f, 0.55f, 1f - (1f - k) * (1f - k)) + Freddy.transform.rotation * shake;
                Player.Head.LookAt(face);
                Player.Head.Rotate(0f, 0f, (Mathf.PerlinNoise(seed + 9f, t * 12f) - 0.5f) * 14f * calm, Space.Self);
                view.fieldOfView = Mathf.Lerp(70f, 48f, k);
                jumpscareLight.transform.position = face + forward * 0.5f - Vector3.up * 0.4f;
                chromatic.intensity.value = Mathf.Lerp(0.3f, 1f, calm);
                distortion.intensity.value = -0.45f * calm;
                grain.intensity.value = Mathf.Lerp(0.35f, 1f, calm);
                yield return null;
            }

            // The camera falls to the floor and everything goes dark.
            Vector3 start = Player.Head.position;
            Quaternion startRotation = Player.Head.rotation;
            Vector3 floor = new Vector3(start.x, Player.transform.position.y + 0.25f, start.z) - Freddy.transform.forward * 0.6f;
            t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.45f);
                Player.Head.position = Vector3.Lerp(start, floor, k * k);
                Player.Head.rotation = startRotation * Quaternion.Euler(-20f * k, 0f, 80f * k);
                vignette.intensity.value = Mathf.Lerp(0.4f, 1f, t / 0.9f);
                jumpscareLight.intensity = Mathf.Lerp(6f, 0f, t / 0.9f);
                yield return null;
            }
            // Dragged away along the floor, the view sliding back and jerking with each pull,
            // while the lab's verdict comes up.
            Vector3 ground = Player.Head.position;
            Quaternion lying = Player.Head.rotation;
            Vector3 away = Freddy.transform.forward;
            away.y = 0f;
            Audio.PlayAt(GameAudio.Drag(), ground, 0.8f, 0.9f);
            UI.ShowCinematic(true);
            for (t = 0f; t < 2.6f; t += Time.deltaTime)
            {
                float pulls = t * 2.5f;
                float pulled = Mathf.Floor(pulls) + Mathf.SmoothStep(0f, 1f, Mathf.Repeat(pulls, 1f));
                Player.Head.position = ground + away.normalized * pulled * 0.45f;
                Player.Head.rotation = lying * Quaternion.Euler(Mathf.Sin(pulls * Mathf.PI * 2f) * 2f, 0f, 0f);
                jumpscareLight.intensity = 0f;
                UI.SetCinematic("", Mathf.Clamp01((t - 1.4f) / 1.2f));
                UI.SetCinematicCard("GUARD ADDED TO THE TRAINING SET", Mathf.Clamp01((t - 0.6f) / 0.6f), 40);
                if (Mathf.Repeat(pulls, 1f) < Time.deltaTime * 2.5f && t > 0.3f)
                {
                    Audio.PlayAt(GameAudio.Drag(), Player.Head.position, 0.6f, Random.Range(0.85f, 1f));
                }
                yield return null;
            }
            UI.SetCinematicCard("", 0f);
            UI.ShowCinematic(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            UI.ShowEnd(HuntOutcome.Caught);
        }

        private void ResetEffects()
        {
            if (Player != null)
            {
                Player.View.fieldOfView = 70f;
            }
            if (chromatic != null)
            {
                chromatic.intensity.value = 0.15f;
                distortion.intensity.value = 0f;
                grain.intensity.value = 0.35f;
                vignette.intensity.value = 0.42f;
            }
            if (jumpscareLight != null)
            {
                jumpscareLight.enabled = false;
                jumpscareLight.intensity = 6f;
            }
        }

        // ------------------------------------------------------------------ look

        private void SetUpAtmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            // Next to no ambient light and no grey sky reflection: away from the few working lamps,
            // without the flashlight, you see nothing.
            RenderSettings.ambientLight = new Color(0.008f, 0.008f, 0.011f);
            RenderSettings.reflectionIntensity = 0.15f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.03f;
            // A faint cold haze: distant rooms fade into it instead of into pure black.
            RenderSettings.fogColor = new Color(0.012f, 0.014f, 0.02f);
            RenderSettings.skybox = null;

            var volumeObject = new GameObject("PostProcessing");
            volumeObject.transform.SetParent(transform, false);
            volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.5f);
            grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Medium4);
            grain.intensity.Override(0.35f);
            chromatic = profile.Add<ChromaticAberration>(true);
            chromatic.intensity.Override(0.15f);
            distortion = profile.Add<LensDistortion>(true);
            distortion.intensity.Override(0f);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            colorAdjustments = color;
            color.saturation.Override(-20f);
            color.contrast.Override(18f);
            color.postExposure.Override(BaseExposure);
            color.colorFilter.Override(new Color(1f, 0.96f, 0.9f));
            ShadowsMidtonesHighlights tones = profile.Add<ShadowsMidtonesHighlights>(true);
            tones.shadows.Override(new Vector4(0.9f, 1f, 1.1f, 0f));
            Bloom bloom = profile.Add<Bloom>(true);
            GraphicsQuality.RegisterBloom(bloom);
            // Smudges on the "lens" catch the glow of lamps and signs.
            bloom.dirtTexture.Override(ProceduralTextures.LensDirt());
            bloom.dirtIntensity.Override(2.5f);
            // Horror grade: cold shadows, warm practical lights.
            WhiteBalance balance = profile.Add<WhiteBalance>(true);
            balance.temperature.Override(-6f);
            SplitToning split = profile.Add<SplitToning>(true);
            split.shadows.Override(new Color(0.32f, 0.45f, 0.55f));
            split.highlights.Override(new Color(0.85f, 0.65f, 0.45f));
            split.balance.Override(-15f);
            MotionBlur blur = profile.Add<MotionBlur>(true);
            blur.quality.Override(MotionBlurQuality.Low);
            blur.intensity.Override(0.12f);
            GraphicsQuality.RegisterMotionBlur(blur);
            bloom.intensity.Override(0.8f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.7f);
            // Lamps, neon and the flashlight in a wet lens: ghosts and a thin horizontal streak.
            ScreenSpaceLensFlare flare = profile.Add<ScreenSpaceLensFlare>(true);
            flare.intensity.Override(0.35f);
            flare.tintColor.Override(new Color(1f, 0.85f, 0.7f));
            flare.firstFlareIntensity.Override(0.6f);
            flare.secondaryFlareIntensity.Override(0.4f);
            flare.warpedFlareIntensity.Override(0.3f);
            flare.streaksIntensity.Override(0.45f);
            flare.streaksLength.Override(0.35f);
            flare.streaksThreshold.Override(0.55f);
            flare.chromaticAbberationIntensity.Override(0.6f);
            GraphicsQuality.RegisterLensFlare(flare);
            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);
            volume.profile = profile;

            jumpscareLight = new GameObject("JumpscareLight").AddComponent<Light>();
            jumpscareLight.transform.SetParent(transform, false);
            jumpscareLight.type = LightType.Point;
            jumpscareLight.range = 3f;
            jumpscareLight.intensity = 6f;
            jumpscareLight.color = new Color(1f, 0.12f, 0.08f);
            jumpscareLight.enabled = false;
        }

        private void LateUpdate()
        {
            // Post-processing must be enabled on the camera, which is created at runtime.
            UniversalAdditionalCameraData data = Player.View.GetUniversalAdditionalCameraData();
            if (!data.renderPostProcessing)
            {
                data.renderPostProcessing = true;
            }
        }
    }
}
