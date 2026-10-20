using UnityEngine;
using UnityEngine.InputSystem;

namespace NightShift.Game
{
    /// <summary>
    /// Every game input in one place, for keyboard and mouse and for a gamepad (Xbox layout).
    /// <code>
    ///   Move        WASD            left stick
    ///   Look        mouse           right stick
    ///   Sprint      Shift           left stick click (L3)
    ///   Crouch      Ctrl / C (hold) B (toggle)
    ///   Use / hide  E               A
    ///   Flashlight  F               Y
    ///   Party horn  G               RB
    ///   Hold breath Space           X
    ///   Inspector   Tab             View
    ///   Pause       Esc             Menu
    ///   Skip        Enter           D-pad down
    /// </code>
    /// The diagram in docs/controls (and on the website) shows the same table.
    /// </summary>
    public static class Controls
    {
        /// <summary>Right stick speed, degrees per second at full tilt.</summary>
        private const float StickLookSpeed = 150f;

        private static bool crouchToggled;
        private static int polledFrame = -1;

        /// <summary>True when the last input came from a gamepad (prompts and pointer lock use it).</summary>
        public static bool UsingGamepad { get; private set; }

        private static Keyboard Keys => Keyboard.current;
        private static Mouse Pointer => Mouse.current;
        private static Gamepad Pad => Gamepad.current;

        /// <summary>Call once per frame before reading anything.</summary>
        public static void Poll()
        {
            if (polledFrame == Time.frameCount)
            {
                return;
            }
            polledFrame = Time.frameCount;
            if (Pad != null && Pad.wasUpdatedThisFrame && PadActive())
            {
                UsingGamepad = true;
            }
            else if ((Keys != null && Keys.anyKey.wasPressedThisFrame) || (Pointer != null && Pointer.delta.ReadValue().sqrMagnitude > 4f))
            {
                UsingGamepad = false;
            }
            if (Pad != null && Pad.buttonEast.wasPressedThisFrame)
            {
                crouchToggled = !crouchToggled;
            }
        }

        private static bool PadActive()
        {
            Gamepad pad = Pad;
            return pad.leftStick.ReadValue().sqrMagnitude > 0.04f || pad.rightStick.ReadValue().sqrMagnitude > 0.04f
                || pad.buttonSouth.isPressed || pad.buttonNorth.isPressed || pad.buttonEast.isPressed || pad.buttonWest.isPressed
                || pad.startButton.isPressed || pad.selectButton.isPressed || pad.rightShoulder.isPressed || pad.dpad.down.isPressed;
        }

        /// <summary>Clears toggles (new night, leaving a hiding spot).</summary>
        public static void ResetToggles() => crouchToggled = false;

        public static Vector2 Move
        {
            get
            {
                Vector2 input = Vector2.zero;
                if (Keys != null)
                {
                    if (Keys.wKey.isPressed) input.y += 1f;
                    if (Keys.sKey.isPressed) input.y -= 1f;
                    if (Keys.dKey.isPressed) input.x += 1f;
                    if (Keys.aKey.isPressed) input.x -= 1f;
                }
                if (Pad != null)
                {
                    Vector2 stick = Pad.leftStick.ReadValue();
                    if (stick.sqrMagnitude > 0.02f)
                    {
                        input += stick;
                    }
                }
                return Vector2.ClampMagnitude(input, 1f);
            }
        }

        /// <summary>Look change this frame in degrees (x = yaw, y = pitch up).</summary>
        public static Vector2 Look
        {
            get
            {
                Vector2 look = Pointer != null ? Pointer.delta.ReadValue() * 0.08f : Vector2.zero;
                if (Pad != null)
                {
                    Vector2 stick = Pad.rightStick.ReadValue();
                    // Squared response: precise near the center, fast at full tilt.
                    look += stick * stick.magnitude * StickLookSpeed * Time.unscaledDeltaTime;
                }
                return look;
            }
        }

