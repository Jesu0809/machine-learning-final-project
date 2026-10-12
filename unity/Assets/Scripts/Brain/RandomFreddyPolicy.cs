using System;

namespace NightShift.Hunt.Brain
{
    /// <summary>Lower baseline: a uniformly random legal action every tick.</summary>
    public class RandomFreddyPolicy : IFreddyPolicy
    {
        private readonly Random random;
        private readonly int[] legal = new int[FreddyActions.Count];

        public RandomFreddyPolicy(int seed)
        {
            random = new Random(seed);
        }

        public string PolicyName => "random";

        public void OnNightStarted(HuntState state)
        {
        }

        public void Decide(HuntState state)
        {
            int count = 0;
            for (int action = 0; action < FreddyActions.Count; action++)
            {
                if (state.IsLegalFreddyAction(action))
                {
                    legal[count++] = action;
                }
            }
            state.ApplyFreddyAction(legal[random.Next(count)]);
        }

        public void OnNightEnded(HuntState state, HuntOutcome outcome)
        {
        }
    }
}
