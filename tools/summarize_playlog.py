"""Summarize the game's play logs: how nights really went, to balance the game with real plays.

The game writes one line per night to playlog.jsonl on the player's computer, and keeps the same
nights in the player's anonymous save on the server (GET /api/save/export with X-Export-Token gives
them all, one save per line). This tool reads either file:
    macOS    ~/Library/Application Support/UdeC Machine Learning/Night Shift/playlog.jsonl
    Windows  %USERPROFILE%/AppData/LocalLow/UdeC Machine Learning/Night Shift/playlog.jsonl

Usage:
    python tools/summarize_playlog.py                 # this computer's log
    python tools/summarize_playlog.py a.jsonl b.jsonl # logs sent by testers, one per person
"""
import json
import statistics
import sys
from collections import Counter, defaultdict
from pathlib import Path

DEFAULT = Path.home() / "Library/Application Support/UdeC Machine Learning/Night Shift/playlog.jsonl"


def read(path):
    with open(path, encoding="utf-8") as lines:
        entries = [json.loads(line) for line in lines if line.strip()]
    nights = []
    for entry in entries:
        # A whole save from the server holds its nights in "history".
        nights += entry.get("history", []) if "history" in entry else [entry]
    return nights


def main():
    paths = [Path(arg) for arg in sys.argv[1:]] or [DEFAULT]
    nights = []
    for path in paths:
        if not path.exists():
            print(f"No log at {path}")
            continue
        nights += read(path)
    if not nights:
        return

    by_night = defaultdict(list)
    for entry in nights:
        by_night[entry["night"]].append(entry)
    print(f"{len(nights)} nights from {len(paths)} log(s)\n")
    print("Night  Plays  Escaped  Median time  Objectives  Seen  Threat")
    for night in sorted(by_night):
        plays = by_night[night]
        escaped = sum(entry["outcome"] == "Escaped" for entry in plays) / len(plays)
        minutes = statistics.median(entry["seconds"] for entry in plays) / 60
        done = statistics.mean(entry["objectivesDone"] / max(1, entry["objectivesTotal"]) for entry in plays)
        seen = statistics.mean(entry["seen"] for entry in plays)
        threat = statistics.mean(entry["threatAfter"] for entry in plays)
        print(f"{night:5}  {len(plays):5}  {escaped:6.0%}  {minutes:8.1f} min  {done:9.0%}  {seen:4.1f}  {threat:6.2f}")

    caught = Counter(entry["room"] for entry in nights if entry["outcome"] == "Caught")
    if caught:
        print("\nWhere players are caught: " + ", ".join(f"{room} {count}" for room, count in caught.most_common(5)))


if __name__ == "__main__":
    main()
