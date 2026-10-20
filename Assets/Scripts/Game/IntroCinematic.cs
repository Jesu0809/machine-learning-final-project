using System.Collections;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The story intro, about a minute long and skippable (Enter, Esc, a click, or A on a gamepad):
    /// <list type="number">
    /// <item>Rain over the closed pizzeria and the title cards (1987...).</item>
    /// <item>The guard's car on the road; a voicemail plays (<see cref="StoryData.Intro"/>).</item>
    /// <item>The car turns into the parking lot and stops.</item>
    /// <item>First person: out of the car, a look up at the sign under the lightning, the walk to the door.</item>
    /// <item>The door rolls up, the guard walks in; Freddy on his stage; the title.</item>
    /// </list>
    /// Then night 1 begins.
    /// </summary>
    public class IntroCinematic : MonoBehaviour
    {
        // The car comes from far down the road at a steady speed, turns into the lot and stops.
        private const float CarStartX = -210f;
        private const float CarSpeed = 8f;
        private const float BrakingDistance = 14f;
        private const float TurnRadius = 4.5f;
        private const float TurnStartX = -1.5f;

        private const float VoiceStart = 9.6f;
        private const float WalkSpeed = 1.45f;
        private const float EyeHeight = 1.62f;
        private const float SidewalkHeight = 0.12f;

        private HuntDirector director;
        private Exterior exterior;
        private AudioSource voice;
        private AudioSource effects;
        private AudioSource rain;
        private AudioSource engine;
        private bool skip;
        private Coroutine voicemail;

        private string subtitle = "";
        private float voiceEndsAt = float.MaxValue;

        public bool Playing { get; private set; }

        public static IntroCinematic Create(HuntDirector director, Exterior exterior)
        {
            var intro = new GameObject("IntroCinematic").AddComponent<IntroCinematic>();
            intro.transform.SetParent(director.transform, false);
            intro.director = director;
            intro.exterior = exterior;
            intro.voice = intro.Source(false);
            intro.effects = intro.Source(false);
            intro.rain = intro.Source(true);
            intro.engine = intro.Source(true);
            return intro;
        }

        private AudioSource Source(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        public void Skip() => skip = true;

        public void Play()
        {
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        private void Update()
        {
            if (!Playing)
            {
                return;
            }
            Controls.Poll();
            if (Controls.SkipCinematicPressed)
            {
                skip = true;
            }
        }

        // ------------------------------------------------------------------ the car's drive

        private static float TotalDistance => (TurnStartX - CarStartX) + TurnRadius * Mathf.PI * 0.5f;

        private static float CruiseSeconds => (TotalDistance - BrakingDistance) / CarSpeed;

        /// <summary>Second at which the car stops in its parking spot.</summary>
        private static float ArrivalTime => CruiseSeconds + 2f * BrakingDistance / CarSpeed;

        private static float DistanceAt(float t)
        {
            if (t <= CruiseSeconds)
            {
                return CarSpeed * t;
            }
            float braking = CarSpeed * CarSpeed / (2f * BrakingDistance);
            float tau = Mathf.Min(t - CruiseSeconds, CarSpeed / braking);
            return CarSpeed * CruiseSeconds + CarSpeed * tau - 0.5f * braking * tau * tau;
        }

        private static void CarPose(float t, out Vector3 position, out float yaw)
        {
            float s = DistanceAt(t);
            float straight = TurnStartX - CarStartX;
            if (s <= straight)
            {
                position = new Vector3(CarStartX + s, 0f, Exterior.RoadZ);
                yaw = 90f;
                return;
            }
            // A left turn into the lot: from heading east to heading north.
            float angle = Mathf.Min((s - straight) / TurnRadius, Mathf.PI * 0.5f);
            var center = new Vector3(TurnStartX, 0f, Exterior.RoadZ + TurnRadius);
            position = center + new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * TurnRadius;
            yaw = 90f - angle * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------ the walk to the door

        // Around the front of the car, along the sidewalk, then to the door (two curves).
        private static readonly Vector3 WalkStart = new Vector3(1.3f, 0f, -23.2f);
        private static readonly Vector3 WalkBend1 = new Vector3(1.1f, 0f, -21.4f);
        private static readonly Vector3 WalkMiddle = new Vector3(5f, 0f, -20.9f);
        private static readonly Vector3 WalkBend2 = new Vector3(11.5f, 0f, -20.6f);
        private static readonly Vector3 WalkEnd = new Vector3(PizzeriaLayout.ExitDoor.x, 0f, -18.4f);

        private static Vector3 WalkPoint(float u)
        {
            return u < 0.5f ? Curve(WalkStart, WalkBend1, WalkMiddle, u * 2f) : Curve(WalkMiddle, WalkBend2, WalkEnd, u * 2f - 1f);
        }

        private static Vector3 Curve(Vector3 from, Vector3 control, Vector3 to, float u)
        {
            return from * ((1f - u) * (1f - u)) + control * (2f * u * (1f - u)) + to * (u * u);
        }

        private static float Ground(Vector3 point) => point.z > -21.6f ? SidewalkHeight : 0f;

        // ------------------------------------------------------------------ the film

        private IEnumerator Run()
        {
            Playing = true;
            skip = false;
            PlayerController player = director.Player;
            player.Controllable = false;
            player.CameraLocked = true;
            director.UI.ShowCinematic(true);
            director.Audio.PlayAmbient(false);
            exterior.SetOutdoors(true, player.View);
            exterior.Car.SetHeadlights(true);
            exterior.Car.SetDoor(0f);
            Mute();
            rain.clip = director.Assets.RainLoop;
            rain.volume = 0.55f;
            rain.Play();
            engine.clip = director.Assets.CarEngine;
            engine.volume = 0f;
            engine.Play();
            voicemail = StartCoroutine(Voicemail());

            float t = 0f;
            bool thunder1 = false;
            bool thunder2 = false;
            bool engineOff = false;
            bool lightsOff = false;
            bool slammed = false;
            float stepAt = 0f;
            float walkLength = WalkLength();
            // The guard stays in the car while the message plays, and reaches the door as it ends.
            float walkStartsAt = Mathf.Max(ArrivalTime + 5.2f, VoicemailLength() - walkLength / WalkSpeed + 0.6f);
            float walkEndsAt = walkStartsAt + walkLength / WalkSpeed;
            float getOutAt = walkStartsAt - 4.2f;

            // Shots 1–5: outside, until the guard reaches the door and the message has ended.
            while (!skip && (t < walkEndsAt || Time.time < voiceEndsAt + 0.4f))
            {
                t += Time.deltaTime;
                CarPose(t, out Vector3 carPosition, out float carYaw);
                exterior.Car.Place(carPosition, carYaw);
                Vector3 carForward = Quaternion.Euler(0f, carYaw, 0f) * Vector3.forward;
                Vector3 carRight = Quaternion.Euler(0f, carYaw, 0f) * Vector3.right;
                bool driving = t < ArrivalTime;
                engine.volume = engineOff ? Mathf.MoveTowards(engine.volume, 0f, Time.deltaTime) : (t < 9.5f ? 0.15f : 0.35f);
                engine.pitch = driving ? 1.15f : 0.9f;

                // Lightning over the building in the first shot, and again over the sign.
                if (!thunder1 && t > 4.2f)
                {
                    thunder1 = true;
                    exterior.Lightning();
                    Invoke(nameof(Thunder), 1.1f);
                }
                if (!thunder2 && t > walkStartsAt + 2.4f)
                {
                    thunder2 = true;
                    exterior.Lightning();
                    Invoke(nameof(Thunder), 0.7f);
                }
                if (!engineOff && t > ArrivalTime + 0.6f)
                {
                    engineOff = true;
                }
                if (!lightsOff && t > ArrivalTime + 1.2f)
                {
                    lightsOff = true;
                    exterior.Car.SetHeadlights(false);
                    director.Audio.Click();
                }

                float fade = Mathf.Clamp01(1f - t / 1.8f);
                string card = "";
                float cardAlpha = 0f;
                int cardSize = 64;
                Vector3 eye;
                Vector3 look;
                float fov = 50f;

                if (t < 9.5f)
                {
                    // 1. The closed pizzeria in the rain, seen from above the road.
                    float k = Mathf.SmoothStep(0f, 1f, t / 9.5f);
                    eye = Vector3.Lerp(new Vector3(-36f, 13f, -52f), new Vector3(-22f, 6.5f, -43f), k);
                    look = Vector3.Lerp(new Vector3(-5f, 4.5f, -16f), new Vector3(-3f, 2.5f, -21f), k);
                    Card(t, 0.8f, 3.2f, StoryData.IntroCards[0], 110, ref card, ref cardAlpha, ref cardSize);
                    Card(t, 3.5f, 6.6f, StoryData.IntroCards[1], 44, ref card, ref cardAlpha, ref cardSize);
                    Card(t, 6.9f, 9.3f, StoryData.IntroCards[2], 52, ref card, ref cardAlpha, ref cardSize);
                }
                else if (t < 19f)
                {
                    // 2. Behind the car on the road.
                    eye = carPosition - carForward * 7.5f + Vector3.up * 2.3f + carRight * 1.3f;
                    look = carPosition + carForward * 6f + Vector3.up * 0.8f;
                    fade = Mathf.Clamp01(1f - (t - 9.5f) / 0.5f);
                }
                else if (t < 26.5f)
                {
                    // 3. From the roadside by the lot: the car comes and turns in.
                    eye = new Vector3(14f, 1.2f, -34.5f);
                    look = carPosition + Vector3.up * 0.8f;
                    fov = 40f;
                }
                else if (t < Mathf.Min(ArrivalTime + 1.4f, getOutAt))
                {
                    // 4. Low, from the sidewalk: the car pulls up into the headlights' glare and stops.
                    eye = new Vector3(6.6f, 0.75f, -20.9f);
                    look = carPosition + Vector3.up * 0.7f;
                }
                else if (t < getOutAt)
                {
                    // 4b. In the driver's seat, rain on the glass, looking up at the sign while the message ends.
                    fov = 60f;
                    float k = Mathf.SmoothStep(0f, 1f, (t - ArrivalTime - 1.4f) / Mathf.Max(1f, getOutAt - ArrivalTime - 1.4f));
                    eye = carPosition + Quaternion.Euler(0f, carYaw, 0f) * Car.DriverEyes + Vector3.up * Mathf.Sin(t * 1.1f) * 0.006f;
                    look = Vector3.Lerp(new Vector3(-2f, 2.2f, -16.5f), new Vector3(-6.5f, 4.6f, -16.5f), k);
                    fade = Mathf.Clamp01(1f - (t - ArrivalTime - 1.4f) / 0.4f);
                }
                else
                {
                    // 5. First person: open the car door, step out, shut it behind you, turn to the
                    //    pizzeria and walk to its door (a glance up at the sign when the lightning hits).
                    fov = 68f;
                    float g = t - getOutAt;
                    Quaternion carTurn = Quaternion.Euler(0f, carYaw, 0f);
                    float doorOpen = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(g / 0.8f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - 2.15f) / 0.3f)));
                    exterior.Car.SetDoor(doorOpen);
                    if (!slammed && g > 2.4f)
                    {
                        slammed = true;
                        effects.PlayOneShot(director.Assets.LockerDoor, 0.8f);
                    }
                    Vector3 seat = carPosition + carTurn * Car.DriverEyes;
                    Vector3 standing = WalkStart + Vector3.up * EyeHeight;
                    Vector3 signTarget = new Vector3(-6.5f, 5f, -16.5f);
                    Vector3 doorTarget = new Vector3(PizzeriaLayout.ExitDoor.x, 1.5f, -17.3f);
                    Vector3 carDoor = carPosition + carTurn * new Vector3(-0.95f, 0.8f, -0.1f);
                    Vector3 ground = carPosition + carTurn * new Vector3(-2.2f, 0f, 1.6f);
                    if (t < walkStartsAt)
                    {
                        eye = Vector3.Lerp(seat, standing, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - 0.3f) / 1.3f)));
                        // Out, looking down at the wet ground; back at the car door as it slams; then at the pizzeria.
                        look = Vector3.Lerp(new Vector3(-6.5f, 4.6f, -16.5f), ground, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(g / 1.2f)));
                        look = Vector3.Lerp(look, carDoor, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - 1.3f) / 0.8f)));
                        look = Vector3.Lerp(look, doorTarget, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - 2.8f) / 1.3f)));
                    }
                    else
                    {
                        float walked = Mathf.Min((t - walkStartsAt) * WalkSpeed, walkLength);
                        Vector3 feet = WalkPoint(UAtLength(walked));
                        bool walking = walked < walkLength;
                        float bob = walking ? Mathf.Sin(walked * 4.2f) * 0.03f : Mathf.Sin(t * 1.3f) * 0.008f;
                        eye = feet + Vector3.up * (EyeHeight + Ground(feet) + bob);
                        float glance = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - walkStartsAt - 1.6f) / 1f))
                                     * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - walkStartsAt - 4f) / 1.2f)));
                        look = Vector3.Lerp(doorTarget, signTarget, glance);
                        if (walking && t >= stepAt)
                        {
                            stepAt = t + 0.55f;
                            director.Audio.Footstep(0.5f);
                        }
                    }
                }

                Aim(player, eye, look, fov);
                director.UI.SetCinematic(subtitle, Mathf.Max(fade, 0f));
                director.UI.SetCinematicCard(card, cardAlpha, cardSize);
                yield return null;
            }

            // 6. The guard pushes the glass doors open and walks in.
            if (!skip)
            {
                effects.PlayOneShot(director.Assets.LockerDoor, 0.5f);
                float d = 0f;
                Vector3 outside = WalkEnd + Vector3.up * (EyeHeight + SidewalkHeight);
                Vector3 inside = new Vector3(PizzeriaLayout.ExitDoor.x, EyeHeight, -14.6f);
                while (d < 5f && !skip)
                {
                    d += Time.deltaTime;
                    director.Level.Exit.SetLift(Mathf.SmoothStep(0f, 1f, d / 2f));
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 1.8f) / 3f));
                    Vector3 eye = Vector3.Lerp(outside, inside, k) + Vector3.up * Mathf.Sin(k * 18f) * 0.025f;
                    if (eye.z > -17.2f)
                    {
                        exterior.SetOutdoors(false, player.View);
                        director.Audio.PlayAmbient(true);
                    }
                    rain.volume = Mathf.Lerp(0.55f, 0.12f, k);
                    Aim(player, eye, eye + new Vector3(-0.3f, -0.05f, 1f), 68f);
                    director.UI.SetCinematic("", Mathf.Clamp01((d - 4f) / 1f));
                    director.UI.SetCinematicCard("", 0f);
                    if (d > 2.2f && d < 4.6f && Time.frameCount % 34 == 0)
                    {
                        director.Audio.Footstep(0.45f);
                    }
                    yield return null;
                }
                director.Level.Exit.SetLift(0f);
            }

            // 7–8. Inside: Freddy on his stage, then his face and the title.
            yield return Interior(player,
                                  new Vector3(9.0f, 1.5f, 2.3f), new Vector3(7.9f, 1.75f, 6.6f), new Vector3(7.5f, 1.9f, 10.8f), 5.5f, "", 0);
            yield return Interior(player,
                                  new Vector3(8.3f, 2.25f, 8.1f), new Vector3(7.9f, 2.15f, 9.2f), new Vector3(7.5f, 2.0f, 10.8f), 4.5f, "NIGHT SHIFT", 130);

            Finish(player);
            director.StartNight(1);
        }

        private IEnumerator Interior(PlayerController player, Vector3 from, Vector3 to, Vector3 target, float seconds, string title, int size)
        {
            float t = 0f;
            while (t < seconds && !skip)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                Vector3 eye = Vector3.Lerp(from, to, k);
                Aim(player, eye, target, 55f);
                float fade = Mathf.Max(1f - t / 0.8f, (t - (seconds - 0.7f)) / 0.7f);
                director.UI.SetCinematic("", Mathf.Clamp01(fade));
                float alpha = title.Length > 0 ? Mathf.Clamp01((t - 1.2f) / 0.8f) * Mathf.Clamp01((seconds - 0.4f - t) / 0.6f) : 0f;
                director.UI.SetCinematicCard(title, alpha, size > 0 ? size : 64);
                yield return null;
            }
        }

        /// <summary>Plays the answering machine and the message, one line after another, with subtitles.</summary>
        private IEnumerator Voicemail()
        {
            yield return new WaitForSeconds(VoiceStart);
            AudioClip[] clips = director.Assets.IntroLines;
            if (director.Assets.VoicemailBeep != null)
            {
                effects.PlayOneShot(director.Assets.VoicemailBeep, 0.6f);
                yield return new WaitForSeconds(0.6f);
            }
            for (int i = 0; i < StoryData.Intro.Length && !skip; i++)
            {
                AudioClip clip = clips != null && i < clips.Length ? clips[i] : null;
                subtitle = (i == 0 ? "<color=#8a9a8a>" : "") + StoryData.Intro[i] + (i == 0 ? "</color>" : "");
                if (clip != null)
                {
                    voice.clip = clip;
                    voice.volume = 1f;
                    voice.Play();
                    yield return new WaitForSeconds(clip.length + 0.3f);
                }
                else
                {
                    yield return new WaitForSeconds(4f);
                }
                if (i == 0 && director.Assets.VoicemailBeep != null)
                {
                    effects.PlayOneShot(director.Assets.VoicemailBeep, 0.6f);
                    yield return new WaitForSeconds(0.5f);
                }
            }
            subtitle = "";
            voiceEndsAt = Time.time;
        }

        private void Thunder()
        {
            if (Playing && director.Assets.Thunder != null)
            {
                effects.PlayOneShot(director.Assets.Thunder, 0.9f);
            }
        }

        private static void Card(float t, float from, float to, string text, int size, ref string card, ref float alpha, ref int cardSize)
        {
            if (t < from || t > to)
            {
                return;
            }
            card = text;
            cardSize = size;
            alpha = Mathf.Clamp01((t - from) / 0.6f) * Mathf.Clamp01((to - t) / 0.6f);
        }

        private static void Aim(PlayerController player, Vector3 eye, Vector3 target, float fov)
        {
            player.Head.position = eye;
            player.Head.rotation = Quaternion.LookRotation(target - eye);
            player.View.fieldOfView = fov;
        }

        /// <summary>When the voicemail will end, in seconds from the start of the intro.</summary>
        private float VoicemailLength()
        {
            float length = VoiceStart + (director.Assets.VoicemailBeep != null ? 1.1f : 0f);
            AudioClip[] clips = director.Assets.IntroLines;
            for (int i = 0; i < StoryData.Intro.Length; i++)
            {
                length += (clips != null && i < clips.Length && clips[i] != null ? clips[i].length : 4f) + 0.3f;
            }
            return length;
        }

        private float WalkLength()
        {
            float length = 0f;
            Vector3 previous = WalkPoint(0f);
            for (int i = 1; i <= 64; i++)
            {
                Vector3 point = WalkPoint(i / 64f);
                length += Vector3.Distance(previous, point);
                previous = point;
            }
            return length;
        }

        /// <summary>Curve parameter after walking <paramref name="distance"/> meters along it.</summary>
        private static float UAtLength(float distance)
        {
            float walked = 0f;
            Vector3 previous = WalkPoint(0f);
            for (int i = 1; i <= 64; i++)
            {
                Vector3 point = WalkPoint(i / 64f);
                float step = Vector3.Distance(previous, point);
                if (walked + step >= distance)
                {
                    return (i - 1 + (distance - walked) / step) / 64f;
                }
                walked += step;
                previous = point;
            }
            return 1f;
        }

        private void Mute()
        {
            bool on = SoundPrefs.EffectsOn;
            voice.mute = effects.mute = rain.mute = engine.mute = !on;
        }

        private void Finish(PlayerController player)
        {
            CancelInvoke();
            if (voicemail != null)
            {
                StopCoroutine(voicemail);
                voicemail = null;
            }
            voice.Stop();
            rain.Stop();
            engine.Stop();
            exterior.SetOutdoors(false, player.View);
            exterior.Car.Place(Exterior.ParkingSpot, 0f);
            exterior.Car.SetHeadlights(false);
            exterior.Car.SetDoor(0f);
            director.Level.Exit.SetLift(0f);
            director.Audio.PlayAmbient(true);
            director.UI.SetCinematicCard("", 0f);
            director.UI.ShowCinematic(false);
            player.View.fieldOfView = 70f;
            subtitle = "";
            voiceEndsAt = float.MaxValue;
            Playing = false;
        }
    }
}
