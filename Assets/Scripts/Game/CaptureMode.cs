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
            director.PlayStory();
            float introStart = Time.time;
            (float at, string name)[] introShots =
            {
                (3f, "01b-intro-rain"), (7.8f, "01c-intro-card"), (14f, "01d-intro-road"), (23f, "01e-intro-turn"),
                (28.3f, "01f-intro-arrive"), (33f, "01g-intro-in-car"), (35.4f, "01h1-intro-out"), (37f, "01h2-intro-car-door"), (38.4f, "01h3-intro-turn"), (41.5f, "01i-intro-walk"), (47f, "01i2-intro-walk")
            };
            foreach ((float at, string name) in introShots)
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
            director.UI.ShowControlsScreen(false);
            yield return Shot("01k-controls-keyboard");
            director.UI.ShowControlsScreen(true);
            yield return Shot("01l-controls-gamepad");
            director.UI.HideControlsScreen();

            director.StartNight();
            yield return new WaitForSeconds(1f);
            Teleport(new Vector3(-14f, 0f, 9.5f), new Vector3(1f, 0f, 0.4f));
            yield return new WaitForSeconds(7f);
            yield return Shot("02b-call");
            director.SkipCall();
            yield return new WaitForSeconds(0.5f);
            director.UI.ShowNote(StoryData.Notes[4]);
            yield return Shot("02c-note");
            director.UI.HideNote();
            yield return Pose(new Vector3(-15f, 0f, 9f), new Vector3(1f, 0f, 0.6f), "02-office");
            yield return Pose(new Vector3(-7.5f, 0f, 5.5f), new Vector3(1f, 0f, -0.7f), "03-dining");
            yield return Pose(new Vector3(10f, 0f, -4.5f), new Vector3(1f, 0f, 1.2f), "04-arcade");
            yield return Pose(new Vector3(-9.5f, 0f, -9.5f), new Vector3(-1f, 0.05f, -0.35f), "05-stage");
            yield return Pose(new Vector3(-10f, 0f, 4.8f), new Vector3(-1f, 0f, -0.8f), "06-kitchen");
            yield return Pose(new Vector3(-7f, 0f, 11f), new Vector3(1f, 0f, 0f), "07-hall");
            yield return Pose(new Vector3(16.5f, 0f, -9f), new Vector3(-0.6f, 0f, -1f), "08-exit");
            yield return Pose(new Vector3(13f, 0f, -14.2f), new Vector3(0f, 0f, -1f), "08b-front-doors");
            yield return Pose(new Vector3(9.2f, 0f, 9f), new Vector3(1f, 0f, 1f), "09-storage");
            yield return Pose(new Vector3(-11.8f, 0f, 10.4f), new Vector3(1f, 0f, 0.15f), "09b-door");
            director.Player.EnterHidingSpot(director.Level.Spot(Room.Storage, 1));
            yield return new WaitForSeconds(1.5f);
            yield return Shot("09c-inside-cabinet");
            director.Player.LeaveHidingSpot();
            director.Player.EnterHidingSpot(director.Level.Spot(Room.Office, 1));
            yield return new WaitForSeconds(1.5f);
            yield return Shot("09d-under-table");
            director.Player.LeaveHidingSpot();

            Teleport(new Vector3(-12f, 0f, 11.5f), Vector3.right);
            director.Player.EnterHidingSpot(director.Level.Spot(Room.Office, 0));
            yield return new WaitForSeconds(1.5f);
            yield return Shot("10-inside-locker");
            director.Player.LeaveHidingSpot();

            // Lure test: throw a party horn into the Kitchen and see whether the brain goes there.
            director.Player.AddLure();
            Teleport(new Vector3(-12f, 0f, 9f), new Vector3(0f, 0.2f, -1f));
            yield return null;
            ThrownLure.Throw(director, new Vector3(-12f, 1.5f, 8.5f), new Vector3(0f, 0f, -1f));
            yield return new WaitForSeconds(1f);
            Debug.Log("CAPTURE lure: heard=" + (director.LastHeard.HasValue ? director.LastHeard.Value.ToString() : "-"));

            // Door test: close the door between the Office and the North Hall.
            foreach (Door door in Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(door.transform.position, new Vector3(-8f, 0f, 11f)) < 0.5f)
                {
                    door.Use(director.Player);
                }
            }

            // Stand in the Storage and watch the brain work, with the AI inspector open.
            Teleport(new Vector3(11f, 0f, 12f), new Vector3(-1f, 0f, -0.3f));
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
                Teleport(new Vector3(11f, 0f, 12f), new Vector3(0.3f, 0f, -1f));
                director.Freddy.ResetTo(new Vector3(12.5f, 0f, 3.5f), 0f);
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
            Teleport(new Vector3(-12f, 0f, 11.5f), Vector3.right);
            yield return new WaitForSeconds(5f);
            yield return Shot("16-night3-hud");

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
