using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace NightShift.Game
{
    /// <summary>A keyboard action the player can rebind in the settings.</summary>
    public enum InputAction
    {
        Forward,
        Back,
        Left,
        Right,
        Sprint,
        Crouch,
        Use,
        Flashlight,
        Throw,
        HoldBreath,
        Inspector,
        Skip
    }

    /// <summary>
    /// Every game input in one place, for keyboard and mouse and for a gamepad (Xbox layout).
    /// <code>
    ///   Move        WASD            left stick
    ///   Look        mouse           right stick
    ///   Sprint      Shift           left stick click (L3)
    ///   Crouch      C (hold), Ctrl  B (toggle)
    ///   Use / hide  E               A
    ///   Flashlight  F               Y
    ///   Party horn  G               RB
    ///   Hold breath Space           X
    ///   Inspector   Tab             View
    ///   Pause       Esc             Menu
    ///   Skip        Enter           D-pad down
    /// </code>
    /// The keyboard keys can be rebound and the look sensitivity changed in the settings; both are
    /// saved in PlayerPrefs. The diagram in the controls screen (and on the website) shows the defaults.
    /// </summary>
    public static class Controls
    {
        /// <summary>Right stick speed, degrees per second at full tilt (times the controller sensitivity).</summary>
        private const float StickLookSpeed = 150f;

        /// <summary>Degrees per pixel of mouse movement (times the mouse sensitivity).</summary>
        private const float MouseLookScale = 0.08f;

        private static readonly Dictionary<InputAction, Key> Defaults = new Dictionary<InputAction, Key>
        {
            { InputAction.Forward, Key.W }, { InputAction.Back, Key.S }, { InputAction.Left, Key.A }, { InputAction.Right, Key.D },
            { InputAction.Sprint, Key.LeftShift }, { InputAction.Crouch, Key.C }, { InputAction.Use, Key.E },
            { InputAction.Flashlight, Key.F }, { InputAction.Throw, Key.G }, { InputAction.HoldBreath, Key.Space },
            { InputAction.Inspector, Key.Tab }, { InputAction.Skip, Key.Enter }
        };

        private static readonly Dictionary<InputAction, Key> Bindings = new Dictionary<InputAction, Key>();

        private static bool crouchToggled;
        private static int polledFrame = -1;
        private static bool loaded;

        /// <summary>True when the last input came from a gamepad (prompts and pointer lock use it).</summary>
        public static bool UsingGamepad { get; private set; }

        /// <summary>Mouse look speed, 0.2–3 (1 = default).</summary>
        public static float MouseSensitivity { get; private set; } = 1f;

        /// <summary>Right stick look speed, 0.2–3 (1 = default).</summary>
        public static float StickSensitivity { get; private set; } = 1f;

        /// <summary>Moving the mouse or stick up looks down.</summary>
        public static bool InvertY { get; private set; }

        private static Keyboard Keys => Keyboard.current;
        private static Mouse Pointer => Mouse.current;
        private static Gamepad Pad => Gamepad.current;

        /// <summary>Call once per frame before reading anything.</summary>
        public static void Poll()
        {
            Load();
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

        // ------------------------------------------------------------------ settings

        /// <summary>The key bound to <paramref name="action"/>.</summary>
        public static Key Binding(InputAction action)
        {
            Load();
            return Bindings[action];
        }

        /// <summary>Binds <paramref name="key"/> to <paramref name="action"/>; an action that had the key gets the old one (a swap).</summary>
        public static void Rebind(InputAction action, Key key)
        {
            Load();
            Key old = Bindings[action];
            foreach (InputAction other in new List<InputAction>(Bindings.Keys))
            {
                if (other != action && Bindings[other] == key)
                {
                    Bindings[other] = old;
                }
            }
            Bindings[action] = key;
            Save();
        }

        public static void SetMouseSensitivity(float value)
        {
            MouseSensitivity = Mathf.Clamp(value, 0.2f, 3f);
            Save();
        }

        public static void SetStickSensitivity(float value)
        {
            StickSensitivity = Mathf.Clamp(value, 0.2f, 3f);
            Save();
        }

        public static void SetInvertY(bool invert)
        {
            InvertY = invert;
            Save();
        }

        public static void ResetSettings()
        {
            Bindings.Clear();
            foreach (KeyValuePair<InputAction, Key> pair in Defaults)
            {
                Bindings[pair.Key] = pair.Value;
            }
            MouseSensitivity = 1f;
            StickSensitivity = 1f;
            InvertY = false;
            Save();
        }

        /// <summary>A short name for a key, for the settings and the prompts ("E", "SHIFT", "SPACE").</summary>
        public static string KeyName(Key key)
        {
            switch (key)
            {
                case Key.LeftShift: return "SHIFT";
                case Key.RightShift: return "R-SHIFT";
                case Key.LeftCtrl: return "CTRL";
                case Key.RightCtrl: return "R-CTRL";
                case Key.LeftAlt: return "ALT";
                case Key.Enter: return "ENTER";
                case Key.Space: return "SPACE";
                case Key.Tab: return "TAB";
                case Key.Backspace: return "BACKSPACE";
                case Key.CapsLock: return "CAPS";
                default:
                    string name = key.ToString().ToUpperInvariant();
                    return name.StartsWith("DIGIT") ? name.Substring(5) : name;
            }
        }

        /// <summary>The key the player just pressed this frame (for rebinding), or None.</summary>
        public static Key KeyPressedThisFrame()
        {
            if (Keys == null)
            {
                return Key.None;
            }
            foreach (KeyControl control in Keys.allKeys)
            {
                if (control != null && control.wasPressedThisFrame && control.keyCode != Key.Escape)
                {
                    return control.keyCode;
                }
            }
            return Key.None;
        }

        private static void Load()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            foreach (KeyValuePair<InputAction, Key> pair in Defaults)
            {
                int saved = PlayerPrefs.GetInt("key." + pair.Key, (int)pair.Value);
                Bindings[pair.Key] = System.Enum.IsDefined(typeof(Key), saved) && saved != 0 ? (Key)saved : pair.Value;
            }
            MouseSensitivity = PlayerPrefs.GetFloat("look.mouse", 1f);
            StickSensitivity = PlayerPrefs.GetFloat("look.stick", 1f);
            InvertY = PlayerPrefs.GetInt("look.invertY", 0) == 1;
        }

        private static void Save()
        {
            foreach (KeyValuePair<InputAction, Key> pair in Bindings)
            {
                PlayerPrefs.SetInt("key." + pair.Key, (int)pair.Value);
            }
            PlayerPrefs.SetFloat("look.mouse", MouseSensitivity);
            PlayerPrefs.SetFloat("look.stick", StickSensitivity);
            PlayerPrefs.SetInt("look.invertY", InvertY ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static KeyControl Control(InputAction action) => Keys != null ? Keys[Binding(action)] : null;

        private static bool Held(InputAction action) => Control(action)?.isPressed ?? false;

        private static bool Pressed(InputAction action) => Control(action)?.wasPressedThisFrame ?? false;

        // ------------------------------------------------------------------ inputs

        public static Vector2 Move
        {
            get
            {
                Vector2 input = Vector2.zero;
                if (Keys != null)
                {
                    if (Held(InputAction.Forward)) input.y += 1f;
                    if (Held(InputAction.Back)) input.y -= 1f;
                    if (Held(InputAction.Right)) input.x += 1f;
                    if (Held(InputAction.Left)) input.x -= 1f;
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
                Load();
                Vector2 look = Pointer != null ? Pointer.delta.ReadValue() * (MouseLookScale * MouseSensitivity) : Vector2.zero;
                if (Pad != null)
                {
                    Vector2 stick = Pad.rightStick.ReadValue();
                    // Squared response: precise near the center, fast at full tilt.
                    look += stick * stick.magnitude * (StickLookSpeed * StickSensitivity) * Time.unscaledDeltaTime;
                }
                if (InvertY)
                {
                    look.y = -look.y;
                }
                return look;
            }
        }

        public static bool Sprint => Held(InputAction.Sprint) || (Pad != null && Pad.leftStickButton.isPressed);

        /// <summary>Crouch is held with its key (Ctrl always works too) or toggled with B on a gamepad.</summary>
        public static bool Crouch => Held(InputAction.Crouch) || (Keys != null && Keys.leftCtrlKey.isPressed) || crouchToggled;

        /// <summary>True only on the frame crouch is pressed (used to start a slide).</summary>
        public static bool CrouchPressed => Pressed(InputAction.Crouch) || (Keys != null && Keys.leftCtrlKey.wasPressedThisFrame)
                                            || (Pad != null && Pad.buttonEast.wasPressedThisFrame);

        public static bool UsePressed => Pressed(InputAction.Use) || (Pad != null && Pad.buttonSouth.wasPressedThisFrame);

        public static bool UseHeld => Held(InputAction.Use) || (Pad != null && Pad.buttonSouth.isPressed);

        public static bool FlashlightPressed => Pressed(InputAction.Flashlight) || (Pad != null && Pad.buttonNorth.wasPressedThisFrame);

        public static bool ThrowPressed => Pressed(InputAction.Throw) || (Pad != null && Pad.rightShoulder.wasPressedThisFrame);

        public static bool HoldBreath => Held(InputAction.HoldBreath) || (Pad != null && Pad.buttonWest.isPressed);

        public static bool InspectorPressed => Pressed(InputAction.Inspector) || (Pad != null && Pad.selectButton.wasPressedThisFrame);

        public static bool PausePressed => (Keys != null && Keys.escapeKey.wasPressedThisFrame) || (Pad != null && Pad.startButton.wasPressedThisFrame);

        public static bool SkipPressed => Pressed(InputAction.Skip) || (Keys != null && Keys.numpadEnterKey.wasPressedThisFrame)
                                          || (Pad != null && Pad.dpad.down.wasPressedThisFrame);

        /// <summary>Anything that skips a cinematic: Enter, Esc, Space, a click, or A / Menu / D-pad down.</summary>
        public static bool SkipCinematicPressed =>
            SkipPressed
            || (Keys != null && (Keys.escapeKey.wasPressedThisFrame || Keys.spaceKey.wasPressedThisFrame || Keys.enterKey.wasPressedThisFrame))
            || (Pointer != null && Pointer.leftButton.wasPressedThisFrame)
            || (Pad != null && (Pad.buttonSouth.wasPressedThisFrame || Pad.startButton.wasPressedThisFrame));

        /// <summary>Closes a note: the use key, Esc, or A / B.</summary>
        public static bool ClosePressed => Pressed(InputAction.Use) || (Keys != null && Keys.escapeKey.wasPressedThisFrame)
                                           || (Pad != null && (Pad.buttonSouth.wasPressedThisFrame || Pad.buttonEast.wasPressedThisFrame));

        /// <summary>
        /// Rewrites the default keys of a prompt ("[E]", "hold E", "SPACE", "TAB"...) with the keys
        /// the player bound, or with the gamepad buttons when a gamepad is in use.
        /// </summary>
        public static string Localize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            if (UsingGamepad)
            {
                return text.Replace("[Hold E]", "(Hold A)").Replace("[E]", "(A)").Replace("hold E", "hold A").Replace("[G]", "(RB)").Replace("[F]", "(Y)")
                           .Replace("[ENTER]", "(D-PAD DOWN)").Replace("[SPACE]", "(X)").Replace("SPACE", "X")
                           .Replace("TAB", "VIEW").Replace("ESC", "MENU");
            }
            string use = KeyName(Binding(InputAction.Use));
            return text.Replace("[Hold E]", "[Hold " + use + "]").Replace("[E]", "[" + use + "]").Replace("hold E", "hold " + use)
                       .Replace("[G]", "[" + KeyName(Binding(InputAction.Throw)) + "]")
                       .Replace("[F]", "[" + KeyName(Binding(InputAction.Flashlight)) + "]")
                       .Replace("[ENTER]", "[" + KeyName(Binding(InputAction.Skip)) + "]")
                       .Replace("SPACE", KeyName(Binding(InputAction.HoldBreath)))
                       .Replace("TAB", KeyName(Binding(InputAction.Inspector)));
        }

        /// <summary>A key name for prompts, e.g. KeyLabel("E", "A") gives "[E]" or "(A)" depending on the device in use.</summary>
        public static string KeyLabel(string keyboard, string gamepad) => UsingGamepad ? "(" + gamepad + ")" : "[" + keyboard + "]";
    }
}
