using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NightShift.Game
{
    /// <summary>
    /// What the hands did this frame while the player works a panel up close. There is no cursor:
    /// the mouse (or a stick) moves an invisible aim from control to control, the arrow keys, WASD
    /// or the d-pad step between them, and the use key, a click, space or the gamepad's A presses.
    /// </summary>
    public struct FocusInput
    {
        /// <summary>How far the aim moved this frame, in screen pixels (mouse or stick).</summary>
        public Vector2 AimDelta;
        /// <summary>A step from the arrow keys, WASD or the d-pad (each axis -1, 0 or 1).</summary>
        public Vector2Int Step;
        /// <summary>Sideways push this frame for dragging, in meters on the panel (mouse, stick or keys held).</summary>
        public float Slide;
        public bool Pressed;
        public bool Held;
        public bool Released;

        /// <summary>Dragging speed with the keys or a stick held, in meters per second: a fair swipe.</summary>
        private const float SlideSpeed = 0.32f;
        /// <summary>Meters on the panel per screen height of mouse movement.</summary>
        private const float SlidePerScreen = 1.1f;

        private static bool heldBefore;

        public static FocusInput Read()
        {
            Mouse mouse = Mouse.current;
            Keyboard keys = Keyboard.current;
            Gamepad pad = Gamepad.current;
            var input = new FocusInput();

            Vector2 mouseDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            Vector2 stick = pad != null ? pad.leftStick.ReadValue() + pad.rightStick.ReadValue() : Vector2.zero;
            input.AimDelta = mouseDelta + stick * Screen.height * 0.8f * Time.unscaledDeltaTime;

            int x = 0, y = 0;
            if (keys != null)
            {
                x += keys.dKey.wasPressedThisFrame || keys.rightArrowKey.wasPressedThisFrame ? 1 : 0;
                x -= keys.aKey.wasPressedThisFrame || keys.leftArrowKey.wasPressedThisFrame ? 1 : 0;
                y += keys.wKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame ? 1 : 0;
                y -= keys.sKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame ? 1 : 0;
            }
            if (pad != null)
            {
                x += pad.dpad.right.wasPressedThisFrame ? 1 : 0;
                x -= pad.dpad.left.wasPressedThisFrame ? 1 : 0;
                y += pad.dpad.up.wasPressedThisFrame ? 1 : 0;
                y -= pad.dpad.down.wasPressedThisFrame ? 1 : 0;
            }
            input.Step = new Vector2Int(x, y);

            float keySlide = keys == null ? 0f
                : (keys.dKey.isPressed || keys.rightArrowKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed || keys.leftArrowKey.isPressed ? 1f : 0f);
            input.Slide = mouseDelta.x / Mathf.Max(1f, Screen.height) * SlidePerScreen
                          + Mathf.Clamp(keySlide + stick.x, -1f, 1f) * SlideSpeed * Time.deltaTime;

            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool space = keys != null && keys.spaceKey.wasPressedThisFrame;
            input.Pressed = click || space || Controls.UsePressed;
            input.Held = (mouse != null && mouse.leftButton.isPressed) || (keys != null && keys.spaceKey.isPressed) || Controls.UseHeld;
            input.Released = heldBefore && !input.Held;
            heldBefore = input.Held;
            return input;
        }
    }

    /// <summary>
    /// A panel worked up close by hand (the breakers, the card reader, the tape box's keypad): the
    /// camera moves to <see cref="FocusView"/> and every frame the task gets the hands' input.
    /// Freddy keeps moving meanwhile; there is no timer, only him.
    /// </summary>
    public interface IFocusTask
    {
        /// <summary>Where the eyes go, looking at the panel.</summary>
        Transform FocusView { get; }

        /// <summary>One line telling what to do, shown at the bottom of the screen.</summary>
        string FocusHint { get; }

        /// <summary>How the hands move here, shown small under the hint.</summary>
        string FocusControls { get; }

        void FocusUpdate(FocusInput input);

        /// <summary>The player stepped back (or was caught); the task keeps what was done.</summary>
        void FocusEnded();
    }

    /// <summary>
    /// Which control of a panel the fingers are on: the one the invisible aim is nearest to, or the
    /// next one in the direction of a step. It is shown on the panel itself, by a soft ring of warm
    /// light around the control; no cursor is ever drawn.
    /// </summary>
    public class FocusSelector
    {
        private readonly Transform[] targets;
        private readonly Vector2[] sizes;
        private readonly Transform mark;
        private readonly Material markMaterial;
        private Vector2 aim;
        private bool aimSet;

        public int Selected { get; private set; } = -1;

        /// <param name="panel">The panel; the ring lies on its front (local +Z).</param>
        /// <param name="targets">The controls, each placed on its face.</param>
        /// <param name="sizes">Width and height of each control on the panel, in meters.</param>
        public FocusSelector(Transform panel, Transform[] targets, Vector2[] sizes)
        {
            this.targets = targets;
            this.sizes = sizes;
            markMaterial = new Material(BuildKit.Additive("focusMark", ProceduralTextures.Ring(128, 0.1f), new Color(1f, 0.75f, 0.35f, 0.9f)));
            GameObject quad = BuildKit.Quad("FocusMark", panel, Vector3.zero, Vector2.one, 0f, markMaterial);
            mark = quad.transform;
            quad.SetActive(false);
        }

        /// <summary>Moves the selection with this frame's input; <paramref name="usable"/> says which controls can be chosen now.</summary>
        public int Update(FocusInput input, Camera camera, System.Func<int, bool> usable)
        {
            if (Selected < 0 || !usable(Selected))
            {
                Select(Nearest(camera, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), usable), camera);
            }
            if (input.Step != Vector2Int.zero && Selected >= 0)
            {
                Select(InDirection(camera, new Vector2(input.Step.x, input.Step.y), usable), camera);
            }
            else if (input.AimDelta.sqrMagnitude > 0.01f)
            {
                if (!aimSet && Selected >= 0)
                {
                    aim = ScreenOf(camera, Selected);
                    aimSet = true;
                }
                aim += input.AimDelta;
                aim = new Vector2(Mathf.Clamp(aim.x, 0f, Screen.width), Mathf.Clamp(aim.y, 0f, Screen.height));
                int nearest = Nearest(camera, aim, usable);
                // A little stickiness, so the ring does not flicker between two neighbors.
                if (nearest >= 0 && nearest != Selected
                    && (Selected < 0 || Vector2.Distance(aim, ScreenOf(camera, nearest)) + 12f < Vector2.Distance(aim, ScreenOf(camera, Selected))))
                {
                    Selected = nearest;
                }
            }
            Draw();
            return Selected;
        }

        /// <summary>The ring goes away (the panel is done, or the player stepped back).</summary>
        public void Hide()
        {
            mark.gameObject.SetActive(false);
            aimSet = false;
        }

        /// <summary>
        /// Test helper (capture tour): moves the fingers to a control one step at a time, as the
        /// arrow keys would, then presses it; everything goes through the task's own input path.
        /// </summary>
        public IEnumerator Press(IFocusTask task, Camera camera, int target)
        {
            for (int guard = 0; guard < 12 && Selected != target; guard++)
            {
                Vector2Int step = Vector2Int.zero;
                if (Selected >= 0)
                {
                    Vector2 offset = ScreenOf(camera, target) - ScreenOf(camera, Selected);
                    step = Mathf.Abs(offset.x) > Mathf.Abs(offset.y) ? new Vector2Int(offset.x > 0f ? 1 : -1, 0) : new Vector2Int(0, offset.y > 0f ? 1 : -1);
                }
                task.FocusUpdate(new FocusInput { Step = step });
                yield return null;
            }
            if (Selected != target)
            {
                Debug.Log("CAPTURE focus could not reach control " + target + " (stuck on " + Selected + ")");
            }
            task.FocusUpdate(new FocusInput { Pressed = true, Held = true });
            yield return null;
            task.FocusUpdate(new FocusInput { Released = true });
        }

        private void Select(int index, Camera camera)
        {
            if (index < 0)
            {
                return;
            }
            Selected = index;
            aim = ScreenOf(camera, index);
            aimSet = true;
        }

        private void Draw()
        {
            if (Selected < 0)
            {
                mark.gameObject.SetActive(false);
                return;
            }
            mark.gameObject.SetActive(true);
            Vector3 local = mark.parent.InverseTransformPoint(targets[Selected].position);
            mark.localPosition = new Vector3(local.x, local.y, local.z + 0.012f);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.04f;
            mark.localScale = new Vector3(sizes[Selected].x * 1.35f * pulse, sizes[Selected].y * 1.35f * pulse, 1f);
            markMaterial.SetColor("_BaseColor", new Color(1f, 0.75f, 0.35f, 0.65f + Mathf.Sin(Time.unscaledTime * 6f) * 0.2f));
        }

        private Vector2 ScreenOf(Camera camera, int index) => camera.WorldToScreenPoint(targets[index].position);

        private int Nearest(Camera camera, Vector2 point, System.Func<int, bool> usable)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < targets.Length; i++)
            {
                if (!usable(i))
                {
                    continue;
                }
                float distance = Vector2.Distance(point, ScreenOf(camera, i));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        private int InDirection(Camera camera, Vector2 direction, System.Func<int, bool> usable)
        {
            Vector2 from = ScreenOf(camera, Selected);
            Vector2 unit = direction.normalized;
            int best = Selected;
            float bestScore = float.MaxValue;
            for (int i = 0; i < targets.Length; i++)
            {
                if (i == Selected || !usable(i))
                {
                    continue;
                }
                Vector2 offset = ScreenOf(camera, i) - from;
                float along = Vector2.Dot(offset, unit);
                if (along <= 1f)
                {
                    continue;
                }
                // Straight ahead first: sideways distance counts double.
                float across = Mathf.Abs(offset.x * unit.y - offset.y * unit.x);
                float score = along + across * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            return best;
        }
    }
}
