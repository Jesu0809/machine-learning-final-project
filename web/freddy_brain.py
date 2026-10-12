"""Freddy's trained brain, run on the server for the "Application" page.

The weights are the same network the game uses (exported from the .onnx model by
training/scripts/export_policy_weights.py). The observation vector is built exactly like
FreddyObservations.cs (54 values) and the legal actions like FreddyActions.cs (13 actions), so the
probabilities shown on the website are the ones the game's Freddy would have in that situation.
"""
import os
import struct

import numpy as np

DATA_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data")
WEIGHTS_FILE = os.path.join(DATA_DIR, "FreddyPolicy.bytes")

ROOMS = ["Office", "West Hall", "Storage", "Kitchen", "Dining Hall", "Arcade", "Stage", "East Hall", "Exit Lobby"]
NEIGHBORS = [
    [1, 3],        # Office
    [0, 2, 4],     # West Hall
    [1, 5],        # Storage
    [0, 4, 6],     # Kitchen
    [1, 3, 5, 7],  # Dining Hall
    [2, 4, 8],     # Arcade
    [3, 7],        # Stage
    [4, 6, 8],     # East Hall
    [5, 7],        # Exit Lobby
]
SPOTS = [2, 1, 3, 3, 3, 2, 2, 1, 1]
SPOT_NAMES = [
    ["locker", "table"], ["locker"], ["locker", "cabinet A", "cabinet B"], ["locker", "cabinet", "table"],
    ["locker", "table A", "table B"], ["locker", "cabinet"], ["locker", "cabinet"], ["locker"], ["locker"],
]
ROOM_COUNT = 9
MAX_SPOTS = 3
ACTION_COUNT = ROOM_COUNT + MAX_SPOTS + 1
SENSE = ROOM_COUNT + MAX_SPOTS

# HuntSettings defaults (the values the brain was trained with).
MEMORY_WINDOW = 60
SENSE_COOLDOWN = 30
MAX_TICKS = 240
FUSES = 3
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
    """Sizes of the network layers, e.g. [54, 256, 256, 13]."""
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
    """The 54 inputs, as FreddyObservations.cs builds them, from a description of what Freddy knows."""
    obs = np.zeros(54, dtype=np.float64)
    room = int(state.get("freddy_room", 6))
    obs[room] = 1.0
    for heard, level in state.get("noises", {}).items():
        obs[9 + int(heard)] = min(1.0, max(0.0, float(level) / 3.0))
    seen = state.get("last_seen_room")
    if seen is not None:
        obs[18 + int(seen)] = 1.0
        obs[27] = min(1.0, float(state.get("seconds_since_seen", 0)) / SECONDS_PER_DECISION / MEMORY_WINDOW)
    else:
        obs[27] = 1.0
    unchecked = set(int(r) for r in state.get("rooms_not_checked", range(ROOM_COUNT)))
    for r in range(ROOM_COUNT):
        obs[28 + r] = 0.0 if r == room else (1.0 if r in unchecked else 0.15)
    obs[37] = 1.0 if state.get("sees_player") else 0.0
    obs[38] = min(1.0, float(state.get("fuses", 0)) / FUSES)
    obs[39] = min(1.0, max(0.0, float(state.get("night_progress", 0.1))))
    for spot in state.get("searched_here", []):
        if 0 <= int(spot) < MAX_SPOTS:
            obs[40 + int(spot)] = 1.0
    obs[43] = 1.0 if state.get("sense_ready", True) else 0.0
    sensed = state.get("sensed_room")
    if sensed is not None:
        obs[44 + int(sensed)] = 1.0
        obs[53] = min(1.0, float(state.get("seconds_since_sensed", 0)) / SECONDS_PER_DECISION / MEMORY_WINDOW)
    else:
        obs[53] = 1.0
    return obs


def legal_actions(state):
    room = int(state.get("freddy_room", 6))
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
    room = int(state.get("freddy_room", 6))
    actions = [
        {"index": int(a), "name": action_name(a, room), "probability": float(probabilities[a])}
        for a in np.argsort(-probabilities) if legal[a]
    ]
    return {"actions": actions, "chosen": actions[0], "inputs": [round(float(v), 3) for v in observations(state)]}
