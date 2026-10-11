using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// One office door: the closed-door layer that slides down over the doorway, the clickable red
    /// button on the wall, and the door sounds.
    /// </summary>
    public class DoorButtonUI : MonoBehaviour
    {
        private const float SlideSeconds = 0.3f;

        private NightController night;
        private OfficeViewUI office;
        private NightAudio audioPlayer;
        private DoorSide side;
        private Image layer;
        private Button button;
        private Image statusBack;
        private Text status;
        private float closedAmount;
        private bool wasClosed;
        private bool slamPending;
        private int nightSeen;

        /// <summary>True when the door is completely open (the doorway is visible).</summary>
        public bool IsFullyOpen => closedAmount <= 0.001f;

        public static DoorButtonUI Create(Transform picture, NightController night, OfficeViewUI office, NightAudio audioPlayer,
                                          DoorSide side, Image closedLayer, RectInt wallButton)
        {
            bool left = side == DoorSide.Left;
            var holder = UiFactory.CreateArea(left ? "LeftDoor" : "RightDoor", picture, Vector2.zero, Vector2.one);
            var door = holder.gameObject.AddComponent<DoorButtonUI>();
            door.night = night;
            door.office = office;
            door.audioPlayer = audioPlayer;
            door.side = side;

            door.layer = closedLayer;
            door.layer.type = Image.Type.Filled;
            door.layer.fillMethod = Image.FillMethod.Vertical;
            door.layer.fillOrigin = (int)Image.OriginVertical.Top;
            door.layer.fillAmount = 0f;

            // Invisible button over the red button painted on the wall, with a soft glow on top.
            var hit = UiFactory.CreateImage("WallButton", holder, new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.one);
            UiFactory.PlaceInPixels(hit.rectTransform, OfficeViewUI.ImageWidth, OfficeViewUI.ImageHeight,
                                    wallButton.x, wallButton.y, wallButton.width, wallButton.height);
            hit.raycastTarget = true;
            door.button = hit.gameObject.AddComponent<Button>();
            door.button.targetGraphic = hit;
            door.button.navigation = new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = door.button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 0.35f, 0.3f, 0.22f);
            colors.pressedColor = new Color(1f, 0.2f, 0.2f, 0.4f);
            colors.selectedColor = colors.normalColor;
            colors.disabledColor = new Color(0f, 0f, 0f, 0f);
            door.button.colors = colors;
            door.button.onClick.AddListener(door.Toggle);

            door.statusBack = UiFactory.CreateImage("StatusBack", hit.transform, new Color(0f, 0f, 0f, 0.65f), new Vector2(-0.55f, -0.42f), new Vector2(1.55f, -0.06f));
            door.status = UiFactory.CreateLabel("Status", door.statusBack.transform, "", 30, NightColors.Ink, TextAnchor.MiddleCenter,
                                                Vector2.zero, Vector2.one);
            return door;
        }

        /// <summary>Hooked to the wall button.</summary>
        public void Toggle()
        {
            night.ToggleDoor(side);
        }

        private void Update()
        {
            Refresh(Time.unscaledDeltaTime);
        }

        public void Refresh(float deltaSeconds)
        {
            DoorSystem doors = night.State.Doors;
            bool closed = doors.IsClosed(side);
            bool playing = night.Phase == NightPhase.Playing;

            if (nightSeen != night.NightNumber)
            {
                // New night: doors start open, without sounds.
                nightSeen = night.NightNumber;
                wasClosed = closed;
                closedAmount = closed ? 1f : 0f;
                slamPending = false;
            }
            else if (closed != wasClosed)
            {
                wasClosed = closed;
                if (playing && !doors.IsLocked)
                {
                    audioPlayer.PlayDoorSlide();
                }
                slamPending = closed;
            }

            closedAmount = Mathf.MoveTowards(closedAmount, closed ? 1f : 0f, deltaSeconds / SlideSeconds);
            if (slamPending && closedAmount >= 1f)
            {
                slamPending = false;
                audioPlayer.PlayDoorSlam();
                office.Shake(10f, 0.18f);
            }

            layer.fillAmount = closedAmount;
            layer.enabled = closedAmount > 0f && !doors.IsLocked;

            button.interactable = playing && !doors.IsLocked;
            string key = side == DoorSide.Left ? "A" : "D";
            statusBack.enabled = !doors.IsLocked && playing;
            status.text = doors.IsLocked ? "" : "[" + key + "] " + (closed ? "CLOSED" : "OPEN");
            status.color = closed ? NightColors.Warning : NightColors.Ink;
        }
    }
}
