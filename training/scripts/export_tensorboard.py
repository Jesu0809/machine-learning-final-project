"""Export the TensorBoard scalars of a training run to a CSV for the website.

Usage (from training/, with the venv active):
    python scripts/export_tensorboard.py freddy_v2
"""
import argparse
from pathlib import Path

import pandas as pd
from tensorboard.backend.event_processing.event_accumulator import EventAccumulator

TRAINING_DIR = Path(__file__).resolve().parents[1]
RESULTS_DIR = TRAINING_DIR / "results"
CURVES_DIR = TRAINING_DIR.parent / "web" / "data" / "curves"
BEHAVIOR_NAME = "Freddy"

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
        series = pd.Series(
            [event.value for event in events],
            index=[event.step for event in events],
            name=column,
        )
        # A resumed run (--resume) writes some steps twice; keep the latest value.
        columns.append(series[~series.index.duplicated(keep="last")])

    if not columns:
        raise SystemExit(f"No scalars found in {run_dir}")

    df = pd.concat(columns, axis=1).sort_index()
    df.index.name = "step"
    return df


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("run_id", help="ML-Agents run ID, e.g. freddy_v2")
    parser.add_argument("--output", type=Path, default=None, help="default: web/data/curves/<run_id>.csv")
    args = parser.parse_args()

    run_dir = RESULTS_DIR / args.run_id / BEHAVIOR_NAME
    if not run_dir.is_dir():
        raise SystemExit(f"Run folder not found: {run_dir}")

    df = load_scalars(run_dir)
    if args.output is None:
        args.output = CURVES_DIR / f"{args.run_id}.csv"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    df.to_csv(args.output, float_format="%.5f")
    print(f"Wrote {len(df)} rows to {args.output}")


if __name__ == "__main__":
    main()
