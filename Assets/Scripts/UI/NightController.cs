using System;
using NightShift.Core;
using NightShift.Monster;
using UnityEngine;

namespace NightShift.UI
{
    /// <summary>Which monster the Night scene uses.</summary>
    public enum MonsterKind
    {
        Random,
        Scripted,
        Trained
    }

    /// <summary>
    /// Runs the playable night: owns the <see cref="GameState"/>, advances it in real time and asks
    /// the monster for a decision every <see cref="GameSettings.DecisionIntervalSeconds"/>.
    /// UI components read the state from here and send the player's input back.
    /// </summary>
    public class NightController : MonoBehaviour
    {
        [SerializeField] private GameSettings settings = new GameSettings();
        [SerializeField] private MonsterKind monsterKind = MonsterKind.Random;

        [Tooltip("Required when Monster Kind is Trained. Behavior Type must be Inference Only.")]
        [SerializeField] private MonsterAgent trainedMonster;

        /// <summary>The current night. Created in Awake.</summary>
        public GameState State { get; private set; }

        /// <summary>Raised when the night ends, so the UI can show the jumpscare or the 6 AM screen.</summary>
        public event Action<NightOutcome> NightEnded;

        /// <summary>Starts a new night with the selected monster.</summary>
        public void StartNight()
        {
            throw new NotImplementedException();
        }
    }
}
