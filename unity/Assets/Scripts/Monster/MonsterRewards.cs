using NightShift.Core;

namespace NightShift.Monster
{
    /// <summary>
    /// Reward function of the monster (docs/ml-design.md, section 6). Kept apart from the agent so
    /// it can be checked without ML-Agents and so every training run changes it in one place.
    /// </summary>
    public static class MonsterRewards
    {
        // monster_v1
        public const float MovedCloser = 0.05f;
        public const float MovedAway = -0.05f;
        public const float BlockedByDoor = -0.3f;
        public const float SeenWhileMoving = -0.05f;
        public const float PerStep = -0.005f;
        public const float EnteredOffice = 1.0f;
        public const float NightEnded = -0.5f;

        /// <summary>Reward for one decision, terminal rewards excluded.</summary>
        public static float ForStep(in MonsterStep step)
        {
            float reward = PerStep;

            switch (step.Result)
            {
                case MoveResult.MovedCloser:
                    reward += MovedCloser;
                    break;
                case MoveResult.MovedAway:
                    reward += MovedAway;
                    break;
                case MoveResult.BlockedByDoor:
                    reward += BlockedByDoor;
                    break;
            }

            if (step.SeenWhileMoving)
            {
                reward += SeenWhileMoving;
            }

            return reward;
        }

        /// <summary>Terminal reward at the end of a night. Zero for an interrupted episode.</summary>
        public static float ForOutcome(NightOutcome outcome)
        {
            switch (outcome)
            {
                case NightOutcome.MonsterEntered: return EnteredOffice;
                case NightOutcome.GuardSurvived: return NightEnded;
                default: return 0f;
            }
        }
    }
}
