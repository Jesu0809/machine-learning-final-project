using System;

namespace NightShift.Core
{
    /// <summary>Simulated time of one night, from 12 AM to 6 AM.</summary>
    public class NightClock
    {
        /// <summary>In-game hours in a night (12 AM to 6 AM).</summary>
        public const int HoursPerNight = 6;

        private readonly GameSettings settings;

        public NightClock(GameSettings settings)
        {
            this.settings = settings;
        }

        /// <summary>Simulated seconds since the night started.</summary>
        public float ElapsedSeconds { get; private set; }

        /// <summary>Elapsed time divided by night duration, in [0, 1]. Used as an observation.</summary>
        public float Progress => Math.Min(1f, ElapsedSeconds / settings.NightDurationSeconds);

        /// <summary>True when the guard has survived until 6 AM.</summary>
        public bool IsOver => ElapsedSeconds >= settings.NightDurationSeconds;

        /// <summary>Hour shown on the HUD: 0 means 12 AM, 6 means 6 AM.</summary>
        public int CurrentHour => Math.Min(HoursPerNight, (int)(Progress * HoursPerNight));

        /// <summary>Advances the clock.</summary>
        public void Tick(float deltaSeconds)
        {
            ElapsedSeconds += deltaSeconds;
        }

        /// <summary>Back to 12 AM.</summary>
        public void Reset()
        {
            ElapsedSeconds = 0f;
        }
    }
}
