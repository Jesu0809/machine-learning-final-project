using System;

namespace NightShift.Core
{
    /// <summary>
    /// Tunable numbers of one night. Serializable so the same values can be edited in the
    /// Inspector of the Night scene and of the training arena. All values are initial and will be
    /// balanced in week 4.
    /// </summary>
    [Serializable]
    public class GameSettings
    {
        /// <summary>Real seconds between two monster decisions in play mode (the RL time step).</summary>
        public float DecisionIntervalSeconds = 4f;

        /// <summary>Real seconds of one night (12 AM to 6 AM).</summary>
        public float NightDurationSeconds = 300f;

        /// <summary>Power at the start of the night.</summary>
        public float MaxPower = 100f;

        /// <summary>Power lost per second with everything off.</summary>
        public float BaseDrainPerSecond = 0.12f;

        /// <summary>Extra power lost per second for each closed door.</summary>
        public float DoorDrainPerSecond = 0.25f;

        /// <summary>Extra power lost per second while the camera panel is open.</summary>
        public float CameraDrainPerSecond = 0.1f;

        /// <summary>Seconds after which "time since camera viewed" is clamped to 1.</summary>
        public float CameraNeglectWindowSeconds = 30f;

        /// <summary>Safety limit on decisions per night; a normal night has about 75.</summary>
        public int MaxStepsPerEpisode = 100;
    }
}
