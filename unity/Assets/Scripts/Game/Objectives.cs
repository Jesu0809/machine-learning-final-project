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
    /// An object of the objectives (breaker box, keycard lockbox, tape). Hold or press E when its
    /// step is the current one; the director decides what happens.
    /// </summary>
    public class ObjectiveItem : MonoBehaviour, IInteractable
    {
        private HuntDirector director;
        private float progress;
        private Renderer lamp;

        public ObjectiveStep Step { get; private set; }
        public float HoldSeconds { get; private set; }
        public float Progress => progress;
        public bool HoldToUse => HoldSeconds > 0f;

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
            Transform t = go.transform;
            switch (step)
            {
                case ObjectiveStep.Breaker:
                    BuildKit.Box("BreakerBox", t, Vector3.zero, new Vector3(0.8f, 1.0f, 0.2f), BuildKit.Surface("breaker", () => ProceduralTextures.Grimy(new Color(0.35f, 0.38f, 0.35f), 1.5f), 0.4f, 0.6f));
                    for (int i = 0; i < 3; i++)
                    {
                        BuildKit.Box("Switch", t, new Vector3(-0.22f + i * 0.22f, 0.1f, 0.11f), new Vector3(0.08f, 0.2f, 0.04f), BuildKit.Plain("black", Color.black), 1f, false);
                    }
                    item.lamp = BuildKit.Box("Lamp", t, new Vector3(0f, 0.38f, 0.11f), new Vector3(0.3f, 0.06f, 0.02f), BuildKit.Glow("objectiveLamp", new Color(1f, 0.1f, 0.05f), 2f), 1f, false).GetComponent<Renderer>();
                    item.lamp.material = new Material(item.lamp.sharedMaterial);
                    break;
                case ObjectiveStep.Keycard:
                    BuildKit.Box("Lockbox", t, Vector3.zero, new Vector3(0.5f, 0.4f, 0.25f), BuildKit.Plain("lockbox", new Color(0.2f, 0.22f, 0.25f), 0.5f, 0.7f));
                    item.lamp = BuildKit.Box("Lamp", t, new Vector3(0f, 0.1f, 0.13f), new Vector3(0.08f, 0.04f, 0.01f), BuildKit.Glow("objectiveLamp", new Color(1f, 0.1f, 0.05f), 2f), 1f, false).GetComponent<Renderer>();
                    item.lamp.material = new Material(item.lamp.sharedMaterial);
                    BuildKit.Box("Card", t, new Vector3(0f, -0.05f, 0.13f), new Vector3(0.2f, 0.12f, 0.01f), BuildKit.Glow("keycard", new Color(0.3f, 0.7f, 1f), 0.8f), 1f, false);
                    break;
                default:
                    BuildKit.Box("Shelf", t, new Vector3(0f, -0.04f, 0.1f), new Vector3(0.7f, 0.04f, 0.45f), BuildKit.Plain("pipe", new Color(0.32f, 0.3f, 0.27f), 0.5f, 0.6f), 1f, false);
                    BuildKit.Box("Tape", t, Vector3.zero, new Vector3(0.2f, 0.03f, 0.12f), BuildKit.Plain("tape", new Color(0.08f, 0.08f, 0.08f), 0.6f), 1f, false);
                    BuildKit.Box("Label", t, new Vector3(0f, 0.016f, 0f), new Vector3(0.14f, 0.002f, 0.06f), BuildKit.Glow("tapeLabel", new Color(0.9f, 0.85f, 0.6f), 0.5f), 1f, false);
                    var trigger = go.AddComponent<SphereCollider>();
                    trigger.radius = 0.35f;
                    trigger.isTrigger = true;
                    break;
            }
            return item;
        }

        public void ResetItem()
        {
            progress = 0f;
            gameObject.SetActive(true);
            SetLamp(false);
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || !director.ObjectiveList.Needs(Step) || director.ObjectiveList.IsDone(Step))
            {
                return "";
            }
            if (director.ObjectiveList.Current != Step)
            {
                return Step == ObjectiveStep.Keycard ? "The lockbox has no power. Restore the breaker first." : "Not yet: " + HuntDirector.Describe(director.ObjectiveList.Current);
            }
            switch (Step)
            {
                case ObjectiveStep.Breaker: return "[Hold E] Insert the fuses and restore the breaker (LOUD)  " + Mathf.RoundToInt(progress / HoldSeconds * 100f) + "%";
                case ObjectiveStep.Keycard: return "[E] Take the keycard";
                default: return "[E] Take the tape";
            }
        }

        public void Use(PlayerController player)
        {
            if (!director.ObjectiveList.Needs(Step) || director.ObjectiveList.IsDone(Step) || director.ObjectiveList.Current != Step)
            {
                return;
            }
            if (HoldToUse)
            {
                progress += Time.deltaTime;
                player.MakeNoise(2);
                if (progress < HoldSeconds)
                {
                    return;
                }
            }
            SetLamp(true);
            director.CompleteObjective(Step, this);
        }

        private void SetLamp(bool on)
        {
            if (lamp != null)
            {
                lamp.material.SetColor("_EmissionColor", (on ? new Color(0.1f, 1f, 0.2f) : new Color(1f, 0.1f, 0.05f)) * 2f);
            }
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
