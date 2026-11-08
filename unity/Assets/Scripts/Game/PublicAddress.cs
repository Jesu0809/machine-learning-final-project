using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The building's speakers. At the start of each night Pockets explains the night and tells the
    /// story while he sleeps in the ring, and the building is free to explore (the objectives only
    /// appear when he stops talking); when he stops, or the player skips with Enter, he wakes up.
    /// During the night it plays his short taunts, which never interrupt anything.
    /// </summary>
    public class PublicAddress : MonoBehaviour
    {
        private GameAudio audio;
        private AudioSource voice;
        private AudioClip[] clips;
        private string[] lines = new string[0];
        private GameAssets assets;
        private int line = -1;
        private float ringUntil;
        private string taunt = "";

        public bool Active { get; private set; }

        /// <summary>True while a taunt or the night's speech is being heard.</summary>
        public bool Talking => Active || voice.isPlaying;

        /// <summary>The subtitle on screen right now ("" when nothing is said).</summary>
        public string Subtitle => Active && line >= 0 && line < lines.Length ? lines[line] : voice.isPlaying ? taunt : "";

        public static PublicAddress Create(Transform parent, GameAudio audio, GameAssets assets)
        {
            var speakers = new GameObject("PublicAddress").AddComponent<PublicAddress>();
            speakers.transform.SetParent(parent, false);
            speakers.audio = audio;
            speakers.assets = assets;
            speakers.voice = speakers.gameObject.AddComponent<AudioSource>();
            speakers.voice.playOnAwake = false;
            speakers.voice.spatialBlend = 0f;
            speakers.voice.volume = 1f;
            return speakers;
        }

        public bool HasVoice => clips != null && clips.Length > 0;

        /// <summary>Starts the speech of <paramref name="night"/> (1–7), if it has voice clips.</summary>
        public void Begin(int night)
        {
            int index = night - 1;
            clips = assets.Calls != null && index < assets.Calls.Length ? assets.Calls[index].Lines : null;
            lines = index < StoryData.Calls.Length ? StoryData.Calls[index] : new string[0];
            Active = HasVoice;
            line = -1;
            ringUntil = Time.time + 2.2f;
            voice.Stop();
            if (Active)
            {
                audio.Ring();
            }
        }

        /// <summary>No speech tonight (the encore and the show of the day).</summary>
        public void Silence()
        {
            voice.Stop();
            Active = false;
        }

        /// <summary>A short line during the night; ignored if he is already talking or it has no clip.</summary>
        public bool Say(AudioClip clip, string text)
        {
            if (clip == null || Talking)
            {
                return false;
            }
            taunt = text;
            voice.clip = clip;
            voice.Play();
            return true;
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
            if (Time.timeScale == 0f)
            {
                if (voice.isPlaying)
                {
                    voice.Pause();
                }
                return;
            }
            if (!Active)
            {
                if (voice.clip != null && !voice.isPlaying && voice.time > 0f && voice.time < voice.clip.length - 0.05f)
                {
                    voice.UnPause();
                }
                return;
            }
            if (Time.time < ringUntil || voice.isPlaying)
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