        public static bool Sprint => (Keys != null && Keys.leftShiftKey.isPressed) || (Pad != null && Pad.leftStickButton.isPressed);

        public static bool Crouch => (Keys != null && (Keys.leftCtrlKey.isPressed || Keys.cKey.isPressed)) || crouchToggled;

        /// <summary>True only on the frame crouch is pressed (used to start a slide).</summary>
        public static bool CrouchPressed => (Keys != null && (Keys.leftCtrlKey.wasPressedThisFrame || Keys.cKey.wasPressedThisFrame))
                                            || (Pad != null && Pad.buttonEast.wasPressedThisFrame);

        public static bool UsePressed => (Keys != null && Keys.eKey.wasPressedThisFrame) || (Pad != null && Pad.buttonSouth.wasPressedThisFrame);

        public static bool UseHeld => (Keys != null && Keys.eKey.isPressed) || (Pad != null && Pad.buttonSouth.isPressed);

        public static bool FlashlightPressed => (Keys != null && Keys.fKey.wasPressedThisFrame) || (Pad != null && Pad.buttonNorth.wasPressedThisFrame);

        public static bool ThrowPressed => (Keys != null && Keys.gKey.wasPressedThisFrame) || (Pad != null && Pad.rightShoulder.wasPressedThisFrame);

        public static bool HoldBreath => (Keys != null && Keys.spaceKey.isPressed) || (Pad != null && Pad.buttonWest.isPressed);

        public static bool InspectorPressed => (Keys != null && Keys.tabKey.wasPressedThisFrame) || (Pad != null && Pad.selectButton.wasPressedThisFrame);

        public static bool PausePressed => (Keys != null && Keys.escapeKey.wasPressedThisFrame) || (Pad != null && Pad.startButton.wasPressedThisFrame);

        public static bool SkipPressed => (Keys != null && (Keys.enterKey.wasPressedThisFrame || Keys.numpadEnterKey.wasPressedThisFrame))
                                          || (Pad != null && Pad.dpad.down.wasPressedThisFrame);

        /// <summary>Anything that skips a cinematic: Enter, Esc, Space, a click, or A / Menu / D-pad down.</summary>
        public static bool SkipCinematicPressed =>
            SkipPressed
            || (Keys != null && (Keys.escapeKey.wasPressedThisFrame || Keys.spaceKey.wasPressedThisFrame))
            || (Pointer != null && Pointer.leftButton.wasPressedThisFrame)
            || (Pad != null && (Pad.buttonSouth.wasPressedThisFrame || Pad.startButton.wasPressedThisFrame));

        /// <summary>Closes a note: E, Esc, or A / B.</summary>
        public static bool ClosePressed => (Keys != null && (Keys.eKey.wasPressedThisFrame || Keys.escapeKey.wasPressedThisFrame))
                                           || (Pad != null && (Pad.buttonSouth.wasPressedThisFrame || Pad.buttonEast.wasPressedThisFrame));

        /// <summary>Rewrites the keyboard keys of a prompt for the gamepad when one is in use.</summary>
        public static string Localize(string text)
        {
            if (!UsingGamepad || string.IsNullOrEmpty(text))
            {
                return text;
            }
            return text.Replace("[E]", "(A)").Replace("hold E", "hold A").Replace("[G]", "(RB)").Replace("[F]", "(Y)")
                       .Replace("[ENTER]", "(D-PAD DOWN)").Replace("[SPACE]", "(X)").Replace("SPACE", "X")
                       .Replace("TAB", "VIEW").Replace("ESC", "MENU");
        }

        /// <summary>A key name for prompts, e.g. Key("E", "A") gives "[E]" or "(A)" depending on the device in use.</summary>
        public static string Key(string keyboard, string gamepad) => UsingGamepad ? "(" + gamepad + ")" : "[" + keyboard + "]";
    }
}
