using System.Collections;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Colors of the power cells and their sockets. From night 2 each cell fits only the socket of
    /// its color (index = socket in <see cref="HuntMap.SocketRooms"/>); on night 1 they are all
    /// yellow and fit any socket (index -1).
    /// </summary>
    public static class CellColors
    {
        private static readonly Color[] Colors = { new Color(1f, 0.74f, 0.12f), new Color(0.2f, 0.55f, 1f), new Color(0.25f, 1f, 0.4f) };
        private static readonly string[] Names = { "YELLOW", "BLUE", "GREEN" };

        public static Color Of(int socket) => Colors[socket < 0 ? 0 : socket];

        public static string Name(int socket) => Names[socket < 0 ? 0 : socket];

        /// <summary>The glowing core of a cell of this color.</summary>
        public static Material Core(int socket) => BuildKit.Glow("cellCore" + Name(socket), Of(socket), 6f);

        /// <summary>The halo under a waiting cell.</summary>
        public static Material Halo(int socket) => BuildKit.Additive("cellHalo" + Name(socket), ProceduralTextures.Ring(128, 0.12f), Of(socket) * 0.9f);

        /// <summary>Paints a power cell model (made by LevelBuilder.PowerCell) in a color.</summary>
        public static void Paint(GameObject cell, int socket)
        {
            Transform core = cell.transform.Find("Core");
            if (core != null)
            {
                core.GetComponent<Renderer>().sharedMaterial = Core(socket);
            }
        }
    }

    /// <summary>
    /// A power cell waiting on a wall shelf. Each night the director tells it which cell it is
    /// (its color); taking it puts it in the player's hand, where it stays until it goes into a
    /// socket. Only one cell can be carried at a time.
    /// </summary>
    public class CellPickup : MonoBehaviour, IInteractable
    {
        public Room Room { get; private set; }

        /// <summary>The cell it is tonight (index in the state), and the socket it fits (-1: any).</summary>
        public int Cell { get; private set; } = -1;
        public int Socket { get; private set; } = -1;

        private Light glow;
        private Renderer halo;
        private Vector3 basePosition;
        private bool taken;

        public bool HoldToUse => false;

        public static CellPickup Attach(GameObject go, Room room, Light glow, Renderer halo)
        {
            var cell = go.AddComponent<CellPickup>();
            cell.Room = room;
            cell.glow = glow;
            cell.halo = halo;
            cell.basePosition = go.transform.localPosition;
            return cell;
        }

        /// <summary>Tonight this shelf holds cell <paramref name="cell"/>, which fits socket <paramref name="socket"/>.</summary>
        public void SetCell(int cell, int socket)
        {
            Cell = cell;
            Socket = socket;
            CellColors.Paint(gameObject, socket);
            if (glow != null)
            {
                glow.color = CellColors.Of(socket);
            }
            if (halo != null)
            {
                halo.sharedMaterial = CellColors.Halo(socket);
            }
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || taken)
            {
                return "";
            }
            if (player.Director.InCall)
            {
                return "The cell is stuck... listen to the call first  [ENTER] skip";
            }
            if (player.Cell.Carrying)
            {
                return "Your hands are full: take the " + CellColors.Name(player.Cell.Socket) + " cell to its socket first";
            }
            return "[E] Take the " + CellColors.Name(Socket) + " power cell";
        }

        public void Use(PlayerController player)
        {
            if (player.Director.InCall || taken || player.Cell.Carrying)
            {
                return;
            }
            taken = true;
            player.Director.OnCellTaken(this);
            StartCoroutine(FlyToHand(player));
        }

        /// <summary>The cell comes off the shelf into the player's hand, where the held one takes over.</summary>
        private IEnumerator FlyToHand(PlayerController player)
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
                Vector3 hand = view.TransformPoint(HeldCell.HandPosition);
                transform.position = Vector3.Lerp(start, hand, ease) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.12f;
                transform.rotation = Quaternion.Slerp(startRotation, view.rotation * HeldCell.HandRotation, ease);
                transform.localScale = startScale * Mathf.Lerp(1f, HeldCell.HandScale, ease);
                if (glow != null)
                {
                    glow.intensity = Mathf.Lerp(1.6f, 0f, ease);
                }
                yield return null;
            }
            player.Cell.Hold(Cell, Socket);
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
            // Slow bob and pulse so the cell can be found in the dark.
            float t = Time.time;
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(t * 2f) * 0.03f);
            transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            if (glow != null)
            {
                glow.intensity = 1.2f + Mathf.Sin(t * 3f) * 0.4f;
            }
        }
    }

    /// <summary>
    /// The cell in the player's hand: low on the right of the view, swaying with the walk, its
    /// core lighting the hand. Hidden in hiding spots and ducts (it is held close to the chest).
    /// </summary>
    public class HeldCell : MonoBehaviour
    {
        public static readonly Vector3 HandPosition = new Vector3(0.24f, -0.3f, 0.48f);
        public static readonly Quaternion HandRotation = Quaternion.Euler(-12f, -20f, 8f);
        public const float HandScale = 0.55f;

        private PlayerController player;
        private GameObject model;
        private Light glow;

        public int CellIndex { get; private set; } = -1;
        public int Socket { get; private set; } = -1;
        public bool Carrying => CellIndex >= 0;

        public static HeldCell Create(PlayerController player)
        {
            var held = player.View.gameObject.AddComponent<HeldCell>();
            held.player = player;
            held.model = LevelBuilder.PowerCell(player.View.transform);
            held.model.name = "HeldCell";
            held.model.transform.localPosition = HandPosition;
            held.model.transform.localRotation = HandRotation;
            held.model.transform.localScale = Vector3.one * HandScale;
            foreach (Collider collider in held.model.GetComponentsInChildren<Collider>())
            {
                Destroy(collider);
            }
            held.glow = new GameObject("HeldGlow").AddComponent<Light>();
            held.glow.transform.SetParent(held.model.transform, false);
            held.glow.type = LightType.Point;
            held.glow.range = 1.6f;
            held.glow.intensity = 0.7f;
            held.glow.shadows = LightShadows.None;
            held.model.SetActive(false);
            return held;
        }

        public void Hold(int cell, int socket)
        {
            CellIndex = cell;
            Socket = socket;
            CellColors.Paint(model, socket);
            glow.color = CellColors.Of(socket);
            model.SetActive(true);
        }

        /// <summary>Lets go of the cell (it went into a socket, or the night ended); returns where it was.</summary>
        public Pose Release()
        {
            var pose = new Pose(model.transform.position, model.transform.rotation);
            CellIndex = -1;
            Socket = -1;
            model.SetActive(false);
            return pose;
        }

        private void LateUpdate()
        {
            if (!Carrying)
            {
                return;
            }
            bool tucked = player.HiddenIn != null || player.Crawling || player.InFocus;
            model.SetActive(!tucked);
            // A little sway with the steps, heavier than the flashlight.
            float bob = player.Moving ? Mathf.Sin(Time.time * (player.Running ? 11f : 7f)) : 0f;
            model.transform.localPosition = HandPosition + new Vector3(bob * 0.008f, Mathf.Abs(bob) * -0.012f, 0f);
            glow.intensity = 0.6f + Mathf.Sin(Time.time * 3f) * 0.15f;
        }
    }

    /// <summary>
    /// A power socket on a wall: a steel housing with a round mouth ringed in its color (amber on
    /// night 1, when any cell fits), conduits up to the ceiling and a status lamp. Bringing the
    /// right cell and pressing E pushes it in: it slides from the hand into the mouth, turns, locks
    /// with a clunk and sparks, and the ring and lamp light up. Loud: Freddy hears it.
    /// </summary>
    public class PowerSocket : MonoBehaviour, IInteractable
    {
        private HuntDirector director;
        private Renderer ring;
        private Renderer lamp;
        private Light ringLight;
        private GameObject seated;
        private ParticleSystem sparks;
        private bool busy;

        public int Index { get; private set; }
        public bool Powered { get; private set; }
        public bool HoldToUse => false;

        public static PowerSocket Build(HuntDirector director, Transform parent, int index)
        {
            Vector3 position = PizzeriaLayout.SocketPosition(index);
            var go = new GameObject("PowerSocket " + CellColors.Name(index));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = PizzeriaLayout.SocketFacing(index);
            Transform t = go.transform;
            var socket = go.AddComponent<PowerSocket>();
            socket.director = director;
            socket.Index = index;

            Material steel = BuildKit.Pbr("metal_worn", new Color(0.5f, 0.53f, 0.55f)) ?? BuildKit.Plain("socketSteel", new Color(0.3f, 0.32f, 0.33f), 0.45f, 0.7f);
            Material dark = BuildKit.Plain("socketDark", new Color(0.03f, 0.03f, 0.035f), 0.4f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Material hazard = BuildKit.Pbr("metal_hazard", Color.white) ?? BuildKit.Plain("hazardYellow", new Color(0.7f, 0.6f, 0.1f), 0.4f);

            // Back plate on the wall, the housing, a hazard band along the bottom.
            BuildKit.Box("Plate", t, new Vector3(0f, -0.05f, 0.02f), new Vector3(0.62f, 0.95f, 0.04f), steel);
            BuildKit.Box("Housing", t, new Vector3(0f, 0f, 0.13f), new Vector3(0.46f, 0.56f, 0.2f), steel);
            BuildKit.Box("Hazard", t, new Vector3(0f, -0.43f, 0.07f), new Vector3(0.62f, 0.1f, 0.06f), hazard, 1f, false);
            // The mouth: a chrome collar, the dark bore, the colored ring that lights up.
            BuildKit.Cylinder("Collar", t, new Vector3(0f, 0.02f, 0.235f), 0.24f, 0.03f, chrome).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            BuildKit.Cylinder("Bore", t, new Vector3(0f, 0.02f, 0.252f), 0.17f, 0.005f, dark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            GameObject ringObject = BuildKit.Cylinder("Ring", t, new Vector3(0f, 0.02f, 0.249f), 0.215f, 0.004f, BuildKit.Glow("socketRing", Color.white, 2f));
            ringObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            socket.ring = ringObject.GetComponent<Renderer>();
            socket.ring.material = new Material(socket.ring.sharedMaterial);
            GameObject lampObject = BuildKit.Box("Lamp", t, new Vector3(0f, 0.22f, 0.235f), new Vector3(0.12f, 0.04f, 0.02f), BuildKit.Glow("socketLamp", Color.red, 2f), 1f, false);
            socket.lamp = lampObject.GetComponent<Renderer>();
            socket.lamp.material = new Material(socket.lamp.sharedMaterial);
            // Conduits up to the ceiling, and a cable gland under the housing.
            foreach (float x in new[] { -0.14f, 0.14f })
            {
                BuildKit.Cylinder("Conduit", t, new Vector3(x, 1.2f, 0.06f), 0.05f, 1.9f, chrome);
            }
            BuildKit.Cylinder("Gland", t, new Vector3(0f, -0.33f, 0.13f), 0.08f, 0.1f, dark);

            // The cell once it is in: mostly inside the bore, its cap showing.
            socket.seated = LevelBuilder.PowerCell(t);
            socket.seated.transform.localPosition = new Vector3(0f, 0.02f, 0.2f);
            socket.seated.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            socket.seated.transform.localScale = Vector3.one * 0.6f;
            socket.seated.SetActive(false);

            socket.ringLight = new GameObject("RingLight").AddComponent<Light>();
            socket.ringLight.transform.SetParent(t, false);
            socket.ringLight.transform.localPosition = new Vector3(0f, 0.02f, 0.5f);
            socket.ringLight.type = LightType.Point;
            socket.ringLight.range = 2.5f;
            socket.ringLight.shadows = LightShadows.None;

            var sparkRoot = new GameObject("SocketSparks");
            sparkRoot.transform.SetParent(t, false);
            sparkRoot.transform.localPosition = new Vector3(0f, 0.02f, 0.27f);
            LevelBuilder.Sparks(sparkRoot.transform);
            socket.sparks = sparkRoot.GetComponentInChildren<ParticleSystem>();
            socket.sparks.Stop();
            return socket;
        }

        /// <summary>A new night: empty, the ring in its color (amber on night 1, when any cell fits).</summary>
        public void ResetSocket(bool colored)
        {
            StopAllCoroutines();
            busy = false;
            Powered = false;
            seated.SetActive(false);
            Color color = colored ? CellColors.Of(Index) : new Color(1f, 0.6f, 0.2f);
            ring.material.SetColor("_EmissionColor", color * 1.4f);
            ringLight.color = color;
            ringLight.intensity = 0.35f;
            lamp.material.SetColor("_EmissionColor", new Color(1f, 0.08f, 0.04f) * 2f);
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || Powered || busy)
            {
                return "";
            }
            HuntState state = director.State;
            string mine = state.Night >= 2 ? CellColors.Name(Index) + " " : "";
            if (!player.Cell.Carrying)
            {
                return "Power socket: bring a " + mine + "power cell here";
            }
            if (!state.CellFits(player.Cell.CellIndex, Index))
            {
                return "This socket only takes the " + mine + "cell";
            }
            return "[E] Put the " + CellColors.Name(player.Cell.Socket) + " cell in";
        }

        public void Use(PlayerController player)
        {
            if (Powered || busy || !player.Cell.Carrying || !director.State.CellFits(player.Cell.CellIndex, Index))
            {
                return;
            }
            StartCoroutine(Insert(player));
        }

        private IEnumerator Insert(PlayerController player)
        {
            busy = true;
            int socket = player.Cell.Socket;
            Pose hand = player.Cell.Release();
            CellColors.Paint(seated, socket);
            seated.SetActive(true);
            Transform cell = seated.transform;
            Vector3 mouth = new Vector3(0f, 0.02f, 0.42f);
            Vector3 inside = new Vector3(0f, 0.02f, 0.2f);
            Quaternion inSocket = Quaternion.Euler(-90f, 0f, 0f);
            Vector3 handLocal = transform.InverseTransformPoint(hand.position);
            Quaternion handRotation = Quaternion.Inverse(transform.rotation) * hand.rotation;
            // From the hand to the mouth, the player watching the socket.
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float k = Ease(t / 0.35f);
                cell.localPosition = Vector3.Lerp(handLocal, mouth, k);
                cell.localRotation = Quaternion.Slerp(handRotation, inSocket, k);
                player.LookToward(transform.position, 6f);
                yield return null;
            }
            // Pushed in against the contacts, a quarter turn to lock.
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                float k = Ease(t / 0.4f);
                cell.localPosition = Vector3.Lerp(mouth, inside, k);
                cell.localRotation = inSocket * Quaternion.Euler(0f, 90f * Mathf.Clamp01((k - 0.6f) / 0.4f), 0f);
                yield return null;
            }
            Powered = true;
            busy = false;
            sparks.Emit(30);
            player.MakeNoise(2);
            director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 0.6f, 1.6f);
            Color lit = CellColors.Of(socket);
            ring.material.SetColor("_EmissionColor", lit * 2f);
            ringLight.color = lit;
            ringLight.intensity = 1.4f;
            lamp.material.SetColor("_EmissionColor", new Color(0.1f, 1f, 0.2f) * 2.5f);
            director.OnCellInserted(this);
        }

        private void Update()
        {
            if (!Powered && !busy)
            {
                // A slow pulse: the socket is waiting.
                ringLight.intensity = 0.25f + Mathf.Sin(Time.time * 2f + Index) * 0.12f;
            }
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
