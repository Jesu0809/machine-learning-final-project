using System.Collections.Generic;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>What has to be done tonight before the exit lever works.</summary>
    public enum ObjectiveStep
    {
        /// <summary>One power cell in each of the three sockets, carried one at a time.</summary>
        Cells,
        /// <summary>Night 3 on: the tripped breakers in the storage.</summary>
        Breaker,
        /// <summary>Night 4 on: the keycard from the veranda, swiped in the office.</summary>
        Keycard,
        /// <summary>Night 5: the tape, locked in the keypad box in the exit lobby.</summary>
        Tape,
        /// <summary>The lever at tonight's exit.</summary>
        Exit
    }

    /// <summary>
    /// The objectives of a night, read from the hunt's state (which training simulates too): night
    /// 1 three cells that fit any socket, night 2 cells of the sockets' colors, night 3 the
    /// breakers, night 4 the keycard, night 5 the tape. They can be done in any order; the exit
    /// lever works once all are done.
    /// </summary>
    public class Objectives
    {
        private readonly List<ObjectiveStep> steps = new List<ObjectiveStep>();
        private HuntState state;

        public IReadOnlyList<ObjectiveStep> Steps => steps;

        public void Begin(HuntState hunt)
        {
            state = hunt;
            steps.Clear();
            steps.Add(ObjectiveStep.Cells);
            if (state.NeedsBreaker) steps.Add(ObjectiveStep.Breaker);
            if (state.NeedsCard) steps.Add(ObjectiveStep.Keycard);
            if (state.NeedsTape) steps.Add(ObjectiveStep.Tape);
            steps.Add(ObjectiveStep.Exit);
        }

        public bool Needs(ObjectiveStep step) => steps.Contains(step);

        public bool IsDone(ObjectiveStep step)
        {
            if (state == null || !Needs(step))
            {
                return true;
            }
            switch (step)
            {
                case ObjectiveStep.Cells: return state.CellsInserted >= state.Settings.CellCount;
                case ObjectiveStep.Breaker: return state.BreakerDone;
                case ObjectiveStep.Keycard: return state.CardSwiped;
                case ObjectiveStep.Tape: return state.TapeTaken;
                default: return false;
            }
        }

        public bool ExitUnlocked => state != null && state.ExitUnlocked;

        /// <summary>What is still to do before the exit, in words (the exit panel's prompt).</summary>
        public string Missing()
        {
            var left = new List<string>();
            foreach (ObjectiveStep step in steps)
            {
                if (step != ObjectiveStep.Exit && !IsDone(step))
                {
                    left.Add(HuntDirector.Describe(step));
                }
            }
            return string.Join(", ", left);
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
