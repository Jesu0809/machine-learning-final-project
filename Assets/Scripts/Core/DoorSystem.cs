namespace NightShift.Core
{
    /// <summary>The left and right office doors.</summary>
    public class DoorSystem
    {
        private bool leftClosed;
        private bool rightClosed;

        /// <summary>True after a power outage: doors are forced open and ignore input.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>Number of doors currently closed (0–2), used for power drain.</summary>
        public int ClosedCount => (leftClosed ? 1 : 0) + (rightClosed ? 1 : 0);

        /// <summary>Whether the given door is closed.</summary>
        public bool IsClosed(DoorSide side)
        {
            return side == DoorSide.Left ? leftClosed : rightClosed;
        }

        /// <summary>Closes or opens a door. Does nothing while <see cref="IsLocked"/>.</summary>
        /// <returns>True if the door changed state.</returns>
        public bool SetClosed(DoorSide side, bool closed)
        {
            if (IsLocked || IsClosed(side) == closed)
            {
                return false;
            }

            if (side == DoorSide.Left)
            {
                leftClosed = closed;
            }
            else
            {
                rightClosed = closed;
            }
            return true;
        }

        /// <summary>Flips a door between open and closed (UI button).</summary>
        /// <returns>True if the door changed state.</returns>
        public bool Toggle(DoorSide side)
        {
            return SetClosed(side, !IsClosed(side));
        }

        /// <summary>Opens both doors and locks them. Called when power runs out.</summary>
        public void ForceOpenAndLock()
        {
            leftClosed = false;
            rightClosed = false;
            IsLocked = true;
        }

        /// <summary>Opens both doors and unlocks them for a new night.</summary>
        public void Reset()
        {
            leftClosed = false;
            rightClosed = false;
            IsLocked = false;
        }
    }
}
