using UnityEngine;

namespace NightShift.UI
{
    /// <summary>
    /// Sound for the night: the office hum and the monitor static as loops, plus one-shot effects.
    /// The loops follow the game state every frame, so no other script has to start or stop them.
    /// </summary>
    public class NightAudio : MonoBehaviour
    {
        private const float HumVolume = 0.45f;
        private const float HumVolumeOnCameras = 0.15f;
        private const float StaticVolume = 0.35f;

        private NightController night;
        private NightArt art;
        private AudioSource hum;
        private AudioSource monitor;
        private AudioSource effects;

        public static NightAudio Create(Transform parent, NightController night, NightArt art)
        {
            var go = new GameObject("NightAudio");
            go.transform.SetParent(parent, false);
            var audio = go.AddComponent<NightAudio>();
            audio.night = night;
            audio.art = art;
            audio.hum = audio.CreateSource("Hum", art.ambientHum, true);
            audio.monitor = audio.CreateSource("MonitorStatic", art.staticLoop, true);
            audio.effects = audio.CreateSource("Effects", null, false);

            if (FindAnyObjectByType<AudioListener>() == null)
            {
                go.AddComponent<AudioListener>();
            }
            return audio;
        }

        private AudioSource CreateSource(string name, AudioClip clip, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            return source;
        }

        public void Play(AudioClip clip, float volume = 1f)
        {
            if (clip != null)
            {
                effects.PlayOneShot(clip, volume);
            }
        }

        public void PlayCameraSwitch() => Play(art.cameraSwitch, 0.7f);
        public void PlayDoorSlide() => Play(art.doorSlide, 0.8f);
        public void PlayDoorSlam() => Play(art.doorSlam, 0.9f);
        public void PlayPowerOut() => Play(art.powerOut, 1f);
        public void PlayScream() => Play(art.scream, 1f);

        /// <summary>Stops every effect immediately (used when a night is restarted).</summary>
        public void StopEffects()
        {
            effects.Stop();
        }

        private void Update()
        {
            bool playing = night.Phase == NightPhase.Playing;
            bool powered = !night.State.Power.IsDepleted;
            bool camerasOpen = playing && night.State.Cameras.IsOpen;

            float humTarget = playing && powered ? (camerasOpen ? HumVolumeOnCameras : HumVolume) : 0f;
            float staticTarget = camerasOpen ? StaticVolume : 0f;
            Fade(hum, humTarget);
            Fade(monitor, staticTarget);
        }

        private static void Fade(AudioSource source, float target)
        {
            if (source.clip == null)
            {
                return;
            }
            source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * 2f);
            if (source.volume > 0f && !source.isPlaying)
            {
                source.Play();
            }
            else if (source.volume <= 0f && source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
