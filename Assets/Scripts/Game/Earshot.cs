using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Makes a sound in the building readable by ear, since the player has no radar: how far it is,
    /// which side, in front or behind, and whether a wall is in the way. On a 3D AudioSource:
    /// <list type="bullet">
    /// <item>Distance: a hand-made curve, loud up close, clear two rooms away, faint at 30 m, silent at 40 m (far away is calm).</item>
    /// <item>Left and right: Unity's panning, from the listener's (the player's head's) orientation.</item>
    /// <item>Behind: a sound behind the head is duller and a little quieter, the cue the ear uses to
    /// tell back from front (plain stereo panning sounds the same both ways).</item>
    /// <item>Walls: each wall between the ear and the sound muffles it and makes it a little quieter, so a
    /// step in the next room sounds like the next room (but is still heard). Furniture does not count.</item>
    /// </list>
    /// Owners set <see cref="Volume"/> instead of the AudioSource's volume. The muffling uses a
    /// low-pass filter, which WebGL builds lack: there only the volume changes.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class Earshot : MonoBehaviour
    {
        public const float MaxDistance = 40f;

        /// <summary>Only what reaches this high is a wall: tables, booths, cabinets and lockers do not muffle a sound.</summary>
        private const float WallTop = 2.3f;

        private static readonly RaycastHit[] Hits = new RaycastHit[8];
        private static AnimationCurve rolloff;
        private static AudioListener listener;

        private AudioSource source;
        private AudioLowPassFilter filter;
        private Transform owner;
        private float occlusion = -1f;

        /// <summary>The sound's own volume (before distance, walls and direction).</summary>
        public float Volume { get; set; } = 1f;

        /// <summary>Walls between the ear and the sound at the last update (for tests).</summary>
        public int Walls { get; private set; }

        /// <summary>Configures <paramref name="source"/> as a sound in the world and adds its Earshot.</summary>
        /// <param name="owner">The object making it (Freddy, a locker): its own colliders never muffle it.</param>
        public static Earshot Attach(AudioSource source, float volume, Transform owner = null)
        {
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.minDistance = 1f;
            source.maxDistance = MaxDistance;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff());
            var earshot = source.gameObject.AddComponent<Earshot>();
            earshot.source = source;
            earshot.owner = owner;
            earshot.Volume = volume;
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                earshot.filter = source.gameObject.AddComponent<AudioLowPassFilter>();
                earshot.filter.cutoffFrequency = 22000f;
            }
            earshot.Apply(true);
            return earshot;
        }

        /// <summary>Gain over distance / <see cref="MaxDistance"/>: full within 2 m, 0.8 at 6 m, half at 12 m, a quarter at 20 m, a whisper at 30 m, nothing at 40 m.</summary>
        private static AnimationCurve Rolloff()
        {
            if (rolloff == null)
            {
                rolloff = new AnimationCurve(
                    new Keyframe(0f, 1f), new Keyframe(0.05f, 1f), new Keyframe(0.15f, 0.8f), new Keyframe(0.3f, 0.5f),
                    new Keyframe(0.5f, 0.25f), new Keyframe(0.75f, 0.08f), new Keyframe(1f, 0f));
                for (int i = 1; i < rolloff.length - 1; i++)
                {
                    rolloff.SmoothTangents(i, 0f);
                }
            }
            return rolloff;
        }

        private void LateUpdate()
        {
            Apply(false);
        }

        private void Apply(bool immediate)
        {
            if (listener == null)
            {
                listener = FindAnyObjectByType<AudioListener>();
                if (listener == null)
                {
                    source.volume = Volume;
                    return;
                }
            }
            Transform ear = listener.transform;
            Vector3 toSound = transform.position - ear.position;
            float distance = toSound.magnitude;

            // Behind the head: 0 in front or to the side, 1 straight behind.
            Vector3 facing = Vector3.ProjectOnPlane(ear.forward, Vector3.up);
            Vector3 flat = Vector3.ProjectOnPlane(toSound, Vector3.up);
            float behind = facing.sqrMagnitude > 1e-4f && flat.sqrMagnitude > 1e-4f ? Mathf.Clamp01((Vector3.Angle(facing, flat) - 90f) / 90f) : 0f;

            // Walls in between (up to two count), eased so a door or a corner does not click.
            Walls = distance > 0.5f && distance < MaxDistance ? CountWalls(ear.position) : 0;
            float target = Mathf.Min(2, Walls) / 2f;
            occlusion = immediate || occlusion < 0f ? target : Mathf.MoveTowards(occlusion, target, Time.deltaTime * 4f);

            // One wall: 0.75 of the volume, cut above about 5 kHz; two: 0.55, cut above about 1.6 kHz
            // (his clank sits around 0.5 to 2.5 kHz, so it still comes through, dull).
            source.volume = Volume * Mathf.Lerp(1f, 0.85f, behind) * Mathf.Lerp(1f, 0.55f, occlusion);
            if (filter != null)
            {
                float air = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(distance / MaxDistance));
                filter.cutoffFrequency = 22000f * air * Mathf.Pow(0.4f, behind) * Mathf.Pow(0.075f, occlusion);
            }
        }

        /// <summary>
        /// Walls between the ear and the sound, along a level line at the ear's height (so a step on
        /// the floor is not hidden by the low half of a wall twice): only tall solid things, not the
        /// sound's owner or the player.
        /// </summary>
        private int CountWalls(Vector3 ear)
        {
            Vector3 toSound = transform.position - ear;
            toSound.y = 0f;
            float distance = toSound.magnitude;
            if (distance < 0.5f)
            {
                return 0;
            }
            int count = Physics.RaycastNonAlloc(ear, toSound / distance, Hits, distance - 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            int walls = 0;
            for (int i = 0; i < count; i++)
            {
                Collider hit = Hits[i].collider;
                if (hit is CharacterController || (owner != null && hit.transform.IsChildOf(owner)) || hit.bounds.max.y < WallTop)
                {
                    continue;
                }
                walls++;
            }
            return walls;
        }
    }
}
