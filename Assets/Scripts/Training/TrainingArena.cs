using System;
using UnityEngine;

namespace NightShift.Training
{
    /// <summary>
    /// Root of the TrainingArena scene. Spawns <see cref="instanceCount"/> environment copies and
    /// steps all of them with a fixed simulated tick instead of real time, which is what makes
    /// training fast.
    /// </summary>
    /// <remarks>
    /// Turns off Academy automatic stepping and calls Academy.Instance.EnvironmentStep() after
    /// ticking every instance, so one Academy step equals one monster decision in every copy.
    /// </remarks>
    public class TrainingArena : MonoBehaviour
    {
        [SerializeField] private EnvironmentInstance instancePrefab;
        [SerializeField, Range(1, 64)] private int instanceCount = 16;
        [SerializeField] private int baseSeed = 42;

        [Tooltip("Monster ticks simulated per FixedUpdate. Raise it to train faster.")]
        [SerializeField, Range(1, 50)] private int ticksPerFixedUpdate = 1;

        /// <summary>The spawned copies.</summary>
        public EnvironmentInstance[] Instances { get; private set; }
    }
}
