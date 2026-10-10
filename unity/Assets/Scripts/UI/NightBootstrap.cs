using NightShift.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace NightShift.UI
{
    /// <summary>
    /// Builds the whole Night scene UI from code when the scene starts: camera, event system,
    /// canvas, office view, doors, camera panel, HUD, end-of-night screens and menu. The scene
    /// itself only needs one GameObject with a <see cref="NightController"/> and this component.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(NightController))]
    public class NightBootstrap : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        /// <summary>The canvas that holds every screen.</summary>
        public Canvas Canvas { get; private set; }

        private void Awake()
        {
            Build();
        }

        /// <summary>Creates everything. Safe to call only once per scene.</summary>
        public void Build()
        {
            var night = GetComponent<NightController>();
            night.EnsureState();

            EnsureCamera();
            EnsureEventSystem();
            Canvas = CreateCanvas();
            Transform root = Canvas.transform;

            // Creation order is drawing order: later objects are drawn on top.
            CreateOffice(root);
            DoorButtonUI.Create(root, night, DoorSide.Left);
            DoorButtonUI.Create(root, night, DoorSide.Right);
            CameraPanelUI.Create(root, night);
            HudUI.Create(root, night);
            JumpscareUI.Create(root, night);
            MenuUI.Create(root, night);
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null)
            {
                return;
            }
            var go = new GameObject("Main Camera", typeof(Camera));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private Canvas CreateCanvas()
        {
            var go = new GameObject("NightCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void CreateOffice(Transform root)
        {
            var office = UiFactory.CreateArea("Office", root, Vector2.zero, Vector2.one);
            UiFactory.CreateImage("Background", office, NightColors.Background, Vector2.zero, Vector2.one);
            UiFactory.CreateImage("BackWall", office, NightColors.Wall, new Vector2(0f, 0.2f), new Vector2(1f, 1f));
            UiFactory.CreateImage("Floor", office, new Color32(16, 14, 14, 255), new Vector2(0f, 0f), new Vector2(1f, 0.2f));

            // Desk with an old monitor and some clutter.
            UiFactory.CreateImage("Desk", office, new Color32(54, 40, 30, 255), new Vector2(0.27f, 0.2f), new Vector2(0.73f, 0.34f));
            UiFactory.CreateImage("DeskTop", office, new Color32(74, 56, 42, 255), new Vector2(0.26f, 0.33f), new Vector2(0.74f, 0.36f));
            UiFactory.CreateImage("Monitor", office, new Color32(30, 32, 36, 255), new Vector2(0.42f, 0.36f), new Vector2(0.58f, 0.52f));
            UiFactory.CreateImage("Screen", office, new Color32(18, 40, 30, 255), new Vector2(0.435f, 0.38f), new Vector2(0.565f, 0.5f));
            UiFactory.CreateImage("Cup", office, new Color32(170, 160, 150, 255), new Vector2(0.62f, 0.36f), new Vector2(0.645f, 0.41f));
            UiFactory.CreateImage("Fan", office, new Color32(60, 64, 70, 255), new Vector2(0.31f, 0.36f), new Vector2(0.36f, 0.45f));

            // Posters on the back wall.
            UiFactory.CreateImage("Poster", office, new Color32(110, 40, 46, 255), new Vector2(0.3f, 0.6f), new Vector2(0.38f, 0.78f));
            UiFactory.CreateImage("Poster2", office, new Color32(46, 70, 110, 255), new Vector2(0.62f, 0.62f), new Vector2(0.69f, 0.76f));
            UiFactory.CreateLabel("Sign", office, "SECURITY OFFICE", 28, NightColors.InkDim, TextAnchor.MiddleCenter,
                                  new Vector2(0.4f, 0.8f), new Vector2(0.6f, 0.85f), FontStyle.Bold);
        }
    }
}
