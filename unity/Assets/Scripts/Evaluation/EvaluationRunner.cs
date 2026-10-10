using System;
using System.Collections.Generic;
using NightShift.Core;
using NightShift.Monster;
using NightShift.Player;
using UnityEngine;

namespace NightShift.Evaluation
{
    /// <summary>
    /// One row of results.json. Field names are snake_case on purpose: JsonUtility writes field
    /// names as they are, and the website and Python scripts read these keys.
    /// </summary>
    [Serializable]
    public class EvaluationRun
    {
        public string policy;
        public string bot_preset;
        public int nights;
        public float success_rate;
        public float avg_time_to_office_s;
        public float avg_door_blocks;
        public float avg_power_remaining;
    }

    /// <summary>Root object of results.json (format in docs/ml-design.md, section 9).</summary>
    [Serializable]
    public class EvaluationResults
    {
        public string generated_at;
        public string model;
        public int nights_per_run;
        public List<EvaluationRun> runs = new List<EvaluationRun>();
    }

    /// <summary>
    /// Runs every policy (trained, scripted, random) against every bot preset (Easy, Normal, Hard)
    /// for <see cref="nightsPerRun"/> nights each, headless and as fast as possible, then writes
    /// training/evaluation/results.json.
    /// </summary>
    public class EvaluationRunner : MonoBehaviour
    {
        [SerializeField] private GameSettings settings = new GameSettings();
        [SerializeField] private int nightsPerRun = 200;
        [SerializeField] private int seed = 7;

        [Tooltip("Agent with the trained model assigned and Behavior Type = Inference Only.")]
        [SerializeField] private MonsterAgent trainedMonster;

        [Tooltip("Model name written to results.json, e.g. monster_v3.")]
        [SerializeField] private string modelName = "monster_v1";

        [Tooltip("Relative to the Unity project folder.")]
        [SerializeField] private string outputPath = "../training/evaluation/results.json";

        /// <summary>Plays <paramref name="nights"/> nights of one policy against one preset.</summary>
        public EvaluationRun RunPolicy(IMonsterPolicy policy, BotPreset preset, int nights)
        {
            throw new NotImplementedException();
        }

        /// <summary>Runs the full 3 × 3 grid and writes the JSON file.</summary>
        public void RunAll()
        {
            throw new NotImplementedException();
        }
    }
}
