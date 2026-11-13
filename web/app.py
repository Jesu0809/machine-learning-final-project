import csv
import hashlib
import io
import json
import os
import re
import secrets
import time

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from datetime import timedelta  # noqa: E402

from flask import Flask, Response, g, jsonify, request, send_from_directory  # noqa: E402
from werkzeug.exceptions import HTTPException  # noqa: E402

import auth  # noqa: E402
import db  # noqa: E402
import freddy_brain  # noqa: E402
import saves  # noqa: E402
import studio  # noqa: E402

app = Flask(__name__)


def _secret_key():
    """SECRET_KEY from the host; without it, one derived from the (secret) database address, and in
    local development a random key per run (sessions then end when the server restarts)."""
    if os.environ.get("SECRET_KEY"):
        return os.environ["SECRET_KEY"]
    if db.DATABASE_URL:
        return hashlib.sha256(("night-shift-session:" + db.DATABASE_URL).encode()).hexdigest()
    return secrets.token_hex(32)


app.config.update(
    SECRET_KEY=_secret_key(),
    SESSION_COOKIE_HTTPONLY=True,
    SESSION_COOKIE_SAMESITE="Lax",
    SESSION_COOKIE_SECURE=bool(os.environ.get("RENDER")),
    PERMANENT_SESSION_LIFETIME=timedelta(days=7),
)
app.register_blueprint(auth.blueprint)
app.register_blueprint(studio.blueprint)
app.teardown_appcontext(db.close_connection)

with app.app_context():
    try:
        db.ensure_schema()
        auth.bootstrap_admin()
    finally:
        db.close_connection()


@app.before_request
def start_timer():
    g.started = time.perf_counter()
    # Changes through the API must come as JSON: a plain cross-site form cannot send that.
    if request.path.startswith(("/api/auth/", "/api/studio/")) and request.method in ("POST", "PUT", "DELETE"):
        if request.content_length and not request.is_json:
            return jsonify({"error": "Send JSON."}), 415


@app.after_request
def log_request(response):
    """Every API call is logged with its status and response time (the monitoring page reads them)."""
    if request.path.startswith("/api/") and request.path != "/api/game/status":
        try:
            user = auth.current_user()
            latency = (time.perf_counter() - g.get("started", time.perf_counter())) * 1000
            db.run("INSERT INTO request_log (at, method, path, status, latency_ms, user_id) VALUES (?, ?, ?, ?, ?, ?)",
                   (int(time.time()), request.method, request.path[:200], response.status_code, round(latency, 2),
                    user["id"] if user else None))
        except Exception:  # noqa: BLE001 - logging must never break a response
            pass
    return response


