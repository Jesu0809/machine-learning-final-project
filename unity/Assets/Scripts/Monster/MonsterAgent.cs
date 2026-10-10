using NightShift.Core;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightShift.Monster
{
    /// <summary>
    /// The monster as an ML-Agents agent trained with PPO. Observations, actions, masking and
    /// rewards follow docs/ml-design.md exactly; update both together.
    /// </summary>
    /// <remarks>
    /// Behavior Parameters in the Inspector:
    /// Behavior Name = "MonsterAgent", Vector Observation Space Size = 15, Stacked Vectors = 1,
    /// Actions: Continuous = 0, Discrete Branches = 1, Branch 0 Size = 6.
    /// No DecisionRequester component: decisions are requested from <see cref="Decide"/>.
    /// The game state is reset by its owner (NightController or EnvironmentInstance), not here.
    /// </remarks>
    public class MonsterAgent : Agent, IMonsterPolicy
    {
        public const int ObservationSize = 15;

        // Action chosen with the keyboard in Heuristic mode, waiting for the next decision.
        private int pendingKeyAction = -1;

        // True once the current night's episode has been closed, so it is never closed twice.
        private bool episodeClosed;

        // True while OnActionReceived applies an action. A night that ends inside that call is
        // closed by OnActionReceived itself, after the step reward has been added.
        private bool applyingAction;

        /// <summary>The night this agent plays.</summary>
        public GameState State { get; private set; }

        /// <summary>Last applied step, for debugging and the UI.</summary>
        public MonsterStep? LastStep { get; private set; }

        public string PolicyName => "trained";

        public override void Initialize()
        {
            // The night decides when an episode ends, not a step counter.
            MaxStep = 0;
        }

        public void OnNightStarted(GameState state)
        {
            State = state;
            episodeClosed = false;
            pendingKeyAction = -1;
            LastStep = null;
        }

        /// <summary>Requests one decision; the action arrives in <see cref="OnActionReceived"/>.</summary>
        public void Decide(GameState state)
        {
            State = state;
            if (!state.IsOver && !episodeClosed)
            {
                RequestDecision();
            }
        }

        /// <summary>Gives the terminal reward and closes the episode. Safe to call more than once.</summary>
        public void OnNightEnded(GameState state, NightOutcome outcome)
        {
            if (!applyingAction)
            {
                CloseEpisode(outcome);
            }
        }

        /// <summary>
        /// Adds the 15 observations in this order: room one-hot (5), left door, right door, power,
        /// night progress, camera neglect per room (5), watched right now (1).
        /// </summary>
        public override void CollectObservations(VectorSensor sensor)
        {
            if (State == null || State.IsOver)
            {
                for (int i = 0; i < ObservationSize; i++)
                {
                    sensor.AddObservation(0f);
                }
                return;
            }

            sensor.AddOneHotObservation((int)State.MonsterRoom, RoomGraph.ObservableRoomCount);
            sensor.AddObservation(State.Doors.IsClosed(DoorSide.Left) ? 1f : 0f);
            sensor.AddObservation(State.Doors.IsClosed(DoorSide.Right) ? 1f : 0f);
            sensor.AddObservation(State.Power.Normalized);
            sensor.AddObservation(State.Clock.Progress);
            for (int room = 0; room < RoomGraph.ObservableRoomCount; room++)
            {
                sensor.AddObservation(State.Cameras.NormalizedNeglect((RoomId)room));
            }
            sensor.AddObservation(State.Cameras.IsWatching(State.MonsterRoom) ? 1f : 0f);
        }

        /// <summary>Disables every action that is not legal from the monster's current room.</summary>
        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (State == null || State.IsOver)
            {
                return;
            }

            for (int action = 0; action < MonsterActions.Count; action++)
            {
                actionMask.SetActionEnabled(0, action, State.IsLegalAction(action));
            }
        }

        /// <summary>Applies the chosen target room to the game and adds the step reward.</summary>
        public override void OnActionReceived(ActionBuffers actions)
        {
            // The night can end between the request and the action (e.g. 6 AM in play mode).
            if (State == null || State.IsOver || episodeClosed)
            {
                return;
            }

            applyingAction = true;
            MonsterStep step;
            try
            {
                step = State.ApplyMonsterAction(actions.DiscreteActions[0]);
            }
            finally
            {
                applyingAction = false;
            }
            LastStep = step;
            AddReward(MonsterRewards.ForStep(step));

            if (State.IsOver)
            {
                CloseEpisode(State.Outcome);
            }
        }

        /// <summary>
        /// Keyboard control for testing the environment: Shift + 0–5 (or numpad 0–5) choose the
        /// target room for the next decision. Shift keeps these keys apart from the guard's camera
        /// keys 1–5. With no key pressed, or an illegal key, the monster stays.
        /// </summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discrete = actionsOut.DiscreteActions;
            bool usable = State != null && !State.IsOver && pendingKeyAction >= 0 && State.IsLegalAction(pendingKeyAction);
            discrete[0] = usable ? pendingKeyAction
                        : State != null && !State.IsOver ? MonsterActions.Stay(State.MonsterRoom)
                        : 0;
            pendingKeyAction = -1;
        }

        private void Update()
        {
            int key = ReadActionKey();
            if (key >= 0)
            {
                pendingKeyAction = key;
            }
        }

        private void CloseEpisode(NightOutcome outcome)
        {
            if (episodeClosed || outcome == NightOutcome.InProgress)
            {
                return;
            }
            episodeClosed = true;

            if (outcome == NightOutcome.StepLimitReached)
            {
                EpisodeInterrupted();
                return;
            }

            AddReward(MonsterRewards.ForOutcome(outcome));
            EndEpisode();
        }

#if ENABLE_INPUT_SYSTEM
        // Listed one by one: in the Key enum Digit0 comes after Digit9, so "Digit0 + n" is wrong.
        private static readonly Key[] DigitKeys = { Key.Digit0, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
        private static readonly Key[] NumpadKeys = { Key.Numpad0, Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5 };
#else
        private static readonly KeyCode[] DigitKeys = { KeyCode.Alpha0, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };
        private static readonly KeyCode[] NumpadKeys = { KeyCode.Keypad0, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5 };
#endif

        private static int ReadActionKey()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return -1;
            }
            bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            for (int action = 0; action < MonsterActions.Count; action++)
            {
                if ((shift && keyboard[DigitKeys[action]].wasPressedThisFrame) || keyboard[NumpadKeys[action]].wasPressedThisFrame)
                {
                    return action;
                }
            }
#else
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            for (int action = 0; action < MonsterActions.Count; action++)
            {
                if ((shift && Input.GetKeyDown(DigitKeys[action])) || Input.GetKeyDown(NumpadKeys[action]))
                {
                    return action;
                }
            }
#endif
            return -1;
        }
    }
}
