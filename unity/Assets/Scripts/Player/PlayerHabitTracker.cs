using System;
using System.Collections.Generic;
using NightShift.Core;

namespace NightShift.Player
{
    /// <summary>
    /// Optional, week 4. Stores how much the human player neglects each room across nights
    /// (saved with PlayerPrefs) and gives the monster a starting route bias.
    /// This is a hand-written heuristic, not reinforcement learning, and must be described that
    /// way in the report and on the website.
    /// </summary>
    public class PlayerHabitTracker
    {
        private const string PrefsKey = "NightShift.RoomNeglect";

        // Room index -> average share of the night the room went unwatched.
        private readonly Dictionary<int, float> neglectByRoom = new Dictionary<int, float>();

        /// <summary>Records the camera history of the night that just ended.</summary>
        public void RecordNight(CameraSystem cameras)
        {
            throw new NotImplementedException();
        }

        /// <summary>The door corridor the player watches least, used as the preferred route.</summary>
        public RoomId MostNeglectedDoorRoom()
        {
            throw new NotImplementedException();
        }

        public void Load()
        {
            throw new NotImplementedException();
        }

        public void Save()
        {
            throw new NotImplementedException();
        }
    }
}
