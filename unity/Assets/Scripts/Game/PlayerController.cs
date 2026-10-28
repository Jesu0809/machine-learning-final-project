using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightShift.Game
{
    /// <summary>
    /// First-person survivor: WASD to walk, Shift to run (fast, loud), Ctrl or C to crouch (slow,
    /// silent), mouse to look, F for the flashlight, E to use things and to hide, Space to hold
    /// the breath while hidden; or the same on a gamepad (see <see cref="Controls"/>). Every action
    /// produces a noise level that Freddy's brain can hear.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public const float WalkSpeed = 3.0f;
        public const float RunSpeed = 5.6f;
        public const float CrouchSpeed = 1.5f;
        public const float StandHeight = 1.75f;
        public const float CrouchHeight = 1.1f;
        public const float InteractDistance = 2.3f;

        /// <summary>Crouching while running slides: a fast burst that fades into crouch speed.</summary>
        public const float SlideDuration = 1f;
        public const float SlideStartSpeed = 11f;
        public const float SlideCooldown = 0.6f;
        /// <summary>Stamina (0 to 1) spent shoving a closed door open at speed: 5% of the maximum.</summary>
        public const float DoorBumpStamina = 0.05f;
        /// <summary>Shoving a door open also slows the player: 25% less speed for half a second.</summary>
        public const float DoorBumpSlow = 0.25f;
        public const float DoorBumpSlowSeconds = 0.5f;

        /// <summary>Wider view while hidden, so the room outside the vents stays readable.</summary>
        private const float HiddenFieldOfView = 82f;
        private const float FieldOfView = 70f;

        /// <summary>Standing in a doorway this close to its center makes the door the target even without looking at it.</summary>
        private const float DoorwayReach = 1.5f;

        private static bool doorHintShown;


        private CharacterController body;
        private Transform head;
        private float yaw;
        private float pitch;
        private float verticalSpeed;
        private float bobPhase;
        private float stepDistance;
        private int noiseSinceSample;
        private float breathBlockedUntil;

        // Sliding: direction is locked when the slide starts.
        private float slideTimer;
        private Vector3 slideDirection;
        private float slideCooldownUntil;
        private bool wasRunning;
        private float doorSlowUntil;

        // Hiding: the camera sits inside the spot and can only look around a little.
        private float hideYaw;
        private float hidePitch;
        private bool flashlightBeforeHiding;
        // Getting in and out of a spot: the eyes travel from where they were, through the front
        // of the spot, to the hide point (and back out), instead of jumping.
        private float hideMotion = 1f;
        private float leaveMotion = 1f;
        private float hideMotionSeconds;
        private Vector3 hideFromPosition;
        private Quaternion hideFromRotation;
        // Inside: the door is pulled shut with the flashlight still on, the beam fades out with a
        // click, then the eyes slowly get used to the dark.
        private const float MoveShare = 0.55f;
        private const float DoorShare = 0.5f;
        private const float FadeStart = 0.78f;
        private const float FadeEnd = 1f;
        private bool doorPulled;
        private bool beamClicked;
        private float eyeAdaptation;
        private Light adaptationLight;

        public HuntDirector Director { get; private set; }
        public Camera View { get; private set; }
        public Light Flashlight { get; private set; }
        private Flashlight flashlightRig;

        public bool Controllable { get; set; }

        /// <summary>The power cell in the player's hand, if any (one at a time).</summary>
        public HeldCell Cell { get; private set; }

        /// <summary>The panel being worked up close with the pointer, or null (see <see cref="IFocusTask"/>).</summary>
        public IFocusTask Focus { get; private set; }
        public bool InFocus => Focus != null;

        private const float FocusSeconds = 0.35f;
        private float focusBlend;
        private Vector3 focusFromPosition;
        private Quaternion focusFromRotation;

        /// <summary>While true the camera is placed by someone else (menu view, jumpscare).</summary>
        public bool CameraLocked { get; set; }

        public Transform Head => head;
        public bool Running { get; private set; }

        /// <summary>
        /// How tired the guard is, 0–1: it grows with every hour of the night and a little with
        /// every night (set by the director). Slower running, breath that runs out sooner and comes
        /// back slower, heavier breathing. Only the guard's body: Freddy's brain never sees it.
        /// </summary>
        public float Fatigue { get; set; }
        public bool Crouching { get; private set; }
        public bool Sliding { get; private set; }
        public bool Moving { get; private set; }
        public HidingSpot HiddenIn { get; private set; }

        /// <summary>True while crawling through an air duct (Freddy cannot see or reach the player).</summary>
        public bool Crawling { get; private set; }

        /// <summary>Room at the far end of the duct being crawled through.</summary>
        public Hunt.Room CrawlTarget { get; private set; }

        // Crawling: the eyes follow a path through the duct.
        private const float CrawlSpeed = 2.3f;
        private List<Vector3> crawlPath;
        private int crawlSegment;
        private float crawlAlong;
        private float crawlStepDistance;
        private float crawlYaw;
        private float crawlLookYaw;
        private float crawlLookPitch;
        // The animation around the crawl: kneeling at the grate, the rhythm of hands and knees,
        // stopping to push the far grate open.
        private const float KneelSeconds = 0.75f;
        private const float ExitPauseSeconds = 0.5f;
        private float crawlKneel;
        private float crawlExitPause;
        private float crawlRhythm;
        private Vector3 kneelFromPosition;
        private Quaternion kneelFromRotation;
        private VentGrate crawlEntry;
        private VentGrate crawlExit;

        /// <summary>True while hidden and holding the breath (Freddy will not notice you if he searches).</summary>
        public bool HoldingBreath { get; private set; }

        /// <summary>Breath left, 0–1.</summary>
        public float Breath { get; private set; } = 1f;

        /// <summary>
        /// Sprint stamina, 0–1 (as in Friday the 13th: sprinting is an escape, not a way to travel).
        /// A full bar lasts about 4.5 s; empty, the player is winded until it is back to 35%.
        /// </summary>
        public float Stamina { get; private set; } = 1f;
        public bool Winded { get; private set; }

        /// <summary>
        /// Fear, 0–1: rises when Freddy is close or sees the player. High fear drains stamina
        /// faster, makes the flashlight flicker and the breathing loud enough to hear in the room.
        /// </summary>
        public float Fear { get; private set; }

        /// <summary>Party horns carried (throw with G).</summary>
        public int Lures { get; private set; }

        public void AddLure() => Lures++;

        private float lastSprintAt = -10f;
        private float breathPhase;
        private float nextPantAt;
        private float flashlightGlitchUntil;

        /// <summary>What the player is looking at, for the HUD prompt.</summary>
        public IInteractable Target { get; private set; }

        public Vector3 HeadPosition => head.position;

        public static PlayerController Create(HuntDirector director, Vector3 position, float facing)
        {
            var go = new GameObject("Player");
            go.layer = 2; // Ignore Raycast: Freddy's eyes and the interaction ray pass through the player.
            go.transform.position = position;

            var controller = go.AddComponent<CharacterController>();
            controller.height = StandHeight;
            controller.radius = 0.3f;
            controller.center = new Vector3(0f, StandHeight * 0.5f, 0f);
            controller.stepOffset = 0.3f;

            var player = go.AddComponent<PlayerController>();
            player.body = controller;
            player.Director = director;
            player.yaw = facing;

            player.head = new GameObject("Head").transform;
            player.head.SetParent(go.transform, false);
            player.head.localPosition = new Vector3(0f, 1.6f, 0f);

            var cameraObject = new GameObject("Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.head, false);
            player.View = cameraObject.AddComponent<Camera>();
            player.View.fieldOfView = FieldOfView;
            player.View.nearClipPlane = 0.05f;
            player.View.farClipPlane = 60f;
            // A night, not Unity's default blue: seen through the glass front doors, outside is dark.
            player.View.clearFlags = CameraClearFlags.SolidColor;
            player.View.backgroundColor = new Color(0.006f, 0.007f, 0.011f);
            cameraObject.AddComponent<AudioListener>();

            // The only reliable light in the building (see Flashlight.cs).
            player.flashlightRig = Game.Flashlight.Create(cameraObject.transform);
            player.Flashlight = player.flashlightRig.Beam;
            player.Flashlight.enabled = true;
            player.Cell = HeldCell.Create(player);
            return player;
        }

        /// <summary>Empty hands for a new night (the cell carried last night is gone).</summary>
        public void ClearHands()
        {
            if (Cell != null && Cell.Carrying)
            {
                Cell.Release();
            }
        }

        /// <summary>Steps up to a panel and works it with the pointer (see <see cref="IFocusTask"/>).</summary>
        public void BeginFocus(IFocusTask task)
        {
            if (InFocus || HiddenIn != null || Crawling)
            {
                return;
            }
            Focus = task;
            focusBlend = 0f;
            focusFromPosition = head.position;
            focusFromRotation = head.rotation;
            Sliding = false;
            // Hands, not a pointer: the cursor stays hidden (see FocusInput).
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>Steps back from the panel: the view keeps looking where it looked, the pointer goes away.</summary>
        public void EndFocus()
        {
            if (!InFocus)
            {
                return;
            }
            IFocusTask task = Focus;
            Focus = null;
            task.FocusEnded();
            Vector3 forward = head.forward;
            yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg, -80f, 80f);
            if (Director.Playing)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void UpdateFocus(bool input)
        {
            focusBlend = Mathf.Min(1f, focusBlend + Time.deltaTime / FocusSeconds);
            float k = Ease(focusBlend);
            Transform view = Focus.FocusView;
            head.position = Vector3.Lerp(focusFromPosition, view.position, k);
            head.rotation = Quaternion.Slerp(focusFromRotation, view.rotation, k) * JoltShake();
            if (focusBlend < 1f || !input)
            {
                return;
            }
            // Step back: right click, backspace or the gamepad's B (Esc too, see HuntDirector). The
            // movement keys work the panel now, so walking away no longer leaves it.
            Mouse mouse = Mouse.current;
            Gamepad pad = Gamepad.current;
            Keyboard keys = Keyboard.current;
            if ((mouse != null && mouse.rightButton.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)
                || (keys != null && keys.backspaceKey.wasPressedThisFrame))
            {
                EndFocus();
                return;
            }
            Focus.FocusUpdate(FocusInput.Read());
        }

        /// <summary>Noise level since the last call (0 silent … 3 very loud), then resets.</summary>
        public int ConsumeNoise()
        {
            int noise = noiseSinceSample;
            noiseSinceSample = 0;
            return noise;
        }

        public void MakeNoise(int level)
        {
            noiseSinceSample = Mathf.Max(noiseSinceSample, level);
        }

        public void EnterHidingSpot(HidingSpot spot)
        {
            HiddenIn = spot;
            spot.Occupied = true;
            body.enabled = false;
            Sliding = false;
            // Nobody hides with the flashlight on: it goes off as the door shuts, and comes back
            // on when leaving.
            flashlightBeforeHiding = Flashlight.enabled;
            hideYaw = 0f;
            hidePitch = 0f;
            Breath = Mathf.Max(Breath, 0.3f);
            hideMotion = 0f;
            leaveMotion = 1f;
            hideMotionSeconds = spot.Kind == SpotKind.Locker ? 1.6f : 1.8f;
            hideFromPosition = head.position;
            hideFromRotation = head.rotation;
            doorPulled = false;
            beamClicked = false;
            // The door stays open while climbing in, then is pulled shut in front of the eyes.
            spot.Rattle(hideMotionSeconds * DoorShare, 2.5f);
            MakeNoise(1);
            Director.Audio.PlayAt(Director.Assets.LockerDoor, spot.transform.position, 0.5f);
        }

        public void LeaveHidingSpot()
        {
            HidingSpot spot = HiddenIn;
            spot.Occupied = false;
            HiddenIn = null;
            HoldingBreath = false;
            hideMotion = 1f;
            leaveMotion = 0f;
            hideMotionSeconds = spot.Kind == SpotKind.Locker ? 0.7f : 0.9f;
            flashlightRig.Dim = 0f;
            hideFromPosition = head.position;
            hideFromRotation = head.rotation;
            transform.position = spot.ExitPoint.position;
            yaw = spot.transform.eulerAngles.y;
            pitch = 0f;
            body.enabled = true;
            Flashlight.enabled = flashlightBeforeHiding;
            spot.Rattle();
            MakeNoise(1);
            Director.Audio.PlayAt(Director.Assets.LockerDoor, spot.transform.position, 0.5f);
        }

        /// <summary>
        /// Test helper (the capture tour's walk audit): walks the real body one frame toward
        /// <paramref name="target"/>, with collisions and gravity, as if the player held forward.
        /// </summary>
        public void DriveToward(Vector3 target, float speed)
        {
            Vector3 flat = target - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
            {
                yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            }
            Vector3 move = flat.normalized * speed;
            move.y = body.isGrounded ? -1f : -9.81f;
            body.Move(move * Time.deltaTime);
            PlaceCamera();
        }

        /// <summary>
        /// Into an air duct: kneel in front of the grate while it swings open, crawl along
        /// <paramref name="path"/> with the rhythm of hands and knees, push the far grate open and
        /// climb out into the room at the other end.
        /// </summary>
        public void StartCrawl(List<Vector3> path, Hunt.Room target, VentGrate entry = null, VentGrate exit = null)
        {
            if (Crawling || HiddenIn != null || path == null || path.Count < 2)
            {
                return;
            }
            Crawling = true;
            CrawlTarget = target;
            crawlPath = path;
            crawlSegment = 0;
            crawlAlong = 0f;
            crawlStepDistance = 0f;
            crawlRhythm = 0f;
            crawlKneel = 0f;
            crawlExitPause = -1f;
            crawlYaw = yaw;
            crawlLookYaw = 0f;
            crawlLookPitch = 0f;
            crawlEntry = entry;
            crawlExit = exit;
            kneelFromPosition = head.position;
            kneelFromRotation = head.rotation;
            body.enabled = false;
            Sliding = false;
            Flashlight.enabled = true;
            MakeNoise(1);
            if (entry != null)
            {
                entry.OpenFor(2.5f, this);
            }
            else
            {
                Director.Audio.PlayAt(Director.Assets.LockerDoor, path[0], 0.45f, 1.5f);
            }
        }

        private void UpdateCrawl(Vector2 look)
        {
            HoldingBreath = false;
            Breath = Mathf.MoveTowards(Breath, 1f, Time.deltaTime * 0.15f);
            Stamina = Mathf.MoveTowards(Stamina, 1f, Time.deltaTime * 0.1f);
            crawlLookYaw = Mathf.Clamp(crawlLookYaw + look.x, -70f, 70f);
            crawlLookPitch = Mathf.Clamp(crawlLookPitch - look.y, -45f, 45f);

            Vector3 from = crawlPath[crawlSegment];
            Vector3 to = crawlPath[crawlSegment + 1];
            float length = Mathf.Max(0.01f, Vector3.Distance(from, to));
            Vector3 direction = (to - from) / length;
            bool vertical = Mathf.Abs(to.y - from.y) > 0.8f * length;

            // 1. Kneel down in front of the grate, facing it, while it swings open.
            if (crawlKneel < 1f)
            {
                crawlKneel = Mathf.Min(1f, crawlKneel + Time.deltaTime / KneelSeconds);
                float k = crawlKneel * crawlKneel * (3f - 2f * crawlKneel);
                Vector3 into = to - from;
                float facing = Mathf.Atan2(into.x, into.z) * Mathf.Rad2Deg;
                float lookDown = vertical || into.y < -0.3f ? 55f : 15f;
                // The head dips lower than the end pose half-way through: getting down on the knees.
                Vector3 position = Vector3.Lerp(kneelFromPosition, from, k) - Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.12f;
                head.position = position;
                head.rotation = Quaternion.Slerp(kneelFromRotation, Quaternion.Euler(lookDown, facing, 0f), k);
                crawlYaw = facing;
                return;
            }

            // 3. At the far end: stop, push the grate open, wait for it to swing.
            bool lastSegment = crawlSegment == crawlPath.Count - 2;
            if (lastSegment && crawlExitPause < 0f)
            {
                crawlExitPause = 0f;
                if (crawlExit != null)
                {
                    crawlExit.OpenFor(2.5f, this);
                }
            }
            if (lastSegment && crawlExitPause < ExitPauseSeconds)
            {
                crawlExitPause += Time.deltaTime;
                // A shove against the grate: the view jolts forward a little.
                float shove = Mathf.Sin(Mathf.Clamp01(crawlExitPause / 0.25f) * Mathf.PI) * 0.06f;
                head.position = from + direction * shove;
                head.rotation = Quaternion.Euler(crawlLookPitch, crawlYaw + crawlLookYaw, 0f);
                return;
            }

            // 2. Hands and knees: each push moves you forward, then a short pause.
            float baseSpeed = crawlSegment == 0 || lastSegment ? CrawlSpeed * 0.55f : vertical ? CrawlSpeed * 0.8f : CrawlSpeed;
            crawlRhythm += Time.deltaTime * 7.5f;
            float push = 0.35f + 0.65f * Mathf.Pow(Mathf.Abs(Mathf.Sin(crawlRhythm * 0.5f)), 1.5f);
            float step = Time.deltaTime * baseSpeed * push * 1.25f;
            crawlAlong += step;
            crawlStepDistance += step;
            if (crawlStepDistance > 0.6f)
            {
                // Hands and knees on sheet metal: a dull clank, too soft to carry.
                crawlStepDistance = 0f;
                Director.Audio.Footstep(0.12f);
            }
            if (crawlAlong >= length)
            {
                crawlAlong -= length;
                crawlSegment++;
                if (crawlSegment >= crawlPath.Count - 1)
                {
                    FinishCrawl();
                    return;
                }
                from = crawlPath[crawlSegment];
                to = crawlPath[crawlSegment + 1];
                length = Mathf.Max(0.01f, Vector3.Distance(from, to));
                direction = (to - from) / length;
                vertical = Mathf.Abs(to.y - from.y) > 0.8f * length;
            }
            if (!vertical && new Vector2(direction.x, direction.z).sqrMagnitude > 0.01f)
            {
                // Corners turn smoothly, not on the spot.
                crawlYaw = Mathf.MoveTowardsAngle(crawlYaw, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, Time.deltaTime * 160f);
            }
            // Going down a shaft you look down it; climbing, you look up.
            float slopePitch = vertical ? (direction.y < 0f ? 60f : -55f) : 0f;
            float bob = -Mathf.Abs(Mathf.Sin(crawlRhythm * 0.5f)) * 0.045f;
            float roll = Mathf.Sin(crawlRhythm * 0.5f) * 3.5f;
            float sway = Mathf.Sin(crawlRhythm * 0.25f) * 2f;
            // Climbing out: the head rises from the duct to a crouch.
            float rise = lastSegment ? Mathf.SmoothStep(0f, 1f, crawlAlong / length) : 0f;
            head.position = Vector3.Lerp(from, to, Mathf.Clamp01(crawlAlong / length)) + Vector3.up * (bob * (1f - rise));
            head.rotation = Quaternion.Euler(slopePitch + crawlLookPitch + bob * 40f, crawlYaw + crawlLookYaw + sway, roll * (1f - rise));
        }

        private void FinishCrawl()
        {
            Vector3 stand = crawlPath[crawlPath.Count - 1] - Vector3.up * 1.0f;
            Crawling = false;
            transform.position = stand;
            body.enabled = true;
            yaw = crawlYaw;
            pitch = 0f;
            head.localPosition = new Vector3(0f, CrouchHeight - 0.15f, 0f);
            MakeNoise(1);
        }

        /// <summary>Moves the player out of any spot and back to a position (new night).</summary>
        public void ResetTo(Vector3 position, float facing)
        {
            if (InFocus)
            {
                EndFocus();
            }
            Crawling = false;
            hideMotion = 1f;
            leaveMotion = 1f;
            if (HiddenIn != null)
            {
                HiddenIn.Occupied = false;
            }
            HiddenIn = null;
            HoldingBreath = false;
            Breath = 1f;
            body.enabled = false;
            transform.position = position;
            body.enabled = true;
            yaw = facing;
            pitch = 0f;
            Crouching = false;
            Sliding = false;
            wasRunning = false;
            Controls.ResetToggles();
            Flashlight.enabled = true;
            noiseSinceSample = 0;
            Stamina = 1f;
            Winded = false;
            Fear = 0f;
            Lures = 0;
        }

        private void Update()
        {
            Controls.Poll();
            Moving = false;
            Running = false;
            Target = null;

            if (Crawling)
            {
                // A crawl, once started, goes on to the other end (it only stops when the game pauses).
                UpdateCrawl(Controllable ? Controls.Look : Vector2.zero);
                return;
            }
            if (InFocus)
            {
                // The eyes always move to the panel; the pointer only works when the player has control.
                UpdateFocus(Controllable);
                return;
            }
            if (!Controllable)
            {
                HoldingBreath = false;
                PlaceCamera();
                return;
            }
            if (leaveMotion < 1f)
            {
                // Climbing out of a hiding spot: no walking until the feet are out.
                PlaceCamera();
                return;
            }

            Vector2 look = Controls.Look;
            UpdateFear();
            if (Controls.ThrowPressed && HiddenIn == null && !Crawling && Lures > 0)
            {
                Lures--;
                ThrownLure.Throw(Director, View.transform.position + View.transform.forward * 0.6f, View.transform.forward);
                MakeNoise(1);
            }
            if (Controls.FlashlightPressed && HiddenIn == null)
            {
                Flashlight.enabled = !Flashlight.enabled;
                Director.Audio.Click();
            }

            if (HiddenIn != null)
            {
                UpdateHidden(look);
            }
            else
            {
                UpdateWalking(look);
            }
            UpdateInteraction();
            PlaceCamera();
        }

        private void UpdateWalking(Vector2 look)
        {
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
            HoldingBreath = false;
            Breath = Mathf.MoveTowards(Breath, 1f, Time.deltaTime * 0.15f);

            Vector2 input = Controls.Move;

            // Crouch pressed while running: slide in the current direction.
            if (!Sliding && wasRunning && Controls.CrouchPressed && body.isGrounded && Time.time >= slideCooldownUntil)
            {
                Sliding = true;
                slideTimer = SlideDuration;
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
                slideDirection = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
            }
            if (Sliding)
            {
                slideTimer -= Time.deltaTime;
                if (slideTimer <= 0f || !body.isGrounded)
                {
                    Sliding = false;
                    slideCooldownUntil = Time.time + SlideCooldown;
                }
            }

            // Crashing into a closed door while running or sliding shoves it open (the slide goes on) and costs a little stamina.
            if (Sliding || wasRunning)
            {
                Vector3 heading = Sliding ? slideDirection : Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
                Door bumped = heading.sqrMagnitude > 0.001f ? Door.ClosedNear(transform.position + heading.normalized * 0.6f, 0.5f) : null;
                if (bumped != null && bumped.Bump(this))
                {
                    Stamina = Mathf.Max(0f, Stamina - DoorBumpStamina);
                    doorSlowUntil = Time.time + DoorBumpSlowSeconds;
                }
            }

            Crouching = Controls.Crouch || Sliding;
            float targetHeight = Crouching ? CrouchHeight : StandHeight;
            body.height = Mathf.MoveTowards(body.height, targetHeight, Time.deltaTime * 4f);
            body.center = new Vector3(0f, body.height * 0.5f, 0f);

            Running = !Crouching && !Winded && Stamina > 0f && Controls.Sprint && input.y > 0.1f;
            wasRunning = Running;
            UpdateStamina();
            float speed = Sliding ? Mathf.Lerp(CrouchSpeed, SlideStartSpeed, slideTimer / SlideDuration)
                        : Crouching ? CrouchSpeed : Running ? RunSpeed * (1f - 0.28f * Fatigue) : WalkSpeed * (1f - 0.08f * Fatigue);
            if (Time.time < doorSlowUntil)
            {
                speed *= 1f - DoorBumpSlow;
            }
            Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
            Vector3 move = Sliding ? slideDirection * speed : facing * new Vector3(input.x, 0f, input.y) * speed;

            verticalSpeed = body.isGrounded ? -1f : verticalSpeed - 9.81f * Time.deltaTime;
            move.y = verticalSpeed;
            body.Move(move * Time.deltaTime);

            Moving = input.sqrMagnitude > 0.01f || Sliding;
            if (Moving)
            {
                MakeNoise(Sliding ? 2 : Crouching ? 0 : Running ? 2 : 1);
                bobPhase += Time.deltaTime * speed * 2.2f;
                stepDistance += speed * Time.deltaTime;
                float stride = Running ? 2.0f : 1.6f;
                if (stepDistance >= stride)
                {
                    stepDistance = 0f;
                    Director.Audio.Footstep(Crouching ? 0.15f : Running ? 0.8f : 0.45f);
                }
            }
        }

        private void UpdateStamina()
        {
            if (Running)
            {
                lastSprintAt = Time.time;
                Stamina = Mathf.Max(0f, Stamina - Time.deltaTime / 4.5f * (1f + Fear * 0.5f) * (1f + 0.9f * Fatigue));
                if (Stamina <= 0f)
                {
                    Winded = true;
                }
            }
            else if (Time.time - lastSprintAt > 1.2f)
            {
                Stamina = Mathf.Min(1f, Stamina + Time.deltaTime * (Moving ? 0.12f : 0.22f) * (1f - 0.55f * Fatigue));
            }
            if (Winded && Stamina >= 0.35f)
            {
                Winded = false;
            }
            // Out of breath: panting that Freddy can hear in the same room.
            if ((Winded || Fear > 0.75f) && Time.time >= nextPantAt)
            {
                nextPantAt = Time.time + 0.9f;
                MakeNoise(1);
                Director.Audio.Pant(Winded ? 0.5f : 0.3f);
            }
        }

        private void UpdateFear()
        {
            FreddyBody freddy = Director.Freddy;
            float distance = Vector3.Distance(transform.position, freddy.transform.position);
            float target = Mathf.Clamp01(1f - (distance - 3f) / 12f) * 0.7f + (freddy.SeesPlayer ? 0.5f : 0f);
            Fear = Mathf.MoveTowards(Fear, Mathf.Clamp01(target), Time.deltaTime * (target > Fear ? 0.8f : 0.15f));

            // Terrified, the flashlight sputters (hands shaking, a bad contact).
            if (Fear > 0.6f && HiddenIn == null && Random.value < Time.deltaTime * Fear * 1.5f)
            {
                flashlightGlitchUntil = Time.time + Random.Range(0.08f, 0.3f);
            }
            flashlightRig.Glitching = Time.time < flashlightGlitchUntil;
        }

        private void UpdateHidden(Vector2 look)
        {
            float range = HiddenIn.Kind == SpotKind.Locker ? 50f : 70f;
            if (hideMotion >= 1f)
            {
                hideYaw = Mathf.Clamp(hideYaw + look.x, -range, range);
                hidePitch = Mathf.Clamp(hidePitch - look.y, -30f, 30f);
            }

            bool wantsToHold = Controls.HoldBreath && Time.time >= breathBlockedUntil;
            HoldingBreath = wantsToHold && Breath > 0f;
            if (HoldingBreath)
            {
                Breath = Mathf.MoveTowards(Breath, 0f, Time.deltaTime * (0.18f + Fear * 0.12f));
                if (Breath <= 0f)
                {
                    // Out of air: a loud gasp, and no holding for a moment.
                    HoldingBreath = false;
                    breathBlockedUntil = Time.time + 2.5f;
                    MakeNoise(2);
                    Director.Audio.Gasp();
                }
            }
            else
            {
                Breath = Mathf.MoveTowards(Breath, 1f, Time.deltaTime * 0.12f);
            }
        }

        private void UpdateInteraction()
        {
            if (HiddenIn != null)
            {
                Target = HiddenIn;
            }
            else if (Physics.Raycast(View.transform.position, View.transform.forward, out RaycastHit hit, InteractDistance,
                                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            {
                Target = hit.collider.GetComponentInParent<IInteractable>();
            }
            // Walking through a doorway, its door is always at hand: no need to aim at it.
            if (Target == null && HiddenIn == null)
            {
                Target = Door.InDoorway(transform.position, DoorwayReach);
            }

            if (Target == null)
            {
                return;
            }
            if (Target is Door && !doorHintShown && !Director.InCall)
            {
                doorHintShown = true;
                Director.UI.Toast(Controls.Localize("You can close doors behind you  [E]   He has to break them down"));
            }
            if (Target.HoldToUse ? Controls.UseHeld : Controls.UsePressed)
            {
                Target.Use(this);
            }
        }

        /// <summary>
        /// Hidden in the dark the eyes adapt over a couple of seconds: the picture brightens and a
        /// faint cold fill (what the eye makes of the little light there is) shows the inside of
        /// the spot and the room through the gap. Out of the spot it is gone at once.
        /// </summary>
        private void UpdateEyeAdaptation()
        {
            bool adapting = HiddenIn != null && hideMotion >= 1f;
            eyeAdaptation = Mathf.MoveTowards(eyeAdaptation, adapting ? 1f : 0f, Time.deltaTime * (adapting ? 0.5f : 3f));
            if (adaptationLight == null)
            {
                adaptationLight = new GameObject("EyeAdaptation").AddComponent<Light>();
                // Out in the room, not at the eyes: up close it would burn the vents white.
                adaptationLight.transform.SetParent(View.transform, false);
                adaptationLight.transform.localPosition = new Vector3(0f, 0f, 2.2f);
                adaptationLight.type = LightType.Point;
                adaptationLight.shadows = LightShadows.None;
                adaptationLight.range = 5f;
                adaptationLight.color = new Color(0.68f, 0.72f, 0.8f);
            }
            float k = Mathf.SmoothStep(0f, 1f, eyeAdaptation);
            adaptationLight.intensity = 0.8f * k;
            adaptationLight.enabled = k > 0.001f;
            Director.SetEyeAdaptation(k);
        }

        /// <summary>
        /// Climbing into a spot: up to its front looking in (the door is swinging open), then in,
        /// turning around to face out; under a table or into a cabinet the head ducks low.
        /// </summary>
        private void PlaceClimbingIn(HidingSpot spot, float progress)
        {
            Transform point = spot.HidePoint;
            bool locker = spot.Kind == SpotKind.Locker;
            Vector3 front = spot.ExitPoint.position + Vector3.up * (locker ? 1.6f : 0.8f);
            Vector3 inward = point.position - front;
            float inYaw = Mathf.Atan2(inward.x, inward.z) * Mathf.Rad2Deg;
            float inPitch = -Mathf.Atan2(inward.y, new Vector2(inward.x, inward.z).magnitude) * Mathf.Rad2Deg;
            const float split = 0.45f;
            if (progress < split)
            {
                float a = Ease(progress / split);
                head.position = Vector3.Lerp(hideFromPosition, front, a);
                head.rotation = Quaternion.Slerp(hideFromRotation, Quaternion.Euler(inPitch, inYaw, 0f), a);
                return;
            }
            float b = Ease((progress - split) / (1f - split));
            // Turning around always the same way, never a random spin.
            float turn = Mathf.DeltaAngle(inYaw, point.eulerAngles.y);
            if (Mathf.Abs(turn) > 170f)
            {
                turn = 180f;
            }
            float duck = Mathf.Sin(b * Mathf.PI) * (locker ? 0.04f : 0.14f);
            head.position = Vector3.Lerp(front, point.position, b) - Vector3.up * duck;
            head.rotation = Quaternion.Euler(Mathf.Lerp(inPitch, 0f, b), inYaw + turn * b, 0f);
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>A short knock of the view (a heavy clunk, a lever hitting its stop), in degrees; it dies away in a moment.</summary>
        public void Jolt(float degrees)
        {
            jolt = Mathf.Max(jolt, degrees);
        }

        private float jolt;

        private Quaternion JoltShake()
        {
            if (jolt <= 0.001f)
            {
                return Quaternion.identity;
            }
            jolt = Mathf.MoveTowards(jolt, 0f, Time.deltaTime * (jolt * 7f + 0.4f));
            float t = Time.time * 40f;
            return Quaternion.Euler((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * jolt,
                                    (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * jolt,
                                    (Mathf.PerlinNoise(t, t) - 0.5f) * jolt);
        }

        /// <summary>Turns the view smoothly toward a point (working a panel, looking at what the hands do).</summary>
        public void LookToward(Vector3 point, float speed)
        {
            Vector3 direction = point - head.position;
            var flat = new Vector2(direction.x, direction.z);
            if (flat.sqrMagnitude < 0.0001f)
            {
                return;
            }
            float k = 1f - Mathf.Exp(-speed * Time.deltaTime);
            yaw = Mathf.LerpAngle(yaw, Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, k);
            pitch = Mathf.LerpAngle(pitch, -Mathf.Atan2(direction.y, flat.magnitude) * Mathf.Rad2Deg, k);
        }

        private void PlaceCamera()
        {
            UpdateEyeAdaptation();
            if (CameraLocked || Crawling || InFocus)
            {
                return;
            }
            View.fieldOfView = Mathf.MoveTowards(View.fieldOfView, HiddenIn != null ? HiddenFieldOfView : FieldOfView, Time.deltaTime * 40f);
            if (HiddenIn != null)
            {
                Transform point = HiddenIn.HidePoint;
                if (hideMotion < 1f)
                {
                    hideMotion = Mathf.Min(1f, hideMotion + Time.deltaTime / hideMotionSeconds);
                    PlaceClimbingIn(HiddenIn, Mathf.Clamp01(hideMotion / MoveShare));
                    if (hideMotion >= DoorShare && !doorPulled)
                    {
                        doorPulled = true;
                        Director.Audio.PlayAt(Director.Assets.LockerDoor, HiddenIn.transform.position, 0.4f, 0.8f);
                    }
                    if (hideMotion >= FadeStart && !beamClicked)
                    {
                        beamClicked = true;
                        Director.Audio.Click();
                    }
                    flashlightRig.Dim = 1f - Mathf.SmoothStep(0f, 1f, (hideMotion - FadeStart) / (FadeEnd - FadeStart));
                    if (hideMotion >= 1f)
                    {
                        Flashlight.enabled = false;
                        flashlightRig.Dim = 1f;
                    }
                    return;
                }
                head.position = point.position + Vector3.up * BreathRise();
                head.rotation = point.rotation * Quaternion.Euler(hidePitch, hideYaw, 0f) * BreathSway();
                return;
            }
            float eye = body.height - 0.15f;
            // Out of the spot the flashlight comes back up in a moment.
            flashlightRig.Dim = Mathf.MoveTowards(flashlightRig.Dim, 1f, Time.deltaTime * 4f);
            if (leaveMotion < 1f)
            {
                // Out of the spot: from inside to standing in front of it, already facing the room.
                leaveMotion = Mathf.Min(1f, leaveMotion + Time.deltaTime / hideMotionSeconds);
                float k = Ease(leaveMotion);
                Vector3 standing = transform.position + Vector3.up * eye;
                head.position = Vector3.Lerp(hideFromPosition, standing, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.04f);
                head.rotation = Quaternion.Slerp(hideFromRotation, Quaternion.Euler(pitch, yaw, 0f), k);
                return;
            }
            float bob = Moving ? Mathf.Sin(bobPhase) * (Running ? 0.05f : 0.025f) : 0f;
            head.localPosition = Vector3.Lerp(head.localPosition, new Vector3(0f, eye + bob + BreathRise(), 0f), Time.deltaTime * 12f);
            head.rotation = Quaternion.Euler(pitch, yaw, 0f) * BreathSway() * JoltShake();
        }

        /// <summary>
        /// Breathing: the view drifts a fraction of a degree with each breath, slow when calm,
        /// faster and wider when afraid or winded, almost still while holding the breath.
        /// </summary>
        private Quaternion BreathSway()
        {
            breathPhase += Time.deltaTime * (1.6f + Fear * 2.5f + (Winded ? 2.5f : 0f));
            float amount = BreathAmount();
            return Quaternion.Euler(Mathf.Sin(breathPhase) * amount * 0.5f,
                                    Mathf.Sin(breathPhase * 0.5f + 0.7f) * amount * 0.35f,
                                    Mathf.Sin(breathPhase * 0.5f) * amount * 0.3f);
        }

        private float BreathRise() => Mathf.Sin(breathPhase) * 0.004f * BreathAmount();

        private float BreathAmount() => (0.25f + Fear * 0.6f + (1f - Stamina) * 0.7f + Fatigue * 0.35f) * (HoldingBreath ? 0.15f : 1f);

        /// <summary>Moves the body as if running, for scripted moments (the 6 AM sequence); walls still stop it.</summary>
        public void ScriptedRun(Vector3 direction, float speed)
        {
            Moving = true;
            Running = true;
            body.Move((direction.normalized * speed + Vector3.down) * Time.deltaTime);
            bobPhase += Time.deltaTime * speed * 2.2f;
            stepDistance += speed * Time.deltaTime;
            if (stepDistance >= 2f)
            {
                stepDistance = 0f;
                Director.Audio.Footstep(0.85f);
            }
        }
    }
}
