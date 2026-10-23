using System.Collections.Generic;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// A grate at one end of an air duct. With E the player crawls through to the other end: the
    /// duct is too small for Freddy, he cannot see into it, and only the grates make a sound.
    /// </summary>
    public class VentGrate : MonoBehaviour, IInteractable
    {
        public DuctPath Duct { get; private set; }

        /// <summary>True if this grate is at the duct's A end.</summary>
        public bool EndA { get; private set; }

        /// <summary>The grate at the duct's other end.</summary>
        public VentGrate Other { get; set; }

        public bool HoldToUse => false;

        private Transform hinge;
        private GameObject hole;
        private float openness;
        private float target;
        private float closeAt = -1f;

        public static VentGrate Attach(GameObject go, DuctPath duct, bool endA, Transform hinge, GameObject hole)
        {
            var grate = go.AddComponent<VentGrate>();
            grate.Duct = duct;
            grate.EndA = endA;
            grate.hinge = hinge;
            grate.hole = hole;
            return grate;
        }

        /// <summary>Swings the grille open (a creak), and shut again after <paramref name="closeAfter"/> seconds.</summary>
        public void OpenFor(float closeAfter, PlayerController player)
        {
            if (target < 1f && player != null)
            {
                player.Director.Audio.PlayAt(player.Director.Assets.LockerDoor, transform.position, 0.35f, 1.8f);
            }
            target = 1f;
            closeAt = Time.time + closeAfter;
        }

        /// <summary>How open the grille is (0 shut, 1 open).</summary>
        public float Openness => openness;

        private void Update()
        {
            if (closeAt > 0f && Time.time >= closeAt)
            {
                target = 0f;
                closeAt = -1f;
            }
            // Opens with a jerk, settles; closes slowly.
            openness = Mathf.MoveTowards(openness, target, Time.deltaTime * (target > openness ? 2.6f : 1.4f));
            float k = openness * openness * (3f - 2f * openness);
            if (hinge != null)
            {
                hinge.localRotation = Quaternion.Euler(-105f * k, 0f, 0f);
            }
        }

        public string Prompt(PlayerController player)
        {
            if (player.HiddenIn != null || player.Crawling)
            {
                return "";
            }
            string where = Hunt.HuntMap.DisplayName(EndA ? Duct.B : Duct.A);
            return "[E] Crawl into the vent  (to the " + where + " — he can't follow you)";
        }

        public void Use(PlayerController player)
        {
            if (player.HiddenIn == null && !player.Crawling)
            {
                player.StartCrawl(CrawlPath(), EndA ? Duct.B : Duct.A, this, Other);
            }
        }

        /// <summary>The eye's path: from where the player stands, into the grate, along the duct, out of the far grate.</summary>
        private List<Vector3> CrawlPath()
        {
            const float crouchedEyes = 1.0f;
            var path = new List<Vector3>();
            Vector3 start = EndA ? Duct.StandA : Duct.StandB;
            Vector3 end = EndA ? Duct.StandB : Duct.StandA;
            path.Add(start + Vector3.up * crouchedEyes);
            Vector3[] points = Duct.Points;
            for (int i = 0; i < points.Length; i++)
            {
                path.Add(points[EndA ? i : points.Length - 1 - i] + DuctEyeOffset(EndA ? i : points.Length - 1 - i));
            }
            path.Add(end + Vector3.up * crouchedEyes);
            return path;
        }

        /// <summary>Floor grates sit at floor level; inside, the eyes run along the middle of the duct.</summary>
        private Vector3 DuctEyeOffset(int index)
        {
            bool floorEnd = (index == 0 && Duct.FloorGrateA) || (index == Duct.Points.Length - 1 && Duct.FloorGrateB);
            return floorEnd ? Vector3.up * 0.2f : Vector3.zero;
        }
    }
}
