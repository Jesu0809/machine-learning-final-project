using Unity.MLAgents;
using UnityEngine;

namespace NightShift.Hunt.Training
{
    /// <summary>
    /// Root of the HuntTraining scene. Spawns many copies of the hunt and steps all of them with a
    /// simulated tick instead of real time: one Academy step is one Freddy decision in every copy.
    /// </summary>
    public class HuntTrainingArena : MonoBehaviour
    {
        [SerializeField, Range(1, 64)] private int instanceCount = 32;
        [SerializeField] private int baseSeed = 42;

        [Tooltip("Ticks simulated per frame. Higher trains faster.")]
        [SerializeField, Range(1, 200)] private int ticksPerFrame = 50;

        [SerializeField] private HuntSettings settings = new HuntSettings();

        private float nextLogAt;

        public HuntEnvironment[] Instances { get; private set; }

        private void Awake()
        {
            // The arena decides when the Academy steps, so no physics or real time is involved.
            Academy.Instance.AutomaticSteppingEnabled = false;
            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;

            Instances = new HuntEnvironment[instanceCount];
            for (int i = 0; i < instanceCount; i++)
            {
                Instances[i] = HuntEnvironment.Create(transform, i, settings, baseSeed + i * 7919);
            }
        }

        private void Update()
        {
            for (int t = 0; t < ticksPerFrame; t++)
            {
                StepOnce();
            }

            if (Time.realtimeSinceStartup >= nextLogAt)
            {
                nextLogAt = Time.realtimeSinceStartup + 30f;
                LogProgress();
            }
        }

        /// <summary>One tick in every copy, then one Academy step to decide and apply the actions.</summary>
        public void StepOnce()
        {
            foreach (HuntEnvironment instance in Instances)
            {
                instance.Tick();
            }
            Academy.Instance.EnvironmentStep();
        }

        private void LogProgress()
        {
            int nights = 0;
            int catches = 0;
            foreach (HuntEnvironment instance in Instances)
            {
                nights += instance.NightsPlayed;
                catches += instance.Catches;
            }
            if (nights > 0)
            {
                Debug.Log("HuntTraining: " + nights + " nights, Freddy caught the player in " + (100f * catches / nights).ToString("0.0") + "%");
            }
        }
    }
}
