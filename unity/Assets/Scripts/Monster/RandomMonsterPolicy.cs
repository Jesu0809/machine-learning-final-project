using System;
using System.Collections.Generic;
using NightShift.Core;

namespace NightShift.Monster
{
    /// <summary>Baseline: picks a random legal action every tick. Lower bound for evaluation.</summary>
    public class RandomMonsterPolicy : IMonsterPolicy
    {
        private readonly Random random;
        private readonly List<int> legalActions = new List<int>(MonsterActions.Count);

        /// <param name="seed">Fixed seed so evaluation runs are reproducible.</param>
        public RandomMonsterPolicy(int seed)
        {
            random = new Random(seed);
        }

        public string PolicyName => "random";

        public void OnNightStarted(GameState state)
        {
        }

        public void Decide(GameState state)
        {
            state.ApplyMonsterAction(ChooseAction(state));
        }

        public void OnNightEnded(GameState state, NightOutcome outcome)
        {
        }

        /// <summary>Uniform choice among the legal actions, "stay" included.</summary>
        public int ChooseAction(GameState state)
        {
            legalActions.Clear();
            for (int action = 0; action < MonsterActions.Count; action++)
            {
                if (state.IsLegalAction(action))
                {
                    legalActions.Add(action);
                }
            }
            return legalActions[random.Next(legalActions.Count)];
        }
    }
}
