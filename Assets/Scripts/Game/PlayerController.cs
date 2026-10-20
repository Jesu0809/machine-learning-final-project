using UnityEngine;

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

        public HuntDirector Director { get; private set; }
        public Camera View { get; private set; }
        public Light Flashlight { get; private set; }

        public bool Controllable { get; set; }

        /// <summary>While true the camera is placed by someone else (menu view, jumpscare).</summary>
        public bool CameraLocked { get; set; }

        public Transform Head => head;
        public bool Running { get; private set; }
        public bool Crouching { get; private set; }
        public bool Sliding { get; private set; }
        public bool Moving { get; private set; }
        public HidingSpot HiddenIn { get; private set; }

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

            player.Flashlight = new GameObject("Flashlight").AddComponent<Light>();
            player.Flashlight.transform.SetParent(cameraObject.transform, false);
            player.Flashlight.transform.localPosition = new Vector3(0.2f, -0.15f, 0.1f);
            player.Flashlight.type = LightType.Spot;
            player.Flashlight.spotAngle = 55f;
            player.Flashlight.innerSpotAngle = 25f;
            player.Flashlight.range = 18f;
            player.Flashlight.intensity = 6f;
            player.Flashlight.color = new Color(1f, 0.95f, 0.85f);
            player.Flashlight.shadows = LightShadows.Soft;
            player.Flashlight.enabled = true;
            return player;
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
            // Nobody hides with the flashlight on; it comes back on when leaving.
            flashlightBeforeHiding = Flashlight.enabled;
            Flashlight.enabled = false;
            hideYaw = 0f;
            hidePitch = 0f;
            Breath = Mathf.Max(Breath, 0.3f);
            spot.Rattle();
            MakeNoise(1);
            Director.Audio.PlayAt(Director.Assets.LockerDoor, spot.transform.position, 0.5f);
        }

        public void LeaveHidingSpot()
        {
            HidingSpot spot = HiddenIn;
            spot.Occupied = false;
            HiddenIn = null;
            HoldingBreath = false;
            transform.position = spot.ExitPoint.position;
            yaw = spot.transform.eulerAngles.y;
            pitch = 0f;
            body.enabled = true;
            Flashlight.enabled = flashlightBeforeHiding;
            spot.Rattle();
            MakeNoise(1);
            Director.Audio.PlayAt(Director.Assets.LockerDoor, spot.transform.position, 0.5f);
        }

        /// <summary>Moves the player out of any spot and back to a position (new night).</summary>
        public void ResetTo(Vector3 position, float facing)
        {
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

            if (!Controllable)
            {
                HoldingBreath = false;
                PlaceCamera();
                return;
            }

            Vector2 look = Controls.Look;
            UpdateFear();
            if (Controls.ThrowPressed && HiddenIn == null && Lures > 0)
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
                        : Crouching ? CrouchSpeed : Running ? RunSpeed : WalkSpeed;
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
                Stamina = Mathf.Max(0f, Stamina - Time.deltaTime / 4.5f * (1f + Fear * 0.5f));
                if (Stamina <= 0f)
                {
                    Winded = true;
                }
            }
            else if (Time.time - lastSprintAt > 1.2f)
            {
                Stamina = Mathf.Min(1f, Stamina + Time.deltaTime * (Moving ? 0.12f : 0.22f));
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
            Flashlight.intensity = Time.time < flashlightGlitchUntil ? 0.6f : 6f;
        }

        private void UpdateHidden(Vector2 look)
        {
            float range = HiddenIn.Kind == SpotKind.Locker ? 50f : 70f;
            hideYaw = Mathf.Clamp(hideYaw + look.x, -range, range);
            hidePitch = Mathf.Clamp(hidePitch - look.y, -30f, 30f);

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

        private void PlaceCamera()
        {
            if (CameraLocked)
            {
                return;
            }
            View.fieldOfView = Mathf.MoveTowards(View.fieldOfView, HiddenIn != null ? HiddenFieldOfView : FieldOfView, Time.deltaTime * 40f);
            if (HiddenIn != null)
            {
                Transform point = HiddenIn.HidePoint;
                head.position = Vector3.Lerp(head.position, point.position, Time.deltaTime * 10f);
                head.rotation = point.rotation * Quaternion.Euler(hidePitch, hideYaw, 0f);
                return;
            }
            float eye = body.height - 0.15f;
            float bob = Moving ? Mathf.Sin(bobPhase) * (Running ? 0.05f : 0.025f) : 0f;
            head.localPosition = Vector3.Lerp(head.localPosition, new Vector3(0f, eye + bob, 0f), Time.deltaTime * 12f);
            head.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