@app.errorhandler(Exception)
def unexpected_error(error):
    if isinstance(error, HTTPException):
        return error
    try:
        user = auth.current_user()
        db.run("INSERT INTO error_log (at, path, message, user_id) VALUES (?, ?, ?, ?)",
               (int(time.time()), request.path[:200], (type(error).__name__ + ": " + str(error))[:1000], user["id"] if user else None))
    except Exception:  # noqa: BLE001
        pass
    app.logger.exception("Unexpected error")
    return jsonify({"error": "Something went wrong on the server. It was logged."}), 500

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
    {"index": _span(0, R), "name": "His own room", "encoding": "one-hot"},
    {"index": _span(freddy_brain.NOISE, R), "name": "Noise memory per room", "encoding": "heard level / 3, fades x0.8 per decision"},
    {"index": _span(freddy_brain.LAST_SEEN, R), "name": "Room where he last saw the player", "encoding": "one-hot (all 0 if never)"},
    {"index": _span(freddy_brain.SINCE_SEEN, 1), "name": "Time since he saw the player", "encoding": "decisions / 60"},
    {"index": _span(freddy_brain.VISITED, R), "name": "Time since he checked each room", "encoding": "decisions / 60"},
    {"index": _span(freddy_brain.SEEN_NOW, 1), "name": "Sees the player right now", "encoding": "0 or 1"},
    {"index": _span(freddy_brain.OBJECTIVES_INDEX, 1), "name": "Objectives the player has done", "encoding": "done / total tonight"},
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
    {"night": 1, "objectives": "3 power cells, exit lever", "freddy": "A decision every 3.5 s, sense every 30 decisions",
     "other": "He explains the rules over the speakers"},
    {"night": 2, "objectives": "Each cell in its own colored socket, exit", "freddy": "3.2 s",
     "other": "3 rubber toys on the floor"},
    {"night": 3, "objectives": "Cells, breaker, exit", "freddy": "2.9 s",
     "other": "4 toys, a surprise box, the dummies turn, 30% of the lights dead"},
    {"night": 4, "objectives": "Cells, breaker, keycard, exit", "freddy": "2.6 s, sense every 20",
     "other": "5 toys, 2 boxes, half the lights dead, tracker 18 m"},
    {"night": 5, "objectives": "Cells, breaker, keycard, exit", "freddy": "2.3 s, sense every 16",
     "other": "The exit takes 45 s to open, with a siren; tracker 15 m"},
    {"night": 6, "objectives": "Cells, breaker, keycard, exit", "freddy": "2.3 s, sense every 16",
     "other": "3 boxes, 90% of the lights dead"},
    {"night": 7, "objectives": "Cells, breaker, keycard, the tape, exit", "freddy": "2.3 s, awake from the start",
     "other": "6 toys, a 60 s exit, the vents bolted shut"},
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
        situation = studio.clean_situation(dict(state, freddy_room=room))
    except ValueError as error:
        return jsonify({"error": str(error)}), 400
    try:
        decision = freddy_brain.decide(situation)
    except RuntimeError as error:
        return jsonify({"error": str(error)}), 500
    # The public demo is part of the inference history too (anonymous unless signed in).
    user = auth.current_user()
    studio.infer(situation, user_id=user["id"] if user else None)
    decision["model_version"] = studio.MODEL_VERSION
    return jsonify(decision)


# ---------------------------------------------------------------- training and evaluation

@app.route("/api/results", methods=["GET"])
def results():
    return jsonify(load_json("results.json"))


@app.route("/api/runs", methods=["GET"])
def runs():
    return jsonify(load_json("runs.json"))


@app.route("/api/plots/training")
def training_plot():
    run = request.args.get("run", "freddy_v19")
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


@app.route("/api/plots/versions")
def versions_plot():
    """Final training reward against the evaluated catch rate (Expert) for every version: a higher
    reward does not always mean a better hunter (v13 is the clearest case)."""
    runs_ = load_json("runs.json")["runs"]
    figure, axes = dark_figure(9, 4.2)
    names = [r["id"].replace("freddy_", "") for r in runs_]
    x = list(range(len(runs_)))
    axes.plot(x, [r["reward_end"] for r in runs_], color="#f2a33a", marker="o", label="Final mean reward (training)")
    axes.set_ylabel("Mean reward per night", color="#f2a33a")
    second = axes.twinx()
    expert = [(i, r["catch_rate"]["Expert"] * 100) for i, r in enumerate(runs_) if r.get("catch_rate")]
    second.plot([i for i, _ in expert], [v for _, v in expert], color=ACCENT, marker="s", linewidth=2, label="Catch rate vs Expert (evaluation)")
    second.set_ylabel("Catch rate vs Expert (%)", color=ACCENT)
    second.tick_params(colors="#e6d6da")
    second.set_ylim(50, 102)
    axes.set_xticks(x)
    axes.set_xticklabels(names, fontsize=8)
    axes.set_title("Training reward vs evaluated catch rate, v1-v19 (versions without a mark were not evaluated)")
    lines = axes.get_legend_handles_labels()
    lines2 = second.get_legend_handles_labels()
    legend = axes.legend(lines[0] + lines2[0], lines[1] + lines2[1], facecolor="#1b1117", edgecolor="#5a3a44", loc="lower right")
    for text in legend.get_texts():
        text.set_color("#e6d6da")
    return png(figure)


