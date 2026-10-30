using System.Collections;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Colors of the power cells and their sockets. From night 2 each cell fits only the socket of
    /// its color (index = socket in <see cref="HuntState.SocketRooms"/>); on night 1 they are all
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
        private float settle;

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
            // It lands with its weight: the hand dips and comes back up.
            settle = 1f;
            player.Jolt(0.5f);
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
            settle = Mathf.MoveTowards(settle, 0f, Time.deltaTime * 2.5f);
            float dip = Mathf.Sin(settle * Mathf.PI) * 0.05f;
            model.transform.localPosition = HandPosition + new Vector3(bob * 0.008f, Mathf.Abs(bob) * -0.012f - dip, 0f);
            model.transform.localRotation = HandRotation * Quaternion.Euler(dip * 120f, 0f, 0f);
            // Brighter for a moment as it is picked up, then the slow pulse.
            glow.intensity = 0.6f + Mathf.Sin(Time.time * 3f) * 0.15f + settle * 1.2f;
        }
    }

    /// <summary>
    /// A power socket on a wall: a steel housing with a round mouth under a hinged hazard hatch,
    /// a gauge of light segments around it in its color (amber on night 1, when any cell fits),
    /// two clamp jaws, conduits up to the ceiling and a status lamp. Bringing the right cell and
    /// pressing E plays the whole thing out: the hatch flips up, the cell goes from the hand into
    /// the mouth, the jaws bite with a clunk that knocks the view, it ratchets a quarter turn, the
    /// gauge charges up both sides while the lamp blinks, then the lamp turns green, a pulse of
    /// light runs up the conduits and the room's lamps surge. Loud: Freddy hears it.
    /// </summary>
    public class PowerSocket : MonoBehaviour, IInteractable
    {
        private const int Segments = 14;
        private static readonly Vector3 Mouth = new Vector3(0f, 0.02f, 0f);

        private HuntDirector director;
        private Transform hatch;
        private readonly Transform[] jaws = new Transform[2];
        private readonly Renderer[] segments = new Renderer[Segments];
        private readonly Transform[] pulses = new Transform[2];
        private Light pulseLight;
        private Renderer lamp;
        private Light ringLight;
        private GameObject seated;
        private ParticleSystem sparks;
        private Color color;
        private bool busy;

        /// <summary>The room whose wall it is on.</summary>
        public Room Room { get; private set; }

        /// <summary>Its place among tonight's live sockets (its color), or -1 if it is dead tonight.</summary>
        public int Index => director.State.SocketIn(Room);

        public bool Live => Index >= 0;
        public bool Powered { get; private set; }
        public bool HoldToUse => false;

        public static PowerSocket Build(HuntDirector director, Transform parent, Room room)
        {
            Vector3 position = PizzeriaLayout.SocketPosition(room);
            var go = new GameObject("PowerSocket " + HuntMap.DisplayName(room));
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = PizzeriaLayout.SocketFacing(room);
            Transform t = go.transform;
            var socket = go.AddComponent<PowerSocket>();
            socket.director = director;
            socket.Room = room;

            Material steel = BuildKit.Pbr("metal_worn", new Color(0.5f, 0.53f, 0.55f)) ?? BuildKit.Plain("socketSteel", new Color(0.3f, 0.32f, 0.33f), 0.45f, 0.7f);
            Material dark = BuildKit.Plain("socketDark", new Color(0.03f, 0.03f, 0.035f), 0.4f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Material hazard = BuildKit.Pbr("metal_hazard", Color.white) ?? BuildKit.Plain("hazardYellow", new Color(0.7f, 0.6f, 0.1f), 0.4f);
            Material stripes = BuildKit.Picture("cellHazard", ProceduralTextures.HazardStripes(), 0f);

            // Back plate on the wall, the housing, a hazard band along the bottom.
            BuildKit.Box("Plate", t, new Vector3(0f, -0.05f, 0.02f), new Vector3(0.62f, 0.95f, 0.04f), steel);
            BuildKit.Box("Hazard", t, new Vector3(0f, -0.43f, 0.07f), new Vector3(0.62f, 0.1f, 0.06f), hazard, 1f, false);
            // The housing is a hollow box: four walls, and a front plate with a round hole in it
            // (drawn, with the hole cut out in its alpha), so the cell really goes in.
            BuildKit.Box("HousingTop", t, new Vector3(0f, 0.27f, 0.13f), new Vector3(0.46f, 0.02f, 0.2f), steel);
            BuildKit.Box("HousingBottom", t, new Vector3(0f, -0.27f, 0.13f), new Vector3(0.46f, 0.02f, 0.2f), steel);
            BuildKit.Box("HousingLeft", t, new Vector3(-0.22f, 0f, 0.13f), new Vector3(0.02f, 0.56f, 0.2f), steel);
            BuildKit.Box("HousingRight", t, new Vector3(0.22f, 0f, 0.13f), new Vector3(0.02f, 0.56f, 0.2f), steel);
            Texture2D faceTexture = BuildKit.SignTexture("decal_socket_face");
            Material face = faceTexture != null ? BuildKit.Decal("socketFace", faceTexture, Color.white) : steel;
            BuildKit.Quad("Face", t, new Vector3(0f, 0f, 0.231f), new Vector2(0.46f, 0.56f), 0f, face);
            // Behind the hole, a steel tube (twenty staves, so its inside shows) and a dark floor
            // with the two copper contacts the cell is pushed against.
            Material bore = BuildKit.Plain("boreSteel", new Color(0.09f, 0.09f, 0.1f), 0.55f, 0.6f);
            const float boreRadius = 0.167f;
            for (int i = 0; i < 20; i++)
            {
                float a = i * 18f;
                Vector3 at = Quaternion.Euler(0f, 0f, a) * new Vector3(0f, boreRadius + 0.005f, 0f);
                BuildKit.Box("BoreWall", t, new Vector3(at.x, 0.02f + at.y, 0.13f), new Vector3(0.056f, 0.01f, 0.2f), bore, 1f, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, a);
            }
            BuildKit.Cylinder("BoreFloor", t, new Vector3(0f, 0.02f, 0.035f), 0.35f, 0.01f, dark).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Material copper = BuildKit.Plain("copper", new Color(0.72f, 0.42f, 0.25f), 0.7f, 1f);
            BuildKit.Box("ContactPlus", t, new Vector3(0.035f, 0.02f, 0.045f), new Vector3(0.03f, 0.03f, 0.01f), copper, 1f, false);
            BuildKit.Box("ContactMinus", t, new Vector3(-0.035f, 0.02f, 0.045f), new Vector3(0.03f, 0.03f, 0.01f), copper, 1f, false);
            var reach = go.AddComponent<BoxCollider>();
            reach.center = new Vector3(0f, 0f, 0.13f);
            reach.size = new Vector3(0.46f, 0.56f, 0.2f);

            // The gauge: two arcs of segments, up the left and the right of the mouth, filled from
            // the bottom (the jaws sit at the sides, between the arcs).
            Material segment = BuildKit.Glow("socketSegment", Color.white, 1f);
            for (int i = 0; i < Segments; i++)
            {
                int step = i % (Segments / 2);
                float side = i < Segments / 2 ? 1f : -1f;
                float angle = (-60f + step * 20f) * Mathf.Deg2Rad;
                // Right arc from -60 to 60 degrees mirrored for the left, so both rise together.
                var local = new Vector3(Mathf.Cos(angle) * 0.2f * side, 0.02f + Mathf.Sin(angle) * 0.2f, 0.236f);
                // Only the upper and lower parts: the jaws cover the middle of each side.
                GameObject piece = BuildKit.Box("Segment", t, local, new Vector3(0.016f, 0.05f, 0.006f), segment, 1f, false);
                piece.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg * side);
                socket.segments[i] = piece.GetComponent<Renderer>();
                socket.segments[i].material = new Material(segment);
                // Dark glass when unlit: only the emission shows the charge.
                socket.segments[i].material.SetColor("_BaseColor", new Color(0.05f, 0.05f, 0.05f));
            }

            // The hinged hatch over the bore, hazard-striped, hinge at the top.
            socket.hatch = new GameObject("HatchHinge").transform;
            socket.hatch.SetParent(t, false);
            // Cylinder sizes are diameters: the hatch is 36 cm across, over the 33 cm hole.
            socket.hatch.localPosition = new Vector3(0f, 0.02f + 0.18f, 0.245f);
            BuildKit.Cylinder("Hatch", socket.hatch, new Vector3(0f, -0.18f, 0f), 0.36f, 0.02f, stripes).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            BuildKit.Box("Hinge", socket.hatch, Vector3.zero, new Vector3(0.14f, 0.025f, 0.025f), chrome, 1f, false);
            BuildKit.Box("Handle", socket.hatch, new Vector3(0f, -0.33f, 0.018f), new Vector3(0.08f, 0.022f, 0.02f), chrome, 1f, false);

            // Clamp jaws at the sides, on short rails.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                BuildKit.Box("JawRail", t, new Vector3(side * 0.205f, 0.02f, 0.255f), new Vector3(0.09f, 0.03f, 0.012f), dark, 1f, false);
                socket.jaws[i] = new GameObject("Jaw").transform;
                socket.jaws[i].SetParent(t, false);
                BuildKit.Box("Block", socket.jaws[i], Vector3.zero, new Vector3(0.05f, 0.075f, 0.04f), steel, 1f, false);
                BuildKit.Box("Tooth", socket.jaws[i], new Vector3(-side * 0.03f, 0f, 0.006f), new Vector3(0.015f, 0.05f, 0.026f), chrome, 1f, false);
            }

            // Status lamp in a bezel on the plate, above the housing.
            BuildKit.Box("LampBezel", t, new Vector3(0f, 0.35f, 0.05f), new Vector3(0.14f, 0.06f, 0.03f), dark, 1f, false);
            GameObject lampObject = BuildKit.Box("Lamp", t, new Vector3(0f, 0.35f, 0.067f), new Vector3(0.11f, 0.035f, 0.01f), BuildKit.Glow("socketLamp", Color.red, 2f), 1f, false);
            socket.lamp = lampObject.GetComponent<Renderer>();
            socket.lamp.material = new Material(socket.lamp.sharedMaterial);

            // Conduits up to the ceiling, a cable gland under the housing, and the pulse of power
            // that runs up the conduits once the cell is in.
            Material pulse = BuildKit.Additive("conduitPulse", ProceduralTextures.Disc(32), Color.white);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.14f : 0.14f;
                BuildKit.Cylinder("Conduit", t, new Vector3(x, 1.2f, 0.06f), 0.05f, 1.9f, chrome);
                BuildKit.Box("Strap", t, new Vector3(x, 0.75f, 0.06f), new Vector3(0.07f, 0.02f, 0.07f), dark, 1f, false);
                BuildKit.Box("Strap", t, new Vector3(x, 1.6f, 0.06f), new Vector3(0.07f, 0.02f, 0.07f), dark, 1f, false);
                GameObject glow = BuildKit.Quad("Pulse", t, new Vector3(x, 0.4f, 0.1f), new Vector2(0.22f, 0.32f), 180f, new Material(pulse));
                socket.pulses[i] = glow.transform;
                glow.SetActive(false);
            }
            socket.pulseLight = new GameObject("PulseLight").AddComponent<Light>();
            socket.pulseLight.transform.SetParent(t, false);
            socket.pulseLight.type = LightType.Point;
            socket.pulseLight.range = 1.4f;
            socket.pulseLight.shadows = LightShadows.None;
            socket.pulseLight.enabled = false;
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
            sparkRoot.transform.localPosition = new Vector3(0f, 0.02f, 0.3f);
            LevelBuilder.Sparks(sparkRoot.transform);
            socket.sparks = sparkRoot.GetComponentInChildren<ParticleSystem>();
            socket.sparks.Stop();
            return socket;
        }

        /// <summary>
        /// A new night: empty and shut, the gauge dim in its color (amber on night 1, when any cell
        /// fits). A socket that is not live tonight is dark: no ring, no lamp, a dead gauge.
        /// </summary>
        public void ResetSocket(bool colored)
        {
            StopAllCoroutines();
            busy = false;
            Powered = false;
            seated.SetActive(false);
            color = !Live ? new Color(0.08f, 0.08f, 0.08f) : colored ? CellColors.Of(Index) : new Color(1f, 0.6f, 0.2f);
            SetHatch(0f);
            SetJaws(0f);
            SetGauge(0f, 0f);
            ringLight.color = color;
            ringLight.intensity = 0.35f;
            ringLight.enabled = Live;
            SetLamp(Live ? new Color(1f, 0.08f, 0.04f) : Color.black, 2f);
            pulseLight.enabled = false;
            foreach (Transform pulse in pulses)
            {
                pulse.gameObject.SetActive(false);
            }
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || Powered || busy)
            {
                return "";
            }
            HuntState state = director.State;
            if (!Live)
            {
                return "Dead socket: no power on this line tonight. Live tonight: " + director.LiveSocketsText;
            }
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
            if (!Live || Powered || busy || !player.Cell.Carrying || !director.State.CellFits(player.Cell.CellIndex, Index))
            {
                return;
            }
            StartCoroutine(Insert(player));
        }

        private IEnumerator Insert(PlayerController player)
        {
            busy = true;
            int socket = player.Cell.Socket;
            Color lit = CellColors.Of(socket);
            Vector3 center = transform.TransformPoint(Mouth);

            // The hatch flips up while the hand brings the cell.
            director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 0.3f, 2.2f);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                SetHatch(Overshoot(t / 0.3f));
                player.LookToward(center, 7f);
                yield return null;
            }
            SetHatch(1f);

            Pose hand = player.Cell.Release();
            CellColors.Paint(seated, socket);
            seated.SetActive(true);
            Transform cell = seated.transform;
            var mouth = new Vector3(0f, 0.02f, 0.48f);
            var contact = new Vector3(0f, 0.02f, 0.2f);
            Quaternion inSocket = Quaternion.Euler(-90f, 0f, 0f);
            Vector3 handLocal = transform.InverseTransformPoint(hand.position);
            Quaternion handRotation = Quaternion.Inverse(transform.rotation) * hand.rotation;
            // From the hand to the mouth, lined up with the bore.
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float k = Ease(t / 0.35f);
                cell.localPosition = Vector3.Lerp(handLocal, mouth, k);
                cell.localRotation = Quaternion.Slerp(handRotation, inSocket, k);
                player.LookToward(center, 7f);
                yield return null;
            }
            // Pushed in, slow at first then home against the contacts.
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float k = t / 0.3f;
                cell.localPosition = Vector3.Lerp(mouth, contact, k * k);
                player.LookToward(center, 7f);
                yield return null;
            }
            cell.localPosition = contact;

            // The jaws bite: a clunk that knocks the view.
            for (float t = 0f; t < 0.07f; t += Time.deltaTime)
            {
                SetJaws(t / 0.07f);
                yield return null;
            }
            SetJaws(1f);
            director.Audio.PlayAt(GameAudio.Clunk(), transform.position, 0.9f);
            player.Jolt(1.4f);
            sparks.Emit(12);

            // A quarter turn in three ratchet clicks.
            for (int click = 0; click < 3; click++)
            {
                Quaternion from = inSocket * Quaternion.Euler(0f, 30f * click, 0f);
                Quaternion to = inSocket * Quaternion.Euler(0f, 30f * (click + 1), 0f);
                for (float t = 0f; t < 0.11f; t += Time.deltaTime)
                {
                    cell.localRotation = Quaternion.Slerp(from, to, Ease(t / 0.11f));
                    yield return null;
                }
                cell.localRotation = to;
                director.Audio.PlayAt(GameAudio.Ratchet(), transform.position, 0.6f, 0.9f + click * 0.08f);
                yield return new WaitForSeconds(0.06f);
            }

            // Charging: the gauge fills up both sides, the lamp blinks amber, the whine rises.
            director.Audio.PlayAt(GameAudio.Charge(), transform.position, 0.55f);
            const float charge = 0.9f;
            for (float t = 0f; t < charge; t += Time.deltaTime)
            {
                float k = t / charge;
                SetGauge(k, 1f);
                ringLight.color = Color.Lerp(color, lit, k);
                ringLight.intensity = Mathf.Lerp(0.35f, 1.2f, k) + Random.Range(-0.1f, 0.1f);
                bool blink = Mathf.Repeat(t, 0.16f) < 0.08f;
                SetLamp(blink ? new Color(1f, 0.55f, 0.05f) : new Color(0.2f, 0.08f, 0f), 2.5f);
                yield return null;
            }

            // Powered: green, a burst, light running up the conduits, the room's lamps surge.
            Powered = true;
            SetGauge(1f, 1.6f);
            SetLamp(new Color(0.1f, 1f, 0.2f), 3f);
            sparks.Emit(35);
            player.MakeNoise(2);
            player.Jolt(0.6f);
            director.Audio.PlayAt(director.Assets.FusePickup, transform.position, 0.8f, 1.3f);
            ringLight.intensity = 3f;
            FlickerLight.Surge(transform.position, 9f, 1.4f);
            director.OnCellInserted(this);
            busy = false;
            yield return RunUpTheConduits(lit);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                ringLight.intensity = Mathf.Lerp(3f, 1.4f, t / 0.5f);
                SetGauge(1f, Mathf.Lerp(1.6f, 1f, t / 0.5f));
                yield return null;
            }
            ringLight.intensity = 1.4f;
        }

        /// <summary>A bead of light climbing both conduits to the ceiling, lighting the wall as it goes.</summary>
        private IEnumerator RunUpTheConduits(Color lit)
        {
            pulseLight.enabled = true;
            pulseLight.color = lit;
            foreach (Transform pulse in pulses)
            {
                pulse.gameObject.SetActive(true);
                pulse.GetComponent<Renderer>().material.SetColor("_BaseColor", lit * 1.5f);
            }
            const float seconds = 0.55f;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                float y = Mathf.Lerp(0.35f, 2.15f, k * k);
                float fade = 1f - Mathf.Clamp01((k - 0.8f) / 0.2f);
                foreach (Transform pulse in pulses)
                {
                    pulse.localPosition = new Vector3(pulse.localPosition.x, y, 0.12f);
                    pulse.localScale = new Vector3(0.22f, 0.32f, 1f) * fade;
                }
                pulseLight.transform.localPosition = new Vector3(0f, y, 0.25f);
                pulseLight.intensity = 2.2f * fade;
                yield return null;
            }
            foreach (Transform pulse in pulses)
            {
                pulse.gameObject.SetActive(false);
            }
            pulseLight.enabled = false;
        }

        private void SetHatch(float open) => hatch.localRotation = Quaternion.Euler(-105f * open, 0f, 0f);

        private void SetJaws(float closed)
        {
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                jaws[i].localPosition = new Vector3(side * Mathf.Lerp(0.24f, 0.135f, closed), 0.02f, 0.3f);
            }
        }

        /// <summary>Lights the gauge up to <paramref name="fill"/> (0 to 1), both arcs from the bottom; unlit segments glow faintly.</summary>
        private void SetGauge(float fill, float brightness)
        {
            int half = Segments / 2;
            for (int i = 0; i < Segments; i++)
            {
                int step = i % half;
                bool on = fill * half > step + 0.001f && fill > 0f;
                segments[i].material.SetColor("_EmissionColor", on ? color * (1.6f * brightness) : color * 0.12f);
            }
        }

        private void SetLamp(Color tint, float strength) => lamp.material.SetColor("_EmissionColor", tint * strength);

        private void Update()
        {
            if (!Powered && !busy && Live)
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

        /// <summary>Eases past the end and settles back, like something flung open against its stop.</summary>
        private static float Overshoot(float t)
        {
            t = Mathf.Clamp01(t);
            float s = 1.7f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
