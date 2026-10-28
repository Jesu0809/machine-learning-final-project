using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightShift.Game
{
    /// <summary>
    /// After the fifth night, the tape (about a minute, skippable):
    /// <list type="number">
    /// <item>Later that night, the guard's apartment in the rain; the tape goes into the VCR.</item>
    /// <item>The tape plays on the old TV: the grand opening in 1987, Freddy on his stage.</item>
    /// <item>Nightshift Labs, training session 0001: green CCTV, Freddy walking in circles.</item>
    /// <item>Guard #4's fifth night: the office camera, Freddy coming closer, then snow.</item>
    /// <item>"Live": the apartment itself, from the corner. Freddy stands in the doorway on the tape;
    ///       the room is empty when the guard turns around.</item>
    /// <item>The blue screen: guard #5 added to the training set, freddy_v8 training. The lights die.</item>
    /// </list>
    /// The footage is real: a second camera films the building (and the apartment) into a texture
    /// shown on the TV, with the tape's on-screen text, scanlines and tracking noise on top.
    /// </summary>
    public class EndingCinematic : MonoBehaviour
    {
        /// <summary>The apartment is built far under the building, where nothing else can be seen.</summary>
        private static readonly Vector3 Home = new Vector3(0f, -80f, 0f);
        private const int FeedWidth = 320;
        private const int FeedHeight = 240;

        private HuntDirector director;
        private GameObject apartment;
        private Transform tape;
        private Transform flap;
        private Renderer clock;
        private Material screen;
        private Material osd;
        private Renderer osdRenderer;
        private Material sky;
        private Color skyColor;
        private Light tvLight;
        private Light lamp;
        private Light moon;
        private Camera feed;
        private RenderTexture feedTexture;
        private Light[] footageLights;
        private GameObject visitor;
        private Renderer[] visitorRenderers;
        private Vector3 vcrFront;
        private Vector3 tvGlass;
        private AudioSource rain;
        private AudioSource tvSound;
        private AudioClip snow;
        private bool skip;
        private float flashUntil;

        public bool Playing { get; private set; }

        /// <summary>Seconds since the ending started (the capture tour photographs along it).</summary>
        public float Elapsed { get; private set; }

        public static EndingCinematic Create(HuntDirector director)
        {
            var ending = new GameObject("EndingCinematic").AddComponent<EndingCinematic>();
            ending.transform.SetParent(director.transform, false);
            ending.director = director;
            return ending;
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

        // ------------------------------------------------------------------ the sequence

        public IEnumerator Play()
        {
            Playing = true;
            skip = false;
            visitorEverywhere = false;
            Elapsed = 0f;
            PlayerController player = director.Player;
            player.Controllable = false;
            player.CameraLocked = true;
            director.Audio.PlayAmbient(false);
            director.UI.ShowCinematic(true);
            yield return Escape(player);
            director.Exterior.SetOutdoors(false, player.View);
            Build();
            rain = Source(director.Assets.RainLoop, true, 0.45f);
            snow = Snow();
            tvSound = Source(snow, true, 0f);
            var muffle = tvSound.gameObject.AddComponent<AudioLowPassFilter>();
            muffle.cutoffFrequency = 2600f;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;

            bool tapeIn = false, clicked = false, playing = false, music = false, burst = false, lightsOut = false, step = false;
            int lastFrame = -1;
            while (!skip && Elapsed < 64f)
            {
                Elapsed += Time.deltaTime;
                float t = Elapsed;
                string card = "";
                float cardAlpha = 0f;
                int cardSize = 64;
                float fade = 0f;
                Vector3 eye, look;
                float fov = 50f;
                Vector3 room = Home;

                if (t < 2.4f && t > 1.5f && flashUntil < Time.time)
                {
                    Flash(0.5f);
                }
                if (t < 5f)
                {
                    // 1. The apartment, dark, the rain on the window; a flash of lightning.
                    float k = Mathf.SmoothStep(0f, 1f, t / 5f);
                    eye = room + Vector3.Lerp(new Vector3(-1.85f, 1.75f, -1.5f), new Vector3(-1.6f, 1.6f, -1.15f), k);
                    look = room + new Vector3(0.2f, 0.9f, 1.4f);
                    fov = 55f;
                    fade = Mathf.Clamp01(1f - t / 1.5f);
                    Card(t, 0.6f, 4.4f, "LATER THAT NIGHT", 52, ref card, ref cardAlpha, ref cardSize);
                    SetScreen(null, Color.black, null);
                }
                else if (t < 10.5f)
                {
                    // 2. First person at the VCR: the tape comes in from the hand and slides into the slot.
                    float g = t - 5f;
                    eye = vcrFront + new Vector3(0.12f, 0.2f, -0.42f) + Handheld(t, 0.004f);
                    look = vcrFront + new Vector3(-0.02f, -0.01f, 0f);
                    fov = 45f;
                    fade = Mathf.Clamp01(1f - g / 0.35f);
                    PlaceTape(g);
                    float into = Mathf.Clamp01((g - 2.7f) / 0.8f);
                    flap.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -80f, Mathf.Clamp01(into * 2.5f)), 0f, 0f);
                    if (!clicked && g > 2.7f)
                    {
                        clicked = true;
                        director.Audio.PlayAt(GameAudio.Ratchet(), vcrFront, 0.8f, 0.7f);
                    }
                    if (!tapeIn && g > 3.5f)
                    {
                        tapeIn = true;
                        director.Audio.PlayAt(GameAudio.Clunk(), vcrFront, 0.7f, 1.5f);
                        director.Audio.PlayAt(GameAudio.Charge(), vcrFront, 0.35f, 0.45f);
                        tape.gameObject.SetActive(false);
                        flap.localRotation = Quaternion.identity;
                    }
                    bool blink = Mathf.Repeat(t, 1f) < 0.5f;
                    clock.sharedMaterial = TaskKit.Label(g > 4.3f ? "lcd_vcr_play" : "lcd_vcr_clock", Color.black, g > 4.3f || blink ? 1.4f : 0.2f);
                    if (!playing && g > 4.3f)
                    {
                        playing = true;
                        director.Audio.PlayAt(GameAudio.Ratchet(), vcrFront, 0.6f, 1.1f);
                    }
                    SetScreen(null, Color.black, null);
                }
                else if (t < 46f)
                {
                    // 3–6. The tape on the TV, over the guard's shoulder; a few cuts around the room.
                    float g = t - 10.5f;
                    eye = tvGlass + new Vector3(0.28f, 0.12f, -1.05f) + new Vector3(0f, 0f, Mathf.Min(g, 30f) * 0.012f) + Handheld(t, 0.006f);
                    look = tvGlass + new Vector3(0f, -0.02f, 0f);
                    fov = 46f;
                    if (t < 12f)
                    {
                        Snow(1f);
                        tvSound.volume = 0.35f;
                    }
                    else if (t < 13f)
                    {
                        SetScreen(Sign("vhs_blue_play"), Color.white, null);
                        tvSound.volume = 0f;
                    }
                    else if (t < 21.5f)
                    {
                        // The grand opening, 1987: the stage lit, music behind the crowd.
                        if (!music)
                        {
                            music = true;
                            tvSound.clip = director.Assets.MenuMusic;
                            tvSound.volume = 0.32f;
                            tvSound.pitch = 0.96f;
                            tvSound.Play();
                            director.Freddy.ResetTo(PizzeriaLayout.FreddyStart, PizzeriaLayout.FreddyFacing);
                            director.Freddy.WakeUp();
                        }
                        Film(new Vector3(6.2f, 1.55f, 3.2f) + Handheld(t, 0.05f), new Vector3(7.5f, 2.0f, 10.8f), 50f, true);
                        SetScreen(feedTexture, new Color(1.15f, 1.0f, 0.82f), "vhs_osd_1987");
                    }
                    else if (t < 22f)
                    {
                        Snow(0.6f);
                    }
                    else if (t < 29.5f)
                    {
                        // Training session 0001: the dining hall on CCTV, Freddy jumping from place to place.
                        if (music)
                        {
                            music = false;
                            tvSound.clip = snow;
                            tvSound.pitch = 1f;
                            tvSound.volume = 0.04f;
                            tvSound.Play();
                        }
                        int frame = Mathf.FloorToInt((t - 22f) / 0.9f);
                        if (frame != lastFrame)
                        {
                            lastFrame = frame;
                            Vector3[] path = { new Vector3(5f, 0f, 6f), new Vector3(9.5f, 0f, 5f), new Vector3(10f, 0f, 8.5f), new Vector3(6f, 0f, 8.8f), new Vector3(4.5f, 0f, 5.5f), new Vector3(8f, 0f, 4f), new Vector3(10.5f, 0f, 6.5f), new Vector3(7f, 0f, 7f), new Vector3(5f, 0f, 6f) };
                            Vector3 at = path[frame % path.Length];
                            director.Freddy.ResetTo(at, 40f * frame + 90f);
                            director.Freddy.WakeUp();
                        }
                        Film(new Vector3(12.6f, 3.9f, 2.4f), new Vector3(6.5f, 0.5f, 7.5f), 70f, false);
                        SetScreen(feedTexture, new Color(0.75f, 1.25f, 0.8f), "vhs_osd_sim");
                    }
                    else if (t < 30f)
                    {
                        Snow(0.6f);
                    }
                    else if (t < 36.6f)
                    {
                        // Guard #4, night 5: the office camera. Empty, then he is at the door, then closer.
                        float h = t - 30f;
                        int frame = 100 + (h < 2f ? 0 : h < 4f ? 1 : h < 5.4f ? 2 : 3);
                        if (frame != lastFrame)
                        {
                            lastFrame = frame;
                            Vector3[] steps = { new Vector3(-30f, 0f, -30f), new Vector3(-19f, 0f, -8.6f), new Vector3(-19.2f, 0f, -11f), new Vector3(-20.6f, 0f, -14f) };
                            director.Freddy.ResetTo(steps[frame - 100], 200f);
                            director.Freddy.WakeUp();
                        }
                        Film(new Vector3(-21.6f, 2.8f, -15.6f), new Vector3(-18.5f, 1f, -9.5f), 72f, false);
                        SetScreen(feedTexture, new Color(0.85f, 0.95f, 0.85f), "vhs_osd_guard4");
                        if (h > 5.4f)
                        {
                            Shake(0.06f);
                        }
                    }
                    else if (t < 38f)
                    {
                        if (!burst)
                        {
                            burst = true;
                            director.Audio.StaticBurst();
                        }
                        Snow(1f);
                    }
                    else if (t < 44f)
                    {
                        // "Live": this room, from the corner behind the guard. Someone is in the doorway.
                        float h = t - 38f;
                        Film(room + new Vector3(1.95f, 2.35f, 1.2f), room + new Vector3(0.6f, 0.9f, -1.6f), 68f, false);
                        SetScreen(feedTexture, new Color(0.8f, 0.85f, 0.95f), "vhs_osd_guard5");
                        if (h > 3.4f)
                        {
                            // The guard turns around: the doorway is empty.
                            eye = room + new Vector3(0.25f, 1.25f, 0.9f) + Handheld(t, 0.01f);
                            look = room + new Vector3(1.45f, 1.3f, -1.9f);
                            fov = 50f;
                        }
                    }
                    else
                    {
                        Snow(1f);
                        tvSound.volume = 0.3f;
                    }
                    // A cut to the window, lightning over the houses.
                    if (t > 25.5f && t < 27.2f)
                    {
                        if (flashUntil < Time.time - 1f)
                        {
                            Flash(0.4f);
                            if (director.Assets.Thunder != null)
                            {
                                director.Audio.PlayAt(director.Assets.Thunder, room, 0.5f);
                            }
                        }
                        eye = room + new Vector3(0.9f, 1.5f, -0.4f);
                        look = room + new Vector3(2.2f, 1.45f, 0.2f);
                        fov = 50f;
                    }
                }
                else if (t < 50.5f)
                {
                    // 7. The blue screen: what the tape is for.
                    float g = t - 46f;
                    tvSound.volume = 0f;
                    eye = tvGlass + new Vector3(0.05f, 0.02f, -0.75f + g * 0.04f) + Handheld(t, 0.003f);
                    look = tvGlass;
                    fov = 42f;
                    SetScreen(Sign("vhs_blue_end"), Color.white, null);
                }
                else if (t < 51.2f)
                {
                    Snow(1f);
                    if (!lightsOut)
                    {
                        lightsOut = true;
                        director.Audio.StaticBurst();
                        // He is in the room now, right behind the chair.
                        if (visitor != null)
                        {
                            visitor.transform.localPosition = new Vector3(0.25f, 0f, -1.3f);
                            visitor.transform.localRotation = Quaternion.identity;
                        }
                    }
                    eye = room + new Vector3(0.25f, 1.15f, -0.35f) + Handheld(t, 0.004f);
                    look = tvGlass;
                    fov = 46f;
                }
                else if (t < 55.4f)
                {
                    // 8. LIVE again, from the corner: someone stands behind the guard's chair.
                    //    The guard looks at the doorway (empty), back at the TV...
                    float g = t - 51.2f;
                    Film(room + new Vector3(1.95f, 2.35f, 1.2f), room + new Vector3(0.3f, 1f, -0.9f), 62f, false);
                    SetScreen(feedTexture, new Color(0.8f, 0.85f, 0.95f), "vhs_osd_guard5");
                    Vector3 seat = room + new Vector3(0.25f, 1.15f, -0.35f);
                    Vector3 doorway = room + new Vector3(1.45f, 1.2f, -1.8f);
                    float glance = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - 1.2f) / 0.6f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((g - 2.4f) / 0.5f)));
                    eye = seat + Handheld(t, 0.005f);
                    look = Vector3.Lerp(tvGlass, doorway, glance);
                    fov = Mathf.Lerp(46f, 40f, Mathf.Clamp01((g - 2.9f) / 1.3f));
                    lamp.enabled = Random.value < 0.85f;
                }
                else if (t < 56.6f)
                {
                    // 9. He turns around. He is there.
                    float g = t - 55.4f;
                    if (!step)
                    {
                        step = true;
                        visitorEverywhere = true;
                        director.Audio.Scream();
                        director.Audio.StaticBurst();
                        director.Audio.Impact();
                        lamp.enabled = false;
                        // Red light on his face, from his own eyes.
                        PointLight(apartment.transform, VisitorFace() - Home + Vector3.back * 0.35f, 2.2f, new Color(1f, 0.12f, 0.06f), 3.5f);
                    }
                    Vector3 seat = room + new Vector3(0.25f, 1.15f, -0.35f);
                    Vector3 face = VisitorFace();
                    float whip = Mathf.Clamp01(g / 0.16f);
                    eye = seat + (face - seat) * 0.35f * whip + new Vector3(Mathf.PerlinNoise(t * 30f, 1f) - 0.5f, Mathf.PerlinNoise(2f, t * 30f) - 0.5f, 0f) * 0.06f;
                    look = Vector3.Lerp(tvGlass, face, 1f - (1f - whip) * (1f - whip));
                    fov = Mathf.Lerp(46f, 34f, whip);
                    tvLight.color = new Color(1f, 0.1f, 0.05f);
                    fade = g > 0.9f ? 1f : 0f;
                }
                else
                {
                    // 10. Black. The title, and who it learns from now.
                    float g = t - 56.6f;
                    eye = tvGlass + new Vector3(0f, 0f, -0.6f);
                    look = tvGlass;
                    fade = 1f;
                    Card(g, 0.6f, 3.6f, "NIGHT SHIFT", 130, ref card, ref cardAlpha, ref cardSize);
                    Card(g, 3.8f, 6.8f, "freddy_v8 is learning from you.", 40, ref card, ref cardAlpha, ref cardSize);
                    rain.volume = Mathf.MoveTowards(rain.volume, 0f, Time.deltaTime * 0.15f);
                }

                UpdateLights(t);
                Aim(player, eye, look, fov);
                director.UI.SetCinematic("", fade);
                director.UI.SetCinematicCard(card, cardAlpha, cardSize);
                yield return null;
            }
            Finish(player);
        }

        private void Finish(PlayerController player)
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            if (rain != null)
            {
                Destroy(rain.gameObject);
            }
            if (tvSound != null)
            {
                Destroy(tvSound.gameObject);
            }
            if (feedTexture != null)
            {
                feedTexture.Release();
                Destroy(feedTexture);
            }
            if (apartment != null)
            {
                Destroy(apartment);
            }
            director.UI.SetCinematicCard("", 0f);
            director.UI.SetCinematic("", 1f);
            director.UI.ShowCinematic(false);
            player.View.fieldOfView = 70f;
            Playing = false;
        }

        private void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
        }

        // ------------------------------------------------------------------ the escape

        /// <summary>
        /// Out of the building with the tape (about 15 s): he steps out of the doorway behind you
        /// under the lightning; you run for the car in the rain; in the headlights he is standing
        /// in front of it; the engine will not catch, twice, while he comes closer; the third time
        /// it does, and you reverse away from him into the night.
        /// </summary>
        private IEnumerator Escape(PlayerController player)
        {
            Exterior outside = director.Exterior;
            Car car = outside.Car;
            FreddyBody freddy = director.Freddy;
            outside.SetOutdoors(true, player.View);
            car.Place(Exterior.ParkingSpot, 0f);
            car.SetHeadlights(false);
            car.SetDoor(0f);
            director.Level.Exit.SetLift(1f);
            AudioSource rainSound = Source(director.Assets.RainLoop, true, 0.6f);
            AudioSource engine = Source(director.Assets.CarEngine, true, 0f);
            Vector3 door = PizzeriaLayout.ExitDoor;
            Vector3 start = Exterior.Entrance + new Vector3(0f, 0f, -1.2f);
            Vector3 carSide = Exterior.ParkingSpot + new Vector3(-1.35f, 0f, 0.3f);
            Vector3 inFront = Exterior.ParkingSpot + new Vector3(0f, 0f, 7.5f);
            freddy.ResetTo(door + new Vector3(0f, 0f, 0.6f), 180f);
            freddy.WakeUp();
            freddy.FlashEyes(30f);
            freddy.ScriptedWalk = 0f;
            float t = 0f;
            bool flash = false, slam = false, lights = false, lookedBack = false;
            int crank = 0;
            float nextStep = 0f, nextPant = 0f;
            while (!skip && t < 15.5f)
            {
                t += Time.deltaTime;
                Vector3 eye, look;
                float fov = 62f, fade = Mathf.Clamp01(1f - t / 0.4f);
                if (t < 2.4f)
                {
                    // 1. Over the shoulder: he steps out of the doorway into the rain.
                    float k = Mathf.Clamp01((t - 0.5f) / 1.6f);
                    freddy.transform.position = Vector3.Lerp(door + new Vector3(0f, 0f, 0.6f), door + new Vector3(0f, 0f, -1.4f), k);
                    freddy.ScriptedWalk = k > 0f && k < 1f ? FreddyBody.WalkSpeed : 0f;
                    if (!flash && t > 0.9f)
                    {
                        flash = true;
                        outside.Lightning();
                        director.Audio.Sting();
                        if (director.Assets.Thunder != null)
                        {
                            director.Audio.PlayAt(director.Assets.Thunder, start, 0.9f);
                        }
                    }
                    eye = start + new Vector3(0.5f, 1.6f, -2.2f) + Handheld(t, 0.03f);
                    look = freddy.EyePosition - new Vector3(0f, 0.2f, 0f);
                    fov = 48f;
                }
                else if (t < 5.6f)
                {
                    // 2. Run for the car: hand-held, breath, the splash of each step, a look back.
                    float k = Mathf.SmoothStep(0f, 1f, (t - 2.4f) / 3.2f);
                    Vector3 feet = Vector3.Lerp(start, carSide, k);
                    float bob = Mathf.Sin(t * 15f) * 0.05f;
                    eye = feet + new Vector3(0f, 1.55f + bob, 0f) + Handheld(t, 0.04f);
                    bool glance = t > 3.9f && t < 4.6f;
                    if (glance && !lookedBack)
                    {
                        // Lightning as you look back: there he is, walking after you.
                        lookedBack = true;
                        outside.Lightning();
                        director.Audio.Sting();
                    }
                    look = glance ? freddy.EyePosition : carSide + new Vector3(0.6f, 1.1f, 0f);
                    freddy.transform.position = Vector3.MoveTowards(freddy.transform.position, carSide, Time.deltaTime * FreddyBody.WalkSpeed);
                    freddy.transform.rotation = Quaternion.LookRotation(Flat(carSide - freddy.transform.position));
                    freddy.ScriptedWalk = FreddyBody.WalkSpeed;
                    fov = 66f;
                    if (t >= nextStep)
                    {
                        nextStep = t + 0.3f;
                        director.Audio.Footstep(0.9f);
                    }
                    if (t >= nextPant)
                    {
                        nextPant = t + 0.7f;
                        director.Audio.Pant(0.6f);
                    }
                }
                else
                {
                    // 3. In the driver's seat. The door slams; the headlights find him in front of the car.
                    if (!slam)
                    {
                        slam = true;
                        car.SetDoor(0f);
                        director.Audio.PlayAt(director.Assets.LockerDoor, carSide, 1f, 0.8f);
                        freddy.transform.SetPositionAndRotation(inFront, Quaternion.Euler(0f, 180f, 0f));
                        freddy.ScriptedWalk = 0f;
                    }
                    if (!lights && t > 6.4f)
                    {
                        lights = true;
                        car.SetHeadlights(true);
                        director.Audio.Click();
                        director.Audio.Sting();
                    }
                    // The key: two cranks that do not catch (he walks closer through each), then it starts.
                    float[] cranks = { 7.2f, 9.0f, 10.8f };
                    if (crank < cranks.Length && t > cranks[crank])
                    {
                        if (crank < 2)
                        {
                            director.Audio.PlayAt(GameAudio.Crank(), car.transform.position, 0.9f, 1f - crank * 0.08f);
                        }
                        else
                        {
                            director.Audio.PlayAt(GameAudio.Crank(), car.transform.position, 0.9f, 1.1f);
                            engine.volume = 0.5f;
                            engine.pitch = 1.25f;
                        }
                        crank++;
                    }
                    bool walking = (t > 7.4f && t < 8.6f) || (t > 9.2f && t < 10.4f);
                    if (walking)
                    {
                        freddy.transform.position += Vector3.back * Time.deltaTime * 1.4f;
                    }
                    freddy.ScriptedWalk = walking ? FreddyBody.WalkSpeed : 0f;
                    // Reverse away once it catches, turning out of the lot.
                    if (t > 11.6f)
                    {
                        float r = Mathf.Clamp01((t - 11.6f) / 3.4f);
                        float back = r * r * 11f;
                        float yaw = -55f * Mathf.SmoothStep(0f, 1f, r);
                        car.Place(Exterior.ParkingSpot + Quaternion.Euler(0f, yaw * 0.5f, 0f) * new Vector3(0f, 0f, -back), yaw);
                    }
                    Transform body = car.transform;
                    eye = body.TransformPoint(car.DriverEyes) + Handheld(t, t < 11.6f ? 0.004f : 0.012f);
                    look = Vector3.Lerp(body.TransformPoint(car.DriverEyes + new Vector3(0.3f, -0.05f, 4f)), freddy.EyePosition, t < 11.6f ? 0.85f : 0.6f);
                    fov = 58f;
                    if (t > 6.4f && t < 6.6f)
                    {
                        fade = 0f;
                    }
                    fade = Mathf.Max(fade, Mathf.Clamp01((t - 14.6f) / 0.9f));
                }
                Aim(player, eye, look, fov);
                director.UI.SetCinematic("", fade);
                director.UI.SetCinematicCard("", 0f);
                yield return null;
            }
            engine.Stop();
            rainSound.Stop();
            Destroy(engine.gameObject);
            Destroy(rainSound.gameObject);
            freddy.ScriptedWalk = -1f;
            car.SetHeadlights(false);
            car.Place(Exterior.ParkingSpot, 0f);
            director.Level.Exit.SetLift(0f);
            director.UI.SetCinematic("", 1f);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v : Vector3.forward;
        }

        // ------------------------------------------------------------------ the TV and the footage

        /// <summary>Films a shot of the tape: the second camera, from <paramref name="from"/> toward <paramref name="at"/>.</summary>
        private void Film(Vector3 from, Vector3 at, float fov, bool showLights)
        {
            feed.enabled = true;
            feed.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from));
            feed.fieldOfView = fov;
            footageLightsOn = showLights;
        }

        private bool footageLightsOn;

        private void SetScreen(Texture picture, Color tint, string overlay)
        {
            if (picture != feedTexture)
            {
                feed.enabled = false;
            }
            screen.SetTexture("_BaseMap", picture != null ? picture : Texture2D.blackTexture);
            screen.SetTextureScale("_BaseMap", Vector2.one);
            // Tracking: the picture jumps a little now and then, like a worn tape.
            float jump = picture == feedTexture && Random.value < 0.04f ? Random.Range(-0.04f, 0.04f) : 0f;
            screen.SetTextureOffset("_BaseMap", new Vector2(0f, jump));
            screen.SetColor("_BaseColor", tint * 1.25f);
            Texture2D text = overlay != null ? Sign(overlay) : null;
            osdRenderer.enabled = text != null;
            if (text != null)
            {
                osd.SetTexture("_BaseMap", text);
            }
            tvLight.color = picture == null ? Color.black : tint.grayscale > 0.9f && picture != feedTexture ? new Color(0.35f, 0.4f, 1f) : tint * 0.8f;
        }

        /// <summary>TV snow: the grain jumps every frame.</summary>
        private void Snow(float brightness)
        {
            feed.enabled = false;
            screen.SetTexture("_BaseMap", Sign("vhs_static"));
            screen.SetTextureScale("_BaseMap", new Vector2(0.5f, 0.5f));
            screen.SetTextureOffset("_BaseMap", new Vector2(Random.value * 0.5f, Random.value * 0.5f));
            screen.SetColor("_BaseColor", Color.white * brightness * 1.1f);
            osdRenderer.enabled = false;
            tvLight.color = new Color(0.7f, 0.75f, 0.8f) * brightness;
        }

        private void Shake(float amount)
        {
            screen.SetTextureOffset("_BaseMap", new Vector2(Random.Range(-amount, amount) * 0.3f, Random.Range(-amount, amount)));
        }

        private void UpdateLights(float t)
        {
            // The TV light breathes with the picture; lightning lights the window side of the room.
            tvLight.intensity = 0.8f + Mathf.PerlinNoise(t * 7f, 0.2f) * 0.4f;
            float flash = Time.time < flashUntil ? (Mathf.Repeat(Time.time * 9f, 1f) < 0.6f ? 1f : 0.2f) : 0f;
            moon.intensity = 1.2f + flash * 9f;
            if (sky != null)
            {
                sky.SetColor("_BaseColor", skyColor * (1f + flash * 4f));
            }
        }

        private void Flash(float seconds) => flashUntil = Time.time + seconds;

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            bool filming = camera == feed;
            // The footage has its own lights (the opening's stage lights, or the camera's lamp), and
            // the visitor in the doorway is only on the tape.
            foreach (Light light in footageLights)
            {
                light.enabled = filming && footageLightsOn;
            }
            footageLights[footageLights.Length - 1].enabled = filming && !footageLightsOn;
            foreach (Renderer renderer in visitorRenderers)
            {
                renderer.enabled = filming || visitorEverywhere;
            }
        }

        /// <summary>From the last moment on, the visitor is not only on the tape.</summary>
        private bool visitorEverywhere;

        /// <summary>Where the visitor's face is (his head bone, or about head height).</summary>
        private Vector3 VisitorFace()
        {
            if (visitor == null)
            {
                return Home + new Vector3(0.25f, 1.9f, -1.3f);
            }
            Animator animator = visitor.GetComponentInChildren<Animator>();
            Transform head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            return head != null ? head.position + visitor.transform.forward * 0.12f : visitor.transform.position + Vector3.up * 1.9f;
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == feed)
            {
                foreach (Light light in footageLights)
                {
                    light.enabled = false;
                }
                foreach (Renderer renderer in visitorRenderers)
                {
                    renderer.enabled = visitorEverywhere;
                }
            }
        }

        // ------------------------------------------------------------------ the apartment

        private void Build()
        {
            apartment = new GameObject("Apartment");
            apartment.transform.SetParent(transform, false);
            apartment.transform.position = Home;
            Transform root = apartment.transform;

            Material floor = BuildKit.Pbr("wood_planks", new Color(0.62f, 0.5f, 0.42f)) ?? BuildKit.Plain("aptFloor", new Color(0.25f, 0.18f, 0.12f), 0.3f);
            Material wall = BuildKit.Pbr("staff_wall", new Color(0.62f, 0.58f, 0.5f)) ?? BuildKit.Plain("aptWall", new Color(0.4f, 0.38f, 0.33f), 0.2f);
            Material ceiling = BuildKit.Pbr("concrete_wall", new Color(0.6f, 0.58f, 0.55f)) ?? wall;
            Material trim = BuildKit.Pbr("wood_planks", new Color(0.3f, 0.22f, 0.16f)) ?? floor;
            const float w = 2.2f, d = 1.8f, h = 2.6f;
            BuildKit.Box("Floor", root, new Vector3(0f, -0.05f, 0f), new Vector3(w * 2f, 0.1f, d * 2f), floor, 3f);
            BuildKit.Box("Ceiling", root, new Vector3(0f, h + 0.05f, 0f), new Vector3(w * 2f, 0.1f, d * 2f), ceiling, 2f);
            BuildKit.Box("WallNorth", root, new Vector3(0f, h * 0.5f, d + 0.05f), new Vector3(w * 2f, h, 0.1f), wall, 2f);
            BuildKit.Box("WallWest", root, new Vector3(-w - 0.05f, h * 0.5f, 0f), new Vector3(0.1f, h, d * 2f), wall, 2f);
            // South wall with the doorway to the dark hall.
            BuildKit.Box("WallSouthA", root, new Vector3(-0.6f, h * 0.5f, -d - 0.05f), new Vector3(3.2f, h, 0.1f), wall, 2f);
            BuildKit.Box("WallSouthB", root, new Vector3(2.05f, h * 0.5f, -d - 0.05f), new Vector3(0.3f, h, 0.1f), wall, 2f);
            BuildKit.Box("WallSouthTop", root, new Vector3(1.45f, 2.35f, -d - 0.05f), new Vector3(0.9f, 0.5f, 0.1f), wall, 2f);
            BuildKit.Box("Hall", root, new Vector3(1.45f, h * 0.5f, -d - 1.6f), new Vector3(1.4f, h, 0.1f), wall, 2f);
            BuildKit.Box("HallSideA", root, new Vector3(0.75f, h * 0.5f, -d - 0.8f), new Vector3(0.1f, h, 1.6f), wall, 2f);
            BuildKit.Box("HallSideB", root, new Vector3(2.15f, h * 0.5f, -d - 0.8f), new Vector3(0.1f, h, 1.6f), wall, 2f);
            BuildKit.Box("HallFloor", root, new Vector3(1.45f, -0.05f, -d - 0.8f), new Vector3(1.4f, 0.1f, 1.6f), floor, 2f);
            BuildKit.Box("HallCeiling", root, new Vector3(1.45f, h + 0.05f, -d - 0.8f), new Vector3(1.4f, 0.1f, 1.6f), ceiling, 2f);
            // East wall with a window onto the rain.
            BuildKit.Box("WallEastA", root, new Vector3(w + 0.05f, h * 0.5f, -1.2f), new Vector3(0.1f, h, 1.2f), wall, 2f);
            BuildKit.Box("WallEastB", root, new Vector3(w + 0.05f, h * 0.5f, 1.2f), new Vector3(0.1f, h, 1.2f), wall, 2f);
            BuildKit.Box("WallEastLow", root, new Vector3(w + 0.05f, 0.45f, 0f), new Vector3(0.1f, 0.9f, 1.2f), wall, 2f);
            BuildKit.Box("WallEastHigh", root, new Vector3(w + 0.05f, 2.35f, 0f), new Vector3(0.1f, 0.5f, 1.2f), wall, 2f);
            BuildKit.Box("Sill", root, new Vector3(w - 0.02f, 0.92f, 0f), new Vector3(0.18f, 0.04f, 1.3f), trim, 1f, false);
            BuildKit.Box("Mullion", root, new Vector3(w + 0.02f, 1.5f, 0f), new Vector3(0.04f, 1.2f, 0.04f), trim, 1f, false);
            BuildKit.Box("Transom", root, new Vector3(w + 0.02f, 1.5f, 0f), new Vector3(0.04f, 0.04f, 1.2f), trim, 1f, false);
            Material glass = BuildKit.Decal("windowGlass", Texture2D.whiteTexture, new Color(0.55f, 0.62f, 0.7f, 0.12f));
            BuildKit.Quad("Glass", root, new Vector3(w + 0.03f, 1.5f, 0f), new Vector2(1.2f, 1.2f), 270f, glass);
            Texture2D rainTexture = Sign("glass_rain");
            if (rainTexture != null)
            {
                Material water = new Material(BuildKit.Decal("glassRainHome", rainTexture, new Color(1f, 1f, 1f, 0.75f)));
                BuildKit.Quad("Rain", root, new Vector3(w + 0.02f, 1.5f, 0f), new Vector2(1.2f, 1.2f), 270f, water);
                apartment.AddComponent<RunningWater>().Set(water);
            }
            Texture2D skyTexture = Sign("sky_night");
            if (skyTexture != null)
            {
                skyColor = new Color(1.6f, 1.6f, 1.75f);
                sky = BuildKit.Unlit(skyTexture, skyColor);
                BuildKit.Quad("Sky", root, new Vector3(w + 6f, 2f, 0f), new Vector2(16f, 8f), 270f, sky);
            }
            // Baseboards.
            BuildKit.Box("Baseboard", root, new Vector3(0f, 0.05f, d - 0.01f), new Vector3(w * 2f, 0.1f, 0.03f), trim, 1f, false);
            BuildKit.Box("Baseboard", root, new Vector3(-w + 0.01f, 0.05f, 0f), new Vector3(0.03f, 0.1f, d * 2f), trim, 1f, false);

            // The TV stand: an old chest of drawers; the VCR on it, the TV on the VCR.
            float top = 0.75f;
            GameObject chest = BuildKit.Prop("vintage_wooden_drawer_01", root, new Vector3(0f, 0f, d - 0.35f), 180f);
            if (chest != null)
            {
                top = BoundsOf(chest).max.y - Home.y;
            }
            else
            {
                BuildKit.Box("Stand", root, new Vector3(0f, top * 0.5f, d - 0.35f), new Vector3(1f, top, 0.5f), trim);
            }
            BuildVcr(root, new Vector3(0f, top, d - 0.42f));
            GameObject tvScreen;
            LevelProps.TvSet(root, new Vector3(0f, top + 0.09f, d - 0.32f), 180f, out tvScreen);
            screen = new Material(BuildKit.Unlit(Texture2D.blackTexture, Color.white));
            if (tvScreen != null)
            {
                tvScreen.GetComponent<Renderer>().sharedMaterial = screen;
                tvGlass = tvScreen.transform.position;
                Transform glassFrame = tvScreen.transform;
                osdRenderer = Overlay(glassFrame, -0.002f, out osd);
                Texture2D lines = Sign("glass_vhs_scanlines");
                if (lines != null)
                {
                    GameObject scan = BuildKit.Quad("Scanlines", glassFrame, new Vector3(0f, 0f, -0.003f), Vector2.one, 0f, new Material(BuildKit.Stain("vhsScan", lines, Color.white)));
                    scan.transform.localRotation = Quaternion.identity;
                }
            }
            else
            {
                tvGlass = root.TransformPoint(new Vector3(0f, top + 0.35f, d - 0.6f));
                osdRenderer = Overlay(root, 0f, out osd);
            }

            // A chair facing the TV, boxes not yet unpacked, a pizza box on the floor.
            BuildKit.Prop("plastic_monobloc_chair_01", root, new Vector3(0.25f, 0f, -0.35f), 180f, 1f, false);
            BuildKit.Prop("cardboard_box_01", root, new Vector3(-1.75f, 0f, 1.3f), 12f, 1f, false);
            BuildKit.Prop("cardboard_box_01", root, new Vector3(-1.7f, 0.42f, 1.25f), -20f, 0.8f, false);
            BuildKit.Prop("metal_trash_can", root, new Vector3(-1.9f, 0f, -1.45f), 0f, 1f, false);
            BuildKit.Prop("wall_clock", root, new Vector3(-w + 0.02f, 1.9f, 0.4f), 90f, 1f, false);
            Texture2D pizza = Sign("pizza_box_top");
            Material cardboard = BuildKit.Plain("cardboard", new Color(0.55f, 0.42f, 0.28f), 0.15f);
            GameObject box = BuildKit.Box("PizzaBox", root, new Vector3(0.9f, 0.025f, -0.6f), new Vector3(0.42f, 0.05f, 0.42f), cardboard, 1f, false);
            box.transform.localRotation = Quaternion.Euler(0f, 23f, 0f);
            if (pizza != null)
            {
                BuildKit.Quad("PizzaTop", box.transform, new Vector3(0f, 0.51f, 0f), Vector2.one, 0f, BuildKit.Picture("pizzaTop", pizza)).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            // Lights: the TV's glow, a dim lamp in the corner, the night through the window.
            tvLight = PointLight(root, tvGlass - Home + new Vector3(0f, 0.1f, -1.1f), 4.5f, Color.black, 0.9f);
            lamp = PointLight(root, new Vector3(-1.85f, 1.3f, -1.35f), 3.2f, new Color(1f, 0.72f, 0.45f), 0.45f);
            moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(root, false);
            moon.transform.localPosition = new Vector3(w + 1.5f, 2.4f, 0f);
            moon.transform.localRotation = Quaternion.Euler(30f, 270f, 0f);
            moon.type = LightType.Spot;
            moon.spotAngle = 70f;
            moon.range = 8f;
            moon.color = new Color(0.55f, 0.65f, 1f);
            moon.shadows = LightShadows.Soft;

            // The tape, as it is carried to the VCR.
            tape = new GameObject("Tape").transform;
            tape.SetParent(root, false);
            Material plastic = BuildKit.Plain("vhsPlastic", new Color(0.05f, 0.05f, 0.05f), 0.55f);
            BuildKit.Box("Body", tape, Vector3.zero, new Vector3(0.187f, 0.025f, 0.103f), plastic, 1f, false);
            BuildKit.Box("Window", tape, new Vector3(0f, 0.0128f, -0.012f), new Vector3(0.1f, 0.001f, 0.035f), BuildKit.Plain("vhsWindow", new Color(0.12f, 0.1f, 0.09f), 0.9f), 1f, false);
            BuildKit.Quad("Label", tape, new Vector3(0f, 0.0131f, 0.03f), new Vector2(0.15f, 0.05f), 0f, TaskKit.Label("label_tape", new Color(0.9f, 0.85f, 0.6f)))
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // The second camera, that films the footage.
            feedTexture = new RenderTexture(FeedWidth, FeedHeight, 16, RenderTextureFormat.ARGB32) { name = "Tape", filterMode = FilterMode.Point };
            feed = new GameObject("TapeCamera").AddComponent<Camera>();
            feed.transform.SetParent(transform, false);
            feed.targetTexture = feedTexture;
            feed.nearClipPlane = 0.1f;
            feed.farClipPlane = 30f;
            feed.depth = -10f;
            feed.clearFlags = CameraClearFlags.SolidColor;
            feed.backgroundColor = Color.black;
            feed.allowMSAA = false;
            feed.enabled = false;
            UniversalAdditionalCameraData data = feed.GetUniversalAdditionalCameraData();
            data.renderShadows = true;
            data.renderPostProcessing = false;

            // The grand opening's lights over the stage, and the CCTV camera's own lamp (last).
            footageLights = new[]
            {
                SpotLight(new Vector3(7.5f, 4.2f, 6.5f), new Vector3(7.5f, 0.7f, 10.8f), 50f, 14f, new Color(1f, 0.85f, 0.65f), 14f),
                PointLight(transform, new Vector3(7f, 3.4f, 4.5f), 13f, new Color(1f, 0.9f, 0.75f), 4f),
                PointLight(transform, new Vector3(4.5f, 3f, 9f), 9f, new Color(1f, 0.55f, 0.8f), 2.5f),
                CameraLamp()
            };
            foreach (Light light in footageLights)
            {
                light.enabled = false;
            }

            // Who stands in the doorway on the tape (and only on the tape).
            visitor = director.Assets.FreddyPrefab != null ? Instantiate(director.Assets.FreddyPrefab, root) : null;
            if (visitor != null)
            {
                visitor.transform.localPosition = new Vector3(1.45f, 0f, -d - 0.75f);
                visitor.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                visitorRenderers = visitor.GetComponentsInChildren<Renderer>();
                foreach (Behaviour behaviour in visitor.GetComponentsInChildren<MonoBehaviour>())
                {
                    behaviour.enabled = false;
                }
                foreach (Renderer renderer in visitorRenderers)
                {
                    renderer.enabled = false;
                }
            }
            else
            {
                visitorRenderers = new Renderer[0];
            }
        }

        private void BuildVcr(Transform root, Vector3 bottomFront)
        {
            Material body = BuildKit.Plain("vcrBody", new Color(0.07f, 0.07f, 0.075f), 0.45f, 0.4f);
            Vector3 center = bottomFront + new Vector3(0f, 0.045f, 0.15f);
            BuildKit.Box("Vcr", root, center, new Vector3(0.46f, 0.09f, 0.3f), body, 1f, false);
            for (int i = 0; i < 4; i++)
            {
                BuildKit.Box("Foot", root, center + new Vector3(i % 2 == 0 ? -0.2f : 0.2f, -0.047f, i < 2 ? -0.12f : 0.12f), new Vector3(0.03f, 0.006f, 0.03f), body, 1f, false);
            }
            Vector3 face = bottomFront + new Vector3(0f, 0.045f, -0.001f);
            Texture2D faceTexture = Sign("vcr_front");
            if (faceTexture != null)
            {
                BuildKit.Quad("VcrFace", root, face, new Vector2(0.46f, 0.09f), 180f, BuildKit.Picture("vcrFace", faceTexture));
            }
            // The slot on the viewer's left, the clock window on the right (as drawn on the face).
            vcrFront = root.TransformPoint(face + new Vector3(-0.093f, 0.0036f, 0f));
            // The cassette door, hinged at its top, swings in as the tape pushes it.
            flap = new GameObject("FlapHinge").transform;
            flap.SetParent(root, false);
            flap.localPosition = face + new Vector3(-0.093f, 0.0036f + 0.019f, -0.002f);
            BuildKit.Box("Flap", flap, new Vector3(0f, -0.019f, 0f), new Vector3(0.19f, 0.036f, 0.003f), BuildKit.Plain("vcrFlap", new Color(0.03f, 0.03f, 0.035f), 0.5f), 1f, false);
            clock = BuildKit.Quad("Clock", root, face + new Vector3(0.089f, 0.0103f, -0.0015f), new Vector2(0.1f, 0.026f), 180f, TaskKit.Label("lcd_vcr_clock", Color.black, 1.4f)).GetComponent<Renderer>();
        }

        /// <summary>The tape in the guard's hand: up from below the view, lined up with the slot, pushed in.</summary>
        private void PlaceTape(float g)
        {
            // Flat, label up, its long side across the slot: the way a VHS tape goes in.
            Quaternion held = Quaternion.Euler(-35f, 20f, 12f);
            Vector3 low = vcrFront + new Vector3(0.16f, -0.2f, -0.3f);
            Vector3 lined = vcrFront + new Vector3(0f, 0f, -0.1f);
            Vector3 inside = vcrFront + new Vector3(0f, 0f, 0.07f);
            if (g < 1.4f)
            {
                tape.gameObject.SetActive(g > 0.6f);
                tape.SetPositionAndRotation(low, held);
            }
            else if (g < 2.7f)
            {
                float k = Mathf.SmoothStep(0f, 1f, (g - 1.4f) / 1.3f);
                tape.SetPositionAndRotation(Vector3.Lerp(low, lined, k) + Handheld(g, 0.003f * (1f - k)), Quaternion.Slerp(held, Quaternion.identity, k));
            }
            else
            {
                float k = Mathf.SmoothStep(0f, 1f, (g - 2.7f) / 0.8f);
                tape.SetPositionAndRotation(Vector3.Lerp(lined, inside, k), Quaternion.identity);
            }
        }

        // ------------------------------------------------------------------ helpers

        private static void Aim(PlayerController player, Vector3 eye, Vector3 target, float fov)
        {
            player.Head.position = eye;
            player.Head.rotation = Quaternion.LookRotation(target - eye);
            player.View.fieldOfView = fov;
        }

        /// <summary>A hand-held wobble: slow drift and small shakes, never perfectly still.</summary>
        private static Vector3 Handheld(float t, float amount)
        {
            return new Vector3(Mathf.PerlinNoise(t * 0.7f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.1f, t * 0.8f) - 0.5f, Mathf.PerlinNoise(t * 0.5f, 4.2f) - 0.5f) * amount * 2f
                   + new Vector3(Mathf.PerlinNoise(t * 6f, 0.4f) - 0.5f, Mathf.PerlinNoise(0.9f, t * 6f) - 0.5f, 0f) * amount * 0.6f;
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

        private static Texture2D Sign(string name) => BuildKit.SignTexture(name);

        private static Bounds BoundsOf(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            return bounds;
        }

        private static Renderer Overlay(Transform parent, float z, out Material material)
        {
            material = new Material(BuildKit.Stain("vhsOsd", Texture2D.blackTexture, Color.white));
            GameObject quad = BuildKit.Quad("TapeText", parent, new Vector3(0f, 0f, z), Vector2.one, 0f, material);
            quad.transform.localRotation = Quaternion.identity;
            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.enabled = false;
            return renderer;
        }

        private static Light PointLight(Transform parent, Vector3 position, float range, Color color, float intensity)
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.range = range;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        private Light SpotLight(Vector3 position, Vector3 target, float angle, float range, Color color, float intensity)
        {
            var light = new GameObject("StageLight").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.position = position;
            light.transform.rotation = Quaternion.LookRotation(target - position);
            light.type = LightType.Spot;
            light.spotAngle = angle;
            light.range = range;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            return light;
        }

        /// <summary>The CCTV camera's infrared lamp, riding on the tape camera.</summary>
        private Light CameraLamp()
        {
            var light = new GameObject("CameraLamp").AddComponent<Light>();
            light.transform.SetParent(feed.transform, false);
            light.type = LightType.Spot;
            light.spotAngle = 100f;
            light.range = 18f;
            light.intensity = 5f;
            light.color = new Color(0.85f, 1f, 0.9f);
            light.shadows = LightShadows.None;
            return light;
        }

        private AudioSource Source(AudioClip clip, bool loop, float volume)
        {
            var source = new GameObject("EndingSound").AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.clip = clip;
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f;
            source.mute = !SoundPrefs.EffectsOn;
            if (clip != null)
            {
                source.Play();
            }
            return source;
        }

        /// <summary>The hiss of a TV between channels, two seconds that loop.</summary>
        private static AudioClip Snow()
        {
            const int rate = 22050;
            var data = new float[rate * 2];
            var random = new System.Random(9);
            float previous = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float white = (float)random.NextDouble() * 2f - 1f;
                previous = Mathf.Lerp(white, previous, 0.35f);
                data[i] = previous * 0.5f;
            }
            AudioClip clip = AudioClip.Create("TvSnow", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
