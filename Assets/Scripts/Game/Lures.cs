using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// A party horn lying around (as the noisemakers of Alien: Isolation). Take it with E and
    /// throw it with G: it blows where it lands, and Freddy's brain hears a noise there. The
    /// network cannot tell it from the player, so you can watch it take the bait in the inspector.
    /// </summary>
    public class LurePickup : MonoBehaviour, IInteractable
    {
        private Vector3 basePosition;

        public bool HoldToUse => false;

        public static LurePickup Create(Transform parent, Vector3 position, Color color)
        {
            var go = new GameObject("PartyHorn");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            BuildKit.Cone("Horn", go.transform, Vector3.zero, 0.06f, 0.28f, BuildKit.Glow("horn" + color.GetHashCode(), color, 1.2f))
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var collider = go.AddComponent<SphereCollider>();
            collider.radius = 0.3f;
            collider.isTrigger = true;
            var pickup = go.AddComponent<LurePickup>();
            pickup.basePosition = position;
            return pickup;
        }

        public string Prompt(PlayerController player) => player.HiddenIn == null ? "[E] Take the party horn (throw it with G)" : "";

        public void Use(PlayerController player)
        {
            player.AddLure();
            player.Director.Audio.Click();
            gameObject.SetActive(false);
        }

        private void Update()
        {
            transform.position = basePosition + Vector3.up * (0.05f + Mathf.Sin(Time.time * 2.5f) * 0.02f);
            transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        }
    }

    /// <summary>A thrown party horn: it flies, lands, and blows (a loud noise in that room).</summary>
    public class ThrownLure : MonoBehaviour
    {
        private HuntDirector director;
        private bool blown;
        private float bornAt;

        public static void Throw(HuntDirector director, Vector3 from, Vector3 direction)
        {
            var go = new GameObject("ThrownHorn");
            go.layer = 2;
            go.transform.position = from;
            BuildKit.Cone("Horn", go.transform, Vector3.zero, 0.06f, 0.28f, BuildKit.Glow("hornThrown", new Color(1f, 0.8f, 0.2f), 1.2f));
            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.linearVelocity = direction.normalized * 9f + Vector3.up * 2.5f;
            body.angularVelocity = Random.insideUnitSphere * 10f;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            go.AddComponent<SphereCollider>().radius = 0.08f;
            var lure = go.AddComponent<ThrownLure>();
            lure.director = director;
            lure.bornAt = Time.time;
        }

        private void OnCollisionEnter(Collision collision)
        {
            Blow();
        }

        private void Update()
        {
            if (!blown && Time.time - bornAt > 2.5f)
            {
                Blow();
            }
        }

        private void Blow()
        {
            if (blown)
            {
                return;
            }
            blown = true;
            director.OnLureBlown(transform.position);
            Destroy(gameObject, 6f);
        }
    }
}
