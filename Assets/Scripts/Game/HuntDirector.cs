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
        private ObjectiveItem breaker;
        private ObjectiveItem keycard;
        private ObjectiveItem tape;
        private readonly List<LurePickup> lures = new List<LurePickup>();
        private readonly List<NotePickup> notes = new List<NotePickup>();
        private bool pointerWasLocked;

        private Volume volume;
        private ChromaticAberration chromatic;
        private LensDistortion distortion;
        private FilmGrain grain;
        private Vignette vignette;
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

        public int FusesCollected => State.FusesCollected;
        public int FusesNeeded => settings.FuseCount;

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

        /// <summary>True while the intro cinematic plays.</summary>
        public bool InIntro => intro != null && intro.Playing;

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
            settings = new HuntSettings { ResolveCatches = false };
            State = new HuntState(settings, System.Environment.TickCount);
            if (Assets.PolicyWeights != null)
            {
                probe = PolicyProbe.FromBytes(Assets.PolicyWeights.bytes);
            }

            SetUpAtmosphere();
            Audio = GameAudio.Create(transform, Assets);
            Level = LevelBuilder.Build(transform, Assets);
            Player = PlayerController.Create(this, PizzeriaLayout.PlayerStart, PizzeriaLayout.PlayerFacing);
            Freddy = FreddyBody.Create(this, Assets.FreddyPrefab, PizzeriaLayout.FreddyStart, PizzeriaLayout.FreddyFacing);
            Material doorSteel = BuildKit.Surface("doorSteel", () => ProceduralTextures.SteelPanel(new Color(0.42f, 0.09f, 0.08f)), 0.4f, 0.35f);
            foreach ((Room _, Room _, Vector3 door, bool alongX) in PizzeriaLayout.Doorways())
            {
                Door.Create(this, Level.Root, door, alongX, doorSteel);
            }
            AiVision.Create(this);
            call = PhoneCall.Create(transform, Audio, Assets);
            Exterior = Exterior.Build(transform, Assets);
            intro = IntroCinematic.Create(this, Exterior);
            // Objective objects: the breaker in the storage room, the keycard lockbox and the tape in the office.
            breaker = ObjectiveItem.Create(this, ObjectiveStep.Breaker, Level.Root, new Vector3(13.2f, 1.4f, 15.68f), 180f, 3f);
            keycard = ObjectiveItem.Create(this, ObjectiveStep.Keycard, Level.Root, new Vector3(-9.9f, 1.3f, 8.33f), 0f, 0f);
            tape = ObjectiveItem.Create(this, ObjectiveStep.Tape, Level.Root, new Vector3(-13.4f, 0.92f, 14.4f), 20f, 0f);
            CreateBrain();
            UI = GameUI.Create(this);
#if UNITY_EDITOR
            if (QuickStartInEditor)
            {
                // Testing shortcut: skip the menu and the intro and drop straight into night 1.
                StartNight(1);
                CaptureMode.AttachIfRequested(this);
                return;
            }
