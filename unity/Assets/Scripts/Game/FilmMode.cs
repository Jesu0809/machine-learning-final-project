using System.Collections;
using System.IO;
using NightShift.Hunt;
using Unity.Collections;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Records a tour of the game to a folder (numbered pictures and the sound, joined by
    /// tools/make_film.py), with nothing coming out of the speakers: Unity renders the sound offline.
    /// Usage: -capture folder -film. The -mute flag alone just plays the game in silence.
    /// </summary>
    public class FilmMode : MonoBehaviour
    {
        private const int FramesPerSecond = 30;

        private string folder;
        private int frame;
        private BinaryWriter sound;
        private static bool rendering;

        /// <summary>First thing in the game: nothing is ever heard in these runs.</summary>
        public static void SilenceIfRequested()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            bool film = System.Array.IndexOf(args, "-film") >= 0;
            if (film)
            {
                rendering = AudioRenderer.Start();
            }
            if (!rendering && (film || System.Array.IndexOf(args, "-mute") >= 0))
            {
                AudioListener.volume = 0f;
            }
        }

        public static FilmMode Begin(GameObject owner, string folder)
        {
            var film = owner.AddComponent<FilmMode>();
            film.folder = folder;
            Directory.CreateDirectory(Path.Combine(folder, "frames"));
            film.sound = new BinaryWriter(File.Create(Path.Combine(folder, "sound.raw")));
            Time.captureFramerate = FramesPerSecond;
            film.StartCoroutine(film.Record());
            return film;
        }

        private IEnumerator Record()
        {
            int channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            File.WriteAllText(Path.Combine(folder, "format.txt"), AudioSettings.outputSampleRate + " " + channels + " " + rendering);
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                Texture2D picture = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, "frames", frame.ToString("000000") + ".jpg"), picture.EncodeToJPG(82));
                Destroy(picture);
                frame++;
                if (rendering)
                {
                    int samples = AudioRenderer.GetSampleCountForCaptureFrame() * channels;
                    if (samples > 0)
                    {
                        var buffer = new NativeArray<float>(samples, Allocator.Temp);
                        AudioRenderer.Render(buffer);
                        foreach (float value in buffer)
                        {
                            sound.Write(value);
                        }
                        buffer.Dispose();
                    }
                }
            }
        }

        public void Stop()
        {
            sound.Flush();
            sound.Close();
            if (rendering)
            {
                AudioRenderer.Stop();
            }
            Debug.Log("FILM wrote " + frame + " frames to " + folder);
        }

        /// <summary>The tour: the home, the intro, his first speech, every room, his pranks, a catch and the ending.</summary>
        /// <summary>
        /// Every move he has, in the ring, with the camera three meters away and following him.
        /// </summary>
        private static IEnumerator HisMoves(HuntDirector director)
        {
            FreddyBody freddy = director.Freddy;
            PlayerController player = director.Player;
            Animator animator = freddy.GetComponentInChildren<Animator>();
            Rect hall = PizzeriaLayout.Area(Room.DiningHall);
            float z = hall.yMin + 3.6f;
            var left = new Vector3(hall.xMin + 2f, 0f, z);
            var right = new Vector3(hall.xMax - 2f, 0f, z);
            var middle = new Vector3(hall.center.x, 0f, z);
            player.Head.position = new Vector3(hall.center.x, 1.5f, hall.yMin + 0.6f);
            player.Flashlight.enabled = true;

            void Look()
            {
                Vector3 at = freddy.transform.position + Vector3.up * 1.15f;
                player.Head.rotation = Quaternion.Slerp(player.Head.rotation, Quaternion.LookRotation(at - player.Head.position), 1f - Mathf.Exp(-Time.deltaTime * 6f));
            }
            void Face(Vector3 direction) => freddy.transform.rotation = Quaternion.LookRotation(direction);
            IEnumerator Hold(float seconds)
            {
                freddy.ScriptedWalk = 0f;
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    Look();
                    yield return null;
                }
            }
            IEnumerator Go(Vector3 from, Vector3 to, float speed)
            {
                freddy.transform.position = from;
                Face(to - from);
                freddy.ScriptedWalk = speed;
                float seconds = Vector3.Distance(from, to) / speed;
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    freddy.transform.position = Vector3.Lerp(from, to, t / seconds);
                    Look();
                    yield return null;
                }
                freddy.transform.position = to;
            }

            freddy.transform.position = middle;
            Face(Vector3.back);
            director.UI.Toast("Standing");
            yield return Hold(4f);
            director.UI.Toast("Listening");
            animator.SetBool("Listening", true);
            yield return Hold(5f);
            animator.SetBool("Listening", false);
            director.UI.Toast("Walking");
            yield return Go(left, right, FreddyBody.WalkSpeed);
            yield return Go(right, middle, FreddyBody.WalkSpeed);
            director.UI.Toast("Hurrying");
            yield return Go(middle, left, FreddyBody.WalkSpeed * 1.35f);
            director.UI.Toast("The roar, and the chase");
            animator.SetTrigger("Roar");
            yield return Go(left, right, FreddyBody.ChaseSpeed);
            director.UI.Toast("Laughing, walking");
            animator.SetTrigger("Laugh");
            yield return Go(right, middle, FreddyBody.WalkSpeed);
            Face(Vector3.back);
            director.UI.Toast("Breaking a door");
            for (int blow = 0; blow < 3; blow++)
            {
                animator.SetTrigger("Smash");
                yield return Hold(Door.BreakSeconds / 3f);
            }
            yield return Hold(0.8f);
            director.UI.Toast("Looking inside a locker");
            animator.SetBool("Searching", true);
            yield return Hold(4f);
            animator.SetBool("Searching", false);
            yield return Hold(1f);
            freddy.ScriptedWalk = -1f;
        }

        public static IEnumerator Tour(HuntDirector director, string folder)
        {
            FilmMode film = Begin(director.gameObject, folder);
            string[] args = System.Environment.GetCommandLineArgs();
            bool quick = System.Array.IndexOf(args, "-quick") >= 0;

            // The home.
            yield return new WaitForSeconds(quick ? 3f : 12f);
            director.UI.ShowModesForFilm();
            yield return new WaitForSeconds(4f);
            director.UI.ShowMenu();
            yield return new WaitForSeconds(2f);

            if (!quick)
            {
                // The whole intro, up to the first night.
                director.PlayStory();
                yield return null;
                while (director.InIntro)
                {
                    yield return null;
                }
                // His first speech, for a while.
                yield return new WaitForSeconds(48f);
            }

            // Every room with him asleep.
            director.BeginExplore();
            director.HuntHeld = true;
            PlayerController player = director.Player;
            player.Controllable = false;
            player.CameraLocked = true;
            player.Flashlight.enabled = true;
            for (int i = 0; i < HuntMap.RoomCount; i++)
            {
                Rect area = PizzeriaLayout.Area((Room)i);
                director.UI.Toast(HuntMap.DisplayName((Room)i));
                Vector3 middle = new Vector3(area.center.x, 1.65f, area.center.y);
                Vector3 corner = new Vector3(area.xMin + 1f, 1.65f, area.yMin + 1f);
                Vector3 from = Vector3.Lerp(middle, corner, 0.6f);
                Vector3 to = Vector3.Lerp(middle, corner, 0.2f);
                Vector3 look = new Vector3(area.xMax - 0.5f, 1.3f, area.yMax - 0.5f);
                float seconds = quick ? 1.2f : 5f;
                for (float t = 0f; t < seconds; t += Time.deltaTime)
                {
                    float k = t / seconds;
                    player.Head.position = Vector3.Lerp(from, to, k);
                    player.Head.rotation = Quaternion.LookRotation(look + new Vector3(Mathf.Sin(k * 3f), 0f, 0f) - player.Head.position);
                    yield return null;
                }
            }

            // His pranks and his taunts, in front of the camera in the ring.
            Rect hall = PizzeriaLayout.Area(Room.DiningHall);
            var stand = new Vector3(hall.center.x, 0f, hall.yMin + 2f);
            player.transform.position = stand + Vector3.right * 4f;
            player.Head.position = stand + Vector3.up * 1.65f;
            player.Head.rotation = Quaternion.LookRotation(Vector3.forward + Vector3.down * 0.3f);
            SurpriseBox.Create(director, director.Level.Root, stand + Vector3.forward * 2.2f);
            yield return new WaitForSeconds(1.5f);
            player.transform.position = stand + Vector3.forward * 0.5f;
            player.Head.position = stand + Vector3.up * 1.65f;
            player.Head.rotation = Quaternion.LookRotation(Vector3.forward + Vector3.down * 0.3f);
            ClownPatter patter = director.GetComponent<ClownPatter>();
            foreach (string key in new[] { "laugh", "seen", "habit_locker", "caught_5", "hiding", "sprint", "laugh" })
            {
                float wait = 0f;
                while (!patter.SayForFilm(key) && wait < 8f)
                {
                    wait += Time.deltaTime;
                    yield return null;
                }
                yield return new WaitForSeconds(quick ? 0.5f : 6f);
            }

            yield return HisMoves(director);
            director.HuntHeld = false;

            if (!quick)
            {
                // A caught night, then the ending from the last night.
                director.StartNight(7);
                director.SkipCall();
                director.Player.Controllable = false;
                yield return new WaitForSeconds(3f);
                director.CatchForFilm();
                yield return new WaitForSeconds(8f);
                director.StartNight(7);
                director.SkipCall();
                yield return new WaitForSeconds(1f);
                director.EscapeForTest();
                yield return null;
                float limit = 80f;
                for (float t = 0f; t < limit && director.Ending.Playing; t += Time.deltaTime)
                {
                    yield return null;
                }
            }

            film.Stop();
            Application.Quit();
        }
    }
}
