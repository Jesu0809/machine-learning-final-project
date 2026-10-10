using System;

namespace NightShift.Core
{
    /// <summary>
    /// The camera panel. Tracks which room the guard is watching and how long ago each room was
    /// last viewed; those times are the monster's "neglect" observations. Only rooms 0–4 have a
    /// camera. After a power outage the panel shuts down for the rest of the night.
    /// </summary>
    public class CameraSystem
    {
        private readonly GameSettings settings;
        private readonly float[] secondsSinceViewed = new float[RoomGraph.ObservableRoomCount];

        public CameraSystem(GameSettings settings)
        {
            this.settings = settings;
            Reset();
        }

        /// <summary>True while the camera panel is open.</summary>
        public bool IsOpen => WatchedRoom.HasValue;

        /// <summary>False after a power outage; the panel cannot be opened again that night.</summary>
        public bool IsAvailable { get; private set; }

        /// <summary>Room shown on the panel, or null when the panel is closed.</summary>
        public RoomId? WatchedRoom { get; private set; }

        /// <summary>Opens the panel on <paramref name="room"/>, or switches to it if already open.</summary>
        /// <returns>False if the panel is unavailable.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="room"/> has no camera (Office).</exception>
        public bool View(RoomId room)
        {
            if ((int)room >= RoomGraph.ObservableRoomCount)
            {
                throw new ArgumentOutOfRangeException(nameof(room), $"{room} has no camera.");
            }
            if (!IsAvailable)
            {
                return false;
            }

            WatchedRoom = room;
            secondsSinceViewed[(int)room] = 0f;
            return true;
        }

        /// <summary>Closes the panel.</summary>
        public void Close()
        {
            WatchedRoom = null;
        }

        /// <summary>Closes the panel and blocks it for the rest of the night (power outage).</summary>
        public void Shutdown()
        {
            Close();
            IsAvailable = false;
        }

        /// <summary>True if the guard is watching <paramref name="room"/> right now.</summary>
        public bool IsWatching(RoomId room)
        {
            return WatchedRoom == room;
        }

        /// <summary>Seconds since <paramref name="room"/> was last on screen.</summary>
        public float SecondsSinceViewed(RoomId room)
        {
            return secondsSinceViewed[(int)room];
        }

        /// <summary>
        /// <see cref="SecondsSinceViewed"/> divided by the neglect window and clamped to [0, 1].
        /// </summary>
        public float NormalizedNeglect(RoomId room)
        {
            float value = SecondsSinceViewed(room) / settings.CameraNeglectWindowSeconds;
            return Math.Min(1f, Math.Max(0f, value));
        }

        /// <summary>Advances the "time since viewed" counters. The watched room stays at zero.</summary>
        public void Tick(float deltaSeconds)
        {
            for (int room = 0; room < secondsSinceViewed.Length; room++)
            {
                secondsSinceViewed[room] = WatchedRoom == (RoomId)room ? 0f : secondsSinceViewed[room] + deltaSeconds;
            }
        }

        /// <summary>Closes the panel, makes it available, and treats every room as just viewed.</summary>
        public void Reset()
        {
            WatchedRoom = null;
            IsAvailable = true;
            Array.Clear(secondsSinceViewed, 0, secondsSinceViewed.Length);
        }
    }
}
