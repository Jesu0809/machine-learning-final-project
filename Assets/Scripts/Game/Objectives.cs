using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>The steps between the player and the exit; each night adds some.</summary>
    public enum ObjectiveStep
    {
        Fuses,
        Breaker,
        Keycard,
        Tape,
        Exit
    }

    /// <summary>
    /// The objectives of a night. Night 1: fuses, then the exit. Night 2 adds the breaker in the
    /// storage room; nights 3–4 the keycard in the office (it needs power); night 5 the tape.
    /// </summary>
    public class Objectives
    {
        private readonly List<ObjectiveStep> steps = new List<ObjectiveStep>();
        private readonly HashSet<ObjectiveStep> done = new HashSet<ObjectiveStep>();

        public IReadOnlyList<ObjectiveStep> Steps => steps;

        public void Begin(int night)
        {
            steps.Clear();
            done.Clear();
            steps.Add(ObjectiveStep.Fuses);
            if (night >= 2) steps.Add(ObjectiveStep.Breaker);
            if (night >= 3) steps.Add(ObjectiveStep.Keycard);
            if (night >= 5) steps.Add(ObjectiveStep.Tape);
            steps.Add(ObjectiveStep.Exit);
        }

        public bool Needs(ObjectiveStep step) => steps.Contains(step);
        public bool IsDone(ObjectiveStep step) => done.Contains(step) || !steps.Contains(step);
        public void Complete(ObjectiveStep step) => done.Add(step);

        /// <summary>True when every step before the exit is done.</summary>
        public bool ExitUnlocked
        {
            get
            {
                foreach (ObjectiveStep step in steps)
                {
                    if (step != ObjectiveStep.Exit && !done.Contains(step))
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>The first step not done yet.</summary>
        public ObjectiveStep Current
        {
            get
            {
                foreach (ObjectiveStep step in steps)
                {
                    if (!done.Contains(step))
                    {
                        return step;
                    }
                }
                return ObjectiveStep.Exit;
            }
        }
    }

    /// <summary>
    /// An object of the objectives, with its own moving parts. The breaker box (hold E): its door
    /// swings open, the six breakers flip up one by one with a click, then the main handle on its
    /// side is pushed up and the lamp turns green. Like the exit panel, everything is drawn from
    /// the saved progress, so letting go keeps what was done (the handle sags back a little). The
    /// keycard lockbox (E): the glass door pops open and the card comes out into the hand. The
    /// tape (E): picked up off the table and pocketed. The director decides what each one does.
    /// </summary>
    public class ObjectiveItem : MonoBehaviour, IInteractable
    {
        // Breaker progress shares: the door, the six breakers, then the main handle.
        private const float BreakerDoorShare = 0.1f;
        private const float BreakerSwitchShare = 0.1f;
        private const float BreakerHandleStart = BreakerDoorShare + 6 * BreakerSwitchShare;

        private HuntDirector director;
        private float progress;
        private Renderer lamp;
        private Transform door;
        private Transform[] switches;
        private bool[] flipped;
        private Transform handle;
        private Transform card;
        private Transform tape;
        private ParticleSystem sparks;
        private float lastUsed = -10f;
        private float resumeKick;
        private float handleShown;
        private float doorShown;
        private bool taken;

        public ObjectiveStep Step { get; private set; }
        public float HoldSeconds { get; private set; }
        public float Progress => progress;
        public bool HoldToUse => HoldSeconds > 0f;

        private bool Held => Time.time - lastUsed < 0.15f;

        public static ObjectiveItem Create(HuntDirector director, ObjectiveStep step, Transform parent, Vector3 position, float facing, float holdSeconds)
        {
            var go = new GameObject(step + "Item");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, facing, 0f);
            var item = go.AddComponent<ObjectiveItem>();
            item.director = director;
            item.Step = step;
            item.HoldSeconds = holdSeconds;
            switch (step)
            {
                case ObjectiveStep.Breaker: item.BuildBreaker(); break;
                case ObjectiveStep.Keycard: item.BuildLockbox(); break;
                default: item.BuildTape(); break;
            }
            item.Draw();
            return item;
        }

        // ------------------------------------------------------------------ the objects

        private static Material Label(string name, Color fallback)
        {
            Texture2D texture = BuildKit.SignTexture(name);
            return texture != null ? BuildKit.Picture("label:" + name, texture) : BuildKit.Plain("label:" + name, fallback, 0.3f);
        }

        private Renderer Lamp(Transform parent, Vector3 position, Vector3 size)
        {
            Renderer renderer = BuildKit.Box("Lamp", parent, position, size, BuildKit.Glow("objectiveLamp", new Color(1f, 0.1f, 0.05f), 2f), 1f, false).GetComponent<Renderer>();
            renderer.material = new Material(renderer.sharedMaterial);
            return renderer;
        }

        private void BuildBreaker()
        {
            Transform t = transform;
            Material steel = BuildKit.Pbr("metal_worn", new Color(0.5f, 0.55f, 0.52f)) ?? BuildKit.Plain("breakerSteel", new Color(0.33f, 0.36f, 0.34f), 0.4f, 0.6f);
            Material doorSteel = BuildKit.Pbr("metal_green", new Color(0.75f, 0.8f, 0.78f)) ?? steel;
            Material inside = BuildKit.Plain("breakerInside", new Color(0.05f, 0.05f, 0.055f), 0.3f, 0.5f);
            // Matte: a flat face square to the flashlight would otherwise light up like a mirror.
            Material bakelite = BuildKit.Plain("bakelite", new Color(0.05f, 0.048f, 0.045f), 0.25f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);

            // The housing: what the player aims at.
            BuildKit.Box("BreakerBox", t, Vector3.zero, new Vector3(0.8f, 1.0f, 0.2f), steel, 1f);
            BuildKit.Box("Inside", t, new Vector3(0f, 0f, 0.095f), new Vector3(0.72f, 0.92f, 0.01f), inside, 1f, false);
            // A rim in front of the housing: the breakers sit in it and the door closes over it.
            BuildKit.Box("RimTop", t, new Vector3(0f, 0.485f, 0.135f), new Vector3(0.8f, 0.03f, 0.07f), steel, 1f, false);
            BuildKit.Box("RimBottom", t, new Vector3(0f, -0.485f, 0.135f), new Vector3(0.8f, 0.03f, 0.07f), steel, 1f, false);
            BuildKit.Box("RimLeft", t, new Vector3(-0.385f, 0f, 0.135f), new Vector3(0.03f, 1.0f, 0.07f), steel, 1f, false);
            BuildKit.Box("RimRight", t, new Vector3(0.385f, 0f, 0.135f), new Vector3(0.03f, 1.0f, 0.07f), steel, 1f, false);
            BuildKit.Box("Conduit", t, new Vector3(-0.25f, 0.85f, -0.02f), new Vector3(0.07f, 0.7f, 0.07f), chrome, 1f, false);
            BuildKit.Box("Conduit", t, new Vector3(0.25f, 0.85f, -0.02f), new Vector3(0.07f, 0.7f, 0.07f), chrome, 1f, false);

            // Six breakers in two columns: a black body with a toggle that flips up.
            switches = new Transform[6];
            flipped = new bool[6];
            for (int i = 0; i < 6; i++)
            {
                var place = new Vector3(i < 3 ? -0.13f : 0.13f, 0.2f - (i % 3) * 0.2f, 0.12f);
                BuildKit.Box("Breaker", t, place, new Vector3(0.18f, 0.12f, 0.04f), bakelite, 1f, false);
                BuildKit.Box("BreakerTag", t, place + new Vector3(0f, -0.075f, -0.015f), new Vector3(0.14f, 0.025f, 0.004f), BuildKit.Plain("breakerTag", new Color(0.5f, 0.48f, 0.42f), 0.1f), 1f, false);
                var pivot = new GameObject("Toggle").transform;
                pivot.SetParent(t, false);
                pivot.localPosition = place + new Vector3(0f, 0f, 0.02f);
                BuildKit.Box("Lever", pivot, new Vector3(0f, 0.035f, 0f), new Vector3(0.04f, 0.07f, 0.03f), BuildKit.Plain("toggleRed", new Color(0.55f, 0.06f, 0.05f), 0.5f), 1f, false);
                switches[i] = pivot;
            }

            // The door, hinged on its left edge, with the warning plate.
            door = new GameObject("DoorHinge").transform;
            door.SetParent(t, false);
            door.localPosition = new Vector3(-0.4f, 0f, 0.17f);
            BuildKit.Box("Door", door, new Vector3(0.39f, 0f, 0.012f), new Vector3(0.78f, 0.98f, 0.022f), doorSteel, 1f, false);
            BuildKit.Quad("Warning", door, new Vector3(0.39f, 0.15f, 0.0245f), new Vector2(0.3f, 0.375f), 0f, Label("label_danger", new Color(0.9f, 0.75f, 0.1f)));
            BuildKit.Box("Latch", door, new Vector3(0.72f, -0.05f, 0.04f), new Vector3(0.035f, 0.16f, 0.035f), chrome, 1f, false);

            // The main handle on the right side: down is off, up is on.
            BuildKit.Cylinder("HandleHub", t, new Vector3(0.43f, -0.1f, 0.04f), 0.09f, 0.06f, chrome).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            handle = new GameObject("HandlePivot").transform;
            handle.SetParent(t, false);
            handle.localPosition = new Vector3(0.47f, -0.1f, 0.04f);
            BuildKit.Box("HandleArm", handle, new Vector3(0f, 0.14f, 0f), new Vector3(0.035f, 0.28f, 0.035f), chrome, 1f, false);
            BuildKit.Box("HandleGrip", handle, new Vector3(0f, 0.3f, 0f), new Vector3(0.05f, 0.09f, 0.06f), BuildKit.Plain("leverGrip", new Color(0.6f, 0.05f, 0.04f), 0.4f), 1f, false);

            lamp = Lamp(t, new Vector3(0f, 0.54f, 0.04f), new Vector3(0.22f, 0.06f, 0.06f));
            var sparksRoot = new GameObject("BreakerSparks");
            sparksRoot.transform.SetParent(t, false);
            LevelBuilder.Sparks(sparksRoot.transform);
            sparks = sparksRoot.GetComponentInChildren<ParticleSystem>();
            sparks.Stop();
        }

        private void BuildLockbox()
        {
            Transform t = transform;
            Material steel = BuildKit.Pbr("metal_worn", new Color(0.35f, 0.38f, 0.42f)) ?? BuildKit.Plain("lockbox", new Color(0.2f, 0.22f, 0.25f), 0.5f, 0.7f);
            Material inside = BuildKit.Plain("lockboxInside", new Color(0.06f, 0.06f, 0.07f), 0.3f, 0.4f);
            Material chrome = BuildKit.Plain("chrome", new Color(0.7f, 0.7f, 0.72f), 0.8f, 1f);
            Material frame = BuildKit.Plain("lockboxFrame", new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.6f);

            // A hollow steel box (0.5 x 0.4 x 0.25), dark inside, open at the front.
            BuildKit.Box("Back", t, new Vector3(0f, 0f, -0.115f), new Vector3(0.5f, 0.4f, 0.02f), steel);
            BuildKit.Box("Left", t, new Vector3(-0.24f, 0f, 0f), new Vector3(0.02f, 0.4f, 0.25f), steel);
            BuildKit.Box("Right", t, new Vector3(0.24f, 0f, 0f), new Vector3(0.02f, 0.4f, 0.25f), steel);
            BuildKit.Box("Top", t, new Vector3(0f, 0.19f, 0f), new Vector3(0.5f, 0.02f, 0.25f), steel);
            BuildKit.Box("Bottom", t, new Vector3(0f, -0.19f, 0f), new Vector3(0.5f, 0.02f, 0.25f), steel);
            BuildKit.Box("Inside", t, new Vector3(-0.06f, 0f, -0.1f), new Vector3(0.34f, 0.36f, 0.01f), inside, 1f, false);
            // The keypad side: a fixed panel with the lamp and nine keys.
            BuildKit.Box("KeypadPanel", t, new Vector3(0.17f, 0f, 0.115f), new Vector3(0.12f, 0.36f, 0.02f), steel);
            lamp = Lamp(t, new Vector3(0.17f, 0.12f, 0.127f), new Vector3(0.05f, 0.025f, 0.01f));
            for (int key = 0; key < 9; key++)
            {
                BuildKit.Box("Key", t, new Vector3(0.145f + (key % 3) * 0.025f, 0.04f - (key / 3) * 0.03f, 0.127f), new Vector3(0.018f, 0.02f, 0.008f), chrome, 1f, false);
            }

            // The card hangs on a hook, behind the glass.
            BuildKit.Cylinder("Hook", t, new Vector3(-0.06f, 0.1f, -0.06f), 0.012f, 0.08f, chrome).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            card = new GameObject("Keycard").transform;
            card.SetParent(t, false);
            BuildKit.Box("CardBody", card, Vector3.zero, new Vector3(0.0856f * 1.4f, 0.054f * 1.4f, 0.004f), BuildKit.Plain("cardWhite", new Color(0.75f, 0.76f, 0.78f), 0.25f), 1f, false);
            BuildKit.Quad("CardFace", card, new Vector3(0f, 0f, 0.0025f), new Vector2(0.0856f * 1.4f, 0.054f * 1.4f), 0f, Label("label_keycard", new Color(0.3f, 0.5f, 0.9f)));
            ResetCard();

            // The glass door, hinged on the left.
            door = new GameObject("DoorHinge").transform;
            door.SetParent(t, false);
            door.localPosition = new Vector3(-0.23f, 0f, 0.12f);
            BuildKit.Box("FrameTop", door, new Vector3(0.17f, 0.1675f, 0f), new Vector3(0.34f, 0.025f, 0.02f), frame, 1f, false);
            BuildKit.Box("FrameBottom", door, new Vector3(0.17f, -0.1675f, 0f), new Vector3(0.34f, 0.025f, 0.02f), frame, 1f, false);
            BuildKit.Box("FrameLeft", door, new Vector3(0.0125f, 0f, 0f), new Vector3(0.025f, 0.36f, 0.02f), frame, 1f, false);
            BuildKit.Box("FrameRight", door, new Vector3(0.3275f, 0f, 0f), new Vector3(0.025f, 0.36f, 0.02f), frame, 1f, false);
            BuildKit.Quad("Glass", door, new Vector3(0.17f, 0f, 0f), new Vector2(0.3f, 0.31f), 0f,
                          BuildKit.Decal("lockboxGlass", Texture2D.whiteTexture, new Color(0.6f, 0.72f, 0.8f, 0.12f)));
            BuildKit.Box("Latch", door, new Vector3(0.33f, 0f, 0.015f), new Vector3(0.02f, 0.05f, 0.015f), chrome, 1f, false);
        }

        private void BuildTape()
        {
            // A VHS cassette lying on the table, its label up.
            tape = new GameObject("Cassette").transform;
            tape.SetParent(transform, false);
            Material plastic = BuildKit.Plain("vhsPlastic", new Color(0.05f, 0.05f, 0.05f), 0.55f);
            Material window = BuildKit.Plain("vhsWindow", new Color(0.12f, 0.1f, 0.09f), 0.9f);
            BuildKit.Box("Body", tape, Vector3.zero, new Vector3(0.187f, 0.025f, 0.103f), plastic, 1f, false);
            BuildKit.Box("Window", tape, new Vector3(0f, 0.0128f, -0.012f), new Vector3(0.1f, 0.001f, 0.035f), window, 1f, false);
            for (int side = -1; side <= 1; side += 2)
            {
                BuildKit.Cylinder("Reel", tape, new Vector3(side * 0.03f, 0.0129f, -0.012f), 0.03f, 0.001f, BuildKit.Plain("vhsReel", new Color(0.35f, 0.32f, 0.3f), 0.6f));
            }
            GameObject label = BuildKit.Quad("Label", tape, new Vector3(0f, 0.0131f, 0.03f), new Vector2(0.15f, 0.05f), 0f, Label("label_tape", new Color(0.9f, 0.85f, 0.6f)));
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.radius = 0.35f;
            trigger.isTrigger = true;
        }

        // ------------------------------------------------------------------ state

        public void ResetItem()
        {
            progress = 0f;
            taken = false;
            handleShown = 0f;
            doorShown = 0f;
            StopAllCoroutines();
            gameObject.SetActive(true);
            if (flipped != null)
            {
                for (int i = 0; i < flipped.Length; i++)
                {
                    flipped[i] = false;
                }
            }
            ResetCard();
            if (tape != null)
            {
                tape.localPosition = Vector3.zero;
                tape.localRotation = Quaternion.identity;
                tape.localScale = Vector3.one;
                tape.gameObject.SetActive(true);
            }
            Draw();
        }

        private void ResetCard()
        {
            if (card == null)
            {
                return;
            }
            card.gameObject.SetActive(true);
            card.localPosition = new Vector3(-0.06f, 0.035f, -0.06f);
            card.localRotation = Quaternion.Euler(0f, 0f, 4f);
            card.localScale = Vector3.one;
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || !director.ObjectiveList.Needs(Step) || director.ObjectiveList.IsDone(Step) || taken)
            {
                return "";
            }
            if (director.ObjectiveList.Current != Step)
            {
                return Step == ObjectiveStep.Keycard ? "The lockbox has no power. Restore the breaker first." : "Not yet: " + HuntDirector.Describe(director.ObjectiveList.Current);
            }
            switch (Step)
            {
                case ObjectiveStep.Breaker:
                    string verb = progress <= 0f ? "Reset the breakers" : Fraction < BreakerHandleStart ? "Keep resetting the breakers" : "Push the main handle up";
                    return "[Hold E] " + verb + " (LOUD)  " + Mathf.RoundToInt(Fraction * 100f) + "%";
                case ObjectiveStep.Keycard: return "[E] Take the keycard";
                default: return "[E] Take the tape";
            }
        }

        private float Fraction => HoldSeconds > 0f ? Mathf.Clamp01(progress / HoldSeconds) : 0f;

        public void Use(PlayerController player)
        {
            if (taken || !director.ObjectiveList.Needs(Step) || director.ObjectiveList.IsDone(Step) || director.ObjectiveList.Current != Step)
            {
                return;
            }
            if (HoldToUse)
            {
                if (!Held)
                {
                    resumeKick = 1f;
                }
                lastUsed = Time.time;
                player.LookToward(transform.position, 4f);
                progress += Time.deltaTime;
                player.MakeNoise(2);
                if (progress < HoldSeconds)
                {
                    return;
                }
                progress = HoldSeconds;
                sparks.transform.localPosition = handle.localPosition + new Vector3(0f, 0.1f, 0.05f);
                sparks.Emit(40);
            }
            taken = true;
            if (Step == ObjectiveStep.Keycard)
            {
                StartCoroutine(TakeCard(player));
            }
            else if (Step == ObjectiveStep.Tape)
            {
                StartCoroutine(Pocket(player, tape, 0.6f));
            }
            director.CompleteObjective(Step, this);
        }

        // ------------------------------------------------------------------ animation

        private void Update()
        {
            if (Step != ObjectiveStep.Breaker)
            {
                if (Step == ObjectiveStep.Keycard && !taken)
                {
                    // Powered and waiting: the lamp pulses amber; without power it is a dull red.
                    bool powered = director.ObjectiveList.Needs(Step) && director.ObjectiveList.Current == Step;
                    float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 4f);
                    SetLampColor(powered ? new Color(1f, 0.55f, 0f) * pulse : new Color(0.35f, 0.02f, 0.01f));
                }
                return;
            }
            resumeKick = Mathf.MoveTowards(resumeKick, 0f, Time.deltaTime * 2.5f);
            float handleTarget = Mathf.Clamp01((Fraction - BreakerHandleStart) / (1f - BreakerHandleStart));
            handleShown = Mathf.MoveTowards(handleShown, Held || taken ? handleTarget : handleTarget * 0.8f, Time.deltaTime * 3f);
            Draw();
        }

        /// <summary>Puts the breaker's moving parts where the progress says.</summary>
        private void Draw()
        {
            if (Step != ObjectiveStep.Breaker)
            {
                if (Step == ObjectiveStep.Keycard)
                {
                    door.localRotation = Quaternion.Euler(0f, -105f * Ease(doorShown), 0f);
                    if (taken)
                    {
                        SetLampColor(new Color(0.1f, 1f, 0.2f));
                    }
                }
                return;
            }
            float p = Fraction;
            float jiggle = Mathf.Sin(Time.time * 38f) * 2.5f * resumeKick;
            door.localRotation = Quaternion.Euler(0f, -110f * Ease(p / BreakerDoorShare) + (p > 0f ? jiggle : 0f), 0f);
            for (int i = 0; i < switches.Length; i++)
            {
                float t = (p - BreakerDoorShare - i * BreakerSwitchShare) / BreakerSwitchShare;
                // The finger finds the toggle, pushes against the spring, and it snaps over.
                float push = Mathf.Clamp01((t - 0.3f) / 0.5f);
                float angle = push < 1f ? Mathf.Lerp(35f, 15f, push) : -35f;
                switches[i].localRotation = Quaternion.Euler(angle, 0f, 0f);
                if (push >= 1f && !flipped[i])
                {
                    flipped[i] = true;
                    director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 0.25f, 2.4f);
                    if (i == switches.Length - 1)
                    {
                        sparks.transform.localPosition = switches[i].localPosition + new Vector3(0f, 0f, 0.05f);
                        sparks.Emit(8);
                    }
                }
            }
            // Down (off) to up (on); it sags back when let go before the end.
            handle.localRotation = Quaternion.Euler(Mathf.Lerp(150f, 15f, Ease(handleShown)), 0f, 0f);
            bool blink = Mathf.Repeat(Time.time * 3f, 1f) < 0.5f;
            SetLampColor(taken ? new Color(0.1f, 1f, 0.2f) : p > 0f ? (blink ? new Color(1f, 0.55f, 0f) : new Color(0.3f, 0.12f, 0f)) : new Color(1f, 0.1f, 0.05f));
        }

        /// <summary>The lockbox: the door pops open, the card slides off its hook and into the hand.</summary>
        private System.Collections.IEnumerator TakeCard(PlayerController player)
        {
            director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 0.35f, 1.9f);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                doorShown = t / 0.3f;
                Draw();
                yield return null;
            }
            doorShown = 1f;
            Draw();
            Vector3 hook = card.localPosition;
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                card.localPosition = hook + new Vector3(0f, 0.01f, 0.2f) * Ease(t / 0.2f);
                yield return null;
            }
            yield return Pocket(player, card, 0.45f);
        }

        /// <summary>Something small goes from where it is into the player's hand, then away.</summary>
        private System.Collections.IEnumerator Pocket(PlayerController player, Transform thing, float seconds)
        {
            Vector3 start = thing.position;
            Quaternion startRotation = thing.rotation;
            Vector3 startScale = thing.localScale;
            Renderer[] renderers = thing.GetComponentsInChildren<Renderer>();
            var shade = new MaterialPropertyBlock();
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Ease(t / seconds);
                // Right under the flashlight it would turn white: darken it as it comes close.
                foreach (Renderer renderer in renderers)
                {
                    shade.SetColor("_BaseColor", renderer.sharedMaterial.GetColor("_BaseColor") * Mathf.Lerp(1f, 0.3f, k));
                    renderer.SetPropertyBlock(shade);
                }
                Transform view = player.View.transform;
                // Held up in front of the eyes for a moment, then down and away.
                // Held at arm's length: closer, the flashlight would burn it white.
                Vector3 look = view.TransformPoint(new Vector3(0.06f, -0.12f, 0.55f));
                Vector3 hand = view.TransformPoint(new Vector3(0.22f, -0.32f, 0.3f));
                Vector3 target = k < 0.7f ? look : Vector3.Lerp(look, hand, (k - 0.7f) / 0.3f);
                thing.position = Vector3.Lerp(start, target, Mathf.Min(1f, k / 0.7f)) + Vector3.up * Mathf.Sin(Mathf.Min(1f, k / 0.7f) * Mathf.PI) * 0.06f;
                // The card's face, or the tape's label, turned toward the eyes.
                Quaternion shown = view.rotation * (thing == tape ? Quaternion.Euler(-60f, 0f, 0f) : Quaternion.Euler(0f, 180f, 0f));
                thing.rotation = Quaternion.Slerp(startRotation, shown, Mathf.Min(1f, k / 0.7f));
                thing.localScale = startScale * Mathf.Lerp(1f, 0.7f, k);
                yield return null;
            }
            foreach (Renderer renderer in renderers)
            {
                renderer.SetPropertyBlock(null);
            }
            thing.gameObject.SetActive(false);
            if (thing == tape)
            {
                gameObject.SetActive(false);
            }
        }

        private void SetLampColor(Color color)
        {
            if (lamp != null)
            {
                lamp.material.SetColor("_EmissionColor", color * 2f);
            }
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>A lab note on the floor: E to read it (the game keeps running).</summary>
    public class NotePickup : MonoBehaviour, IInteractable
    {
        private HuntDirector director;
        public string Text { get; private set; }
        public bool HoldToUse => false;

        public static NotePickup Create(HuntDirector director, Transform parent, Vector3 position, float yaw, string text)
        {
            var go = new GameObject("LabNote");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            BuildKit.Box("Paper", go.transform, Vector3.zero, new Vector3(0.3f, 0.004f, 0.4f), BuildKit.Glow("notePaper", new Color(0.9f, 0.88f, 0.75f), 0.25f), 1f, false);
            var trigger = go.AddComponent<BoxCollider>();
            trigger.size = new Vector3(0.6f, 0.3f, 0.7f);
            trigger.isTrigger = true;
            var note = go.AddComponent<NotePickup>();
            note.director = director;
            note.Text = text;
            return note;
        }

        public string Prompt(PlayerController player) => player.HiddenIn == null ? "[E] Read the note" : "";

        public void Use(PlayerController player)
        {
            director.UI.ShowNote(Text);
            director.Audio.Click();
        }
    }
}
