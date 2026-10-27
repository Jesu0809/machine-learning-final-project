using UnityEngine;

namespace NightShift.Game
{
    /// <summary>What the pointer did this frame while the player works a panel up close.</summary>
    public struct FocusInput
    {
        /// <summary>Ray from the camera through the pointer.</summary>
        public Ray Ray;
        /// <summary>Pointer position in screen pixels.</summary>
        public Vector2 Screen;
        public bool Pressed;
        public bool Held;
        public bool Released;
    }

    /// <summary>
    /// A panel worked up close with the mouse (the breakers, the card reader, the tape box's
    /// keypad): the camera moves to <see cref="FocusView"/>, the pointer appears, and every frame
    /// the task gets the pointer. Freddy keeps moving meanwhile; there is no timer, only him.
    /// </summary>
    public interface IFocusTask
    {
        /// <summary>Where the eyes go, looking at the panel.</summary>
        Transform FocusView { get; }

        /// <summary>One line telling what to do, shown at the bottom of the screen.</summary>
        string FocusHint { get; }

        void FocusUpdate(FocusInput input);

        /// <summary>The player stepped back (or was caught); the task keeps what was done.</summary>
        void FocusEnded();
    }
}
