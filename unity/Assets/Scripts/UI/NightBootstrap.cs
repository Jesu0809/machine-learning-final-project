using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace NightShift.UI
{
    /// <summary>
    /// Builds the whole Night scene from code when the scene starts: camera, event system, audio,
    /// canvas, office, camera monitor, HUD, end-of-night screens, menu and CRT overlay. The scene
    /// itself only needs one GameObject with a <see cref="NightController"/> and this component.
    /// Pictures, sounds and the font come from the <see cref="NightArt"/> catalog in Resources.
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

        /// <summary>Creates everything. Call it only once per scene.</summary>
        public void Build()
        {
            var night = GetComponent<NightController>();
            night.EnsureState();

            EnsureCamera();
            EnsureEventSystem();
            Canvas = CreateCanvas();
            Transform root = Canvas.transform;

            NightArt art = NightArt.Load();
            if (art == null)
            {
                Debug.LogError("NightArt catalog not found. Run 'Night Shift/Rebuild Art Catalog' in the Unity menu.");
                UiFactory.CreateLabel("Error", root, "Missing art catalog.\nRun Night Shift > Rebuild Art Catalog.", 48,
                                      NightColors.Accent, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
                return;
            }

            NightAudio audioPlayer = NightAudio.Create(transform, night, art);

            // Creation order is drawing order: later objects are drawn on top.
            OfficeViewUI.Create(root, night, art, audioPlayer);
            CameraPanelUI.Create(root, night, art, audioPlayer);
            HudUI.Create(root, night);
            JumpscareUI.Create(root, night, art, audioPlayer);
            MenuUI.Create(root, night, art);
            CrtOverlayUI.Create(root);
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
    }
}
