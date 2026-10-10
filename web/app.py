import csv
import json
import os
from flask import Flask, render_template, request

app = Flask(__name__)

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
GAME_BUILD_DIR = os.path.join(BASE_DIR, "static", "game", "Build")
DATA_DIR = os.path.join(BASE_DIR, "static", "data")

# Must match the build name chosen in Unity (Build/<name>.loader.js).
GAME_BUILD_NAME = "game"

ROOMS = [
    {"index": 0, "name": "Stage", "neighbors": "Hall", "distance": 3},
    {"index": 1, "name": "Hall", "neighbors": "Stage, WestCorridor, Kitchen", "distance": 2},
    {"index": 2, "name": "WestCorridor", "neighbors": "Hall, Office (left door)", "distance": 1},
    {"index": 3, "name": "Kitchen", "neighbors": "Hall, EastCorridor", "distance": 2},
    {"index": 4, "name": "EastCorridor", "neighbors": "Kitchen, Office (right door)", "distance": 1},
    {"index": 5, "name": "Office", "neighbors": "terminal", "distance": 0},
]

OBSERVATIONS = [
    {"name": "Current room (one-hot)", "size": 5, "encoding": "1 for the monster's room, 0 for the rest"},
    {"name": "Left door closed", "size": 1, "encoding": "0 or 1"},
    {"name": "Right door closed", "size": 1, "encoding": "0 or 1"},
    {"name": "Power remaining", "size": 1, "encoding": "power / maxPower"},
    {"name": "Night progress", "size": 1, "encoding": "elapsed / nightDuration"},
    {"name": "Time since each camera was viewed", "size": 5, "encoding": "clamp(t / 30 s, 0, 1)"},
    {"name": "Player is watching my room", "size": 1, "encoding": "0 or 1"},
]

REWARDS = [
    {"event": "Moves closer to the office", "reward": "+0.05", "kind": "shaping"},
    {"event": "Moves away from the office", "reward": "−0.05", "kind": "shaping"},
    {"event": "Tries to enter through a closed door (sent back to Hall)", "reward": "−0.3", "kind": "shaping"},
    {"event": "Moves while the player is watching", "reward": "−0.05", "kind": "shaping"},
    {"event": "Every step (time pressure)", "reward": "−0.005", "kind": "shaping"},
    {"event": "Enters the office (episode ends)", "reward": "+1.0", "kind": "terminal"},
    {"event": "Night ends without entering (episode ends)", "reward": "−0.5", "kind": "terminal"},
]

POLICY_LABELS = {"trained": "Trained (PPO)", "scripted": "Scripted", "random": "Random"}
POLICY_ORDER = ["trained", "scripted", "random"]
PRESET_ORDER = ["Easy", "Normal", "Hard"]


def find_game_files():
    """Locate the Unity WebGL build files, whatever compression suffix Unity used."""
    if not os.path.isdir(GAME_BUILD_DIR):
        return None

    names = os.listdir(GAME_BUILD_DIR)

    def first(prefix):
        matches = sorted(n for n in names if n.startswith(prefix))
        return f"game/Build/{matches[0]}" if matches else None

    files = {
        "loader": first(f"{GAME_BUILD_NAME}.loader.js"),
        "data": first(f"{GAME_BUILD_NAME}.data"),
        "framework": first(f"{GAME_BUILD_NAME}.framework.js"),
        "code": first(f"{GAME_BUILD_NAME}.wasm"),
    }
    return files if all(files.values()) else None


def load_results():
    path = os.path.join(DATA_DIR, "results.json")
    if not os.path.isfile(path):
        return None
    with open(path, encoding="utf-8") as file:
        results = json.load(file)
    if not results.get("runs"):
        return None

    results["runs"].sort(key=lambda run: (PRESET_ORDER.index(run["bot_preset"]),
                                          POLICY_ORDER.index(run["policy"])))
    return results


def load_training_curves():
    path = os.path.join(DATA_DIR, "training_curves.csv")
    if not os.path.isfile(path):
        return None

    curves = {"step": [], "cumulative_reward": [], "episode_length": [], "policy_loss": []}
    with open(path, newline="", encoding="utf-8") as file:
        for row in csv.DictReader(file):
            for column in curves:
                value = row.get(column, "")
                curves[column].append(float(value) if value else None)
    return curves if curves["step"] else None


@app.route("/")
def index():
    return render_template("index.html", game=find_game_files())


@app.route("/how-it-works")
def how_it_works():
    return render_template(
        "how_it_works.html",
        rooms=ROOMS,
        observations=OBSERVATIONS,
        rewards=REWARDS,
        curves=load_training_curves(),
    )


@app.route("/results")
def results():
    return render_template(
        "results.html",
        results=load_results(),
        policy_labels=POLICY_LABELS,
        presets=PRESET_ORDER,
    )


# Unity builds compressed with Gzip but without "Decompression Fallback" produce *.gz files that
# the browser must decompress itself, so they need these headers. With the fallback enabled the
# files end in .unityweb and this does nothing.
GZIP_CONTENT_TYPES = {
    ".js.gz": "application/javascript",
    ".wasm.gz": "application/wasm",
    ".data.gz": "application/octet-stream",
}


@app.after_request
def add_gzip_headers(response):
    for suffix, content_type in GZIP_CONTENT_TYPES.items():
        if request.path.endswith(suffix):
            response.headers["Content-Encoding"] = "gzip"
            response.headers["Content-Type"] = content_type
            break
    return response


@app.errorhandler(404)
def not_found(error):
    return render_template("404.html"), 404


if __name__ == "__main__":
    app.run(debug=True)
