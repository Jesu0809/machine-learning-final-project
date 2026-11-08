using System;

namespace NightShift.Hunt
{
    /// <summary>
    /// How dangerous Freddy is for this player, 0 to 1, kept between sessions. Someone who knows the
    /// game escapes quickly and raises it; someone still learning it is caught and lowers it, so the
    /// same seven nights challenge both. It changes his senses and his pace, never the night's
    /// objectives and never what his network knows: his brain still gets no hint of where the player is. Pure rules, so
    /// they can be tested outside Unity. <see cref="Start"/> is about the difficulty before this existed.
    /// </summary>
    public static class ThreatLevel
    {
        public const float Start = 0.35f;

        /// <summary>From here on he wakes sooner and hears running one room farther.</summary>
        public const float SharpEars = 0.7f;

        /// <summary>The level after a night. An escape raises it, more if fast and unseen; being caught lowers it, more if little was done.</summary>
        /// <param name="progress">Share of the night's objectives done, 0 to 1.</param>
        /// <param name="nightShare">Share of the night's length that was played, 0 to 1.</param>
        public static float AfterNight(float threat, HuntOutcome outcome, float progress, float nightShare, int timesSeen)
        {
            float change;
            if (outcome == HuntOutcome.Escaped)
            {
                float fast = Clamp01(1f - nightShare * 2f);
                change = 0.12f + 0.12f * fast + (timesSeen == 0 ? 0.04f : 0f);
            }
            else if (outcome == HuntOutcome.Caught)
            {
                change = -0.03f - 0.07f * (1f - Clamp01(progress));
            }
            else
            {
                change = -0.02f;
            }
            return Clamp01(threat + change);
        }

        /// <summary>Sets the rules of a night for this threat level. The night keeps its length in seconds.</summary>
        public static void Apply(HuntSettings settings, int night, float threat)
        {
            int pace = Math.Min(night, 5);   // nights 6 and 7 add rules, not more speed
            float baseInterval = 3.5f - (pace - 1) * 0.3f;
            settings.DecisionIntervalSeconds = baseInterval * Lerp(1.15f, 0.8f, threat);
            settings.MaxTicks = (int)Math.Round(240f * baseInterval / settings.DecisionIntervalSeconds);
            int cooldown = pace >= 5 ? 16 : pace >= 4 ? 20 : 30;
            settings.SenseCooldownTicks = (int)Math.Round(cooldown * Lerp(1.25f, 0.7f, threat));
            settings.FreddyWakeTicks = night >= 7 ? 0 : threat >= SharpEars ? 3 : 6;
            settings.HearingRange = threat >= SharpEars ? new[] { -1, 0, 2, 3 } : new[] { -1, 0, 1, 2 };
            settings.NoiseMisplaceChance = Lerp(0.35f, 0.1f, threat);
        }

        /// <summary>Multiplies his walking and running speed.</summary>
        public static float SpeedScale(float threat) => Lerp(0.93f, 1.1f, threat);

        /// <summary>Multiplies how far he sees.</summary>
        public static float SightScale(float threat) => Lerp(0.85f, 1.25f, threat);

        /// <summary>Seconds before the player can enter another duct.</summary>
        public static float DuctCooldownSeconds(float threat) => Lerp(18f, 40f, threat);

        /// <summary>The level as the player reads it, 1 to 10.</summary>
        public static int Display(float threat) => 1 + (int)Math.Round(Clamp01(threat) * 9f);

        private static float Lerp(float from, float to, float t) => from + (to - from) * Clamp01(t);

        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }
}
