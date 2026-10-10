using NightShift.Core;

namespace NightShift.Monster
{
    /// <summary>
    /// Anything that can control the monster: the trained agent or a baseline. The Night scene,
    /// the training arena and the evaluation runner only talk to this interface, so policies can
    /// be swapped without touching the game.
    /// </summary>
    public interface IMonsterPolicy
    {
        /// <summary>Name used in logs and in results.json ("trained", "random", "scripted").</summary>
        string PolicyName { get; }

        /// <summary>Called after <see cref="GameState.Reset"/> at the start of every night.</summary>
        void OnNightStarted(GameState state);

        /// <summary>
        /// Called once per monster tick. The policy must end up calling
        /// <see cref="GameState.ApplyMonsterAction"/> exactly once for this tick. Baselines do it
        /// immediately; the ML-Agents agent requests a decision and applies it in
        /// OnActionReceived when the Academy steps.
        /// </summary>
        void Decide(GameState state);

        /// <summary>Called once when the night ends, with the final outcome.</summary>
        void OnNightEnded(GameState state, NightOutcome outcome);
    }
}
