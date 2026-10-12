using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The phone call at the start of each night, in the tradition of the original games: a voice
    /// explains the night and tells the story while Freddy sleeps on the stage, and the building
    /// is free to explore (the objectives only appear when the call ends).
    /// When the call ends (or the player skips it with Enter), Freddy wakes up.
    /// </summary>
    public class PhoneCall : MonoBehaviour
    {
        private GameAudio audio;
        private AudioSource voice;
        private AudioClip[] clips;
        private string[] lines = new string[0];
        private GameAssets assets;
        private int line = -1;
        private float ringUntil;

        public bool Active { get; private set; }

        /// <summary>The subtitle on screen right now ("" when nothing is said).</summary>
        public string Subtitle => Active && line >= 0 && line < lines.Length ? lines[line] : "";

        public static PhoneCall Create(Transform parent, GameAudio audio, GameAssets assets)
        {
            var call = new GameObject("PhoneCall").AddComponent<PhoneCall>();
            call.transform.SetParent(parent, false);
            call.audio = audio;
            call.assets = assets;
            call.voice = call.gameObject.AddComponent<AudioSource>();
            call.voice.playOnAwake = false;
            call.voice.spatialBlend = 0f;
            call.voice.volume = 1f;
            return call;
        }

        public bool HasVoice => clips != null && clips.Length > 0;

        /// <summary>Starts the call of <paramref name="night"/> (1–5), if it has voice clips.</summary>
        public void Begin(int night)
        {
            int index = night - 1;
            clips = assets.Calls != null && index < assets.Calls.Length ? assets.Calls[index].Lines : null;
            lines = index < StoryData.Calls.Length ? StoryData.Calls[index] : new string[0];
            Active = HasVoice;
            line = -1;
            ringUntil = Time.time + 2.2f;
            if (Active)
            {
                audio.Ring();
            }
        }

        public void Skip()
        {
            if (Active)
            {
                voice.Stop();
                audio.Click();
                Active = false;
            }
        }

        private void Update()
        {
            voice.mute = !SoundPrefs.EffectsOn;
            if (!Active || Time.time < ringUntil || Time.timeScale == 0f)
            {
                if (Time.timeScale == 0f && voice.isPlaying)
                {
                    voice.Pause();
                }
                return;
            }
            if (voice.isPlaying)
            {
                return;
            }
            if (line >= 0 && voice.time > 0f && voice.clip != null && voice.time < voice.clip.length - 0.05f)
            {
                voice.UnPause();
                return;
            }
            line++;
            if (line >= clips.Length || line >= lines.Length)
            {
                audio.Click();
                Active = false;
                return;
            }
            voice.clip = clips[line];
            voice.Play();
        }
    }
}
