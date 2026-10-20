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

        public bool HoldToUse => false;

        public static VentGrate Attach(GameObject go, DuctPath duct, bool endA)
        {
            var grate = go.AddComponent<VentGrate>();
            grate.Duct = duct;
            grate.EndA = endA;
            return grate;
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
                player.StartCrawl(CrawlPath(), EndA ? Duct.B : Duct.A);
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
