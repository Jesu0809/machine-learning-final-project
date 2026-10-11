using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NightShift.UI
{
    /// <summary>Keys the guard uses during a night.</summary>
    public enum GameKey
    {
        Camera1,
        Camera2,
        Camera3,
        Camera4,
        Camera5,
        ToggleCameras,
        LeftDoor,
        RightDoor,
        Confirm,
        Restart,
        Back,
        Debug
    }

    /// <summary>
    /// Reads the keyboard with whichever input system is active, so the game works with
    /// "Input System Package", "Input Manager (Old)" or "Both".
    /// </summary>
    public static class KeyboardInput
    {
        public static bool WasPressed(GameKey key)
        {
            // Shift + digit is reserved for driving the monster by hand (MonsterAgent heuristic).
            if (key <= GameKey.Camera5 && ShiftHeld())
            {
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return false;
            }
            switch (key)
            {
                case GameKey.Camera1: return keyboard.digit1Key.wasPressedThisFrame;
                case GameKey.Camera2: return keyboard.digit2Key.wasPressedThisFrame;
                case GameKey.Camera3: return keyboard.digit3Key.wasPressedThisFrame;
                case GameKey.Camera4: return keyboard.digit4Key.wasPressedThisFrame;
                case GameKey.Camera5: return keyboard.digit5Key.wasPressedThisFrame;
                case GameKey.ToggleCameras: return keyboard.spaceKey.wasPressedThisFrame;
                case GameKey.LeftDoor: return keyboard.aKey.wasPressedThisFrame;
                case GameKey.RightDoor: return keyboard.dKey.wasPressedThisFrame;
                case GameKey.Confirm: return keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                case GameKey.Restart: return keyboard.rKey.wasPressedThisFrame;
                case GameKey.Back: return keyboard.escapeKey.wasPressedThisFrame;
                case GameKey.Debug: return keyboard.tabKey.wasPressedThisFrame;
                default: return false;
            }
#else
            switch (key)
            {
                case GameKey.Camera1: return Input.GetKeyDown(KeyCode.Alpha1);
                case GameKey.Camera2: return Input.GetKeyDown(KeyCode.Alpha2);
                case GameKey.Camera3: return Input.GetKeyDown(KeyCode.Alpha3);
                case GameKey.Camera4: return Input.GetKeyDown(KeyCode.Alpha4);
                case GameKey.Camera5: return Input.GetKeyDown(KeyCode.Alpha5);
                case GameKey.ToggleCameras: return Input.GetKeyDown(KeyCode.Space);
                case GameKey.LeftDoor: return Input.GetKeyDown(KeyCode.A);
                case GameKey.RightDoor: return Input.GetKeyDown(KeyCode.D);
                case GameKey.Confirm: return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                case GameKey.Restart: return Input.GetKeyDown(KeyCode.R);
                case GameKey.Back: return Input.GetKeyDown(KeyCode.Escape);
                case GameKey.Debug: return Input.GetKeyDown(KeyCode.Tab);
                default: return false;
            }
#endif
        }

        /// <summary>Mouse X position from 0 (left edge) to 1 (right edge), or 0.5 without a mouse.</summary>
        public static float PointerX()
        {
            float width = Mathf.Max(1f, Screen.width);
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse == null ? 0.5f : Mathf.Clamp01(mouse.position.ReadValue().x / width);
#else
            return Mathf.Clamp01(Input.mousePosition.x / width);
#endif
        }

        private static bool ShiftHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#else
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
        }
    }
}
