"""Export the weights of a trained Freddy policy so the game can show its action probabilities.

ML-Agents exports the policy to .onnx with only the chosen action as output. The game also wants
the probability of every action (the AI inspector shows them), so this script copies the weights
of the three dense layers into a small binary file that PolicyProbe.cs reads and evaluates.

Usage (from training/):
    python scripts/export_policy_weights.py ../unity/Assets/ML/Models/freddy_v2.onnx
"""
import argparse
import struct
from pathlib import Path

import numpy as np
import onnx
from onnx import numpy_helper

TRAINING_DIR = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = TRAINING_DIR.parent / "unity" / "Assets" / "Resources" / "FreddyPolicy.bytes"

LAYERS = [
    "network_body._body_endoder.seq_layers.0",
    "network_body._body_endoder.seq_layers.2",
    "action_model._discrete_distribution.branches.0",
]


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("model", type=Path, help="trained .onnx model")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()

    weights = {init.name: numpy_helper.to_array(init) for init in onnx.load(str(args.model)).graph.initializer}
    layers = [(weights[name + ".weight"].astype(np.float32), weights[name + ".bias"].astype(np.float32)) for name in LAYERS]

    # Format: "FPW1", layer count, then per layer: rows, cols, weights (row-major, rows x cols), bias.
    with open(args.output, "wb") as file:
        file.write(b"FPW1")
        file.write(struct.pack("<i", len(layers)))
        for weight, bias in layers:
            rows, cols = weight.shape
            file.write(struct.pack("<ii", rows, cols))
            file.write(weight.astype("<f4").tobytes())
            file.write(bias.astype("<f4").tobytes())
    sizes = " -> ".join([str(layers[0][0].shape[1])] + [str(w.shape[0]) for w, _ in layers])
    print(f"Wrote {args.output} ({sizes})")


if __name__ == "__main__":
    main()
