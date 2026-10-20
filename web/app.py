import csv
import io
import json
import os

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from flask import Flask, Response, jsonify, request, send_from_directory  # noqa: E402

import freddy_brain  # noqa: E402

app = Flask(__name__)

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, "data")
GAME_DIR = os.path.join(BASE_DIR, "game")
ANGULAR_DIST_PATH = os.path.join(BASE_DIR, "ml-frontend", "dist", "ml-frontend", "browser")

ACCENT = "#e0313b"
POLICY_COLORS = {"trained": "#e0313b", "scripted": "#f2a33a", "random": "#7b8399"}
POLICY_LABELS = {"trained": "Trained (PPO)", "scripted": "Scripted rules", "random": "Random"}
SURVIVORS = ["Novice", "Normal", "Expert"]

R = freddy_brain.ROOM_COUNT
S = freddy_brain.MAX_SPOTS


def _span(first, count):
    return str(first) if count == 1 else f"{first}-{first + count - 1}"


OBSERVATIONS = [
    {"index": _span(0, R), "name": "Freddy's room", "encoding": "one-hot"},
    {"index": _span(freddy_brain.NOISE, R), "name": "Noise memory per room", "encoding": "heard level / 3, fades x0.8 per decision"},
    {"index": _span(freddy_brain.LAST_SEEN, R), "name": "Room where he last saw the player", "encoding": "one-hot (all 0 if never)"},
    {"index": _span(freddy_brain.SINCE_SEEN, 1), "name": "Time since he saw the player", "encoding": "decisions / 60"},
    {"index": _span(freddy_brain.VISITED, R), "name": "Time since he checked each room", "encoding": "decisions / 60"},
    {"index": _span(freddy_brain.SEEN_NOW, 1), "name": "Sees the player right now", "encoding": "0 or 1"},
    {"index": _span(freddy_brain.FUSES_INDEX, 1), "name": "Fuses the player has", "encoding": "fuses / 3"},
    {"index": _span(freddy_brain.PROGRESS, 1), "name": "Night progress", "encoding": "decision / 240"},
    {"index": _span(freddy_brain.SEARCHED, S), "name": "Hiding spots here already searched", "encoding": "0 or 1 each"},
    {"index": _span(freddy_brain.SENSE_READY, 1), "name": "Sense readiness", "encoding": "1 - cooldown / 30"},
    {"index": _span(freddy_brain.SENSED, R), "name": "Room where sense last found the player", "encoding": "one-hot"},
    {"index": _span(freddy_brain.SINCE_SENSED, 1), "name": "Time since that", "encoding": "decisions / 60"},
]

ACTIONS = [
    {"index": _span(0, R), "name": "Go to room i", "detail": "Only connected rooms (action masking). His own room means stay and listen. A long corridor takes two decisions to walk."},
    {"index": _span(R, S), "name": "Search hiding spot 0, 1 or 2", "detail": "Only spots that exist in his room. 80% chance to find a hidden player in that spot."},
    {"index": str(freddy_brain.SENSE), "name": "Sense", "detail": "Reveals the player's room if their fear is at least 0.5. Recharges for 30 decisions."},
]

REWARDS = [
    {"event": "Catches the survivor", "reward": "+1.0", "kind": "terminal"},
    {"event": "The survivor escapes", "reward": "-1.0", "kind": "terminal"},
    {"event": "The night ends with the survivor inside", "reward": "-0.5", "kind": "terminal"},
    {"event": "Every decision", "reward": "-0.001", "kind": "shaping"},
    {"event": "Searches an empty hiding spot", "reward": "-0.01", "kind": "shaping"},
    {"event": "Searches a spot he already searched", "reward": "-0.02 extra", "kind": "shaping"},
    {"event": "Uses sense and finds nothing", "reward": "-0.01", "kind": "shaping"},
    {"event": "Gets closer to the survivor (potential-based)", "reward": "0.1 (0.99 phi' - phi)", "kind": "shaping"},
]

