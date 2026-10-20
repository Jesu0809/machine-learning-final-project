using System.Collections;
using System.IO;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Screenshot tour for the report and for checking a build without playing it:
    /// <c>NightShift.app/Contents/MacOS/NightShift -capture /some/folder</c>. It photographs the
    /// menu and several rooms, lets Freddy's brain run for a while (logging its decisions), shows
    /// the AI inspector, and quits.
    /// </summary>
    public class CaptureMode : MonoBehaviour
    {
        private HuntDirector director;
        private string folder;

        /// <summary>True while a capture tour runs (focus changes must not pause it).</summary>
        public static bool Active { get; private set; }

        public static void AttachIfRequested(HuntDirector director)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-capture")
                {
                    Active = true;
                    Application.runInBackground = true;
                    var capture = director.gameObject.AddComponent<CaptureMode>();
                    capture.director = director;
                    capture.folder = args[i + 1];
                    Directory.CreateDirectory(capture.folder);
                }
            }
        }

        private IEnumerator Start()
        {
            Debug.Log("CAPTURE start, brain: " + director.BrainDescription);
            yield return new WaitForSeconds(2f);
            yield return Shot("01-menu");

            // The story intro, photographed along the way and played to the end (it starts night 1).
            bool skipIntro = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nointro") >= 0;
            director.PlayStory();
            if (skipIntro)
            {
                yield return new WaitForSeconds(1f);
                director.SkipIntro();
            }
            float introStart = Time.time;
            (float at, string name)[] introShots =
            {
                (3f, "01b-intro-rain"), (7.8f, "01c-intro-card"), (14f, "01d-intro-road"), (23f, "01e-intro-turn"),
                (28.3f, "01f-intro-arrive"), (33f, "01g-intro-in-car"), (35.4f, "01h1-intro-out"), (37f, "01h2-intro-car-door"), (38.4f, "01h3-intro-turn"), (41.5f, "01i-intro-walk"), (47f, "01i2-intro-walk")
            };
            foreach ((float at, string name) in skipIntro ? new (float, string)[0] : introShots)
            {
                while (Time.time - introStart < at)
                {
                    yield return null;
                }
                yield return Shot(name);
            }
            int insideShot = 0;
            while (director.InIntro && Time.time - introStart < 90f)
            {
                yield return new WaitForSeconds(3f);
                if (director.InIntro)
                {
                    yield return Shot("01j-intro-end-" + insideShot++);
                }
            }
            Debug.Log("CAPTURE intro length=" + (Time.time - introStart).ToString("0.0") + " s");
            director.UI.ShowSettingsScreen();
            yield return Shot("01m-settings");
            director.UI.HideSettingsScreen();
            director.UI.ShowControlsScreen(false);
            yield return Shot("01k-controls-keyboard");
            director.UI.ShowControlsScreen(true);
            yield return Shot("01l-controls-gamepad");
            director.UI.HideControlsScreen();

            director.StartNight();
            yield return new WaitForSeconds(1f);
            const float up = PizzeriaLayout.UpperFloor;
            Teleport(PizzeriaLayout.PlayerStart, new Vector3(-1f, 0f, 0.2f));
            yield return new WaitForSeconds(7f);
            yield return Shot("02b-call");
            director.SkipCall();
            yield return new WaitForSeconds(0.5f);
            director.UI.ShowNote(StoryData.Notes[4]);
            yield return Shot("02c-note");
            director.UI.HideNote();
            // A power cell (fuse) up close, in the dark and with the flashlight off.
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                FusePickup fuse = director.Level.Fuses[room];
                if (fuse != null && fuse.gameObject.activeSelf)
                {
                    Quaternion shelf = PizzeriaLayout.FuseFacing((Room)room);
                    Vector3 stand = fuse.transform.position + shelf * Vector3.forward * 1.6f;
                    Teleport(new Vector3(stand.x, PizzeriaLayout.Elevation((Room)room), stand.z), -(shelf * Vector3.forward));
                    director.Player.Flashlight.enabled = false;
                    yield return new WaitForSeconds(0.8f);
                    yield return Shot("02f-fuse-dark");
                    director.Player.Flashlight.enabled = true;
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot("02g-fuse-flashlight");
                    break;
                }
            }
            // Upstairs.
            yield return Pose(new Vector3(-11f, up, 15f), new Vector3(-1f, 0f, 0.2f), "02-office");
            yield return Pose(new Vector3(-1f, up, 14f), new Vector3(-1f, 0f, 1f), "02d-manager");
            yield return Pose(new Vector3(3f, up, 14f), new Vector3(1f, 0f, 1f), "02e-lounge");
            yield return Pose(new Vector3(-12f, up, 9.5f), new Vector3(1f, 0f, 0f), "07-upper-hall");
            yield return Pose(new Vector3(-11f, up, 4.5f), new Vector3(-1f, 0f, -0.6f), "07b-parts");
            yield return Pose(new Vector3(11.5f, up, 4.5f), new Vector3(1f, 0f, -0.8f), "07c-maintenance");
            // The dining hall from the top of its stairs, then from the floor.
            yield return Pose(new Vector3(5.2f, up, 4.6f), new Vector3(-0.6f, -0.45f, -1f), "03-dining-balcony");
            yield return Pose(new Vector3(2f, 0f, -7f), new Vector3(-0.3f, 0.25f, 1f), "03b-dining-stairs");
            // Ground floor.
            yield return Pose(new Vector3(11f, 0f, -4f), new Vector3(1f, 0f, 1.2f), "04-arcade");
            yield return Pose(new Vector3(-9.5f, 0f, -9.5f), new Vector3(-1f, 0.05f, -0.35f), "05-stage");
            yield return Pose(new Vector3(-11f, 0f, 3f), new Vector3(-1f, 0f, -0.6f), "06-kitchen");
            yield return Pose(new Vector3(-12f, 0f, 9.5f), new Vector3(1f, 0f, 0f), "06b-back-hall");
            yield return Pose(new Vector3(16.5f, 0f, -9f), new Vector3(-0.6f, 0f, -1f), "08-exit");
            yield return Pose(new Vector3(13f, 0f, -14.2f), new Vector3(0f, 0f, -1f), "08b-front-doors");
            yield return Pose(new Vector3(-5f, 0f, -18f), new Vector3(1f, 0f, 0.8f), "08c-party-room");
            yield return Pose(new Vector3(21f, 0f, 9f), new Vector3(-1f, 0.35f, 1f), "09-storage");
            yield return Pose(new Vector3(-6f, up, 16f), new Vector3(-1f, 0f, 0f), "09b-door");
            director.Player.EnterHidingSpot(director.Level.Spot(Room.Storage, 1));
            yield return new WaitForSeconds(1.5f);
            yield return Shot("09c-inside-cabinet");
            director.Player.LeaveHidingSpot();
            director.Player.EnterHidingSpot(director.Level.Spot(Room.Office, 1));
            yield return new WaitForSeconds(1.5f);
            yield return Shot("09d-under-table");
            director.Player.LeaveHidingSpot();

            Teleport(PizzeriaLayout.PlayerStart, Vector3.left);
            director.Player.EnterHidingSpot(director.Level.Spot(Room.Office, 0));
            yield return new WaitForSeconds(1.5f);
            yield return Shot("10-inside-locker");
            director.Player.LeaveHidingSpot();

            // Air ducts: from the office into the Parts room, then down the shaft onto the stage.
            foreach (VentGrate vent in director.Level.Vents)
            {
                if (vent.EndA && (vent.Duct.A == Room.Office || vent.Duct.A == Room.Parts))
                {
                    Teleport(vent.Duct.StandA, vent.Duct.Points[0] - vent.Duct.StandA);
                    yield return new WaitForSeconds(0.5f);
                    yield return Shot("17-vent-" + vent.Duct.A);
                    vent.Use(director.Player);
                    yield return new WaitForSeconds(1.6f);
                    yield return Shot("17b-crawl-" + vent.Duct.A);
                    float crawlStart = Time.time;
                    while (director.Player.Crawling && Time.time < crawlStart + 30f)
                    {
                        yield return null;
                    }
                    yield return new WaitForSeconds(0.4f);
                    yield return Shot("17c-out-in-" + vent.Duct.B);
                    Debug.Log("CAPTURE duct " + vent.Duct.A + " -> " + vent.Duct.B + ": " + (Time.time - crawlStart).ToString("0.0") + " s, now in " + PizzeriaLayout.RoomAt(director.Player.transform.position));
                }
            }

            // Lure test: throw a party horn into the dining hall and see whether the brain goes there.
            director.Player.AddLure();
            Teleport(new Vector3(-3f, 0f, -6f), new Vector3(0f, 0.2f, 1f));
            yield return null;
            ThrownLure.Throw(director, new Vector3(-3f, 1.5f, -5.5f), new Vector3(0f, 0f, 1f));
            yield return new WaitForSeconds(1f);
            Debug.Log("CAPTURE lure: heard=" + (director.LastHeard.HasValue ? director.LastHeard.Value.ToString() : "-"));

            // Door test: close the door between the Manager's office and the security office.
            foreach (Door door in Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(door.transform.position, new Vector3(-8f, up, 16f)) < 0.5f)
                {
                    door.Use(director.Player);
                }
            }

            // Stand in the Storage and watch the brain work, with the AI inspector open.
            Teleport(new Vector3(19.5f, 0f, 12f), new Vector3(-1f, 0f, -0.3f));
            director.UI.ToggleInspector();
            string last = "";
            int decisions = 0;
            float until = Time.time + 70f;
            while (Time.time < until && director.Phase != GamePhase.Caught)
            {
                if (director.Phase == GamePhase.Paused)
                {
                    director.SetPaused(false);
                    director.Player.Controllable = false;
                }
                if (director.LastDecision != last)
                {
                    last = director.LastDecision;
                    decisions++;
                    Debug.Log("CAPTURE t=" + Time.time.ToString("0.0") + " decision=" + last + " (p=" +
                              (director.Decisions.Count > 0 ? director.Decisions[director.Decisions.Count - 1].Confidence.ToString("0.00") : "-") + ") body=" + director.Freddy.Activity +
                              " heard=" + (director.LastHeard.HasValue ? director.LastHeard.Value.ToString() : "-"));
                    if (decisions == 6)
                    {
                        yield return Shot("11-inspector");
                    }
                }
                if (Random.value < 0.01f)
                {
                    director.Player.MakeNoise(2);
                }
                yield return null;
            }

            // Chase test (the case that used to get stuck in doorways): the player waits in the
            // Storage and Freddy stands in the Arcade looking up the corridor at them.
            if (director.Phase != GamePhase.Caught)
            {
                director.UI.ToggleInspector();
                Teleport(new Vector3(18f, 0f, 11f), new Vector3(0f, 0f, -1f));
                director.Freddy.ResetTo(new Vector3(16f, 0f, 2f), 0f);
                director.Freddy.WakeUp();
                float start = Time.time;
                while (director.Phase != GamePhase.Caught && Time.time < start + 12f)
                {
                    yield return null;
                }
                Debug.Log("CAPTURE chase test: " + (director.Phase == GamePhase.Caught
                    ? "caught after " + (Time.time - start).ToString("0.0") + " s"
                    : "NOT caught in 12 s, Freddy is " + director.Freddy.Activity + " at " + director.Freddy.transform.position));
            }
            yield return new WaitForSeconds(0.35f);
            yield return Shot("12-jumpscare");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("13-jumpscare-late");
            yield return new WaitForSeconds(3f);
            yield return Shot("14-end");
            // Night 3: the title card with what changed, then the objectives on the HUD.
            director.StartNight(3);
            yield return new WaitForSeconds(2f);
            yield return Shot("15-night3-card");
            director.SkipCall();
            Teleport(PizzeriaLayout.PlayerStart, Vector3.left);
            yield return new WaitForSeconds(5f);
            yield return Shot("16-night3-hud");

 // Night 5: Freddy is awake from the start and must start hunting (he used to stay on the stage).
            director.StartNight(5);
            Vector3 freddyAtStart = director.Freddy.transform.position;
            yield return new WaitForSeconds(15f);
            Debug.Log("CAPTURE night 5: Freddy moved " + Vector3.Distance(freddyAtStart, director.Freddy.transform.position).ToString("0.0") +
                      " m in 15 s, decisions=" + director.Decisions.Count + ", activity=" + director.Freddy.Activity);
            Teleport(PizzeriaLayout.PlayerStart, Vector3.left);
            director.Player.Flashlight.enabled = false;
            yield return new WaitForSeconds(0.5f);
            yield return Shot("18-dark-no-flashlight");
            director.Player.Flashlight.enabled = true;
            yield return new WaitForSeconds(0.3f);
            yield return Shot("18b-flashlight");

            Debug.Log("CAPTURE average fps=" + (Time.frameCount / Time.realtimeSinceStartup).ToString("0"));
            Debug.Log("CAPTURE done. outcome=" + director.State.Outcome + " reason=" + director.EndReason + " decisions=" + director.Decisions.Count);
            Application.Quit();
        }

        private IEnumerator Pose(Vector3 position, Vector3 look, string name)
        {
            Teleport(position, look);
            yield return new WaitForSeconds(0.8f);
            yield return Shot(name);
        }

        private void Teleport(Vector3 position, Vector3 look)
        {
            PlayerController player = director.Player;
            // The tour drives the player; keyboard and mouse (the window may have focus) are ignored.
            player.Controllable = false;
            Cursor.lockState = CursorLockMode.None;
            float yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            player.ResetTo(position + Vector3.up * 0.05f, yaw);
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(folder, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("CAPTURE shot " + path);
            yield return null;
            yield return null;
        }
    }
}
