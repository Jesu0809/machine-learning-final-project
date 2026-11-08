using System;

namespace NightShift.Hunt
{
    /// <summary>
    /// What each night of the show adds, the same for every player: how good someone is changes how
    /// dangerous he is (<see cref="ThreatLevel"/>), never what the night asks for. Pure rules, so the
    /// trained brain is not involved: it hears the noises these things make like any other noise.
    /// </summary>
    public class NightRules
    {
        /// <summary>The exit takes this long to open after the lever, with the siren going (0: at once).</summary>
        public float ExitSeconds;

        /// <summary>Share of the ceiling lamps that are dead.</summary>
        public float DeadLights;

        /// <summary>Rubber toys on the floor: stepping on one squeaks unless crouching.</summary>
        public int Toys;

        /// <summary>Jack-in-the-boxes: passing close pops them, very loud, unless crouching.</summary>
        public int Surprises;

        /// <summary>The dummies turn to face the player when not looked at.</summary>
        public bool DummiesMove;

        /// <summary>The vents are bolted shut.</summary>
        public bool VentsShut;

        public static NightRules ForNight(int night)
        {
            switch (Math.Max(1, night))
            {
                case 1: return new NightRules();
                case 2: return new NightRules { Toys = 3 };
                case 3: return new NightRules { Toys = 4, Surprises = 1, DummiesMove = true, DeadLights = 0.3f };
                case 4: return new NightRules { Toys = 5, Surprises = 2, DummiesMove = true, DeadLights = 0.5f };
                case 5: return new NightRules { Toys = 5, Surprises = 2, DummiesMove = true, DeadLights = 0.5f, ExitSeconds = 45f };
                case 6: return new NightRules { Toys = 5, Surprises = 3, DummiesMove = true, DeadLights = 0.9f, ExitSeconds = 45f };
                default: return new NightRules { Toys = 6, Surprises = 3, DummiesMove = true, DeadLights = 0.5f, ExitSeconds = 60f, VentsShut = true };
            }
        }

        /// <summary>A round of the encore, or the show of the day: the same seed gives the same rules.</summary>
        public static NightRules Rolled(int seed, int round)
        {
            var random = new Random(seed);
            return new NightRules
            {
                Toys = 4 + random.Next(4),
                Surprises = 2 + random.Next(3),
                DummiesMove = true,
                DeadLights = random.NextDouble() < 0.3 ? 0.9f : 0.3f + (float)random.NextDouble() * 0.4f,
                ExitSeconds = 30f + Math.Min(round, 6) * 5f,
                VentsShut = random.NextDouble() < 0.25
            };
        }
    }
}
