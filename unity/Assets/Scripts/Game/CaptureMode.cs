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
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-audit") >= 0)
            {
                yield return WalkAudit();
                Application.Quit();
                yield break;
            }
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
                    Teleport(new Vector3(stand.x, 0f, stand.z), -(shelf * Vector3.forward));
                    director.Player.Flashlight.enabled = false;
                    yield return new WaitForSeconds(0.8f);
                    yield return Shot("02f-fuse-dark");
                    director.Player.Flashlight.enabled = true;
                    yield return new WaitForSeconds(0.3f);
                    yield return Shot("02g-fuse-flashlight");
                    break;
                }
            }
            // Staff wing.
            yield return Pose(new Vector3(-17f, 0f, -14.5f), new Vector3(-0.5f, 0f, 1f), "02-office");
            yield return Pose(new Vector3(-17f, 0f, -11.5f), new Vector3(1f, 0f, 0f), "09b-door");
            yield return Pose(new Vector3(-9f, 0f, -13f), new Vector3(-1f, 0f, 1f), "07b-parts");
            yield return Pose(new Vector3(-3f, 0f, -8f), new Vector3(-0.5f, 0f, -1f), "07c-restrooms");
            yield return Pose(new Vector3(-21f, 0f, -1.5f), new Vector3(1f, 0f, 0f), "06b-back-hall");
            yield return Pose(new Vector3(-6f, 0f, 3f), new Vector3(-1f, 0f, 0.6f), "06-kitchen");
            yield return Pose(new Vector3(-17f, 0f, 3f), new Vector3(-1f, 0.2f, 1f), "09-storage");
            // Between the wings.
            yield return Pose(new Vector3(-4.5f, 0f, 6f), new Vector3(1f, 0f, 0f), "03b-kitchen-pass");
            yield return Pose(new Vector3(-2.5f, 0f, -1.5f), new Vector3(1f, 0f, 0f), "07d-service-corridor");
            // Public wing.
            yield return Pose(new Vector3(3f, 0f, 3f), new Vector3(0.6f, 0.1f, 1f), "03-dining");
            yield return Pose(new Vector3(7.5f, 0f, 5f), new Vector3(0f, 0.1f, 1f), "05-stage");
            yield return Pose(new Vector3(16f, 0f, 3f), new Vector3(1f, 0f, 1f), "04-arcade");
            yield return Pose(new Vector3(5f, 0f, -1.5f), new Vector3(1f, 0f, 0f), "07-main-hall");
            yield return Pose(new Vector3(9f, 0f, -8f), new Vector3(-1f, 0f, -1f), "08c-party-room");
            yield return Pose(new Vector3(13f, 0f, -10f), new Vector3(0.5f, 0f, -1f), "08-exit");
            yield return Pose(new Vector3(16f, 0f, -15.8f), new Vector3(0f, 0f, -1f), "08b-front-doors");
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

            // Air ducts: from the office into the storage, and from the parts room into the dining hall.
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
            Teleport(new Vector3(0f, 0f, 6f), new Vector3(1f, 0.2f, 0f));
            yield return null;
            ThrownLure.Throw(director, new Vector3(1f, 1.5f, 6f), new Vector3(1f, 0f, 0f));
            yield return new WaitForSeconds(1f);
            Debug.Log("CAPTURE lure: heard=" + (director.LastHeard.HasValue ? director.LastHeard.Value.ToString() : "-"));

            // Door test: close the door between the security office and the parts room.
            foreach (Door door in Object.FindObjectsByType<Door>(FindObjectsSortMode.None))
            {
                if (Vector3.Distance(door.transform.position, new Vector3(-16f, 0f, -11.5f)) < 0.5f)
                {
                    door.Use(director.Player);
                }
            }

            // Stand in the Storage and watch the brain work, with the AI inspector open.
            Teleport(new Vector3(-19f, 0f, 6f), new Vector3(1f, 0f, -0.3f));
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
            // Arcade and Freddy stands in the Main Hall looking up the corridor at them.
            if (director.Phase != GamePhase.Caught)
            {
                director.UI.ToggleInspector();
                Teleport(new Vector3(19f, 0f, 5f), new Vector3(0f, 0f, -1f));
                director.Freddy.ResetTo(new Vector3(19f, 0f, -1.5f), 0f);
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

        /// <summary>
        /// Bug audit of the map: the player's real body walks every connection of the room graph
        /// (both ways: doorways and corridors) and crawls every duct; anything that blocks it is
        /// logged with a screenshot. Run with "-capture folder -audit".
        /// </summary>
        private IEnumerator WalkAudit()
        {
            yield return new WaitForSeconds(2f);
            director.StartNight(1);
            director.SkipCall();
            director.Freddy.gameObject.SetActive(false);   // nobody gets caught during the audit
            int blocked = 0;
            int tested = 0;
            for (int a = 0; a < HuntMap.RoomCount; a++)
            {
                foreach (Room b in HuntMap.GetNeighbors((Room)a))
                {
                    var from = (Room)a;
                    var path = new System.Collections.Generic.List<Vector3> { PizzeriaLayout.Center(from) };
                    path.AddRange(PizzeriaLayout.PassageBetween(from, b));
                    // Like a player: through the last doorway first, then turn toward the middle of the room.
                    Vector3 door = path[path.Count - 1];
                    Vector3 hub = PizzeriaLayout.Center(b);
                    Rect area = PizzeriaLayout.Area(b);
                    bool onZWall = Mathf.Abs(door.z - area.yMin) < 0.05f || Mathf.Abs(door.z - area.yMax) < 0.05f;
                    path.Add(door + (onZWall ? new Vector3(0f, 0f, Mathf.Sign(hub.z - door.z)) : new Vector3(Mathf.Sign(hub.x - door.x), 0f, 0f)) * 1.2f);
                    path.Add(hub);
                    Teleport(path[0] + Vector3.up * 0.1f, path[1] - path[0]);
                    director.Player.Controllable = false;
                    yield return null;
                    bool ok = true;
                    for (int i = 1; i < path.Count && ok; i++)
                    {
                        float start = Time.time;
                        float lastProgressAt = Time.time;
                        float best = float.MaxValue;
                        while (true)
                        {
                            Vector3 here = director.Player.transform.position;
                            var flat = new Vector2(path[i].x - here.x, path[i].z - here.z);
                            if (flat.magnitude < 0.35f)
                            {
                                break;
                            }
                            if (flat.magnitude < best - 0.05f)
                            {
                                best = flat.magnitude;
                                lastProgressAt = Time.time;
                            }
                            if (Time.time - lastProgressAt > 1.5f || Time.time - start > 25f)
                            {
                                ok = false;
                                blocked++;
                                Debug.Log("CAPTURE AUDIT BLOCKED " + from + " -> " + b + " before waypoint " + i + " " + path[i] +
                                          ", stuck at " + here + " (" + PizzeriaLayout.RoomAt(here) + ")");
                                yield return Shot("audit-" + from + "-" + b);
                                break;
                            }
                            director.Player.DriveToward(path[i], 4f);
                            yield return null;
                        }
                    }
                    tested++;
                    if (ok)
                    {
                        Room reached = PizzeriaLayout.RoomAt(director.Player.transform.position);
                        float height = director.Player.transform.position.y;
                        Debug.Log("CAPTURE AUDIT ok " + from + " -> " + b + " (ended in " + reached + " at y " + height.ToString("0.0") + ")");
                        if (reached != b)
                        {
                            Debug.Log("CAPTURE AUDIT WRONG-ROOM " + from + " -> " + b + ": RoomAt says " + reached);
                        }
                    }
                }
            }
            foreach (VentGrate vent in director.Level.Vents)
            {
                Room start = vent.EndA ? vent.Duct.A : vent.Duct.B;
                Room end = vent.EndA ? vent.Duct.B : vent.Duct.A;
                Teleport(vent.EndA ? vent.Duct.StandA : vent.Duct.StandB, Vector3.forward);
                yield return null;
                vent.Use(director.Player);
                float t0 = Time.time;
                while (director.Player.Crawling && Time.time < t0 + 40f)
                {
                    yield return null;
                }
                yield return new WaitForSeconds(0.3f);
                Room reached = PizzeriaLayout.RoomAt(director.Player.transform.position);
                Debug.Log("CAPTURE AUDIT duct " + start + " -> " + end + ": " + (reached == end ? "ok" : "ENDED IN " + reached) +
                          " after " + (Time.time - t0).ToString("0.0") + " s, standing " + (director.Player.transform.position.y).ToString("0.00"));
            }
            Debug.Log("CAPTURE AUDIT done: " + tested + " walks, " + blocked + " blocked");
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
