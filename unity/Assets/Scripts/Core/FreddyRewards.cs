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
        /// A step toward the freshest noise he heard, or into its room (freddy_v8 rewarded only
        /// walking into the room, +0.02; freddy_v10 rewards every step toward it, +0.05). Without
        /// it, v7 learned to cut the survivor off at the north wing and ignored most noises:
        /// unbeatable for the bot, but the stealth and the party horns no longer mattered to a
        /// person. He hunts by ear, as the game tells the player he does.
        /// </summary>
        public const float InvestigatedNoise = 0.05f;

        /// <summary>
        /// Walking away from (or past) a fresh noise (freddy_v10). v9 still learned that a room no
        /// objective sends the survivor to (the Dining Hall) is never worth checking, and ignored a
        /// noise there from the next room: for a person who goes there, a bear that will not come.
        /// −0.03 in v10–v14, −0.05 since freddy_v15 (v14 walked its loop past fresh noises). Since
        /// freddy_v17 standing still also counts (v16 waited at the door of the Office, a dead end).
        /// Since freddy_v18 a recent sighting or sense counts like a noise, and so does standing still
        /// with the clue in his own room and a spot there still shut (v17 camped a locker). Since
        /// freddy_v19, staying a second time in a row where any trace of the guard is left counts too,
        /// spots opened or not (v18 opened a locker, the guard held their breath, and he stood by it).
        /// </summary>
        public const float IgnoredNoise = -0.05f;

        /// <summary>With nothing fresh to go on, walking straight back into the room he just left (freddy_v11).</summary>
        public const float Backtrack = -0.03f;

        /// <summary>
        /// Opening a hiding spot in the room a fresh clue points to (a noise ≥ 0.3, or a sense or
        /// sighting in the last 20 decisions), not a spot already opened (freddy_v15). It replaces the
        /// patrol bonus of v11–v14 (+0.04/0.08 for walking into a long-unchecked room) and the cost of
        /// standing still (v13–v14, −0.02): with them, freddy_v14 walked a loop around the building,
        /// heard the player twenty times in a night and opened no spot; v13 gave up searching for them.
        /// </summary>
        public const float SearchedClue = 0.05f;

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
            if (step.IgnoredNoise)
            {
                reward += IgnoredNoise;
            }
            if (step.Backtracked)
            {
                reward += Backtrack;
            }
            if (step.SearchedClue)
            {
                reward += SearchedClue;
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
