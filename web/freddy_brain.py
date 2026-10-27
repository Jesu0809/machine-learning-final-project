"""Freddy's trained brain, run on the server for the "Application" page.

The weights are the same network the game uses (exported from the .onnx model by
training/scripts/export_policy_weights.py). The observation vector is built exactly like
FreddyObservations.cs (74 values) and the legal actions like FreddyActions.cs (17 actions), so the
probabilities shown on the website are the ones the game's Freddy would have in that situation.
"""
import os
import struct

import numpy as np

DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data")
WEIGHTS_FILE = os.path.join(DATA_DIR, "FreddyPolicy.bytes")

# The map of HuntMap.cs: a staff wing (west) and a public wing (east), joined only through the
# north wing (Banquet Room and Veranda).
ROOMS = [
    "Office", "Parts & Service", "Restrooms", "Back Hall", "Storage", "Kitchen",
    "Dining Hall", "Main Hall", "Party Room", "Arcade", "Exit Lobby", "Banquet Room", "Veranda",
]
NEIGHBORS = [
    [1, 3],              # Office
    [0, 3],              # Parts & Service
    [3],                 # Restrooms
    [0, 1, 2, 5],        # Back Hall
    [5],                 # Storage
    [3, 4, 11],          # Kitchen
    [9],                 # Dining Hall
    [8, 9],              # Main Hall
    [7, 10],             # Party Room
    [6, 7, 12],          # Arcade
    [8],                 # Exit Lobby
    [5, 12],             # Banquet Room
    [11, 9],             # Veranda
]
SPOT_NAMES = [
    ["locker", "table"], ["locker", "cabinet"], ["locker", "cabinet"], ["locker"],
    ["locker", "cabinet A", "cabinet B"], ["locker", "cabinet", "table"], ["locker", "table A", "table B"],
    ["locker"], ["locker", "table"], ["locker", "cabinet"], ["locker"],
    ["table A", "table B", "table C"], ["table A", "table B"],
]
SPOTS = [len(names) for names in SPOT_NAMES]
ROOM_COUNT = len(ROOMS)
MAX_SPOTS = 3
ACTION_COUNT = ROOM_COUNT + MAX_SPOTS + 1
SENSE = ROOM_COUNT + MAX_SPOTS
FREDDY_START = 6  # asleep on the stage of the Dining Hall

# Offsets of the observation vector (FreddyObservations.cs): 5 R + 6 + MAX_SPOTS values.
R = ROOM_COUNT
OBS_SIZE = 5 * R + 6 + MAX_SPOTS
NOISE = R
LAST_SEEN = 2 * R
SINCE_SEEN = 3 * R
VISITED = 3 * R + 1
SEEN_NOW = 4 * R + 1
OBJECTIVES_INDEX = 4 * R + 2
PROGRESS = 4 * R + 3
SEARCHED = 4 * R + 4
SENSE_READY = SEARCHED + MAX_SPOTS
SENSED = SENSE_READY + 1
SINCE_SENSED = SENSED + R

# HuntSettings defaults (the values the brain was trained with).
MEMORY_WINDOW = 60
SENSE_COOLDOWN = 30
MAX_TICKS = 240
SECONDS_PER_DECISION = 3.5


def _load_layers():
    with open(WEIGHTS_FILE, "rb") as file:
        if file.read(4) != b"FPW1":
            raise ValueError("Not a policy weights file")
        (count,) = struct.unpack("<i", file.read(4))
        layers = []
        for _ in range(count):
            rows, cols = struct.unpack("<ii", file.read(8))
            weights = np.frombuffer(file.read(rows * cols * 4), dtype="<f4").reshape(rows, cols).astype(np.float64)
            bias = np.frombuffer(file.read(rows * 4), dtype="<f4").astype(np.float64)
            layers.append((weights, bias))
    return layers


_LAYERS = _load_layers() if os.path.isfile(WEIGHTS_FILE) else None


def network_info():
    """Sizes of the network layers, e.g. [74, 256, 256, 17]."""
    if _LAYERS is None:
        return []
    return [int(_LAYERS[0][0].shape[1])] + [int(weights.shape[0]) for weights, _ in _LAYERS]