@app.route("/api/plots/pca")
def pca_plot():
    """PCA of the network's 100 inputs over 2,000 synthetic situations (seeded), coloured by the
    kind of action the trained network chooses. It shows how much of the input space a few
    directions explain and which situations lead to which decisions."""
    import random as random_module

    import numpy as np

    rng = random_module.Random(2026)
    vectors, kinds = [], []
    for _ in range(2000):
        situation = studio._random_situation(rng)
        decision = freddy_brain.decide(situation)
        vectors.append(freddy_brain.observations(situation))
        index = decision["chosen"]["index"]
        if index == freddy_brain.SENSE:
            kinds.append("Sense")
        elif index >= freddy_brain.ROOM_COUNT:
            kinds.append("Search a spot")
        elif index == situation["freddy_room"]:
            kinds.append("Stay and listen")
        else:
            kinds.append("Move to a room")
    data = np.array(vectors)
    centered = data - data.mean(axis=0)
    # Accelerate's matmul raises spurious floating point warnings on macOS; the result is exact.
    with np.errstate(all="ignore"):
        _, singular, components = np.linalg.svd(centered, full_matrices=False)
        variance = singular ** 2 / (singular ** 2).sum()
        projected = centered @ components[:2].T
    figure, (left, right) = plt.subplots(1, 2, figsize=(11, 4.4), gridspec_kw={"width_ratios": [1.4, 1]})
    figure.patch.set_facecolor("#140c12")
    colors = {"Move to a room": "#4aa3df", "Stay and listen": "#7b8399", "Search a spot": "#f2a33a", "Sense": ACCENT}
    for axes in (left, right):
        axes.set_facecolor("#1b1117")
        for spine in axes.spines.values():
            spine.set_color("#5a3a44")
        axes.tick_params(colors="#e6d6da")
        axes.grid(alpha=0.15)
    for kind, color in colors.items():
        points = projected[[k == kind for k in kinds]]
        left.scatter(points[:, 0], points[:, 1], s=7, alpha=0.6, color=color, label=f"{kind} ({len(points)})")
    left.set_xlabel(f"PC1 ({variance[0]:.1%} of the variance)", color="#e6d6da")
    left.set_ylabel(f"PC2 ({variance[1]:.1%})", color="#e6d6da")
    left.set_title("Inputs projected on 2 principal components", color="#ffffff")
    styled_legend(left)
    cumulative = np.cumsum(variance)[:30] * 100
    right.plot(range(1, 31), cumulative, color=ACCENT, marker="o", markersize=3)
    right.axhline(90, color="#7b8399", linestyle="--", linewidth=1)
    needed = int(np.searchsorted(np.cumsum(variance), 0.9)) + 1
    right.set_title(f"Cumulative variance: {needed} components reach 90%", color="#ffffff")
    right.set_xlabel("Number of components", color="#e6d6da")
    right.set_ylabel("Variance explained (%)", color="#e6d6da")
    figure.tight_layout()
    return png(figure)


# ---------------------------------------------------------------- the game (Unity WebGL build)

# Unity builds compressed with Gzip without "Decompression Fallback" produce *.gz files the
# browser must decompress itself, so they need these headers.
GZIP_CONTENT_TYPES = {
    ".js.gz": "application/javascript",
    ".wasm.gz": "application/wasm",
    ".data.gz": "application/octet-stream",
}


build_version = {"key": None, "value": "0"}


def game_version():
    """A fingerprint of the build's files, computed once per build: any change to any of them
    gives new URLs, so the files themselves can be cached for good."""
    folder = os.path.join(GAME_DIR, "Build")
    if not os.path.isdir(folder):
        return "0"
    files = sorted(os.listdir(folder))
    key = tuple((name, os.path.getsize(os.path.join(folder, name)), os.path.getmtime(os.path.join(folder, name))) for name in files)
    if key != build_version["key"]:
        digest = hashlib.sha1()
        for name in files:
            with open(os.path.join(folder, name), "rb") as handle:
                for block in iter(lambda: handle.read(1 << 20), b""):
                    digest.update(block)
        build_version.update(key=key, value=digest.hexdigest()[:12])
    return build_version["value"]


