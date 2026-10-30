using System.Collections.Generic;
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
        private AudioSource dread;
        private AudioClip footstep;
        private AudioClip click;
        private AudioClip gasp;
        private AudioClip beep;
        private AudioClip heartbeat;
        private AudioClip[] freddySteps;
        private AudioClip[] freddyServos;
        private AudioClip[] freddyCreaks;
        private int lastStep = -1;
        private int lastCreak = -1;
        private AudioClip sting;
        private AudioClip staticBurst;
        private AudioClip impact;
        private AudioClip ring;
        private AudioClip horn;
        private AudioClip senseWhisper;
        private bool musicWanted;
        private bool ambientWanted;
        private bool restartLoops;

        /// <summary>Looping sounds in the level that must keep playing (lamp hums, the fridge).</summary>
        private static readonly List<AudioSource> Loops = new List<AudioSource>();

        public GameAssets Assets { get; private set; }

        public static GameAudio Create(Transform parent, GameAssets assets)
        {
            var audio = new GameObject("Audio").AddComponent<GameAudio>();
            audio.transform.SetParent(parent, false);
            audio.Assets = assets;
            audio.music = audio.Source("Music", assets.MenuMusic, true, 0.5f);
            // Kept low: the building's hum must never cover Freddy.
            audio.ambient = audio.Source("Ambient", assets.AmbientLoop, true, 0.1f);
            audio.ui = audio.Source("UI", null, false, 1f);
            // A low, uneasy drone that rises only when Freddy is close (no direction: that is the pressure).
            audio.dread = audio.Source("Dread", Synth("Dread", 4f, (t, i) =>
                (Mathf.Sin(2f * Mathf.PI * 41f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 43.5f * t) * 0.4f
                 + Mathf.Sin(2f * Mathf.PI * 123f * t + Mathf.Sin(2f * Mathf.PI * 0.5f * t) * 3f) * 0.12f + Noise(i) * 0.04f) * 0.8f, 0.6f), true, 0f);

            audio.footstep = Synth("Footstep", 0.12f, (t, i) => Noise(i) * Mathf.Exp(-t * 45f) * 0.6f, 0.5f);
            audio.click = Synth("Click", 0.03f, (t, i) => Noise(i) * Mathf.Exp(-t * 200f), 0f);
            audio.gasp = Synth("Gasp", 0.7f, (t, i) => Noise(i) * Mathf.Sin(Mathf.PI * t / 0.7f) * 0.7f, 0.85f);
            audio.beep = Synth("Beep", 0.07f, (t, i) => Mathf.Sin(2f * Mathf.PI * 1250f * t) * Mathf.Exp(-t * 30f) * 0.5f, 0f);
            // Lub-dub, with harmonics over the 55 Hz beat so it is felt on headphones and small speakers too.
            audio.heartbeat = Synth("Heartbeat", 0.5f, (t, i) =>
                (Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 165f * t) * 0.15f)
                * (Mathf.Exp(-t * 30f) + (t > 0.18f ? Mathf.Exp(-(t - 0.18f) * 30f) * 0.7f : 0f)), 0.3f);
            // Freddy: recordings when the project has them (Assets/Audio/Freddy), synthesized
            // stand-ins otherwise (FreddyStepClip, ServoClip, CreakClip).
            audio.freddySteps = Recorded(assets.FreddySteps) ?? new[] { FreddyStepClip(0), FreddyStepClip(1), FreddyStepClip(2), FreddyStepClip(3) };
            audio.freddyServos = Recorded(assets.FreddyServos) ?? new[] { ServoClip() };
            audio.freddyCreaks = Recorded(assets.FreddyCreaks) ?? new[] { CreakClip() };
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

        /// <summary>Keeps <paramref name="source"/> looping when the audio output changes (see <see cref="DeviceChanged"/>).</summary>
        public static void KeepLooping(AudioSource source) => Loops.Add(source);

        private void OnEnable() => AudioSettings.OnAudioConfigurationChanged += DeviceChanged;

        private void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= DeviceChanged;

        /// <summary>
        /// Plugging in headphones (or AirPods connecting) mid-game resets Unity's audio, which stops
        /// every playing source; one-shots are new each time, but loops were started once and stayed
        /// silent: Freddy could no longer be heard moving or ticking. Restart them next frame.
        /// </summary>
        private void DeviceChanged(bool deviceWasChanged)
        {
            if (deviceWasChanged)
            {
                // Unity's own reset can fail on the switch ("failed to initialize it"): open the new
                // device again (this calls back with deviceWasChanged false).
                AudioSettings.Reset(AudioSettings.GetConfiguration());
            }
            restartLoops = true;
        }

        private void Update()
        {
            if (!restartLoops)
            {
                return;
            }
            restartLoops = false;
            if (musicWanted && !music.isPlaying)
            {
                music.Play();
            }
            if (ambientWanted && !ambient.isPlaying)
            {
                ambient.Play();
            }
            Loops.RemoveAll(source => source == null);
            foreach (AudioSource source in Loops)
            {
                if (source.isActiveAndEnabled && !source.isPlaying)
                {
                    source.Play();
                }
            }
        }

        public void PlayMenuMusic(bool on)
        {
            bool play = on && SoundPrefs.MusicOn;
            musicWanted = play;
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
            ambientWanted = play;
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

        /// <summary>A sound at a point in the level (lockers, Freddy, the exit door), heard through <see cref="Earshot"/>.</summary>
        /// <param name="owner">What makes it, if it is solid (its own colliders do not muffle it).</param>
        public void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch = 1f, Transform owner = null)
        {
            if (clip == null || !SoundPrefs.EffectsOn)
            {
                return;
            }
            var go = new GameObject("Sound " + clip.name);
            go.transform.position = position;
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.pitch = pitch;
            Earshot.Attach(source, volume, owner);
            source.Play();
            Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.1f);
        }

        /// <summary>The drone of dread, 0 (he is far: quiet) to 1 (he is right there).</summary>
        public void SetDread(float amount)
        {
            if (!SoundPrefs.EffectsOn || amount <= 0.001f)
            {
                dread.volume = Mathf.MoveTowards(dread.volume, 0f, Time.deltaTime * 0.5f);
                if (dread.volume <= 0f && dread.isPlaying)
                {
                    dread.Stop();
                }
                return;
            }
            if (!dread.isPlaying)
            {
                dread.Play();
            }
            dread.volume = Mathf.MoveTowards(dread.volume, amount * 0.2f, Time.deltaTime * 0.6f);
        }

        public void FreddyStep(Vector3 position, bool running, Transform freddy = null)
        {
            // Never the same recording twice in a row, and each one a little different in pitch and
            // weight, so a night of his steps does not turn into a loop.
            AudioClip step = Pick(freddySteps, ref lastStep);
            // Pitched down: the recordings are a lighter metal foot than a 2 m bear.
            PlayAt(step, position + Vector3.up * 0.3f, Random.Range(0.85f, 1f), running ? Random.Range(0.86f, 0.96f) : Random.Range(0.76f, 0.88f), freddy);
            if (Random.value < (running ? 0.5f : 0.3f))
            {
                // Now and then a joint whirs with the step.
                PlayAt(freddyServos[Random.Range(0, freddyServos.Length)], position + Vector3.up * 1.4f, Random.Range(0.45f, 0.7f), Random.Range(0.9f, 1.12f), freddy);
            }
        }

        /// <summary>His frame creaks as he stands still (called at random moments by his body).</summary>
        public void FreddyCreak(Vector3 position, Transform freddy)
        {
            PlayAt(Pick(freddyCreaks, ref lastCreak), position + Vector3.up * 1.4f, Random.Range(0.6f, 0.9f), Random.Range(0.88f, 1.08f), freddy);
        }

        /// <summary>A clip at random, not <paramref name="last"/> again when there is a choice.</summary>
        private static AudioClip Pick(AudioClip[] clips, ref int last)
        {
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == last)
            {
                index = (index + 1 + Random.Range(0, clips.Length - 1)) % clips.Length;
            }
            last = index;
            return clips[index];
        }

        private static AudioClip[] Recorded(AudioClip[] clips)
        {
            return clips != null && clips.Length > 0 && System.Array.TrueForAll(clips, clip => clip != null) ? clips : null;
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

        private static AudioClip hiss;

        /// <summary>A seal letting go: a short burst of air, bright, falling away.</summary>
        public static AudioClip Hiss()
        {
            if (hiss == null)
            {
                hiss = Synth("Hiss", 0.6f, (t, i) => Noise(i) * Mathf.Exp(-t * 6f) * Mathf.Clamp01(t / 0.01f) * 0.7f, 0.05f);
            }
            return hiss;
        }

        private static AudioClip bell;
        private static AudioClip drag;

        /// <summary>One strike of an old clock's bell: inharmonic partials ringing away.</summary>
        public static AudioClip Bell()
        {
            if (bell == null)
            {
                bell = Synth("Bell", 2.6f, (t, i) =>
                {
                    float w = 2f * Mathf.PI * 330f * t;
                    float ring = Mathf.Sin(w) * 0.5f * Mathf.Exp(-t * 1.4f) + Mathf.Sin(w * 2.76f) * 0.25f * Mathf.Exp(-t * 2.5f)
                                 + Mathf.Sin(w * 5.4f) * 0.12f * Mathf.Exp(-t * 4f) + Mathf.Sin(w * 0.5f) * 0.2f * Mathf.Exp(-t * 0.9f);
                    return ring * Mathf.Clamp01(t / 0.004f) * 0.8f;
                }, 0f);
            }
            return bell;
        }

        /// <summary>A body dragged over a tiled floor: a rough, low scrape in pulls.</summary>
        public static AudioClip Drag()
        {
            if (drag == null)
            {
                drag = Synth("Drag", 1.2f, (t, i) =>
                {
                    float pull = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2.5f)), 2f);
                    return Noise(i) * pull * 0.6f;
                }, 0.7f);
            }
            return drag;
        }

        private static AudioClip crank;

        /// <summary>An old starter motor turning over and not catching: a strained, uneven whirr.</summary>
        public static AudioClip Crank()
        {
            if (crank == null)
            {
                crank = Synth("Crank", 1.3f, (t, i) =>
                {
                    float labor = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * (5.5f - t * 1.5f) * t);
                    float motor = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 95f * t)) * 0.3f + Noise(i) * 0.25f;
                    float envelope = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((1.3f - t) / 0.2f);
                    return motor * labor * envelope;
                }, 0.55f);
            }
            return crank;
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

        // ------------------------------------------------------------------ Freddy's sounds

        /// <summary>
        /// One heavy step: the heel's dull thump (low-passed noise and a little weight under it), the
        /// floor's slap in the mids, and loose metal inside the suit rattling just after, a few small
        /// rings at random (which is also what the ear places left or right). Each seed is a different step.
        /// </summary>
        private static AudioClip FreddyStepClip(int seed)
        {
            var random = new System.Random(seed);
            float Rand(float from, float to) => from + (float)random.NextDouble() * (to - from);
            int n = (int)(0.5f * Rate);
            var data = new float[n];
            var heel = new Biquad(false, 180f, 0.8f);
            var slap = new Biquad(true, 480f, 1f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                data[i] = heel.Run(Rand(-1f, 1f)) * Envelope(t, 22f, 0.002f) * 2.2f
                          + Mathf.Sin(2f * Mathf.PI * 52f * t) * Envelope(t, 18f, 0.004f) * 0.55f
                          + slap.Run(Rand(-1f, 1f)) * Envelope(t, 45f, 0.002f) * 4f;
            }
            int rattles = random.Next(3, 6);
            for (int r = 0; r < rattles; r++)
            {
                int at = (int)(Rand(0.012f, 0.11f) * Rate);
                int length = (int)(0.05f * Rate);
                var ring = new Biquad(true, Rand(2200f, 4800f), 9f);
                float decay = Rand(70f, 120f), gain = Rand(1.2f, 2f);
                for (int i = 0; i < length && at + i < n; i++)
                {
                    data[at + i] += ring.Run(Rand(-1f, 1f)) * Envelope(i / (float)Rate, decay, 0.002f) * gain;
                }
            }
            return Clip("FreddyStep" + seed, data, 2.4f, 0.95f);
        }

        /// <summary>
        /// A joint moving: one burst of a small geared motor sliding up in pitch (a whir with the grit
        /// of its gears), the way a servo works. Played now and then with a step.
        /// </summary>
        private static AudioClip ServoClip()
        {
            var random = new System.Random(11);
            var data = new float[(int)(0.65f * Rate)];
            (float start, float length, float from, float to)[] moves = { (0.05f, 0.55f, 950f, 1250f) };
            foreach (var move in moves)
            {
                int start = (int)(move.start * Rate);
                int length = (int)(move.length * Rate);
                var grit = new Biquad(true, move.from * 1.8f, 4f);
                float phase = 0f;
                for (int i = 0; i < length && start + i < data.Length; i++)
                {
                    float t = i / (float)Rate;
                    phase += 2f * Mathf.PI * Mathf.Lerp(move.from, move.to, t / move.length) / Rate;
                    float gear = Mathf.Sin(phase) * 0.35f + Mathf.Sin(2f * phase) * 0.2f + Mathf.Sin(phase * 0.5f) * 0.15f;
                    float shape = Mathf.Pow(Mathf.Sin(Mathf.PI * t / move.length), 0.6f);
                    data[start + i] += (gear * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 37f * t)) + grit.Run((float)random.NextDouble() * 2f - 1f) * 0.9f) * shape;
                }
            }
            return Clip("Servo", data, 2f, 0.85f);
        }

        /// <summary>
        /// The frame settling as he stands still: a creak (metal sticking and slipping, little pulses
        /// through a resonance) and a small twitch of a servo.
        /// </summary>
        private static AudioClip CreakClip()
        {
            var random = new System.Random(5);
            var data = new float[(int)(1.2f * Rate)];
            (float start, float length, float rate, float frequency)[] creaks = { (0.05f, 0.55f, 38f, 620f) };
            foreach (var creak in creaks)
            {
                int start = (int)(creak.start * Rate);
                int length = (int)(creak.length * Rate);
                var body = new Biquad(true, creak.frequency, 14f);
                var edge = new Biquad(true, creak.frequency * 2.3f, 10f);
                float nextPulse = 0f;
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)Rate;
                    float pulse = 0f;
                    if (t >= nextPulse)
                    {
                        pulse = 0.5f + (float)random.NextDouble() * 0.5f;
                        nextPulse += 1f / (creak.rate * (0.8f + (float)random.NextDouble() * 0.45f));
                    }
                    data[start + i] += (body.Run(pulse) * 28f + edge.Run(pulse) * 12f) * Mathf.Sin(Mathf.PI * t / creak.length);
                }
            }
            int twitch = (int)(0.8f * Rate);
            int twitchLength = (int)(0.18f * Rate);
            var whir = new Biquad(true, 2200f, 4f);
            float twitchPhase = 0f;
            for (int i = 0; i < twitchLength; i++)
            {
                float t = i / (float)Rate;
                twitchPhase += 2f * Mathf.PI * (1300f - t * 900f) / Rate;
                data[twitch + i] += (Mathf.Sin(twitchPhase) * 0.3f + whir.Run((float)random.NextDouble() * 2f - 1f) * 0.6f) * Mathf.Sin(Mathf.PI * t / 0.18f);
            }
            return Clip("Creaks", data, 2f, 0.9f);
        }

        private static float Envelope(float t, float decay, float attack) => Mathf.Min(1f, t / attack) * Mathf.Exp(-t * decay);

        /// <summary>A mono clip from <paramref name="data"/>, driven by <paramref name="drive"/> through soft clipping and scaled to <paramref name="level"/>.</summary>
        private static AudioClip Clip(string name, float[] data, float drive, float level)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Drive(data[i] * drive) * level;
            }
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A two-pole filter (the RBJ cookbook): band-pass with a 0 dB peak, or low-pass.</summary>
        private sealed class Biquad
        {
            private readonly float b0, b1, b2, a1, a2;
            private float x1, x2, y1, y2;

            public Biquad(bool bandPass, float frequency, float q)
            {
                float w = 2f * Mathf.PI * frequency / Rate;
                float alpha = Mathf.Sin(w) / (2f * q);
                float cos = Mathf.Cos(w);
                float a0 = 1f + alpha;
                if (bandPass)
                {
                    b0 = alpha / a0;
                    b1 = 0f;
                    b2 = -alpha / a0;
                }
                else
                {
                    b0 = (1f - cos) / 2f / a0;
                    b1 = (1f - cos) / a0;
                    b2 = b0;
                }
                a1 = -2f * cos / a0;
                a2 = (1f - alpha) / a0;
            }

            public float Run(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x;
                y2 = y1;
                y1 = y;
                return y;
            }
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

        /// <summary>Soft clipping: loud without harsh distortion.</summary>
        private static float Drive(float x) => (float)System.Math.Tanh(x);

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
