namespace NightShift.Hunt
{
    /// <summary>
    /// Freddy's reward function. See docs/ml-design.md, section 6.
    /// </summary>
    /// <remarks>
    /// The shaping term is potential-based (Ng, Harada and Russell, 1999):
    /// F = γ·Φ(s') − Φ(s) with Φ(s) = −distance(Freddy, player) / diameter, so it speeds up
    /// learning without changing which policy is optimal. It uses the player's real room, which
    /// Freddy never observes: privileged information is allowed in the reward, not in the inputs.
    /// </remarks>
    public static class FreddyRewards
    {
        public const float Catch = 1f;
        public const float Escape = -1f;
        public const float TimeUp = -0.5f;
        public const float StepPenalty = -0.001f;
        public const float FailedSearch = -0.01f;
        /// <summary>Extra penalty for searching a spot already searched since arriving.</summary>
        public const float RepeatedSearch = -0.02f;
        /// <summary>Using "sense" when the player is calm finds nothing.</summary>
        public const float WastedSense = -0.01f;
        /// <summary>
        /// Staying or sensing in the room where the player hides instead of searching: camping a
        /// hiding spot is dull to play against, so he should open the spots or move on.
        /// </summary>
        public const float WaitedOutsideHiding = -0.05f;
        /// <summary>
        /// Walking into a room where he just heard a noise (freddy_v8). Without it, v7 learned to
        /// cut the survivor off at the north wing and ignored most noises: unbeatable for the bot,
        /// but the stealth and the party horns no longer mattered to a person. A small bonus: he
        /// hunts by ear, as the game tells the player he does.
        /// </summary>
        public const float InvestigatedNoise = 0.02f;
        /// <summary>Noise memory (level / 3, fading ×0.8 a tick) above which a noise counts as fresh.</summary>
        public const float FreshNoise = 0.3f;
        public const float ShapingScale = 0.1f;
        public const float Gamma = 0.99f;

        /// <summary>Reward for one action, without the end-of-night reward.</summary>
        public static float ForStep(FreddyStep step)
        {
            float reward = StepPenalty;
            if (step.FailedSearch)
            {
                reward += FailedSearch;
            }
            if (step.RepeatedSearch)
            {
                reward += RepeatedSearch;
            }
            if (step.SenseUsed && !step.SenseFound)
            {
                reward += WastedSense;
            }
            if (step.WaitedOutsideHiding)
            {
                reward += WaitedOutsideHiding;
            }
            if (step.InvestigatedNoise)
            {
                reward += InvestigatedNoise;
            }
            if (step.Outcome == HuntOutcome.InProgress)
            {
                float before = -step.DistanceBefore / (float)HuntMap.Diameter;
                float after = -step.DistanceAfter / (float)HuntMap.Diameter;
                reward += ShapingScale * (Gamma * after - before);
            }
            return reward;
        }

        /// <summary>Reward when the night ends.</summary>
        public static float ForOutcome(HuntOutcome outcome)
        {
            switch (outcome)
            {
                case HuntOutcome.Caught: return Catch;
                case HuntOutcome.Escaped: return Escape;
                case HuntOutcome.TimeUp: return TimeUp;
                default: return 0f;
            }
        }
    }
}
