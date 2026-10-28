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
        private float doorSpeed = 4f;

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

        /// <summary>
        /// Opens the door for a moment (entering, leaving, or Freddy checking inside), then swings
        /// it shut at <paramref name="closeSpeed"/> (doors per second; pulled shut from inside is slow).
        /// </summary>
        public void Rattle(float seconds = 0.6f, float closeSpeed = 4f)
        {
            doorTarget = 1f;
            doorSpeed = 4f;
            closingSpeed = closeSpeed;
            CancelInvoke(nameof(CloseDoor));
            Invoke(nameof(CloseDoor), seconds);
        }

        private float closingSpeed = 4f;

        private void CloseDoor()
        {
            doorTarget = 0f;
            doorSpeed = closingSpeed;
        }

        private void Update()
        {
            if (door == null)
            {
                return;
            }
            // A locker has vents; a cabinet door or a tablecloth is left open a crack to look through.
            float rest = Occupied && Kind != SpotKind.Locker ? 0.3f : 0f;
            doorOpen = Mathf.MoveTowards(doorOpen, Mathf.Max(doorTarget, rest), Time.deltaTime * doorSpeed);
            door.localRotation = Quaternion.Euler(0f, -100f * doorOpen, 0f);
        }
    }

    /// <summary>
    /// The panel beside an exit door: three lamps that light up as the night's power sockets are
    /// filled, a status lamp, and a big lever. With every objective done the player presses E: the
    /// panel door swings open, the lever is hauled down against its spring, sparks fly and the exit
    /// opens. Very loud. Only tonight's exit works; the other door stays chained.
    /// </summary>
    public class ExitPanel : MonoBehaviour, IInteractable
    {
        /// <summary>The moving parts, made by LevelBuilder.</summary>
        public class Parts
        {
            public GameObject Root;
            public Transform Door;
            public Renderer[] SocketLights;
            public Transform Lever;
            /// <summary>The lever's angle about its hub (degrees, about x) before and after the pull.</summary>
            public float LeverUp = -35f;
            public float LeverDown = 80f;
            public Renderer Lamp;
            public ParticleSystem Sparks;
        }

        private Transform[] doors;   // the two door leaves' hinges
        private Parts parts;
        private float doorOpen;
        private float panelDoor;
        private float lever;
        private bool pulling;
        private HuntDirector director;

        public Room Room { get; private set; }

        /// <summary>True on the night this is the way out.</summary>
        public bool Active { get; set; }

        /// <summary>0 before the lever, 1 once it is down and the doors open.</summary>
        public float Progress { get; private set; }
        public bool Open => Progress >= 1f;

        public bool HoldToUse => false;

        public void ResetPanel()
        {
            StopAllCoroutines();
            pulling = false;
            Progress = 0f;
            panelDoor = 0f;
            lever = 0f;
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

        private HuntDirector Director => director != null ? director : director = FindAnyObjectByType<HuntDirector>();

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || Open || pulling)
            {
                return "";
            }
            if (!Active)
            {
                return "Chained shut tonight. The way out is " + HuntDirector.Describe(ObjectiveStep.Exit).Replace("pull the lever at ", "");
            }
            if (!Director.State.ExitUnlocked)
            {
                return "No power to the exit yet. Still to do: " + Director.ObjectiveList.Missing();
            }
            return "[E] Pull the lever (LOUD)";
        }

        public void Use(PlayerController player)
        {
            if (Open || pulling || !Active || !Director.State.ExitUnlocked)
            {
                return;
            }
            StartCoroutine(Pull(player));
        }

        /// <summary>The panel door swings open, then the lever is hauled down against its spring.</summary>
        private System.Collections.IEnumerator Pull(PlayerController player)
        {
            pulling = true;
            player.MakeNoise(3);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                panelDoor = t / 0.35f;
                player.LookToward(transform.position, 6f);
                Draw();
                yield return null;
            }
            panelDoor = 1f;
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                // Stiff at first, then it gives.
                float k = t / 0.8f;
                lever = k < 0.4f ? k * 0.5f : 0.2f + (k - 0.4f) / 0.6f * 0.8f;
                player.LookToward(parts.Lever.position, 6f);
                player.MakeNoise(3);
                Draw();
                yield return null;
            }
            lever = 1f;
            Progress = 1f;
            pulling = false;
            parts.Sparks.transform.position = parts.Lever.position + parts.Root.transform.TransformDirection(new Vector3(0f, 0.05f, 0.08f));
            parts.Sparks.Emit(40);
            player.Jolt(1.8f);
            Director.Audio.PlayAt(GameAudio.Clunk(), transform.position, 1f, 0.8f);
            FlickerLight.Surge(transform.position, 12f, 1.6f);
            Draw();
            Director.OnExitOpened();
        }

        private void Update()
        {
            Draw();
            if (Open && doorOpen < 1f)
            {
                SetLift(Mathf.MoveTowards(doorOpen, 1f, Time.deltaTime * 0.6f));
            }
        }

        /// <summary>Puts every moving part where the night says.</summary>
        private void Draw()
        {
            if (parts == null)
            {
                return;
            }
            parts.Door.localRotation = Quaternion.Euler(0f, -115f * Ease(panelDoor), 0f);
            parts.Lever.localRotation = Quaternion.Euler(Mathf.Lerp(parts.LeverUp, parts.LeverDown, Ease(lever)), 0f, 0f);
            HuntState state = Director != null ? Director.State : null;
            for (int i = 0; i < parts.SocketLights.Length; i++)
            {
                bool powered = state != null && state.SocketPowered(i);
                Color off = new Color(0.6f, 0f, 0f);
                Color color = !Active ? new Color(0.05f, 0.05f, 0.05f) : powered ? (state.Night >= 2 ? CellColors.Of(i) : new Color(0.1f, 1f, 0.2f)) : off;
                // Not too bright and saturated (each channel squared), or the bloom turns every color white.
                parts.SocketLights[i].material.SetColor("_EmissionColor", new Color(color.r * color.r, color.g * color.g, color.b * color.b) * 0.8f);
            }
            bool ready = state != null && state.ExitUnlocked;
            bool blink = Mathf.Repeat(Time.time * 2f, 1f) < 0.5f;
            Color lamp = !Active ? new Color(0.05f, 0.05f, 0.05f) : Open ? Color.green : ready ? (blink ? new Color(1f, 0.7f, 0f) : new Color(0.3f, 0.2f, 0f)) : new Color(0.6f, 0f, 0f);
            parts.Lamp.material.SetColor("_EmissionColor", lamp * 2f);
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
        private bool lastSurging;
        private AudioSource hum;
        private const float HumVolume = 0.06f;

        private static Vector3 surgeAt;
        private static float surgeRadius;
        private static float surgeStart = -10f;
        private static float surgeSeconds = 1f;

        /// <summary>
        /// Power comes back near a point: the lamps within the radius flare up, stutter and settle
        /// (a socket powered, the breakers back on).
        /// </summary>
        public static void Surge(Vector3 point, float radius, float seconds, float delay = 0f)
        {
            surgeAt = point;
            surgeRadius = radius;
            surgeStart = Time.time + delay;
            surgeSeconds = seconds;
        }

        /// <summary>How much brighter this lamp is in a surge (1 when none).</summary>
        private float SurgeGain(float t)
        {
            float k = (t - surgeStart) / surgeSeconds;
            if (k < 0f || k > 1f)
            {
                return 1f;
            }
            Vector3 offset = transform.position - surgeAt;
            offset.y = 0f;
            if (offset.magnitude > surgeRadius)
            {
                return 1f;
            }
            // A flash, a few stutters, then back to normal; the nearest lamps first.
            float delay = offset.magnitude / surgeRadius * 0.15f;
            k = Mathf.Clamp01(k - delay);
            float flare = Mathf.Exp(-k * 4f) * 2.2f;
            float stutter = k > 0.15f && k < 0.45f && Mathf.Repeat(t * 23f + offset.x, 1f) < 0.35f ? -0.8f : 0f;
            return Mathf.Max(0.1f, 1f + flare + stutter);
        }

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
            float gain = SurgeGain(t);
            lamp.intensity = on ? baseIntensity * gain : baseIntensity * 0.05f;
            if (hum != null)
            {
                // A flickering tube crackles: louder and rougher while it struggles.
                bool struggling = t < flickerUntil;
                hum.volume = !on ? HumVolume * 0.1f : struggling ? HumVolume * 2.2f : HumVolume;
                hum.pitch = struggling ? Random.Range(0.85f, 1.2f) : 1f;
            }
            if (gain != 1f && on && fixture != null)
            {
                fixture.material.SetColor("_EmissionColor", lamp.color * 2.2f * gain);
                lastSurging = true;
            }
            else if (lastSurging && on && fixture != null)
            {
                fixture.material.SetColor("_EmissionColor", lamp.color * 2.2f);
                lastSurging = false;
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