NIGHTS = [
    {"night": 1, "objectives": "Fuses, exit", "freddy": "A decision every 3.5 s", "other": "Phone call: the rules"},
    {"night": 2, "objectives": "Fuses, breaker, exit", "freddy": "3.2 s, 7% faster", "other": "Fewer party horns"},
    {"night": 3, "objectives": "Fuses, breaker, keycard, exit", "freddy": "2.9 s, 14% faster", "other": "30% of the lights dead"},
    {"night": 4, "objectives": "Fuses, breaker, keycard, exit", "freddy": "2.6 s, 21% faster, sense every 20 decisions", "other": "Half the lights dead, tracker 18 m"},
    {"night": 5, "objectives": "Fuses, breaker, keycard, tape, exit", "freddy": "2.3 s, 28% faster, sense every 16, awake from the start", "other": "Tracker 15 m"},
]


def load_json(name):
    with open(os.path.join(DATA_DIR, name), encoding="utf-8") as file:
        return json.load(file)


def png(figure):
    buffer = io.BytesIO()
    figure.savefig(buffer, format="png", dpi=130, bbox_inches="tight", facecolor=figure.get_facecolor())
    plt.close(figure)
    return Response(buffer.getvalue(), mimetype="image/png", headers={"Cache-Control": "no-store"})


def dark_figure(width, height):
    figure, axes = plt.subplots(figsize=(width, height))
    figure.patch.set_facecolor("#140c12")
    axes.set_facecolor("#1b1117")
    for spine in axes.spines.values():
        spine.set_color("#5a3a44")
    axes.tick_params(colors="#e6d6da")
    axes.xaxis.label.set_color("#e6d6da")
    axes.yaxis.label.set_color("#e6d6da")
    axes.title.set_color("#ffffff")
    axes.grid(alpha=0.15)
    return figure, axes


def styled_legend(axes):
    legend = axes.legend(facecolor="#1b1117", edgecolor="#5a3a44")
    for text in legend.get_texts():
        text.set_color("#e6d6da")


# ---------------------------------------------------------------- the model

@app.route("/api/model/info", methods=["GET"])
def model_info():
    return jsonify({
        "network": freddy_brain.network_info(),
        "rooms": freddy_brain.ROOMS,
        "neighbors": freddy_brain.NEIGHBORS,
        "spots": freddy_brain.SPOT_NAMES,
        "observations": OBSERVATIONS,
        "actions": ACTIONS,
        "rewards": REWARDS,
        "nights": NIGHTS,
        "settings": {
            "seconds_per_decision": freddy_brain.SECONDS_PER_DECISION,
            "max_decisions": freddy_brain.MAX_TICKS,
            "sense_cooldown": freddy_brain.SENSE_COOLDOWN,
            "gamma": 0.99,
            "algorithm": "PPO (Unity ML-Agents 4.1, mlagents 1.1.0)",
        },
    })


@app.route("/api/brain/decide", methods=["POST"])
def brain_decide():
    state = request.get_json(silent=True) or {}
    try:
        room = int(state.get("freddy_room", freddy_brain.FREDDY_START))
        if not 0 <= room < freddy_brain.ROOM_COUNT:
            raise ValueError
    except (TypeError, ValueError):
        return jsonify({"error": f"freddy_room must be a room index 0-{freddy_brain.ROOM_COUNT - 1}."}), 400
    try:
        return jsonify(freddy_brain.decide(state))
    except RuntimeError as error:
        return jsonify({"error": str(error)}), 500


# ---------------------------------------------------------------- training and evaluation

@app.route("/api/results", methods=["GET"])
def results():
    return jsonify(load_json("results.json"))


@app.route("/api/runs", methods=["GET"])
def runs():
    return jsonify(load_json("runs.json"))


@app.route("/api/plots/training")
def training_plot():
    run = request.args.get("run", "freddy_v5")
    path = os.path.join(DATA_DIR, "curves", os.path.basename(run) + ".csv")
    if not os.path.isfile(path):
        return jsonify({"error": "Unknown run."}), 404
    steps, rewards = [], []
    with open(path, newline="", encoding="utf-8") as file:
        for row in csv.DictReader(file):
            if row.get("cumulative_reward"):
                steps.append(float(row["step"]) / 1e6)
                rewards.append(float(row["cumulative_reward"]))
    figure, axes = dark_figure(9, 4)
    axes.plot(steps, rewards, color=ACCENT, linewidth=2)
    axes.axhline(1.0, color="#7b8399", linestyle="--", linewidth=1)
    axes.axhline(-1.0, color="#7b8399", linestyle="--", linewidth=1)
    axes.set_xlabel("Training steps (millions)")
    axes.set_ylabel("Mean reward per night")
    axes.set_title(f"{run}: reward while training (+1 = he catches the survivor, -1 = it escapes)")
    return png(figure)