@app.route("/api/game/status", methods=["GET"])
def game_status():
    loader = os.path.join(GAME_DIR, "Build", "game.loader.js")
    available = os.path.isfile(loader)
    # The page adds the version to the file URLs, so the browser (and Unity's own cache of the
    # data file) never keeps an old build.
    return jsonify({"available": available, "version": game_version()})


@app.route("/game/<path:filename>")
def game_files(filename):
    response = send_from_directory(GAME_DIR, filename)
    # Versioned build files never change under the same URL: the browser and the CDN may keep them.
    versioned = filename.startswith("Build/") and request.args.get("v") == game_version()
    response.headers["Cache-Control"] = "public, max-age=31536000, immutable" if versioned else "no-cache"
    for suffix, content_type in GZIP_CONTENT_TYPES.items():
        if filename.endswith(suffix):
            response.headers["Content-Encoding"] = "gzip"
            response.headers["Content-Type"] = content_type
            break
    return response


# ---------------------------------------------------------------- saves that follow the player

MAX_SAVE_BYTES = 512 * 1024
PLAYER_ID = re.compile(r"^[0-9a-f]{32}$")
# Attempts per address per minute at guessing a link code.
REDEEM_LIMIT = 10
redeem_attempts = {}


def valid_player(player):
    return PLAYER_ID.match(player) is not None


@app.route("/api/save/<player>", methods=["GET"])
def save_get(player):
    if not valid_player(player):
        return jsonify({"error": "bad player"}), 400
    body = saves.load(player)
    if body is None:
        return jsonify({"error": "no save"}), 404
    return Response(body, mimetype="application/json")


@app.route("/api/save/<player>", methods=["PUT"])
def save_put(player):
    if not valid_player(player):
        return jsonify({"error": "bad player"}), 400
    if (request.content_length or 0) > MAX_SAVE_BYTES:
        return jsonify({"error": "too large"}), 413
    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        return jsonify({"error": "not a save"}), 400
    saves.store(player, json.dumps(data))
    # A save of a playtester's account brings its finished nights into the studio.
    imported = studio.import_save(player, data)
    return jsonify({"ok": True, "imported": imported})


@app.route("/api/save/<player>/link", methods=["POST"])
def save_link(player):
    if not valid_player(player):
        return jsonify({"error": "bad player"}), 400
    return jsonify({"code": saves.new_code(player)})


@app.route("/api/save/redeem", methods=["POST"])
def save_redeem():
    address = request.headers.get("X-Forwarded-For", request.remote_addr or "").split(",")[0]
    minute = int(time.time() // 60)
    key = (address, minute)
    redeem_attempts[key] = redeem_attempts.get(key, 0) + 1
    if len(redeem_attempts) > 10000:
        redeem_attempts.clear()
    if redeem_attempts[key] > REDEEM_LIMIT:
        return jsonify({"error": "too many attempts"}), 429
    data = request.get_json(silent=True) or {}
    code = str(data.get("code", "")).strip().upper()
    player = saves.redeem_code(code) if code else None
    if player is None:
        return jsonify({"error": "unknown code"}), 404
    return jsonify({"player": player})


@app.route("/api/save/export", methods=["GET"])
def save_export():
    """Every anonymous save, one JSON per line, for tools/summarize_playlog.py. Needs SAVES_EXPORT_TOKEN."""
    token = os.environ.get("SAVES_EXPORT_TOKEN", "")
    if not token or not secrets.compare_digest(request.headers.get("X-Export-Token", ""), token):
        return jsonify({"error": "forbidden"}), 403
    return Response("\n".join(saves.all_saves()) + "\n", mimetype="application/x-ndjson")


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
