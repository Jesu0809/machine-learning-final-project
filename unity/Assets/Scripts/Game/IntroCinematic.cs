using System.Collections;
using System.Collections.Generic;
using NightShift.Hunt;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NightShift.Game
{
    /// <summary>
    /// The story intro, about a minute and a half and skippable (Enter, Esc, a click, or A on a gamepad):
    /// <list type="number">
    /// <item>A home video, August 1987: a birthday at Freddy's, the bear on his stage under the
    /// lights. He turns his head to the camera, the music drags, the tape tears into snow.</item>
    /// <item>Black, two cards: the pizzeria closed after the incidents; thirty-eight years later.</item>
    /// <item>The road at night in the rain: the guard's station wagon throws spray as it passes, the
    /// wipers beat, the street lamps sweep through the car, and the phone on the dashboard plays a
    /// voicemail from the agency (<see cref="StoryData.Intro"/>).</item>
    /// <item>The car turns into the lot; its headlights sweep the boarded front and stop on the doors.
    /// The engine, then the lights, go off.</item>
    /// <item>On foot with the flashlight. Lightning: someone stands under the dead street lamp. The
    /// next flash, nobody.</item>
    /// <item>The front doors are chained: the key, the padlock, the chain drops. Inside, far away, the
    /// office phone starts ringing. Freddy on his stage; his eyes; the title.</item>
    /// </list>
    /// Then night 1 begins.
    /// </summary>
    public class IntroCinematic : MonoBehaviour
    {
        // The car's route: east along the road's south lane, then a left turn into the lot that ends
        // in the parking spot. Between the drive's shots the car jumps along it (cuts).
        private const float RouteStartX = -232f;
        private const float LaneZ = Exterior.RoadZ - 2f;
        private const float CruiseSpeed = 11f;
        private const float BrakingDistance = 20f;

        private const float TapeLength = 11f;
        private const float CardsLength = 6f;
        private const float VoiceDelay = 2f;      // into the drive
        private const float Arrival = 38.5f;      // into the drive: parked
        private const float DriveLength = 43.5f;

        private const float EyeHeight = 1.62f;
        private const float SidewalkHeight = 0.12f;
        private const float SidewalkZ = -19.6f;
        private const int TapeWidth = 320;
        private const int TapeHeight = 240;

        /// <summary>Under the dead street lamp, at the corner of the lot: where the stranger stands.</summary>
        private static readonly Vector3 StrangerSpot = new Vector3(-12.6f, 0f, -22.3f);

        private HuntDirector director;
        private Exterior exterior;
        private AudioSource voice;
        private AudioSource effects;
        private AudioSource rain;
        private AudioLowPassFilter rainMuffle;
        private AudioSource engine;
        private AudioSource music;
        private bool skip;
        private Coroutine voicemail;
        private string subtitle = "";

        // The home video: its camera, picture and the party's lights.
        private Camera tapeCamera;
        private RenderTexture tapeTexture;
        private readonly List<Light> partyLights = new List<Light>();
        private Quaternion headRest;
        private float headTurn;

        // The bear's show on the tape: his bones (humanoid), their rest pose, and how much he performs (0–1).
        private Transform[] showBones;
        private Quaternion[] showRest;
        private float perform;
        private float lean;
        private float showClock;
        private readonly List<Transform> kids = new List<Transform>();
        private Light[] sweepers;
        private readonly List<ParticleSystem> confetti = new List<ParticleSystem>();
        private int savedMask;

        // Props of the night: the phone on the dashboard, the stranger, the chain on the doors.
        private readonly List<GameObject> props = new List<GameObject>();
        private Transform phone;
        private Renderer[] strangerRenderers;
        private Light strangerRim;
        private Light strangerFill;
        private Transform padlock;
        private Transform shackle;
        private Transform key;
        private Transform keyRing;
        private ChainRope chain;

        public bool Playing { get; private set; }

        /// <summary>Which part is playing and how far into it (the capture tour photographs along it).</summary>
        public string Part { get; private set; } = "";
        public float PartTime { get; private set; }

        public static IntroCinematic Create(HuntDirector director, Exterior exterior)
        {
            var intro = new GameObject("IntroCinematic").AddComponent<IntroCinematic>();
            intro.transform.SetParent(director.transform, false);
            intro.director = director;
            intro.exterior = exterior;
            intro.voice = intro.Source(false);
            intro.effects = intro.Source(false);
            intro.rain = intro.Source(true);
            intro.rainMuffle = intro.rain.gameObject.AddComponent<AudioLowPassFilter>();
            intro.engine = intro.Source(true);
            intro.music = intro.Source(true);
            return intro;
        }

        private AudioSource Source(bool loop)
        {
            // Each on its own object, so a filter on one (the rain heard from inside the car) leaves the others clear.
            var source = new GameObject("IntroSound").AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
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

        private void LateUpdate()
        {
            // On the tape the bear performs, then stops and turns to the camera (posed after his animation).
            Transform head = director != null && director.Freddy != null ? director.Freddy.Head : null;
            if (!Playing || head == null || head == director.Freddy.transform || tapeCamera == null || !tapeCamera.enabled)
            {
                return;
            }
            head.localRotation = headRest;
            Transform body = director.Freddy.transform;
            Vector3 forward = body.forward, right = body.right;
            float beat = showClock * Mathf.PI * 2f;     // two beats a second
            if (showBones != null)
            {
                for (int i = 0; i < showBones.Length; i++)
                {
                    showBones[i].localRotation = showRest[i];
                }
                Transform spine = showBones[0], rightUpper = showBones[1], rightLower = showBones[2], leftUpper = showBones[3], leftLower = showBones[4];
                // Swaying to the song, the right paw up and waving, the left holding the microphone.
                spine.rotation = Quaternion.AngleAxis(Mathf.Sin(beat * 0.5f) * 7f * perform, forward) * Quaternion.AngleAxis(lean * 14f, right) * spine.rotation;
                rightUpper.rotation = Quaternion.AngleAxis(125f * perform, forward) * rightUpper.rotation;
                rightLower.rotation = Quaternion.AngleAxis((Mathf.Sin(beat) * 28f + 20f) * perform, forward) * rightLower.rotation;
                leftUpper.rotation = Quaternion.AngleAxis(-25f * perform, forward) * Quaternion.AngleAxis(-35f * perform, right) * leftUpper.rotation;
                leftLower.rotation = Quaternion.AngleAxis(-95f * perform, right) * leftLower.rotation;
            }
            head.rotation = Quaternion.AngleAxis(Mathf.Sin(beat) * 9f * perform, right) * head.rotation;
            if (headTurn > 0f)
            {
                // Then the head turns to the lens, tilted, like something curious.
                Vector3 toCamera = (tapeCamera.transform.position - head.position).normalized;
                Quaternion turn = Quaternion.FromToRotation(forward, toCamera) * Quaternion.AngleAxis(-20f, forward);
                head.rotation = Quaternion.Slerp(Quaternion.identity, turn, headTurn) * head.rotation;
            }
        }

        // ------------------------------------------------------------------ the film

        private IEnumerator Run()
        {
            Playing = true;
            skip = false;
            PlayerController player = director.Player;
            player.Controllable = false;
            player.CameraLocked = true;
            player.Flashlight.enabled = false;
            director.UI.ShowCinematic(true);
            director.Audio.PlayAmbient(false);
            Mute();
            Prepare();

            yield return Tape(player);
            if (!skip)
            {
                yield return Cards();
            }
            if (!skip)
            {
                yield return Drive(player);
            }
            if (!skip)
            {
                yield return OnFoot(player);
            }
            if (!skip)
            {
                yield return Door(player);
            }
            if (!skip)
            {
                // Freddy on his stage, then his face and the title, with the phone ringing far away.
                yield return Interior(player, new Vector3(9.0f, 1.5f, 2.3f), new Vector3(7.9f, 1.75f, 6.6f), new Vector3(7.5f, 1.9f, 10.8f), 5f, "", 0, false);
                yield return Interior(player, new Vector3(8.3f, 2.25f, 8.1f), new Vector3(7.9f, 2.15f, 9.2f), new Vector3(7.5f, 2.0f, 10.8f), 4.5f, "NIGHT SHIFT", 130, true);
            }

            Finish(player);
            director.StartNight(1);
        }

        // ------------------------------------------------------------------ 1. the home video

        private IEnumerator Tape(PlayerController player)
        {
            savedMask = player.View.cullingMask;
            player.View.cullingMask = 0;
            tapeCamera.enabled = true;
            foreach (Light light in partyLights)
            {
                light.enabled = true;
            }
            foreach (Light light in sweepers)
            {
                light.enabled = true;
            }
            Transform head = director.Freddy.Head;
            headRest = head.localRotation;
            headTurn = 0f;
            Animator animator = director.Freddy.GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                showBones = new[]
                {
                    animator.GetBoneTransform(HumanBodyBones.Spine), animator.GetBoneTransform(HumanBodyBones.RightUpperArm),
                    animator.GetBoneTransform(HumanBodyBones.RightLowerArm), animator.GetBoneTransform(HumanBodyBones.LeftUpperArm),
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerArm)
                };
                if (System.Array.IndexOf(showBones, null) >= 0)
                {
                    showBones = null;
                }
                else
                {
                    showRest = System.Array.ConvertAll(showBones, b => b.localRotation);
                }
            }
            music.clip = director.Assets.MenuMusic;
            music.volume = 0.32f;
            music.pitch = 1f;
            var muffle = music.gameObject.AddComponent<AudioLowPassFilter>();
            muffle.cutoffFrequency = 3600f;
            music.Play();
            showClock = 0f;
            foreach (ParticleSystem system in confetti)
            {
                system.Play();
            }
            director.Audio.StaticBurst();
            director.UI.SetCinematic("", 0f);
            director.UI.SetCinematicCard("", 0f);

            const float freezeAt = 6.2f;
            bool eyes = false, tear = false;
            for (float t = 0f; t < TapeLength && !skip; t += Time.deltaTime)
            {
                Mark("tape", t);
                bool show = t < freezeAt;
                if (show)
                {
                    showClock += Time.deltaTime;
                }
                // The show: the bear sings, the kids in front jump and sway, the colored spots sweep.
                perform = Mathf.Clamp01((t - 0.4f) / 0.4f) * (1f - Mathf.Clamp01((t - freezeAt) / 0.25f));
                for (int i = 0; i < kids.Count; i++)
                {
                    float hop = Mathf.Max(0f, Mathf.Sin(showClock * Mathf.PI * 2f + i * 1.7f)) * 0.07f * perform;
                    kids[i].localPosition = new Vector3(kids[i].localPosition.x, hop, kids[i].localPosition.z);
                    kids[i].localRotation = Quaternion.Euler(0f, Mathf.Sin(showClock * 2.4f + i) * 12f * perform, Mathf.Sin(showClock * 3f + i * 2f) * 6f * perform);
                }
                for (int i = 0; i < sweepers.Length; i++)
                {
                    float swing = Mathf.Sin(showClock * 1.3f + i * 2.1f) * 18f;
                    sweepers[i].transform.rotation = Quaternion.LookRotation(PizzeriaLayout.FreddyStart + Vector3.up * 1.2f - sweepers[i].transform.position) * Quaternion.Euler(0f, swing, 0f);
                    sweepers[i].intensity = show ? 12f : 0f;
                }
                partyLights[0].intensity = show ? 18f : 15f;
                foreach (ParticleSystem system in confetti)
                {
                    // When he stops, so does the room: the confetti hangs in the air.
                    ParticleSystem.MainModule fall = system.main;
                    fall.simulationSpeed = show ? 1f : 0.03f;
                }

                // He stops dead mid-song; the music dies. He leans and turns his head to the lens,
                // his eyes go red, the camera zooms in on him, and the tape tears.
                lean = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 6.9f) / 1.4f));
                headTurn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 6.9f) / 1.5f));
                music.pitch = show ? 1f : Mathf.Lerp(1f, 0.35f, Mathf.Clamp01((t - freezeAt) / 0.6f));
                music.volume = show ? 0.32f : Mathf.Lerp(0.32f, 0f, Mathf.Clamp01((t - freezeAt) / 0.8f));
                if (!eyes && t > 8f)
                {
                    eyes = true;
                    director.Freddy.FlashEyes(1.8f);
                    director.Audio.Sting();
                }
                if (!tear && t > 8.8f)
                {
                    tear = true;
                    director.Audio.StaticBurst();
                }

                // The camcorder: a parent behind the kids, filming the stage, walking up between them.
                float walk = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.7f) / 5.3f));
                Vector3 eye = Vector3.Lerp(new Vector3(6.2f, 1.42f, 5.4f), new Vector3(6.8f, 1.45f, 7.3f), walk) + Handheld(t, 0.03f);
                Vector3 look = Vector3.Lerp(PizzeriaLayout.FreddyStart + Vector3.up * 1.1f, head.position, Mathf.Clamp01((t - 3f) / 3f));
                tapeCamera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye));
                float zoom = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 8.2f) / 0.5f));
                tapeCamera.fieldOfView = Mathf.Lerp(Mathf.Lerp(48f, 34f, walk), 13f, zoom);

                float trouble = Mathf.Clamp01((t - freezeAt) / 2.6f);
                float jump = Random.value < 0.03f + trouble * 0.3f ? Random.Range(-0.02f, 0.02f) * (1f + trouble * 4f) : 0f;
                float snow = t < 0.7f ? 1f : t < 8.8f ? 0.02f + trouble * 0.06f : Mathf.Lerp(0.5f, 1f, (t - 8.8f) / 1f);
                Color tint = Color.Lerp(new Color(1.15f, 1.0f, 0.85f), new Color(1.3f, 0.75f, 0.75f), Mathf.Clamp01((t - 8.2f) / 1f));
                if (t < 10.6f)
                {
                    Texture picture = t < 0.7f || t > 9.8f ? (Texture)Texture2D.blackTexture : tapeTexture;
                    director.UI.SetTape(picture, tint, t > 0.7f && t < 9.8f ? BuildKit.SignTexture("vhs_osd_party") : null, snow, jump);
                }
                else
                {
                    director.UI.HideTape();
                    director.UI.SetCinematic("", 1f);
                }
                yield return null;
            }

            foreach (ParticleSystem system in confetti)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            perform = 0f;
            lean = 0f;
            if (showBones != null)
            {
                for (int i = 0; i < showBones.Length; i++)
                {
                    showBones[i].localRotation = showRest[i];
                }
            }
            foreach (Transform kid in kids)
            {
                kid.gameObject.SetActive(false);
            }
            music.Stop();
            Destroy(muffle);
            director.UI.HideTape();
            headTurn = 0f;
            head.localRotation = headRest;
            tapeCamera.enabled = false;
            foreach (Light light in partyLights)
            {
                light.enabled = false;
            }
            foreach (Light light in sweepers)
            {
                light.enabled = false;
            }
            player.View.cullingMask = savedMask;
        }

        // ------------------------------------------------------------------ 2. the cards

        private IEnumerator Cards()
        {
            rain.clip = director.Assets.RainLoop;
            rain.volume = 0f;
            rainMuffle.cutoffFrequency = 22000f;
            rain.Play();
            for (float t = 0f; t < CardsLength && !skip; t += Time.deltaTime)
            {
                Mark("cards", t);
                string card = "";
                float alpha = 0f;
                int size = 64;
                Card(t, 0.3f, 3.3f, StoryData.IntroCards[1], 44, ref card, ref alpha, ref size);
                Card(t, 3.5f, 5.8f, StoryData.IntroCards[2], 52, ref card, ref alpha, ref size);
                rain.volume = Mathf.Lerp(0f, 0.5f, Mathf.Clamp01((t - 3.5f) / 2.5f));
                director.UI.SetCinematic("", 1f);
                director.UI.SetCinematicCard(card, alpha, size);
                yield return null;
            }
            director.UI.SetCinematicCard("", 0f);
        }

        // ------------------------------------------------------------------ 3–4. the drive

        private static float TurnRadius => Exterior.ParkingSpot.z - LaneZ;
        private static float TurnStartX => Exterior.ParkingSpot.x - TurnRadius;
        private static float StraightLength => TurnStartX - RouteStartX;
        private static float RouteLength => StraightLength + TurnRadius * Mathf.PI * 0.5f;

        /// <summary>Where the car is <paramref name="s"/> meters along its route, its heading and the steering wheel's turn.</summary>
        private static void CarPose(float s, out Vector3 position, out float yaw, out float steer)
        {
            if (s <= StraightLength)
            {
                position = new Vector3(RouteStartX + s, 0f, LaneZ);
                yaw = 90f;
                steer = 0f;
                return;
            }
            float angle = Mathf.Min((s - StraightLength) / TurnRadius, Mathf.PI * 0.5f);
            var center = new Vector3(TurnStartX, 0f, LaneZ + TurnRadius);
            position = center + new Vector3(Mathf.Sin(angle), 0f, -Mathf.Cos(angle)) * TurnRadius;
            yaw = 90f - angle * Mathf.Rad2Deg;
            steer = -200f * Mathf.Sin(angle * 2f);
        }

        /// <summary>Meters along the route at <paramref name="u"/> seconds into the drive (each shot is a cut).</summary>
        private static float RouteAt(float u)
        {
            float At(float x, float from) => x - RouteStartX + CruiseSpeed * (u - from);
            if (u < 6f)
            {
                return At(-185f, 0f);
            }
            if (u < 17f)
            {
                return At(-205f, 6f);
            }
            if (u < 22.5f)
            {
                return At(-200f, 17f);
            }
            if (u < 30.5f)
            {
                return At(-150f, 22.5f);
            }
            // The last stretch: cruising, then braking through the turn to stop in the spot at Arrival.
            float brakingTime = 2f * BrakingDistance / CruiseSpeed;
            float brakesAt = Arrival - brakingTime;
            if (u < brakesAt)
            {
                return RouteLength - BrakingDistance - CruiseSpeed * (brakesAt - u);
            }
            float tau = Mathf.Min(u - brakesAt, brakingTime);
            return RouteLength - BrakingDistance + CruiseSpeed * tau - 0.5f * (CruiseSpeed / brakingTime) * tau * tau;
        }

        private IEnumerator Drive(PlayerController player)
        {
            Car car = exterior.Car;
            exterior.SetOutdoors(true, player.View);
            car.SetHeadlights(true);
            car.Wipers = true;
            engine.clip = director.Assets.CarEngine;
            engine.volume = 0f;
            engine.Play();
            phone.gameObject.SetActive(true);
            voicemail = StartCoroutine(Voicemail(VoiceDelay));

            bool thunder = false, engineOff = false, lightsOff = false;
            float nextThump = 0f;
            for (float u = 0f; u < DriveLength && !skip; u += Time.deltaTime)
            {
                Mark("drive", u);
                CarPose(RouteAt(u), out Vector3 carPosition, out float carYaw, out float steer);
                car.Place(carPosition, carYaw);
                car.Steer(steer + Mathf.Sin(u * 0.7f) * 3f);
                Transform body = car.transform;
                bool inside = u >= 6f && u < 17f || u >= 22.5f && u < Arrival + 0.4f;
                rainMuffle.cutoffFrequency = inside ? 1300f : 22000f;
                rain.volume = inside ? 0.7f : 0.5f;
                if (!engineOff && u > Arrival + 1.4f)
                {
                    engineOff = true;
                    car.Wipers = false;
                    effects.PlayOneShot(GameAudio.Clunk(), 0.25f);
                }
                if (!lightsOff && u > Arrival + 2.8f)
                {
                    lightsOff = true;
                    car.SetHeadlights(false);
                    director.Audio.Click();
                }
                if (!thunder && u > 12.5f)
                {
                    thunder = true;
                    exterior.Lightning();
                    Invoke(nameof(Thunder), 1.4f);
                }
                if (inside && car.Wipers && u >= nextThump)
                {
                    // The wipers' soft knock at the end of each stroke.
                    nextThump = u + 1.3f;
                    effects.PlayOneShot(GameAudio.Clunk(), 0.05f);
                }

                Vector3 eye, look;
                float fov = 55f, fade = 0f;
                engine.pitch = 1.05f;
                Vector3 driver = body.TransformPoint(car.DriverEyes);
                if (u < 6f)
                {
                    // A. Low at the roadside: the headlights come out of the rain and the car rushes past.
                    eye = new Vector3(-118f, 0.32f, Exterior.RoadZ - 4.6f);
                    float pass = 1f - Mathf.Clamp01(Mathf.Abs(carPosition.x - eye.x) / 6f);
                    eye += Handheld(u, 0.01f + pass * 0.03f);
                    look = carPosition + Vector3.up * 0.7f;
                    fov = 38f;
                    fade = Mathf.Clamp01(1f - u / 1.2f);
                    engine.volume = Mathf.Clamp01(1.6f / Mathf.Max(1f, Vector3.Distance(eye, carPosition))) * 0.9f;
                    engine.pitch = carPosition.x < eye.x ? 1.2f : 0.92f;   // the pitch drops as it passes
                }
                else if (u < 17f)
                {
                    // B. At the wheel: the road ahead, the wipers, the lamps passing over; a glance at the phone.
                    float glance = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 9.5f) / 0.7f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 11.6f) / 0.7f)));
                    eye = driver + Handheld(u, 0.006f) + Vector3.up * Mathf.Sin(u * 9f) * 0.002f;
                    look = Vector3.Lerp(body.TransformPoint(car.DriverEyes + new Vector3(0.1f, -0.1f, 6f)), phone.position, glance * 0.85f);
                    fov = 62f;
                    engine.volume = 0.3f;
                }
                else if (u < 22.5f)
                {
                    // C. Alongside, on the passenger's side: rain streaking past, the wheels throwing spray.
                    eye = carPosition + body.rotation * new Vector3(3.6f, 1.05f, 2.4f) + Handheld(u, 0.05f);
                    look = carPosition + body.rotation * new Vector3(0f, 0.75f, 0.2f);
                    fov = 44f;
                    engine.volume = 0.55f;
                }
                else if (u < 30.5f)
                {
                    // D. The phone on the dashboard, close, then up through the glass at the road.
                    float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((u - 26.5f) / 1.5f));
                    eye = body.TransformPoint(car.DriverEyes + new Vector3(0.12f, -0.08f, 0.1f)) + Handheld(u, 0.004f);
                    look = Vector3.Lerp(phone.position, body.TransformPoint(car.DriverEyes + new Vector3(0f, -0.05f, 6f)), up);
                    fov = Mathf.Lerp(32f, 60f, up);
                    engine.volume = 0.3f;
                }
                else if (u < Arrival + 0.4f)
                {
                    // E. At the wheel again: the pizzeria's sign in the rain, the turn into the lot; the
                    //    headlights sweep the boarded front and stop on the doors.
                    float turning = Mathf.Abs(steer) / 200f;
                    eye = driver + Handheld(u, 0.006f);
                    look = body.TransformPoint(car.DriverEyes + Quaternion.Euler(0f, -22f * turning, 0f) * new Vector3(0f, -0.08f, 6f));
                    fov = 62f;
                    engine.volume = Mathf.Lerp(0.3f, 0.18f, Mathf.Clamp01((u - 33f) / 4f));
                }
                else
                {
                    // F. From the sidewalk: the car stopped in the glare of its own lights, then dark.
                    eye = new Vector3(5.2f, 1.0f, -19.9f) + Handheld(u, 0.012f);
                    look = carPosition + new Vector3(0f, 0.8f, 0.6f);
                    fov = 42f;
                    engine.pitch = 0.8f;
                    engine.volume = engineOff ? Mathf.MoveTowards(engine.volume, 0f, Time.deltaTime * 0.8f) : 0.22f;
                    fade = Mathf.Clamp01((u - (DriveLength - 0.8f)) / 0.8f);
                }

                Aim(player, eye, look, fov);
                director.UI.SetCinematic(subtitle, fade);
                yield return null;
            }
            engine.Stop();
            car.Wipers = false;
            car.Steer(0f);
        }

        // ------------------------------------------------------------------ 5. on foot

        // From the driver's door, around the front of the car, along the sidewalk to the doors.
        private static readonly Vector3 WalkStart = new Vector3(1.55f, 0f, -24.3f);
        private static readonly Vector3 WalkBend1 = new Vector3(1.4f, 0f, -21.0f);
        private static readonly Vector3 WalkMiddle = new Vector3(6.5f, 0f, -20.3f);
        private static readonly Vector3 WalkBend2 = new Vector3(13.5f, 0f, -20.0f);
        private static readonly Vector3 WalkEnd = new Vector3(PizzeriaLayout.ExitDoor.x, 0f, -18.35f);

        private const float RevealAt = 6.4f;

        private IEnumerator OnFoot(PlayerController player)
        {
            Car car = exterior.Car;
            float length = WalkLength();
            float walked = 0f;
            float stepAt = 0f;
            bool slam = false, light = false, flash1 = false, flash2 = false, flash3 = false, gasp = false, hidden = false;
            var doorTarget = new Vector3(PizzeriaLayout.ExitDoor.x, 1.4f, -17.3f);
            Vector3 strangerFace = StrangerSpot + Vector3.up * 1.95f;
            effects.PlayOneShot(director.Assets.LockerDoor, 0.25f);

            for (float g = 0f; walked < length && !skip; g += Time.deltaTime)
            {
                Mark("foot", g);
                // Lightning three times: the first just a flash, the second shows him, the third shows nobody.
                if (!flash1 && g > 5.1f)
                {
                    flash1 = true;
                    exterior.Lightning();
                    Invoke(nameof(Thunder), 1.3f);
                }
                if (!flash2 && g > RevealAt)
                {
                    flash2 = true;
                    exterior.Lightning();
                    ShowStranger(true);
                    director.Audio.Sting();
                    director.Audio.Heartbeat(0.9f);
                }
                if (!hidden && g > RevealAt + 1.1f)
                {
                    hidden = true;
                    ShowStranger(false);
                }
                if (!flash3 && g > RevealAt + 1.9f)
                {
                    flash3 = true;
                    exterior.Lightning();
                    Invoke(nameof(Thunder), 0.6f);
                }
                if (!gasp && g > RevealAt + 2.2f)
                {
                    gasp = true;
                    director.Audio.Gasp();
                }

                Vector3 eye, look;
                float fov = 68f, fade = 0f;
                if (g < 0.9f)
                {
                    // Still in the seat, in the dark, reaching for the door.
                    eye = car.transform.TransformPoint(car.DriverEyes);
                    look = car.transform.TransformPoint(car.DriverEyes + new Vector3(-1f, -0.25f, 0.4f));
                    fade = 1f - g / 0.9f;
                }
                else
                {
                    if (!slam && g > 1.1f)
                    {
                        slam = true;
                        effects.PlayOneShot(director.Assets.LockerDoor, 0.8f);
                    }
                    if (!light && g > 1.8f)
                    {
                        light = true;
                        player.Flashlight.enabled = true;
                        director.Audio.Click();
                    }
                    // Walking: steady, stopped dead by what the lightning shows, then fast and out of breath.
                    bool frozen = g > RevealAt - 0.1f && g < RevealAt + 2.8f;
                    float speed = g < 2.1f || frozen ? 0f : g > RevealAt ? 1.85f : 1.3f;
                    walked = Mathf.Min(walked + speed * Time.deltaTime, length);
                    Vector3 feet = WalkPoint(UAtLength(walked));
                    float bob = speed > 0f ? Mathf.Sin(walked * (speed > 1.5f ? 4.8f : 4.2f)) * (speed > 1.5f ? 0.04f : 0.028f) : Mathf.Sin(g * 1.3f) * 0.006f;
                    eye = feet + Vector3.up * (EyeHeight + (feet.z > SidewalkZ ? SidewalkHeight : 0f) + bob);
                    look = Vector3.Lerp(new Vector3(-2f, 4.2f, -16.4f), doorTarget, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - 2f) / 2f)));
                    // The head jerks toward the corner when the lightning shows someone there, and the
                    // eyes stay on the empty spot a moment after it is gone; later one look back over the shoulder.
                    float stare = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - (RevealAt - 0.15f)) / 0.18f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - (RevealAt + 2.6f)) / 0.7f)));
                    look = Vector3.Lerp(look, strangerFace, stare);
                    fov = Mathf.Lerp(68f, 26f, stare * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - RevealAt) / 0.3f)));
                    float back = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - (RevealAt + 5.2f)) / 0.4f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - (RevealAt + 6.3f)) / 0.5f)));
                    look = Vector3.Lerp(look, StrangerSpot + Vector3.up * 1.4f, back * 0.9f);
                    eye += Handheld(g, frozen ? 0.012f : 0.006f);
                    if (speed > 0f && g >= stepAt)
                    {
                        stepAt = g + (speed > 1.5f ? 0.38f : 0.55f);
                        director.Audio.Footstep(0.55f);
                    }
                    if (g > RevealAt + 2.8f && Time.frameCount % 70 == 0)
                    {
                        director.Audio.Pant(0.35f);
                    }
                }
                Aim(player, eye, look, fov);
                director.UI.SetCinematic(subtitle, fade);
                yield return null;
            }
            ShowStranger(false);
        }

        private void ShowStranger(bool on)
        {
            if (strangerRenderers == null)
            {
                return;
            }
            foreach (Renderer renderer in strangerRenderers)
            {
                renderer.enabled = on;
            }
            strangerRim.enabled = on;
            strangerFill.enabled = on;
        }

        private static Vector3 WalkPoint(float u)
        {
            return u < 0.5f ? Curve(WalkStart, WalkBend1, WalkMiddle, u * 2f) : Curve(WalkMiddle, WalkBend2, WalkEnd, u * 2f - 1f);
        }

        private static Vector3 Curve(Vector3 from, Vector3 control, Vector3 to, float u)
        {
            return from * ((1f - u) * (1f - u)) + control * (2f * u * (1f - u)) + to * (u * u);
        }

        private static float WalkLength()
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

        // ------------------------------------------------------------------ 6. the chained doors

        private IEnumerator Door(PlayerController player)
        {
            Vector3 outside = WalkEnd + Vector3.up * (EyeHeight + SidewalkHeight);
            var inside = new Vector3(PizzeriaLayout.ExitDoor.x, EyeHeight, -14.6f);
            Vector3 outward = -PizzeriaLayout.ExitInward(HuntMap.Exit);
            Vector3 right = Vector3.Cross(Vector3.up, -outward);
            Vector3 keyhole = padlock.position;
            Vector3 lockFrom = padlock.position;
            Quaternion lockRest = padlock.rotation;
            // The key comes up from below the frame (the guard's hand), on its ring.
            Vector3 keyStart = keyhole + outward * 0.32f + right * 0.2f + Vector3.down * 0.3f;
            Vector3 keyBend = keyhole + outward * 0.22f + right * 0.06f + Vector3.down * 0.05f;
            Vector3 keyIn = key.position;
            Quaternion keyRest = key.rotation;
            key.gameObject.SetActive(true);
            Vector3 lockAway = lockFrom + outward * 0.35f + right * 0.18f + Vector3.down * 0.35f;
            bool scrape = false, click = false, pop = false, opened = false, leftGo = false, rightGo = false, doors = false, ring1 = false, ring2 = false;
            for (float d = 0f; d < 9.2f && !skip; d += Time.deltaTime)
            {
                Mark("door", d);
                // 1. The key: up into the light, a wobble as it finds the slot, in; a quarter turn.
                float bring = Mathf.Clamp01((d - 1f) / 0.55f);
                float u = Mathf.SmoothStep(0f, 1f, bring);
                Vector3 path = (1f - u) * (1f - u) * keyStart + 2f * u * (1f - u) * keyBend + u * u * keyhole;
                if (key.parent != padlock)
                {
                    float find = Mathf.Clamp01((d - 1.4f) / 0.15f);
                    key.position = Vector3.Lerp(path + (keyIn - keyhole), keyIn, find) + right * Mathf.Sin(d * 40f) * 0.002f * (1f - find) * bring;
                    key.rotation = keyRest * Quaternion.Euler(-25f * (1f - u), 18f * (1f - u), 0f);
                    if (find >= 1f)
                    {
                        key.SetParent(padlock, true);
                    }
                }
                else
                {
                    key.localRotation = Quaternion.Inverse(lockRest) * keyRest * Quaternion.Euler(0f, 0f, 90f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 1.65f) / 0.25f)));
                }
                // The ring and its other key swing on the bow, livelier while the key moves.
                float sway = Mathf.Exp(-Mathf.Max(0f, d - 1.55f) * 2.5f) * (d > 1f ? 1f : 0f);
                keyRing.localRotation = Quaternion.Euler(Mathf.Sin(d * 9f) * 28f * sway, 0f, Mathf.Sin(d * 6.3f + 1f) * 18f * sway);
                if (!scrape && d > 1.4f)
                {
                    scrape = true;
                    effects.PlayOneShot(GameAudio.Ratchet(), 0.35f);
                }
                if (!click && d > 1.9f)
                {
                    click = true;
                    effects.PlayOneShot(GameAudio.Ratchet(), 0.6f);
                }
                if (!pop && d > 2f)
                {
                    pop = true;
                    effects.PlayOneShot(GameAudio.Clunk(), 0.5f);
                }
                shackle.localPosition = new Vector3(0f, 0.014f * Mathf.Clamp01((d - 2f) / 0.06f), 0f);

                // 2. The open lock is lifted off the chain and taken away, the key still in it.
                if (d > 2.15f)
                {
                    float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 2.15f) / 0.15f));
                    float away = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 2.3f) / 0.3f));
                    padlock.position = Vector3.Lerp(lockFrom + Vector3.up * 0.02f * lift, lockAway, away);
                    padlock.rotation = lockRest * Quaternion.Euler(-40f * away, 25f * away, 0f);
                    if (away >= 1f)
                    {
                        padlock.gameObject.SetActive(false);
                    }
                }
                if (!opened && d > 2.3f)
                {
                    opened = true;
                    chain.Unlock();
                    effects.PlayOneShot(GameAudio.Ratchet(), 0.5f);
                }
                // 3. The halves swing from the bars, then slide off them and drop to the floor.
                if (!leftGo && d > 3f)
                {
                    leftGo = true;
                    chain.Release(0);
                    effects.PlayOneShot(GameAudio.Ratchet(), 0.7f);
                }
                if (!rightGo && d > 3.35f)
                {
                    rightGo = true;
                    chain.Release(1);
                    effects.PlayOneShot(GameAudio.Ratchet(), 0.7f);
                }
                chain.Step(Time.deltaTime);
                if (chain.Landed())
                {
                    effects.PlayOneShot(GameAudio.Clunk(), 0.6f);
                    effects.PlayOneShot(GameAudio.Ratchet(), 0.9f);
                }

                if (!doors && d > 4f)
                {
                    doors = true;
                    effects.PlayOneShot(director.Assets.ExitDoor != null ? director.Assets.ExitDoor : director.Assets.LockerDoor, 0.6f);
                }
                director.Level.Exit.SetLift(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 4f) / 1.8f)) * 0.75f);

                // Far inside, the office phone starts to ring.
                if (!ring1 && d > 6.6f)
                {
                    ring1 = true;
                    director.Audio.Ring();
                }
                if (!ring2 && d > 8.8f)
                {
                    ring2 = true;
                    director.Audio.Ring();
                }

                Vector3 eye, look;
                float fov;
                if (d < 4f)
                {
                    // Close on the padlock in the flashlight, then down with the chain as it falls.
                    // When the lock comes off, a step back to see both halves swing and drop.
                    float back = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 2.3f) / 0.6f));
                    Vector3 close = keyhole + outward * 0.5f + new Vector3(0f, 0.14f, 0f) + right * 0.06f;
                    Vector3 wide = keyhole + outward * 1.15f + new Vector3(0f, 0.3f, 0f) + right * 0.1f;
                    eye = Vector3.Lerp(outside, Vector3.Lerp(close, wide, back), Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / 1f))) + Handheld(d, 0.004f);
                    look = Vector3.Lerp(new Vector3(PizzeriaLayout.ExitDoor.x, 1.4f, -17.3f), keyhole, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / 0.9f)));
                    look = Vector3.Lerp(look, chain.Center, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 2.4f) / 0.8f)) * 0.8f);
                    fov = Mathf.Lerp(46f, 62f, back);
                }
                else
                {
                    // Push in through the doors into the dark lobby.
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 4.5f) / 3.6f));
                    eye = Vector3.Lerp(outside, inside, k) + Vector3.up * Mathf.Sin(k * 18f) * 0.025f;
                    look = eye + new Vector3(-0.3f * k, -0.05f, 1f);
                    fov = 68f;
                    if (eye.z > -17.2f)
                    {
                        exterior.SetOutdoors(false, player.View);
                        director.Audio.PlayAmbient(true);
                    }
                    rain.volume = Mathf.Lerp(0.5f, 0.12f, k);
                    if (d > 4.7f && d < 8f && Time.frameCount % 34 == 0)
                    {
                        director.Audio.Footstep(0.45f);
                    }
                }
                Aim(player, eye, look, fov);
                director.UI.SetCinematic(subtitle, Mathf.Clamp01((d - 8.4f) / 0.8f));
                yield return null;
            }
            director.Level.Exit.SetLift(0f);
        }

        // ------------------------------------------------------------------ 7. inside

        private IEnumerator Interior(PlayerController player, Vector3 from, Vector3 to, Vector3 target, float seconds, string title, int size, bool wake)
        {
            float t = 0f;
            bool woke = false;
            float ringAt = 0.8f;
            while (t < seconds && !skip)
            {
                t += Time.deltaTime;
                Mark(wake ? "title" : "inside", t);
                if (wake && !woke && t > 1.1f)
                {
                    // Just as the title comes up, his eyes open red for a moment, with a sting.
                    woke = true;
                    director.Freddy.FlashEyes(1.3f);
                    director.Audio.Sting();
                }
                if (!wake && t > ringAt)
                {
                    ringAt += 2.6f;
                    director.Audio.Ring();
                }
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

        // ------------------------------------------------------------------ the voicemail

        /// <summary>Plays the answering machine and the message, one line after another, with subtitles.</summary>
        private IEnumerator Voicemail(float delay)
        {
            yield return new WaitForSeconds(delay);
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
        }

        private void Thunder()
        {
            if (Playing && director.Assets.Thunder != null)
            {
                effects.PlayOneShot(director.Assets.Thunder, 0.9f);
            }
        }

        // ------------------------------------------------------------------ the props of the intro

        private void Prepare()
        {
            // The camcorder of 1987: a second camera filming into a small picture, and the party's lights.
            tapeTexture = new RenderTexture(TapeWidth, TapeHeight, 16, RenderTextureFormat.ARGB32) { name = "HomeVideo", filterMode = FilterMode.Bilinear };
            tapeCamera = new GameObject("HomeVideoCamera").AddComponent<Camera>();
            tapeCamera.transform.SetParent(transform, false);
            tapeCamera.targetTexture = tapeTexture;
            tapeCamera.nearClipPlane = 0.1f;
            tapeCamera.farClipPlane = 40f;
            tapeCamera.clearFlags = CameraClearFlags.SolidColor;
            tapeCamera.backgroundColor = Color.black;
            tapeCamera.allowMSAA = false;
            tapeCamera.enabled = false;
            UniversalAdditionalCameraData data = tapeCamera.GetUniversalAdditionalCameraData();
            data.renderShadows = true;
            // No post-processing: its film grain, blown up from this small picture, would bury it.
            data.renderPostProcessing = false;
            partyLights.Add(Spot(new Vector3(7.3f, 4.6f, 6.4f), PizzeriaLayout.FreddyStart + Vector3.up * 1.2f, 26f, 14f, new Color(1f, 0.9f, 0.72f), 18f));
            partyLights.Add(Point(new Vector3(6.6f, 2.6f, 5.6f), 10f, new Color(1f, 0.8f, 0.6f), 3.5f));
            partyLights.Add(Point(new Vector3(9.5f, 2.8f, 6.5f), 8f, new Color(0.6f, 0.75f, 1f), 2f));
            foreach (Light light in partyLights)
            {
                light.enabled = false;
            }
            // Two colored spots sweeping across the stage, from the ceiling at either side.
            sweepers = new[]
            {
                Spot(new Vector3(4.3f, 4f, 7.6f), PizzeriaLayout.FreddyStart, 22f, 13f, new Color(1f, 0.25f, 0.6f), 12f),
                Spot(new Vector3(10.7f, 4f, 7.6f), PizzeriaLayout.FreddyStart, 22f, 13f, new Color(0.25f, 0.5f, 1f), 12f)
            };
            foreach (Light light in sweepers)
            {
                light.enabled = false;
                props.Add(light.gameObject);
            }
            BuildParty();

            // The phone in a vent clip on the dashboard, playing the message.
            Car car = exterior.Car;
            phone = new GameObject("Phone").transform;
            phone.SetParent(car.transform, false);
            // On a suction mount low on the windshield, right of the wheel, a hand's width behind the glass.
            Bounds glass = car.Windshield;
            float mountHeight = glass.size.y > 0f ? glass.min.y + 0.12f : car.SteeringCenter.y + 0.2f;
            float glassZ = glass.size.y > 0f ? glass.max.z - (mountHeight - glass.min.y) * glass.size.z / glass.size.y : car.SteeringCenter.z + 0.3f;
            phone.localPosition = new Vector3(car.SteeringCenter.x + 0.34f, mountHeight, glassZ - 0.07f);
            phone.localRotation = Quaternion.LookRotation(phone.localPosition - car.DriverEyes);
            BuildKit.Box("Body", phone, Vector3.zero, new Vector3(0.075f, 0.155f, 0.009f), BuildKit.Plain("phoneBody", new Color(0.03f, 0.03f, 0.035f), 0.7f, 0.3f), 1f, false);
            Texture2D screen = BuildKit.SignTexture("screen_voicemail");
            if (screen != null)
            {
                BuildKit.Quad("Screen", phone, new Vector3(0f, 0f, -0.0048f), new Vector2(0.068f, 0.143f), 180f, BuildKit.Picture("phoneScreen", screen, 1.3f));
            }
            var glow = new GameObject("ScreenLight").AddComponent<Light>();
            glow.transform.SetParent(phone, false);
            glow.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            glow.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            glow.type = LightType.Spot;
            glow.spotAngle = 100f;
            glow.range = 1.2f;
            glow.intensity = 0.6f;
            glow.color = new Color(0.7f, 0.8f, 1f);
            glow.shadows = LightShadows.None;
            phone.gameObject.SetActive(false);
            props.Add(phone.gameObject);

            // Who stands under the dead lamp (only while the lightning shows him).
            if (director.Assets.FreddyPrefab != null)
            {
                GameObject stranger = Instantiate(director.Assets.FreddyPrefab, transform);
                stranger.name = "Stranger";
                stranger.transform.SetPositionAndRotation(StrangerSpot, Quaternion.LookRotation(Flat(WalkMiddle - StrangerSpot)));
                foreach (MonoBehaviour behaviour in stranger.GetComponentsInChildren<MonoBehaviour>())
                {
                    behaviour.enabled = false;
                }
                // A cold edge of light on him from the lightning behind, so he reads against the dark.
                strangerRim = new GameObject("Rim").AddComponent<Light>();
                strangerRim.transform.SetParent(stranger.transform, false);
                strangerRim.transform.localPosition = new Vector3(-1.2f, 3.2f, -2.2f);
                strangerRim.transform.LookAt(stranger.transform.position + Vector3.up * 1.4f);
                strangerRim.type = LightType.Spot;
                strangerRim.spotAngle = 40f;
                strangerRim.range = 8f;
                strangerRim.intensity = 70f;
                strangerRim.color = new Color(0.65f, 0.75f, 1f);
                strangerRim.shadows = LightShadows.None;
                strangerFill = new GameObject("Fill").AddComponent<Light>();
                strangerFill.transform.SetParent(stranger.transform, false);
                strangerFill.transform.localPosition = new Vector3(0.6f, 2.2f, 3f);
                strangerFill.type = LightType.Point;
                strangerFill.range = 5f;
                strangerFill.intensity = 3f;
                strangerFill.color = new Color(0.6f, 0.7f, 1f);
                strangerFill.shadows = LightShadows.None;
                strangerRenderers = stranger.GetComponentsInChildren<Renderer>();
                ShowStranger(false);
                props.Add(stranger);
            }

            BuildChain();
        }

        /// <summary>
        /// The birthday on the tape: kids in party hats in front of the stage (seen from behind, over
        /// their heads) and confetti drifting down through the stage lights.
        /// </summary>
        private void BuildParty()
        {
            var party = new GameObject("Party").transform;
            party.SetParent(transform, false);
            props.Add(party.gameObject);
            Color[] shirts = { new Color(0.7f, 0.12f, 0.1f), new Color(0.15f, 0.3f, 0.7f), new Color(0.9f, 0.75f, 0.2f), new Color(0.2f, 0.55f, 0.25f), new Color(0.6f, 0.2f, 0.6f), new Color(0.9f, 0.45f, 0.15f), new Color(0.35f, 0.35f, 0.4f) };
            Color[] hats = { new Color(1f, 0.3f, 0.5f), new Color(0.3f, 0.8f, 1f), new Color(1f, 0.85f, 0.2f), new Color(0.5f, 1f, 0.4f) };
            Material skin = BuildKit.Plain("kidSkin", new Color(0.72f, 0.52f, 0.4f), 0.3f);
            Material hair = BuildKit.Plain("kidHair", new Color(0.12f, 0.08f, 0.05f), 0.25f);
            var random = new System.Random(8);
            for (int i = 0; i < 9; i++)
            {
                var spot = new Vector3(5.2f + i * 0.58f + (float)random.NextDouble() * 0.2f, 0f, 8.5f + (float)random.NextDouble() * 0.9f - (i % 2) * 0.5f);
                var root = new GameObject("KidSpot").transform;
                root.SetParent(party, false);
                root.localPosition = spot;
                var kid = new GameObject("Kid").transform;
                kid.SetParent(root, false);
                float height = 1f + (float)random.NextDouble() * 0.25f;
                Material shirt = BuildKit.Plain("kidShirt" + i % shirts.Length, shirts[i % shirts.Length], 0.3f);
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(body.GetComponent<Collider>());
                body.transform.SetParent(kid, false);
                body.transform.localPosition = new Vector3(0f, height * 0.42f, 0f);
                body.transform.localScale = new Vector3(0.3f, height * 0.42f, 0.22f);
                body.GetComponent<Renderer>().sharedMaterial = shirt;
                BuildKit.Sphere("Head", kid, new Vector3(0f, height * 0.88f, 0f), 0.21f, skin);
                BuildKit.Sphere("Hair", kid, new Vector3(0f, height * 0.9f, -0.02f), 0.215f, hair);
                BuildKit.Cone("Hat", kid, new Vector3(0f, height * 0.97f, 0f), 0.07f, 0.2f, BuildKit.Plain("hat" + i % hats.Length, hats[i % hats.Length], 0.3f));
                kids.Add(kid);
            }

            // Confetti in four colors (one system each: the lit material takes no per-particle color).
            Color[] papers = { new Color(1f, 0.3f, 0.5f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.75f, 1f), new Color(0.45f, 0.95f, 0.4f) };
            for (int c = 0; c < papers.Length; c++)
            {
                var drift = new GameObject("Confetti");
                drift.transform.SetParent(party, false);
                drift.transform.position = PizzeriaLayout.FreddyStart + new Vector3(0f, 3.6f, -1.8f);
                ParticleSystem system = drift.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                main.prewarm = true;
                main.startLifetime = 7f;
                main.startSpeed = 0.1f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = 0.03f;
                main.maxParticles = 160;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = 18f;
                ParticleSystem.ShapeModule shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(7f, 0.1f, 3.5f);
                ParticleSystem.NoiseModule flutter = system.noise;
                flutter.enabled = true;
                flutter.strength = 0.25f;
                flutter.frequency = 0.8f;
                system.GetComponent<ParticleSystemRenderer>().sharedMaterial = BuildKit.Glow("confetti" + c, papers[c], 0.6f);
                confetti.Add(system);
            }
        }

        /// <summary>A chain looped through the front doors' push bars, a padlock joining its ends, and the key on its ring.</summary>
        private void BuildChain()
        {
            Vector3 outward = -PizzeriaLayout.ExitInward(HuntMap.Exit);
            var root = new GameObject("Chain").transform;
            root.SetParent(transform, false);
            root.SetPositionAndRotation(PizzeriaLayout.ExitDoor + outward * 0.11f + Vector3.up * 1.05f, Quaternion.LookRotation(-outward));
            props.Add(root.gameObject);
            Material steel = BuildKit.Plain("chainSteel", new Color(0.16f, 0.16f, 0.17f), 0.5f, 0.9f);
            Material brass = BuildKit.Plain("padlockBrass", new Color(0.36f, 0.27f, 0.12f), 0.45f, 1f);
            Material keySteel = BuildKit.Plain("keySteel", new Color(0.62f, 0.6f, 0.53f), 0.65f, 1f);

            padlock = new GameObject("Padlock").transform;
            padlock.SetParent(root, false);
            padlock.localPosition = new Vector3(0.01f, -0.17f, -0.02f);
            BuildKit.Box("Body", padlock, Vector3.zero, new Vector3(0.06f, 0.055f, 0.024f), brass, 1f, false);
            BuildKit.Box("Face", padlock, new Vector3(0f, -0.004f, -0.0125f), new Vector3(0.05f, 0.04f, 0.002f), BuildKit.Plain("padlockFace", new Color(0.5f, 0.38f, 0.16f), 0.5f, 1f), 1f, false);
            BuildKit.Cylinder("Keyway", padlock, new Vector3(0f, -0.008f, -0.0132f), 0.012f, 0.001f, BuildKit.Plain("keyway", new Color(0.05f, 0.05f, 0.05f), 0.3f)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shackle = new GameObject("Shackle").transform;
            shackle.SetParent(padlock, false);
            for (int side = -1; side <= 1; side += 2)
            {
                BuildKit.Cylinder("Leg", shackle, new Vector3(side * 0.019f, 0.03f, 0f), 0.009f, 0.05f, steel);
            }
            BuildKit.Box("Bow", shackle, new Vector3(0f, 0.056f, 0f), new Vector3(0.047f, 0.009f, 0.009f), steel, 1f, false);

            // The chain: from the left push bar down to the lock and up to the right bar, a rope of links.
            Transform links = new GameObject("Links").transform;
            links.SetParent(transform, false);
            props.Add(links.gameObject);
            chain = new ChainRope(root.TransformPoint(new Vector3(-0.42f, 0f, 0f)), root.TransformPoint(new Vector3(0.42f, 0f, 0f)),
                                  padlock.TransformPoint(new Vector3(0f, 0.056f, 0f)), PizzeriaLayout.ExitDoor.y + 0.03f, links, steel);

            // The key, out of sight until the hand brings it, with a ring, a second key and a tag.
            key = new GameObject("Key").transform;
            key.SetParent(root, false);
            key.localPosition = padlock.localPosition + new Vector3(0f, -0.008f, -0.03f);
            key.localRotation = Quaternion.identity;
            BuildKit.Box("Blade", key, new Vector3(0f, 0f, 0.012f), new Vector3(0.006f, 0.013f, 0.03f), keySteel, 1f, false);
            BuildKit.Box("Shoulder", key, new Vector3(0f, 0f, -0.004f), new Vector3(0.007f, 0.018f, 0.004f), keySteel, 1f, false);
            BuildKit.Box("Bow", key, new Vector3(0f, 0f, -0.019f), new Vector3(0.004f, 0.03f, 0.026f), keySteel, 1f, false);
            keyRing = new GameObject("Ring").transform;
            keyRing.SetParent(key, false);
            keyRing.localPosition = new Vector3(0f, -0.01f, -0.03f);
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.PI * 2f / 12f;
                var at = new Vector3(0f, -0.014f + Mathf.Cos(angle) * 0.014f, Mathf.Sin(angle) * 0.014f);
                BuildKit.Cylinder("Wire", keyRing, at, 0.0018f, 0.008f, steel).transform.localRotation = Quaternion.Euler(angle * Mathf.Rad2Deg + 90f, 0f, 0f);
            }
            var spare = new GameObject("SpareKey").transform;
            spare.SetParent(keyRing, false);
            spare.localPosition = new Vector3(0.002f, -0.028f, 0f);
            spare.localRotation = Quaternion.Euler(70f, 15f, 0f);
            BuildKit.Box("Bow", spare, new Vector3(0f, 0f, 0.01f), new Vector3(0.004f, 0.026f, 0.022f), BuildKit.Plain("spareKey", new Color(0.55f, 0.45f, 0.22f), 0.6f, 1f), 1f, false);
            BuildKit.Box("Blade", spare, new Vector3(0f, 0f, 0.035f), new Vector3(0.005f, 0.012f, 0.03f), BuildKit.Plain("spareKey", new Color(0.55f, 0.45f, 0.22f), 0.6f, 1f), 1f, false);
            BuildKit.Box("Tag", keyRing, new Vector3(-0.004f, -0.05f, 0.004f), new Vector3(0.003f, 0.04f, 0.024f), BuildKit.Plain("keyTag", new Color(0.45f, 0.08f, 0.06f), 0.35f), 1f, false)
                .transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            key.gameObject.SetActive(false);
        }

        /// <summary>
        /// The chain as a rope of points a link apart (Verlet integration with distance constraints):
        /// held at both push bars and, through the padlock, in the middle. Opened, the two halves swing
        /// from the bars; released, each slides off and falls, and lies on the floor.
        /// </summary>
        private class ChainRope
        {
            private const int Half = 16;              // links from a bar to the lock
            private const float Tick = 1f / 120f;
            private static readonly Vector3 Gravity = new Vector3(0f, -9.81f, 0f);

            private readonly Vector3[] position;
            private readonly Vector3[] previous;
            private readonly bool[] pinned;
            private readonly float rest;
            private readonly float floor;
            private readonly Transform[] links;
            private bool locked = true;
            private float pending;
            private bool landedLeft, landedRight;

            public ChainRope(Vector3 leftBar, Vector3 rightBar, Vector3 lockPoint, float floorHeight, Transform parent, Material steel)
            {
                int count = 2 * Half + 2;
                position = new Vector3[count];
                previous = new Vector3[count];
                pinned = new bool[count];
                floor = floorHeight;
                float length = 0f;
                for (int i = 0; i <= Half; i++)
                {
                    float u = i / (float)Half;
                    position[i] = Vector3.Lerp(leftBar, lockPoint, u) + Vector3.down * Mathf.Sin(u * Mathf.PI) * 0.045f;
                    position[count - 1 - i] = Vector3.Lerp(rightBar, lockPoint, u) + Vector3.down * Mathf.Sin(u * Mathf.PI) * 0.045f;
                    if (i > 0)
                    {
                        length += Vector3.Distance(position[i - 1], position[i]);
                    }
                }
                rest = length / Half;
                pinned[0] = pinned[count - 1] = pinned[Half] = pinned[Half + 1] = true;
                System.Array.Copy(position, previous, count);
                links = new Transform[count - 2];
                for (int i = 0; i < links.Length; i++)
                {
                    var link = new GameObject("Link").transform;
                    link.SetParent(parent, false);
                    // An oval link along its local x: two thin sides and their rounded ends.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        BuildKit.Cylinder("Side", link, new Vector3(0f, side * 0.008f, 0f), 0.006f, rest, steel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        BuildKit.Cylinder("End", link, new Vector3(side * rest * 0.55f, 0f, 0f), 0.006f, 0.018f, steel);
                    }
                    links[i] = link;
                }
                Draw();
            }

            /// <summary>Where the chain is, on average (for the camera to follow it down).</summary>
            public Vector3 Center
            {
                get
                {
                    Vector3 sum = Vector3.zero;
                    foreach (Vector3 point in position)
                    {
                        sum += point;
                    }
                    return sum / position.Length;
                }
            }

            /// <summary>The lock is off: the two ends in the middle hang free.</summary>
            public void Unlock()
            {
                locked = false;
                pinned[Half] = pinned[Half + 1] = false;
                // A small tug as the shackle comes out.
                previous[Half] = position[Half] + new Vector3(0.01f, -0.02f, 0.01f);
                previous[Half + 1] = position[Half + 1] + new Vector3(-0.01f, -0.02f, 0.01f);
            }

            /// <summary>One half (0 left, 1 right) slides off its push bar.</summary>
            public void Release(int side)
            {
                int end = side == 0 ? 0 : position.Length - 1;
                pinned[end] = false;
                previous[end] = position[end] + new Vector3(side == 0 ? 0.03f : -0.03f, 0f, 0.02f);
            }

            /// <summary>True once, the first time each half hits the floor.</summary>
            public bool Landed()
            {
                bool left = position[Half / 2].y <= floor + 0.01f;
                bool right = position[position.Length - 1 - Half / 2].y <= floor + 0.01f;
                bool now = left && !landedLeft || right && !landedRight;
                landedLeft |= left;
                landedRight |= right;
                return now;
            }

            public void Step(float deltaTime)
            {
                pending = Mathf.Min(pending + deltaTime, 0.1f);
                while (pending >= Tick)
                {
                    pending -= Tick;
                    Simulate();
                }
                Draw();
            }

            private void Simulate()
            {
                for (int i = 0; i < position.Length; i++)
                {
                    if (pinned[i])
                    {
                        previous[i] = position[i];
                        continue;
                    }
                    Vector3 velocity = (position[i] - previous[i]) * 0.992f;
                    previous[i] = position[i];
                    position[i] += velocity + Gravity * (Tick * Tick);
                }
                for (int iteration = 0; iteration < 16; iteration++)
                {
                    for (int i = 0; i < position.Length - 1; i++)
                    {
                        if (i == Half && !locked)
                        {
                            continue;   // the lock no longer joins the two halves
                        }
                        float length = i == Half ? 0f : rest;
                        Vector3 delta = position[i + 1] - position[i];
                        float distance = delta.magnitude;
                        if (distance < 1e-6f)
                        {
                            continue;
                        }
                        Vector3 correction = delta * ((distance - length) / distance);
                        bool a = pinned[i], b = pinned[i + 1];
                        if (a && b)
                        {
                            continue;
                        }
                        if (a)
                        {
                            position[i + 1] -= correction;
                        }
                        else if (b)
                        {
                            position[i] += correction;
                        }
                        else
                        {
                            position[i] += correction * 0.5f;
                            position[i + 1] -= correction * 0.5f;
                        }
                    }
                    for (int i = 0; i < position.Length; i++)
                    {
                        if (position[i].y < floor)
                        {
                            // On the floor: it stops falling and drags (friction).
                            position[i].y = floor;
                            previous[i] = Vector3.Lerp(previous[i], position[i], 0.6f);
                            previous[i].y = floor;
                        }
                    }
                }
            }

            private void Draw()
            {
                int link = 0;
                for (int i = 0; i < position.Length - 1; i++)
                {
                    if (i == Half)
                    {
                        continue;   // the lock, not a link
                    }
                    Vector3 along = position[i + 1] - position[i];
                    Transform t = links[link];
                    t.position = (position[i] + position[i + 1]) * 0.5f;
                    if (along.sqrMagnitude > 1e-8f)
                    {
                        Vector3 up = Mathf.Abs(Vector3.Dot(along.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                        // The link's long side along the chain; every other one turned a quarter about it.
                        t.rotation = Quaternion.LookRotation(along, up) * Quaternion.Euler(0f, -90f, 0f) * Quaternion.Euler(link % 2 == 0 ? 0f : 90f, 0f, 0f);
                    }
                    link++;
                }
            }
        }

        // ------------------------------------------------------------------ helpers

        private void Mark(string part, float time)
        {
            Part = part;
            PartTime = time;
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
            // Never perfectly still: a slow hand-held drift of under a degree, like a real camera operator.
            float t = Time.time;
            Quaternion drift = Quaternion.Euler((Mathf.PerlinNoise(t * 0.6f, 0.2f) - 0.5f) * 0.8f,
                                                (Mathf.PerlinNoise(0.8f, t * 0.5f) - 0.5f) * 1f,
                                                (Mathf.PerlinNoise(t * 0.4f, 3.1f) - 0.5f) * 0.6f);
            player.Head.position = eye;
            player.Head.rotation = Quaternion.LookRotation(target - eye) * drift;
            player.View.fieldOfView = fov;
        }

        /// <summary>A hand-held wobble: slow drift and small shakes.</summary>
        private static Vector3 Handheld(float t, float amount)
        {
            return new Vector3(Mathf.PerlinNoise(t * 0.7f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, t * 0.8f) - 0.5f, Mathf.PerlinNoise(t * 0.5f, 4.2f) - 0.5f) * amount * 2f
                   + new Vector3(Mathf.PerlinNoise(t * 6f, 0.4f) - 0.5f, Mathf.PerlinNoise(0.9f, t * 6f) - 0.5f, 0f) * amount * 0.6f;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v : Vector3.forward;
        }

        private Light Spot(Vector3 position, Vector3 target, float angle, float range, Color color, float intensity)
        {
            var light = new GameObject("PartyLight").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            light.type = LightType.Spot;
            light.spotAngle = angle;
            light.range = range;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            return light;
        }

        private Light Point(Vector3 position, float range, Color color, float intensity)
        {
            var light = new GameObject("PartyLight").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.range = range;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private void Mute()
        {
            bool on = SoundPrefs.EffectsOn;
            voice.mute = effects.mute = rain.mute = engine.mute = music.mute = !on;
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
            music.Stop();
            if (tapeCamera != null)
            {
                if (tapeCamera.enabled)
                {
                    player.View.cullingMask = savedMask;
                    if (director.Freddy.Head != null && director.Freddy.Head != director.Freddy.transform)
                    {
                        director.Freddy.Head.localRotation = headRest;
                    }
                }
                Destroy(tapeCamera.gameObject);
                tapeCamera = null;
            }
            if (tapeTexture != null)
            {
                tapeTexture.Release();
                Destroy(tapeTexture);
                tapeTexture = null;
            }
            foreach (Light light in partyLights)
            {
                Destroy(light.gameObject);
            }
            partyLights.Clear();
            foreach (GameObject prop in props)
            {
                Destroy(prop);
            }
            props.Clear();
            chain = null;
            kids.Clear();
            confetti.Clear();
            showBones = null;
            strangerRenderers = null;
            headTurn = 0f;
            exterior.SetOutdoors(false, player.View);
            exterior.Car.Place(Exterior.ParkingSpot, 0f);
            exterior.Car.SetHeadlights(false);
            exterior.Car.Wipers = false;
            exterior.Car.Steer(0f);
            exterior.Car.SetDoor(0f);
            player.Flashlight.enabled = true;
            director.Level.Exit.SetLift(0f);
            director.Audio.PlayAmbient(true);
            director.UI.HideTape();
            director.UI.SetCinematicCard("", 0f);
            director.UI.ShowCinematic(false);
            player.View.fieldOfView = 70f;
            subtitle = "";
            Part = "";
            Playing = false;
        }
    }
}
