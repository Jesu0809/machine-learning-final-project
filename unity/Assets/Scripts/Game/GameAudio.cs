using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The player's sound choices (music and sound effects), saved with PlayerPrefs so they are
    /// remembered between sessions.
    /// </summary>
    public static class SoundPrefs
    {
        private const string MusicKey = "NightShift.MusicOn";
        private const string EffectsKey = "NightShift.EffectsOn";

        public static bool MusicOn
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set => Save(MusicKey, value);
        }

        public static bool EffectsOn
        {
            get => PlayerPrefs.GetInt(EffectsKey, 1) == 1;
            set => Save(EffectsKey, value);
        }

        private static void Save(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// All game sound: menu music, the building's hum, the player's footsteps and breath, the
    /// motion tracker and Freddy's heavy steps. Short effects are synthesized in code.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        private const int Rate = 44100;

        private AudioSource music;
        private AudioSource ambient;
        private AudioSource ui;
        private AudioClip footstep;
        private AudioClip click;
        private AudioClip gasp;
        private AudioClip beep;
        private AudioClip heartbeat;
        private AudioClip freddyStep;
        private AudioClip sting;
        private AudioClip staticBurst;
        private AudioClip impact;
        private AudioClip ring;
        private AudioClip horn;
        private AudioClip senseWhisper;

        /// <summary>Freddy's servos: a low mechanical whir that loops while he moves.</summary>
        public AudioClip ServoLoop { get; private set; }

        public GameAssets Assets { get; private set; }

        public static GameAudio Create(Transform parent, GameAssets assets)
        {
            var audio = new GameObject("Audio").AddComponent<GameAudio>();
            audio.transform.SetParent(parent, false);
            audio.Assets = assets;
            audio.music = audio.Source("Music", assets.MenuMusic, true, 0.5f);
            audio.ambient = audio.Source("Ambient", assets.AmbientLoop, true, 0.35f);
            audio.ui = audio.Source("UI", null, false, 1f);

            audio.footstep = Synth("Footstep", 0.12f, (t, i) => Noise(i) * Mathf.Exp(-t * 45f) * 0.6f, 0.5f);
            audio.click = Synth("Click", 0.03f, (t, i) => Noise(i) * Mathf.Exp(-t * 200f), 0f);
            audio.gasp = Synth("Gasp", 0.7f, (t, i) => Noise(i) * Mathf.Sin(Mathf.PI * t / 0.7f) * 0.7f, 0.85f);
            audio.beep = Synth("Beep", 0.07f, (t, i) => Mathf.Sin(2f * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 30f) * 0.5f, 0f);
            audio.heartbeat = Synth("Heartbeat", 0.5f, (t, i) =>
                Mathf.Sin(2f * Mathf.PI * 55f * t) * (Mathf.Exp(-t * 30f) + (t > 0.18f ? Mathf.Exp(-(t - 0.18f) * 30f) * 0.7f : 0f)), 0f);
            audio.freddyStep = Synth("FreddyStep", 0.35f, (t, i) =>
                (Mathf.Sin(2f * Mathf.PI * 48f * t) * 0.9f + Noise(i) * 0.25f + Mathf.Sin(2f * Mathf.PI * 1900f * t) * 0.08f * Mathf.Exp(-t * 60f))
                * Mathf.Exp(-t * 12f), 0.3f);
            audio.ServoLoop = Synth("Servo", 2f, (t, i) =>
                (Mathf.Sin(2f * Mathf.PI * 92f * t + Mathf.Sin(2f * Mathf.PI * 3f * t) * 2f) * 0.25f
                 + Mathf.Sin(2f * Mathf.PI * 184f * t) * 0.08f + Noise(i) * 0.05f)
                * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 1.5f * t)), 0.4f);
            audio.staticBurst = Synth("Static", 1.2f, (t, i) => Noise(i) * 0.8f * (t < 0.9f ? 1f : (1.2f - t) / 0.3f), 0f);
            audio.impact = Synth("Impact", 1.0f, (t, i) =>
                (Mathf.Sin(2f * Mathf.PI * 45f * t * (1f - t * 0.4f)) + Noise(i) * 0.4f) * Mathf.Exp(-t * 4f), 0.5f);
            // Old phone ring: two tones, two bursts.
            audio.ring = Synth("Ring", 2.0f, (t, i) =>
                ((t % 1f) < 0.4f || ((t % 1f) > 0.5f && (t % 1f) < 0.9f) ? 1f : 0f)
                * (Mathf.Sin(2f * Mathf.PI * 440f * t) + Mathf.Sin(2f * Mathf.PI * 480f * t)) * 0.25f
                * (Mathf.Sin(2f * Mathf.PI * 20f * t) > 0f ? 1f : 0.6f), 0f);
            // Party horn: a buzzy, wobbling square-ish tone.
            audio.horn = Synth("PartyHorn", 1.4f, (t, i) =>
                Mathf.Sign(Mathf.Sin(2f * Mathf.PI * (520f + Mathf.Sin(t * 30f) * 25f) * t)) * 0.35f * Mathf.Clamp01((1.4f - t) * 3f) * Mathf.Clamp01(t * 20f), 0.55f);
            // Freddy's sense: a low, breathy whoosh with a distorted rising tone.
            audio.senseWhisper = Synth("Sense", 2.2f, (t, i) =>
                (Noise(i) * 0.5f + Mathf.Sin(2f * Mathf.PI * (60f + t * 80f) * t) * 0.4f) * Mathf.Sin(Mathf.PI * t / 2.2f), 0.85f);
            audio.sting = Synth("Sting", 1.6f, (t, i) =>
                (Mathf.Sin(2f * Mathf.PI * 110f * t * (1f + t * 0.4f)) * 0.4f + Noise(i) * 0.15f) * Mathf.Exp(-t * 2.2f), 0.6f);
            return audio;
        }

        public void PlayMenuMusic(bool on)
        {
            bool play = on && SoundPrefs.MusicOn;
            if (play && !music.isPlaying)
            {
                music.Play();
            }
            else if (!play && music.isPlaying)
            {
                music.Stop();
            }
        }

        public void PlayAmbient(bool on)
        {
            bool play = on && SoundPrefs.EffectsOn;
            if (play && !ambient.isPlaying)
            {
                ambient.Play();
            }
            else if (!play && ambient.isPlaying)
            {
                ambient.Stop();
            }
        }

        public void Footstep(float volume) => Ui(footstep, volume, Random.Range(0.85f, 1.15f));
        public void Click() => Ui(click, 0.4f, 1f);
        public void Gasp() => Ui(gasp, 0.8f, 1f);
        public void Pant(float volume) => Ui(gasp, volume, Random.Range(1.3f, 1.5f));
        public void Beep(float pitch) => Ui(beep, 0.35f, pitch);
        public void Heartbeat(float volume) => Ui(heartbeat, volume, 1f);
        public void Sting() => Ui(sting, 0.7f, 1f);
        public void Scream() => Ui(Assets.Scream, 1f, 1f);
        public void StaticBurst() => Ui(staticBurst, 0.7f, 1f);
        public void Ring() => Ui(ring, 0.5f, 1f);
        public void SenseWhisper() => Ui(senseWhisper, 0.9f, 1f);
        public AudioClip PartyHorn => horn;
        public void Impact() => Ui(impact, 1f, 1f);

        /// <summary>A looping 3D sound that follows a moving object (Freddy's servos).</summary>
        public AudioSource Attach3DLoop(Transform target, AudioClip clip, float maxDistance)
        {
            var source = new GameObject("Loop " + clip.name).AddComponent<AudioSource>();
            source.transform.SetParent(target, false);
            source.transform.localPosition = Vector3.up * 1.5f;
            source.clip = clip;
            source.loop = true;
            source.volume = 0f;
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.mute = !SoundPrefs.EffectsOn;
            source.Play();
            return source;
        }

        /// <summary>A 3D sound at a point in the level (lockers, Freddy, the exit door).</summary>
        public void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch = 1f)
        {
            if (clip == null || !SoundPrefs.EffectsOn)
            {
                return;
            }
            var go = new GameObject("Sound " + clip.name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.pitch = pitch;
            source.spatialBlend = 1f;
            source.minDistance = 2f;
            source.maxDistance = 30f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        public void FreddyStep(Vector3 position, bool running)
        {
            PlayAt(freddyStep, position, running ? 1f : 0.8f, Random.Range(0.85f, 1.05f));
        }

        private void Ui(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || !SoundPrefs.EffectsOn)
            {
                return;
            }
            ui.pitch = pitch;
            ui.PlayOneShot(clip, volume);
        }

        private AudioSource Source(string name, AudioClip clip, bool loop, float volume)
        {
            var source = new GameObject(name).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private static AudioClip lampHum;

        /// <summary>
        /// The buzz of a fluorescent tube: mains hum at 120 Hz with its harmonics and a little
        /// crackle. One second long, it loops without a seam.
        /// </summary>
        public static AudioClip LampHum()
        {
            if (lampHum == null)
            {
                lampHum = Synth("LampHum", 1f, (t, i) =>
                {
                    float w = 2f * Mathf.PI * 120f * t;
                    float buzz = Mathf.Sin(w) * 0.35f + Mathf.Sin(w * 2f) * 0.25f + Mathf.Sin(w * 3f) * 0.1f;
                    buzz += Mathf.Sign(Mathf.Sin(w)) * 0.06f;
                    return buzz * 0.6f + Noise(i) * 0.03f;
                }, 0.2f);
            }
            return lampHum;
        }

        private static AudioClip honk;

        /// <summary>
        /// A plush's squeeze horn: a reedy, nasal honk that bends up and back down, with the
        /// rattle of the reed in it.
        /// </summary>
        public static AudioClip Honk()
        {
            if (honk == null)
            {
                float phase = 0f;
                honk = Synth("Honk", 0.42f, (t, i) =>
                {
                    float pitch = 330f + 70f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.36f));
                    phase += 2f * Mathf.PI * pitch / Rate;
                    float reed = Mathf.Sign(Mathf.Sin(phase)) * 0.35f + Mathf.Sin(phase * 2f) * 0.3f + Mathf.Sin(phase * 3f) * 0.22f + Mathf.Sin(phase * 5f) * 0.1f;
                    float rattle = 0.78f + 0.22f * Mathf.Sin(2f * Mathf.PI * 34f * t);
                    float envelope = Mathf.Clamp01(t / 0.015f) * Mathf.Clamp01((0.42f - t) / 0.08f);
                    return reed * rattle * envelope * 0.8f + Noise(i) * 0.02f * envelope;
                }, 0.45f);
            }
            return honk;
        }

        private static AudioClip clunk;
        private static AudioClip ratchet;
        private static AudioClip charge;

        /// <summary>A heavy steel clunk: a low thud with a metallic ring and a burst of grit.</summary>
        public static AudioClip Clunk()
        {
            if (clunk == null)
            {
                clunk = Synth("Clunk", 0.45f, (t, i) =>
                {
                    float thud = Mathf.Sin(2f * Mathf.PI * (70f - t * 40f) * t) * Mathf.Exp(-t * 18f);
                    float ring = (Mathf.Sin(2f * Mathf.PI * 410f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 1130f * t) * 0.3f) * Mathf.Exp(-t * 14f);
                    float grit = Noise(i) * Mathf.Exp(-t * 60f);
                    return thud * 0.9f + ring * 0.35f + grit * 0.5f;
                }, 0.35f);
            }
            return clunk;
        }

        /// <summary>One click of a ratchet.</summary>
        public static AudioClip Ratchet()
        {
            if (ratchet == null)
            {
                ratchet = Synth("Ratchet", 0.08f, (t, i) =>
                    (Noise(i) * 0.6f + Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.5f) * Mathf.Exp(-t * 90f), 0.1f);
            }
            return ratchet;
        }

        /// <summary>A capacitor charging: a whine rising for most of a second, with a crackle.</summary>
        public static AudioClip Charge()
        {
            if (charge == null)
            {
                float phase = 0f;
                charge = Synth("Charge", 1f, (t, i) =>
                {
                    phase += 2f * Mathf.PI * (300f + t * t * 1500f) / Rate;
                    float swell = Mathf.Clamp01(t * 3f) * (1f - Mathf.Clamp01((t - 0.9f) * 10f));
                    float crackle = Noise(i) * (Noise(i / 300) > 0.7f ? 0.3f : 0.03f);
                    return (Mathf.Sin(phase) * 0.35f + Mathf.Sin(phase * 2f) * 0.12f + crackle) * swell;
                }, 0.15f);
            }
            return charge;
        }

        private static AudioClip Synth(string name, float seconds, System.Func<float, int, float> sample, float lowPass)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            float previous = 0f;
            for (int i = 0; i < count; i++)
            {
                float value = sample(i / (float)Rate, i);
                // Simple one-pole low-pass to soften raw noise (0 = off).
                previous = lowPass > 0f ? Mathf.Lerp(value, previous, lowPass) : value;
                data[i] = Mathf.Clamp(previous, -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Noise(int i)
        {
            uint h = (uint)i * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFF) / 32767.5f - 1f;
        }
    }
}
