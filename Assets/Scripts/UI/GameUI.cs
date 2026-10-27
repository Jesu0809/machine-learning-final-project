using System.Collections.Generic;
using NightShift.Game;
using NightShift.Hunt;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// Every screen of the game, built in code: main menu (with the ML explanation, controls and
    /// credits), HUD (clock, fuses, prompt, motion tracker, breath), AI inspector, pause menu,
    /// jumpscare flash and end screens.
    /// </summary>
    public class GameUI : MonoBehaviour
    {

        private HuntDirector director;

        private GameObject menu;
        private GameObject menuInfo;
        private Text menuInfoTitle;
        private Text menuInfoBody;
        private Button musicButton;
        private Button soundButton;

        private GameObject hud;
        private Text clock;
        private Text fuses;
        private Text prompt;
        private Text keysLabel;
        private Text breathHint;
        private Text noteClose;
        private GameObject settingsScreen;
        private Text mouseValue;
        private Text stickValue;
        private Button invertButton;
        private readonly Dictionary<InputAction, Button> keyButtons = new Dictionary<InputAction, Button>();
        private InputAction? rebinding;
        private Text cinematicCard;
        private Text cinematicSkip;
        private GameObject controlsScreen;
        private Image controlsPicture;
        private Button keyboardTab;
        private Button gamepadTab;
        private Text toast;
        private float toastUntil;
        private RectTransform trackerBlip;
        private Image trackerBlipImage;
        private Text trackerLabel;
        private GameObject breath;
        private Image breathFill;
        private GameObject lockerView;
        private GameObject staminaBar;
        private Image staminaFill;
        private Image fearView;
        private Text subtitle;
        private Text items;
        private Image fearMeter;
        private Image senseFlash;
        private Text senseText;
        private Image mapSensed;
        private Text titleCard;
        private Button continueButton;
        private GameObject explainerCharts;
        private Text crouchHint;
        private Button againButton;
        private Button nextButton;
        private GameObject crouchView;

        private GameObject inspector;
        private MapView inspectorMap;
        private Image mapFreddy;
        private Image mapPlayer;
        private Image mapSeen;
        private readonly Text[] optionLabels = new Text[5];
        private readonly Image[] optionBars = new Image[5];
        private Text inspectorStatus;
        private Text inspectorLog;
        private readonly int[] sortedActions = new int[FreddyActions.Count];

        private GameObject pause;
        private Button pauseMusic;
        private Button pauseSound;
        private Button pauseInspector;

        private GameObject end;
        private Text endTitle;
        private Text endReason;
        private Text endBody;
        private MapView endMap;
        private Image endCatch;
        private Text endTimeline;

        private Image flash;
        private float flashAlpha;
        private RawImage staticNoise;
        private float staticUntil;

        public bool InspectorVisible { get; private set; }

        public static GameUI Create(HuntDirector director)
        {
            UiKit.Font = director.Assets.Font != null ? director.Assets.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            UiKit.EnsureEventSystem();
            var ui = new GameObject("UI").AddComponent<GameUI>();
            ui.transform.SetParent(director.transform, false);
            ui.director = director;
            Transform root = UiKit.CreateCanvas("Canvas", ui.transform, 10).transform;
            ui.BuildHud(root);
            ui.BuildInspector(root);
            ui.BuildMenu(root);
            ui.BuildPause(root);
            ui.BuildEnd(root);
            ui.BuildControls(root);
            ui.BuildSettings(root);
            ui.BuildCinematic(root);
            ui.flash = UiKit.Panel("Flash", root, Vector2.zero, Vector2.one, new Color(0.6f, 0f, 0f, 0f));
            ui.flash.raycastTarget = false;
            ui.staticNoise = new GameObject("Static", typeof(RectTransform)).AddComponent<RawImage>();
            ui.staticNoise.transform.SetParent(root, false);
            RectTransform noiseRect = ui.staticNoise.rectTransform;
            noiseRect.anchorMin = Vector2.zero;
            noiseRect.anchorMax = Vector2.one;
            noiseRect.offsetMin = noiseRect.offsetMax = Vector2.zero;
            ui.staticNoise.texture = ProceduralTextures.Static();
            ui.staticNoise.raycastTarget = false;
            ui.staticNoise.enabled = false;
            return ui;
        }

        // ------------------------------------------------------------------ screens

        public void ShowMenu()
        {
            SetScreens(menu: true);
            menuInfo.SetActive(false);
            int best = HuntDirector.BestNight;
            continueButton.gameObject.SetActive(best > 1);
            UiKit.SetButtonText(continueButton, "CONTINUE  (NIGHT " + best + ")");
            RefreshSoundButtons();
        }

        public void ShowHud()
        {
            SetScreens(hud: true);
            inspector.SetActive(InspectorVisible);
        }

        public void ShowPause()
        {
            SetScreens(pause: true);
            inspector.SetActive(InspectorVisible);
            RefreshSoundButtons();
        }

        public void ShowJumpscare()
        {
            SetScreens();
            // Short and faint: in linear color space overlay alphas look much stronger than their value.
            flashAlpha = 0.3f;
            staticUntil = Time.unscaledTime + 2.4f;
        }

        public void ShowEnd(HuntOutcome outcome)
        {
            SetScreens(end: true);
            HuntState state = director.State;
            switch (outcome)
            {
                case HuntOutcome.Caught:
                    endTitle.text = "CAUGHT";
                    endTitle.color = Palette.Accent;
                    break;
                case HuntOutcome.Escaped:
                    endTitle.text = "NIGHT " + director.Night + " SURVIVED";
                    endTitle.color = new Color(0.45f, 0.95f, 0.5f);
                    break;
                default:
                    endTitle.text = "6 AM - STILL INSIDE";
                    endTitle.color = Palette.Gold;
                    break;
            }
            endReason.text = director.EndReason;
            bool next = outcome == HuntOutcome.Escaped && director.Night < HuntDirector.LastNight;
            nextButton.gameObject.SetActive(next);
            againButton.gameObject.SetActive(!next);
            if (outcome == HuntOutcome.Escaped && director.Night >= HuntDirector.LastNight)
            {
                endReason.text += "\nYou survived all five nights.";
            }
            endBody.text =
                "Time survived: " + FormatClock(director.NightSeconds) + "\n" +
                "Objectives done: " + state.ObjectivesDone + " / " + state.ObjectivesTotal + "\n" +
                "Noises Freddy heard: " + state.TimesHeard + "\n" +
                "Hiding spots he opened: " + state.Searches + "\n" +
                "Times he used SENSE: " + state.SenseUses + " (found you " + state.SenseHits + ")\n" +
                "Decisions his brain made: " + director.Decisions.Count + "\n\n" +
                "<color=#8a9a8a>Freddy's brain: " + director.BrainDescription + "</color>";
            ShowReplay();
        }

        /// <summary>The map of the night: Freddy's route (red), yours (green) and his decisions (gold).</summary>
        private void ShowReplay()
        {
            endMap.Dots("player", director.PlayerTrail, new Color(0.4f, 1f, 0.5f, 0.8f), 6f);
            endMap.Dots("freddy", director.FreddyTrail, new Color(1f, 0.25f, 0.2f, 0.9f), 7f);
            var decisionPoints = new System.Collections.Generic.List<Vector3>();
            foreach (DecisionRecord record in director.Decisions)
            {
                decisionPoints.Add(record.Position);
            }
            endMap.Dots("decisions", decisionPoints, Palette.Gold, 11f);
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                endMap.SetRoom((Room)room, new Color(0.1f, 0.11f, 0.11f, 1f), HuntMap.DisplayName((Room)room), new Color(0.4f, 0.45f, 0.42f, 0.8f));
            }
            bool caught = director.State.Outcome == HuntOutcome.Caught;
            endCatch.gameObject.SetActive(caught);
            if (caught)
            {
                endMap.Place(endCatch, director.Player.transform.position);
            }

            var lines = new System.Text.StringBuilder();
            int first = Mathf.Max(0, director.Decisions.Count - 8);
            for (int i = first; i < director.Decisions.Count; i++)
            {
                DecisionRecord record = director.Decisions[i];
                lines.Append("<color=#8a9a8a>").Append(FormatClock(record.Time)).Append("</color>  ");
                lines.Append(record.Text);
                if (record.Confidence >= 0f)
                {
                    lines.Append(" <color=#ffc840>").Append(Mathf.RoundToInt(record.Confidence * 100f)).Append("%</color>");
                }
                if (record.Heard.HasValue)
                {
                    lines.Append("  <color=#d9604a>heard you: ").Append(HuntMap.DisplayName(record.Heard.Value)).Append("</color>");
                }
                lines.Append('\n');
            }
            endTimeline.text = director.Decisions.Count == 0 ? "Freddy never woke up." : lines.ToString();
        }

        public void ToggleInspector()
        {
            InspectorVisible = !InspectorVisible;
            inspector.SetActive(InspectorVisible && (hud.activeSelf || pause.activeSelf));
            RefreshSoundButtons();
        }

        public void Toast(string text)
        {
            toast.text = text;
            toastUntil = Time.time + 3f;
        }

        /// <summary>The objectives of the night, done ones checked; the exit lights up once it works.</summary>
        private string ObjectivesText()
        {
            var text = new System.Text.StringBuilder("TONIGHT:\n");
            Objectives list = director.ObjectiveList;
            HuntState state = director.State;
            foreach (ObjectiveStep step in list.Steps)
            {
                bool done = list.IsDone(step);
                string line;
                if (step == ObjectiveStep.Cells)
                {
                    line = "Power the sockets  " + state.CellsInserted + "/" + director.CellsNeeded + "  (one cell at a time)";
                    if (director.Player.Cell.Carrying)
                    {
                        int socket = director.Player.Cell.Socket;
                        line += state.Night >= 2
                            ? "\n      In your hands: the " + CellColors.Name(socket) + " cell  ->  " + HuntMap.DisplayName(HuntMap.SocketRooms[socket])
                            : "\n      In your hands: a cell  ->  any socket";
                    }
                }
                else if (step == ObjectiveStep.Keycard && state.CardTaken && !state.CardSwiped)
                {
                    line = "Swipe the keycard in the Office";
                }
                else
                {
                    line = char.ToUpper(HuntDirector.Describe(step)[0]) + HuntDirector.Describe(step).Substring(1);
                }
                bool waiting = step == ObjectiveStep.Exit && !list.ExitUnlocked;
                text.Append(done ? "<color=#6a8a6a>[x] " + line + "</color>" : waiting ? "<color=#8a8a7a>[ ] " + line + "</color>" : "<color=#ffc840>[ ] " + line + "</color>");
                text.Append('\n');
            }
            return text.ToString();
        }

        // ------------------------------------------------------------------ cinematic and notes

        private GameObject cinematic;
        private Text cinematicText;
        private Image cinematicFade;
        private GameObject note;
        private Text noteText;
        private int noteOpenedFrame;

        private void BuildCinematic(Transform root)
        {
            cinematic = UiKit.Area("Cinematic", root, Vector2.zero, Vector2.one).gameObject;
            Transform c = cinematic.transform;
            cinematicFade = UiKit.Panel("Fade", c, Vector2.zero, Vector2.one, Color.black);
            cinematicFade.raycastTarget = false;
            UiKit.Panel("BarTop", c, new Vector2(0f, 0.88f), Vector2.one, Color.black).raycastTarget = false;
            UiKit.Panel("BarBottom", c, Vector2.zero, new Vector2(1f, 0.16f), Color.black).raycastTarget = false;
            cinematicText = UiKit.Label("Text", c, "", 34, Palette.Ink, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.15f));
            cinematicCard = UiKit.Label("Card", c, "", 64, Palette.Ink, TextAnchor.MiddleCenter, new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.7f));
            cinematicCard.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            cinematicSkip = UiKit.Label("Skip", c, "[ENTER] skip", 18, Palette.InkDim, TextAnchor.UpperRight, new Vector2(0.7f, 0.9f), new Vector2(0.98f, 0.98f));
            cinematic.SetActive(false);

            note = UiKit.Panel("Note", root, new Vector2(0.3f, 0.15f), new Vector2(0.7f, 0.85f), new Color(0.86f, 0.83f, 0.72f, 0.97f)).gameObject;
            noteText = UiKit.Label("Text", note.transform, "", 30, new Color(0.12f, 0.1f, 0.08f), TextAnchor.UpperLeft, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.92f));
            noteText.supportRichText = false;
            noteClose = UiKit.Label("Close", note.transform, "[E] close", 20, new Color(0.35f, 0.3f, 0.25f), TextAnchor.LowerRight, new Vector2(0.5f, 0.02f), new Vector2(0.95f, 0.1f));
            note.SetActive(false);
        }

        public void ShowCinematic(bool on)
        {
            SetScreens();
            cinematic.SetActive(on);
        }

        /// <summary>The narration line (typed so far) and how black the screen is (0–1).</summary>
        public void SetCinematic(string text, float fade)
        {
            cinematicText.text = text;
            cinematicFade.color = new Color(0f, 0f, 0f, fade);
            cinematicSkip.text = Controls.Localize("[ENTER] skip");
        }

        /// <summary>A title card in the middle of the screen (empty to hide), with its opacity.</summary>
        public void SetCinematicCard(string text, float alpha, int size = 64)
        {
            cinematicCard.text = text;
            cinematicCard.fontSize = size;
            cinematicCard.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, alpha);
        }

        /// <summary>A lab note on screen. The night keeps going while you read.</summary>
        public void ShowNote(string text)
        {
            noteText.text = text;
            noteClose.text = Controls.Localize("[E] close");
            note.SetActive(true);
            noteOpenedFrame = Time.frameCount;
        }

        public void HideNote() => note.SetActive(false);

        private void UpdateNote()
        {
            if (!note.activeSelf || Time.frameCount == noteOpenedFrame)
            {
                return;
            }
            bool close = Controls.ClosePressed || director.Phase != GamePhase.Playing;
            if (close)
            {
                note.SetActive(false);
            }
        }

        private void SetScreens(bool menu = false, bool hud = false, bool pause = false, bool end = false)
        {
            this.menu.SetActive(menu);
            this.hud.SetActive(hud);
            this.pause.SetActive(pause);
            this.end.SetActive(end);
            inspector.SetActive(false);
            if (controlsScreen != null && !pause)
            {
                controlsScreen.SetActive(false);
            }
            if (settingsScreen != null && !pause)
            {
                settingsScreen.SetActive(false);
                rebinding = null;
            }
        }

        // ------------------------------------------------------------------ menu

        private void BuildMenu(Transform root)
        {
            menu = UiKit.Area("Menu", root, Vector2.zero, Vector2.one).gameObject;
            Transform m = menu.transform;
            UiKit.Picture("Shade", m, Vector2.zero, Vector2.one, ProceduralTextures.Vignette(256, 1.3f), Color.white);
            UiKit.Panel("Side", m, Vector2.zero, new Vector2(0.4f, 1f), new Color(0f, 0f, 0f, 0.72f));

            UiKit.Label("Title", m, "NIGHT SHIFT", 150, Palette.Ink, TextAnchor.LowerLeft, new Vector2(0.05f, 0.76f), new Vector2(0.6f, 0.93f));
            UiKit.Label("Subtitle", m, "Freddy has learned to hunt. Find the fuses. Get out.", 36, Palette.Accent,
                        TextAnchor.UpperLeft, new Vector2(0.05f, 0.69f), new Vector2(0.4f, 0.76f));

            float top = 0.63f;
            Button Add(string text, UnityEngine.Events.UnityAction action)
            {
                Button button = UiKit.Button(text, m, text, 36, new Vector2(0.05f, top - 0.052f), new Vector2(0.3f, top), action);
                top -= 0.06f;
                return button;
            }
            UiKit.Tint(Add("NEW GAME  (STORY)", director.PlayStory), Palette.Accent * 0.8f + new Color(0f, 0f, 0f, 0.2f));
            continueButton = Add("CONTINUE", () => director.StartNight(HuntDirector.BestNight));
            Add("HOW FREDDY THINKS", () => ShowInfo("HOW FREDDY THINKS", ExplainerText()));
            Add("CONTROLS", ShowControls);
            Add("SETTINGS", ShowSettings);
            Add("CREDITS", () => ShowInfo("CREDITS", CreditsText));
            musicButton = Add("MUSIC", () => { SoundPrefs.MusicOn = !SoundPrefs.MusicOn; director.RefreshSound(); RefreshSoundButtons(); });
            soundButton = Add("SOUND", () => { SoundPrefs.EffectsOn = !SoundPrefs.EffectsOn; director.RefreshSound(); RefreshSoundButtons(); });
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                Add("QUIT", Application.Quit);
            }

            UiKit.Label("Brain", m, "Freddy's brain: " + director.BrainDescription, 26, Palette.InkDim, TextAnchor.LowerLeft,
                        new Vector2(0.05f, 0.04f), new Vector2(0.6f, 0.09f));
            UiKit.Label("Fan", m, "Fan work, non-commercial. Freddy Fazbear © Scott Cawthon.", 22, Palette.InkDim, TextAnchor.LowerRight,
                        new Vector2(0.5f, 0.02f), new Vector2(0.98f, 0.06f));

            menuInfo = UiKit.Panel("Info", m, new Vector2(0.42f, 0.1f), new Vector2(0.97f, 0.9f), Palette.Panel).gameObject;
            Transform info = menuInfo.transform;
            menuInfoTitle = UiKit.Label("Title", info, "", 54, Palette.Gold, TextAnchor.UpperLeft, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f));
            menuInfoBody = UiKit.Label("Body", info, "", 29, Palette.Ink, TextAnchor.UpperLeft, new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.86f));
            UiKit.Button("Close", info, "CLOSE", 32, new Vector2(0.75f, 0.02f), new Vector2(0.95f, 0.09f), () => menuInfo.SetActive(false));
            BuildCharts(info);
        }

        private void ShowInfo(string title, string body)
        {
            menuInfoTitle.text = title;
            menuInfoBody.text = body;
            menuInfo.SetActive(true);
            bool charts = title == "HOW FREDDY THINKS";
            explainerCharts.SetActive(charts);
            menuInfoBody.rectTransform.anchorMin = new Vector2(0.05f, charts ? 0.47f : 0.1f);
            menuInfoBody.fontSize = charts ? 24 : 29;
        }

        /// <summary>
        /// The evidence that Freddy learned: the real reward curve of his training and the catch rates
        /// of the trained network against a scripted and a random hunter.
        /// </summary>
        private void BuildCharts(Transform info)
        {
            explainerCharts = UiKit.Area("Charts", info, new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.46f)).gameObject;
            Transform c = explainerCharts.transform;

            UiKit.Label("CurveTitle", c, "HOW IT LEARNED: reward per night during training", 20, Palette.Gold, TextAnchor.UpperLeft, new Vector2(0.02f, 0.88f), new Vector2(0.5f, 1f));
            Image plot = UiKit.Panel("Plot", c, new Vector2(0.02f, 0.12f), new Vector2(0.48f, 0.86f), new Color(1f, 1f, 1f, 0.04f));
            UiKit.Label("X", c, "training steps (0 to 3 million)", 16, Palette.InkDim, TextAnchor.UpperCenter, new Vector2(0.02f, 0f), new Vector2(0.48f, 0.11f));
            var points = new System.Collections.Generic.List<Vector2>();
            if (director.Assets.TrainingCurve != null)
            {
                foreach (string row in director.Assets.TrainingCurve.text.Split('\n'))
                {
                    string[] cells = row.Split(',');
                    if (cells.Length > 1 && float.TryParse(cells[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float step)
                        && float.TryParse(cells[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float reward))
                    {
                        points.Add(new Vector2(step, reward));
                    }
                }
            }
            if (points.Count > 1)
            {
                float maxStep = points[points.Count - 1].x;
                foreach (Vector2 p in points)
                {
                    float x = p.x / maxStep;
                    float y = Mathf.InverseLerp(-1f, 1.2f, p.y);
                    Image dot = UiKit.Panel("Point", plot.transform, new Vector2(x, y), new Vector2(x, y), Palette.Accent);
                    dot.rectTransform.sizeDelta = new Vector2(5f, 5f);
                }
                UiKit.Label("Low", plot.transform, "-1 (he loses)", 14, Palette.InkDim, TextAnchor.LowerLeft, new Vector2(0.01f, 0f), new Vector2(0.5f, 0.1f));
                UiKit.Label("High", plot.transform, "+1 (he catches you)", 14, Palette.InkDim, TextAnchor.UpperLeft, new Vector2(0.01f, 0.88f), new Vector2(0.6f, 1f));
            }

            UiKit.Label("BarsTitle", c, "DOES IT BEAT A HAND-WRITTEN HUNTER? catch rate", 20, Palette.Gold, TextAnchor.UpperLeft, new Vector2(0.52f, 0.88f), new Vector2(0.98f, 1f));
            ResultsFile results = director.Assets.EvaluationResults != null ? JsonUtility.FromJson<ResultsFile>(director.Assets.EvaluationResults.text) : null;
            string[] policies = { "trained", "scripted", "random" };
            string[] names = { "trained network", "scripted rules", "random" };
            Color[] colors = { Palette.Accent, new Color(0.95f, 0.65f, 0.2f), new Color(0.45f, 0.5f, 0.55f) };
            for (int i = 0; i < policies.Length; i++)
            {
                float rate = 0f;
                if (results != null && results.runs != null)
                {
                    foreach (Run run in results.runs)
                    {
                        if (run.policy == policies[i] && run.survivor == "Normal")
                        {
                            rate = run.catch_rate;
                        }
                    }
                }
                float top = 0.78f - i * 0.22f;
                UiKit.Label("Name", c, names[i], 18, Palette.Ink, TextAnchor.MiddleLeft, new Vector2(0.52f, top - 0.14f), new Vector2(0.7f, top));
                UiKit.Panel("Back", c, new Vector2(0.7f, top - 0.12f), new Vector2(0.98f, top - 0.02f), new Color(1f, 1f, 1f, 0.06f));
                UiKit.Panel("Bar", c, new Vector2(0.7f, top - 0.12f), new Vector2(0.7f + 0.28f * rate, top - 0.02f), colors[i]);
                UiKit.Label("Value", c, Mathf.RoundToInt(rate * 100f) + "%", 18, Palette.Ink, TextAnchor.MiddleRight, new Vector2(0.7f, top - 0.14f), new Vector2(0.97f, top));
            }
            string model = results != null ? results.model : "";
            UiKit.Label("Note", c, "Against the \"Normal\" simulated survivor, " + (results != null ? results.nights_per_run : 0) + " nights each. Model: " + model,
                        15, Palette.InkDim, TextAnchor.UpperLeft, new Vector2(0.52f, 0f), new Vector2(0.98f, 0.11f));
            explainerCharts.SetActive(false);
        }

        [System.Serializable]
        private class ResultsFile
        {
            public string model;
            public int nights_per_run;
            public Run[] runs;
        }

        [System.Serializable]
        private class Run
        {
            public string policy;
            public string survivor;
            public float catch_rate;
        }

        private string ExplainerText()
        {
            return
                "Freddy is an NPC built with <color=#ffc840>Unity ML-Agents</color>. His <color=#ffc840>brain</color> is a neural network trained with " +
                "reinforcement learning (PPO) in millions of simulated nights on this two-floor building. Every few seconds it picks one of " +
                FreddyActions.Count + " actions: go to one of the " + HuntMap.RoomCount + " rooms, stay and listen, search a hiding spot, or " +
                "<color=#cc66ff>SENSE</color> (find you if you are afraid; long recharge). Nobody told it when to use each one: it learned.\n" +
                "It trained against survivors that escape through the <color=#ffc840>air ducts</color>, where it cannot follow.\n" +
                "It only knows what it heard, where it last saw you and how long since it checked each room. It never knows where you are. " +
                "That is why a thrown <color=#ffc840>party horn</color> fools it: to the network a noise is a noise.\n" +
                "Press <color=#ffc840>TAB</color> in the game to watch it think. Below: the proof it learned.";
        }

        private const string CreditsText =
            "Machine Learning final project\nUniversidad de Cundinamarca, Chía\n\nJesús Nuncira  ·  Miguel Moreno\n\n" +
            "Freddy model: \"Freddy Fazbear\" by 2012carlitos17 (sketchfab.com), licensed under CC BY 4.0. " +
            "Rigged and animated with Adobe Mixamo.\n\n" +
            "Font: VT323 by Peter Hull, SIL Open Font License.\n\n" +
            "Built with Unity 6 and Unity ML-Agents.\n\n" +
            "Fan work, non-commercial. Freddy Fazbear © Scott Cawthon. Not affiliated with the original games.";

        private void RefreshSoundButtons()
        {
            string music = "MUSIC: " + (SoundPrefs.MusicOn ? "ON" : "OFF");
            string sound = "SOUND: " + (SoundPrefs.EffectsOn ? "ON" : "OFF");
            UiKit.SetButtonText(musicButton, music);
            UiKit.SetButtonText(soundButton, sound);
            UiKit.SetButtonText(pauseMusic, music);
            UiKit.SetButtonText(pauseSound, sound);
            UiKit.SetButtonText(pauseInspector, "AI INSPECTOR: " + (InspectorVisible ? "ON" : "OFF") + "  [TAB]");
        }

        // ------------------------------------------------------------------ HUD

        private void BuildHud(Transform root)
        {
            hud = UiKit.Area("Hud", root, Vector2.zero, Vector2.one).gameObject;
            Transform h = hud.transform;

            lockerView = UiKit.Picture("LockerView", h, Vector2.zero, Vector2.one, ProceduralTextures.Vignette(256, 0.95f), Color.white).gameObject;
            crouchView = UiKit.Picture("HiddenView", h, Vector2.zero, Vector2.one, ProceduralTextures.Vignette(256, 1.1f), Color.white).gameObject;

            // Fear: the edges of the screen pulse red as Freddy gets close.
            fearView = UiKit.Picture("Fear", h, Vector2.zero, Vector2.one, ProceduralTextures.Vignette(256, 1.4f), new Color(0.5f, 0f, 0f, 0f));

            staminaBar = UiKit.Area("Stamina", h, new Vector2(0.42f, 0.04f), new Vector2(0.58f, 0.055f)).gameObject;
            UiKit.Panel("Back", staminaBar.transform, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.6f));
            staminaFill = UiKit.Panel("Fill", staminaBar.transform, Vector2.zero, Vector2.one, new Color(0.85f, 0.85f, 0.7f, 0.85f));

            senseFlash = UiKit.Picture("SenseFlash", h, Vector2.zero, Vector2.one, ProceduralTextures.Vignette(256, 2f), new Color(0.6f, 0.1f, 0.9f, 0f));
            senseText = UiKit.Label("SenseText", h, "", 64, new Color(0.85f, 0.4f, 1f), TextAnchor.MiddleCenter, new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.68f));

            subtitle = UiKit.Label("Subtitle", h, "", 30, Palette.Ink, TextAnchor.LowerCenter, new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.3f));
            subtitle.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            items = UiKit.Label("Items", h, "", 24, Palette.Ink, TextAnchor.UpperLeft, new Vector2(0.02f, 0.64f), new Vector2(0.42f, 0.69f));
            UiKit.Label("FearLabel", h, "FEAR  (he can sense it)", 20, Palette.InkDim, TextAnchor.LowerLeft, new Vector2(0.02f, 0.605f), new Vector2(0.3f, 0.64f));
            UiKit.Panel("FearBack", h, new Vector2(0.02f, 0.59f), new Vector2(0.17f, 0.602f), new Color(0f, 0f, 0f, 0.6f));
            fearMeter = UiKit.Panel("FearFill", h, new Vector2(0.02f, 0.59f), new Vector2(0.17f, 0.602f), new Color(0.7f, 0.25f, 0.9f, 0.9f));

            crouchHint = UiKit.Label("Crouch", h, "", 22, Palette.InkDim, TextAnchor.LowerCenter, new Vector2(0.3f, 0.06f), new Vector2(0.7f, 0.1f));
            titleCard = UiKit.Label("TitleCard", h, "", 110, Palette.Ink, TextAnchor.MiddleCenter, new Vector2(0f, 0.4f), new Vector2(1f, 0.62f));

            clock = UiKit.Label("Clock", h, "12:00 AM", 54, Palette.Ink, TextAnchor.UpperLeft, new Vector2(0.02f, 0.88f), new Vector2(0.3f, 0.98f));
            fuses = UiKit.Label("Objectives", h, "", 24, Palette.Gold, TextAnchor.UpperLeft, new Vector2(0.02f, 0.69f), new Vector2(0.42f, 0.875f));
            keysLabel = UiKit.Label("Keys", h, "TAB  AI inspector     ESC  pause", 24, Palette.InkDim, TextAnchor.UpperRight,
                        new Vector2(0.6f, 0.94f), new Vector2(0.98f, 0.98f));

            UiKit.Picture("Crosshair", h, new Vector2(0.4985f, 0.4975f), new Vector2(0.5015f, 0.5025f), ProceduralTextures.Disc(16), new Color(1f, 1f, 1f, 0.6f));
            prompt = UiKit.Label("Prompt", h, "", 34, Palette.Ink, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.33f), new Vector2(0.75f, 0.4f));
            toast = UiKit.Label("Toast", h, "", 40, Palette.Gold, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.72f), new Vector2(0.8f, 0.8f));

            // Motion tracker.
            RectTransform tracker = UiKit.Area("Tracker", h, new Vector2(0.015f, 0.03f), new Vector2(0.015f, 0.03f));
            tracker.sizeDelta = new Vector2(300f, 300f);
            tracker.pivot = Vector2.zero;
            UiKit.Picture("Screen", tracker, Vector2.zero, Vector2.one, ProceduralTextures.Disc(128), new Color(0.05f, 0.18f, 0.1f, 0.85f));
            UiKit.Picture("Ring", tracker, new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.75f), ProceduralTextures.Disc(128), new Color(0.2f, 0.6f, 0.3f, 0.15f));
            UiKit.Picture("You", tracker, new Vector2(0.48f, 0.48f), new Vector2(0.52f, 0.52f), ProceduralTextures.Disc(32), new Color(0.6f, 1f, 0.7f, 0.9f));
            trackerBlipImage = UiKit.Picture("Blip", tracker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), ProceduralTextures.Disc(64), new Color(0.6f, 1f, 0.6f, 1f));
            trackerBlip = trackerBlipImage.rectTransform;
            trackerBlip.sizeDelta = new Vector2(26f, 26f);
            trackerLabel = UiKit.Label("Distance", tracker, "", 26, new Color(0.6f, 1f, 0.7f), TextAnchor.UpperCenter, new Vector2(0f, -0.14f), new Vector2(1f, -0.02f));

            breath = UiKit.Area("Breath", h, new Vector2(0.38f, 0.08f), new Vector2(0.62f, 0.16f)).gameObject;
            breathHint = UiKit.Label("Hint", breath.transform, "Hold SPACE to hold your breath    [E] leave", 28, Palette.Ink, TextAnchor.UpperCenter, new Vector2(0f, 0.5f), new Vector2(1f, 1f));
            UiKit.Panel("Back", breath.transform, new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Color(0f, 0f, 0f, 0.7f));
            breathFill = UiKit.Panel("Fill", breath.transform, new Vector2(0f, 0f), new Vector2(1f, 0.3f), new Color(0.5f, 0.8f, 1f, 0.9f));
        }

        private void Update()
        {
            UpdateNote();
            UpdateGamepadSelection();
            UpdateRebinding();
            if (breathHint != null && breath.activeSelf)
            {
                breathHint.text = Controls.Localize("Hold SPACE to hold your breath    [E] leave");
            }
            if (flashAlpha > 0f)
            {
                flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 0.5f);
                // Flickers on and off like a failing screen.
                flash.color = new Color(0.45f, 0f, 0f, Random.value < 0.6f ? flashAlpha : 0f);
            }
            bool noisy = Time.unscaledTime < staticUntil;
            staticNoise.enabled = noisy;
            if (noisy)
            {
                float left = staticUntil - Time.unscaledTime;
                staticNoise.uvRect = new Rect(Random.value, Random.value, 1f, 1f);
                staticNoise.color = new Color(1f, 1f, 1f, (left > 2f ? 0.35f : 0.12f) * (Random.value < 0.75f ? 1f : 0.3f));
            }
            if (hud.activeSelf)
            {
                RefreshHud();
            }
            if (inspector.activeSelf)
            {
                RefreshInspector();
            }
        }

        private void RefreshHud()
        {
            PlayerController player = director.Player;
            clock.text = FormatHour(director.NightSeconds / director.NightLengthSeconds);
            fuses.text = director.InCall ? "TONIGHT: listen to the call, explore\n<color=#8a9a8a>(the objectives appear when it ends)</color>" : ObjectivesText();

            // While hidden the breath bar already says how to leave.
            // At a panel the hint goes to the bottom of the screen, out of the way of what is worked on.
            bool atPanel = player.InFocus;
            prompt.rectTransform.anchorMin = atPanel ? new Vector2(0.15f, 0.03f) : new Vector2(0.25f, 0.33f);
            prompt.rectTransform.anchorMax = atPanel ? new Vector2(0.85f, 0.11f) : new Vector2(0.75f, 0.4f);
            prompt.text = director.NeedsClickToLook ? "CLICK TO LOOK AROUND"
                        : player.InFocus ? player.Focus.FocusHint + Controls.Localize("\n<size=20><color=#8a9a8a>[RIGHT CLICK] or [ESC] step back</color></size>")
                        : player.Target != null && player.HiddenIn == null ? Controls.Localize(player.Target.Prompt(player)) : "";
            keysLabel.text = Controls.Localize("TAB  AI inspector     ESC  pause");
            toast.text = Time.time < toastUntil ? toast.text : "";

            HuntState hunt = director.State;
            float sinceStart = Time.time - director.NightStartedAt;
            float card = sinceStart < 1f ? sinceStart : sinceStart < 3.5f ? 1f : Mathf.Clamp01(1f - (sinceStart - 3.5f) / 1.2f);
            string changes = StoryData.NightChanges[Mathf.Clamp(director.Night - 1, 0, StoryData.NightChanges.Length - 1)];
            titleCard.text = card > 0f ? "NIGHT " + director.Night + "\n<size=48>12:00 AM</size>" + (changes.Length > 0 ? "\n<size=28><color=#d94a3a>" + changes + "</color></size>" : "") : "";
            titleCard.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, card);
            subtitle.text = director.InCall ? director.CallSubtitle + Controls.Localize("\n<size=20><color=#8a9a8a>[ENTER] skip the call</color></size>") : "";
            items.text = player.Lures > 0 ? Controls.Localize("PARTY HORNS: " + player.Lures + "   [G] throw to lure him") : "";
            float fear = director.InCall ? 0f : hunt.PlayerFear;
            fearMeter.rectTransform.anchorMax = new Vector2(0.02f + 0.15f * fear, 0.602f);
            fearMeter.color = fear >= hunt.Settings.SenseFearThreshold ? new Color(0.9f, 0.2f, 0.9f, 0.95f) : new Color(0.55f, 0.35f, 0.7f, 0.8f);
            float sinceSense = Time.time - director.LastSenseAt;
            float senseAlpha = sinceSense < 2.5f ? (1f - sinceSense / 2.5f) : 0f;
            senseFlash.color = new Color(0.6f, 0.1f, 0.9f, senseAlpha * (director.LastSenseFound ? 0.6f : 0.25f));
            senseText.text = sinceSense < 2.5f && director.LastSenseFound ? "SENSED" : "";
            senseText.color = new Color(0.85f, 0.4f, 1f, senseAlpha);

            crouchHint.text = player.Crouching && player.HiddenIn == null
                ? (player.Flashlight.enabled ? "CROUCHING  (turn off the flashlight to be a shadow)" : "CROUCHING IN THE DARK  (he only sees you up close)")
                : "";
            bool hidden = player.HiddenIn != null;
            staminaBar.SetActive(!hidden && player.Stamina < 0.99f);
            staminaFill.rectTransform.anchorMax = new Vector2(player.Stamina, 1f);
            staminaFill.color = player.Winded ? new Color(0.85f, 0.25f, 0.2f, 0.9f) : new Color(0.85f, 0.85f, 0.7f, 0.85f);
            float heartbeat = 0.75f + 0.25f * Mathf.Sin(Time.time * (4f + player.Fear * 6f));
            // Linear color space: keep the alpha small, it looks much stronger than its value.
            fearView.color = new Color(0.55f, 0f, 0f, player.Fear * player.Fear * 0.45f * heartbeat);
            lockerView.SetActive(hidden && player.HiddenIn.Kind == SpotKind.Locker);
            crouchView.SetActive(hidden && player.HiddenIn.Kind != SpotKind.Locker);
            breath.SetActive(hidden);
            if (hidden)
            {
                breathFill.rectTransform.anchorMax = new Vector2(player.Breath, 0.3f);
                breathFill.color = player.HoldingBreath ? new Color(0.5f, 0.8f, 1f, 0.95f) : new Color(0.5f, 0.6f, 0.7f, 0.6f);
            }

            // Tracker: Freddy's position relative to where the player looks.
            Vector3 offset = director.Freddy.transform.position - player.transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            bool inRange = distance <= director.TrackerRange;
            trackerBlipImage.enabled = inRange;
            trackerLabel.text = inRange ? Mathf.RoundToInt(distance) + " m" : "";
            if (inRange)
            {
                float yaw = player.Head.eulerAngles.y;
                Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * offset / director.TrackerRange;
                trackerBlip.anchorMin = trackerBlip.anchorMax = new Vector2(0.5f + local.x * 0.46f, 0.5f + local.z * 0.46f);
                float pulse = 0.6f + Mathf.PingPong(Time.time * 2f, 0.4f);
                trackerBlipImage.color = distance < 8f ? new Color(1f, 0.35f, 0.3f, pulse) : new Color(0.6f, 1f, 0.6f, pulse);
            }
        }

        // ------------------------------------------------------------------ AI inspector

        private void BuildInspector(Transform root)
        {
            inspector = UiKit.Panel("Inspector", root, new Vector2(0.655f, 0.05f), new Vector2(0.995f, 0.95f), Palette.Panel).gameObject;
            Transform i = inspector.transform;
            UiKit.Label("Title", i, "FREDDY'S BRAIN", 44, Palette.Gold, TextAnchor.UpperLeft, new Vector2(0.04f, 0.935f), new Vector2(0.96f, 0.995f));
            UiKit.Label("Sub", i, "Neural network trained with PPO (Unity ML-Agents). It only knows what it heard and saw.", 19, Palette.InkDim,
                        TextAnchor.UpperLeft, new Vector2(0.04f, 0.9f), new Vector2(0.96f, 0.94f));

            inspectorMap = MapView.Create("Map", i, new Vector2(0.03f, 0.55f), new Vector2(0.97f, 0.9f));
            mapSeen = inspectorMap.Marker("Seen", new Color(0.6f, 0.8f, 1f, 0.9f), 16f);
            mapPlayer = inspectorMap.Marker("You", new Color(0.5f, 1f, 0.6f, 0.9f), 12f);
            mapSensed = inspectorMap.Marker("Sensed", new Color(0.8f, 0.35f, 1f, 0.9f), 22f);
            mapFreddy = inspectorMap.Marker("Freddy", new Color(1f, 0.3f, 0.2f, 1f), 18f);
            UiKit.Label("Legend", i, "<color=#ff5a4a>o</color> Freddy   <color=#80ff99>o</color> you (he cannot see this)   <color=#99ccff>o</color> where he last saw you\n" +
                                     "<color=#d94a3a>red rooms</color> = noise he remembers   <color=#5ad8ff>blue line</color> = his route   <color=#cc66ff>o</color> where SENSE found you",
                        17, Palette.InkDim, TextAnchor.UpperLeft, new Vector2(0.04f, 0.495f), new Vector2(0.96f, 0.55f));

            UiKit.Label("OptionsTitle", i, "WHAT THE NETWORK CONSIDERED AT ITS LAST DECISION", 19, Palette.Gold, TextAnchor.UpperLeft,
                        new Vector2(0.04f, 0.455f), new Vector2(0.96f, 0.49f));
            for (int row = 0; row < optionLabels.Length; row++)
            {
                float top = 0.45f - row * 0.043f;
                UiKit.Panel("BarBack", i, new Vector2(0.52f, top - 0.032f), new Vector2(0.96f, top - 0.006f), new Color(1f, 1f, 1f, 0.06f));
                optionBars[row] = UiKit.Panel("Bar", i, new Vector2(0.52f, top - 0.032f), new Vector2(0.96f, top - 0.006f), Palette.Accent);
                optionLabels[row] = UiKit.Label("Option", i, "", 20, Palette.Ink, TextAnchor.MiddleLeft, new Vector2(0.04f, top - 0.038f), new Vector2(0.51f, top));
            }

            inspectorStatus = UiKit.Label("Status", i, "", 20, Palette.Ink, TextAnchor.UpperLeft, new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.23f));
            inspectorLog = UiKit.Label("Log", i, "", 18, Palette.InkDim, TextAnchor.UpperLeft, new Vector2(0.04f, 0.005f), new Vector2(0.96f, 0.13f));
            inspector.SetActive(false);
        }

        private void RefreshInspector()
        {
            HuntState state = director.State;
            for (int room = 0; room < HuntMap.RoomCount; room++)
            {
                float noise = Mathf.Clamp01(state.NoiseMemory[room] * 1.4f);
                bool freddyRoom = (Room)room == state.FreddyRoom;
                string label = HuntMap.DisplayName((Room)room);
                bool hall = PizzeriaLayout.Area((Room)room).height < 4f;
                if (state.TicksSinceVisited[room] >= state.Settings.MemoryWindowTicks && !freddyRoom && !hall)
                {
                    label += "\n<size=13><color=#6a7a6a>not checked</color></size>";
                }
                inspectorMap.SetRoom((Room)room,
                                     Color.Lerp(new Color(0.09f, 0.1f, 0.1f, 1f), new Color(0.75f, 0.12f, 0.08f, 1f), noise),
                                     label,
                                     freddyRoom ? Palette.Gold : new Color(0.4f, 0.45f, 0.42f, 0.8f));
            }
            inspectorMap.Place(mapFreddy, director.Freddy.transform.position);
            inspectorMap.Place(mapPlayer, director.Player.transform.position);
            mapSeen.gameObject.SetActive(state.LastSeenRoom.HasValue);
            if (state.LastSeenRoom.HasValue)
            {
                inspectorMap.Place(mapSeen, PizzeriaLayout.Center(state.LastSeenRoom.Value));
            }
            mapSensed.gameObject.SetActive(state.SensedRoom.HasValue && state.TicksSinceSensed < 15);
            if (state.SensedRoom.HasValue)
            {
                inspectorMap.Place(mapSensed, PizzeriaLayout.Center(state.SensedRoom.Value));
            }
            var route = new System.Collections.Generic.List<Vector3>(director.Freddy.PlannedPath);
            inspectorMap.Dots("route", route, new Color(0.35f, 0.85f, 1f, 0.9f), 7f);

            // The five most likely actions at the last decision, with their probabilities.
            int legalCount = 0;
            for (int a = 0; a < FreddyActions.Count; a++)
            {
                if (director.HasProbabilities && director.LegalActions[a])
                {
                    sortedActions[legalCount++] = a;
                }
            }
            System.Array.Sort(sortedActions, 0, legalCount, Comparer.Instance(director.Probabilities));
            for (int row = 0; row < optionLabels.Length; row++)
            {
                bool used = row < legalCount;
                optionLabels[row].gameObject.SetActive(used);
                optionBars[row].gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }
                int action = sortedActions[row];
                float p = director.Probabilities[action];
                bool chosen = director.ProbedChoice == action;
                optionLabels[row].text = (chosen ? "<color=#ffc840>> </color>" : "  ") + director.Describe(action);
                optionLabels[row].color = chosen ? Palette.Ink : Palette.InkDim;
                RectTransform bar = optionBars[row].rectTransform;
                bar.anchorMax = new Vector2(0.52f + 0.44f * p, bar.anchorMax.y);
                optionBars[row].color = chosen ? Palette.Accent : new Color(0.6f, 0.35f, 0.3f, 0.8f);
            }
            if (legalCount == 0)
            {
                optionLabels[0].gameObject.SetActive(true);
                optionLabels[0].text = director.HasProbabilities ? "" : "  (waiting for the first decision)";
            }

            inspectorStatus.text =
                "<color=#ffc840>Body:</color> " + director.Freddy.Activity + "\n" +
                "<color=#ffc840>Heard you last in:</color> " + (director.LastHeard.HasValue ? HuntMap.DisplayName(director.LastHeard.Value) : "nothing yet") +
                "   <color=#ffc840>Sees you:</color> " + (director.Freddy.SeesPlayer ? "<color=#ff6a5a>YES</color>" : "no") + "\n" +
                "<color=#ffc840>Sense:</color> " + (state.SenseReady ? "<color=#cc66ff>READY</color> (the network decides when)" :
                    "recharging, " + Mathf.CeilToInt(state.SenseCooldown * state.Settings.DecisionIntervalSeconds) + " s") +
                (state.SensedRoom.HasValue ? "   last found you in: " + HuntMap.DisplayName(state.SensedRoom.Value) : "");

            var log = new System.Text.StringBuilder();
            for (int k = director.Decisions.Count - 1; k >= 0 && k >= director.Decisions.Count - 4; k--)
            {
                DecisionRecord record = director.Decisions[k];
                log.Append(FormatClock(record.Time)).Append("  ").Append(record.Text);
                if (record.Confidence >= 0f)
                {
                    log.Append("  ").Append(Mathf.RoundToInt(record.Confidence * 100f)).Append('%');
                }
                log.Append('\n');
            }
            inspectorLog.text = log.ToString();
        }

        /// <summary>Sorts action indices by probability, highest first.</summary>
        private class Comparer : System.Collections.Generic.IComparer<int>
        {
            private static readonly Comparer Shared = new Comparer();
            private float[] probabilities;

            public static Comparer Instance(float[] probabilities)
            {
                Shared.probabilities = probabilities;
                return Shared;
            }

            public int Compare(int a, int b) => probabilities[b].CompareTo(probabilities[a]);
        }

        // ------------------------------------------------------------------ pause and end

        private void BuildPause(Transform root)
        {
            pause = UiKit.Panel("Pause", root, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.8f)).gameObject;
            Transform p = pause.transform;
            UiKit.Label("Title", p, "PAUSED", 120, Palette.Ink, TextAnchor.LowerLeft, new Vector2(0.06f, 0.72f), new Vector2(0.6f, 0.88f));
            float top = 0.68f;
            Button Add(string text, UnityEngine.Events.UnityAction action)
            {
                Button button = UiKit.Button(text, p, text, 38, new Vector2(0.06f, top - 0.065f), new Vector2(0.36f, top), action);
                top -= 0.08f;
                return button;
            }
            UiKit.Tint(Add("RESUME  [ESC]", () => director.SetPaused(false)), Palette.Accent * 0.8f + new Color(0f, 0f, 0f, 0.2f));
            Add("RESTART NIGHT", director.StartNight);
            pauseMusic = Add("MUSIC", () => { SoundPrefs.MusicOn = !SoundPrefs.MusicOn; RefreshSoundButtons(); });
            pauseSound = Add("SOUND", () => { SoundPrefs.EffectsOn = !SoundPrefs.EffectsOn; RefreshSoundButtons(); });
            pauseInspector = Add("AI INSPECTOR", ToggleInspector);
            Add("CONTROLS", ShowControls);
            Add("SETTINGS", ShowSettings);
            Add("QUIT TO MENU", director.ShowMenu);
        }

        // ------------------------------------------------------------------ controls

        /// <summary>Full-screen controls diagram (keyboard and mouse, or Xbox controller), drawn by tools/generate_controls_diagram.py.</summary>
        private void BuildControls(Transform root)
        {
            controlsScreen = UiKit.Panel("Controls", root, Vector2.zero, Vector2.one, new Color(0.02f, 0.02f, 0.025f, 1f)).gameObject;
            Transform c = controlsScreen.transform;
            UiKit.Label("Title", c, "CONTROLS", 90, Palette.Ink, TextAnchor.LowerLeft, new Vector2(0.04f, 0.86f), new Vector2(0.4f, 0.97f));
            keyboardTab = UiKit.Button("Keyboard", c, "KEYBOARD + MOUSE", 30, new Vector2(0.42f, 0.885f), new Vector2(0.64f, 0.945f), () => ShowControlsFor(false));
            gamepadTab = UiKit.Button("Gamepad", c, "XBOX CONTROLLER", 30, new Vector2(0.65f, 0.885f), new Vector2(0.85f, 0.945f), () => ShowControlsFor(true));
            UiKit.Button("Close", c, "CLOSE", 30, new Vector2(0.86f, 0.885f), new Vector2(0.96f, 0.945f), () => controlsScreen.SetActive(false));
            controlsPicture = UiKit.Panel("Diagram", c, new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.86f), Color.white);
            controlsPicture.preserveAspect = true;
            controlsPicture.raycastTarget = false;
            UiKit.Label("Goal", c, "Find what the night asks for, power the exit and leave before 6 AM.   Crouch in the dark and he only sees you up close.   " +
                        "A controller works in the browser too: press any button on it once the game has loaded.",
                        24, Palette.InkDim, TextAnchor.MiddleCenter, new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.09f));
            controlsScreen.SetActive(false);
        }

        /// <summary>Opens the controls screen on one of its two diagrams (used by the capture tour).</summary>
        public void ShowControlsScreen(bool gamepad)
        {
            controlsScreen.SetActive(true);
            controlsScreen.transform.SetAsLastSibling();
            ShowControlsFor(gamepad);
        }

        public void HideControlsScreen() => controlsScreen.SetActive(false);

        // ------------------------------------------------------------------ settings

        private static readonly (InputAction action, string label)[] RebindableActions =
        {
            (InputAction.Forward, "MOVE FORWARD"), (InputAction.Back, "MOVE BACK"), (InputAction.Left, "MOVE LEFT"), (InputAction.Right, "MOVE RIGHT"),
            (InputAction.Sprint, "SPRINT"), (InputAction.Crouch, "CROUCH"), (InputAction.Use, "USE / HIDE / DOORS"), (InputAction.Flashlight, "FLASHLIGHT"),
            (InputAction.Throw, "THROW PARTY HORN"), (InputAction.HoldBreath, "HOLD BREATH"), (InputAction.Inspector, "AI INSPECTOR"), (InputAction.Skip, "SKIP CALL")
        };

        /// <summary>Look sensitivity, invert look and the keyboard keys (saved between sessions).</summary>
        private Text qualityValue;
        private Text qualityNote;

        private static readonly string[] QualityNotes =
        {
            "For slow computers: lower resolution, no shadows from the lamps, no ambient occlusion.",
            "Balanced (the web default): ambient occlusion, light shafts, the flashlight's shadows.",
            "Lamp shadows, reflections on the floors and metal, light bouncing off the floors, lens flares.",
            "Every lamp casts shadows, sharper shadows and reflections. Needs a strong graphics card.",
        };

        private void BuildSettings(Transform root)
        {
            settingsScreen = UiKit.Panel("Settings", root, Vector2.zero, Vector2.one, new Color(0.02f, 0.02f, 0.025f, 1f)).gameObject;
            Transform t = settingsScreen.transform;
            UiKit.Label("Title", t, "SETTINGS", 90, Palette.Ink, TextAnchor.LowerLeft, new Vector2(0.04f, 0.86f), new Vector2(0.5f, 0.97f));
            UiKit.Button("Close", t, "CLOSE", 30, new Vector2(0.86f, 0.885f), new Vector2(0.96f, 0.945f), () => { rebinding = null; settingsScreen.SetActive(false); });
            UiKit.Button("Reset", t, "RESET DEFAULTS", 30, new Vector2(0.66f, 0.885f), new Vector2(0.85f, 0.945f), () => { Controls.ResetSettings(); RefreshSettings(); });

            // Left column: looking around.
            UiKit.Label("LookTitle", t, "LOOK", 40, Palette.Gold, TextAnchor.LowerLeft, new Vector2(0.06f, 0.74f), new Vector2(0.4f, 0.8f));
            float row = 0.66f;
            mouseValue = SettingRow(t, "MOUSE SENSITIVITY", row, () => Controls.SetMouseSensitivity(Controls.MouseSensitivity - 0.1f), () => Controls.SetMouseSensitivity(Controls.MouseSensitivity + 0.1f));
            row -= 0.1f;
            stickValue = SettingRow(t, "CONTROLLER SENSITIVITY", row, () => Controls.SetStickSensitivity(Controls.StickSensitivity - 0.1f), () => Controls.SetStickSensitivity(Controls.StickSensitivity + 0.1f));
            row -= 0.1f;
            UiKit.Label("InvertLabel", t, "INVERT LOOK UP / DOWN", 30, Palette.Ink, TextAnchor.MiddleLeft, new Vector2(0.06f, row), new Vector2(0.3f, row + 0.07f));
            invertButton = UiKit.Button("Invert", t, "OFF", 30, new Vector2(0.31f, row + 0.005f), new Vector2(0.44f, row + 0.065f), () => { Controls.SetInvertY(!Controls.InvertY); RefreshSettings(); });
            UiKit.Label("LookNote", t, "A controller always uses the Xbox layout shown in CONTROLS.", 22, Palette.InkDim, TextAnchor.UpperLeft, new Vector2(0.06f, row - 0.12f), new Vector2(0.45f, row - 0.02f));

            // Left column, below: graphics quality.
            UiKit.Label("GraphicsTitle", t, "GRAPHICS", 40, Palette.Gold, TextAnchor.LowerLeft, new Vector2(0.06f, 0.27f), new Vector2(0.4f, 0.33f));
            float g = 0.19f;
            UiKit.Label("QualityLabel", t, "QUALITY", 30, Palette.Ink, TextAnchor.MiddleLeft, new Vector2(0.06f, g), new Vector2(0.3f, g + 0.07f));
            UiKit.Button("QualityLess", t, "<", 34, new Vector2(0.31f, g + 0.005f), new Vector2(0.35f, g + 0.065f), () => { GraphicsQuality.Cycle(-1); RefreshSettings(); });
            qualityValue = UiKit.Label("QualityValue", t, "", 34, Palette.Gold, TextAnchor.MiddleCenter, new Vector2(0.35f, g), new Vector2(0.46f, g + 0.07f));
            UiKit.Button("QualityMore", t, ">", 34, new Vector2(0.46f, g + 0.005f), new Vector2(0.5f, g + 0.065f), () => { GraphicsQuality.Cycle(1); RefreshSettings(); });
            qualityNote = UiKit.Label("QualityNote", t, "", 22, Palette.InkDim, TextAnchor.UpperLeft, new Vector2(0.06f, g - 0.13f), new Vector2(0.5f, g - 0.01f));

            // Right column: keyboard keys.
            UiKit.Label("KeysTitle", t, "KEYBOARD  (click an action, then press a key)", 32, Palette.Gold, TextAnchor.LowerLeft, new Vector2(0.52f, 0.74f), new Vector2(0.97f, 0.8f));
            for (int i = 0; i < RebindableActions.Length; i++)
            {
                (InputAction action, string label) = RebindableActions[i];
                float y = 0.68f - i * 0.052f;
                UiKit.Label("Action", t, label, 26, Palette.Ink, TextAnchor.MiddleLeft, new Vector2(0.52f, y), new Vector2(0.76f, y + 0.045f));
                keyButtons[action] = UiKit.Button("Key" + action, t, "", 26, new Vector2(0.77f, y + 0.003f), new Vector2(0.95f, y + 0.042f), () => { rebinding = action; RefreshSettings(); });
            }
            UiKit.Label("KeysNote", t, "Esc always pauses. A key already in use swaps with the old one.", 22, Palette.InkDim, TextAnchor.UpperLeft, new Vector2(0.52f, 0.02f), new Vector2(0.97f, 0.06f));
            settingsScreen.SetActive(false);
        }

        private Text SettingRow(Transform t, string label, float y, UnityEngine.Events.UnityAction less, UnityEngine.Events.UnityAction more)
        {
            UiKit.Label(label, t, label, 30, Palette.Ink, TextAnchor.MiddleLeft, new Vector2(0.06f, y), new Vector2(0.3f, y + 0.07f));
            UiKit.Button("Less", t, "<", 34, new Vector2(0.31f, y + 0.005f), new Vector2(0.35f, y + 0.065f), () => { less(); RefreshSettings(); });
            Text value = UiKit.Label("Value", t, "", 34, Palette.Gold, TextAnchor.MiddleCenter, new Vector2(0.35f, y), new Vector2(0.4f, y + 0.07f));
            UiKit.Button("More", t, ">", 34, new Vector2(0.4f, y + 0.005f), new Vector2(0.44f, y + 0.065f), () => { more(); RefreshSettings(); });
            return value;
        }

        /// <summary>Opens and closes the settings screen (used by the capture tour).</summary>
        public void ShowSettingsScreen() => ShowSettings();

        public void HideSettingsScreen() => settingsScreen.SetActive(false);

        private void ShowSettings()
        {
            settingsScreen.SetActive(true);
            settingsScreen.transform.SetAsLastSibling();
            rebinding = null;
            RefreshSettings();
        }

        private void RefreshSettings()
        {
            mouseValue.text = Controls.MouseSensitivity.ToString("0.0");
            stickValue.text = Controls.StickSensitivity.ToString("0.0");
            UiKit.SetButtonText(invertButton, Controls.InvertY ? "ON" : "OFF");
            qualityValue.text = GraphicsQuality.Name(GraphicsQuality.Level);
            qualityNote.text = QualityNotes[(int)GraphicsQuality.Level];
            foreach (KeyValuePair<InputAction, Button> pair in keyButtons)
            {
                bool waiting = rebinding == pair.Key;
                UiKit.SetButtonText(pair.Value, waiting ? "PRESS A KEY..." : Controls.KeyName(Controls.Binding(pair.Key)));
                UiKit.Tint(pair.Value, waiting ? Palette.Accent * 0.8f + new Color(0f, 0f, 0f, 0.2f) : Palette.Button);
            }
        }

        /// <summary>While an action waits for its new key, the next key pressed is bound to it.</summary>
        private void UpdateRebinding()
        {
            if (!rebinding.HasValue || settingsScreen == null || !settingsScreen.activeSelf)
            {
                return;
            }
            UnityEngine.InputSystem.Key key = Controls.KeyPressedThisFrame();
            if (key == UnityEngine.InputSystem.Key.None)
            {
                return;
            }
            Controls.Rebind(rebinding.Value, key);
            rebinding = null;
            RefreshSettings();
        }

        private void ShowControls()
        {
            controlsScreen.SetActive(true);
            controlsScreen.transform.SetAsLastSibling();
            ShowControlsFor(Controls.UsingGamepad);
        }

        private void ShowControlsFor(bool gamepad)
        {
            Texture2D texture = gamepad ? director.Assets.ControlsGamepad : director.Assets.ControlsKeyboard;
            controlsPicture.enabled = texture != null;
            if (texture != null)
            {
                controlsPicture.sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            }
            UiKit.Tint(keyboardTab, gamepad ? Palette.Button : Palette.Accent * 0.8f + new Color(0f, 0f, 0f, 0.2f));
            UiKit.Tint(gamepadTab, gamepad ? Palette.Accent * 0.8f + new Color(0f, 0f, 0f, 0.2f) : Palette.Button);
        }

        /// <summary>With a gamepad, keep a button of the screen on top selected so the menus can be navigated.</summary>
        private void UpdateGamepadSelection()
        {
            EventSystem events = EventSystem.current;
            if (!Controls.UsingGamepad || events == null)
            {
                return;
            }
            GameObject selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy)
            {
                return;
            }
            GameObject screen = settingsScreen != null && settingsScreen.activeSelf ? settingsScreen
                              : controlsScreen.activeSelf ? controlsScreen
                              : menuInfo.activeInHierarchy ? menuInfo
                              : pause.activeSelf ? pause
                              : end.activeSelf ? end
                              : menu.activeSelf ? menu : null;
            Button first = screen != null ? screen.GetComponentInChildren<Button>() : null;
            if (first != null)
            {
                events.SetSelectedGameObject(first.gameObject);
            }
        }

        private void BuildEnd(Transform root)
        {
            end = UiKit.Panel("End", root, Vector2.zero, Vector2.one, new Color(0f, 0f, 0f, 0.94f)).gameObject;
            Transform e = end.transform;
            endTitle = UiKit.Label("Title", e, "", 120, Palette.Accent, TextAnchor.LowerLeft, new Vector2(0.05f, 0.76f), new Vector2(0.5f, 0.93f));
            endReason = UiKit.Label("Reason", e, "", 34, Palette.Ink, TextAnchor.UpperLeft, new Vector2(0.05f, 0.62f), new Vector2(0.46f, 0.76f));
            endBody = UiKit.Label("Body", e, "", 28, Palette.InkDim, TextAnchor.UpperLeft, new Vector2(0.05f, 0.3f), new Vector2(0.46f, 0.6f));

            UiKit.Label("ReplayTitle", e, "HOW FREDDY HUNTED YOU", 36, Palette.Gold, TextAnchor.LowerLeft, new Vector2(0.5f, 0.88f), new Vector2(0.97f, 0.94f));
            UiKit.Label("ReplayLegend", e, "<color=#ff5a4a>o</color> Freddy's route   <color=#80ff99>o</color> your route   <color=#ffc840>o</color> a decision of his neural network",
                        20, Palette.InkDim, TextAnchor.UpperLeft, new Vector2(0.5f, 0.84f), new Vector2(0.97f, 0.88f));
            endMap = MapView.Create("ReplayMap", e, new Vector2(0.5f, 0.38f), new Vector2(0.97f, 0.84f));
            endCatch = endMap.Marker("Caught", Color.white, 22f);
            UiKit.Label("TimelineTitle", e, "HIS LAST DECISIONS (probability the network gave to each choice)", 20, Palette.Gold, TextAnchor.UpperLeft,
                        new Vector2(0.5f, 0.33f), new Vector2(0.97f, 0.37f));
            endTimeline = UiKit.Label("Timeline", e, "", 21, Palette.Ink, TextAnchor.UpperLeft, new Vector2(0.5f, 0.05f), new Vector2(0.97f, 0.33f));

            againButton = UiKit.Button("Again", e, "TRY AGAIN", 40, new Vector2(0.05f, 0.1f), new Vector2(0.23f, 0.18f), director.RetryNight);
            UiKit.Tint(againButton, Palette.Accent * 0.8f + new Color(0f, 0f, 0f, 0.2f));
            nextButton = UiKit.Button("Next", e, "NEXT NIGHT", 40, new Vector2(0.05f, 0.1f), new Vector2(0.23f, 0.18f), director.NextNight);
            UiKit.Tint(nextButton, new Color(0.2f, 0.55f, 0.25f, 1f));
            UiKit.Button("Menu", e, "MAIN MENU", 40, new Vector2(0.25f, 0.1f), new Vector2(0.43f, 0.18f), director.ShowMenu);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>12:00 AM at the start of the night, 6:00 AM at the end.</summary>
        public static string FormatHour(float progress)
        {
            float hours = Mathf.Clamp01(progress) * 6f;
            int hour = Mathf.FloorToInt(hours);
            int minute = Mathf.FloorToInt((hours - hour) * 60f);
            return (hour == 0 ? 12 : hour) + ":" + minute.ToString("00") + " AM";
        }

        private static string FormatClock(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            return total / 60 + ":" + (total % 60).ToString("00");
        }
    }
}
