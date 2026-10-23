using System.IO;
using NightShift.Hunt;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace NightShift.Game
{
    /// <summary>
    /// Developer tool: the building with nobody hunting, to walk or fly through it and look at
    /// the rooms, props and lights. Freddy sleeps on the stage, every objective item is out and
    /// nothing ends the night. Open it with <c>NightShift.app/Contents/MacOS/Night Shift -explore</c>
    /// or, in the editor, Night Shift > Explore Map. It is never reachable from the game's menus.
    /// Keys: V fly / walk, Space and C up and down while flying, Shift faster, [ and ] previous and
    /// next room, L work lights, K graphics quality, P screenshot, H hide the help.
    /// </summary>
    public class ExploreMode : MonoBehaviour
    {
        public const string EditorFlag = "NightShift.Explore";
        private const float FlySpeed = 5f;

        private HuntDirector director;
        private CharacterController body;
        private bool flying;
        private float yaw;
        private float pitch;
        private int roomIndex;
        private bool workLights;
        private bool showHelp = true;
        private string message = "";
        private float messageUntil;
        private Color savedAmbient;
        private float savedReflection;
        private bool savedFog;
        private SphericalHarmonicsL2 savedProbe;
        private Light workLight;
        private GUIStyle style;

        /// <summary>True while exploring (the director runs no night: no brain, no clock, no end).</summary>
        public static bool Active { get; private set; }

        public static void AttachIfRequested(HuntDirector director)
        {
            bool requested = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-explore") >= 0;
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool(EditorFlag, false))
            {
                UnityEditor.SessionState.SetBool(EditorFlag, false);
                requested = true;
            }
#endif
            if (requested && !Active && !CaptureMode.Active)
            {
                Attach(director);
            }
        }

        /// <summary>Starts exploring (also used by the capture tour's test of this mode).</summary>
        public static ExploreMode Attach(HuntDirector director)
        {
            Active = true;
            var explore = director.gameObject.AddComponent<ExploreMode>();
            explore.director = director;
            explore.body = director.Player.GetComponent<CharacterController>();
            return explore;
        }

        private void Start()
        {
            director.BeginExplore();
            roomIndex = (int)PizzeriaLayout.RoomAt(director.Player.transform.position);
            Show("Explore mode: Freddy is asleep, nothing can end the night");
        }

        private void OnDestroy() => Active = false;

        private void Update()
        {
            Keyboard keys = Keyboard.current;
            if (keys == null || director.Phase != GamePhase.Playing)
            {
                return;
            }
            if (keys.vKey.wasPressedThisFrame)
            {
                SetFlying(!flying);
            }
            if (keys.rightBracketKey.wasPressedThisFrame)
            {
                GoToRoom(roomIndex + 1);
            }
            if (keys.leftBracketKey.wasPressedThisFrame)
            {
                GoToRoom(roomIndex - 1);
            }
            if (keys.lKey.wasPressedThisFrame)
            {
                SetWorkLights(!workLights);
            }
            if (keys.kKey.wasPressedThisFrame)
            {
                GraphicsQuality.Cycle(1);
                Show("Graphics: " + GraphicsQuality.Name(GraphicsQuality.Level));
            }
            if (keys.hKey.wasPressedThisFrame)
            {
                showHelp = !showHelp;
            }
            if (keys.pKey.wasPressedThisFrame)
            {
                Screenshot();
            }
            if (flying)
            {
                Fly(keys);
            }
        }

        private void Fly(Keyboard keys)
        {
            Vector2 look = Controls.Look;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, -89f, 89f);
            Vector2 input = Controls.Move;
            float up = (keys.spaceKey.isPressed ? 1f : 0f) - (keys.cKey.isPressed || keys.leftCtrlKey.isPressed ? 1f : 0f);
            Quaternion view = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 move = view * new Vector3(input.x, 0f, input.y) + Vector3.up * up;
            float speed = FlySpeed * (keys.leftShiftKey.isPressed ? 3f : 1f);
            director.Player.transform.position += move * speed * Time.deltaTime;
            director.Player.Head.rotation = view;
        }

        internal void SetFlying(bool on)
        {
            PlayerController player = director.Player;
            if (player.HiddenIn != null || player.Crawling)
            {
                return;
            }
            flying = on;
            if (on)
            {
                Vector3 angles = player.Head.eulerAngles;
                yaw = angles.y;
                pitch = Mathf.DeltaAngle(0f, angles.x);
                body.enabled = false;
                player.Controllable = false;
                player.CameraLocked = true;
                player.Head.localPosition = new Vector3(0f, 1.6f, 0f);
                Show("Flying: walls do not stop you");
            }
            else
            {
                // Land on whatever is below the eyes.
                Vector3 eyes = player.Head.position;
                Vector3 feet = Physics.Raycast(eyes, Vector3.down, out RaycastHit hit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    ? hit.point + Vector3.up * 0.05f
                    : new Vector3(eyes.x, 0.05f, eyes.z);
                player.ResetTo(feet, yaw);
                player.CameraLocked = false;
                player.Controllable = true;
                Show("Walking");
            }
        }

        internal void GoToRoom(int index)
        {
            roomIndex = (index % HuntMap.RoomCount + HuntMap.RoomCount) % HuntMap.RoomCount;
            var room = (Room)roomIndex;
            Vector3 center = PizzeriaLayout.Center(room);
            // Stand back from the hub, looking across the room.
            Rect area = PizzeriaLayout.Area(room);
            Vector3 corner = new Vector3(Mathf.Lerp(area.xMin, area.xMax, 0.2f), 0.05f, Mathf.Lerp(area.yMin, area.yMax, 0.2f));
            Vector3 toCenter = center - corner;
            float facing = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
            if (flying)
            {
                director.Player.transform.position = corner;
                yaw = facing;
                pitch = 0f;
            }
            else
            {
                director.Player.ResetTo(corner, facing);
            }
            Show(HuntMap.DisplayName(room));
        }

        internal void SetWorkLights(bool on)
        {
            if (on && !workLights)
            {
                savedAmbient = RenderSettings.ambientLight;
                savedProbe = RenderSettings.ambientProbe;
                savedReflection = RenderSettings.reflectionIntensity;
                savedFog = RenderSettings.fog;
                // URP reads the ambient from the probe, so set both.
                var ambient = new Color(0.3f, 0.29f, 0.28f);
                var probe = new SphericalHarmonicsL2();
                probe.AddAmbientLight(ambient);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = ambient;
                RenderSettings.ambientProbe = probe;
                RenderSettings.reflectionIntensity = 0.7f;
                RenderSettings.fog = false;
                if (workLight == null)
                {
                    workLight = new GameObject("ExploreWorkLight").AddComponent<Light>();
                    workLight.transform.SetParent(transform, false);
                    workLight.type = LightType.Directional;
                    workLight.shadows = LightShadows.None;
                    workLight.intensity = 0.6f;
                    workLight.color = new Color(1f, 0.96f, 0.9f);
                    workLight.transform.rotation = Quaternion.Euler(65f, 30f, 0f);
                }
                workLight.enabled = true;
            }
            else if (!on && workLights)
            {
                RenderSettings.ambientLight = savedAmbient;
                RenderSettings.ambientProbe = savedProbe;
                RenderSettings.reflectionIntensity = savedReflection;
                RenderSettings.fog = savedFog;
                workLight.enabled = false;
            }
            workLights = on;
            Show(on ? "Work lights on" : "Work lights off: as the player sees it");
        }

        private void Screenshot()
        {
            string folder = Path.Combine(Application.persistentDataPath, "Explore");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "explore-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            ScreenCapture.CaptureScreenshot(file);
            Debug.Log("EXPLORE screenshot " + file);
            Show("Saved " + file);
        }

        private void Show(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 3f;
        }

        private void OnGUI()
        {
            if (director == null || director.Phase != GamePhase.Playing)
            {
                return;
            }
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(13, Screen.height / 60), richText = true };
                style.normal.textColor = new Color(0.85f, 1f, 0.85f);
            }
            Vector3 position = director.Player.Head.position;
            string text = "<b>EXPLORE MODE</b>   " + HuntMap.DisplayName(PizzeriaLayout.RoomAt(position)) +
                          string.Format("   ({0:0.0}, {1:0.0}, {2:0.0})", position.x, position.y, position.z) +
                          (flying ? "   FLYING" : "") + (workLights ? "   WORK LIGHTS" : "") +
                          "   " + GraphicsQuality.Name(GraphicsQuality.Level);
            if (showHelp)
            {
                text += "\nV fly / walk    SPACE / C up, down (flying)    SHIFT faster    [ ] rooms" +
                        "\nL work lights    K graphics    P screenshot    H hide this    ESC pause";
            }
            if (Time.unscaledTime < messageUntil)
            {
                text += "\n<color=#ffd27a>" + message + "</color>";
            }
            Vector2 size = style.CalcSize(new GUIContent(text));
            // Top right, under the HUD's key hints: clear of the tracker and the prompts.
            var area = new Rect(Screen.width - size.x - 18f, Screen.height * 0.06f, size.x, size.y);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(area.x - 8f, area.y - 6f, area.width + 16f, area.height + 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(area, text, style);
        }
    }
}
