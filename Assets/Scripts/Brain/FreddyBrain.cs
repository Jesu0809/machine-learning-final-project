using Unity.InferenceEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace NightShift.Hunt.Brain
{
    /// <summary>
    /// Adds the ML-Agents components that make a GameObject Freddy's brain. Training, evaluation
    /// and the 3D game all use this, so the Behavior Parameters always match the code.
    /// </summary>
    public static class FreddyBrain
    {
        public const string BehaviorName = "Freddy";

        /// <param name="type">Default for training (driven by mlagents-learn), InferenceOnly to run a model.</param>
        /// <param name="model">The trained .onnx model; required for InferenceOnly.</param>
        public static FreddyAgent Add(GameObject go, BehaviorType type, ModelAsset model = null)
        {
            // Behavior Parameters must exist before the agent, which reads them when it initializes.
            var parameters = go.AddComponent<BehaviorParameters>();
            parameters.BehaviorName = BehaviorName;
            parameters.BehaviorType = type;
            parameters.Model = model;
            parameters.InferenceDevice = InferenceDevice.Default;
            parameters.BrainParameters.VectorObservationSize = FreddyObservations.Size;
            parameters.BrainParameters.NumStackedVectorObservations = 1;
            parameters.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(FreddyActions.Count);
            return go.AddComponent<FreddyAgent>();
        }
    }
}
