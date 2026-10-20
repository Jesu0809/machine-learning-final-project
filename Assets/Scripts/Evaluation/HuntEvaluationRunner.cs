using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NightShift.Hunt;
using NightShift.Hunt.Brain;
using NightShift.Hunt.Survivor;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace NightShift.Evaluation
{
    /// <summary>
    /// Plays a fixed number of nights for every Freddy (trained, scripted, random) against every
    /// survivor preset and writes results.json (docs/ml-design.md, section 10). Runs in the editor
    /// (Evaluation scene) or as a standalone build:
    /// <c>Evaluation.app/Contents/MacOS/Evaluation -nights 400 -results /path/results.json</c>.
    /// </summary>
    public class HuntEvaluationRunner : MonoBehaviour
    {
        public const int ParallelNights = 16;

        [SerializeField] private ModelAsset model;
        [SerializeField] private int nightsPerRun = 400;
        [SerializeField] private string resultsPath = "";

        private readonly List<string> runs = new List<string>();

        public void Configure(ModelAsset trainedModel, int nights)
        {
            model = trainedModel;
            nightsPerRun = nights;
        }

        private void Awake()
        {
            Academy.Instance.AutomaticSteppingEnabled = false;
            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-nights")
                {
                    nightsPerRun = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
                }
                else if (args[i] == "-results")
                {
                    resultsPath = args[i + 1];
                }
            }
            if (string.IsNullOrEmpty(resultsPath))
            {
                // training/evaluation in the main repository, the ignored Builds folder in the game repository.
                string training = Path.GetFullPath(Path.Combine(Application.dataPath, "../../training"));
                resultsPath = Directory.Exists(Path.Combine(training, "config"))
                    ? Path.Combine(training, "evaluation/results.json")
                    : Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/evaluation/results.json"));
            }
        }

        private IEnumerator Start()
        {
            var policies = new List<string>();
            if (model != null)
            {
                policies.Add("trained");
            }
            policies.Add("scripted");
            policies.Add("random");

            foreach (string policy in policies)
            {
                foreach (SurvivorPreset preset in Enum.GetValues(typeof(SurvivorPreset)))
                {
                    yield return Run(policy, preset);
                }
            }
            Write();
            Debug.Log("EVALUATION done: " + resultsPath);
            if (!Application.isEditor)
            {
                Application.Quit();
            }
        }

        private IEnumerator Run(string policyName, SurvivorPreset preset)
        {
            var settings = new HuntSettings();
            var slots = new Slot[ParallelNights];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = new Slot(this, policyName, preset, settings, 1000 + i * 7919);
            }

            var stats = new Stats();
            int started = 0;
            foreach (Slot slot in slots)
            {
                slot.Start(ref started, nightsPerRun);
            }

            while (stats.Nights < nightsPerRun)
            {
                for (int step = 0; step < 200 && stats.Nights < nightsPerRun; step++)
                {
                    foreach (Slot slot in slots)
                    {
                        slot.Tick(stats, ref started, nightsPerRun);
                    }
                    if (policyName == "trained")
                    {
                        Academy.Instance.EnvironmentStep();
                    }
                }
                yield return null;
            }
            foreach (Slot slot in slots)
            {
                slot.Dispose();
            }

            runs.Add(stats.ToJson(policyName, preset.ToString()));
            Debug.Log("EVALUATION " + policyName + " vs " + preset + ": caught " + (100.0 * stats.Caught / stats.Nights).ToString("0.0") + "%");
        }

        private void Write()
        {
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append("  \"generated_at\": \"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)).Append("\",\n");
            json.Append("  \"model\": \"").Append(model != null ? model.name : "").Append("\",\n");
            json.Append("  \"nights_per_run\": ").Append(nightsPerRun).Append(",\n");
            json.Append("  \"runs\": [\n    ").Append(string.Join(",\n    ", runs)).Append("\n  ]\n}\n");
            Directory.CreateDirectory(Path.GetDirectoryName(resultsPath));
            File.WriteAllText(resultsPath, json.ToString());
        }

        /// <summary>One copy of the hunt that plays nights back to back.</summary>
        private class Slot
        {
            private readonly HuntState state;
            private readonly SurvivorBot bot;
            private readonly IFreddyPolicy policy;
            private readonly GameObject agentObject;
            private bool active;
            private int[] searchesBySpot = new int[HuntMap.MaxSpots];
            private int lastTick;

            public Slot(HuntEvaluationRunner runner, string policyName, SurvivorPreset preset, HuntSettings settings, int seed)
            {
                state = new HuntState(settings, seed);
                bot = new SurvivorBot(seed + 1, SurvivorSettings.FromPreset(preset));
                switch (policyName)
                {
                    case "trained":
                        agentObject = new GameObject("Evaluated Freddy");
                        agentObject.transform.SetParent(runner.transform, false);
                        policy = FreddyBrain.Add(agentObject, BehaviorType.InferenceOnly, runner.model);
                        break;
                    case "scripted":
                        policy = new ScriptedFreddyPolicy(seed + 2);
                        break;
                    default:
                        policy = new RandomFreddyPolicy(seed + 2);
                        break;
                }
            }

            public void Start(ref int started, int total)
            {
                active = started < total;
                if (!active)
                {
                    return;
                }
                started++;
                state.Reset();
                bot.OnNightStarted(state);
                policy.OnNightStarted(state);
                Array.Clear(searchesBySpot, 0, searchesBySpot.Length);
                lastTick = 0;
            }

            public void Tick(Stats stats, ref int started, int total)
            {
                if (!active)
                {
                    return;
                }
                // Count the action applied since the last tick (the agent answers on the Academy step).
                if (state.Tick != lastTick && FreddyActions.IsSearch(state.LastFreddyAction))
                {
                    searchesBySpot[FreddyActions.SpotOf(state.LastFreddyAction)]++;
                }
                lastTick = state.Tick;

                if (state.IsOver)
                {
                    policy.OnNightEnded(state, state.Outcome);
                    stats.Add(state, searchesBySpot);
                    Start(ref started, total);
                    if (!active)
                    {
                        return;
                    }
                }

                while (!state.IsOver && !state.FreddyTurn)
                {
                    bot.Act(state);
                }
                if (state.IsOver)
                {
                    return;
                }
                if (state.FreddyCanDecide)
                {
                    policy.Decide(state);
                }
                else
                {
                    state.SkipFreddyTurn();
                }
            }

            public void Dispose()
            {
                if (agentObject != null)
                {
                    Destroy(agentObject);
                }
            }
        }

        private class Stats
        {
            public int Nights;
            public int Caught;
            public int Escaped;
            public int TimeUp;
            private long ticks;
            private long ticksWhenCaught;
            private long searches;
            private long failedSearches;
            private readonly long[] searchesBySpot = new long[HuntMap.MaxSpots];

            public void Add(HuntState state, int[] spots)
            {
                Nights++;
                ticks += state.Tick;
                searches += state.Searches;
                failedSearches += state.FailedSearches;
                for (int i = 0; i < spots.Length; i++)
                {
                    searchesBySpot[i] += spots[i];
                }
                switch (state.Outcome)
                {
                    case HuntOutcome.Caught:
                        Caught++;
                        ticksWhenCaught += state.Tick;
                        break;
                    case HuntOutcome.Escaped: Escaped++; break;
                    default: TimeUp++; break;
                }
            }

            public string ToJson(string policy, string survivor)
            {
                (double low, double high) = Wilson(Caught, Nights);
                long totalSpots = searchesBySpot[0] + searchesBySpot[1] + searchesBySpot[2];
                var c = CultureInfo.InvariantCulture;
                return "{\"policy\": \"" + policy + "\", \"survivor\": \"" + survivor + "\", \"nights\": " + Nights +
                       ", \"catch_rate\": " + Rate(Caught).ToString("0.0000", c) +
                       ", \"catch_ci_low\": " + low.ToString("0.0000", c) +
                       ", \"catch_ci_high\": " + high.ToString("0.0000", c) +
                       ", \"escape_rate\": " + Rate(Escaped).ToString("0.0000", c) +
                       ", \"timeup_rate\": " + Rate(TimeUp).ToString("0.0000", c) +
                       ", \"avg_ticks\": " + ((double)ticks / Math.Max(1, Nights)).ToString("0.00", c) +
                       ", \"avg_ticks_to_catch\": " + ((double)ticksWhenCaught / Math.Max(1, Caught)).ToString("0.00", c) +
                       ", \"avg_searches\": " + ((double)searches / Math.Max(1, Nights)).ToString("0.00", c) +
                       ", \"failed_search_rate\": " + ((double)failedSearches / Math.Max(1, searches)).ToString("0.0000", c) +
                       ", \"search_spot0_share\": " + ((double)searchesBySpot[0] / Math.Max(1, totalSpots)).ToString("0.0000", c) + "}";
            }

            private double Rate(int count) => (double)count / Math.Max(1, Nights);

            /// <summary>95% Wilson score interval for a proportion.</summary>
            private static (double, double) Wilson(int successes, int n)
            {
                if (n == 0)
                {
                    return (0, 0);
                }
                const double z = 1.96;
                double p = (double)successes / n;
                double denominator = 1 + z * z / n;
                double center = (p + z * z / (2 * n)) / denominator;
                double margin = z * Math.Sqrt(p * (1 - p) / n + z * z / (4.0 * n * n)) / denominator;
                return (Math.Max(0, center - margin), Math.Min(1, center + margin));
            }
        }
    }
}
