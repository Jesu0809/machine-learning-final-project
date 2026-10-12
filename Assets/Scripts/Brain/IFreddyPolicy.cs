namespace NightShift.Hunt.Brain
{
    /// <summary>
    /// Anything that can decide for Freddy: the trained agent or a baseline. The game, the training
    /// arena and the evaluation runner only talk to this interface, so policies can be swapped.
    /// </summary>
    public interface IFreddyPolicy
    {
        /// <summary>Name used in logs and in results.json ("trained", "random", "scripted").</summary>
        string PolicyName { get; }

        /// <summary>Called after <see cref="HuntState.Reset"/> at the start of every night.</summary>
        void OnNightStarted(HuntState state);

        /// <summary>
        /// Called once per tick, after the player's action. The policy must end up calling
        /// <see cref="HuntState.ApplyFreddyAction"/> exactly once. Baselines do it immediately; the
        /// ML-Agents agent requests a decision and applies it when the Academy steps.
        /// </summary>
        void Decide(HuntState state);

        /// <summary>Called once when the night ends, with the final outcome.</summary>
        void OnNightEnded(HuntState state, HuntOutcome outcome);
    }
}
