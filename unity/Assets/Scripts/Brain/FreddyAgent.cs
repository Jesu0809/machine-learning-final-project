using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

namespace NightShift.Hunt.Brain
{
    /// <summary>
    /// Freddy's brain as an ML-Agents agent trained with PPO. Observations, actions, masking and
    /// rewards follow docs/ml-design.md exactly; update both together.
    /// </summary>
    /// <remarks>
    /// Behavior Parameters (set in code by <see cref="FreddyBrain"/>): Behavior Name = "Freddy",
    /// 79 vector observations, one discrete branch of 18 actions (see FreddyObservations and FreddyActions). No DecisionRequester: decisions
    /// are requested from <see cref="Decide"/>. The hunt is reset by its owner, not here.
    /// </remarks>
    public class FreddyAgent : Agent, IFreddyPolicy
    {
        private readonly float[] observations = new float[FreddyObservations.Size];

        // True once the current night's episode has been closed, so it is never closed twice.
        private bool episodeClosed;

        // True while OnActionReceived applies an action; a night that ends inside that call is
        // closed there, after the step reward has been added.
        private bool applyingAction;

        public HuntState State { get; private set; }

        /// <summary>Last applied step, for the AI inspector.</summary>
        public FreddyStep? LastStep { get; private set; }

        public string PolicyName => "trained";

        /// <summary>Name of the model in Behavior Parameters, or "" when none.</summary>
        public string ModelName
        {
            get
            {
                var parameters = GetComponent<BehaviorParameters>();
                return parameters != null && parameters.Model != null ? parameters.Model.name : "";
            }
        }

        public override void Initialize()
        {
            // The night decides when an episode ends, not a step counter.
            MaxStep = 0;
        }

        public void OnNightStarted(HuntState state)
        {
            State = state;
            episodeClosed = false;
            LastStep = null;
        }

        /// <summary>Requests one decision; the action arrives in <see cref="OnActionReceived"/>.</summary>
        public void Decide(HuntState state)
        {
            State = state;
            if (!state.IsOver && !episodeClosed)
            {
                RequestDecision();
            }
        }

        /// <summary>Gives the terminal reward and closes the episode. Safe to call more than once.</summary>
        public void OnNightEnded(HuntState state, HuntOutcome outcome)
        {
            if (!applyingAction)
            {
                CloseEpisode(outcome);
            }
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (State == null)
            {
                System.Array.Clear(observations, 0, observations.Length);
            }
            else
            {
                FreddyObservations.Write(State, observations);
            }
            sensor.AddObservation(observations);
        }

        /// <summary>Disables moves to rooms that are not connected and searches of spots that do not exist.</summary>
        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (State == null || State.IsOver)
            {
                return;
            }
            for (int action = 0; action < FreddyActions.Count; action++)
            {
                actionMask.SetActionEnabled(0, action, State.IsLegalFreddyAction(action));
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            // The night can end between the request and the action.
            if (State == null || State.IsOver || episodeClosed)
            {
                return;
            }

            applyingAction = true;
            FreddyStep step;
            try
            {
                step = State.ApplyFreddyAction(actions.DiscreteActions[0]);
            }
            finally
            {
                applyingAction = false;
            }
            LastStep = step;
            AddReward(FreddyRewards.ForStep(step));

            if (State.IsOver)
            {
                CloseEpisode(State.Outcome);
            }
        }

        /// <summary>Without a model, Freddy just stays and listens.</summary>
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var discrete = actionsOut.DiscreteActions;
            discrete[0] = State != null ? FreddyActions.GoTo(State.FreddyRoom) : 0;
        }

        private void CloseEpisode(HuntOutcome outcome)
        {
            if (episodeClosed || outcome == HuntOutcome.InProgress)
            {
                return;
            }
            episodeClosed = true;
            AddReward(FreddyRewards.ForOutcome(outcome));
            EndEpisode();
        }
    }
}
