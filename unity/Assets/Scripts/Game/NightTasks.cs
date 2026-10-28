using System.Collections;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>Helpers shared by the night's tasks.</summary>
    public static class TaskKit
    {
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>A drawn texture as a matte material, or a plain color if it was not imported.</summary>
        public static Material Label(string name, Color fallback, float glow = 0f)
        {
            Texture2D texture = BuildKit.SignTexture(name);
            return texture != null ? BuildKit.Picture("label:" + name + glow.ToString("0.0"), texture, glow) : BuildKit.Plain("label:" + name, fallback, 0.3f);
        }

        /// <summary>A small glowing lamp with its own material, so it can change color.</summary>
        public static Renderer Lamp(Transform parent, Vector3 position, Vector3 size)
        {
            Renderer renderer = BuildKit.Box("Lamp", parent, position, size, BuildKit.Glow("taskLamp", new Color(1f, 0.1f, 0.05f), 2f), 1f, false).GetComponent<Renderer>();
            renderer.material = new Material(renderer.sharedMaterial);
            return renderer;
        }

        public static void SetLamp(Renderer lamp, Color color)
        {
            if (lamp != null)
            {
                lamp.material.SetColor("_EmissionColor", color * 2f);
            }
        }

        /// <summary>Where the eyes go to work a panel: <paramref name="distance"/> in front of it, looking at it.</summary>
        public static Transform View(Transform panel, Vector3 target, float distance, float height = 0f)
        {
            var view = new GameObject("FocusView").transform;
            view.SetParent(panel, false);
            view.localPosition = target + new Vector3(0f, height, distance);
            view.localRotation = Quaternion.LookRotation(target - view.localPosition);
            return view;
        }

        /// <summary>
        /// Something small is taken: first straight out of where it lies, along <paramref name="outOf"/>
        /// (up out of an open case or box, never through its walls), then up in front of the eyes for
        /// a moment, then down into a pocket. Darkened as it comes close, or the flashlight would burn it white.
        /// </summary>
        public static IEnumerator Pocket(PlayerController player, Transform thing, Vector3 outOf, float clearance, Quaternion shown)
        {
            Vector3 start = thing.position;
            Quaternion startRotation = thing.rotation;
            Vector3 startScale = thing.localScale;
            Vector3 lifted = start + outOf.normalized * clearance;
            Renderer[] renderers = thing.GetComponentsInChildren<Renderer>();
            var shade = new MaterialPropertyBlock();
            const float liftSeconds = 0.4f, bringSeconds = 0.45f, holdSeconds = 0.35f, pocketSeconds = 0.3f;
            float total = liftSeconds + bringSeconds + holdSeconds + pocketSeconds;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                Transform view = player.View.transform;
                Vector3 look = view.TransformPoint(new Vector3(0.05f, -0.1f, 0.5f));
                Vector3 hand = view.TransformPoint(new Vector3(0.22f, -0.45f, 0.25f));
                float darken;
                if (t < liftSeconds)
                {
                    // Out along the opening, slowing at the top; a slight tilt as the fingers pull it.
                    float k = Ease(t / liftSeconds);
                    thing.position = Vector3.Lerp(start, lifted, 1f - (1f - k) * (1f - k));
                    thing.rotation = startRotation * Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * 6f, 0f, Mathf.Sin(k * Mathf.PI) * 4f);
                    player.LookToward(thing.position, 5f);
                    darken = 0f;
                }
                else if (t < liftSeconds + bringSeconds)
                {
                    // To the eyes, in an arc, turning to show its face.
                    float k = Ease((t - liftSeconds) / bringSeconds);
                    thing.position = Vector3.Lerp(lifted, look, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.05f;
                    thing.rotation = Quaternion.Slerp(startRotation, view.rotation * shown, k);
                    darken = k * 0.6f;
                }
                else if (t < liftSeconds + bringSeconds + holdSeconds)
                {
                    // A look at it, held in a hand that is not quite still.
                    float wobble = Time.time * 7f;
                    thing.position = look + view.TransformVector(new Vector3(Mathf.Sin(wobble) * 0.003f, Mathf.Cos(wobble * 1.3f) * 0.003f, 0f));
                    thing.rotation = view.rotation * shown;
                    darken = 0.6f;
                }
                else
                {
                    // Away into the pocket.
                    float k = Ease((t - liftSeconds - bringSeconds - holdSeconds) / pocketSeconds);
                    thing.position = Vector3.Lerp(look, hand, k);
                    thing.rotation = view.rotation * shown * Quaternion.Euler(k * 50f, 0f, 0f);
                    thing.localScale = startScale * Mathf.Lerp(1f, 0.8f, k);
                    darken = 0.6f + k * 0.4f;
                }
                foreach (Renderer renderer in renderers)
                {
                    shade.SetColor("_BaseColor", renderer.sharedMaterial.GetColor("_BaseColor") * Mathf.Lerp(1f, 0.3f, darken));
                    renderer.SetPropertyBlock(shade);
                }
                yield return null;
            }
            foreach (Renderer renderer in renderers)
            {
                renderer.SetPropertyBlock(null);
            }
            thing.gameObject.SetActive(false);
            thing.position = start;
            thing.rotation = startRotation;
            thing.localScale = startScale;
        }
    }

    /// <summary>
    /// The breaker box in the storage (night 3 on). E opens its door and steps up to it; some of
    /// the six breakers have tripped (different ones every night). The fingers go from breaker to
    /// breaker (mouse, arrows or WASD) and E or a click flips the one they are on (a loud clack
    /// Freddy can hear); flipping one that is on trips it again. With every breaker up, the main
    /// handle on the side is pushed up and the power comes back.
    /// </summary>
    public class BreakerBox : MonoBehaviour, IInteractable, IFocusTask
    {
        private HuntDirector director;
        private Transform door;
        private Transform handle;
        private readonly Transform[] toggles = new Transform[6];
        private FocusSelector selector;
        private readonly bool[] up = new bool[6];
        private readonly Renderer[] flags = new Renderer[6];
        private readonly float[] shown = new float[6];
        private Renderer lamp;
        private ParticleSystem sparks;
        private float doorShown;
        private float handleShown;
        private float handleOpen = 150f;    // the main handle's angle (about x) when off, and when on
        private float handleClosed = 15f;
        private bool opening;

        public bool Done { get; private set; }
        public bool HoldToUse => false;
        public Transform FocusView { get; private set; }

        private bool AllUp
        {
            get
            {
                foreach (bool on in up)
                {
                    if (!on)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public string FocusControls => "[MOUSE] or [WASD] move your hand";
        public string FocusHint => AllUp ? "Every breaker is up: throw the main switch  [E]" : "Flip the tripped breakers up  [E]   (each one is loud)";

        public static BreakerBox Create(HuntDirector director, Transform parent, Vector3 position, float facing)
        {
            var go = new GameObject("BreakerBox");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, facing, 0f);
            var box = go.AddComponent<BreakerBox>();
            box.director = director;
            box.Build();
            box.FocusView = TaskKit.View(go.transform, new Vector3(0.16f, 0f, 0.1f), 1.1f, 0.05f);
            box.ResetTask(3);
            return box;
        }

        private void Build()
        {
            Transform t = transform;
            Material steel = BuildKit.Pbr("metal_worn", new Color(0.5f, 0.55f, 0.52f)) ?? BuildKit.Plain("breakerSteel", new Color(0.33f, 0.36f, 0.34f), 0.4f, 0.6f);
            Material doorSteel = BuildKit.Pbr("metal_green", new Color(0.75f, 0.8f, 0.78f)) ?? steel;
            Material inside = BuildKit.Plain("breakerInside", new Color(0.05f, 0.05f, 0.055f), 0.3f, 0.5f);
            // Matte: a flat face square to the flashlight would otherwise light up like a mirror.
            Material bakelite = BuildKit.Plain("bakelite", new Color(0.05f, 0.048f, 0.045f), 0.25f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);

            BuildKit.Box("BreakerBox", t, Vector3.zero, new Vector3(0.8f, 1.0f, 0.2f), steel, 1f);
            BuildKit.Box("Inside", t, new Vector3(0f, 0f, 0.095f), new Vector3(0.72f, 0.92f, 0.01f), inside, 1f, false);
            if (BuildKit.SignTexture("breaker_deadfront") != null)
            {
                BuildKit.Quad("DeadFront", t, new Vector3(0f, 0f, 0.1005f), new Vector2(0.72f, 0.92f), 0f, TaskKit.Label("breaker_deadfront", Color.grey));
            }
            BuildKit.Box("RimTop", t, new Vector3(0f, 0.485f, 0.135f), new Vector3(0.8f, 0.03f, 0.07f), steel, 1f, false);
            BuildKit.Box("RimBottom", t, new Vector3(0f, -0.485f, 0.135f), new Vector3(0.8f, 0.03f, 0.07f), steel, 1f, false);
            BuildKit.Box("RimLeft", t, new Vector3(-0.385f, 0f, 0.135f), new Vector3(0.03f, 1.0f, 0.07f), steel, 1f, false);
            BuildKit.Box("RimRight", t, new Vector3(0.385f, 0f, 0.135f), new Vector3(0.03f, 1.0f, 0.07f), steel, 1f, false);
            BuildKit.Box("Conduit", t, new Vector3(-0.25f, 0.85f, -0.02f), new Vector3(0.07f, 0.7f, 0.07f), chrome, 1f, false);
            BuildKit.Box("Conduit", t, new Vector3(0.25f, 0.85f, -0.02f), new Vector3(0.07f, 0.7f, 0.07f), chrome, 1f, false);

            // Six breakers in two columns, and the main handle: what the fingers can reach.
            var reach = new Transform[7];
            var sizes = new Vector2[7];
            for (int i = 0; i < 6; i++)
            {
                var place = new Vector3(i < 3 ? -0.13f : 0.13f, 0.2f - (i % 3) * 0.2f, 0.12f);
                BuildKit.Box("Breaker", t, place, new Vector3(0.18f, 0.12f, 0.04f), bakelite, 1f, false);
                if (BuildKit.SignTexture("breaker_module") != null)
                {
                    BuildKit.Quad("BreakerFace", t, place + new Vector3(0f, 0f, 0.0205f), new Vector2(0.18f, 0.12f), 0f, TaskKit.Label("breaker_module", Color.black));
                }
                var pivot = new GameObject("Toggle").transform;
                pivot.SetParent(t, false);
                pivot.localPosition = place + new Vector3(0f, 0f, 0.02f);
                BuildKit.Box("Lever", pivot, new Vector3(0f, 0.03f, 0f), new Vector3(0.036f, 0.06f, 0.026f), bakelite, 1f, false);
                for (int rib = 0; rib < 3; rib++)
                {
                    BuildKit.Box("Rib", pivot, new Vector3(0f, 0.04f + rib * 0.008f, 0.014f), new Vector3(0.036f, 0.003f, 0.004f), BuildKit.Plain("rib", new Color(0.12f, 0.12f, 0.13f), 0.3f), 1f, false);
                }
                // The trip flag: a red window that shows while the breaker is off.
                flags[i] = BuildKit.Box("TripFlag", t, place + new Vector3(0.062f, 0.032f, 0.0215f), new Vector3(0.026f, 0.018f, 0.002f),
                                        BuildKit.Glow("tripFlag", new Color(1f, 0.15f, 0.05f), 1.6f), 1f, false).GetComponent<Renderer>();
                toggles[i] = pivot;
                reach[i] = pivot;
                sizes[i] = new Vector2(0.17f, 0.11f);
            }

            door = new GameObject("DoorHinge").transform;
            door.SetParent(t, false);
            door.localPosition = new Vector3(-0.4f, 0f, 0.17f);
            BuildKit.Box("Door", door, new Vector3(0.39f, 0f, 0.012f), new Vector3(0.78f, 0.98f, 0.022f), doorSteel, 1f, false);
            BuildKit.Quad("Warning", door, new Vector3(0.39f, 0.15f, 0.0245f), new Vector2(0.3f, 0.375f), 0f, TaskKit.Label("label_danger", new Color(0.9f, 0.75f, 0.1f)));
            BuildKit.Box("Latch", door, new Vector3(0.72f, -0.05f, 0.04f), new Vector3(0.035f, 0.16f, 0.035f), chrome, 1f, false);
            if (BuildKit.SignTexture("breaker_directory") != null)
            {
                BuildKit.Quad("Directory", door, new Vector3(0.39f, 0.08f, -0.0005f), new Vector2(0.3f, 0.4f), 180f, TaskKit.Label("breaker_directory", Color.white));
            }

            // The main switch on the wall beside the box: an old knife switch, open (its blade
            // hanging out of the jaws) until every breaker is up. A red-gripped handle if the model is missing.
            GameObject knife = director.Assets != null ? director.Assets.KnifeSwitchModel : null;
            var grip = new GameObject("HandleReach").transform;
            if (knife != null)
            {
                GameObject main = Instantiate(knife, t, false);
                main.name = "MainSwitch";
                main.transform.localPosition = new Vector3(0.56f, -0.36f, -0.098f);
                handle = BuildKit.Part(main, "Blade");
                handleOpen = 55f;
                // A caged bulb over it, so the switch reads in the dark beside the box.
                var bulb = new GameObject("SwitchLight").AddComponent<Light>();
                bulb.transform.SetParent(t, false);
                bulb.transform.localPosition = new Vector3(0.56f, 0.35f, 0.3f);
                bulb.type = LightType.Point;
                bulb.range = 1.3f;
                bulb.intensity = 1.2f;
                bulb.color = new Color(1f, 0.78f, 0.5f);
                bulb.shadows = LightShadows.None;
                handleClosed = 0f;
                grip.SetParent(handle, false);
                grip.localPosition = new Vector3(0f, 0.36f, 0.02f);
                sizes[6] = new Vector2(0.14f, 0.3f);
            }
            if (handle == null)
            {
                BuildKit.Cylinder("HandleHub", t, new Vector3(0.43f, -0.1f, 0.04f), 0.09f, 0.06f, chrome).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                handle = new GameObject("HandlePivot").transform;
                handle.SetParent(t, false);
                handle.localPosition = new Vector3(0.47f, -0.1f, 0.04f);
                BuildKit.Box("HandleArm", handle, new Vector3(0f, 0.14f, 0f), new Vector3(0.035f, 0.28f, 0.035f), chrome, 1f, false);
                BuildKit.Box("HandleGrip", handle, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.09f, 0.06f), BuildKit.Plain("leverGrip", new Color(0.6f, 0.05f, 0.04f), 0.4f), 1f, false);
                grip.SetParent(t, false);
                grip.localPosition = new Vector3(0.47f, -0.05f, 0.06f);
                sizes[6] = new Vector2(0.12f, 0.42f);
            }
            reach[6] = grip;
            selector = new FocusSelector(t, reach, sizes);

            lamp = TaskKit.Lamp(t, new Vector3(0f, 0.54f, 0.04f), new Vector3(0.22f, 0.06f, 0.06f));
            var sparksRoot = new GameObject("BreakerSparks");
            sparksRoot.transform.SetParent(t, false);
            LevelBuilder.Sparks(sparksRoot.transform);
            sparks = sparksRoot.GetComponentInChildren<ParticleSystem>();
            sparks.Stop();
        }

        /// <summary>A new night: door shut, handle down, a different set of breakers tripped.</summary>
        public void ResetTask(int seed)
        {
            StopAllCoroutines();
            Done = false;
            opening = false;
            doorShown = 0f;
            handleShown = 0f;
            var random = new System.Random(seed);
            for (int i = 0; i < 6; i++)
            {
                up[i] = true;
            }
            int tripped = 3 + random.Next(2);
            for (int n = 0; n < tripped; n++)
            {
                int i;
                do
                {
                    i = random.Next(6);
                }
                while (!up[i]);
                up[i] = false;
            }
            for (int i = 0; i < 6; i++)
            {
                shown[i] = up[i] ? 1f : 0f;
            }
            Draw();
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || Done || opening || !director.State.NeedsBreaker)
            {
                return "";
            }
            return doorShown < 1f ? "[E] Open the breaker box" : "[E] Reset the breakers";
        }

        public void Use(PlayerController player)
        {
            if (Done || opening || !director.State.NeedsBreaker)
            {
                return;
            }
            if (doorShown < 1f)
            {
                StartCoroutine(OpenDoor(player));
            }
            else
            {
                player.BeginFocus(this);
            }
        }

        private IEnumerator OpenDoor(PlayerController player)
        {
            opening = true;
            director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 0.4f, 1.3f);
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                doorShown = t / 0.4f;
                yield return null;
            }
            doorShown = 1f;
            opening = false;
            player.BeginFocus(this);
        }

        public void FocusUpdate(FocusInput input)
        {
            if (Done)
            {
                selector.Hide();
                return;
            }
            int on = selector.Update(input, director.Player.View, i => true);
            if (!input.Pressed || on < 0)
            {
                return;
            }
            if (on < 6)
            {
                Flip(on);
            }
            else
            {
                if (AllUp)
                {
                    StartCoroutine(Restore());
                }
                else
                {
                    // It will not stay up with a breaker still down: it springs back with a spark.
                    handleShown = 0.4f;
                    sparks.transform.position = handle.position + transform.TransformDirection(new Vector3(0f, 0.1f, 0.05f));
                    sparks.Emit(10);
                    director.Player.MakeNoise(2);
                    director.Audio.Beep(0.5f);
                }
            }
        }

        public void FocusEnded()
        {
            selector.Hide();
        }

        private void Flip(int i)
        {
            up[i] = !up[i];
            director.Player.MakeNoise(2);
            director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 0.3f, up[i] ? 2.4f : 2f);
            director.Player.Jolt(0.25f);
            if (!up[i])
            {
                sparks.transform.localPosition = toggles[i].localPosition + new Vector3(0f, 0f, 0.05f);
                sparks.Emit(6);
            }
        }

        private IEnumerator Restore()
        {
            Done = true;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                handleShown = Mathf.Max(handleShown, t / 0.5f);
                yield return null;
            }
            handleShown = 1f;
            sparks.transform.position = handle.position + transform.TransformDirection(new Vector3(0f, 0.1f, 0.05f));
            sparks.Emit(40);
            director.Player.Jolt(1.2f);
            director.Audio.PlayAt(GameAudio.Clunk(), transform.position, 1f, 0.7f);
            director.OnBreakerRestored(this);
            yield return new WaitForSeconds(0.6f);
            if (director.Player.Focus == (IFocusTask)this)
            {
                director.Player.EndFocus();
            }
        }

        /// <summary>Test helper (capture tour): flips every tripped breaker and pushes the handle, as a player would.</summary>
        public IEnumerator AutoPlay(PlayerController player)
        {
            player.BeginFocus(this);
            yield return new WaitForSeconds(0.5f);
            for (int i = 0; i < 6; i++)
            {
                if (!up[i])
                {
                    yield return selector.Press(this, player.View, i);
                    yield return new WaitForSeconds(0.35f);
                }
            }
            yield return selector.Press(this, player.View, 6);
            yield return new WaitForSeconds(1.2f);
        }

        private void Update()
        {
            if (!Done && handleShown > 0f && handleShown < 1f)
            {
                handleShown = Mathf.MoveTowards(handleShown, 0f, Time.deltaTime * 1.5f);
            }
            for (int i = 0; i < 6; i++)
            {
                shown[i] = Mathf.MoveTowards(shown[i], up[i] ? 1f : 0f, Time.deltaTime * 9f);
            }
            Draw();
        }

        private void Draw()
        {
            door.localRotation = Quaternion.Euler(0f, -110f * TaskKit.Ease(doorShown), 0f);
            for (int i = 0; i < 6; i++)
            {
                // Down (tripped) to up, snapping over at the end.
                toggles[i].localRotation = Quaternion.Euler(Mathf.Lerp(35f, -35f, TaskKit.Ease(shown[i])), 0f, 0f);
                flags[i].enabled = !up[i];
            }
            handle.localRotation = Quaternion.Euler(Mathf.Lerp(handleOpen, handleClosed, TaskKit.Ease(handleShown)), 0f, 0f);
            bool blink = Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
            TaskKit.SetLamp(lamp, Done ? new Color(0.1f, 1f, 0.2f) : doorShown > 0f ? (blink ? new Color(1f, 0.55f, 0f) : new Color(0.3f, 0.12f, 0f)) : new Color(1f, 0.1f, 0.05f));
        }
    }

    /// <summary>
    /// The staff keycard (night 4 on), in a glass case on a wooden plinth in the veranda, under its
    /// own spotlight, so it is the one thing that stands out in the room. E lifts the lid and takes it.
    /// </summary>
    public class KeycardPedestal : MonoBehaviour, IInteractable
    {
        private HuntDirector director;
        private Transform card;
        private Transform lid;
        private Light spot;
        private bool taking;

        public bool HoldToUse => false;

        public static KeycardPedestal Create(HuntDirector director, Transform parent, Vector3 floor)
        {
            var go = new GameObject("KeycardPedestal");
            go.transform.SetParent(parent, false);
            go.transform.position = floor;
            go.transform.rotation = Quaternion.Euler(0f, 0f, 0f);   // against the north wall, the card facing the room
            Transform t = go.transform;
            var pedestal = go.AddComponent<KeycardPedestal>();
            pedestal.director = director;
            Material velvet = BuildKit.Pbr("velvet", new Color(0.55f, 0.08f, 0.12f)) ?? BuildKit.Plain("pedestalRed", new Color(0.4f, 0.05f, 0.08f), 0.2f);
            Material brass = BuildKit.Plain("brass", new Color(0.62f, 0.48f, 0.24f), 0.75f, 1f);
            Material wood = BuildKit.Pbr("stage_wood", new Color(0.35f, 0.22f, 0.16f)) ?? BuildKit.Plain("plinthWood", new Color(0.2f, 0.12f, 0.08f), 0.4f);
            Material glass = BuildKit.Decal("windowGlass", Texture2D.whiteTexture, new Color(0.55f, 0.62f, 0.7f, 0.12f));
            // A wooden plinth with brass trim, like the prize stands at the ticket counter.
            BuildKit.Box("Base", t, new Vector3(0f, 0.04f, 0f), new Vector3(0.58f, 0.08f, 0.5f), wood);
            BuildKit.Box("Column", t, new Vector3(0f, 0.52f, 0f), new Vector3(0.46f, 0.9f, 0.38f), wood);
            BuildKit.Box("TrimLow", t, new Vector3(0f, 0.1f, 0f), new Vector3(0.48f, 0.03f, 0.4f), brass, 1f, false);
            BuildKit.Box("TrimHigh", t, new Vector3(0f, 0.94f, 0f), new Vector3(0.48f, 0.03f, 0.4f), brass, 1f, false);
            BuildKit.Box("Top", t, new Vector3(0f, 0.98f, 0f), new Vector3(0.54f, 0.04f, 0.46f), wood, 1f, false);
            BuildKit.Box("Plaque", t, new Vector3(0f, 0.72f, 0.195f), new Vector3(0.22f, 0.07f, 0.008f), brass, 1f, false);
            // A velvet cushion, the card leaning on it.
            BuildKit.Box("Cushion", t, new Vector3(0f, 1.02f, 0f), new Vector3(0.26f, 0.04f, 0.2f), velvet, 1f, false);
            var stand = BuildKit.Box("Stand", t, new Vector3(0f, 1.06f, 0f), new Vector3(0.14f, 0.008f, 0.1f), BuildKit.Plain("standBlack", new Color(0.04f, 0.04f, 0.04f), 0.5f), 1f, false);
            stand.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            pedestal.card = new GameObject("Keycard").transform;
            pedestal.card.SetParent(t, false);
            pedestal.card.localPosition = new Vector3(0f, 1.075f, 0.005f);
            pedestal.card.localRotation = Quaternion.Euler(-30f, 0f, 0f) * Quaternion.Euler(-90f, 0f, 0f);
            BuildKit.Box("CardBody", pedestal.card, Vector3.zero, new Vector3(0.12f, 0.076f, 0.004f), BuildKit.Plain("cardWhite", new Color(0.55f, 0.56f, 0.58f), 0.25f), 1f, false);
            BuildKit.Quad("CardFace", pedestal.card, new Vector3(0f, 0f, 0.0025f), new Vector2(0.12f, 0.076f), 0f, TaskKit.Label("label_keycard", new Color(0.3f, 0.5f, 0.9f)));
            // The glass case over it, brass posts at the corners; its lid lifts on a hinge at the back.
            const float caseY = 1.0f, caseH = 0.24f, halfX = 0.16f, halfZ = 0.13f;
            BuildKit.Box("GlassFront", t, new Vector3(0f, caseY + caseH * 0.5f, halfZ), new Vector3(halfX * 2f, caseH, 0.004f), glass, 1f, false);
            BuildKit.Box("GlassBack", t, new Vector3(0f, caseY + caseH * 0.5f, -halfZ), new Vector3(halfX * 2f, caseH, 0.004f), glass, 1f, false);
            BuildKit.Box("GlassLeft", t, new Vector3(-halfX, caseY + caseH * 0.5f, 0f), new Vector3(0.004f, caseH, halfZ * 2f), glass, 1f, false);
            BuildKit.Box("GlassRight", t, new Vector3(halfX, caseY + caseH * 0.5f, 0f), new Vector3(0.004f, caseH, halfZ * 2f), glass, 1f, false);
            foreach (Vector2 corner in new[] { new Vector2(-halfX, -halfZ), new Vector2(halfX, -halfZ), new Vector2(-halfX, halfZ), new Vector2(halfX, halfZ) })
            {
                BuildKit.Box("Post", t, new Vector3(corner.x, caseY + caseH * 0.5f, corner.y), new Vector3(0.014f, caseH, 0.014f), brass, 1f, false);
            }
            pedestal.lid = new GameObject("LidHinge").transform;
            pedestal.lid.SetParent(t, false);
            pedestal.lid.localPosition = new Vector3(0f, caseY + caseH, -halfZ);
            BuildKit.Box("Lid", pedestal.lid, new Vector3(0f, 0.004f, halfZ), new Vector3(halfX * 2f + 0.01f, 0.006f, halfZ * 2f + 0.01f), glass, 1f, false);
            BuildKit.Box("LidFrame", pedestal.lid, new Vector3(0f, 0.006f, halfZ * 2f), new Vector3(halfX * 2f + 0.02f, 0.012f, 0.012f), brass, 1f, false);
            BuildKit.Box("LidKnob", pedestal.lid, new Vector3(0f, 0.016f, halfZ * 2f - 0.01f), new Vector3(0.03f, 0.012f, 0.012f), brass, 1f, false);
            var trigger = go.AddComponent<CapsuleCollider>();
            trigger.center = new Vector3(0f, 0.6f, 0f);
            trigger.height = 1.2f;
            trigger.radius = 0.3f;
            // Its own spotlight from the ceiling, and a pool of light on the floor around it.
            pedestal.spot = new GameObject("PedestalSpot").AddComponent<Light>();
            pedestal.spot.transform.SetParent(t, false);
            pedestal.spot.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            pedestal.spot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            pedestal.spot.type = LightType.Spot;
            pedestal.spot.spotAngle = 30f;
            pedestal.spot.innerSpotAngle = 14f;
            pedestal.spot.range = 5f;
            pedestal.spot.intensity = 5f;
            pedestal.spot.color = new Color(1f, 0.92f, 0.78f);
            pedestal.spot.shadows = LightShadows.Soft;
            BuildKit.Cylinder("SpotFixture", t, new Vector3(0f, 3.3f, 0f), 0.18f, 0.16f, BuildKit.Plain("fixtureBlack", new Color(0.05f, 0.05f, 0.05f), 0.4f));
            return pedestal;
        }

        public void ResetTask(bool needed)
        {
            StopAllCoroutines();
            taking = false;
            card.gameObject.SetActive(needed);
            lid.localRotation = Quaternion.identity;
            spot.enabled = needed;
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || taking || !card.gameObject.activeSelf || director.State.CardTaken)
            {
                return "";
            }
            return "[E] Take the staff keycard";
        }

        public void Use(PlayerController player)
        {
            if (taking || !card.gameObject.activeSelf || director.State.CardTaken)
            {
                return;
            }
            taking = true;
            director.OnCardTaken();
            StartCoroutine(Take(player));
        }

        private IEnumerator Take(PlayerController player)
        {
            // The lid swings back on its hinge, then the hand takes the card.
            director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 0.25f, 2.6f);
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float k = TaskKit.Ease(t / 0.35f);
                lid.localRotation = Quaternion.Euler(-100f * k, 0f, 0f);
                player.LookToward(card.position, 6f);
                yield return null;
            }
            lid.localRotation = Quaternion.Euler(-100f, 0f, 0f);
            // Up out of the open case (its lid is back), then to the eyes.
            yield return TaskKit.Pocket(player, card, Vector3.up, 0.34f, Quaternion.Euler(0f, 180f, 0f));
            spot.enabled = false;
            taking = false;
        }
    }

    /// <summary>
    /// The card reader on the office wall. With the keycard, E steps up to it: the card is held at
    /// the start of the slot; holding E (or the mouse button) grips it and moving the mouse right,
    /// or holding D, slides it through. It has to go at a steady speed, like a real reader. Too
    /// fast, too slow or letting go halfway: an error beep (loud) and another try.
    /// </summary>
    public class CardReader : MonoBehaviour, IInteractable, IFocusTask
    {
        private const float StartX = -0.15f;
        private const float EnterX = -0.09f;
        private const float ExitX = 0.1f;
        private const float EndX = 0.17f;
        private const float SlotY = 0.03f;
        private const float MinSeconds = 0.35f;
        private const float MaxSeconds = 1.15f;

        private HuntDirector director;
        private Transform card;
        private float cardX;
        private Renderer screen;
        private Renderer red;
        private Renderer green;
        private bool dragging;
        private float enteredAt = -1f;
        private float messageUntil;
        private bool accepted;

        public bool HoldToUse => false;
        public Transform FocusView { get; private set; }
        public string FocusControls => "[MOUSE] or [D] slide the card";
        public string FocusHint => "Hold [E] and slide the card right at a steady speed  (mouse or D)";

        public static CardReader Create(HuntDirector director, Transform parent, Vector3 position, float facing)
        {
            var go = new GameObject("CardReader");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, facing, 0f);
            Transform t = go.transform;
            var reader = go.AddComponent<CardReader>();
            reader.director = director;
            Material plate = BuildKit.Pbr("metal_worn", new Color(0.45f, 0.47f, 0.5f)) ?? BuildKit.Plain("readerPlate", new Color(0.3f, 0.31f, 0.33f), 0.45f, 0.7f);
            Material plastic = BuildKit.Plain("readerPlastic", new Color(0.06f, 0.06f, 0.065f), 0.35f);
            BuildKit.Box("Plate", t, new Vector3(0f, 0f, 0.01f), new Vector3(0.44f, 0.34f, 0.02f), plate);
            BuildKit.Box("Body", t, new Vector3(0f, -0.02f, 0.045f), new Vector3(0.38f, 0.26f, 0.05f), plastic);
            // The slot: a groove across the top of the body, its front lip hiding the card's edge.
            BuildKit.Box("SlotBack", t, new Vector3(0f, SlotY, 0.072f), new Vector3(0.38f, 0.07f, 0.004f), BuildKit.Plain("slotDark", new Color(0.01f, 0.01f, 0.01f), 0.2f), 1f, false);
            BuildKit.Box("SlotLip", t, new Vector3(0f, SlotY - 0.022f, 0.088f), new Vector3(0.38f, 0.026f, 0.012f), plastic, 1f, false);
            BuildKit.Box("SlotArrow", t, new Vector3(0f, SlotY - 0.022f, 0.0945f), new Vector3(0.2f, 0.004f, 0.001f), BuildKit.Glow("slotArrow", new Color(0.3f, 0.9f, 0.4f), 1.2f), 1f, false);
            GameObject lcd = BuildKit.Quad("Screen", t, new Vector3(0f, -0.07f, 0.071f), new Vector2(0.24f, 0.06f), 0f, TaskKit.Label("lcd_reader_nocard", Color.black, 1.2f));
            reader.screen = lcd.GetComponent<Renderer>();
            reader.red = TaskKit.Lamp(t, new Vector3(-0.05f, -0.125f, 0.072f), new Vector3(0.02f, 0.012f, 0.006f));
            reader.green = TaskKit.Lamp(t, new Vector3(0.05f, -0.125f, 0.072f), new Vector3(0.02f, 0.012f, 0.006f));

            reader.card = new GameObject("SwipeCard").transform;
            reader.card.SetParent(t, false);
            BuildKit.Box("CardBody", reader.card, Vector3.zero, new Vector3(0.12f, 0.076f, 0.004f), BuildKit.Plain("cardWhite", new Color(0.75f, 0.76f, 0.78f), 0.25f), 1f, false);
            BuildKit.Quad("CardFace", reader.card, new Vector3(0f, 0f, 0.0025f), new Vector2(0.12f, 0.076f), 0f, TaskKit.Label("label_keycard", new Color(0.3f, 0.5f, 0.9f)));
            reader.card.gameObject.SetActive(false);
            // Looking a little below the reader, so it sits above the hint at the bottom of the screen.
            reader.FocusView = TaskKit.View(t, new Vector3(0f, -0.04f, 0.08f), 0.5f, 0.02f);
            reader.ResetTask();
            return reader;
        }

        public void ResetTask()
        {
            StopAllCoroutines();
            dragging = false;
            accepted = false;
            enteredAt = -1f;
            card.gameObject.SetActive(false);
            Show("lcd_reader_nocard", 0f);
            TaskKit.SetLamp(red, new Color(0.4f, 0.02f, 0.02f));
            TaskKit.SetLamp(green, new Color(0.02f, 0.1f, 0.03f));
        }

        public string Prompt(PlayerController player)
        {
            HuntState state = director.State;
            if (player.HiddenIn != null || !state.NeedsCard || state.CardSwiped)
            {
                return "";
            }
            return state.CardTaken ? "[E] Swipe the keycard" : "Card reader: it needs the staff keycard (in the Veranda)";
        }

        public void Use(PlayerController player)
        {
            HuntState state = director.State;
            if (!state.NeedsCard || !state.CardTaken || state.CardSwiped)
            {
                return;
            }
            PlaceCard(StartX);
            card.gameObject.SetActive(true);
            Show("lcd_reader_swipe", 0f);
            player.BeginFocus(this);
        }

        private void PlaceCard(float x)
        {
            // Standing in the groove, its lower part hidden behind the lip.
            cardX = x;
            card.localPosition = new Vector3(x, SlotY + 0.012f, 0.08f);
            card.localRotation = Quaternion.identity;
        }

        public void FocusUpdate(FocusInput input)
        {
            if (accepted)
            {
                return;
            }
            if (Time.time > messageUntil && messageUntil > 0f)
            {
                messageUntil = 0f;
                Show("lcd_reader_swipe", 0f);
                TaskKit.SetLamp(red, new Color(0.4f, 0.02f, 0.02f));
            }
            if (input.Pressed)
            {
                dragging = true;
                enteredAt = -1f;
            }
            if (!dragging)
            {
                return;
            }
            // Gripped, the card slides with the hand along the slot.
            float x = Mathf.Clamp(cardX + input.Slide, StartX, EndX);
            PlaceCard(x);
            if (x >= EnterX && enteredAt < 0f)
            {
                enteredAt = Time.time;
            }
            if (x >= ExitX && enteredAt >= 0f)
            {
                dragging = false;
                Judge(Time.time - enteredAt);
                return;
            }
            if (input.Released)
            {
                dragging = false;
                if (enteredAt >= 0f)
                {
                    Fail("lcd_reader_bad");
                }
                else
                {
                    PlaceCard(StartX);
                }
            }
        }

        /// <summary>
        /// Test helper (capture tour): grips the card and slides it through the slot in about
        /// <paramref name="seconds"/>, through the same input path as the player's hand.
        /// </summary>
        public IEnumerator AutoSwipe(float seconds)
        {
            FocusUpdate(new FocusInput { Pressed = true, Held = true });
            float speed = (ExitX - EnterX) / seconds;
            for (float t = 0f; t < seconds * 2f + 1f && dragging; t += Time.deltaTime)
            {
                yield return null;
                FocusUpdate(new FocusInput { Held = true, Slide = speed * Time.deltaTime });
            }
        }

        private void Judge(float seconds)
        {
            if (seconds < MinSeconds)
            {
                Fail("lcd_reader_fast");
            }
            else if (seconds > MaxSeconds)
            {
                Fail("lcd_reader_slow");
            }
            else
            {
                StartCoroutine(Accept());
            }
        }

        private void Fail(string message)
        {
            Show(message, 1.3f);
            TaskKit.SetLamp(red, new Color(1f, 0.08f, 0.05f));
            director.Audio.Beep(0.45f);
            director.Player.MakeNoise(2);
            PlaceCard(StartX);
            enteredAt = -1f;
        }

        private IEnumerator Accept()
        {
            accepted = true;
            Show("lcd_reader_ok", 0f);
            PlaceCard(EndX);
            TaskKit.SetLamp(green, new Color(0.1f, 1f, 0.25f));
            TaskKit.SetLamp(red, new Color(0.1f, 0.01f, 0.01f));
            director.Audio.Beep(1.6f);
            director.OnCardSwiped();
            yield return new WaitForSeconds(0.9f);
            card.gameObject.SetActive(false);
            if (director.Player.Focus == (IFocusTask)this)
            {
                director.Player.EndFocus();
            }
        }

        public void FocusEnded()
        {
            dragging = false;
            if (!accepted)
            {
                card.gameObject.SetActive(false);
                Show("lcd_reader_nocard", 0f);
            }
        }

        private void Show(string message, float seconds)
        {
            screen.sharedMaterial = TaskKit.Label(message, Color.black, 1.2f);
            messageUntil = seconds > 0f ? Time.time + seconds : 0f;
        }
    }

    /// <summary>
    /// The tape (night 5) is locked in a steel box on the ticket booth's counter. Its keypad has
    /// the numbers 1 to 9 in a different order every try: press them in order. A wrong key buzzes
    /// (loud), shuffles the keys and starts over. Open, E takes the tape.
    /// </summary>
    public class TapeBox : MonoBehaviour, IInteractable, IFocusTask
    {
        private HuntDirector director;
        private Transform lid;
        private Transform tape;
        private readonly Transform[] keys = new Transform[9];
        private readonly Renderer[] keyFaces = new Renderer[9];
        private readonly Renderer[] keyGlows = new Renderer[9];
        private FocusSelector selector;
        private readonly int[] numberOn = new int[9];
        private readonly bool[] pressed = new bool[9];
        private Renderer screen;
        private Renderer led;
        private readonly Transform[] latches = new Transform[2];
        private readonly Transform[] strutBodies = new Transform[2];
        private readonly Transform[] strutRods = new Transform[2];
        private Light inside;
        private Pose keypadView;
        private int next = 1;
        private float lidShown;
        private float latchShown;
        private bool opened;
        private bool taking;
        private float wrongUntil;
        private System.Random random = new System.Random(5);

        public bool HoldToUse => false;
        public Transform FocusView { get; private set; }
        public string FocusControls => "[MOUSE] or [WASD] move your hand";
        public string FocusHint => "Press 1 to 9 in order  [E]   (a wrong key buzzes and starts over)";

        public static TapeBox Create(HuntDirector director, Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("TapeBox");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = rotation;
            Transform t = go.transform;
            var box = go.AddComponent<TapeBox>();
            box.director = director;
            Material steel = BuildKit.Pbr("metal_worn", new Color(0.26f, 0.27f, 0.29f)) ?? BuildKit.Plain("tapeBoxSteel", new Color(0.2f, 0.21f, 0.22f), 0.45f, 0.7f);
            Material dark = BuildKit.Plain("tapeBoxInside", new Color(0.04f, 0.04f, 0.045f), 0.3f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Material foam = BuildKit.Plain("foam", new Color(0.09f, 0.09f, 0.095f), 0.05f);
            // A tape safe: thick steel walls, guarded corners, open at the top under a heavy lid.
            BuildKit.Box("Bottom", t, new Vector3(0f, 0.015f, 0f), new Vector3(0.4f, 0.03f, 0.3f), steel);
            BuildKit.Box("Back", t, new Vector3(0f, 0.13f, -0.135f), new Vector3(0.4f, 0.26f, 0.03f), steel);
            BuildKit.Box("Front", t, new Vector3(0f, 0.13f, 0.135f), new Vector3(0.4f, 0.26f, 0.03f), steel);
            BuildKit.Box("Left", t, new Vector3(-0.185f, 0.13f, 0f), new Vector3(0.03f, 0.26f, 0.3f), steel);
            BuildKit.Box("Right", t, new Vector3(0.185f, 0.13f, 0f), new Vector3(0.03f, 0.26f, 0.3f), steel);
            foreach (Vector2 corner in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
            {
                BuildKit.Box("CornerGuard", t, new Vector3(corner.x * 0.198f, 0.13f, corner.y * 0.148f), new Vector3(0.012f, 0.27f, 0.012f), chrome, 1f, false);
            }
            // Inside: grey foam with the cassette's cut-out, and a small lamp that comes on with the lid.
            BuildKit.Box("Foam", t, new Vector3(0f, 0.05f, 0f), new Vector3(0.34f, 0.04f, 0.24f), foam, 1f, false);
            BuildKit.Box("CutOut", t, new Vector3(0f, 0.0705f, 0f), new Vector3(0.2f, 0.002f, 0.115f), dark, 1f, false);
            box.inside = new GameObject("InsideLamp").AddComponent<Light>();
            box.inside.transform.SetParent(t, false);
            box.inside.transform.localPosition = new Vector3(0f, 0.22f, -0.08f);
            box.inside.type = LightType.Point;
            box.inside.range = 0.7f;
            box.inside.color = new Color(0.8f, 0.9f, 1f);
            box.inside.shadows = LightShadows.None;
            box.inside.intensity = 0f;
            // Hinge barrels at the back, the lid with its handle and stenciled words.
            for (int i = 0; i < 2; i++)
            {
                BuildKit.Cylinder("HingeBarrel", t, new Vector3(i == 0 ? -0.12f : 0.12f, 0.265f, -0.155f), 0.022f, 0.08f, chrome).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            box.lid = new GameObject("LidHinge").transform;
            box.lid.SetParent(t, false);
            box.lid.localPosition = new Vector3(0f, 0.26f, -0.15f);
            BuildKit.Box("Lid", box.lid, new Vector3(0f, 0.012f, 0.15f), new Vector3(0.41f, 0.024f, 0.31f), steel, 1f, false);
            BuildKit.Box("LidRim", box.lid, new Vector3(0f, -0.006f, 0.15f), new Vector3(0.37f, 0.012f, 0.27f), dark, 1f, false);
            BuildKit.Box("Handle", box.lid, new Vector3(0f, 0.04f, 0.15f), new Vector3(0.16f, 0.015f, 0.022f), chrome, 1f, false);
            BuildKit.Box("HandlePostL", box.lid, new Vector3(-0.07f, 0.03f, 0.15f), new Vector3(0.015f, 0.02f, 0.02f), chrome, 1f, false);
            BuildKit.Box("HandlePostR", box.lid, new Vector3(0.07f, 0.03f, 0.15f), new Vector3(0.015f, 0.02f, 0.02f), chrome, 1f, false);
            Texture2D stencil = BuildKit.SignTexture("decal_stencil_tapes");
            if (stencil != null)
            {
                BuildKit.Quad("Stencil", box.lid, new Vector3(0f, 0.0245f, 0.07f), new Vector2(0.3f, 0.1f), 0f, BuildKit.Decal("stencilTapes", stencil, Color.white))
                    .transform.localRotation = Quaternion.Euler(90f, 180f, 0f);
            }
            // Two gas struts hold the lid up; they stretch as it opens (see Draw).
            for (int i = 0; i < 2; i++)
            {
                box.strutBodies[i] = BuildKit.Cylinder("StrutBody", t, Vector3.zero, 0.016f, 0.1f, dark).transform;
                box.strutRods[i] = BuildKit.Cylinder("StrutRod", t, Vector3.zero, 0.007f, 0.1f, chrome).transform;
            }
            // Two latches on the sides hold the lid down; they flip open when the code is right.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                box.latches[i] = new GameObject("LatchPivot").transform;
                box.latches[i].SetParent(t, false);
                box.latches[i].localPosition = new Vector3(side * 0.203f, 0.225f, 0.06f);
                BuildKit.Box("Latch", box.latches[i], new Vector3(side * 0.004f, 0.03f, 0f), new Vector3(0.01f, 0.07f, 0.045f), chrome, 1f, false);
                BuildKit.Box("LatchHook", box.latches[i], new Vector3(side * -0.004f, 0.062f, 0f), new Vector3(0.014f, 0.008f, 0.04f), chrome, 1f, false);
            }
            // The keypad on the front: a recessed panel, nine keys, a little screen and a lamp.
            BuildKit.Box("KeypadPanel", t, new Vector3(0.06f, 0.13f, 0.151f), new Vector3(0.24f, 0.22f, 0.004f), dark, 1f, false);
            GameObject lcd = BuildKit.Quad("Screen", t, new Vector3(-0.12f, 0.2f, 0.152f), new Vector2(0.12f, 0.03f), 0f, TaskKit.Label("lcd_keypad_enter", Color.black, 1.2f));
            box.screen = lcd.GetComponent<Renderer>();
            box.led = TaskKit.Lamp(t, new Vector3(-0.12f, 0.16f, 0.153f), new Vector3(0.016f, 0.016f, 0.006f));
            for (int i = 0; i < 9; i++)
            {
                var key = new GameObject("Key " + i).transform;
                key.SetParent(t, false);
                key.localPosition = new Vector3(0.06f + (i % 3 - 1) * 0.062f, 0.13f + (1 - i / 3) * 0.062f, 0.158f);
                BuildKit.Box("Cap", key, Vector3.zero, new Vector3(0.054f, 0.054f, 0.012f), BuildKit.Plain("keyCap", new Color(0.55f, 0.56f, 0.55f), 0.35f), 1f, false);
                GameObject face = BuildKit.Quad("Face", key, new Vector3(0f, 0f, 0.0065f), new Vector2(0.052f, 0.052f), 0f, TaskKit.Label("key_1", Color.grey));
                box.keyFaces[i] = face.GetComponent<Renderer>();
                GameObject glow = BuildKit.Quad("Glow", key, new Vector3(0f, 0f, 0.0072f), new Vector2(0.056f, 0.056f), 0f, BuildKit.Additive("keyGlow", ProceduralTextures.Ring(64, 0.2f), new Color(0.3f, 1f, 0.4f, 0.9f)));
                box.keyGlows[i] = glow.GetComponent<Renderer>();
                box.keyGlows[i].material = new Material(box.keyGlows[i].sharedMaterial);
                box.keys[i] = key;
            }
            var keySizes = new Vector2[9];
            for (int i = 0; i < 9; i++)
            {
                keySizes[i] = new Vector2(0.056f, 0.056f);
            }
            box.selector = new FocusSelector(t, box.keys, keySizes);
            // The tape inside.
            box.tape = new GameObject("Cassette").transform;
            box.tape.SetParent(t, false);
            box.tape.localPosition = new Vector3(0f, 0.08f, 0f);
            Material plastic = BuildKit.Plain("vhsPlastic", new Color(0.05f, 0.05f, 0.05f), 0.55f);
            BuildKit.Box("Body", box.tape, Vector3.zero, new Vector3(0.187f, 0.025f, 0.103f), plastic, 1f, false);
            BuildKit.Box("Window", box.tape, new Vector3(0f, 0.0128f, -0.012f), new Vector3(0.1f, 0.001f, 0.035f), BuildKit.Plain("vhsWindow", new Color(0.12f, 0.1f, 0.09f), 0.9f), 1f, false);
            BuildKit.Quad("Label", box.tape, new Vector3(0f, 0.0131f, 0.03f), new Vector2(0.15f, 0.05f), 0f, TaskKit.Label("label_tape", new Color(0.9f, 0.85f, 0.6f)))
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var trigger = go.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, 0.15f, 0f);
            trigger.size = new Vector3(0.5f, 0.35f, 0.4f);
            trigger.isTrigger = true;
            box.FocusView = TaskKit.View(t, new Vector3(0.02f, 0.14f, 0.15f), 0.42f, 0.12f);
            box.keypadView = new Pose(box.FocusView.localPosition, box.FocusView.localRotation);
            box.ResetTask();
            return box;
        }

        public void ResetTask()
        {
            StopAllCoroutines();
            opened = false;
            taking = false;
            lidShown = 0f;
            latchShown = 0f;
            FocusView.localPosition = keypadView.position;
            FocusView.localRotation = keypadView.rotation;
            tape.gameObject.SetActive(true);
            Shuffle();
            Show("lcd_keypad_enter");
            Draw();
        }

        private void Shuffle()
        {
            for (int i = 0; i < 9; i++)
            {
                numberOn[i] = i + 1;
            }
            for (int i = 8; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (numberOn[i], numberOn[j]) = (numberOn[j], numberOn[i]);
            }
            for (int i = 0; i < 9; i++)
            {
                pressed[i] = false;
                keyFaces[i].sharedMaterial = TaskKit.Label("key_" + numberOn[i], Color.grey);
            }
            next = 1;
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || taking || director.State.TapeTaken || !director.State.NeedsTape)
            {
                return "";
            }
            return opened ? "[E] Take the tape" : "[E] Open the tape box (keypad)";
        }

        public void Use(PlayerController player)
        {
            if (taking || director.State.TapeTaken || !director.State.NeedsTape)
            {
                return;
            }
            if (opened)
            {
                taking = true;
                StartCoroutine(Take(player));
            }
            else
            {
                player.BeginFocus(this);
            }
        }

        private IEnumerator Take(PlayerController player)
        {
            director.OnTapeTaken();
            // Up out of the open box, then to the eyes.
            yield return TaskKit.Pocket(player, tape, transform.up, 0.36f, Quaternion.Euler(-60f, 0f, 0f));
            taking = false;
        }

        public void FocusUpdate(FocusInput input)
        {
            if (opened)
            {
                selector.Hide();
                return;
            }
            int on = selector.Update(input, director.Player.View, i => true);
            if (input.Pressed && on >= 0 && Time.time >= wrongUntil)
            {
                Press(on);
            }
        }

        /// <summary>A key pressed: the next number lights up, any other buzzes and starts over.</summary>
        private void Press(int key)
        {
            if (pressed[key])
            {
                return;
            }
            if (numberOn[key] == next)
            {
                pressed[key] = true;
                next++;
                director.Audio.Beep(1.2f + next * 0.04f);
                director.Player.MakeNoise(1);
                if (next > 9)
                {
                    StartCoroutine(Open());
                }
            }
            else
            {
                StartCoroutine(Wrong());
            }
        }

        private IEnumerator Wrong()
        {
            wrongUntil = Time.time + 0.6f;
            Show("lcd_keypad_wrong");
            director.Audio.Beep(0.35f);
            director.Player.MakeNoise(2);
            TaskKit.SetLamp(led, new Color(1f, 0.05f, 0.05f));
            yield return new WaitForSeconds(0.6f);
            Shuffle();
            Show("lcd_keypad_enter");
            TaskKit.SetLamp(led, new Color(0.4f, 0.02f, 0.02f));
        }

        private IEnumerator Open()
        {
            opened = true;
            Show("lcd_keypad_open");
            TaskKit.SetLamp(led, new Color(0.1f, 1f, 0.2f));
            yield return new WaitForSeconds(0.25f);
            // The eyes leave the keypad: up and back, to watch the whole box open.
            var overlook = new Vector3(0.04f, 0.78f, 0.36f);
            Quaternion down = Quaternion.LookRotation(new Vector3(0f, 0.1f, -0.03f) - overlook);
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float k = TaskKit.Ease(t / 0.6f);
                FocusView.localPosition = Vector3.Lerp(keypadView.position, overlook, k);
                FocusView.localRotation = Quaternion.Slerp(keypadView.rotation, down, k);
                yield return null;
            }
            // The latches flip open, one, then the other: two heavy clacks.
            for (int i = 0; i < 2; i++)
            {
                director.Audio.PlayAt(GameAudio.Clunk(), transform.position, 0.55f, 1.6f + i * 0.15f);
                director.Player.Jolt(0.35f);
                for (float t = 0f; t < 0.12f; t += Time.deltaTime)
                {
                    latchShown = Mathf.Max(latchShown, (i + t / 0.12f) / 2f);
                    yield return null;
                }
                latchShown = (i + 1) / 2f;
                yield return new WaitForSeconds(0.18f);
            }
            // The seal lets go: the lid jumps a finger's width with a hiss of air.
            director.Audio.PlayAt(GameAudio.Hiss(), transform.position, 0.6f);
            for (float t = 0f; t < 0.12f; t += Time.deltaTime)
            {
                lidShown = 0.05f * (t / 0.12f);
                yield return null;
            }
            yield return new WaitForSeconds(0.35f);
            // Then up on its struts, slowing to the top, the light inside coming on.
            director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 0.3f, 1.4f);
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                float k = t / 1.1f;
                lidShown = Mathf.Lerp(0.05f, 1f, 1f - (1f - k) * (1f - k) * (1f - k));
                inside.intensity = Mathf.Clamp01(k * 2f) * 1.6f * (Random.value < 0.08f && k < 0.5f ? 0.2f : 1f);
                yield return null;
            }
            lidShown = 1f;
            inside.intensity = 1.6f;
            yield return new WaitForSeconds(0.9f);
            if (director.Player.Focus == (IFocusTask)this)
            {
                director.Player.EndFocus();
            }
        }

        /// <summary>Test helper (capture tour): a wrong key, then 1 to 9 in order.</summary>
        public IEnumerator AutoPlay(PlayerController player, string shotPrefix, System.Func<string, IEnumerator> shot, float pace = 0.15f)
        {
            player.BeginFocus(this);
            yield return new WaitForSeconds(0.5f);
            if (shot != null)
            {
                yield return shot(shotPrefix + "a-keypad");
            }
            yield return selector.Press(this, player.View, System.Array.IndexOf(numberOn, 5));
            yield return new WaitForSeconds(0.2f);
            if (shot != null)
            {
                yield return shot(shotPrefix + "b-keypad-wrong");
            }
            yield return new WaitForSeconds(0.6f);
            for (int n = 1; n <= 9; n++)
            {
                yield return selector.Press(this, player.View, System.Array.IndexOf(numberOn, n));
                yield return new WaitForSeconds(pace);
                if (n == 5 && shot != null)
                {
                    yield return shot(shotPrefix + "c-keypad-half");
                }
            }
            yield return new WaitForSeconds(1.2f);
        }

        public void FocusEnded()
        {
            selector.Hide();
        }

        private void Show(string message)
        {
            screen.sharedMaterial = TaskKit.Label(message, Color.black, 1.2f);
        }

        private void Update()
        {
            Draw();
        }

        private void Draw()
        {
            lid.localRotation = Quaternion.Euler(-100f * lidShown, 0f, 0f);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                latches[i].localRotation = Quaternion.Euler(0f, 0f, side * 115f * TaskKit.Ease(latchShown * 2f - i));
                // Each strut from its foot on the side wall to its pin under the lid: a body and a rod.
                Vector3 foot = new Vector3(side * 0.155f, 0.12f, -0.1f);
                Vector3 pin = transform.InverseTransformPoint(lid.TransformPoint(new Vector3(side * 0.155f, -0.005f, 0.1f)));
                Vector3 along = pin - foot;
                float length = along.magnitude;
                Quaternion turn = Quaternion.FromToRotation(Vector3.up, along);
                const float body = 0.1f;
                strutBodies[i].localPosition = foot + along.normalized * body * 0.5f;
                strutBodies[i].localRotation = turn;
                strutBodies[i].localScale = new Vector3(0.016f, body * 0.5f, 0.016f);
                float rod = Mathf.Max(0.01f, length - body);
                strutRods[i].localPosition = foot + along.normalized * (body + rod * 0.5f);
                strutRods[i].localRotation = turn;
                strutRods[i].localScale = new Vector3(0.007f, rod * 0.5f, 0.007f);
            }
            bool wrong = Time.time < wrongUntil;
            for (int i = 0; i < 9; i++)
            {
                // Pressed keys stay in; the one under the fingers stands out a little.
                bool under = selector != null && selector.Selected == i && director != null && director.Player.Focus == (IFocusTask)this;
                keys[i].localPosition = new Vector3(keys[i].localPosition.x, keys[i].localPosition.y, pressed[i] ? 0.153f : under ? 0.161f : 0.158f);
                keyGlows[i].enabled = pressed[i] || wrong;
                keyGlows[i].material.SetColor("_BaseColor", wrong ? new Color(1f, 0.15f, 0.1f, 0.9f) : new Color(0.3f, 1f, 0.4f, 0.9f));
            }
        }
    }
}
