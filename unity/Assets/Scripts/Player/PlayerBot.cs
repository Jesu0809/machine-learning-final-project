using System;
using NightShift.Core;

namespace NightShift.Player
{
    /// <summary>Fixed bot difficulties used in evaluation.</summary>
    public enum BotPreset
    {
        Easy,
        Normal,
        Hard
    }

    /// <summary>Behavior parameters of the simulated guard. See docs/ml-design.md, section 7.</summary>
    [Serializable]
    public class PlayerBotSettings
    {
        /// <summary>Probability of closing the door when the monster is seen at it.</summary>
        public float ReactionProbability = 0.65f;

        /// <summary>Monster ticks between seeing the monster at a door and closing it.</summary>
        public int ReactionDelayTicks = 1;

        /// <summary>The bot checks one camera every this many monster ticks.</summary>
        public int CheckEveryTicks = 2;

        /// <summary>Relative chance of checking each room 0–4. Biased toward the corridors.</summary>
        public float[] RoomWeights = { 0.5f, 1f, 2f, 1f, 2f };

        /// <summary>The fixed settings of an evaluation preset.</summary>
        public static PlayerBotSettings FromPreset(BotPreset preset)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Random settings for one training episode (domain randomization): reaction probability
        /// 0.4–0.9, delay 0–2 ticks, check every 1–4 ticks, random room weights.
        /// </summary>
        public static PlayerBotSettings Randomized(Random random)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Replaces the human during training and evaluation. Checks cameras with a weighted random
    /// choice and closes a door when it sees the monster there, with a reaction probability and
    /// delay. It is stochastic on purpose so the monster cannot learn one fixed pattern.
    /// </summary>
    public class PlayerBot
    {
        private readonly Random random;

        public PlayerBot(int seed)
        {
            random = new Random(seed);
        }

        /// <summary>Settings for the current night.</summary>
        public PlayerBotSettings Settings { get; private set; }

        /// <summary>Uses fixed settings for every night (evaluation).</summary>
        public void UsePreset(BotPreset preset)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called at the start of every night. Draws new random settings unless a preset is set.
        /// </summary>
        public void OnNightStarted(GameState state)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The bot's turn, once per monster tick: maybe switch camera, maybe close or open a door.
        /// </summary>
        public void Act(GameState state)
        {
            throw new NotImplementedException();
        }
    }
}
