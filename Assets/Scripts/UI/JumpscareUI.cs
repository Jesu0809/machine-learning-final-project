using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// End of the night: an animated jumpscare when the monster gets in, or the "6 AM" screen when
    /// the guard survives. Both end with the night's stats and the options to play again.
    /// </summary>
    public class JumpscareUI : MonoBehaviour
    {
        private const float ScareSeconds = 1.6f;

        private NightController night;
        private GameObject content;
        private GameObject scare;
        private RectTransform face;
        private Image redFlash;
        private GameObject survived;
        private Text survivedTitle;
        private GameObject summary;
        private Text summaryTitle;
        private Text summaryStats;

        public static JumpscareUI Create(Transform parent, NightController night)
        {
            var holder = UiFactory.CreateArea("EndOfNight", parent, Vector2.zero, Vector2.one);
            var end = holder.gameObject.AddComponent<JumpscareUI>();
            end.night = night;

            var content = UiFactory.CreateFullScreen("Content", holder, Color.black);
            end.content = content.gameObject;

            // Jumpscare
            var scare = UiFactory.CreateArea("Scare", content.transform, Vector2.zero, Vector2.one);
            end.scare = scare.gameObject;
            end.face = UiFactory.CreateArea("Face", scare, new Vector2(0.25f, 0.05f), new Vector2(0.75f, 0.95f));
            UiFactory.CreateImage("Head", end.face, NightColors.MonsterBody, new Vector2(0.05f, 0f), new Vector2(0.95f, 0.9f));
            UiFactory.CreateImage("EarLeft", end.face, NightColors.MonsterBody, new Vector2(0.05f, 0.85f), new Vector2(0.25f, 1f));
            UiFactory.CreateImage("EarRight", end.face, NightColors.MonsterBody, new Vector2(0.75f, 0.85f), new Vector2(0.95f, 1f));
            UiFactory.CreateImage("EyeLeft", end.face, NightColors.MonsterEyes, new Vector2(0.2f, 0.58f), new Vector2(0.38f, 0.68f));
            UiFactory.CreateImage("EyeRight", end.face, NightColors.MonsterEyes, new Vector2(0.62f, 0.58f), new Vector2(0.8f, 0.68f));
            UiFactory.CreateImage("Mouth", end.face, new Color32(70, 0, 8, 255), new Vector2(0.2f, 0.12f), new Vector2(0.8f, 0.36f));
            for (int tooth = 0; tooth < 6; tooth++)
            {
                float x = 0.22f + tooth * 0.095f;
                UiFactory.CreateImage("Tooth", end.face, new Color32(230, 226, 210, 255), new Vector2(x, 0.27f), new Vector2(x + 0.06f, 0.36f));
                UiFactory.CreateImage("Tooth", end.face, new Color32(230, 226, 210, 255), new Vector2(x + 0.03f, 0.12f), new Vector2(x + 0.09f, 0.2f));
            }
            end.redFlash = UiFactory.CreateImage("RedFlash", scare, new Color(1f, 0f, 0f, 0f), Vector2.zero, Vector2.one);

            // Survived
            var survived = UiFactory.CreateArea("Survived", content.transform, Vector2.zero, Vector2.one);
            end.survived = survived.gameObject;
            end.survivedTitle = UiFactory.CreateLabel("Title", survived, "6 AM", 180, NightColors.Ink, TextAnchor.MiddleCenter,
                                                      new Vector2(0f, 0.45f), new Vector2(1f, 0.85f), FontStyle.Bold);

            // Summary shown after either ending
            var summary = UiFactory.CreateArea("Summary", content.transform, new Vector2(0.25f, 0.08f), new Vector2(0.75f, 0.45f));
            end.summary = summary.gameObject;
            end.summaryTitle = UiFactory.CreateLabel("Title", summary, "", 52, NightColors.Accent, TextAnchor.MiddleCenter,
                                                     new Vector2(0f, 0.7f), new Vector2(1f, 1f), FontStyle.Bold);
            end.summaryStats = UiFactory.CreateLabel("Stats", summary, "", 26, NightColors.InkDim, TextAnchor.MiddleCenter,
                                                     new Vector2(0f, 0.38f), new Vector2(1f, 0.7f));
            UiFactory.CreateButton("Retry", summary, "PLAY AGAIN  [R]", 28, new Vector2(0.06f, 0.05f), new Vector2(0.48f, 0.28f), night.StartNight);
            UiFactory.CreateButton("Menu", summary, "MENU  [Esc]", 28, new Vector2(0.52f, 0.05f), new Vector2(0.94f, 0.28f), night.GoToMenu);

            end.content.SetActive(false);
            return end;
        }

        private void Update()
        {
            if (night.Phase == NightPhase.Ended && Elapsed() > 0.3f)
            {
                if (KeyboardInput.WasPressed(GameKey.Restart))
                {
                    night.StartNight();
                }
                else if (KeyboardInput.WasPressed(GameKey.Back))
                {
                    night.GoToMenu();
                }
            }
            Refresh();
        }

        public void Refresh()
        {
            bool visible = night.Phase == NightPhase.Ended;
            content.SetActive(visible);
            if (!visible)
            {
                return;
            }

            GameState state = night.State;
            float t = Elapsed();
            bool caught = state.Outcome == NightOutcome.MonsterEntered;

            scare.SetActive(caught && t < ScareSeconds);
            survived.SetActive(!caught);
            summary.SetActive(!caught || t >= ScareSeconds);

            if (caught && t < ScareSeconds)
            {
                AnimateScare(t);
            }
            if (!caught)
            {
                survivedTitle.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t / 1.2f));
            }

            summaryTitle.text = caught ? "THE MONSTER GOT IN" : "YOU SURVIVED THE NIGHT";
            summaryTitle.color = caught ? NightColors.Accent : NightColors.Ok;
            summaryStats.text = BuildStats(state, caught);
        }

        private void AnimateScare(float t)
        {
            float grow = Mathf.Clamp01(t / 0.25f);
            face.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.25f, grow);
            float shake = 30f * (1f - t / ScareSeconds);
            face.anchoredPosition = new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));
            redFlash.color = new Color(1f, 0f, 0f, Mathf.Repeat(t, 0.16f) < 0.08f ? 0.35f : 0f);
        }

        private string BuildStats(GameState state, bool caught)
        {
            string when = caught && state.EnteredAtSeconds.HasValue
                ? "Got in at " + UiFactory.FormatHour(HourAt(state, state.EnteredAtSeconds.Value)) + " · "
                : "";
            return when +
                   "Night " + night.NightNumber + " · " + night.Monster.PolicyName + " monster\n" +
                   "Power left: " + Mathf.CeilToInt(state.Power.Normalized * 100f) + "%   ·   " +
                   "Times a door stopped it: " + state.DoorBlocks;
        }

        private static int HourAt(GameState state, float seconds)
        {
            float progress = Mathf.Clamp01(seconds / state.Settings.NightDurationSeconds);
            return Mathf.Min(NightClock.HoursPerNight, (int)(progress * NightClock.HoursPerNight));
        }

        private float Elapsed()
        {
            return Time.unscaledTime - night.PhaseStartedAt;
        }
    }
}
