using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// One office door: the doorway, a steel slab that slides down when closed, and the button
    /// that toggles it.
    /// </summary>
    public class DoorButtonUI : MonoBehaviour
    {
        private const float SlideSpeed = 6f;

        private NightController night;
        private DoorSide side;
        private RectTransform slab;
        private Text stateLabel;
        private Button button;
        private float closedAmount;

        /// <summary>Builds the door inside <paramref name="parent"/> (the office view).</summary>
        public static DoorButtonUI Create(Transform parent, NightController night, DoorSide side)
        {
            bool left = side == DoorSide.Left;
            float x0 = left ? 0.03f : 0.79f;
            float x1 = left ? 0.21f : 0.97f;

            var root = UiFactory.CreateArea(left ? "LeftDoor" : "RightDoor", parent, Vector2.zero, Vector2.one);
            var door = root.gameObject.AddComponent<DoorButtonUI>();
            door.night = night;
            door.side = side;

            UiFactory.CreateImage("Frame", root, NightColors.Steel, new Vector2(x0 - 0.01f, 0.2f), new Vector2(x1 + 0.01f, 0.84f));
            var doorway = UiFactory.CreateImage("Doorway", root, new Color32(4, 5, 8, 255), new Vector2(x0, 0.21f), new Vector2(x1, 0.82f));

            // The slab is a child of the doorway so it slides within it.
            var slabImage = UiFactory.CreateImage("Slab", doorway.transform, new Color32(70, 76, 90, 255), new Vector2(0f, 1f), Vector2.one);
            door.slab = slabImage.rectTransform;
            for (int stripe = 0; stripe < 4; stripe++)
            {
                float y = 0.15f + stripe * 0.22f;
                UiFactory.CreateImage("Stripe", slabImage.transform, NightColors.Warning, new Vector2(0.08f, y), new Vector2(0.92f, y + 0.04f));
            }

            string title = left ? "LEFT DOOR  [A]" : "RIGHT DOOR  [D]";
            door.button = UiFactory.CreateButton("Button", root, title, 26, new Vector2(x0, 0.105f), new Vector2(x1, 0.175f), door.Toggle);
            door.stateLabel = UiFactory.CreateLabel("State", root, "OPEN", 22, NightColors.Ok, TextAnchor.MiddleCenter,
                                                    new Vector2(x0, 0.84f), new Vector2(x1, 0.88f), FontStyle.Bold);
            return door;
        }

        /// <summary>Hooked to the button's OnClick.</summary>
        public void Toggle()
        {
            night.ToggleDoor(side);
        }

        private void Update()
        {
            Refresh(Time.unscaledDeltaTime);
        }

        /// <summary>Updates the slab position and the labels from the game state.</summary>
        public void Refresh(float deltaSeconds)
        {
            DoorSystem doors = night.State.Doors;
            bool closed = doors.IsClosed(side);

            closedAmount = Mathf.MoveTowards(closedAmount, closed ? 1f : 0f, deltaSeconds * SlideSpeed);
            slab.anchorMin = new Vector2(0f, 1f - closedAmount);

            button.interactable = night.Phase == NightPhase.Playing && !doors.IsLocked;

            if (doors.IsLocked)
            {
                stateLabel.text = "NO POWER";
                stateLabel.color = NightColors.Accent;
            }
            else
            {
                stateLabel.text = closed ? "CLOSED" : "OPEN";
                stateLabel.color = closed ? NightColors.Warning : NightColors.Ok;
            }
        }
    }
}
