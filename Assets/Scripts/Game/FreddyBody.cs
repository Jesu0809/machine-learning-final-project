using System.Collections.Generic;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Freddy's body in the 3D game. It carries out the brain's decisions (walk to a room, stay and
    /// listen, search a hiding spot) and handles what the brain does not: sight with a view cone
    /// blocked by walls, chasing a player in view, and the attack. Nothing here is learned; the
    /// learned part is <see cref="Brain.FreddyAgent"/>, which chooses the next action.
    /// </summary>
    public class FreddyBody : MonoBehaviour
    {
        public const float WalkSpeed = 2.9f;
        public const float ChaseSpeed = 4.7f;
        public const float AttackRange = 1.4f;
        public const float SearchSeconds = 1.8f;
        public const float LoseSightSeconds = 3f;
        public const float SightRange = 10f;
        public const float SightRangeFlashlight = 16f;
        public const float StealthRange = 5f;

        private enum Mode
        {
            Asleep,
            Idle,
            Listening,
            Walking,
            Searching,
            Chasing,
            Attacking
        }

        private readonly List<Vector3> path = new List<Vector3>();
        private HuntDirector director;
        private Animator animator;
        private Mode mode = Mode.Asleep;
        private float modeTimer;
        private float speed;
        private float stepDistance;
        private float nextLookAt;
        private float lastSeenAt;
        private Vector3 lastSeenPosition;
        private HidingSpot searchTarget;
        private HidingSpot seenHidingIn;

        /// <summary>True when the body has finished its action and waits for the brain's next one.</summary>
        public bool Ready => mode == Mode.Idle;

        public bool Chasing => mode == Mode.Chasing;
        public bool Attacking => mode == Mode.Attacking;

        /// <summary>True while the player is in Freddy's view right now.</summary>
        public bool SeesPlayer { get; private set; }

        /// <summary>Short description of what the body is doing, for the AI inspector.</summary>
        public string Activity { get; private set; } = "asleep on the stage";

        public Room Room => PizzeriaLayout.RoomAt(transform.position);
        public Vector3 EyePosition => transform.position + Vector3.up * 1.85f;

        public static FreddyBody Create(HuntDirector director, GameObject prefab, Vector3 position, float facing)
        {
            GameObject go = prefab != null ? Instantiate(prefab) : Fallback();
            go.name = "Freddy";
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, facing, 0f);
            foreach (Collider collider in go.GetComponentsInChildren<Collider>())
            {
                Destroy(collider);
            }
            var body = go.AddComponent<FreddyBody>();
            body.director = director;
            body.animator = go.GetComponentInChildren<Animator>();
            body.SetUpEyes();
            body.servo = director.Audio.Attach3DLoop(go.transform, director.Audio.ServoLoop, 14f);
            return body;
        }

        private Light eyeLight;
        private readonly List<Material> eyeMaterials = new List<Material>();
        private AudioSource servo;

        /// <summary>The glowing eyes that are often all you see of him in the dark.</summary>
        private void SetUpEyes()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] materials = renderer.materials;
                foreach (Material material in materials)
                {
                    if (material.name.StartsWith("Eye") && material.IsKeywordEnabled("_EMISSION"))
                    {
                        eyeMaterials.Add(material);
                    }
                }
            }
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            eyeLight = new GameObject("EyeGlow").AddComponent<Light>();
            eyeLight.transform.SetParent(head != null ? head : transform, false);
            eyeLight.transform.localPosition = head != null ? new Vector3(0f, 0.1f, 0.25f) : new Vector3(0f, 1.9f, 0.3f);
            eyeLight.type = LightType.Point;
            eyeLight.range = 2.5f;
            eyeLight.intensity = 1.2f;
            eyeLight.color = new Color(0.8f, 0.85f, 1f);
            Head = head != null ? head : transform;
        }

        /// <summary>Head bone (the jumpscare camera looks at it).</summary>
        public Transform Head { get; private set; }

        private void UpdateEyes()
        {
            bool hunting = mode == Mode.Chasing || mode == Mode.Attacking;
            Color color = hunting ? new Color(1f, 0.08f, 0.05f) : new Color(0.75f, 0.85f, 1f);
            float pulse = mode == Mode.Asleep ? 0.15f : hunting ? 2.2f : 1f;
            if (Time.time < sensingUntil)
            {
                color = new Color(0.8f, 0.2f, 1f);
                pulse = 3f + Mathf.Sin(Time.time * 20f);
            }
            eyeLight.color = color;
            eyeLight.intensity = pulse * (hunting ? 2f : 1f);
            foreach (Material material in eyeMaterials)
            {
                material.SetColor("_EmissionColor", color * (1.5f * pulse));
            }
            if (servo != null)
            {
                servo.mute = !SoundPrefs.EffectsOn;
                float moving = mode == Mode.Walking || mode == Mode.Chasing ? 1f : 0.15f;
                servo.volume = Mathf.MoveTowards(servo.volume, mode == Mode.Asleep ? 0f : 0.55f * moving, Time.deltaTime);
                servo.pitch = mode == Mode.Chasing ? 1.25f : 1f;
            }
        }

        /// <summary>Back to the stage, asleep (new night).</summary>
        public void ResetTo(Vector3 position, float facing)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, facing, 0f));
            path.Clear();
            searchTarget = null;
            seenHidingIn = null;
            SeesPlayer = false;
            SetMode(Mode.Asleep, "asleep on the stage");
            if (animator != null)
            {
                animator.Rebind();
            }
        }

        /// <summary>True while he sleeps on the stage (the brain's decisions wait until he wakes).</summary>
        public bool IsAsleep => mode == Mode.Asleep;

        public void WakeUp()
        {
            if (mode == Mode.Asleep)
            {
                SetMode(Mode.Idle, "awake");
            }
        }

        /// <summary>Carries out one of the brain's actions (see <see cref="FreddyActions"/>).</summary>
        public void Execute(int action, float listenSeconds)
        {
            if (mode == Mode.Chasing || mode == Mode.Attacking)
            {
                return;
            }
            Room here = Room;
            if (FreddyActions.IsSense(action))
            {
                // Stands still, eyes blazing, feeling for fear in the building.
                SetMode(Mode.Listening, "using SENSE, feeling for your fear");
                modeTimer = 1.8f;
                sensingUntil = Time.time + 1.8f;
                return;
            }
            if (FreddyActions.IsSearch(action))
            {
                int spot = FreddyActions.SpotOf(action);
                searchTarget = director.Level.Spot(here, spot);
                WalkTo(PizzeriaLayout.SpotApproach(here, spot), WalkSpeed);
                Activity = "going to search " + Describe(searchTarget);
                return;
            }

            var target = (Room)action;
            if (target == here)
            {
                // "Stay" is not standing in the middle: he prowls to a corner of the room, stops and listens.
                listenAfterArriving = listenSeconds * 0.5f;
                WalkTo(PointOfInterest(here), WalkSpeed * 0.8f);
                Activity = "prowling the " + HuntMap.DisplayName(here) + ", listening";
                return;
            }
            // Into the new room: sometimes to its middle, often straight to a corner or a hiding spot.
            Vector3 destination = Random.value < 0.4f ? PizzeriaLayout.Center(target) : PointOfInterest(target);
            WalkTo(destination, WalkSpeed);
            Activity = "walking to " + HuntMap.DisplayName(target);
        }

        private void Update()
        {
            UpdateEyes();
            if (!director.Playing)
            {
                if (mode != Mode.Attacking)
                {
                    SetAnimatorSpeed(0f);
                }
                return;
            }

            if (mode != Mode.Asleep && mode != Mode.Attacking && Time.time >= nextLookAt)
            {
                nextLookAt = Time.time + 0.1f;
                Look();
            }

            switch (mode)
            {
                case Mode.Asleep:
                case Mode.Idle:
                    SetAnimatorSpeed(0f);
                    break;
                case Mode.Listening:
                    SetAnimatorSpeed(0f);
                    TurnSlowly();
                    modeTimer -= Time.deltaTime;
                    if (modeTimer <= 0f)
                    {
                        SetMode(Mode.Idle, "waiting for the brain");
                    }
                    break;
                case Mode.Walking:
                    if (FollowPath())
                    {
                        Arrived();
                    }
                    break;
                case Mode.Searching:
                    UpdateSearch();
                    break;
                case Mode.Chasing:
                    UpdateChase();
                    break;
            }
        }

        // ------------------------------------------------------------------ senses

        private void Look()
        {
            PlayerController player = director.Player;
            SeesPlayer = CanSee(player);
            if (!SeesPlayer)
            {
                return;
            }
            lastSeenAt = Time.time;
            lastSeenPosition = player.transform.position;
            director.OnFreddySawPlayer();
            if (mode != Mode.Chasing)
            {
                searchTarget = null;
                SetMode(Mode.Chasing, "chasing you");
                listenAfterArriving = 0f;
                fast = true;
                speed = ChaseSpeed * RageScale(true);
                director.Audio.Sting();
            }
        }

        private bool CanSee(PlayerController player)
        {
            Vector3 eye = EyePosition;
            Vector3 head = player.HeadPosition;
            Vector3 toPlayer = head - eye;
            float distance = toPlayer.magnitude;

            float range = player.Flashlight.enabled ? SightRangeFlashlight : SightRange;
            if (player.Crouching)
            {
                // Crouched in the dark you are a shadow among the tables (Alien: Isolation).
                range = player.Flashlight.enabled ? range * 0.7f : StealthRange;
            }
            if (distance > range || player.Crawling)
            {
                // Nobody sees into the air ducts.
                return false;
            }
            if (player.HiddenIn != null)
            {
                // Hidden players are invisible, unless Freddy was watching them a moment ago:
                // then he saw which spot they climbed into.
                if (mode == Mode.Chasing && Time.time - lastSeenAt < 1.2f && seenHidingIn == null)
                {
                    seenHidingIn = player.HiddenIn;
                }
                return false;
            }
            bool close = distance < 2.2f;
            if (!close && Vector3.Angle(transform.forward, toPlayer) > 60f)
            {
                return false;
            }
            if (Physics.Linecast(eye, head, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            seenHidingIn = null;
            return true;
        }

        // ------------------------------------------------------------------ actions

        private void UpdateChase()
        {
            PlayerController player = director.Player;
            if (seenHidingIn != null)
            {
                // He saw where you went: straight to that spot.
                searchTarget = seenHidingIn;
                seenHidingIn = null;
                WalkTo(PizzeriaLayout.SpotApproach(searchTarget.Room, searchTarget.Index), ChaseSpeed);
                Activity = "saw you hide, going to " + Describe(searchTarget);
                return;
            }

            if (SeesPlayer)
            {
                if (Vector3.Distance(transform.position, player.transform.position) < AttackRange)
                {
                    Attack("He saw you in the " + HuntMap.DisplayName(PizzeriaLayout.RoomAt(player.transform.position)) + " and ran you down.");
                    return;
                }
                PathTo(player.transform.position);
            }
            else if (Time.time - lastSeenAt > LoseSightSeconds)
            {
                // Lost the player: check the last place they were seen, then hand back to the brain.
                WalkTo(lastSeenPosition, ChaseSpeed);
                Activity = "lost you, checking where you were";
                return;
            }
            else if (path.Count == 0)
            {
                PathTo(lastSeenPosition);
            }
            FollowPath();
        }

        private Door breakingDoor;
        private float nextBlowAt;
        private int blows;

        private void BreakDoor(Door door)
        {
            SetAnimatorSpeed(0f);
            FaceTowards(door.transform.position);
            if (breakingDoor != door)
            {
                breakingDoor = door;
                blows = 0;
                nextBlowAt = Time.time + 0.4f;
                Activity = "breaking down a door";
                SetSearching(true);
            }
            if (Time.time >= nextBlowAt)
            {
                blows++;
                bool last = blows >= 3;
                door.Hit(last);
                nextBlowAt = Time.time + Door.BreakSeconds / 3f;
                if (last)
                {
                    breakingDoor = null;
                    SetSearching(false);
                }
            }
        }

        private float listenAfterArriving;
        private float sensingUntil;
        private bool fast;

        /// <summary>A random place of the room worth a look: in front of one of its hiding spots, or its hub.</summary>
        private Vector3 PointOfInterest(Room room)
        {
            int spots = HuntMap.SpotCount(room);
            int pick = Random.Range(0, spots + 1);
            Vector3 point = pick < spots ? PizzeriaLayout.SpotApproach(room, pick) : PizzeriaLayout.Center(room);
            if (Vector3.Distance(transform.position, point) < 1f)
            {
                point = PizzeriaLayout.Center(room);
            }
            return point;
        }

        /// <summary>
        /// Gets faster as the player gets closer to escaping, like Jason's rage in Friday the 13th:
        /// +10% walking and +6% chasing per fuse, and once more when the exit panel starts working.
        /// </summary>
        private float RageScale(bool chasing)
        {
            int level = director.FusesCollected + (director.Level.Exit.Progress > 0f ? 1 : 0);
            return (1f + level * (chasing ? 0.06f : 0.1f)) * NightSpeed;
        }

        /// <summary>Speed multiplier of the current night (1 on night 1).</summary>
        public float NightSpeed { get; set; } = 1f;

        private void UpdateSearch()
        {
            SetAnimatorSpeed(0f);
            FaceTowards(searchTarget.transform.position);
            modeTimer -= Time.deltaTime;
            if (modeTimer > 0f)
            {
                return;
            }

            PlayerController player = director.Player;
            searchTarget.Rattle(1.2f);
            director.Audio.PlayAt(director.Assets.LockerDoor, searchTarget.transform.position, 0.9f, 0.8f);
            SetSearching(false);
            HidingSpot searched = searchTarget;
            searchTarget = null;
            if (player.HiddenIn == searched && !player.HoldingBreath)
            {
                Attack("He opened " + Describe(searched) + " in the " + HuntMap.DisplayName(searched.Room) + " and found you breathing.");
                return;
            }
            director.OnSpotSearched(searched, player.HiddenIn == searched);
            SetMode(Mode.Idle, "searched " + Describe(searched) + ": nothing");
        }

        private void Arrived()
        {
            if (searchTarget != null)
            {
                SetMode(Mode.Searching, "searching " + Describe(searchTarget));
                modeTimer = SearchSeconds;
                SetSearching(true);
                return;
            }
            if (mode == Mode.Walking && fast)
            {
                director.OnFreddyLostPlayer();
            }
            if (listenAfterArriving > 0f)
            {
                SetMode(Mode.Listening, "standing still, listening");
                modeTimer = listenAfterArriving;
                listenAfterArriving = 0f;
                return;
            }
            SetMode(Mode.Idle, "arrived");
        }

        private void Attack(string how)
        {
            SetMode(Mode.Attacking, "caught you");
            path.Clear();
            SetSearching(false);
            if (animator != null)
            {
                animator.SetTrigger("Attack");
            }
            director.OnCaught(how);
        }

        // ------------------------------------------------------------------ movement

        private void WalkTo(Vector3 target, float moveSpeed)
        {
            PathTo(target);
            fast = moveSpeed >= ChaseSpeed;
            speed = moveSpeed * RageScale(fast);
            SetMode(Mode.Walking, Activity);
        }

        /// <summary>
        /// Waypoints along the room graph: the hub of each room, the doorways and corridors between
        /// them, then the target. Inside a room these are the prop-free lines of
        /// <see cref="PizzeriaLayout.WalkLines"/>.
        /// </summary>
        private void PathTo(Vector3 target)
        {
            path.Clear();
            Room from = Room;
            Room to = PizzeriaLayout.RoomAt(target);
            int guard = 0;
            bool chasing = mode == Mode.Chasing;
            while (from != to && guard++ < HuntMap.RoomCount)
            {
                Room next = HuntMap.NextStep(from, to);
                // A chase goes straight for the doorway; a calm walk keeps to the room's prop-free lines.
                if (!chasing || guard > 1)
                {
                    path.Add(PizzeriaLayout.Center(from));
                }
                path.AddRange(PizzeriaLayout.PassageBetween(from, next));
                from = next;
            }
            Vector3 goal = Grounded(target);
            if (Vector3.Distance(goal, PizzeriaLayout.Center(to)) > 0.5f && !chasing)
            {
                path.Add(PizzeriaLayout.Center(to));
            }
            path.Add(goal);

            // Drop waypoints Freddy has already passed: one that is behind him (the next one is
            // closer to him than to it) or that he is standing on. The path is rebuilt every frame
            // during a chase, so without this he would step back and forth in doorways.
            Vector3 here = transform.position;
            while (path.Count > 1)
            {
                bool onIt = Vector3.Distance(here, path[0]) < 0.3f;
                bool behind = Vector3.Distance(here, path[1]) <= Vector3.Distance(path[0], path[1]);
                if (!onIt && !behind)
                {
                    break;
                }
                path.RemoveAt(0);
            }
        }

        private static Vector3 Flat(Vector3 point) => new Vector3(point.x, 0f, point.z);

        /// <summary>The point on the floor of the room it is in (the floors are at different heights).</summary>
        private static Vector3 Grounded(Vector3 point) => new Vector3(point.x, PizzeriaLayout.Elevation(PizzeriaLayout.RoomAt(point)), point.z);

        /// <summary>The points Freddy is about to walk through (for the AI vision overlay).</summary>
        public IReadOnlyList<Vector3> PlannedPath => path;

        /// <summary>Moves along the path; true when the last waypoint is reached.</summary>
        private bool FollowPath()
        {
            if (path.Count == 0)
            {
                SetAnimatorSpeed(0f);
                return true;
            }
            Vector3 position = transform.position;
            // Waypoints carry their floor's height: Freddy climbs the stairs and steps down from the stage.
            Vector3 waypoint = path[0];
            Vector3 offset = waypoint - position;
            float step = speed * Time.deltaTime;
            if (offset.magnitude <= step + 0.05f)
            {
                transform.position = waypoint;
                path.RemoveAt(0);
                return path.Count == 0;
            }
            Vector3 direction = offset.normalized;
            // A closed door in the way: stop and break it down (a few loud blows).
            Door door = Door.ClosedNear(position + new Vector3(direction.x, 0f, direction.z) * 0.9f, 1.0f);
            if (door != null)
            {
                BreakDoor(door);
                return false;
            }
            transform.position = position + direction * step;
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 360f * Time.deltaTime);
            }
            SetAnimatorSpeed(fast ? 2f : 1f);

            stepDistance += step;
            if (stepDistance >= (fast ? 1.6f : 1.1f))
            {
                stepDistance = 0f;
                director.Audio.FreddyStep(transform.position, fast);
            }
            return false;
        }

        private void FaceTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 240f * Time.deltaTime);
            }
        }

        private void TurnSlowly()
        {
            transform.Rotate(0f, Mathf.Sin(Time.time * 0.7f) * 25f * Time.deltaTime, 0f);
        }

        private void SetMode(Mode next, string activity)
        {
            mode = next;
            Activity = activity;
        }

        private void SetAnimatorSpeed(float value)
        {
            if (animator != null)
            {
                animator.SetFloat("Speed", value, 0.15f, Time.deltaTime);
            }
        }

        private void SetSearching(bool value)
        {
            if (animator != null)
            {
                animator.SetBool("Searching", value);
            }
        }

        private static string Describe(HidingSpot spot)
        {
            string kind = spot.Kind == SpotKind.Locker ? "the locker" : spot.Kind == SpotKind.Cabinet ? "a cabinet" : "under a table";
            return kind + " (spot " + spot.Index + ")";
        }

        /// <summary>A plain capsule stand-in, used only if the Freddy prefab is missing.</summary>
        private static GameObject Fallback()
        {
            var go = new GameObject("Freddy");
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.transform.SetParent(go.transform, false);
            capsule.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            capsule.transform.localScale = new Vector3(0.9f, 1.05f, 0.9f);
            return go;
        }
    }
}
