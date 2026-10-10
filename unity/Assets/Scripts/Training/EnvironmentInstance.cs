using System;
using NightShift.Core;
using NightShift.Monster;
using NightShift.Player;
using UnityEngine;

namespace NightShift.Training
{
    /// <summary>
    /// One headless copy of the game for training: a <see cref="GameState"/>, a
    /// <see cref="PlayerBot"/> as the guard and a <see cref="MonsterAgent"/>. No UI.
    /// The <see cref="TrainingArena"/> creates many of these and ticks them all.
    /// </summary>
    public class EnvironmentInstance : MonoBehaviour
    {
        [SerializeField] private GameSettings settings = new GameSettings();
        [SerializeField] private MonsterAgent agent;

        public GameState State { get; private set; }
        public PlayerBot Bot { get; private set; }

        /// <summary>Creates the state and bot. <paramref name="seed"/> must differ per instance.</summary>
        public void Initialize(int seed)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// One monster tick of simulated time: advance the state by
        /// <see cref="GameSettings.DecisionIntervalSeconds"/>, let the bot act, then ask the agent
        /// to decide. Starts a new night when the current one ends.
        /// </summary>
        public void Tick()
        {
            throw new NotImplementedException();
        }
    }
}
