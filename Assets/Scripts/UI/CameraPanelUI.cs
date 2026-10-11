using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// The camera monitor: the selected room's picture (empty or with the monster), a soft CRT
    /// filter, and a floor plan of the building with one clickable badge per camera. Every change of
    /// picture happens under a burst of static, never as a crossfade.
    /// </summary>
    public class CameraPanelUI : MonoBehaviour
    {
        private const float BurstSeconds = 0.32f;
        private const float BaseStatic = 0.028f;
        private const float FlipSeconds = 0.18f;
        private const float FeedZoom = 1.12f;
        private const float PanSeconds = 14f;

        private static readonly string[] CameraNames = { "STAGE", "MAIN HALL", "WEST HALL", "KITCHEN", "EAST HALL" };

        // Floor plan, in map coordinates (0–1). Each room is (min, max).
        private static readonly Rect[] RoomRects =
        {
            Rect.MinMaxRect(0.03f, 0.7f, 0.3f, 0.97f),   // Stage
            Rect.MinMaxRect(0.32f, 0.7f, 0.97f, 0.97f),  // Main Hall
            Rect.MinMaxRect(0.32f, 0.1f, 0.42f, 0.7f),   // West Hall
            Rect.MinMaxRect(0.58f, 0.46f, 0.97f, 0.68f), // Kitchen
            Rect.MinMaxRect(0.8f, 0.1f, 0.9f, 0.46f),    // East Hall
        };
        private static readonly Rect OfficeRect = Rect.MinMaxRect(0.42f, 0.03f, 0.8f, 0.27f);

        private NightController night;
        private NightArt art;
        private NightAudio audioPlayer;
        private GameObject monitor;
        private RectTransform monitorRect;
        private bool wasOpen;
        private float openedAt;
        private Button openButton;
        private Image feed;
        private RawImage staticNoise;
        private Text cameraTitle;
        private Text timestamp;
        private Image recDot;
        private Button[] badges;
        private Text[] badgeLabels;
        private float burstStartedAt = -10f;
        private RoomId? shownRoom;
        private bool shownMonster;

        public static CameraPanelUI Create(Transform parent, NightController night, NightArt art, NightAudio audioPlayer)
        {
            var holder = UiFactory.CreateArea("Cameras", parent, Vector2.zero, Vector2.one);
            var cameras = holder.gameObject.AddComponent<CameraPanelUI>();
            cameras.night = night;
            cameras.art = art;
            cameras.audioPlayer = audioPlayer;

            cameras.openButton = UiFactory.CreateButton("OpenCameras", holder, "CAMERAS  [SPACE]", 34,
                                                        new Vector2(0.36f, 0.015f), new Vector2(0.64f, 0.075f), night.ToggleCameras);
            UiFactory.SetButtonColors(cameras.openButton, new Color(0.08f, 0.09f, 0.12f, 0.8f));

            var monitorImage = UiFactory.CreateFullScreen("Monitor", holder, Color.black);
            cameras.monitor = monitorImage.gameObject;
            cameras.monitorRect = monitorImage.rectTransform;
            Transform m = monitorImage.transform;

            cameras.feed = UiFactory.CreateSprite("Feed", m, null, Vector2.zero, Vector2.one, keepAspect: true);
            cameras.staticNoise = UiFactory.CreateRawImage("Static", m, UiFactory.NoiseTexture, new Color(1f, 1f, 1f, BaseStatic), Vector2.zero, Vector2.one);
            var lines = UiFactory.CreateRawImage("Scanlines", m, UiFactory.ScanlineTexture, new Color(1f, 1f, 1f, 0.16f), Vector2.zero, Vector2.one);
            lines.uvRect = new Rect(0f, 0f, 1f, 270f);
            UiFactory.CreateRawImage("Vignette", m, UiFactory.VignetteTexture, new Color(1f, 1f, 1f, 0.55f), Vector2.zero, Vector2.one);

            cameras.recDot = UiFactory.CreateImage("Rec", m, NightColors.Accent, new Vector2(0.027f, 0.917f), new Vector2(0.041f, 0.942f));
            cameras.cameraTitle = UiFactory.CreateLabel("Title", m, "", 48, NightColors.Ink, TextAnchor.UpperLeft,
                                                        new Vector2(0.055f, 0.885f), new Vector2(0.6f, 0.97f));
            cameras.timestamp = UiFactory.CreateLabel("Timestamp", m, "", 34, NightColors.Ink, TextAnchor.UpperLeft,
                                                      new Vector2(0.055f, 0.84f), new Vector2(0.6f, 0.89f));

            cameras.BuildMap(m);

            var close = UiFactory.CreateButton("Close", m, "CLOSE  [SPACE]", 34, new Vector2(0.36f, 0.015f), new Vector2(0.64f, 0.075f), night.CloseCameras);
            UiFactory.SetButtonColors(close, new Color(0.08f, 0.09f, 0.12f, 0.8f));

            night.MonsterMoved += cameras.OnMonsterMoved;
            night.CameraChanged += cameras.OnCameraChanged;
            return cameras;
        }

        private void OnDestroy()
        {
            if (night != null)
            {
                night.MonsterMoved -= OnMonsterMoved;
                night.CameraChanged -= OnCameraChanged;
            }
        }

        private void BuildMap(Transform monitorTransform)
        {
            var panel = UiFactory.CreateImage("Map", monitorTransform, new Color(0.02f, 0.03f, 0.04f, 0.88f), new Vector2(0.66f, 0.1f), new Vector2(0.985f, 0.58f));
            Transform map = UiFactory.CreateArea("Plan", panel.transform, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.9f));
            UiFactory.CreateLabel("Title", panel.transform, "BUILDING MAP", 26, NightColors.InkDim, TextAnchor.MiddleLeft,
                                  new Vector2(0.04f, 0.9f), new Vector2(0.9f, 0.99f));

            Color wall = new Color(0.62f, 0.68f, 0.72f, 0.9f);
            Color floor = new Color(0.07f, 0.09f, 0.11f, 0.95f);
            for (int i = 0; i < RoomRects.Length; i++)
            {
                DrawRoom(map, RoomRects[i], wall, floor);
            }
            DrawRoom(map, OfficeRect, new Color(0.85f, 0.3f, 0.3f, 0.95f), new Color(0.18f, 0.05f, 0.06f, 0.95f));

            // Openings between connected rooms (floor-colored gaps in the shared walls).
            Opening(map, Rect.MinMaxRect(0.295f, 0.79f, 0.325f, 0.87f), floor); // Stage – Main Hall
            Opening(map, Rect.MinMaxRect(0.335f, 0.69f, 0.405f, 0.71f), floor); // Main Hall – West Hall
            Opening(map, Rect.MinMaxRect(0.68f, 0.675f, 0.8f, 0.705f), floor);  // Main Hall – Kitchen
            Opening(map, Rect.MinMaxRect(0.815f, 0.455f, 0.885f, 0.475f), floor); // Kitchen – East Hall
            Opening(map, Rect.MinMaxRect(0.41f, 0.12f, 0.43f, 0.24f), NightColors.Accent);  // Left door
            Opening(map, Rect.MinMaxRect(0.79f, 0.12f, 0.81f, 0.24f), NightColors.Accent);  // Right door

            UiFactory.CreateLabel("You", map, "YOU", 26, NightColors.Ink, TextAnchor.MiddleCenter,
                                  OfficeRect.min, OfficeRect.max);

            badges = new Button[RoomRects.Length];
            badgeLabels = new Text[RoomRects.Length];
            for (int i = 0; i < RoomRects.Length; i++)
            {
                RoomId room = (RoomId)i;
                // The East Hall badge sits higher so it does not cover the office.
                Vector2 c = room == RoomId.EastCorridor ? new Vector2(0.85f, 0.37f) : RoomRects[i].center;
                badges[i] = UiFactory.CreateButton("Cam" + (i + 1), map, "CAM " + (i + 1), 24,
                                                   c - new Vector2(0.085f, 0.045f), c + new Vector2(0.085f, 0.045f),
                                                   () => night.ViewCamera(room));
                badgeLabels[i] = UiFactory.GetLabel(badges[i]);
            }
        }

        private static void DrawRoom(Transform map, Rect rect, Color wall, Color floor)
        {
            const float thickness = 0.008f;
            UiFactory.CreateImage("Wall", map, wall, rect.min, rect.max);
            UiFactory.CreateImage("Floor", map, floor, rect.min + new Vector2(thickness, thickness * 1.6f), rect.max - new Vector2(thickness, thickness * 1.6f));
        }

        private static void Opening(Transform map, Rect rect, Color color)
        {
            UiFactory.CreateImage("Opening", map, color, rect.min, rect.max);
        }

        private void OnMonsterMoved(RoomId from, RoomId to)
        {
            CameraSystem cameras = night.State.Cameras;
            if (cameras.IsOpen && (cameras.IsWatching(from) || cameras.IsWatching(to)))
            {
                StartBurst();
            }
        }

        private void OnCameraChanged(RoomId room)
        {
            StartBurst();
        }

        private void StartBurst()
        {
            burstStartedAt = Time.unscaledTime;
            audioPlayer.PlayCameraSwitch();
        }

        private void Update()
        {
            Refresh();
        }

        public void Refresh()
        {
            GameState state = night.State;
            bool playing = night.Phase == NightPhase.Playing;
            bool open = playing && state.Cameras.IsOpen;

            monitor.SetActive(open);
            openButton.gameObject.SetActive(playing && !open && state.Cameras.IsAvailable);
            if (!open)
            {
                shownRoom = null;
                wasOpen = false;
                return;
            }

            // The monitor flips up from the bottom when it opens.
            if (!wasOpen)
            {
                wasOpen = true;
                openedAt = Time.unscaledTime;
            }
            float flip = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - openedAt) / FlipSeconds));
            monitorRect.anchoredPosition = new Vector2(0f, -(1f - flip) * monitorRect.rect.height * 0.6f);

            // Security cameras sweep slowly from side to side.
            float sweep = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / PanSeconds);
            RectTransform feedRect = feed.rectTransform;
            feedRect.localScale = Vector3.one * FeedZoom;
            feedRect.anchoredPosition = new Vector2(sweep * monitorRect.rect.width * (FeedZoom - 1f) * 0.5f, 0f);

            RoomId watched = state.Cameras.WatchedRoom.Value;
            bool monsterHere = state.MonsterRoom == watched;

            // The picture only changes under static: switching cameras or the monster arriving or leaving.
            if (shownRoom != watched || shownMonster != monsterHere)
            {
                if (Time.unscaledTime - burstStartedAt > BurstSeconds)
                {
                    StartBurst();
                }
                shownRoom = watched;
                shownMonster = monsterHere;
                int index = (int)watched;
                feed.sprite = monsterHere ? art.cameraMonster[index] : art.cameraEmpty[index];
            }

            float sinceBurst = Time.unscaledTime - burstStartedAt;
            float noiseAlpha = sinceBurst < 0.08f ? 1f
                             : sinceBurst < BurstSeconds ? Mathf.Lerp(0.85f, BaseStatic, (sinceBurst - 0.08f) / (BurstSeconds - 0.08f))
                             : BaseStatic + Random.Range(-0.02f, 0.02f);
            staticNoise.color = new Color(1f, 1f, 1f, noiseAlpha);
            UiFactory.AnimateNoise(staticNoise, 3f);

            cameraTitle.text = "CAM " + ((int)watched + 1) + "  " + CameraNames[(int)watched];
            timestamp.text = "NIGHT " + night.NightNumber + "   " + UiFactory.FormatTimestamp(state.Clock.Progress);
            recDot.enabled = Mathf.Repeat(Time.unscaledTime, 1.2f) < 0.7f;

            bool blinkOn = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
            for (int i = 0; i < badges.Length; i++)
            {
                bool selected = i == (int)watched;
                Color color = selected ? (blinkOn ? NightColors.Accent : new Color(0.55f, 0.12f, 0.14f, 1f)) : new Color(0.16f, 0.19f, 0.22f, 0.95f);
                UiFactory.SetButtonColors(badges[i], color);
                badgeLabels[i].color = selected ? Color.white : NightColors.Ink;
            }
        }
    }
}
