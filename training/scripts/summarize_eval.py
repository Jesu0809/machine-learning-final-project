"""Print the evaluation results as a table and copy them to the website.

Usage (from training/):
    python scripts/summarize_eval.py
"""
import json
import shutil
from pathlib import Path

TRAINING_DIR = Path(__file__).resolve().parents[1]
RESULTS_FILE = TRAINING_DIR / "evaluation" / "results.json"
WEB_COPY = TRAINING_DIR.parent / "web" / "static" / "data" / "results.json"

POLICY_ORDER = ["trained", "scripted", "random"]
PRESET_ORDER = ["Easy", "Normal", "Hard"]


def main():
    with open(RESULTS_FILE, encoding="utf-8") as file:
        results = json.load(file)

    runs = results.get("runs", [])
    if not runs:
        print("results.json has no runs yet. Run the EvaluationRunner scene first.")
        return

    runs.sort(key=lambda run: (PRESET_ORDER.index(run["bot_preset"]),
                               POLICY_ORDER.index(run["policy"])))

    print(f"Model: {results.get('model')}  |  nights per run: {results.get('nights_per_run')}")
    header = f"{'Preset':<8}{'Policy':<10}{'Success':>9}{'Time (s)':>10}{'Blocks':>8}{'Power':>8}"
    print(header)
    print("-" * len(header))
    for run in runs:
        print(f"{run['bot_preset']:<8}{run['policy']:<10}"
              f"{run['success_rate']:>9.1%}{run['avg_time_to_office_s']:>10.1f}"
              f"{run['avg_door_blocks']:>8.2f}{run['avg_power_remaining']:>8.1f}")

    WEB_COPY.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(RESULTS_FILE, WEB_COPY)
    print(f"\nCopied to {WEB_COPY}")


if __name__ == "__main__":
    main()