def action_name(action, room):
    if action == SENSE:
        return "Sense (feel for fear)"
    if action >= ROOM_COUNT:
        spot = action - ROOM_COUNT
        return "Search " + SPOT_NAMES[room][spot]
    return "Stay and listen" if action == room else "Go to " + ROOMS[action]


def observations(state):
    """The 74 inputs, as FreddyObservations.cs builds them, from a description of what Freddy knows."""
    obs = np.zeros(OBS_SIZE, dtype=np.float64)
    room = int(state.get("freddy_room", FREDDY_START))
    obs[room] = 1.0
    for heard, level in state.get("noises", {}).items():
        obs[NOISE + int(heard)] = min(1.0, max(0.0, float(level) / 3.0))
    seen = state.get("last_seen_room")
    if seen is not None:
        obs[LAST_SEEN + int(seen)] = 1.0
        obs[SINCE_SEEN] = min(1.0, float(state.get("seconds_since_seen", 0)) / SECONDS_PER_DECISION / MEMORY_WINDOW)
    else:
        obs[SINCE_SEEN] = 1.0
    unchecked = set(int(r) for r in state.get("rooms_not_checked", range(ROOM_COUNT)))
    for r in range(ROOM_COUNT):
        obs[VISITED + r] = 0.0 if r == room else (1.0 if r in unchecked else 0.15)
    obs[SEEN_NOW] = 1.0 if state.get("sees_player") else 0.0
    # Share of tonight's objectives done (cells, breakers, keycard, tape); "fuses" (0-3) is still
    # accepted from older clients and read as the three power cells.
    if "objectives" in state:
        done = float(state.get("objectives", 0))
    else:
        done = float(state.get("fuses", 0)) / 3.0
    obs[OBJECTIVES_INDEX] = min(1.0, max(0.0, done))
    obs[PROGRESS] = min(1.0, max(0.0, float(state.get("night_progress", 0.1))))
    for spot in state.get("searched_here", []):
        if 0 <= int(spot) < MAX_SPOTS:
            obs[SEARCHED + int(spot)] = 1.0
    obs[SENSE_READY] = 1.0 if state.get("sense_ready", True) else 0.0
    sensed = state.get("sensed_room")
    if sensed is not None:
        obs[SENSED + int(sensed)] = 1.0
        obs[SINCE_SENSED] = min(1.0, float(state.get("seconds_since_sensed", 0)) / SECONDS_PER_DECISION / MEMORY_WINDOW)
    else:
        obs[SINCE_SENSED] = 1.0
    return obs


def legal_actions(state):
    room = int(state.get("freddy_room", FREDDY_START))
    legal = np.zeros(ACTION_COUNT, dtype=bool)
    legal[room] = True
    for neighbor in NEIGHBORS[room]:
        legal[neighbor] = True
    for spot in range(SPOTS[room]):
        legal[ROOM_COUNT + spot] = True
    legal[SENSE] = bool(state.get("sense_ready", True))
    return legal


def decide(state):
    """Probabilities of every legal action for this situation, highest first."""
    if _LAYERS is None:
        raise RuntimeError("FreddyPolicy.bytes is missing in web/data")
    x = observations(state)
    legal = legal_actions(state)
    # Accelerate's matmul can raise spurious floating point warnings on macOS; the result is exact.
    with np.errstate(all="ignore"):
        for i, (weights, bias) in enumerate(_LAYERS):
            x = weights @ x + bias
            if i < len(_LAYERS) - 1:
                x = x / (1.0 + np.exp(-x))  # Swish, as in the ML-Agents network
    logits = np.where(legal, x, -np.inf)
    probabilities = np.exp(logits - logits[legal].max())
    probabilities /= probabilities.sum()
    room = int(state.get("freddy_room", FREDDY_START))
    actions = [
        {"index": int(a), "name": action_name(a, room), "probability": float(probabilities[a])}
        for a in np.argsort(-probabilities) if legal[a]
    ]
    return {"actions": actions, "chosen": actions[0], "inputs": [round(float(v), 3) for v in observations(state)]}
