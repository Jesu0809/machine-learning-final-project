using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// What makes Pockets move like a broken machine on top of his animations: his servos stick for
    /// an instant and catch up, his head snaps and shudders at random, sparks fly from his chest, his
    /// jaw hangs and chatters when he hunts, the key in his back turns as he walks, and the mallet
    /// hangs from his hand and drags on the floor. His steps are matched to how fast he moves.
    /// </summary>
    public class PocketsRig : MonoBehaviour
    {
        private const float JawOpenDegrees = 21f;
        /// <summary>From the grip to the middle of the mallet's head, and the head's radius (sculpt meters).</summary>
        private static readonly Vector3 MalletHead = new Vector3(0f, -0.85f, 0f);
        private const float MalletRadius = 0.125f;

        public Transform Jaw;
        public Transform Key;
        public Transform Core;
        public Transform Mallet;

        /// <summary>The meters per second his walk and run clips cover by themselves (0: not known).</summary>
        public float WalkClipSpeed;
        public float RunClipSpeed;

        private Animator animator;
        private FreddyBody body;
        private Transform head;
        private Transform leftArm;
        private Quaternion jawRest;
        private Quaternion keyRest;
        private float stallUntil;
        private float nextStallAt;
        private float owed;
        private float pace;
        private bool matchesSteps;
        private Quaternion malletRest;
        private float malletLoose = 1f;
        private Vector3 malletHang;
        private float keyAngle;
        private float jawAngle;
        private float nextTwitchAt;
        private float twitchUntil;
        private Vector3 twitch;
        private float shudderUntil;
        private Vector3 lastPosition;
        private ParticleSystem sparks;
        private Light coreLight;

        private void Start()
        {
            animator = GetComponentInChildren<Animator>();
            body = GetComponent<FreddyBody>();
            if (animator != null)
            {
                if (animator.isHuman)
                {
                    head = animator.GetBoneTransform(HumanBodyBones.Head);
                    leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                }
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    matchesSteps |= parameter.name == "StepScale";
                }
            }
            malletRest = Mallet != null ? Mallet.localRotation : Quaternion.identity;
            nextStallAt = Time.time + Random.Range(1f, 3f);
            jawRest = Jaw != null ? Jaw.localRotation : Quaternion.identity;
            keyRest = Key != null ? Key.localRotation : Quaternion.identity;
            lastPosition = transform.position;
            nextTwitchAt = Time.time + Random.Range(2f, 5f);
            if (Core != null)
            {
                coreLight = new GameObject("CoreLight").AddComponent<Light>();
                coreLight.transform.SetParent(Core, false);
                coreLight.transform.localPosition = new Vector3(0f, 0f, 0.1f);
                coreLight.type = LightType.Point;
                coreLight.range = 2.4f;
                coreLight.color = new Color(1f, 0.22f, 0.08f);
                sparks = MakeSparks(Core);
            }
        }

        private static ParticleSystem MakeSparks(Transform parent)
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.14f);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.016f);
            main.gravityModifier = 1.4f;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 50f;
            shape.radius = 0.02f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = BuildKit.Additive("spark", ProceduralTextures.Disc(16), new Color(1f, 0.7f, 0.3f, 1f));
            return particles;
        }

        private void Update()
        {
            bool hunting = body != null && (body.Chasing || body.Attacking);
            float moved = Vector3.Distance(transform.position, lastPosition) / Mathf.Max(Time.deltaTime, 0.001f);
            lastPosition = transform.position;
            pace = Mathf.Lerp(pace, moved, 1f - Mathf.Exp(-Time.deltaTime * 8f));
            bool caught = body != null && body.Attacking;
            if (animator != null)
            {
                Servos(caught, hunting);
                MatchSteps();
                FreeTheArms(caught);
            }

            if (Key != null)
            {
                keyAngle += Mathf.Clamp(moved, 0f, 5f) * 75f * Time.deltaTime;
                // It winds in clicks, not smoothly.
                Key.localRotation = keyRest * Quaternion.Euler(0f, 0f, Mathf.Floor(keyAngle / 30f) * 30f);
            }

            if (Jaw != null)
            {
                bool attacking = body != null && body.Attacking;
                float wanted = attacking ? JawOpenDegrees : hunting ? 8f + Mathf.Abs(Mathf.Sin(Time.time * 17f)) * 8f : Time.time < twitchUntil ? 7f : 0f;
                jawAngle = Mathf.MoveTowards(jawAngle, wanted, Time.deltaTime * 170f);
                Jaw.localRotation = jawRest * Quaternion.Euler(jawAngle, 0f, 0f);
            }

            if (Time.time >= nextTwitchAt)
            {
                nextTwitchAt = Time.time + (hunting ? Random.Range(1.2f, 3f) : Random.Range(2.5f, 7f));
                twitchUntil = Time.time + Random.Range(0.08f, 0.22f);
                shudderUntil = Time.time + Random.Range(0.18f, 0.5f);
                twitch = new Vector3(Random.Range(-16f, 16f), Random.Range(-34f, 34f), Random.Range(-30f, 30f));
                if (sparks != null && Random.value < 0.7f)
                {
                    sparks.Emit(Random.Range(5, 16));
                }
            }
            if (coreLight != null)
            {
                float fault = Time.time < shudderUntil ? Random.value * 1.8f : 1f;
                coreLight.intensity = (hunting ? 1.5f : 0.75f) * fault;
            }
        }

        /// <summary>Mostly smooth. Every so often the servos stick for a few frames, then catch up all at once.</summary>
        private void Servos(bool attacking, bool hunting)
        {
            if (attacking)
            {
                animator.speed = 1f;
                owed = 0f;
                return;
            }
            if (Time.time >= nextStallAt)
            {
                nextStallAt = Time.time + (hunting ? Random.Range(0.7f, 2.2f) : Random.Range(1.2f, 3.6f));
                stallUntil = Time.time + Random.Range(0.045f, 0.12f);
            }
            if (Time.time < stallUntil)
            {
                animator.speed = 0f;
                owed += Time.deltaTime;
            }
            else if (owed > 0f)
            {
                const float CatchUpSeconds = 0.09f;
                animator.speed = 1f + owed / CatchUpSeconds;
                owed = Mathf.Max(0f, owed - Time.deltaTime * owed / CatchUpSeconds);
                if (owed < 0.002f)
                {
                    owed = 0f;
                }
            }
            else
            {
                animator.speed = 1f;
            }
        }

        /// <summary>A roar or a laugh takes the upper body, but never from the moves that need both arms.</summary>
        private void FreeTheArms(bool caught)
        {
            if (animator.layerCount < 2)
            {
                return;
            }
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            bool busy = state.IsName("Smash") || state.IsName("Search");
            float weight = caught ? 0f : Mathf.MoveTowards(animator.GetLayerWeight(1), busy ? 0f : 1f, Time.deltaTime * 6f);
            animator.SetLayerWeight(1, weight);
        }

        /// <summary>His steps play as fast as he really moves, so his feet do not slide on the floor.</summary>
        private void MatchSteps()
        {
            if (!matchesSteps)
            {
                return;
            }
            float blend = animator.GetFloat("Speed");
            float scale = 1f;
            if (blend > 0.2f && pace > 0.1f && WalkClipSpeed > 0.01f)
            {
                // A humanoid clip's speed is in units of his body: humanScale, times the prefab's own scale.
                float natural = Mathf.Lerp(WalkClipSpeed, RunClipSpeed > 0.01f ? RunClipSpeed : WalkClipSpeed * 2f, blend - 1f) * animator.humanScale * animator.transform.lossyScale.y;
                scale = Mathf.Clamp(pace / Mathf.Max(natural, 0.1f), 0.6f, 2.4f);
            }
            animator.SetFloat("StepScale", scale);
        }

        private void LateUpdate()
        {
            HangMallet();
            if (head != null && body != null && body.IsAsleep && body.ScriptedWalk < 0f)
            {
                // Powered down: his head hangs.
                head.rotation = Quaternion.AngleAxis(26f, transform.right) * head.rotation;
            }
            // On top of whatever the animation says: the fault in the neck and the arm.
            if (head != null && Time.time < twitchUntil)
            {
                head.localRotation *= Quaternion.Euler(twitch);
            }
            if (leftArm != null && Time.time < shudderUntil)
            {
                leftArm.localRotation *= Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-3f, 3f), Random.Range(-3f, 3f));
            }
        }

        /// <summary>
        /// The mallet is heavy and he holds it loosely: it hangs from the machine hand and trails behind
        /// him as he moves, whatever the arm is doing, and its head rests on the floor when the arm is
        /// low, so he drags it. Only when he swings it (attack, smash) is it rigid in the hand.
        /// </summary>
        private void HangMallet()
        {
            if (Mallet == null)
            {
                return;
            }
            Mallet.localRotation = malletRest;
            Vector3 grip = Mallet.position;
            Vector3 rigid = Mallet.TransformPoint(MalletHead) - grip;
            float length = rigid.magnitude;
            bool swinging = false;
            if (animator != null)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
                swinging = state.IsName("Attack") || state.IsName("Smash");
            }
            malletLoose = Mathf.MoveTowards(malletLoose, swinging ? 0f : 1f, Time.deltaTime * 7f);

            // Trailing behind him and a little out to his left, clear of his leg.
            Vector3 hanging = (Vector3.down - transform.forward * (0.12f + Mathf.Min(pace * 0.12f, 0.5f)) - transform.right * 0.22f).normalized;
            malletHang = malletHang == Vector3.zero ? hanging : Vector3.Slerp(malletHang, hanging, 1f - Mathf.Exp(-Time.deltaTime * 5f)).normalized;
            Vector3 along = Vector3.Slerp(rigid.normalized, malletHang, malletLoose) * length;

            float floor = transform.position.y + MalletRadius * Mallet.lossyScale.y;
            if (grip.y + along.y < floor)
            {
                float drop = Mathf.Clamp(grip.y - floor, 0f, length);
                Vector3 flat = new Vector3(along.x, 0f, along.z);
                flat = flat.sqrMagnitude < 0.0004f ? -transform.forward : flat.normalized;
                along = flat * Mathf.Sqrt(Mathf.Max(length * length - drop * drop, 0f)) - Vector3.up * drop;
            }
            Mallet.rotation = Quaternion.FromToRotation(rigid, along) * Mallet.rotation;
        }
    }
}
