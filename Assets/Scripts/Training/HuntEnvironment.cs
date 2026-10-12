using NightShift.Hunt.Brain;
using NightShift.Hunt.Survivor;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace NightShift.Hunt.Training
{
    /// <summary>
    /// One headless copy of the hunt for training: a <see cref="HuntState"/>, a
    /// <see cref="SurvivorBot"/> as the player and a <see cref="FreddyAgent"/>. No 3D and no UI.
    /// </summary>
    public class HuntEnvironment : MonoBehaviour
    {
        private FreddyAgent agent;

        public HuntState State { get; private set; }
        public SurvivorBot Bot { get; private set; }

        public int NightsPlayed { get; private set; }
        public int Catches { get; private set; }

        public static HuntEnvironment Create(Transform parent, int index, HuntSettings settings, int seed)
        {
            var go = new GameObject("Hunt " + index);
            go.transform.SetParent(parent, false);

            var environment = go.AddComponent<HuntEnvironment>();
            environment.State = new HuntState(settings, seed);
            environment.Bot = new SurvivorBot(seed + 1);
            environment.agent = FreddyBrain.Add(go, BehaviorType.Default);
            environment.StartNight();
            return environment;
        }

        /// <summary>
        /// One tick: the survivor acts (several times), then Freddy asks for a decision, which is
        /// applied when the Academy steps. A finished night is replaced by a new one.
        /// </summary>
        public void Tick()
        {
            if (State.IsOver)
            {
                NightsPlayed++;
                if (State.Outcome == HuntOutcome.Caught)
                {
                    Catches++;
                }
                StartNight();
            }

            while (!State.IsOver && !State.FreddyTurn)
            {
                Bot.Act(State);
            }
            if (State.IsOver)
            {
                // The player escaped or walked into Freddy: not one of Freddy's actions.
                agent.OnNightEnded(State, State.Outcome);
                return;
            }

            if (State.FreddyAwake)
            {
                agent.Decide(State);
            }
            else
            {
                State.SkipFreddyTurn();
            }
        }

        private void StartNight()
        {
            State.Reset();
            Bot.OnNightStarted(State);
            agent.OnNightStarted(State);
        }
    }
}
