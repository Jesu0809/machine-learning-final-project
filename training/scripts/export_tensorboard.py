"""Export the TensorBoard scalars of a training run to a CSV for the website.

Usage (from training/, with the venv active):
    python scripts/export_tensorboard.py monster_v1
"""
import argparse
from pathlib import Path

import pandas as pd
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

TRAINING_DIR = Path(__file__).resolve().parents[1]
RESULTS_DIR = TRAINING_DIR / "results"
DEFAULT_OUTPUT = TRAINING_DIR.parent / "web" / "static" / "data" / "training_curves.csv"
BEHAVIOR_NAME = "MonsterAgent"

# ML-Agents tag -> CSV column
SCALARS = {
    "Environment/Cumulative Reward": "cumulative_reward",
    "Environment/Episode Length": "episode_length",
    "Losses/Policy Loss": "policy_loss",
}


def load_scalars(run_dir):
    accumulator = EventAccumulator(str(run_dir), size_guidance={"scalars": 0})
    accumulator.Reload()
    available = accumulator.Tags()["scalars"]

    columns = []
    for tag, column in SCALARS.items():
        if tag not in available:
            print(f"Warning: '{tag}' not found in {run_dir}")
            continue
        events = accumulator.Scalars(tag)
        columns.append(pd.Series(
            [event.value for event in events],
            index=[event.step for event in events],
            name=column,
        ))

    if not columns:
        raise SystemExit(f"No scalars found in {run_dir}")

    df = pd.concat(columns, axis=1).sort_index()
    df.index.name = "step"
    return df


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("run_id", help="ML-Agents run ID, e.g. monster_v1")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()

    run_dir = RESULTS_DIR / args.run_id / BEHAVIOR_NAME
    if not run_dir.is_dir():
        raise SystemExit(f"Run folder not found: {run_dir}")

    df = load_scalars(run_dir)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    df.to_csv(args.output, float_format="%.5f")
    print(f"Wrote {len(df)} rows to {args.output}")


if __name__ == "__main__":
    main()
