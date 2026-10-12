"""Print the evaluation results as a table and copy them to the website.

Usage (from training/):
    python scripts/summarize_eval.py
"""
import json
import shutil
from pathlib import Path

TRAINING_DIR = Path(__file__).resolve().parents[1]
RESULTS_FILE = TRAINING_DIR / "evaluation" / "results.json"
WEB_COPY = TRAINING_DIR.parent / "web" / "data" / "results.json"

POLICY_ORDER = ["trained", "scripted", "random"]
SURVIVOR_ORDER = ["Novice", "Normal", "Expert"]


def main():
    with open(RESULTS_FILE, encoding="utf-8") as file:
        results = json.load(file)

    runs = results.get("runs", [])
    if not runs:
        print("results.json has no runs yet. Run the Evaluation build first.")
        return

    runs.sort(key=lambda run: (SURVIVOR_ORDER.index(run["survivor"]),
                               POLICY_ORDER.index(run["policy"])))

    print(f"Model: {results.get('model')}  |  nights per run: {results.get('nights_per_run')}")
    header = f"{'Survivor':<10}{'Freddy':<10}{'Catch':>8}{'95% CI':>16}{'Escape':>8}{'To catch':>10}{'Searches':>10}"
    print(header)
    print("-" * len(header))
    for run in runs:
        ci = f"{run['catch_ci_low']:.1%}-{run['catch_ci_high']:.1%}"
        print(f"{run['survivor']:<10}{run['policy']:<10}{run['catch_rate']:>8.1%}{ci:>16}"
              f"{run['escape_rate']:>8.1%}{run['avg_ticks_to_catch']:>10.1f}{run['avg_searches']:>10.1f}")

    WEB_COPY.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(RESULTS_FILE, WEB_COPY)
    print(f"\nCopied to {WEB_COPY}")


if __name__ == "__main__":
    main()
