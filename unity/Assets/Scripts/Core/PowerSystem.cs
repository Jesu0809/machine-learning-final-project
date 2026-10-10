using System;

namespace NightShift.Core
{
    /// <summary>
    /// The guard's limited power. Drains slowly over time and faster while doors are closed or
    /// the camera panel is open. When it reaches zero the doors open and cannot be closed again.
    /// </summary>
    public class PowerSystem
    {
        private readonly GameSettings settings;

        public PowerSystem(GameSettings settings)
        {
            this.settings = settings;
            Reset();
        }

        /// <summary>Current power, from 0 to <see cref="GameSettings.MaxPower"/>.</summary>
        public float Power { get; private set; }

        /// <summary>Power scaled to [0, 1], used as an observation.</summary>
        public float Normalized => Power / settings.MaxPower;

        /// <summary>True once power has reached zero.</summary>
        public bool IsDepleted => Power <= 0f;

        /// <summary>Refills the power for a new night.</summary>
        public void Reset()
        {
            Power = settings.MaxPower;
        }

        /// <summary>Power lost per second with the given usage.</summary>
        public float DrainPerSecond(int closedDoors, bool cameraOpen)
        {
            return settings.BaseDrainPerSecond
                   + closedDoors * settings.DoorDrainPerSecond
                   + (cameraOpen ? settings.CameraDrainPerSecond : 0f);
        }

        /// <summary>Drains power for <paramref name="deltaSeconds"/> of simulated time.</summary>
        /// <param name="deltaSeconds">Simulated seconds since the last tick.</param>
        /// <param name="closedDoors">Number of doors currently closed (0–2).</param>
        /// <param name="cameraOpen">Whether the camera panel is open.</param>
        public void Tick(float deltaSeconds, int closedDoors, bool cameraOpen)
        {
            Power = Math.Max(0f, Power - DrainPerSecond(closedDoors, cameraOpen) * deltaSeconds);
        }
    }
}
