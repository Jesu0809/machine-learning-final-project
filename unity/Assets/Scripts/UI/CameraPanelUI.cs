using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// Camera panel: a monitor that shows the selected room (with the monster when it is there)
    /// and a clickable map of the five cameras. Placeholder art is drawn with plain rectangles.
    /// </summary>
    public class CameraPanelUI : MonoBehaviour
    {
        private const float StaticBurstSeconds = 0.25f;

        private struct Shape
        {
            public Vector2 Min;
            public Vector2 Max;
            public Color Color;

            public Shape(float x0, float y0, float x1, float y1, Color color)
            {
                Min = new Vector2(x0, y0);
                Max = new Vector2(x1, y1);
                Color = color;
            }
        }

        private struct RoomArt
        {
            public string Title;
            public Color Background;
            public Shape[] Shapes;
            public Vector2 MonsterMin;
            public Vector2 MonsterMax;
        }

        // Map layout: every connection is horizontal or vertical, so lines are plain rectangles.
        private static readonly Vector2[] MapPositions =
        {
            new Vector2(0.16f, 0.74f), // Stage
            new Vector2(0.5f, 0.74f),  // Hall
            new Vector2(0.84f, 0.74f), // WestCorridor
            new Vector2(0.5f, 0.38f),  // Kitchen
            new Vector2(0.84f, 0.38f), // EastCorridor
            new Vector2(0.84f, 0.56f)  // Office
        };

        private NightController night;
        private GameObject panel;
        private Button openButton;
        private GameObject[] roomViews;
        private GameObject[] monsterFigures;
        private Button[] mapButtons;
        private Text cameraTitle;
        private Text recLabel;
        private Image staticOverlay;
        private float burstUntil;

        public static CameraPanelUI Create(Transform parent, NightController night)
        {
            var holder = UiFactory.CreateArea("Cameras", parent, Vector2.zero, Vector2.one);
            var cameras = holder.gameObject.AddComponent<CameraPanelUI>();
            cameras.night = night;

            cameras.openButton = UiFactory.CreateButton("OpenCameras", holder, "CAMERAS  [Space]", 28,
                                                        new Vector2(0.38f, 0.105f), new Vector2(0.62f, 0.175f), night.ToggleCameras);

            var panelImage = UiFactory.CreateImage("Panel", holder, NightColors.Panel, new Vector2(0.23f, 0.1f), new Vector2(0.77f, 0.86f));
            panelImage.raycastTarget = true;
            cameras.panel = panelImage.gameObject;

            cameras.BuildMonitor(panelImage.transform);
            cameras.BuildMap(panelImage.transform);

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

        private void BuildMonitor(Transform panelTransform)
        {
            var monitor = UiFactory.CreateImage("Monitor", panelTransform, Color.black, new Vector2(0.02f, 0.03f), new Vector2(0.58f, 0.97f));
            RoomArt[] art = CreateRoomArt();

            roomViews = new GameObject[art.Length];
            monsterFigures = new GameObject[art.Length];
            for (int room = 0; room < art.Length; room++)
            {
                var view = UiFactory.CreateImage(art[room].Title, monitor.transform, art[room].Background, Vector2.zero, Vector2.one);
                foreach (Shape shape in art[room].Shapes)
                {
                    UiFactory.CreateImage("Shape", view.transform, shape.Color, shape.Min, shape.Max);
                }
                monsterFigures[room] = CreateMonsterFigure(view.transform, art[room].MonsterMin, art[room].MonsterMax);
                roomViews[room] = view.gameObject;
            }

            staticOverlay = UiFactory.CreateImage("Static", monitor.transform, new Color(1f, 1f, 1f, 0.05f), Vector2.zero, Vector2.one);
            cameraTitle = UiFactory.CreateLabel("Title", monitor.transform, "", 30, NightColors.Ink, TextAnchor.UpperLeft,
                                                new Vector2(0.03f, 0.85f), new Vector2(0.8f, 0.97f), FontStyle.Bold);
            recLabel = UiFactory.CreateLabel("Rec", monitor.transform, "● REC", 26, NightColors.Accent, TextAnchor.UpperRight,
                                             new Vector2(0.7f, 0.85f), new Vector2(0.97f, 0.97f), FontStyle.Bold);
        }

        private void BuildMap(Transform panelTransform)
        {
            var map = UiFactory.CreateImage("Map", panelTransform, NightColors.Background, new Vector2(0.6f, 0.03f), new Vector2(0.98f, 0.97f));
            UiFactory.CreateLabel("MapTitle", map.transform, "MAP", 26, NightColors.InkDim, TextAnchor.MiddleCenter,
                                  new Vector2(0f, 0.88f), new Vector2(1f, 0.97f), FontStyle.Bold);

            Color line = NightColors.Steel;
            Color door = NightColors.Accent;
            AddMapLine(map.transform, RoomId.Stage, RoomId.Hall, line);
            AddMapLine(map.transform, RoomId.Hall, RoomId.WestCorridor, line);
            AddMapLine(map.transform, RoomId.Hall, RoomId.Kitchen, line);
            AddMapLine(map.transform, RoomId.Kitchen, RoomId.EastCorridor, line);
            AddMapLine(map.transform, RoomId.WestCorridor, RoomId.Office, door);
            AddMapLine(map.transform, RoomId.EastCorridor, RoomId.Office, door);

            mapButtons = new Button[RoomGraph.ObservableRoomCount];
            for (int room = 0; room < mapButtons.Length; room++)
            {
                RoomId id = (RoomId)room;
                Vector2 p = MapPositions[room];
                mapButtons[room] = UiFactory.CreateButton("Cam" + (room + 1), map.transform, "CAM " + (room + 1) + "\n" + ShortName(id), 18,
                                                          p - new Vector2(0.13f, 0.065f), p + new Vector2(0.13f, 0.065f),
                                                          () => night.ViewCamera(id));
            }

            Vector2 office = MapPositions[(int)RoomId.Office];
            var you = UiFactory.CreateImage("Office", map.transform, new Color32(60, 20, 26, 255), office - new Vector2(0.09f, 0.045f), office + new Vector2(0.09f, 0.045f));
            UiFactory.CreateLabel("Label", you.transform, "YOU", 18, NightColors.Ink, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, FontStyle.Bold);

            UiFactory.CreateButton("Close", map.transform, "CLOSE  [Space]", 22, new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.14f), night.CloseCameras);
        }

        private static void AddMapLine(Transform map, RoomId a, RoomId b, Color color)
        {
            Vector2 p = MapPositions[(int)a];
            Vector2 q = MapPositions[(int)b];
            const float half = 0.006f;
            Vector2 min = new Vector2(Mathf.Min(p.x, q.x), Mathf.Min(p.y, q.y));
            Vector2 max = new Vector2(Mathf.Max(p.x, q.x), Mathf.Max(p.y, q.y));
            if (Mathf.Approximately(p.y, q.y))
            {
                min.y -= half * 1.6f;
                max.y += half * 1.6f;
            }
            else
            {
                min.x -= half;
                max.x += half;
            }
            UiFactory.CreateImage("Line", map, color, min, max);
        }

        private static GameObject CreateMonsterFigure(Transform room, Vector2 min, Vector2 max)
        {
            var figure = UiFactory.CreateArea("Monster", room, min, max);
            UiFactory.CreateImage("Body", figure, NightColors.MonsterBody, new Vector2(0.15f, 0f), new Vector2(0.85f, 0.72f));
            UiFactory.CreateImage("Head", figure, NightColors.MonsterBody, new Vector2(0.22f, 0.66f), new Vector2(0.78f, 1f));
            UiFactory.CreateImage("EarLeft", figure, NightColors.MonsterBody, new Vector2(0.2f, 0.92f), new Vector2(0.32f, 1.08f));
            UiFactory.CreateImage("EarRight", figure, NightColors.MonsterBody, new Vector2(0.68f, 0.92f), new Vector2(0.8f, 1.08f));
            UiFactory.CreateImage("EyeLeft", figure, NightColors.MonsterEyes, new Vector2(0.33f, 0.8f), new Vector2(0.43f, 0.86f));
            UiFactory.CreateImage("EyeRight", figure, NightColors.MonsterEyes, new Vector2(0.57f, 0.8f), new Vector2(0.67f, 0.86f));
            figure.gameObject.SetActive(false);
            return figure.gameObject;
        }

        private void OnMonsterMoved(RoomId from, RoomId to)
        {
            // Like the classic game: the feed glitches when something moves on the watched camera.
            CameraSystem cameras = night.State.Cameras;
            if (cameras.IsWatching(from) || cameras.IsWatching(to))
            {
                burstUntil = Time.unscaledTime + StaticBurstSeconds;
            }
        }

        private void OnCameraChanged(RoomId room)
        {
            burstUntil = Time.unscaledTime + StaticBurstSeconds;
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

            panel.SetActive(open);
            openButton.gameObject.SetActive(playing && !open);
            openButton.interactable = state.Cameras.IsAvailable;
            if (!open)
            {
                return;
            }

            RoomId watched = state.Cameras.WatchedRoom.Value;
            for (int room = 0; room < roomViews.Length; room++)
            {
                bool selected = room == (int)watched;
                roomViews[room].SetActive(selected);
                monsterFigures[room].SetActive(selected && state.MonsterRoom == (RoomId)room);
                UiFactory.SetButtonColors(mapButtons[room], selected ? NightColors.Accent : NightColors.PanelLight);
            }

            cameraTitle.text = "CAM " + ((int)watched + 1) + "  ·  " + ShortName(watched).ToUpperInvariant();
            float now = Time.unscaledTime;
            recLabel.enabled = Mathf.Repeat(now, 1f) < 0.6f;
            float alpha = now < burstUntil ? Random.Range(0.55f, 0.9f) : Random.Range(0.03f, 0.08f);
            staticOverlay.color = new Color(1f, 1f, 1f, alpha);
        }

        private static string ShortName(RoomId room)
        {
            switch (room)
            {
                case RoomId.WestCorridor: return "West Hall";
                case RoomId.EastCorridor: return "East Hall";
                case RoomId.Hall: return "Main Hall";
                default: return room.ToString();
            }
        }

        private static RoomArt[] CreateRoomArt()
        {
            Color floor = new Color32(36, 32, 30, 255);
            return new[]
            {
                new RoomArt
                {
                    Title = "Stage",
                    Background = new Color32(28, 20, 34, 255),
                    Shapes = new[]
                    {
                        new Shape(0f, 0f, 1f, 0.3f, new Color32(66, 42, 30, 255)),
                        new Shape(0f, 0.3f, 0.16f, 1f, new Color32(90, 18, 26, 255)),
                        new Shape(0.84f, 0.3f, 1f, 1f, new Color32(90, 18, 26, 255)),
                        new Shape(0.38f, 0.3f, 0.62f, 1f, new Color(1f, 0.92f, 0.6f, 0.08f)),
                        new Shape(0.2f, 0.3f, 0.26f, 0.42f, new Color32(50, 50, 60, 255)),
                    },
                    MonsterMin = new Vector2(0.42f, 0.3f),
                    MonsterMax = new Vector2(0.58f, 0.74f)
                },
                new RoomArt
                {
                    Title = "Hall",
                    Background = new Color32(30, 34, 44, 255),
                    Shapes = new[]
                    {
                        new Shape(0f, 0f, 1f, 0.26f, floor),
                        new Shape(0.08f, 0.26f, 0.24f, 0.72f, new Color32(18, 20, 26, 255)),
                        new Shape(0.76f, 0.26f, 0.92f, 0.72f, new Color32(18, 20, 26, 255)),
                        new Shape(0.3f, 0.26f, 0.38f, 0.4f, new Color32(70, 60, 50, 255)),
                        new Shape(0.36f, 0.6f, 0.64f, 0.66f, new Color32(60, 66, 80, 255)),
                    },
                    MonsterMin = new Vector2(0.44f, 0.24f),
                    MonsterMax = new Vector2(0.6f, 0.68f)
                },
                new RoomArt
                {
                    Title = "West Corridor",
                    Background = new Color32(22, 26, 30, 255),
                    Shapes = new[]
                    {
                        new Shape(0f, 0f, 1f, 0.32f, floor),
                        new Shape(0.36f, 0.32f, 0.64f, 0.78f, new Color32(8, 9, 12, 255)),
                        new Shape(0.08f, 0.5f, 0.22f, 0.74f, new Color32(120, 96, 40, 255)),
                        new Shape(0.82f, 0.62f, 0.9f, 0.66f, new Color32(90, 200, 120, 255)),
                    },
                    MonsterMin = new Vector2(0.38f, 0.18f),
                    MonsterMax = new Vector2(0.62f, 0.8f)
                },
                new RoomArt
                {
                    Title = "Kitchen",
                    Background = new Color32(30, 40, 38, 255),
                    Shapes = new[]
                    {
                        new Shape(0f, 0f, 1f, 0.3f, new Color32(80, 86, 94, 255)),
                        new Shape(0.68f, 0.3f, 0.86f, 0.86f, new Color32(150, 156, 164, 255)),
                        new Shape(0.08f, 0.62f, 0.42f, 0.66f, new Color32(70, 56, 44, 255)),
                        new Shape(0.12f, 0.66f, 0.18f, 0.74f, new Color32(160, 60, 50, 255)),
                        new Shape(0.26f, 0.66f, 0.3f, 0.72f, new Color32(200, 190, 160, 255)),
                    },
                    MonsterMin = new Vector2(0.4f, 0.3f),
                    MonsterMax = new Vector2(0.56f, 0.76f)
                },
                new RoomArt
                {
                    Title = "East Corridor",
                    Background = new Color32(26, 22, 30, 255),
                    Shapes = new[]
                    {
                        new Shape(0f, 0f, 1f, 0.32f, floor),
                        new Shape(0.36f, 0.32f, 0.64f, 0.78f, new Color32(8, 9, 12, 255)),
                        new Shape(0.78f, 0.5f, 0.92f, 0.74f, new Color32(40, 90, 130, 255)),
                        new Shape(0.1f, 0.62f, 0.18f, 0.66f, new Color32(90, 200, 120, 255)),
                    },
                    MonsterMin = new Vector2(0.38f, 0.18f),
                    MonsterMax = new Vector2(0.62f, 0.8f)
                },
            };
        }
    }
}