@app.route("/api/plots/catch-rate")
def catch_rate_plot():
    data = load_json("results.json")
    figure, axes = dark_figure(9, 4.2)
    width = 0.26
    for i, policy in enumerate(["trained", "scripted", "random"]):
        rates, low, high = [], [], []
        for survivor in SURVIVORS:
            run = next((r for r in data["runs"] if r["policy"] == policy and r["survivor"] == survivor), None)
            rate = run["catch_rate"] * 100 if run else 0
            rates.append(rate)
            low.append(rate - run["catch_ci_low"] * 100 if run else 0)
            high.append(run["catch_ci_high"] * 100 - rate if run else 0)
        positions = [x + (i - 1) * width for x in range(len(SURVIVORS))]
        axes.bar(positions, rates, width, label=POLICY_LABELS[policy], color=POLICY_COLORS[policy], yerr=[low, high],
                 ecolor="#ffffff", capsize=4)
    axes.set_xticks(range(len(SURVIVORS)))
    axes.set_xticklabels(SURVIVORS)
    axes.set_ylim(0, 105)
    axes.set_ylabel("Catch rate (%)")
    axes.set_title(f"Catch rate by survivor skill ({data.get('model')}, {data.get('nights_per_run')} nights each, 95% CI)")
    styled_legend(axes)
    return png(figure)


@app.route("/api/plots/sense")
def sense_plot():
    sense = load_json("runs.json")["sense"]
    figure, axes = dark_figure(9, 3.6)
    width = 0.35
    positions = list(range(len(sense)))
    axes.bar([p - width / 2 for p in positions], [s["trained_hit"] * 100 for s in sense], width,
             color=POLICY_COLORS["trained"], label="Trained (PPO)")
    axes.bar([p + width / 2 for p in positions], [s["scripted_hit"] * 100 for s in sense], width,
             color=POLICY_COLORS["scripted"], label="Scripted rules")
    axes.set_xticks(positions)
    axes.set_xticklabels([s["survivor"] for s in sense])
    axes.set_ylim(0, 100)
    axes.set_ylabel("Senses that found the player (%)")
    axes.set_title("When to use SENSE: learned vs hand-written (measured on freddy_v3)")
    styled_legend(axes)
    return png(figure)


# ---------------------------------------------------------------- the game (Unity WebGL build)

# Unity builds compressed with Gzip without "Decompression Fallback" produce *.gz files the
# browser must decompress itself, so they need these headers.
GZIP_CONTENT_TYPES = {
    ".js.gz": "application/javascript",
    ".wasm.gz": "application/wasm",
    ".data.gz": "application/octet-stream",
}


@app.route("/api/game/status", methods=["GET"])
def game_status():
    available = os.path.isfile(os.path.join(GAME_DIR, "Build", "game.loader.js"))
    return jsonify({"available": available})


@app.route("/game/<path:filename>")
def game_files(filename):
    response = send_from_directory(GAME_DIR, filename)
    for suffix, content_type in GZIP_CONTENT_TYPES.items():
        if filename.endswith(suffix):
            response.headers["Content-Encoding"] = "gzip"
            response.headers["Content-Type"] = content_type
            break
    return response


# ---------------------------------------------------------------- the Angular app

@app.route("/")
@app.route("/<path:filename>")
def home(filename="index.html"):
    file_path = os.path.join(ANGULAR_DIST_PATH, filename)
    if filename != "index.html" and not os.path.isfile(file_path):
        filename = "index.html"
    return send_from_directory(ANGULAR_DIST_PATH, filename)


if __name__ == "__main__":
    app.run(debug=True)
