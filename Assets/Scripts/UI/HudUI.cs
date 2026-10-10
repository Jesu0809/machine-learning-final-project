using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// In-night overlay: clock, power and usage, key hints, the power-outage darkness, and a
    /// debug panel (Tab) that shows what the monster is doing.
    /// </summary>
    public class HudUI : MonoBehaviour
    {
        private const int UsageBars = 4;

        private NightController night;
        private GameObject content;
        private Image outageShade;
        private Text outageLabel;
        private Text clock;
        private Text nightLabel;
        private Text power;
        private Image[] usage;
        private Text debugText;
        private GameObject debugPanel;

        public static HudUI Create(Transform parent, NightController night)
        {
            // The component stays on an always-active object; only the content is hidden.
            var holder = UiFactory.CreateArea("Hud", parent, Vector2.zero, Vector2.one);
            var hud = holder.gameObject.AddComponent<HudUI>();
            hud.night = night;
            var root = UiFactory.CreateArea("Content", holder, Vector2.zero, Vector2.one);
            hud.content = root.gameObject;

            hud.outageShade = UiFactory.CreateImage("OutageShade", root, new Color(0f, 0f, 0f, 0.82f), Vector2.zero, Vector2.one);
            hud.outageLabel = UiFactory.CreateLabel("OutageLabel", root, "POWER OUT", 64, NightColors.Accent, TextAnchor.MiddleCenter,
                                                    new Vector2(0.25f, 0.55f), new Vector2(0.75f, 0.7f), FontStyle.Bold);

            hud.clock = UiFactory.CreateLabel("Clock", root, "12 AM", 72, NightColors.Ink, TextAnchor.UpperRight,
                                              new Vector2(0.7f, 0.86f), new Vector2(0.98f, 0.98f), FontStyle.Bold);
            hud.nightLabel = UiFactory.CreateLabel("Night", root, "Night 1", 28, NightColors.InkDim, TextAnchor.UpperRight,
                                                   new Vector2(0.7f, 0.82f), new Vector2(0.98f, 0.86f));

            hud.power = UiFactory.CreateLabel("Power", root, "Power left: 100%", 30, NightColors.Ink, TextAnchor.MiddleLeft,
                                              new Vector2(0.02f, 0.045f), new Vector2(0.3f, 0.09f), FontStyle.Bold);
            UiFactory.CreateLabel("UsageTitle", root, "Usage:", 24, NightColors.InkDim, TextAnchor.MiddleLeft,
                                  new Vector2(0.02f, 0.01f), new Vector2(0.08f, 0.045f));
            hud.usage = new Image[UsageBars];
            for (int i = 0; i < UsageBars; i++)
            {
                float x = 0.075f + i * 0.022f;
                hud.usage[i] = UiFactory.CreateImage("Usage" + i, root, NightColors.Ok, new Vector2(x, 0.017f), new Vector2(x + 0.017f, 0.04f));
            }

            UiFactory.CreateLabel("Hints", root, "Cameras 1–5   ·   Space open/close   ·   A / D doors   ·   Tab debug   ·   Esc menu",
                                  22, NightColors.InkDim, TextAnchor.MiddleRight, new Vector2(0.6f, 0.01f), new Vector2(0.98f, 0.05f));

            var debugImage = UiFactory.CreateImage("Debug", root, new Color(0f, 0f, 0f, 0.7f), new Vector2(0.01f, 0.7f), new Vector2(0.3f, 0.98f));
            hud.debugPanel = debugImage.gameObject;
            hud.debugText = UiFactory.CreateLabel("Text", debugImage.transform, "", 20, NightColors.Ok, TextAnchor.UpperLeft,
                                                  new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f));
            hud.debugPanel.SetActive(false);
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

            int level = 1 + state.Doors.ClosedCount + (state.Cameras.IsOpen ? 1 : 0);
            for (int i = 0; i < usage.Length; i++)
            {
                usage[i].enabled = i < level;
                usage[i].color = level >= 4 ? NightColors.Accent : level == 3 ? NightColors.Warning : NightColors.Ok;
            }

            bool outage = state.Power.IsDepleted;
            outageShade.enabled = outage;
            outageLabel.enabled = outage;

            if (debugPanel.activeSelf)
            {
                debugText.text = BuildDebugText(state);
            }
        }

        private string BuildDebugText(GameState state)
        {
            string last = state.LastMonsterStep.HasValue
                ? state.LastMonsterStep.Value.From + " → " + state.LastMonsterStep.Value.To + " (" + state.LastMonsterStep.Value.Result + ")"
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
