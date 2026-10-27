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
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-itemtest") >= 0)
            {
                yield return ItemTest();
                Application.Quit();
                yield break;
            }
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
            // The photo tour teleports through every room: Freddy waits on his stage meanwhile.
            director.Freddy.gameObject.SetActive(false);
            yield return new WaitForSeconds(7f);
            yield return Shot("02b-call");
            director.SkipCall();
            yield return new WaitForSeconds(0.5f);
            director.UI.ShowNote(StoryData.Notes[4]);
            yield return Shot("02c-note");
            director.UI.HideNote();
            // A power cell up close, in the dark and with the flashlight off.
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                CellPickup fuse = director.Level.Cells[room];
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
            yield return Pose(new Vector3(-18.5f, 0f, 6.5f), new Vector3(-1f, 0.05f, 0f), "09e-loading-door");
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
                    yield return new WaitForSeconds(0.45f);
                    yield return Shot("17a-kneel-" + vent.Duct.A);
                    yield return new WaitForSeconds(1.15f);
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

            yield return PanelTest();
            director.Freddy.gameObject.SetActive(true);
            director.Freddy.ResetTo(PizzeriaLayout.FreddyStart, PizzeriaLayout.FreddyFacing);

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
        /// The exit, night 1: every cell carried to a socket, then the lever pulled. The panel is
        /// reset afterwards so the tour can go on.
        /// </summary>
        private IEnumerator PanelTest()
        {
            yield return PowerTheSockets(null);
            yield return PullTheLever("19");
            ExitPanel panel = director.ActiveExit;
            panel.ResetPanel();
            panel.Active = true;
        }

        /// <summary>
        /// The power cells start to finish, the way a player does it: each one off its shelf into
        /// the hand, carried to a socket it fits and pushed in. Shots of the first one if
        /// <paramref name="prefix"/> is set.
        /// </summary>
        private IEnumerator PowerTheSockets(string prefix)
        {
            PlayerController player = director.Player;
            for (int cell = 0; cell < director.CellsNeeded; cell++)
            {
                Room? room = director.State.CellRoom(cell);
                if (!room.HasValue)
                {
                    continue;
                }
                bool shots = prefix != null && cell == 0;
                CellPickup pickup = director.Level.Cells[(int)room.Value];
                Quaternion shelf = PizzeriaLayout.FuseFacing(room.Value);
                Vector3 stand = pickup.transform.position + shelf * Vector3.forward * 1.3f;
                Teleport(new Vector3(stand.x, 0f, stand.z), -(shelf * Vector3.forward));
                yield return Aim(pickup.transform.position);
                if (shots)
                {
                    yield return Shot(prefix + "a-cell-shelf");
                }
                pickup.Use(player);
                yield return new WaitForSeconds(0.7f);
                if (shots)
                {
                    yield return Shot(prefix + "b-cell-in-hand");
                }
                int socket = 0;
                while (socket < director.Level.Sockets.Length && !director.State.CellFits(cell, socket))
                {
                    socket++;
                }
                PowerSocket target = director.Level.Sockets[socket];
                Vector3 front = target.transform.position + target.transform.forward * 1.1f;
                Teleport(new Vector3(front.x, 0f, front.z), -target.transform.forward);
                yield return Aim(target.transform.position);
                if (shots)
                {
                    yield return Shot(prefix + "c-socket");
                }
                target.Use(player);
                yield return new WaitForSeconds(0.4f);
                if (shots)
                {
                    yield return Shot(prefix + "d-socket-inserting");
                }
                yield return new WaitForSeconds(0.7f);
                if (shots)
                {
                    yield return Shot(prefix + "e-socket-powered");
                }
            }
            Debug.Log("CAPTURE cells inserted=" + director.State.CellsInserted + " of " + director.CellsNeeded);
        }

        private IEnumerator PullTheLever(string prefix)
        {
            ExitPanel panel = director.ActiveExit;
            Vector3 forward = panel.transform.forward;
            Vector3 stand = panel.transform.position + forward * 0.95f;
            Teleport(new Vector3(stand.x, 0f, stand.z), -forward);
            yield return Aim(panel.transform.position);
            yield return Shot(prefix + "a-panel");
            panel.Use(director.Player);
            yield return new WaitForSeconds(0.7f);
            yield return Shot(prefix + "b-lever");
            yield return new WaitForSeconds(1.4f);
            yield return Shot(prefix + "c-open");
            Debug.Log("CAPTURE exit open=" + panel.Open + " unlocked=" + director.State.ExitUnlocked);
        }

        /// <summary>
        /// Night 5 start to finish, every task photographed: the cells, the breakers, the keycard
        /// and its reader (one swipe too fast, then a good one), the tape box (a wrong key, then
        /// 1 to 9) and the lever. Then a locker, a cabinet and the explore mode. Run with
        /// "-capture folder -itemtest". Freddy is left out.
        /// </summary>
        private IEnumerator ItemTest()
        {
            PlayerController player = director.Player;
            yield return new WaitForSeconds(1f);
            director.StartNight(5);
            director.SkipCall();
            director.Freddy.gameObject.SetActive(false);
            yield return new WaitForSeconds(0.5f);
            yield return PowerTheSockets("30");

            BreakerBox breaker = director.Breaker;
            Vector3 front = breaker.transform.forward;
            Vector3 stand = breaker.transform.position + front * 1.1f;
            Teleport(new Vector3(stand.x, 0f, stand.z), -front);
            yield return Aim(breaker.transform.position);
            yield return Shot("31a-breaker-closed");
            breaker.Use(player);
            yield return new WaitForSeconds(1f);
            yield return Shot("31b-breaker-focus");
            StartCoroutine(breaker.AutoPlay(player));
            yield return new WaitForSeconds(1.2f);
            yield return Shot("31c-breaker-flipping");
            yield return new WaitForSeconds(2.2f);
            yield return Shot("31d-breaker-done");
            Debug.Log("CAPTURE breaker done=" + director.State.BreakerDone);

            Vector3 pedestal = PizzeriaLayout.CardPedestal;
            Teleport(new Vector3(pedestal.x, 0f, pedestal.z - 1.3f), Vector3.forward);
            yield return Aim(pedestal + Vector3.up * 1.05f);
            yield return Shot("32a-pedestal");
            director.CardPedestal.Use(player);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("32b-card-taken");
            yield return new WaitForSeconds(0.6f);

            CardReader reader = director.Reader;
            front = reader.transform.forward;
            stand = reader.transform.position + front * 1.0f;
            Teleport(new Vector3(stand.x, 0f, stand.z), -front);
            yield return Aim(reader.transform.position);
            yield return Shot("33a-reader");
            reader.Use(player);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("33b-reader-focus");
            reader.Judge(0.1f);
            yield return new WaitForSeconds(0.2f);
            yield return Shot("33c-reader-too-fast");
            yield return new WaitForSeconds(1.4f);
            reader.Judge(0.6f);
            yield return new WaitForSeconds(0.3f);
            yield return Shot("33d-reader-accepted");
            yield return new WaitForSeconds(1.1f);
            Debug.Log("CAPTURE card swiped=" + director.State.CardSwiped);

            TapeBox box = director.Tape;
            front = box.transform.forward;
            stand = box.transform.position + front * 1.0f;
            Teleport(new Vector3(stand.x, 0f, stand.z), -front);
            yield return Aim(box.transform.position);
            yield return Shot("34-tapebox");
            yield return box.AutoPlay(player, "34", Shot);
            yield return Shot("34d-tapebox-open");
            box.Use(player);
            yield return new WaitForSeconds(0.35f);
            yield return Shot("34e-tape-taken");
            yield return new WaitForSeconds(0.6f);
            Debug.Log("CAPTURE tape taken=" + director.State.TapeTaken + ", objectives " + director.State.ObjectivesDone + "/" + director.State.ObjectivesTotal);

            yield return PullTheLever("35");

            // Into the office locker and out again.
            HidingSpot locker = director.Level.Spot(Room.Office, 0);
            Vector3 lockerFront = locker.ExitPoint.position + locker.transform.forward * 0.6f;
            Teleport(new Vector3(lockerFront.x, 0f, lockerFront.z), -locker.transform.forward);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("23a-locker");
            locker.Use(director.Player);
            foreach (float wait in new[] { 0.3f, 0.45f, 0.3f, 0.3f, 0.4f, 2.2f })
            {
                yield return new WaitForSeconds(wait);
                yield return Shot("23b-locker-in-" + Mathf.RoundToInt(Time.time * 100f) % 1000);
            }
            locker.Use(director.Player);
            foreach (float wait in new[] { 0.25f, 0.6f })
            {
                yield return new WaitForSeconds(wait);
                yield return Shot("23c-locker-out-" + Mathf.RoundToInt(Time.time * 100f) % 1000);
            }
            // And a cabinet (crouching in).
            HidingSpot cabinet = director.Level.Spot(Room.Parts, 1);
            Vector3 cabinetFront = cabinet.ExitPoint.position + cabinet.transform.forward * 0.6f;
            Teleport(new Vector3(cabinetFront.x, 0f, cabinetFront.z), -cabinet.transform.forward);
            yield return new WaitForSeconds(0.6f);
            cabinet.Use(director.Player);
            foreach (float wait in new[] { 0.4f, 0.5f, 0.5f, 2.5f })
            {
                yield return new WaitForSeconds(wait);
                yield return Shot("23d-cabinet-in-" + Mathf.RoundToInt(Time.time * 100f) % 1000);
            }
            cabinet.Use(director.Player);
            yield return new WaitForSeconds(1f);

            // The explore mode, with and without work lights.
            ExploreMode explore = ExploreMode.Attach(director);
            yield return new WaitForSeconds(1f);
            explore.GoToRoom((int)Room.DiningHall);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("24a-explore");
            explore.SetWorkLights(true);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("24b-explore-work-lights");
            explore.GoToRoom((int)Room.Kitchen);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("24c-explore-kitchen");
            explore.GoToRoom((int)Room.Storage);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("24d-explore-storage");
            explore.GoToRoom((int)Room.Parts);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("24e-explore-parts");
            explore.SetFlying(true);
            director.Player.transform.position += Vector3.up * 1.5f;
            yield return new WaitForSeconds(0.5f);
            yield return Shot("24f-explore-flying");
            explore.SetFlying(false);
            foreach ((Vector3 spot, Vector3 look, string name) in new[]
            {
                (new Vector3(-10.4f, 0f, 5.4f), new Vector3(-12.4f, 1.1f, 7.6f), "24g-kitchen-fridge"),
                (new Vector3(-7.6f, 0f, 7.2f), new Vector3(-7.75f, 0.8f, 9.3f), "24h-kitchen-counter"),
                (new Vector3(-12.4f, 0f, -10.6f), new Vector3(-12.9f, 0.9f, -8.9f), "24i-parts-workbench"),
                (new Vector3(-19.4f, 0f, 4.6f), new Vector3(-21.1f, 0.4f, 3.2f), "24j-storage-generator"),
                (new Vector3(-11.2f, 0f, -11.7f), new Vector3(-11.4f, 1.05f, -13.3f), "24k-parts-heads"),
                (new Vector3(-19.6f, 0f, -10.6f), new Vector3(-21.6f, 1.4f, -10.75f), "24l-office-monitors"),
                (new Vector3(-20.4f, 0f, -11.3f), new Vector3(-21.4f, 1.55f, -11.3f), "24m-office-monitor-close"),
                (new Vector3(-17f, 0f, -12f), new Vector3(-17f, 1.6f, -9.3f), "24n-office-north-wall"),
                (new Vector3(16f, 0f, -14.6f), new Vector3(16f, 1.7f, -17f), "24o-front-doors"),
                (new Vector3(-18.6f, 0f, 3.6f), new Vector3(-16.45f, 2.1f, 3.6f), "24p-head-shelf"),
                (new Vector3(-10f, 0f, 18.5f), new Vector3(2f, 1.2f, 18.5f), "26a-banquet-east"),
                (new Vector3(5f, 0f, 18.5f), new Vector3(-10f, 1.2f, 18.5f), "26b-banquet-west"),
                (new Vector3(13f, 0f, 18.5f), new Vector3(21f, 1.2f, 18.5f), "26c-veranda"),
                (new Vector3(-6.3f, 0f, 7f), new Vector3(-6.3f, 2.4f, 10f), "26d-kitchen-to-banquet"),
                (new Vector3(18f, 0f, 7f), new Vector3(18f, 2.4f, 10f), "26e-arcade-to-veranda"),
                (new Vector3(19f, 0f, 4.5f), new Vector3(18f, 1.2f, 9.5f), "26f-arcade-north"),
                (new Vector3(-11f, 0f, 19.5f), new Vector3(-11f, 2.4f, 15f), "26g-banquet-sign"),
                (new Vector3(15f, 0f, 19.5f), new Vector3(14.6f, 2.4f, 15f), "26h-veranda-sign"),
                (new Vector3(-17f, 0f, -2f), new Vector3(-17f, 1.65f, -0.2f), "26i-directory"),
                (new Vector3(-24.6f, 0f, 7.5f), new Vector3(-29f, 1.4f, 3f), "25a-alley"),
                (new Vector3(-26.5f, 0f, 13f), new Vector3(-24f, 1f, 2f), "25b-alley-south"),
                (new Vector3(-10.5f, 0f, 6.2f), new Vector3(-10.8f, 1f, 9.5f), "25c-kitchen-row"),
                (new Vector3(-20.8f, 0f, -10.75f), new Vector3(-21.58f, 1.53f, -10.75f), "25d-tv-head-on"),
                (new Vector3(7.6f, 0f, -9.6f), new Vector3(9.5f, 0.3f, -7.9f), "25e-party-gifts"),
                (new Vector3(14.6f, 0f, -13.2f), new Vector3(13.2f, 1.3f, -9.9f), "25f-ticket-booth"),
                (new Vector3(14.8f, 0f, -12.6f), new Vector3(12.8f, 0.4f, -12.6f), "25g-lost-found"),
                (new Vector3(-4.2f, 0f, -9f), new Vector3(-2.4f, 1.1f, -9f), "25h-restroom-sink"),
                (new Vector3(-19.4f, 0f, -12.8f), new Vector3(-21.2f, 0.8f, -12.8f), "25i-filing-cabinet"),
                (new Vector3(-17.8f, 0f, -11.6f), new Vector3(-17.8f, 2.5f, -9.2f), "25j-clock"),
                (new Vector3(4.25f, 0f, 6.3f), new Vector3(4.25f, 0.8f, 8.25f), "25k-party-table"),
            })
            {
                Teleport(spot, look - spot);
                yield return Aim(look);
                yield return Shot(name);
            }
            Debug.Log("CAPTURE itemtest done");
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

        /// <summary>Turns the player's view onto a point (the tour teleports with level eyes).</summary>
        private IEnumerator Aim(Vector3 point)
        {
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                director.Player.LookToward(point, 12f);
                yield return null;
            }
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
