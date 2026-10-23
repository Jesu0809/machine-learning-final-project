using System.Collections.Generic;
using NightShift.Hunt;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightShift.Game
{
    /// <summary>
    /// The office monitors show the security cameras live: what each camera in the rooms sees,
    /// Freddy included when he walks past. One small camera renders the feeds in turn (one per
    /// frame, so each screen updates at a choppy CCTV rate), only while the player is near the
    /// office. An infrared lamp on it, that only that camera sees, lights the dark rooms.
    /// </summary>
    public class SecurityFeeds : MonoBehaviour
    {
        private const int Width = 256;
        private const int Height = 192;
        private const float ActiveDistance = 9f;

        private struct View
        {
            public Room Room;
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private class Screen
        {
            public Renderer Renderer;
            public Material Material;
            public RenderTexture Target;
            public View View;
            public float RollUntil;
            public float NextRoll;
        }

        private static readonly List<View> Views = new List<View>();
        private static readonly List<Renderer> PendingScreens = new List<Renderer>();

        private readonly List<Screen> screens = new List<Screen>();
        private HuntDirector director;
        private Camera feed;
        private Light infrared;
        private int next;

        /// <summary>A camera in a room, registered while the level is built.</summary>
        public static void RegisterCamera(Room room, Vector3 position, Quaternion rotation)
        {
            Views.Add(new View { Room = room, Position = position, Rotation = rotation });
        }

        /// <summary>A monitor's screen, registered while the level is built; it gets a feed later.</summary>
        public static void RegisterScreen(Renderer screen) => PendingScreens.Add(screen);

        public static SecurityFeeds Create(HuntDirector director)
        {
            var go = new GameObject("SecurityFeeds");
            go.transform.SetParent(director.transform, false);
            var feeds = go.AddComponent<SecurityFeeds>();
            feeds.director = director;
            feeds.Build();
            return feeds;
        }

        private void Build()
        {
            var views = new List<View>();
            foreach (View view in Views)
            {
                if (view.Room != Room.Office)
                {
                    views.Add(view);
                }
            }
            Views.Clear();
            if (views.Count == 0)
            {
                PendingScreens.Clear();
                return;
            }

            feed = new GameObject("FeedCamera").AddComponent<Camera>();
            feed.transform.SetParent(transform, false);
            feed.fieldOfView = 75f;
            feed.nearClipPlane = 0.1f;
            feed.farClipPlane = 22f;
            feed.depth = -10f;
            feed.clearFlags = CameraClearFlags.SolidColor;
            feed.backgroundColor = Color.black;
            feed.allowMSAA = false;
            feed.allowHDR = false;
            feed.enabled = false;
            UniversalAdditionalCameraData data = feed.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.requiresDepthTexture = false;
            data.requiresColorTexture = false;
            data.antialiasing = AntialiasingMode.None;

            infrared = new GameObject("Infrared").AddComponent<Light>();
            infrared.transform.SetParent(feed.transform, false);
            infrared.type = LightType.Spot;
            infrared.spotAngle = 100f;
            infrared.range = 18f;
            infrared.intensity = 5f;
            infrared.color = new Color(0.85f, 1f, 0.9f);
            infrared.shadows = LightShadows.None;
            infrared.enabled = false;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;

            for (int i = 0; i < PendingScreens.Count; i++)
            {
                Renderer renderer = PendingScreens[i];
                if (renderer == null)
                {
                    continue;
                }
                var target = new RenderTexture(Width, Height, 16, RenderTextureFormat.ARGB32) { name = "Feed " + i, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Repeat };
                // Green-grey CCTV, bright enough to bloom a little in the dark office.
                Material material = BuildKit.Unlit(target, new Color(0.8f, 1f, 0.85f) * 1.4f);
                material.name = "Feed " + i;
                renderer.sharedMaterial = material;
                screens.Add(new Screen
                {
                    Renderer = renderer, Material = material, Target = target, View = views[i % views.Count],
                    NextRoll = Time.time + Random.Range(4f, 15f)
                });
                AddOverlay(renderer.transform, views[i % views.Count].Room);
            }
            PendingScreens.Clear();
        }

        /// <summary>Camera number, room name and the REC light, drawn on the glass.</summary>
        private static void AddOverlay(Transform screen, Room room)
        {
            string key = "cctv_" + System.Text.RegularExpressions.Regex.Replace(HuntMap.DisplayName(room).ToLowerInvariant(), "[^a-z]+", "_").Trim('_');
            Texture2D texture = BuildKit.SignTexture(key);
            if (texture == null)
            {
                return;
            }
            var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(overlay.GetComponent<Collider>());
            overlay.name = "Overlay";
            overlay.transform.SetParent(screen, false);
            overlay.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            overlay.GetComponent<Renderer>().sharedMaterial = BuildKit.Stain("overlay:" + key, texture, Color.white);
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == feed)
            {
                infrared.enabled = true;
            }
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == feed)
            {
                infrared.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (feed == null || screens.Count == 0)
            {
                return;
            }
            Vector3 player = director.Player.transform.position;
            Vector3 office = PizzeriaLayout.Center(Room.Office);
            bool watching = PizzeriaLayout.RoomAt(player) == Room.Office || Vector3.Distance(player, office) < ActiveDistance;
            feed.enabled = watching;
            if (!watching)
            {
                return;
            }
            // One feed per frame, in turn.
            next = (next + 1) % screens.Count;
            Screen screen = screens[next];
            feed.transform.SetPositionAndRotation(screen.View.Position, screen.View.Rotation);
            feed.targetTexture = screen.Target;

            // Now and then the picture rolls, like an old monitor losing sync.
            foreach (Screen s in screens)
            {
                if (Time.time >= s.NextRoll)
                {
                    s.RollUntil = Time.time + 0.35f;
                    s.NextRoll = Time.time + Random.Range(6f, 20f);
                }
                float roll = Time.time < s.RollUntil ? 1f - (s.RollUntil - Time.time) / 0.35f : 0f;
                s.Material.SetTextureOffset("_BaseMap", new Vector2(0f, roll));
            }
        }
    }
}
