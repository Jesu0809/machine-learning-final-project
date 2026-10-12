using System;
using System.IO;

namespace NightShift.Hunt
{
    /// <summary>
    /// Recomputes the trained policy's action probabilities from its weights (exported by
    /// training/scripts/export_policy_weights.py). The game still acts with the .onnx model through
    /// ML-Agents; this only lets the AI inspector show how sure Freddy is of each option.
    /// </summary>
    /// <remarks>
    /// Same network as the ML-Agents export: two dense layers with Swish (x·sigmoid(x)), a dense
    /// output layer, illegal actions masked with −1e8, then softmax.
    /// </remarks>
    public class PolicyProbe
    {
        private readonly float[][] weights;
        private readonly float[][] biases;
        private readonly int[] rows;
        private readonly int[] cols;
        private readonly float[][] activations;

        public int InputSize => cols[0];
        public int OutputSize => rows[rows.Length - 1];

        private PolicyProbe(int layers)
        {
            weights = new float[layers][];
            biases = new float[layers][];
            rows = new int[layers];
            cols = new int[layers];
            activations = new float[layers][];
        }

        public static PolicyProbe FromBytes(byte[] data)
        {
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                if (new string(reader.ReadChars(4)) != "FPW1")
                {
                    throw new InvalidDataException("Not a policy weights file.");
                }
                int layers = reader.ReadInt32();
                var probe = new PolicyProbe(layers);
                for (int l = 0; l < layers; l++)
                {
                    probe.rows[l] = reader.ReadInt32();
                    probe.cols[l] = reader.ReadInt32();
                    probe.weights[l] = ReadFloats(reader, probe.rows[l] * probe.cols[l]);
                    probe.biases[l] = ReadFloats(reader, probe.rows[l]);
                    probe.activations[l] = new float[probe.rows[l]];
                }
                return probe;
            }
        }

        /// <summary>Probabilities of every action for these observations (illegal actions get 0).</summary>
        public void Evaluate(float[] observations, bool[] legal, float[] probabilities)
        {
            float[] input = observations;
            int last = weights.Length - 1;
            for (int l = 0; l <= last; l++)
            {
                float[] output = activations[l];
                float[] w = weights[l];
                int n = cols[l];
                for (int r = 0; r < rows[l]; r++)
                {
                    float sum = biases[l][r];
                    int offset = r * n;
                    for (int c = 0; c < n; c++)
                    {
                        sum += w[offset + c] * input[c];
                    }
                    // Swish on the hidden layers, raw logits on the output.
                    output[r] = l < last ? sum / (1f + (float)Math.Exp(-sum)) : sum;
                }
                input = output;
            }

            float max = float.NegativeInfinity;
            for (int a = 0; a < OutputSize; a++)
            {
                if (legal[a] && input[a] > max)
                {
                    max = input[a];
                }
            }
            float total = 0f;
            for (int a = 0; a < OutputSize; a++)
            {
                probabilities[a] = legal[a] ? (float)Math.Exp(input[a] - max) : 0f;
                total += probabilities[a];
            }
            for (int a = 0; a < OutputSize; a++)
            {
                probabilities[a] /= total;
            }
        }

        private static float[] ReadFloats(BinaryReader reader, int count)
        {
            var values = new float[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = reader.ReadSingle();
            }
            return values;
        }
    }
}
