using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>Something the player can use with E while looking at it.</summary>
    public interface IInteractable
    {
        /// <summary>Text of the prompt, e.g. "[E] Hide in locker". Empty when it cannot be used now.</summary>
        string Prompt(PlayerController player);

        /// <summary>True if E must be held (the exit panel); false for a single press.</summary>
        bool HoldToUse { get; }

        /// <summary>Called on press, or every frame while held for hold-to-use objects.</summary>
        void Use(PlayerController player);
    }

    /// <summary>
    /// A hiding spot (locker, cabinet or table with a long cloth). The player hides inside with E;
    /// Freddy can search it. Spot indices match <see cref="Hunt.HuntMap"/>.
    /// </summary>
    public class HidingSpot : MonoBehaviour, IInteractable
    {
        public Hunt.Room Room { get; private set; }
        public int Index { get; private set; }
        public SpotKind Kind { get; private set; }

        /// <summary>Camera position and rotation while hidden inside.</summary>
        public Transform HidePoint { get; private set; }

        /// <summary>Where the player stands after leaving the spot.</summary>
        public Transform ExitPoint { get; private set; }

        private Transform door;
        private float doorOpen;
        private float doorTarget;

        /// <summary>True while the player is inside: cabinets and tables stay ajar so the player can peek out.</summary>
        public bool Occupied { get; set; }

        public bool HoldToUse => false;

        public static HidingSpot Attach(GameObject go, Hunt.Room room, int index, SpotKind kind, Transform door,
                                        Vector3 hideLocal, Vector3 exitLocal)
        {
            var spot = go.AddComponent<HidingSpot>();
            spot.Room = room;
            spot.Index = index;
            spot.Kind = kind;
            spot.door = door;

            spot.HidePoint = new GameObject("HidePoint").transform;
            spot.HidePoint.SetParent(go.transform, false);
            spot.HidePoint.localPosition = hideLocal;
            spot.HidePoint.localRotation = Quaternion.identity;

            spot.ExitPoint = new GameObject("ExitPoint").transform;
            spot.ExitPoint.SetParent(go.transform, false);
            spot.ExitPoint.localPosition = exitLocal;
            return spot;
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn == this)
            {
                return "[E] Leave";
            }
            if (player.HiddenIn != null)
            {
                return "";
            }
            switch (Kind)
            {
                case SpotKind.Locker: return "[E] Hide in the locker";
                case SpotKind.Cabinet: return "[E] Hide in the cabinet";
                default: return "[E] Hide under the table";
            }
        }

        public void Use(PlayerController player)
        {
            if (player.HiddenIn == this)
            {
                player.LeaveHidingSpot();
            }
            else if (player.HiddenIn == null)
            {
                player.EnterHidingSpot(this);
            }
        }

        /// <summary>Opens the door for a moment (entering, leaving, or Freddy checking inside).</summary>
        public void Rattle(float seconds = 0.6f)
        {
            doorTarget = 1f;
            CancelInvoke(nameof(CloseDoor));
            Invoke(nameof(CloseDoor), seconds);
        }

        private void CloseDoor()
        {
            doorTarget = 0f;
        }

        private void Update()
        {
            if (door == null)
            {
                return;
            }
            // A locker has vents; a cabinet door or a tablecloth is left open a crack to look through.
            float rest = Occupied && Kind != SpotKind.Locker ? 0.3f : 0f;
            doorOpen = Mathf.MoveTowards(doorOpen, Mathf.Max(doorTarget, rest), Time.deltaTime * 4f);
            door.localRotation = Quaternion.Euler(0f, -100f * doorOpen, 0f);
        }
    }

    /// <summary>One of the fuses the player needs to power the exit door.</summary>
    public class FusePickup : MonoBehaviour, IInteractable
    {
        public Hunt.Room Room { get; private set; }

        private Light glow;
        private Vector3 basePosition;
        private bool taken;

        public bool HoldToUse => false;

        public static FusePickup Attach(GameObject go, Hunt.Room room, Light glow)
        {
            var fuse = go.AddComponent<FusePickup>();
            fuse.Room = room;
            fuse.glow = glow;
            fuse.basePosition = go.transform.localPosition;
            return fuse;
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null)
            {
                return "";
            }
            return player.Director.InCall ? "The fuse is stuck... listen to the call first  [ENTER] skip" : "[E] Take the fuse";
        }

        public void Use(PlayerController player)
        {
            if (player.Director.InCall)
            {
                return;
            }
            if (taken)
            {
                return;
            }
            taken = true;
            player.Director.OnFuseTaken(this);
            StartCoroutine(FlyToHand(player));
        }

        /// <summary>The cell is taken: it comes off the shelf into the player's hand, then is put away.</summary>
        private System.Collections.IEnumerator FlyToHand(PlayerController player)
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.enabled = false;
            }
            Vector3 start = transform.position;
            Quaternion startRotation = transform.rotation;
            Vector3 startScale = transform.localScale;
            const float seconds = 0.45f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float ease = k * k * (3f - 2f * k);
                Transform view = player.View.transform;
                Vector3 hand = view.TransformPoint(new Vector3(0.2f, -0.28f, 0.42f));
                // A small arc up, then down into the hand.
                transform.position = Vector3.Lerp(start, hand, ease) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.12f;
                transform.rotation = Quaternion.Slerp(startRotation, view.rotation * Quaternion.Euler(-70f, 0f, 20f), ease);
                transform.localScale = startScale * Mathf.Lerp(1f, 0.55f, ease);
                if (glow != null)
                {
                    glow.intensity = Mathf.Lerp(1f, 0f, ease);
                }
                yield return null;
            }
            transform.position = start;
            transform.rotation = startRotation;
            transform.localScale = startScale;
            if (trigger != null)
            {
                trigger.enabled = true;
            }
            taken = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (taken)
            {
                return;
            }
            // Slow bob and pulse so the fuse can be found in the dark.
            float t = Time.time;
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(t * 2f) * 0.03f);
            transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            if (glow != null)
            {
                glow.intensity = 0.6f + Mathf.Sin(t * 3f) * 0.25f;
            }
        }
    }

    /// <summary>
    /// The power panel next to an exit. Holding E with every fuse: the panel door swings open, the
    /// three power cells go one by one from the player's hand into their sockets (each lights up
    /// with a spark when it clicks in), then the big lever comes down and the door opens. Loud.
    /// Everything is drawn from the saved progress, so walking away keeps what was done: a cell
    /// half pushed in stays sticking out of its socket, and on coming back the player jiggles it
    /// and carries on. Only tonight's exit works; the other door stays chained.
    /// </summary>
    public class ExitPanel : MonoBehaviour, IInteractable
    {
        public const float WorkSeconds = 6f;

        /// <summary>The moving parts, made by LevelBuilder.</summary>
        public class Parts
        {
            public GameObject Root;
            public Transform Door;
            public Transform[] Cells;
            public Vector3[] SocketPositions;
            public Renderer[] SocketLights;
            public Transform Lever;
            public Renderer Lamp;
            public ParticleSystem Sparks;
        }

        // Shares of the progress: open the door, then three cells, then the lever.
        private const float DoorShare = 0.06f;
        private const float CellShare = 0.23f;
        private const float LeverStart = DoorShare + 3 * CellShare;

        private Transform[] doors;   // the two door leaves' hinges
        private Parts parts;
        private float doorOpen;
        private float lastUsed = -10f;
        private float resumeKick;
        private float leverShown;
        private readonly bool[] seated = new bool[3];
        private PlayerController user;

        public Room Room { get; private set; }

        /// <summary>True on the night this is the way out.</summary>
        public bool Active { get; set; }

        public float Progress { get; private set; }
        public bool Open => Progress >= 1f;

        public bool HoldToUse => true;

        private bool Held => Time.time - lastUsed < 0.15f;

        public void ResetPanel()
        {
            Progress = 0f;
            leverShown = 0f;
            for (int i = 0; i < seated.Length; i++)
            {
                seated[i] = false;
            }
            SetLift(0f);
            Draw();
        }

        /// <summary>Swings the doors (0 shut, 1 open outward), also used by the intro.</summary>
        public void SetLift(float open)
        {
            doorOpen = open;
            if (doors == null)
            {
                return;
            }
            for (int i = 0; i < doors.Length; i++)
            {
                doors[i].localRotation = Quaternion.Euler(0f, (i == 0 ? 82f : -82f) * open, 0f);
            }
        }

        public static ExitPanel Attach(Parts parts, Room room, Transform[] doors)
        {
            var panel = parts.Root.AddComponent<ExitPanel>();
            panel.parts = parts;
            panel.Room = room;
            panel.doors = doors;
            panel.Draw();
            return panel;
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || Open)
            {
                return "";
            }
            if (!Active)
            {
                return "Chained shut tonight. The way out is " + HuntDirector.Describe(ObjectiveStep.Exit).Replace("open ", "");
            }
            if (!player.Director.ObjectiveList.ExitUnlocked)
            {
                return "The exit is locked. Still to do: " + HuntDirector.Describe(player.Director.ObjectiveList.Current);
            }
            string verb = Progress <= 0f ? "Put the fuses in" : Progress < LeverStart ? "Keep putting the fuses in" : "Pull the lever";
            return "[Hold E] " + verb + "  " + Mathf.RoundToInt(Progress * 100f) + "%";
        }

        public void Use(PlayerController player)
        {
            if (Open || !Active || !player.Director.ObjectiveList.ExitUnlocked)
            {
                return;
            }
            if (!Held)
            {
                // Coming back to it (or starting): settle in front of the panel, jiggle what is there.
                resumeKick = 1f;
            }
            user = player;
            lastUsed = Time.time;
            player.LookToward(parts.Root.transform.position, 4f);
            Progress = Mathf.Min(1f, Progress + Time.deltaTime / WorkSeconds);
            player.MakeNoise(3);
            if (Open)
            {
                player.Director.OnExitOpened();
            }
        }

        private void Update()
        {
            resumeKick = Mathf.MoveTowards(resumeKick, 0f, Time.deltaTime * 2.5f);
            // Let go of the lever and it springs back a little; it never loses what was pulled.
            float leverTarget = Mathf.Clamp01((Progress - LeverStart) / (1f - LeverStart));
            leverShown = Mathf.MoveTowards(leverShown, Held || Open ? leverTarget : leverTarget * 0.85f, Time.deltaTime * 3f);
            Draw();
            if (Open && doorOpen < 1f)
            {
                SetLift(Mathf.MoveTowards(doorOpen, 1f, Time.deltaTime * 0.6f));
            }
        }

        /// <summary>Puts every moving part where the progress says.</summary>
        private void Draw()
        {
            if (parts == null)
            {
                return;
            }
            float door = Mathf.Clamp01(Progress / DoorShare);
            parts.Door.localRotation = Quaternion.Euler(0f, -115f * Ease(door), 0f);

            for (int i = 0; i < 3; i++)
            {
                float t = (Progress - DoorShare - i * CellShare) / CellShare;
                Transform cell = parts.Cells[i];
                Vector3 socket = parts.SocketPositions[i];
                // Lying in the socket: the cell's long axis goes into the panel.
                Quaternion inSocket = Quaternion.Euler(-90f, 0f, 0f);
                if (t <= 0f)
                {
                    cell.gameObject.SetActive(false);
                    SetLight(i, new Color(0.7f, 0f, 0f));
                    continue;
                }
                cell.gameObject.SetActive(true);
                Vector3 mouth = socket + Vector3.forward * 0.16f;
                if (t < 0.25f && Held && user != null)
                {
                    // From the player's hand (low right of the view) to the socket's mouth.
                    float k = Ease(t / 0.25f);
                    Transform view = user.View.transform;
                    Vector3 hand = transform.InverseTransformPoint(view.TransformPoint(new Vector3(0.22f, -0.25f, 0.45f)));
                    Quaternion handRotation = Quaternion.Inverse(transform.rotation) * view.rotation * Quaternion.Euler(-60f, 0f, 0f);
                    cell.localPosition = Vector3.Lerp(hand, mouth, k);
                    cell.localRotation = Quaternion.Slerp(handRotation, inSocket, k);
                }
                else
                {
                    // Pushed in against the socket's resistance; left half-way, it sticks out.
                    float push = Mathf.Clamp01((t - 0.25f) / 0.75f);
                    float jiggle = Mathf.Sin(Time.time * 40f) * 0.004f * resumeKick;
                    cell.localPosition = Vector3.Lerp(mouth, socket, Ease(push)) + new Vector3(jiggle, jiggle * 0.5f, 0f);
                    cell.localRotation = inSocket * Quaternion.Euler(0f, push < 1f ? Mathf.Sin(push * 25f) * 4f * (1f - push) : 0f, 0f);
                }
                bool inPlace = t >= 1f;
                if (inPlace && !seated[i])
                {
                    seated[i] = true;
                    parts.Sparks.transform.localPosition = socket;
                    parts.Sparks.Emit(25);
                    if (user != null)
                    {
                        user.Director.Audio.PlayAt(user.Director.Assets.LockerDoor, transform.position, 0.5f, 1.7f);
                    }
                }
                bool blinking = !inPlace && Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
                SetLight(i, inPlace ? new Color(0.1f, 1f, 0.2f) : blinking ? new Color(1f, 0.6f, 0f) : new Color(0.3f, 0.15f, 0f));
            }

            parts.Lever.localRotation = Quaternion.Euler(Mathf.Lerp(-35f, 80f, Ease(leverShown)), 0f, 0f);
            Color lamp = !Active ? new Color(0.05f, 0.05f, 0.05f) : Open ? Color.green : Progress > 0f ? Color.Lerp(Color.red, Color.yellow, Progress) : new Color(0.6f, 0f, 0f);
            parts.Lamp.material.SetColor("_EmissionColor", lamp * 2f);
        }

        private void SetLight(int index, Color color)
        {
            parts.SocketLights[index].material.SetColor("_EmissionColor", (Active ? color : new Color(0.05f, 0.05f, 0.05f)) * 2.5f);
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// Ceiling lamp that flickers now and then, flickers hard when Freddy walks under it, and can
    /// be blacked out (the jumpscare). The fixture glow and the light shaft follow the light.
    /// </summary>
    public class FlickerLight : MonoBehaviour
    {
        private static readonly System.Collections.Generic.List<FlickerLight> All = new System.Collections.Generic.List<FlickerLight>();
        private static Vector3 freddyPosition = new Vector3(9999f, 0f, 9999f);
        private static float blackoutUntil;

        private Light lamp;
        private Renderer fixture;
        private GameObject shaft;
        private float baseIntensity;
        private float nextFlicker;
        private float flickerUntil;
        private float chance;
        private bool lastOn = true;

        public static FlickerLight Attach(Light lamp, Renderer fixture, float chance, GameObject shaft)
        {
            var flicker = lamp.gameObject.AddComponent<FlickerLight>();
            flicker.lamp = lamp;
            flicker.fixture = fixture;
            flicker.shaft = shaft;
            flicker.baseIntensity = lamp.intensity;
            flicker.chance = chance;
            flicker.nextFlicker = Random.Range(2f, 10f);
            if (lamp.intensity > 0f)
            {
                // Every working tube hums; the hum goes with the light.
                flicker.hum = lamp.gameObject.AddComponent<AudioSource>();
                flicker.hum.clip = GameAudio.LampHum();
                flicker.hum.loop = true;
                flicker.hum.spatialBlend = 1f;
                flicker.hum.rolloffMode = AudioRolloffMode.Linear;
                flicker.hum.minDistance = 0.8f;
                flicker.hum.maxDistance = 6f;
                flicker.hum.volume = HumVolume;
                flicker.hum.pitch = Random.Range(0.97f, 1.03f);
                flicker.hum.time = Random.value * 0.9f;
                flicker.hum.Play();
            }
            if (fixture != null && lamp.intensity > 0f)
            {
                fixture.material = new Material(fixture.sharedMaterial);
            }
            All.Add(flicker);
            return flicker;
        }

        /// <summary>Where Freddy is; lamps near him misbehave.</summary>
        public static void SetFreddyPosition(Vector3 position)
        {
            freddyPosition = position;
        }

        /// <summary>
        /// Kills a fraction of the working lamps for the night (bulbs dying on the later nights);
        /// the same seed always kills the same lamps. 0 brings every lamp back.
        /// </summary>
        public static void KillLights(float fraction, int seed)
        {
            var random = new System.Random(seed);
            foreach (FlickerLight light in All)
            {
                light.dead = light.baseIntensity > 0f && random.NextDouble() < fraction;
                light.lastOn = !light.dead;
                light.lamp.intensity = light.dead ? 0f : light.baseIntensity;
                if (light.hum != null)
                {
                    light.hum.volume = light.dead ? 0f : HumVolume;
                }
                if (light.shaft != null)
                {
                    light.shaft.SetActive(!light.dead);
                }
                if (light.fixture != null && light.baseIntensity > 0f)
                {
                    light.fixture.material.SetColor("_EmissionColor", light.dead ? Color.black : light.lamp.color * 2.2f);
                }
            }
        }

        private bool dead;
        private AudioSource hum;
        private const float HumVolume = 0.06f;

        /// <summary>Every lamp goes dark for a moment.</summary>
        public static void Blackout(float seconds)
        {
            blackoutUntil = Time.time + seconds;
        }

        private void OnDestroy()
        {
            All.Remove(this);
        }

        private void Update()
        {
            if (baseIntensity <= 0f || dead)
            {
                return;
            }
            float t = Time.time;
            float distance = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(freddyPosition.x, 0f, freddyPosition.z));
            bool freddyNear = distance < 5f;

            if (t >= nextFlicker)
            {
                nextFlicker = t + (freddyNear ? Random.Range(0.2f, 0.8f) : Random.Range(3f, 12f));
                if (freddyNear || Random.value < chance)
                {
                    flickerUntil = t + Random.Range(0.15f, freddyNear ? 0.6f : 1.2f);
                }
            }

            bool on = t >= blackoutUntil && (t >= flickerUntil || Random.value < 0.5f);
            lamp.intensity = on ? baseIntensity : baseIntensity * 0.05f;
            if (hum != null)
            {
                // A flickering tube crackles: louder and rougher while it struggles.
                bool struggling = t < flickerUntil;
                hum.volume = !on ? HumVolume * 0.1f : struggling ? HumVolume * 2.2f : HumVolume;
                hum.pitch = struggling ? Random.Range(0.85f, 1.2f) : 1f;
            }
            if (on != lastOn)
            {
                lastOn = on;
                if (fixture != null)
                {
                    fixture.material.SetColor("_EmissionColor", on ? lamp.color * 2.2f : Color.black);
                }
                if (shaft != null)
                {
                    shaft.SetActive(on);
                }
            }
        }
    }
}