#endif
            ShowMenu();
            CaptureMode.AttachIfRequested(this);
        }

        private void CreateBrain()
        {
            if (Assets.FreddyModel != null)
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
                BrainDescription = "scripted baseline (no trained model found)";
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
            Player.Head.position = new Vector3(-12.2f, 1.55f, -12.3f);
            // Aim a little to his side so he stands right of the menu buttons.
            Player.Head.LookAt(PizzeriaLayout.FreddyStart + new Vector3(0f, 1.5f, -2.2f));
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
            State.Reset();
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                if (Level.Fuses[room] != null)
                {
                    Level.Fuses[room].gameObject.SetActive(false);
                }
            }
            Level.Exit.ResetPanel();
            ObjectiveList.Begin(Night);
            breaker.ResetItem();
            keycard.ResetItem();
            tape.ResetItem();
            breaker.gameObject.SetActive(ObjectiveList.Needs(ObjectiveStep.Breaker));
            keycard.gameObject.SetActive(ObjectiveList.Needs(ObjectiveStep.Keycard));
            tape.gameObject.SetActive(ObjectiveList.Needs(ObjectiveStep.Tape));
            PlaceNotes();
            Player.ResetTo(PizzeriaLayout.PlayerStart, PizzeriaLayout.PlayerFacing);
            Player.CameraLocked = false;
            Player.Controllable = true;
            Freddy.ResetTo(PizzeriaLayout.FreddyStart, PizzeriaLayout.FreddyFacing);
            Policy.OnNightStarted(State);
            PlaceLures();
            Door.ResetAll();
            call.Begin(Night);
            wasInCall = InCall;
            itemsRevealed = false;
            if (!InCall)
            {
                RevealItems();
            }

            LastSenseAt = -100f;
            sampleTimer = 0f;
            trailTimer = 0f;
            decisionPending = false;
            NightSeconds = 0f;
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
            if (Controls.PausePressed && !InIntro)
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
            if (Phase != GamePhase.Playing)
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
                if (kind != SpotKind.Locker && HuntMap.SpotCount(DecisionRoom) == 3)
                {
                    // Two cabinets or two tables in the room: say which one.
                    what += spot == 1 ? " A" : " B";
                }
                return "search " + what;
            }
            var room = (Room)action;
            return room == DecisionRoom ? "stay and listen" : "go to " + HuntMap.DisplayName(room);
        }

        /// <summary>A step of the objectives, in words.</summary>
        public static string Describe(ObjectiveStep step)
        {
            switch (step)
            {
                case ObjectiveStep.Fuses: return "find the fuses";
                case ObjectiveStep.Breaker: return "restore the breaker in the Storage";
                case ObjectiveStep.Keycard: return "take the keycard from the Office lockbox";
                case ObjectiveStep.Tape: return "take the tape from the Office";
                default: return "open the exit in the Exit Lobby";
            }
        }

        private void SamplePlayer()
        {
            Room room = PizzeriaLayout.RoomAt(Player.transform.position);
            HidingSpot spot = Player.HiddenIn;
            int noise = Player.ConsumeNoise();
            State.ObservePlayer(spot != null ? spot.Room : room, spot != null, spot != null ? spot.Index : -1, noise);
            if (State.HeardRoom.HasValue)
            {
                LastHeard = State.HeardRoom;
                AiVision.Instance.Ripple(State.HeardRoom.Value, noise);
            }
        }

        private void CheckEnd()
        {
            if (Level.Exit.Open && Player.transform.position.z < PizzeriaLayout.ExitLineZ)
            {
                EndReason = Night >= LastNight ? "You got out with the tape." : "You powered the exit and got out.";
                Finish(HuntOutcome.Escaped);
            }
            else if (State.Outcome == HuntOutcome.TimeUp)
            {
                EndReason = "The night is over and you are still inside.";
                Finish(HuntOutcome.TimeUp);
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
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                if (Level.Fuses[room] != null)
                {
                    Level.Fuses[room].gameObject.SetActive(State.FuseIn((Room)room));
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
            var rooms = new List<Room> { Room.Office, Room.WestHall, Room.Storage, Room.Kitchen, Room.DiningHall, Room.Arcade, Room.EastHall };
            for (int i = first; i < first + 2 && i < StoryData.Notes.Length; i++)
            {
                Room room = rooms[Random.Range(0, rooms.Count)];
                rooms.Remove(room);
                int spot = HuntMap.SpotCount(room) - 1;
                Vector3 position = PizzeriaLayout.SpotApproach(room, spot) - PizzeriaLayout.SpotFacing(room, spot) * Vector3.right * 0.8f;
                notes.Add(NotePickup.Create(this, Level.Root, position + Vector3.up * 0.01f, Random.Range(0f, 360f), StoryData.Notes[i]));
            }
        }

        /// <summary>An objective object was used (breaker restored, keycard or tape taken).</summary>
        public void CompleteObjective(ObjectiveStep step, ObjectiveItem item)
        {
            ObjectiveList.Complete(step);
            switch (step)
            {
                case ObjectiveStep.Breaker:
                    Audio.PlayAt(Assets.FusePickup, item.transform.position, 1f, 0.8f);
                    FlickerLight.Blackout(0.6f);
                    UI.Toast("Power restored. The whole building heard that.");
                    // The breaker is loud: the brain hears it like any other noise.
                    if (State.HearNoiseAt(PizzeriaLayout.RoomAt(item.transform.position), 3))
                    {
                        LastHeard = PizzeriaLayout.RoomAt(item.transform.position);
                    }
                    break;
                case ObjectiveStep.Keycard:
                    Audio.Click();
                    UI.Toast("Keycard taken. Next: " + Describe(ObjectiveList.Current) + ".");
                    break;
                case ObjectiveStep.Tape:
                    Audio.Click();
                    item.gameObject.SetActive(false);
                    UI.Toast("You have the tape. Get out.");
                    break;
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

        public void OnFuseTaken(FusePickup fuse)
        {
            State.CollectFuse(fuse.Room);
            Player.MakeNoise(2);
            if (State.FusesCollected >= FusesNeeded)
            {
                ObjectiveList.Complete(ObjectiveStep.Fuses);
            }
            Audio.PlayAt(Assets.FusePickup, fuse.transform.position, 0.8f, 1.4f);
            UI.Toast(State.FusesCollected >= FusesNeeded
                ? "All fuses! Next: " + Describe(ObjectiveList.Current) + ". He is faster now."
                : "Fuse " + State.FusesCollected + " of " + FusesNeeded + ". That was loud... and he is getting faster.");
        }

        public void OnExitOpened()
        {
            Audio.PlayAt(Assets.ExitDoor, Level.Exit.transform.position, 1f);
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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Audio.PlayAmbient(false);
            UI.ShowEnd(outcome);
        }

        // ------------------------------------------------------------------ jumpscare

        /// <summary>
        /// Lights die, Freddy's face fills the screen in red light while the camera shakes and the
        /// lens warps, he screams over static, and the camera falls to the floor.
        /// </summary>
        private IEnumerator Jumpscare()
        {
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
            Vector3 floor = new Vector3(start.x, 0.25f, start.z) - Freddy.transform.forward * 0.6f;
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
            RenderSettings.ambientLight = new Color(0.07f, 0.065f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.03f;
            RenderSettings.fogColor = new Color(0.012f, 0.01f, 0.014f);
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
            color.saturation.Override(-20f);
            color.contrast.Override(18f);
            color.postExposure.Override(0.75f);
            color.colorFilter.Override(new Color(1f, 0.96f, 0.9f));
            ShadowsMidtonesHighlights tones = profile.Add<ShadowsMidtonesHighlights>(true);
            tones.shadows.Override(new Vector4(0.9f, 1f, 1.1f, 0f));
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.8f);
            bloom.threshold.Override(0.95f);
            bloom.scatter.Override(0.7f);
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
