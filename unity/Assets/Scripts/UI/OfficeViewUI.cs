using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// The security office: an ultra-wide picture panned with the mouse, with layers for the closed
    /// doors, the monster standing in a doorway and the power outage. Layer positions are pixel
    /// rectangles of the 1584 × 672 office picture.
    /// </summary>
    public class OfficeViewUI : MonoBehaviour
    {
        public const float ImageWidth = 1584f;
        public const float ImageHeight = 672f;

        // x, y (top-left), width, height in office-picture pixels.
        private static readonly RectInt LeftDoorLayer = new RectInt(430, 40, 152, 624);
        private static readonly RectInt RightDoorLayer = new RectInt(1004, 40, 176, 628);
        private static readonly RectInt LeftMonsterLayer = new RectInt(440, 170, 200, 472);
        private static readonly RectInt RightMonsterLayer = new RectInt(984, 170, 196, 492);
        private static readonly RectInt LeftWallButton = new RectInt(405, 297, 72, 96);
        private static readonly RectInt RightWallButton = new RectInt(1121, 295, 72, 96);

        private const float PanSmoothing = 6f;
        private const float OutageFlickerSeconds = 1.3f;

        private NightController night;
        private NightAudio audioPlayer;
        private RectTransform viewport;
        private RectTransform picture;
        private Image monsterLeft;
        private Image monsterRight;
        private Image powerOut;
        private Image lowPowerDim;
        private DoorButtonUI leftDoor;
        private DoorButtonUI rightDoor;
        private float pan;
        private float shakeStrength;
        private float shakeUntil;
        private bool outageShown;
        private float outageStartedAt;

        public static OfficeViewUI Create(Transform parent, NightController night, NightArt art, NightAudio audioPlayer)
        {
            var viewport = UiFactory.CreateArea("Office", parent, Vector2.zero, Vector2.one);
            UiFactory.CreateImage("Black", viewport, Color.black, Vector2.zero, Vector2.one);
            var office = viewport.gameObject.AddComponent<OfficeViewUI>();
            office.night = night;
            office.audioPlayer = audioPlayer;
            office.viewport = viewport;

            var picture = UiFactory.CreateSprite("Picture", viewport, art.office, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            office.picture = picture.rectTransform;

            office.monsterLeft = CreateLayer("MonsterLeftDoor", picture.transform, art.monsterLeftDoor, LeftMonsterLayer);
            office.monsterRight = CreateLayer("MonsterRightDoor", picture.transform, art.monsterRightDoor, RightMonsterLayer);

            office.leftDoor = DoorButtonUI.Create(picture.transform, night, office, audioPlayer, DoorSide.Left,
                                                  CreateLayer("LeftDoorClosed", picture.transform, art.leftDoorClosed, LeftDoorLayer),
                                                  LeftWallButton);
            office.rightDoor = DoorButtonUI.Create(picture.transform, night, office, audioPlayer, DoorSide.Right,
                                                   CreateLayer("RightDoorClosed", picture.transform, art.rightDoorClosed, RightDoorLayer),
                                                   RightWallButton);

            office.powerOut = UiFactory.CreateSprite("PowerOut", picture.transform, art.officePowerOut, Vector2.zero, Vector2.one);
            office.powerOut.enabled = false;
            office.lowPowerDim = UiFactory.CreateImage("LowPowerDim", picture.transform, new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.one);
            return office;
        }

        private static Image CreateLayer(string name, Transform picture, Sprite sprite, RectInt pixels)
        {
            var image = UiFactory.CreateSprite(name, picture, sprite, Vector2.zero, Vector2.one);
            UiFactory.PlaceInPixels(image.rectTransform, ImageWidth, ImageHeight, pixels.x, pixels.y, pixels.width, pixels.height);
            image.enabled = false;
            return image;
        }

        /// <summary>Shakes the view for a moment (door slam).</summary>
        public void Shake(float strength, float seconds)
        {
            shakeStrength = strength;
            shakeUntil = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            GameState state = night.State;
            bool playing = night.Phase == NightPhase.Playing;
            bool depleted = state.Power.IsDepleted;

            // The monster is visible in a doorway while it stands in that corridor and the door is open.
            monsterLeft.enabled = playing && !depleted && state.MonsterRoom == RoomId.WestCorridor && leftDoor.IsFullyOpen;
            monsterRight.enabled = playing && !depleted && state.MonsterRoom == RoomId.EastCorridor && rightDoor.IsFullyOpen;

            UpdatePowerOut(depleted);
            UpdateLowPowerFlicker(playing && !depleted && state.Power.Normalized < 0.15f);
        }

        private void UpdatePowerOut(bool depleted)
        {
            if (depleted && !outageShown)
            {
                outageShown = true;
                outageStartedAt = Time.unscaledTime;
                audioPlayer.PlayPowerOut();
            }
            else if (!depleted)
            {
                outageShown = false;
            }

            powerOut.enabled = outageShown;
            if (outageShown)
            {
                float t = Time.unscaledTime - outageStartedAt;
                bool flickerOn = t >= OutageFlickerSeconds || Mathf.Repeat(t, 0.22f) < 0.12f;
                powerOut.color = new Color(1f, 1f, 1f, flickerOn ? 1f : 0.35f);
            }
        }

        private void UpdateLowPowerFlicker(bool lowPower)
        {
            float alpha = lowPowerDim.color.a;
            if (lowPower && Random.value < 0.03f)
            {
                alpha = Random.Range(0.35f, 0.6f);
            }
            alpha = Mathf.MoveTowards(alpha, 0f, Time.unscaledDeltaTime * 3f);
            lowPowerDim.color = new Color(0f, 0f, 0f, alpha);
        }

        private void LateUpdate()
        {
            UpdateLayout(Time.unscaledDeltaTime);
        }

        /// <summary>Fits the picture to the screen height (or width on ultra-wide screens) and pans it.</summary>
        public void UpdateLayout(float deltaSeconds)
        {
            Rect view = viewport.rect;
            float height = view.height;
            float width = height * ImageWidth / ImageHeight;
            if (width < view.width)
            {
                width = view.width;
                height = width * ImageHeight / ImageWidth;
            }
            picture.sizeDelta = new Vector2(width, height);

            bool canLook = night.Phase == NightPhase.Playing && !night.State.Cameras.IsOpen;
            float target = 0f;
            if (canLook)
            {
                float fromCenter = (KeyboardInput.PointerX() - 0.5f) * 2f;
                float amount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.9f, Mathf.Abs(fromCenter)));
                target = Mathf.Sign(fromCenter) * amount;
            }
            pan = Mathf.Lerp(pan, target, 1f - Mathf.Exp(-PanSmoothing * deltaSeconds));

            float range = (width - view.width) * 0.5f;
            Vector2 offset = new Vector2(-pan * range, 0f);
            if (Time.unscaledTime < shakeUntil)
            {
                offset += Random.insideUnitCircle * shakeStrength;
            }
            picture.anchoredPosition = offset;
        }
    }
}
