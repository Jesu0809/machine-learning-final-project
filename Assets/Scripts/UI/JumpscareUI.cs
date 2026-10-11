using NightShift.Core;
using UnityEngine;
using UnityEngine.UI;

namespace NightShift.UI
{
    /// <summary>
    /// End of the night. When the monster gets in: face → beak open → scream in quick succession,
    /// with zoom, shake and the scream sound, then a burst of static and the summary. When the guard
    /// survives: the "6 AM" screen and the summary.
    /// </summary>
    public class JumpscareUI : MonoBehaviour
    {
        private const float FrameFace = 0.14f;
        private const float FrameBeak = 0.28f;
        private const float ScareEnd = 1.7f;
        private const float StaticEnd = 2.05f;

        private NightController night;
        private NightArt art;
        private NightAudio audioPlayer;
        private GameObject content;
        private Image scareImage;
        private RectTransform scareRect;
        private Image redFlash;
        private RawImage staticNoise;
        private Text fiveAm;
        private Text sixAm;
        private Text amLabel;
        private GameObject summary;
        private Text summaryTitle;
        private Text summaryStats;
        private bool showing;
        private bool screamPlayed;

        public static JumpscareUI Create(Transform parent, NightController night, NightArt art, NightAudio audioPlayer)
        {
            var holder = UiFactory.CreateArea("EndOfNight", parent, Vector2.zero, Vector2.one);
            var end = holder.gameObject.AddComponent<JumpscareUI>();
            end.night = night;
            end.art = art;
            end.audioPlayer = audioPlayer;

            var content = UiFactory.CreateFullScreen("Content", holder, Color.black);
            end.content = content.gameObject;
            Transform c = content.transform;

            end.scareImage = UiFactory.CreateSprite("Scare", c, art.jumpscare[0], Vector2.zero, Vector2.one, keepAspect: true);
            end.scareRect = end.scareImage.rectTransform;
            end.redFlash = UiFactory.CreateImage("RedFlash", c, new Color(1f, 0f, 0f, 0f), Vector2.zero, Vector2.one);
            end.staticNoise = UiFactory.CreateRawImage("Static", c, UiFactory.NoiseTexture, new Color(1f, 1f, 1f, 0f), Vector2.zero, Vector2.one);

            // The hour digit rolls from 5 to 6 like a mechanical clock; "AM" stays still.
            var digit = UiFactory.CreateArea("Digit", c, new Vector2(0.3f, 0.5f), new Vector2(0.47f, 0.82f));
            digit.gameObject.AddComponent<RectMask2D>();
            end.fiveAm = UiFactory.CreateLabel("Five", digit, "5", 260, NightColors.Ink, TextAnchor.MiddleRight, Vector2.zero, Vector2.one);
            end.sixAm = UiFactory.CreateLabel("Six", digit, "6", 260, NightColors.Ink, TextAnchor.MiddleRight, Vector2.zero, Vector2.one);
            end.amLabel = UiFactory.CreateLabel("AM", c, "AM", 260, NightColors.Ink, TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(0.75f, 0.82f));

            var summary = UiFactory.CreateArea("Summary", c, new Vector2(0.2f, 0.06f), new Vector2(0.8f, 0.44f));
            end.summary = summary.gameObject;
            end.summaryTitle = UiFactory.CreateLabel("Title", summary, "", 72, NightColors.Accent, TextAnchor.MiddleCenter,
                                                     new Vector2(0f, 0.68f), new Vector2(1f, 1f));
            end.summaryStats = UiFactory.CreateLabel("Stats", summary, "", 36, NightColors.InkDim, TextAnchor.MiddleCenter,
                                                     new Vector2(0f, 0.36f), new Vector2(1f, 0.68f));
            UiFactory.CreateButton("Retry", summary, "PLAY AGAIN  [R]", 40, new Vector2(0.08f, 0.04f), new Vector2(0.48f, 0.26f), night.StartNight);
            UiFactory.CreateButton("Menu", summary, "MENU  [ESC]", 40, new Vector2(0.52f, 0.04f), new Vector2(0.92f, 0.26f), night.GoToMenu);

            end.content.SetActive(false);
            return end;
        }

        private void Update()
        {
            if (night.Phase == NightPhase.Ended && Elapsed() > 0.4f)
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
            if (!visible)
            {
                if (showing)
                {
                    // Leaving the screen early (Play again / Menu) cuts the scream.
                    audioPlayer.StopEffects();
                }
                showing = false;
                screamPlayed = false;
                content.SetActive(false);
                return;
            }
            showing = true;
            content.SetActive(true);

            GameState state = night.State;
            float t = Elapsed();
            bool caught = state.Outcome == NightOutcome.MonsterEntered;

            if (caught)
            {
                ShowScare(t);
            }
            else
            {
                scareImage.enabled = false;
                redFlash.color = Color.clear;
                staticNoise.color = new Color(1f, 1f, 1f, 0.015f);
                UiFactory.AnimateNoise(staticNoise, 3f);
                fiveAm.enabled = true;
                sixAm.enabled = true;
                amLabel.enabled = true;
                float roll = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1.6f, t));
                float rowHeight = 345f;
                fiveAm.rectTransform.anchoredPosition = new Vector2(0f, roll * rowHeight);
                sixAm.rectTransform.anchoredPosition = new Vector2(0f, (roll - 1f) * rowHeight);
            }

            bool showSummary = caught ? t >= StaticEnd : t >= 1.8f;
            summary.SetActive(showSummary);
            if (showSummary)
            {
                summaryTitle.text = caught ? "THE MONSTER GOT IN" : "YOU SURVIVED THE NIGHT";
                summaryTitle.color = caught ? NightColors.Accent : NightColors.Ok;
                summaryStats.text = BuildStats(state, caught);
            }
        }

        private void ShowScare(float t)
        {
            fiveAm.enabled = false;
            sixAm.enabled = false;
            amLabel.enabled = false;

            if (!screamPlayed)
            {
                screamPlayed = true;
                audioPlayer.PlayScream();
            }

            bool scaring = t < ScareEnd;
            scareImage.enabled = scaring;
            if (scaring)
            {
                int frame = t < FrameFace ? 0 : t < FrameBeak ? 1 : 2;
                scareImage.sprite = art.jumpscare[frame];

                float zoom = Mathf.Lerp(1f, 1.18f, Mathf.Clamp01(t / FrameBeak)) + 0.03f * Mathf.Sin(t * 40f) * (1f - t / ScareEnd);
                scareRect.localScale = Vector3.one * zoom;
                float shake = 45f * (1f - t / ScareEnd);
                scareRect.anchoredPosition = Random.insideUnitCircle * shake;
                redFlash.color = new Color(1f, 0f, 0f, t < 0.3f && Mathf.Repeat(t, 0.1f) < 0.04f ? 0.1f : 0f);
            }
            else
            {
                redFlash.color = Color.clear;
            }

            // A burst of static closes the scare, then it settles to a faint background.
            float noise = t < ScareEnd ? 0f : t < StaticEnd ? 1f : 0.015f;
            staticNoise.color = new Color(1f, 1f, 1f, noise);
            UiFactory.AnimateNoise(staticNoise, 3f);
        }

        private string BuildStats(GameState state, bool caught)
        {
            string when = caught && state.EnteredAtSeconds.HasValue
                ? "It got in at " + UiFactory.FormatHour(HourAt(state, state.EnteredAtSeconds.Value)) + "   ·   "
                : "";
            return when + "Night " + night.NightNumber + "   ·   " + night.Monster.PolicyName + " monster\n" +
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
