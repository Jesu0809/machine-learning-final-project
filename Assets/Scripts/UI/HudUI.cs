using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// In-night overlay: clock, power and usage, key hints, and a debug panel (Tab) that shows what
    /// the monster is doing.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        private const int UsageBars = 4;

        private NightController night;
        private GameObject content;
        private Text clock;
        private Text nightLabel;
        private Text power;
        private Image[] usage;
        private Text debugText;
        private GameObject debugPanel;
        private Image introCard;
        private Text introTime;
        private Text introNight;

        public static HudUI Create(Transform parent, NightController night)
        {
            // The component stays on an always-active object; only the content is hidden.
            var holder = UiFactory.CreateArea("Hud", parent, Vector2.zero, Vector2.one);
            var hud = holder.gameObject.AddComponent<HudUI>();
            hud.night = night;
            var root = UiFactory.CreateArea("Content", holder, Vector2.zero, Vector2.one);
            hud.content = root.gameObject;

            var top = UiFactory.CreateRawImage("TopShade", root, UiFactory.VerticalFadeTexture, new Color(1f, 1f, 1f, 0.7f), new Vector2(0f, 0.8f), Vector2.one);
            top.uvRect = new Rect(0f, 1f, 1f, -1f);
            UiFactory.CreateRawImage("BottomShade", root, UiFactory.VerticalFadeTexture, new Color(1f, 1f, 1f, 0.75f), Vector2.zero, new Vector2(1f, 0.16f));

            hud.clock = UiFactory.CreateLabel("Clock", root, "12 AM", 96, NightColors.Ink, TextAnchor.UpperRight,
                                              new Vector2(0.7f, 0.85f), new Vector2(0.985f, 0.99f));
            hud.nightLabel = UiFactory.CreateLabel("Night", root, "Night 1", 36, NightColors.InkDim, TextAnchor.UpperRight,
                                                   new Vector2(0.7f, 0.8f), new Vector2(0.985f, 0.86f));

            hud.power = UiFactory.CreateLabel("Power", root, "Power left: 100%", 42, NightColors.Ink, TextAnchor.MiddleLeft,
                                              new Vector2(0.015f, 0.05f), new Vector2(0.35f, 0.1f));
            UiFactory.CreateLabel("UsageTitle", root, "Usage:", 34, NightColors.InkDim, TextAnchor.MiddleLeft,
                                  new Vector2(0.015f, 0.01f), new Vector2(0.08f, 0.05f));
            hud.usage = new Image[UsageBars];
            for (int i = 0; i < UsageBars; i++)
            {
                float x = 0.075f + i * 0.02f;
                hud.usage[i] = UiFactory.CreateImage("Usage" + i, root, NightColors.Ok, new Vector2(x, 0.017f), new Vector2(x + 0.015f, 0.043f));
            }

            UiFactory.CreateLabel("Hints", root, "1-5 CAMS · SPACE MONITOR · A/D DOORS · TAB DEBUG · ESC MENU",
                                  24, NightColors.InkDim, TextAnchor.LowerRight, new Vector2(0.6f, 0.012f), new Vector2(0.99f, 0.05f));

            var debugImage = UiFactory.CreateImage("Debug", root, new Color(0f, 0f, 0f, 0.75f), new Vector2(0.01f, 0.68f), new Vector2(0.32f, 0.98f));
            hud.debugPanel = debugImage.gameObject;
            hud.debugText = UiFactory.CreateLabel("Text", debugImage.transform, "", 28, NightColors.Ok, TextAnchor.UpperLeft,
                                                  new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f));
            hud.debugPanel.SetActive(false);

            // Title card at the start of every night: "12:00 AM / Night 1", then it fades out.
            hud.introCard = UiFactory.CreateFullScreen("Intro", root, Color.black);
            hud.introCard.raycastTarget = false;
            hud.introTime = UiFactory.CreateLabel("Time", hud.introCard.transform, "12:00 AM", 120, NightColors.Ink, TextAnchor.LowerCenter,
                                                  new Vector2(0f, 0.5f), new Vector2(1f, 0.66f));
            hud.introNight = UiFactory.CreateLabel("Night", hud.introCard.transform, "Night 1", 64, NightColors.Ink, TextAnchor.UpperCenter,
                                                   new Vector2(0f, 0.38f), new Vector2(1f, 0.5f));
            return hud;
        }

        private void Update()
        {
            if (KeyboardInput.WasPressed(GameKey.Debug))
            {
                debugPanel.SetActive(!debugPanel.activeSelf);
            }
            Refresh();
        }

        public void Refresh()
        {
            bool visible = night.Phase == NightPhase.Playing;
            content.SetActive(visible);
            if (!visible)
            {
                return;
            }

            GameState state = night.State;
            clock.text = UiFactory.FormatHour(state.Clock.CurrentHour);
            nightLabel.text = "Night " + night.NightNumber;

            int percent = Mathf.CeilToInt(state.Power.Normalized * 100f);
            power.text = "Power left: " + percent + "%";
            power.color = percent > 30 ? NightColors.Ink : percent > 10 ? NightColors.Warning : NightColors.Accent;

            int level = state.Power.IsDepleted ? 0 : 1 + state.Doors.ClosedCount + (state.Cameras.IsOpen ? 1 : 0);
            for (int i = 0; i < usage.Length; i++)
            {
                usage[i].enabled = i < level;
                usage[i].color = level >= 4 ? NightColors.Accent : level == 3 ? NightColors.Warning : NightColors.Ok;
            }

            if (debugPanel.activeSelf)
            {
                debugText.text = BuildDebugText(state);
            }

            float sinceStart = Time.unscaledTime - night.PhaseStartedAt;
            float intro = 1f - Mathf.Clamp01((sinceStart - 1.4f) / 1.1f);
            introCard.enabled = intro > 0f;
            introCard.color = new Color(0f, 0f, 0f, intro);
            introTime.color = new Color(1f, 1f, 1f, intro);
            introNight.color = new Color(1f, 1f, 1f, intro);
            introNight.text = "Night " + night.NightNumber;
        }

        private string BuildDebugText(GameState state)
        {
            string last = state.LastMonsterStep.HasValue
                ? state.LastMonsterStep.Value.From + " -> " + state.LastMonsterStep.Value.To + " (" + state.LastMonsterStep.Value.Result + ")"
                : "none yet";
            return "DEBUG (Tab)\n" +
                   "Monster: " + night.Monster.PolicyName + "\n" +
                   "Room: " + state.MonsterRoom + "\n" +
                   "Next move in: " + night.SecondsToNextDecision.ToString("0.0") + " s\n" +
                   "Last move: " + last + "\n" +
                   "Decisions: " + state.StepCount + "   Door blocks: " + state.DoorBlocks + "\n" +
                   "Time: " + state.Clock.ElapsedSeconds.ToString("0") + " / " + state.Settings.NightDurationSeconds.ToString("0") + " s";
        }
    }
}
