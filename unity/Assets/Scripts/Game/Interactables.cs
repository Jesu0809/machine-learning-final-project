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
            player.Director.OnFuseTaken(this);
            gameObject.SetActive(false);
        }

        private void Update()
        {
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
    /// The panel next to an exit: hold E with every fuse to open the door. Loud. Only tonight's
    /// exit works; the other door stays chained.
    /// </summary>
    public class ExitPanel : MonoBehaviour, IInteractable
    {
        public const float WorkSeconds = 6f;

        private Transform[] doors;   // the two door leaves' hinges
        private Renderer lamp;
        private float doorOpen;

        public Room Room { get; private set; }

        /// <summary>True on the night this is the way out.</summary>
        public bool Active { get; set; }

        public float Progress { get; private set; }
        public bool Open => Progress >= 1f;

        public bool HoldToUse => true;

        public void ResetPanel()
        {
            Progress = 0f;
            SetLift(0f);
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

        public static ExitPanel Attach(GameObject go, Room room, Transform[] doors, Renderer lamp)
        {
            var panel = go.AddComponent<ExitPanel>();
            panel.Room = room;
            panel.doors = doors;
            panel.lamp = lamp;
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
            return "[Hold E] Restore power and open the door  " + Mathf.RoundToInt(Progress * 100f) + "%";
        }

        public void Use(PlayerController player)
        {
            if (Open || !Active || !player.Director.ObjectiveList.ExitUnlocked)
            {
                return;
            }
            Progress = Mathf.Min(1f, Progress + Time.deltaTime / WorkSeconds);
            player.MakeNoise(3);
            if (Open)
            {
                player.Director.OnExitOpened();
            }
        }

        private void Update()
        {
            if (lamp != null)
            {
                bool ready = Progress > 0f;
                Color color = !Active ? new Color(0.05f, 0.05f, 0.05f) : Open ? Color.green : ready ? Color.Lerp(Color.red, Color.yellow, Progress) : new Color(0.6f, 0f, 0f);
                lamp.material.SetColor("_EmissionColor", color * 2f);
            }
            if (Open && doorOpen < 1f)
            {
                SetLift(Mathf.MoveTowards(doorOpen, 1f, Time.deltaTime * 0.6f));
            }
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
                if (light.fixture != null && light.baseIntensity > 0f)
                {
                    light.fixture.material.SetColor("_EmissionColor", light.dead ? Color.black : light.lamp.color * 2.2f);
                }
            }
        }

        private bool dead;

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
