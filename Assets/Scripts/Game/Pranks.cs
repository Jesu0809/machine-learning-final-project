using System.Collections.Generic;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// A rubber toy on the floor. Stepping on it at a walk or a run squeaks, a noise in that room like
    /// any other; crouching keeps it quiet. The brain hears it through the same rule as everything else.
    /// </summary>
    public class SqueakToy : MonoBehaviour
    {
        private HuntDirector director;
        private float readyAt;

        public static SqueakToy Create(HuntDirector director, Transform parent, Vector3 floor)
        {
            var go = new GameObject("RubberToy");
            go.transform.SetParent(parent, false);
            go.transform.position = floor;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Material yellow = BuildKit.Plain("rubberToy", new Color(0.95f, 0.72f, 0.1f), 0.6f);
            BuildKit.Sphere("Body", go.transform, new Vector3(0f, 0.055f, 0f), 0.13f, yellow);
            BuildKit.Sphere("Head", go.transform, new Vector3(0f, 0.14f, 0.05f), 0.085f, yellow);
            BuildKit.Sphere("Nose", go.transform, new Vector3(0f, 0.14f, 0.095f), 0.035f, BuildKit.Plain("rubberNose", new Color(0.85f, 0.12f, 0.08f), 0.6f));
            var toy = go.AddComponent<SqueakToy>();
            toy.director = director;
            return toy;
        }

        private void Update()
        {
            PlayerController player = director.Player;
            if (director.Phase != GamePhase.Playing || Time.time < readyAt || !player.Moving || player.Crouching || player.Crawling)
            {
                return;
            }
            Vector3 gap = player.transform.position - transform.position;
            gap.y = 0f;
            if (gap.sqrMagnitude < 0.45f * 0.45f)
            {
                readyAt = Time.time + 2f;
                director.OnToySqueaked(transform.position);
            }
        }
    }

    /// <summary>
    /// A wrapped box with a crank. Passing within two meters, standing up, it springs open: a clown's
    /// head on a spring and a very loud noise. Crouching past it is safe, and it only fires once a night.
    /// </summary>
    public class SurpriseBox : MonoBehaviour
    {
        private HuntDirector director;
        private Transform head;
        private Transform lid;
        private float popped = -1f;

        public static SurpriseBox Create(HuntDirector director, Transform parent, Vector3 floor)
        {
            var go = new GameObject("SurpriseBox");
            go.transform.SetParent(parent, false);
            go.transform.position = floor;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Transform t = go.transform;
            Material red = BuildKit.Plain("boxRed", new Color(0.6f, 0.06f, 0.08f), 0.35f);
            Material gold = BuildKit.Plain("boxGold", new Color(0.8f, 0.6f, 0.15f), 0.5f, 0.7f);
            BuildKit.Box("Box", t, new Vector3(0f, 0.2f, 0f), new Vector3(0.4f, 0.4f, 0.4f), red, 1f, false);
            BuildKit.Box("Ribbon", t, new Vector3(0f, 0.2f, 0f), new Vector3(0.41f, 0.41f, 0.06f), gold, 1f, false);
            BuildKit.Cylinder("Crank", t, new Vector3(0.25f, 0.24f, 0f), 0.03f, 0.1f, gold).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            BuildKit.Sphere("Knob", t, new Vector3(0.31f, 0.3f, 0f), 0.05f, gold);
            var box = go.AddComponent<SurpriseBox>();
            box.director = director;
            box.lid = BuildKit.Box("Lid", t, new Vector3(0f, 0.41f, 0f), new Vector3(0.42f, 0.03f, 0.42f), red, 1f, false).transform;
            box.head = CircusDressing.ClownHead(t, new Vector3(0f, 0.15f, 0f), 0.4f);
            return box;
        }

        private void Update()
        {
            if (popped >= 0f)
            {
                float k = (Time.time - popped) / 0.35f;
                float spring = k < 1f ? 1f - (1f - k) * (1f - k) : 1f + Mathf.Sin((k - 1f) * 18f) * Mathf.Exp(-(k - 1f) * 2.2f) * 0.18f;
                head.localPosition = new Vector3(0f, 0.15f + 0.65f * spring, 0f);
                lid.localPosition = new Vector3(0f, 0.41f, -0.21f);
                lid.localRotation = Quaternion.Euler(-110f * Mathf.Clamp01(k), 0f, 0f);
                return;
            }
            PlayerController player = director.Player;
            if (director.Phase != GamePhase.Playing || director.InCall || player.Crouching || player.HiddenIn != null || player.Crawling)
            {
                return;
            }
            if ((player.transform.position - transform.position).sqrMagnitude < 2f * 2f)
            {
                popped = Time.time;
                director.OnSurprisePopped(transform.position);
            }
        }
    }

    /// <summary>A dummy in clown clothes: it turns to face the player only while the player is not looking.</summary>
    public class ClownDummy : MonoBehaviour
    {
        public static readonly List<ClownDummy> All = new List<ClownDummy>();

        private Transform player;
        private Camera view;
        private Quaternion resting;

        /// <summary>False until the night's rules wake them.</summary>
        public static bool Awake { get; set; }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        private void Start() => resting = transform.rotation;

        public static void Reset()
        {
            foreach (ClownDummy dummy in All)
            {
                dummy.transform.rotation = dummy.resting;
            }
        }

        private void Update()
        {
            if (!Awake)
            {
                return;
            }
            if (view == null)
            {
                HuntDirector director = FindAnyObjectByType<HuntDirector>();
                if (director == null || director.Player == null)
                {
                    return;
                }
                view = director.Player.View;
                player = director.Player.transform;
            }
            Vector3 toPlayer = player.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 14f * 14f || toPlayer.sqrMagnitude < 0.01f)
            {
                return;
            }
            Vector3 onScreen = view.WorldToViewportPoint(transform.position + Vector3.up);
            bool looked = onScreen.z > 0f && onScreen.x > -0.15f && onScreen.x < 1.15f && onScreen.y > -0.15f && onScreen.y < 1.15f;
            if (!looked)
            {
                transform.rotation = Quaternion.LookRotation(toPlayer);
            }
        }
    }

    /// <summary>
    /// What Pockets says during the night. Only ever talk, and only every so often: it never tells
    /// where he is and his brain knows nothing about it. The habit lines come from the player's own
    /// history in the save, so they follow the player across devices.
    /// </summary>
    public class ClownPatter : MonoBehaviour
    {
        /// <summary>Seconds of quiet between two taunts: he must be heard coming.</summary>
        private const float MinimumGap = 35f;

        private const int MaxPerNight = 8;

        private HuntDirector director;
        private PublicAddress speakers;
        private float nextAt;
        private int said;
        private bool opened;
        private float hiddenSince;
        private float runningSince;
        private HidingSpot lastSpot;
        private float musicBoxAt;

        public static ClownPatter Create(HuntDirector director, PublicAddress speakers)
        {
            var patter = director.gameObject.AddComponent<ClownPatter>();
            patter.director = director;
            patter.speakers = speakers;
            return patter;
        }

        public void BeginNight()
        {
            nextAt = Time.time + 15f;
            said = 0;
            opened = false;
            lastSpot = null;
            musicBoxAt = Time.time + Random.Range(90f, 240f);
        }

        /// <summary>Once a night a music box starts playing in some other room, and nobody wound it.</summary>
        private void MusicBox()
        {
            musicBoxAt = float.MaxValue;
            AudioClip clip = director.Assets.MusicBox;
            if (clip == null)
            {
                return;
            }
            Vector3 player = director.Player.transform.position;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                if (LevelProps.OpenFloorPoint((Room)Random.Range(0, HuntMap.RoomCount), out Vector3 floor) && Vector3.Distance(floor, player) > 9f)
                {
                    director.Audio.PlayAt(clip, floor + Vector3.up * 0.4f, 0.75f, Random.Range(0.86f, 1f));
                    return;
                }
            }
        }

        /// <summary>Says a line of this kind if it has been quiet long enough (or at once if forced).</summary>
        public void Say(string key, bool force = false)
        {
            if (director.Phase != GamePhase.Playing || director.InCall || said >= MaxPerNight || (!force && Time.time < nextAt))
            {
                return;
            }
            int index = System.Array.FindIndex(StoryData.Taunts, taunt => taunt.key == key);
            if (index < 0 || director.Assets.Taunts == null || index >= director.Assets.Taunts.Length)
            {
                return;
            }
            AudioClip[] clips = director.Assets.Taunts[index].Lines;
            string[] lines = StoryData.Taunts[index].lines;
            int count = Mathf.Min(clips != null ? clips.Length : 0, lines.Length);
            if (count == 0)
            {
                return;
            }
            int pick = Random.Range(0, count);
            if (speakers.Say(clips[pick], lines[pick]))
            {
                said++;
                nextAt = Time.time + MinimumGap;
            }
        }

        /// <summary>Film tour: one line of this kind as soon as the speakers are free.</summary>
        public bool SayForFilm(string key)
        {
            if (speakers.Talking)
            {
                return false;
            }
            said = 0;
            nextAt = 0f;
            Say(key, true);
            return true;
        }

        private void Update()
        {
            if (director.Phase != GamePhase.Playing || director.InCall)
            {
                return;
            }
            PlayerController player = director.Player;
            if (Time.time > musicBoxAt)
            {
                MusicBox();
            }
            if (!opened && Time.time > nextAt && director.Night >= 2)
            {
                opened = true;
                string habit = Habit();
                if (habit != null)
                {
                    Say(habit, true);
                }
            }
            if (player.HiddenIn != lastSpot)
            {
                lastSpot = player.HiddenIn;
                hiddenSince = Time.time;
                if (lastSpot != null)
                {
                    Count(lastSpot.Kind);
                }
            }
            if (lastSpot != null && Time.time - hiddenSince > 25f && Random.value < Time.deltaTime * 0.1f)
            {
                Say("hiding");
            }
            if (!player.Running)
            {
                runningSince = Time.time;
            }
            else if (Time.time - runningSince > 6f)
            {
                Say("sprint");
                runningSince = Time.time;
            }
            if (Random.value < Time.deltaTime * 0.004f)
            {
                Say("laugh");
            }
        }

        private static void Count(SpotKind kind)
        {
            SaveData save = SaveGame.Data;
            if (kind == SpotKind.Locker) save.hidLocker++;
            else if (kind == SpotKind.Cabinet) save.hidCabinet++;
            else save.hidTable++;
        }

        /// <summary>What he has to say about how this player plays: where they die, how they hide, how fast they escape.</summary>
        private static string Habit()
        {
            SaveData save = SaveGame.Data;
            List<PlayLog.Night> nights = save.history;
            if (nights.Count == 0)
            {
                return null;
            }
            PlayLog.Night last = nights[nights.Count - 1];
            int caughtInARow = 0;
            for (int i = nights.Count - 1; i >= 0 && nights[i].outcome == nameof(HuntOutcome.Caught); i--)
            {
                caughtInARow++;
            }
            if (caughtInARow >= 3)
            {
                return "habit_slow";
            }
            if (last.outcome == nameof(HuntOutcome.Escaped) && last.seconds < last.nightLength * 0.4f)
            {
                return "habit_fast";
            }
            if (last.outcome == nameof(HuntOutcome.Caught) && last.roomIndex >= 0 && Random.value < 0.6f)
            {
                return "caught_" + last.roomIndex;
            }
            int most = Mathf.Max(save.hidLocker, Mathf.Max(save.hidCabinet, save.hidTable));
            if (most < 3)
            {
                return null;
            }
            return most == save.hidLocker ? "habit_locker" : most == save.hidCabinet ? "habit_cabinet" : "habit_table";
        }
    }
}
