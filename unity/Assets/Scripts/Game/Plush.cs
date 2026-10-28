using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// A Freddy plush on the guard's desk, left over from when the pizzeria sold them. Pressing its
    /// nose honks, and the plush squashes and springs back. Only for the guard: the honk is not a
    /// noise in the hunt, Freddy's brain never hears it.
    /// </summary>
    public class Plush : MonoBehaviour, IInteractable
    {
        private const float Height = 0.34f;

        private HuntDirector director;
        private Transform body;
        private float squeezedAt = -10f;
        private int honks;

        public bool HoldToUse => false;

        public static Plush Create(HuntDirector director, Transform parent, Pose pose)
        {
            var go = new GameObject("Plush");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            GameObject model = FreddyParts.Plush(go.transform, Vector3.zero, Quaternion.identity, Height);
            if (model == null)
            {
                Destroy(go);
                return null;
            }
            var plush = go.AddComponent<Plush>();
            plush.director = director;
            plush.body = model.transform;
            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, Height * 0.5f, 0.03f);
            trigger.size = new Vector3(0.26f, Height, 0.26f);
            return plush;
        }

        public string Prompt(PlayerController player)
        {
            return player.HiddenIn != null || player.InFocus ? "" : "[E] Press his nose";
        }

        public void Use(PlayerController player)
        {
            if (Time.time - squeezedAt < 0.3f)
            {
                return;
            }
            squeezedAt = Time.time;
            honks++;
            // Played straight to the guard's ears: no MakeNoise, the hunt never hears it.
            director.Audio.PlayAt(GameAudio.Honk(), transform.position, 0.9f, honks % 7 == 0 ? 0.82f : Random.Range(0.97f, 1.04f));
        }

        private void Update()
        {
            // Squashed down and wider, then a springy wobble back.
            float t = Time.time - squeezedAt;
            float squash = t < 0.08f ? t / 0.08f : Mathf.Exp(-(t - 0.08f) * 7f) * Mathf.Cos((t - 0.08f) * 26f);
            float amount = 0.16f * squash;
            body.localScale = new Vector3(Height * (1f + amount * 0.5f), Height * (1f - amount), Height * (1f + amount * 0.5f));
        }
    }
}
