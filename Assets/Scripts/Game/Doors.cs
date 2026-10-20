using System.Collections.Generic;
using NightShift.Hunt;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// A door in a doorway (as in Friday the 13th): open by default; the player can close it with E.
    /// A closed door blocks the way and Freddy's view; Freddy has to break it down, which is loud
    /// and buys the player a few seconds. A broken door stays broken.
    /// To read as usable without breaking the darkness, the leaves stand open in sight with brass
    /// push bars that catch the flashlight, and a dim caged lamp over the frame turns from amber to
    /// red when the door is shut. The whole doorway is the target for E, so the player does not
    /// have to aim at a leaf.
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        public const float BreakSeconds = 2.6f;

        private static readonly List<Door> All = new List<Door>();

        private readonly Transform[] hinges = new Transform[2];
        private readonly GameObject[] leaves = new GameObject[2];
        private readonly List<Renderer> lamps = new List<Renderer>();
        private bool lampsClosed;
        private float axisYaw;
        private float swing;   // 0 closed, 1 open (folded against the wall)
        private HuntDirector director;

        public bool Closed { get; private set; }
        public bool Broken { get; private set; }
        public bool HoldToUse => false;

        /// <summary>Double doors in a doorway; <paramref name="alongX"/> if the wall runs along x.</summary>
        public static Door Create(HuntDirector director, Transform parent, Vector3 doorway, bool alongX, Material material)
        {
            var go = new GameObject("Door");
            go.transform.SetParent(parent, false);
            go.transform.position = doorway;
            var door = go.AddComponent<Door>();
            door.director = director;
            door.axisYaw = alongX ? 0f : 90f;
            float leaf = PizzeriaLayout.DoorWidth * 0.5f - 0.06f;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                door.hinges[i] = new GameObject("Hinge").transform;
                door.hinges[i].SetParent(go.transform, false);
                door.hinges[i].localPosition = Quaternion.Euler(0f, door.axisYaw, 0f) * new Vector3(side * (leaf + 0.02f), 0f, 0f);
                door.leaves[i] = BuildKit.Box("Leaf", door.hinges[i], new Vector3(-side * leaf * 0.5f, PizzeriaLayout.DoorHeight * 0.48f, 0f),
                                              new Vector3(leaf, PizzeriaLayout.DoorHeight * 0.96f, 0.06f), material);
                BuildKit.Box("Window", door.leaves[i].transform, new Vector3(0f, 0.28f, 0f), new Vector3(leaf * 0.5f, 0.3f, 0.08f), BuildKit.Plain("doorWindow", new Color(0.02f, 0.02f, 0.025f), 0.9f), 1f, false);
                // Brass push bar and kick plate on both faces: they catch the flashlight, so the door
                // reads as something you can push, without any glow that would break the darkness.
                BuildKit.Box("PushBar", door.leaves[i].transform, new Vector3(0f, -0.25f, 0f), new Vector3(leaf * 0.75f, 0.05f, 0.12f), Brass, 1f, false);
                BuildKit.Box("KickPlate", door.leaves[i].transform, new Vector3(0f, -PizzeriaLayout.DoorHeight * 0.48f + 0.17f, 0f), new Vector3(leaf * 0.92f, 0.3f, 0.075f), Brass, 1f, false);
            }

            // An old caged lamp over the doorway on both sides: a dim amber bulb, red once the door is shut.
            Quaternion frame = Quaternion.Euler(0f, door.axisYaw, 0f);
            Material casing = BuildKit.Plain("doorCasing", new Color(0.08f, 0.08f, 0.085f), 0.4f, 0.6f);
            for (int face = -1; face <= 1; face += 2)
            {
                float z = face * (PizzeriaLayout.WallThickness * 0.5f + 0.06f);
                BuildKit.Box("LampCage", go.transform, frame * new Vector3(-0.75f, PizzeriaLayout.DoorHeight + 0.14f, z), new Vector3(0.26f, 0.2f, 0.12f), casing, 1f, false, door.axisYaw);
                door.lamps.Add(BuildKit.Box("LampBulb", go.transform, frame * new Vector3(-0.75f, PizzeriaLayout.DoorHeight + 0.12f, z * 1.45f), new Vector3(0.16f, 0.12f, 0.06f), OpenGlow, 1f, false, door.axisYaw).GetComponent<Renderer>());
            }

            // The use target: the whole doorway (a trigger, so nothing collides with it and Freddy's eyes ignore it).
            var reach = new GameObject("Reach");
            reach.transform.SetParent(go.transform, false);
            reach.transform.localPosition = new Vector3(0f, PizzeriaLayout.DoorHeight * 0.5f, 0f);
            reach.transform.localRotation = frame;
            var trigger = reach.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(PizzeriaLayout.DoorWidth, PizzeriaLayout.DoorHeight, 0.6f);

            door.swing = 1f;
            All.Add(door);
            return door;
        }

        private static Material OpenGlow => BuildKit.Glow("doorOpenGlow", new Color(1f, 0.62f, 0.25f), 2.2f);
        private static Material ClosedGlow => BuildKit.Glow("doorClosedGlow", new Color(1f, 0.12f, 0.08f), 2f);
        private static Material Brass => BuildKit.Plain("brass", new Color(0.62f, 0.48f, 0.24f), 0.75f, 1f);

        /// <summary>The usable door whose doorway the point is standing in, or null.</summary>
        public static Door InDoorway(Vector3 point, float distance)
        {
            foreach (Door door in All)
            {
                if (!door.Broken && SameFloor(door, point) && Vector3.Distance(Flat(door.transform.position), Flat(point)) < distance)
                {
                    return door;
                }
            }
            return null;
        }

        /// <summary>Back to open and whole (new night).</summary>
        public static void ResetAll()
        {
            foreach (Door door in All)
            {
                door.Closed = false;
                door.Broken = false;
                door.leaves[0].SetActive(true);
                door.leaves[1].SetActive(true);
                door.swing = 1f;
            }
        }

        /// <summary>A closed, unbroken door within <paramref name="distance"/> of the point, or null.</summary>
        public static Door ClosedNear(Vector3 point, float distance)
        {
            foreach (Door door in All)
            {
                if (door.Closed && !door.Broken && SameFloor(door, point) && Vector3.Distance(Flat(door.transform.position), Flat(point)) < distance)
                {
                    return door;
                }
            }
            return null;
        }

        public string Prompt(PlayerController player)
        {
            if (Broken || player.HiddenIn != null)
            {
                return "";
            }
            return Closed ? "[E] Open the door" : "[E] Close the door  (he will have to break it down)";
        }

        public void Use(PlayerController player)
        {
            if (Broken)
            {
                return;
            }
            Closed = !Closed;
            player.MakeNoise(Closed ? 2 : 1);
            director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, Closed ? 0.9f : 0.5f, 0.7f);
        }

        /// <summary>The player crashes into the closed door (running or sliding): it is pushed open, loudly. True if it was.</summary>
        public bool Bump(PlayerController player)
        {
            if (Broken || !Closed)
            {
                return false;
            }
            Closed = false;
            player.MakeNoise(3);
            director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 1f, 0.6f);
            return true;
        }

        /// <summary>One of Freddy's blows; the last one breaks the door.</summary>
        public void Hit(bool last)
        {
            director.Audio.PlayAt(director.Assets.LockerDoor, transform.position, 1f, last ? 0.5f : 0.6f);
            if (last)
            {
                Broken = true;
                Closed = false;
                leaves[0].SetActive(false);
                leaves[1].SetActive(false);
                director.UI.Toast("He broke the door down.");
            }
            else
            {
                swing = 0.06f; // the leaves jump in their frame
            }
        }

        private void Update()
        {
            bool showClosed = Closed && !Broken;
            if (showClosed != lampsClosed)
            {
                lampsClosed = showClosed;
                foreach (Renderer lamp in lamps)
                {
                    lamp.sharedMaterial = showClosed ? ClosedGlow : OpenGlow;
                }
            }
            float target = Closed ? 0f : 1f;
            swing = Mathf.MoveTowards(swing, target, Time.deltaTime * 2.5f);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                // Open, the leaves rest ajar at an angle: seen from both sides of the doorway, still wide enough to run through.
                hinges[i].localRotation = Quaternion.Euler(0f, axisYaw + side * 75f * swing, 0f);
            }
        }

        private static Vector3 Flat(Vector3 p) => new Vector3(p.x, 0f, p.z);

        /// <summary>Doors on the two floors can stand one above the other; only the one on this floor counts.</summary>
        private static bool SameFloor(Door door, Vector3 point) => Mathf.Abs(point.y - door.transform.position.y) < 2f;
    }
}
