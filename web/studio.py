"""The playtest studio: the business processes of the information system.

1. Playtest sessions. Playtesters play the game with their account; every night they finish is
   imported from the game's save (source "game"), and they can also record one by hand.
2. Behaviour reports (tickets). A playtester reports something Pockets did, with the situation
   he was in. A designer runs the trained network on that situation (server-side inference): if
   the network itself would likely make that move, the report is "expected behaviour"; if it is
   unlikely or impossible for the brain, it is probably a bug in the scripted body or the level.
   The designer confirms or overrides the suggestion, and bugs are resolved when fixed.
3. Indicators, reports, users and monitoring for designers and administrators.

Every inference is stored with the user, the date, the result and the model version.
"""
import csv
import hashlib
import io
import json
import math
import os
import random
import statistics
import time

from flask import Blueprint, Response, jsonify, request

import db
import freddy_brain
from auth import ROLES, current_user, has_role, public, require

blueprint = Blueprint("studio", __name__, url_prefix="/api/studio")

OUTCOMES = ("Caught", "Escaped", "TimeUp", "Quit")
KINDS = ("behaviour", "bug", "feedback")
STATUSES = ("open", "reviewed", "expected", "bug", "resolved")
# A move the network gives at least this probability is something the brain itself would do.
EXPECTED_THRESHOLD = 0.15
# Monitoring thresholds (see the CRISP-ML Monitoring page).
ALERT_ERROR_RATE = 0.05
ALERT_P95_MS = 1500
ALERT_AGREEMENT = 0.70
ALERT_PSI = 0.25
ALERT_CATCH_LOW = 0.40
ALERT_CATCH_HIGH = 0.95


def _model_version():
    digest = hashlib.sha1()
    with open(freddy_brain.WEIGHTS_FILE, "rb") as handle:
        digest.update(handle.read())
    return "freddy_v19 (weights " + digest.hexdigest()[:8] + ")"


MODEL_VERSION = _model_version() if os.path.isfile(freddy_brain.WEIGHTS_FILE) else "unavailable"


def now():
    return int(time.time())


def bad(message, status=400):
    return jsonify({"error": message}), status


def _int(value, low, high, name):
    try:
        number = int(value)
    except (TypeError, ValueError):
        raise ValueError(f"{name} must be a whole number.")
    if not low <= number <= high:
        raise ValueError(f"{name} must be between {low} and {high}.")
    return number


def _float(value, low, high, name):
    try:
        number = float(value)
    except (TypeError, ValueError):
        raise ValueError(f"{name} must be a number.")
    if math.isnan(number) or not low <= number <= high:
        raise ValueError(f"{name} must be between {low} and {high}.")
    return number


def _text(value, name, low=0, high=2000):
    text = str(value or "").strip()
    if not low <= len(text) <= high:
        raise ValueError(f"{name} needs {low} to {high} characters.")
    return text


# ---------------------------------------------------------------- the situation the model reads

def clean_situation(raw):
    """The same input structure the model was trained on (freddy_brain.observations), validated."""
    if not isinstance(raw, dict):
        raise ValueError("The situation is missing.")
    rooms = freddy_brain.ROOM_COUNT
    situation = {"freddy_room": _int(raw.get("freddy_room"), 0, rooms - 1, "Pockets' room")}
    noises = {}
    for room, level in (raw.get("noises") or {}).items():
        room = _int(room, 0, rooms - 1, "A noise room")
        level = _float(level, 0, 3, "A noise level")
        if level > 0:
            noises[str(room)] = level
    situation["noises"] = noises
    if raw.get("last_seen_room") not in (None, ""):
        situation["last_seen_room"] = _int(raw.get("last_seen_room"), 0, rooms - 1, "Last seen room")
        situation["seconds_since_seen"] = _float(raw.get("seconds_since_seen", 0), 0, 3600, "Seconds since seen")
    unchecked = raw.get("rooms_not_checked")
    if unchecked is None:
        unchecked = list(range(rooms))
    situation["rooms_not_checked"] = sorted({_int(r, 0, rooms - 1, "A room") for r in unchecked})
    situation["sees_player"] = bool(raw.get("sees_player"))
    if "objectives" not in raw and "fuses" in raw:
        # Older clients send the three power cells as "fuses" (0-3).
        raw = dict(raw, objectives=_float(raw.get("fuses"), 0, 3, "Power cells") / 3)
    situation["objectives"] = _float(raw.get("objectives", 0), 0, 1, "Objectives done")
    situation["night_progress"] = _float(raw.get("night_progress", 0.3), 0, 1, "Night progress")
    situation["searched_here"] = sorted({_int(s, 0, freddy_brain.MAX_SPOTS - 1, "A hiding spot") for s in raw.get("searched_here") or []})
    situation["sense_ready"] = bool(raw.get("sense_ready", True))
    if raw.get("sensed_room") not in (None, ""):
        situation["sensed_room"] = _int(raw.get("sensed_room"), 0, rooms - 1, "Sensed room")
        situation["seconds_since_sensed"] = _float(raw.get("seconds_since_sensed", 0), 0, 3600, "Seconds since sensed")
    return situation


def judge(decision, observed):
    """What the model says about the move the playtester saw."""
    if observed is None:
        return None, None
    probability = next((a["probability"] for a in decision["actions"] if a["index"] == observed), None)
    if probability is None:
        return 0.0, "impossible"
    return probability, ("expected" if probability >= EXPECTED_THRESHOLD else "unlikely")


def infer(situation, observed=None, ticket_id=None, simulated=False, user_id=None):
    started = time.perf_counter()
    decision = freddy_brain.decide(situation)
    latency = (time.perf_counter() - started) * 1000
    probability, verdict = judge(decision, observed)
    top = decision["actions"][0]
    inference_id = db.run(
        "INSERT INTO inferences (user_id, ticket_id, model_version, situation, result, top_action, top_probability, "
        "observed_action, observed_probability, verdict, latency_ms, simulated, created_at) "
        "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?) RETURNING id",
        (user_id, ticket_id, MODEL_VERSION, json.dumps(situation), json.dumps(decision["actions"][:6]), top["name"],
         top["probability"], observed, probability, verdict, latency, 1 if simulated else 0, now()),
    )
    return inference_id, decision, probability, verdict, latency


def action_label(index, room):
    return freddy_brain.action_name(index, room) if index is not None else None


# ---------------------------------------------------------------- playtest sessions

PLAYTEST_FIELDS = ("night", "mode", "outcome", "cause", "room", "seconds", "objectives_done", "objectives_total", "heard",
                   "seen", "searches", "sense_uses", "sense_hits", "decisions", "threat_before", "threat_after", "notes")


def clean_playtest(data):
    clean = {
        "night": _int(data.get("night"), 1, 7, "Night"),
        "mode": _text(data.get("mode", "Story"), "Mode", 0, 30),
        "outcome": str(data.get("outcome", "")),
        "cause": _text(data.get("cause", ""), "Cause", 0, 200),
        "room": _text(data.get("room", ""), "Room", 0, 60),
        "seconds": _float(data.get("seconds", 0), 0, 7200, "Seconds survived"),
        "objectives_done": _int(data.get("objectives_done", 0), 0, 20, "Objectives done"),
        "objectives_total": _int(data.get("objectives_total", 0), 0, 20, "Objectives in the night"),
        "notes": _text(data.get("notes", ""), "Notes", 0, 1000),
    }
    if clean["outcome"] not in OUTCOMES:
        raise ValueError("The outcome must be Caught, Escaped, TimeUp or Quit.")
    if clean["objectives_done"] > clean["objectives_total"]:
        raise ValueError("Objectives done cannot be more than the objectives of the night.")
    for name in ("heard", "seen", "searches", "sense_uses", "sense_hits", "decisions"):
        clean[name] = _int(data.get(name, 0), 0, 10000, name.replace("_", " ").capitalize())
    if clean["sense_hits"] > clean["sense_uses"]:
        raise ValueError("Sense hits cannot be more than sense uses.")
    for name in ("threat_before", "threat_after"):
        clean[name] = None if data.get(name) in (None, "") else _float(data.get(name), 0, 1, "Threat level")
    return clean


def import_save(player, body):
    """Copies the nights in a game save into the account that owns this player id."""
    user = db.one("SELECT id FROM users WHERE player_id = ?", (player,))
    if user is None or not isinstance(body, dict):
        return 0
    added = 0
    for night in (body.get("history") or [])[-200:]:
        if not isinstance(night, dict):
            continue
        fingerprint = hashlib.sha1(json.dumps([player, night.get("date"), night.get("night"), night.get("outcome"),
                                               night.get("seconds")], sort_keys=True).encode()).hexdigest()[:24]
        if db.one("SELECT id FROM playtests WHERE fingerprint = ?", (fingerprint,)):
            continue
        try:
            clean = clean_playtest({
                "night": night.get("night"), "mode": night.get("mode", "Story"), "outcome": night.get("outcome"),
                "cause": night.get("how", ""), "room": night.get("room", ""), "seconds": night.get("seconds", 0),
                "objectives_done": night.get("objectivesDone", 0), "objectives_total": night.get("objectivesTotal", 0),
                "heard": night.get("heard", 0), "seen": night.get("seen", 0), "searches": night.get("searches", 0),
                "sense_uses": night.get("senseUses", 0), "sense_hits": night.get("senseHits", 0),
                "decisions": night.get("decisions", 0), "threat_before": night.get("threatBefore"),
                "threat_after": night.get("threatAfter"),
            })
        except ValueError:
            continue
        db.run(
            "INSERT INTO playtests (user_id, source, fingerprint, played_at, device, brain, created_at, "
            + ", ".join(PLAYTEST_FIELDS) + ") VALUES (?, 'game', ?, ?, ?, ?, ?, " + ", ".join("?" * len(PLAYTEST_FIELDS)) + ")",
            (user["id"], fingerprint, str(night.get("date") or "")[:32], str(night.get("device") or "")[:40],
             str(night.get("brain") or "")[:80], now()) + tuple(clean[f] for f in PLAYTEST_FIELDS),
        )
        added += 1
    return added


def playtest_row(row):
    row = dict(row)
    row["simulated"] = bool(row["simulated"])
    return row


@blueprint.get("/playtests")
@require()
def list_playtests():
    user = current_user()
    mine = request.args.get("mine") == "1" or not has_role("designer")
    where, params = ["1 = 1"], []
    if mine:
        where.append("p.user_id = ?")
        params.append(user["id"])
    if request.args.get("night"):
        where.append("p.night = ?")
        params.append(int(request.args["night"]))
    if request.args.get("outcome") in OUTCOMES:
        where.append("p.outcome = ?")
        params.append(request.args["outcome"])
    rows = db.all("SELECT p.*, u.username FROM playtests p JOIN users u ON u.id = p.user_id WHERE " + " AND ".join(where)
                  + " ORDER BY p.played_at DESC, p.id DESC LIMIT 500", tuple(params))
    return jsonify({"playtests": [playtest_row(r) for r in rows]})


def _own_playtest(playtest_id):
    row = db.one("SELECT * FROM playtests WHERE id = ?", (playtest_id,))
    if row is None:
        return None, bad("Playtest not found.", 404)
    user = current_user()
    if row["user_id"] != user["id"] and not has_role("designer"):
        return None, bad("This playtest belongs to someone else.", 403)
    return row, None


@blueprint.get("/playtests/<int:playtest_id>")
@require()
def get_playtest(playtest_id):
    row, error = _own_playtest(playtest_id)
    if error:
        return error
    tickets = db.all("SELECT id, title, kind, status FROM tickets WHERE playtest_id = ?", (playtest_id,))
    return jsonify({"playtest": playtest_row(row), "tickets": tickets})


@blueprint.post("/playtests")
@require()
def create_playtest():
    try:
        clean = clean_playtest(request.get_json(silent=True) or {})
    except ValueError as problem:
        return bad(str(problem))
    played_at = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    new_id = db.run(
        "INSERT INTO playtests (user_id, source, played_at, device, created_at, " + ", ".join(PLAYTEST_FIELDS)
        + ") VALUES (?, 'manual', ?, 'entered by hand', ?, " + ", ".join("?" * len(PLAYTEST_FIELDS)) + ") RETURNING id",
        (current_user()["id"], played_at, now()) + tuple(clean[f] for f in PLAYTEST_FIELDS),
    )
    return jsonify({"playtest": playtest_row(db.one("SELECT * FROM playtests WHERE id = ?", (new_id,)))}), 201


@blueprint.put("/playtests/<int:playtest_id>")
@require()
def update_playtest(playtest_id):
    row, error = _own_playtest(playtest_id)
    if error:
        return error
    if row["user_id"] != current_user()["id"] and not has_role("admin"):
        return bad("Only its playtester or an admin can edit a playtest.", 403)
    data = request.get_json(silent=True) or {}
    try:
        if row["source"] == "game":
            # What the game measured is evidence: only the notes can change.
            db.run("UPDATE playtests SET notes = ? WHERE id = ?", (_text(data.get("notes", ""), "Notes", 0, 1000), playtest_id))
        else:
            clean = clean_playtest(data)
            db.run("UPDATE playtests SET " + ", ".join(f"{f} = ?" for f in PLAYTEST_FIELDS) + " WHERE id = ?",
                   tuple(clean[f] for f in PLAYTEST_FIELDS) + (playtest_id,))
    except ValueError as problem:
        return bad(str(problem))
    return jsonify({"playtest": playtest_row(db.one("SELECT * FROM playtests WHERE id = ?", (playtest_id,)))})


@blueprint.delete("/playtests/<int:playtest_id>")
@require()
def delete_playtest(playtest_id):
    row, error = _own_playtest(playtest_id)
    if error:
        return error
    if row["user_id"] != current_user()["id"] and not has_role("admin"):
        return bad("Only its playtester or an admin can delete a playtest.", 403)
    db.run("UPDATE tickets SET playtest_id = NULL WHERE playtest_id = ?", (playtest_id,))
    db.run("DELETE FROM playtests WHERE id = ?", (playtest_id,))
    return jsonify({"ok": True})


# ---------------------------------------------------------------- behaviour reports

def ticket_view(row, with_inference=True):
    ticket = dict(row)
    ticket["situation"] = json.loads(row["situation"]) if row.get("situation") else None
    ticket["simulated"] = bool(row["simulated"])
    room = ticket["situation"]["freddy_room"] if ticket["situation"] else None
    ticket["observed_label"] = action_label(ticket["observed_action"], room) if room is not None else None
    if with_inference and row.get("inference_id"):
        inference = db.one("SELECT * FROM inferences WHERE id = ?", (row["inference_id"],))
        if inference:
            inference["result"] = json.loads(inference["result"])
            inference["situation"] = json.loads(inference["situation"])
            ticket["inference"] = inference
    ticket["suggestion"] = {"expected": "expected", "unlikely": "bug", "impossible": "bug"}.get(ticket.get("ai_verdict"))
    return ticket


def _ticket(ticket_id):
    row = db.one("SELECT t.*, r.username AS reporter, d.username AS designer FROM tickets t JOIN users r ON r.id = t.reporter_id "
                 "LEFT JOIN users d ON d.id = t.designer_id WHERE t.id = ?", (ticket_id,))
    if row is None:
        return None, bad("Report not found.", 404)
    if row["reporter_id"] != current_user()["id"] and not has_role("designer"):
        return None, bad("This report belongs to someone else.", 403)
    return row, None


def clean_ticket(data):
    kind = str(data.get("kind", "behaviour"))
    if kind not in KINDS:
        raise ValueError("The kind must be behaviour, bug or feedback.")
    clean = {
        "kind": kind,
        "title": _text(data.get("title"), "The title", 5, 120),
        "description": _text(data.get("description"), "The description", 10, 2000),
        "night": None if data.get("night") in (None, "") else _int(data.get("night"), 1, 7, "Night"),
        "playtest_id": None if data.get("playtest_id") in (None, "") else _int(data.get("playtest_id"), 1, 10 ** 9, "Playtest"),
        "situation": None,
        "observed_action": None,
    }
    if kind == "behaviour":
        situation = clean_situation(data.get("situation"))
        observed = _int(data.get("observed_action"), 0, freddy_brain.ACTION_COUNT - 1, "What Pockets did")
        clean["situation"] = json.dumps(situation)
        clean["observed_action"] = observed
    if clean["playtest_id"] is not None:
        owner = db.one("SELECT user_id FROM playtests WHERE id = ?", (clean["playtest_id"],))
        if owner is None or (owner["user_id"] != current_user()["id"] and not has_role("designer")):
            raise ValueError("That playtest is not yours.")
    return clean


TICKET_FIELDS = ("kind", "title", "description", "night", "playtest_id", "situation", "observed_action")


@blueprint.get("/tickets")
@require()
def list_tickets():
    where, params = ["1 = 1"], []
    if request.args.get("mine") == "1" or not has_role("designer"):
        where.append("t.reporter_id = ?")
        params.append(current_user()["id"])
    if request.args.get("status") in STATUSES:
        where.append("t.status = ?")
        params.append(request.args["status"])
    if request.args.get("kind") in KINDS:
        where.append("t.kind = ?")
        params.append(request.args["kind"])
    rows = db.all("SELECT t.*, r.username AS reporter, d.username AS designer FROM tickets t JOIN users r ON r.id = t.reporter_id "
                  "LEFT JOIN users d ON d.id = t.designer_id WHERE " + " AND ".join(where) + " ORDER BY t.updated_at DESC LIMIT 500",
                  tuple(params))
    return jsonify({"tickets": [ticket_view(r, with_inference=False) for r in rows]})


@blueprint.get("/tickets/<int:ticket_id>")
@require()
def get_ticket(ticket_id):
    row, error = _ticket(ticket_id)
    if error:
        return error
    return jsonify({"ticket": ticket_view(row)})


@blueprint.post("/tickets")
@require()
def create_ticket():
    try:
        clean = clean_ticket(request.get_json(silent=True) or {})
    except ValueError as problem:
        return bad(str(problem))
    stamp = now()
    new_id = db.run(
        "INSERT INTO tickets (reporter_id, status, created_at, updated_at, " + ", ".join(TICKET_FIELDS)
        + ") VALUES (?, 'open', ?, ?, " + ", ".join("?" * len(TICKET_FIELDS)) + ") RETURNING id",
        (current_user()["id"], stamp, stamp) + tuple(clean[f] for f in TICKET_FIELDS),
    )
    row, _ = _ticket(new_id)
    return jsonify({"ticket": ticket_view(row)}), 201


@blueprint.put("/tickets/<int:ticket_id>")
@require()
def update_ticket(ticket_id):
    row, error = _ticket(ticket_id)
    if error:
        return error
    if row["reporter_id"] != current_user()["id"]:
        return bad("Only the reporter can edit a report.", 403)
    if row["status"] != "open":
        return bad("A report can only be edited while it is open (before the model review).", 409)
    try:
        clean = clean_ticket(request.get_json(silent=True) or {})
    except ValueError as problem:
        return bad(str(problem))
    db.run("UPDATE tickets SET " + ", ".join(f"{f} = ?" for f in TICKET_FIELDS) + ", updated_at = ? WHERE id = ?",
           tuple(clean[f] for f in TICKET_FIELDS) + (now(), ticket_id))
    row, _ = _ticket(ticket_id)
    return jsonify({"ticket": ticket_view(row)})


@blueprint.delete("/tickets/<int:ticket_id>")
@require()
def delete_ticket(ticket_id):
    row, error = _ticket(ticket_id)
    if error:
        return error
    is_owner = row["reporter_id"] == current_user()["id"]
    if not has_role("admin") and not (is_owner and row["status"] == "open"):
        return bad("A playtester can delete a report only while it is open; after that only an admin can.", 403)
    db.run("UPDATE inferences SET ticket_id = NULL WHERE ticket_id = ?", (ticket_id,))
    db.run("DELETE FROM tickets WHERE id = ?", (ticket_id,))
    return jsonify({"ok": True})


@blueprint.post("/tickets/<int:ticket_id>/review")
@require("designer")
def review_ticket(ticket_id):
    """Runs the trained network on the reported situation and stores its suggestion."""
    row, error = _ticket(ticket_id)
    if error:
        return error
    if row["kind"] != "behaviour":
        return bad("Only behaviour reports are reviewed by the model.", 409)
    if row["status"] not in ("open", "reviewed"):
        return bad("This report already has a decision.", 409)
    situation = json.loads(row["situation"])
    inference_id, _, _, verdict, _ = infer(situation, row["observed_action"], ticket_id, bool(row["simulated"]), current_user()["id"])
    db.run("UPDATE tickets SET status = 'reviewed', inference_id = ?, ai_verdict = ?, updated_at = ? WHERE id = ?",
           (inference_id, verdict, now(), ticket_id))
    row, _ = _ticket(ticket_id)
    return jsonify({"ticket": ticket_view(row)})


@blueprint.post("/tickets/<int:ticket_id>/decision")
@require("designer")
def decide_ticket(ticket_id):
    row, error = _ticket(ticket_id)
    if error:
        return error
    data = request.get_json(silent=True) or {}
    decision = data.get("decision")
    if decision not in ("expected", "bug"):
        return bad("The decision must be 'expected' (works as designed) or 'bug'.")
    if row["kind"] == "behaviour" and row["status"] != "reviewed":
        return bad("Run the model review before deciding a behaviour report.", 409)
    if row["kind"] != "behaviour" and row["status"] != "open":
        return bad("This report already has a decision.", 409)
    try:
        note = _text(data.get("note"), "The decision note", 5, 1000)
    except ValueError as problem:
        return bad(str(problem))
    db.run("UPDATE tickets SET status = ?, decision = ?, decision_note = ?, designer_id = ?, updated_at = ? WHERE id = ?",
           (decision, decision, note, current_user()["id"], now(), ticket_id))
    row, _ = _ticket(ticket_id)
    return jsonify({"ticket": ticket_view(row)})


@blueprint.post("/tickets/<int:ticket_id>/resolve")
@require("designer")
def resolve_ticket(ticket_id):
    row, error = _ticket(ticket_id)
    if error:
        return error
    if row["status"] != "bug":
        return bad("Only a report confirmed as a bug can be resolved.", 409)
    try:
        note = _text((request.get_json(silent=True) or {}).get("note"), "The resolution note", 5, 1000)
    except ValueError as problem:
        return bad(str(problem))
    db.run("UPDATE tickets SET status = 'resolved', resolution_note = ?, updated_at = ? WHERE id = ?", (note, now(), ticket_id))
    row, _ = _ticket(ticket_id)
    return jsonify({"ticket": ticket_view(row)})


# ---------------------------------------------------------------- the model, on demand

@blueprint.post("/predict")
@require("designer")
def predict():
    """A designer asks the network about a situation without a report (stored in the history too)."""
    data = request.get_json(silent=True) or {}
    try:
        situation = clean_situation(data.get("situation"))
        observed = None if data.get("observed_action") in (None, "") else _int(data.get("observed_action"), 0, freddy_brain.ACTION_COUNT - 1, "Observed action")
    except ValueError as problem:
        return bad(str(problem))
    inference_id, decision, probability, verdict, latency = infer(situation, observed, user_id=current_user()["id"])
    return jsonify({"inference_id": inference_id, "actions": decision["actions"], "observed_probability": probability,
                    "verdict": verdict, "latency_ms": latency, "model_version": MODEL_VERSION})


@blueprint.get("/inferences")
@require("designer")
def list_inferences():
    include_simulated = request.args.get("simulated") == "1"
    rows = db.all("SELECT i.*, u.username FROM inferences i LEFT JOIN users u ON u.id = i.user_id "
                  + ("" if include_simulated else "WHERE i.simulated = 0 ") + "ORDER BY i.id DESC LIMIT 300")
    for row in rows:
        row["situation"] = json.loads(row["situation"])
        row["result"] = json.loads(row["result"])
        row["simulated"] = bool(row["simulated"])
    return jsonify({"inferences": rows, "model_version": MODEL_VERSION, "threshold": EXPECTED_THRESHOLD})


@blueprint.get("/model")
@require()
def model_card():
    return jsonify({
        "version": MODEL_VERSION,
        "algorithm": "PPO (Unity ML-Agents), 100 inputs -> 256 -> 256 -> 17 actions",
        "trained": "2026-10-29, 54.8 M decisions over 19 versions; v19 fine-tuned 1 M steps from v18",
        "evaluation": "400 nights per survivor skill: caught 100% Novice, 98.5% Normal, 97.8% Expert",
        "threshold": EXPECTED_THRESHOLD,
        "rooms": freddy_brain.ROOMS,
        "neighbors": freddy_brain.NEIGHBORS,
        "spots": freddy_brain.SPOT_NAMES,
    })


# ---------------------------------------------------------------- indicators and reports

def _share(part, whole):
    return round(part / whole, 4) if whole else None


def indicators(include_simulated=False):
    sim = "" if include_simulated else " AND simulated = 0"
    plays = db.all("SELECT night, outcome, seconds FROM playtests WHERE 1 = 1" + sim)
    by_night = []
    for night in range(1, 8):
        rows = [p for p in plays if p["night"] == night]
        caught = sum(1 for p in rows if p["outcome"] == "Caught")
        by_night.append({"night": night, "plays": len(rows), "caught": caught, "catch_rate": _share(caught, len(rows)),
                         "avg_seconds": round(statistics.mean(p["seconds"] for p in rows), 1) if rows else None})
    tickets = db.all("SELECT kind, status, ai_verdict, decision FROM tickets WHERE 1 = 1" + sim)
    decided = [t for t in tickets if t["kind"] == "behaviour" and t["decision"] and t["ai_verdict"]]
    agreed = sum(1 for t in decided if (t["ai_verdict"] == "expected") == (t["decision"] == "expected"))
    caught_all = sum(1 for p in plays if p["outcome"] == "Caught")
    return {
        "playtests": len(plays),
        "catch_rate": _share(caught_all, len(plays)),
        "by_night": by_night,
        "tickets": len(tickets),
        "tickets_by_status": {s: sum(1 for t in tickets if t["status"] == s) for s in STATUSES},
        "tickets_by_kind": {k: sum(1 for t in tickets if t["kind"] == k) for k in KINDS},
        "ai_verdicts": {v: sum(1 for t in tickets if t["ai_verdict"] == v) for v in ("expected", "unlikely", "impossible")},
        "decided_with_model": len(decided),
        "agreement": _share(agreed, len(decided)),
        "inferences": db.value("SELECT COUNT(*) AS n FROM inferences WHERE 1 = 1" + sim),
        "users": db.value("SELECT COUNT(*) AS n FROM users"),
    }


@blueprint.get("/dashboard")
@require()
def dashboard():
    user = current_user()
    mine = {
        "playtests": db.value("SELECT COUNT(*) AS n FROM playtests WHERE user_id = ?", (user["id"],)),
        "caught": db.value("SELECT COUNT(*) AS n FROM playtests WHERE user_id = ? AND outcome = 'Caught'", (user["id"],)),
        "tickets": db.value("SELECT COUNT(*) AS n FROM tickets WHERE reporter_id = ?", (user["id"],)),
        "open_tickets": db.value("SELECT COUNT(*) AS n FROM tickets WHERE reporter_id = ? AND status IN ('open', 'reviewed')", (user["id"],)),
    }
    body = {"user": public(user), "mine": mine, "model_version": MODEL_VERSION}
    if has_role("designer"):
        body["studio"] = indicators(request.args.get("simulated") == "1")
        body["queue"] = db.value("SELECT COUNT(*) AS n FROM tickets WHERE status IN ('open', 'reviewed')")
    return jsonify(body)


@blueprint.get("/reports/playtests.csv")
@require("designer")
def playtests_csv():
    rows = db.all("SELECT p.*, u.username FROM playtests p JOIN users u ON u.id = p.user_id ORDER BY p.id")
    out = io.StringIO()
    columns = ["id", "username", "source", "played_at", "night", "mode", "outcome", "cause", "room", "seconds", "objectives_done",
               "objectives_total", "heard", "seen", "searches", "sense_uses", "sense_hits", "decisions", "threat_before",
               "threat_after", "simulated"]
    writer = csv.DictWriter(out, fieldnames=columns, extrasaction="ignore")
    writer.writeheader()
    writer.writerows(rows)
    return Response(out.getvalue(), mimetype="text/csv",
                    headers={"Content-Disposition": "attachment; filename=night-shift-playtests.csv"})


# ---------------------------------------------------------------- users (admin)

@blueprint.get("/users")
@require("admin")
def list_users():
    rows = db.all("SELECT * FROM users ORDER BY id")
    counts = {r["user_id"]: r["n"] for r in db.all("SELECT user_id, COUNT(*) AS n FROM playtests GROUP BY user_id")}
    return jsonify({"users": [dict(public(r), playtests=counts.get(r["id"], 0)) for r in rows], "roles": ROLES})


@blueprint.put("/users/<int:user_id>")
@require("admin")
def update_user(user_id):
    me = current_user()
    if user_id == me["id"]:
        return bad("You cannot change your own role or disable your own account.", 403)
    target = db.one("SELECT * FROM users WHERE id = ?", (user_id,))
    if target is None:
        return bad("User not found.", 404)
    data = request.get_json(silent=True) or {}
    role = data.get("role", target["role"])
    active = 1 if data.get("active", bool(target["active"])) else 0
    if role not in ROLES:
        return bad("Unknown role.")
    if target["role"] == "admin" and (role != "admin" or not active):
        admins = db.value("SELECT COUNT(*) AS n FROM users WHERE role = 'admin' AND active = 1")
        if admins <= 1:
            return bad("The system needs at least one active administrator.", 409)
    db.run("UPDATE users SET role = ?, active = ? WHERE id = ?", (role, active, user_id))
    return jsonify({"user": public(db.one("SELECT * FROM users WHERE id = ?", (user_id,)))})


# ---------------------------------------------------------------- monitoring (admin)

def _percentile(values, share):
    if not values:
        return None
    values = sorted(values)
    return round(values[min(len(values) - 1, int(share * len(values)))], 1)


def _psi(reference, recent, bins):
    """Population stability index between two samples of a categorical feature."""
    if len(reference) < 10 or len(recent) < 10:
        return None
    total = 0.0
    for b in bins:
        p = max(sum(1 for x in reference if x == b) / len(reference), 1e-4)
        q = max(sum(1 for x in recent if x == b) / len(recent), 1e-4)
        total += (q - p) * math.log(q / p)
    return round(total, 3)


def _features(situation):
    return {
        "room": situation.get("freddy_room"),
        "noise": min(len(situation.get("noises") or {}), 3),
        "sighting": "last_seen_room" in situation,
        "progress": min(int(float(situation.get("night_progress", 0)) * 4), 3),
    }


@blueprint.get("/monitoring")
@require("admin")
def monitoring():
    include_simulated = request.args.get("simulated") == "1"
    sim = "" if include_simulated else " AND simulated = 0"
    since = now() - 7 * 86400
    requests_ = db.all("SELECT at, method, path, status, latency_ms FROM request_log WHERE at >= ?" + sim, (since,))
    latencies = [r["latency_ms"] for r in requests_]
    errors = sum(1 for r in requests_ if r["status"] >= 500)
    by_path = {}
    for r in requests_:
        key = r["method"] + " " + "/".join(part if not part.isdigit() else ":id" for part in r["path"].split("/")[:5])
        by_path.setdefault(key, []).append(r["latency_ms"])
    endpoints = sorted(({"endpoint": k, "count": len(v), "p50_ms": _percentile(v, 0.5), "p95_ms": _percentile(v, 0.95)}
                        for k, v in by_path.items()), key=lambda e: -e["count"])[:15]
    days = {}
    for r in requests_:
        day = time.strftime("%Y-%m-%d", time.gmtime(r["at"]))
        days.setdefault(day, {"day": day, "requests": 0, "errors": 0})
        days[day]["requests"] += 1
        days[day]["errors"] += 1 if r["status"] >= 500 else 0
    inferences = db.all("SELECT situation, latency_ms, verdict, simulated FROM inferences WHERE 1 = 1" + sim + " ORDER BY id")
    features = [_features(json.loads(i["situation"])) for i in inferences]
    half = len(features) // 2
    reference, recent = features[:half], features[half:]
    drift = {
        "reference_size": len(reference),
        "recent_size": len(recent),
        "psi": {
            "Pockets' room": _psi([f["room"] for f in reference], [f["room"] for f in recent], range(freddy_brain.ROOM_COUNT)),
            "Rooms with noise": _psi([f["noise"] for f in reference], [f["noise"] for f in recent], range(4)),
            "Has a sighting": _psi([f["sighting"] for f in reference], [f["sighting"] for f in recent], (True, False)),
            "Night progress (quarter)": _psi([f["progress"] for f in reference], [f["progress"] for f in recent], range(4)),
        },
    }
    stats = indicators(include_simulated)
    recent_plays = db.all("SELECT outcome FROM playtests WHERE 1 = 1" + sim + " ORDER BY id DESC LIMIT 30")
    recent_catch = _share(sum(1 for p in recent_plays if p["outcome"] == "Caught"), len(recent_plays))
    alerts = []
    error_rate = _share(errors, len(requests_))
    p95 = _percentile(latencies, 0.95)
    if error_rate is not None and error_rate > ALERT_ERROR_RATE:
        alerts.append(f"Server error rate {error_rate:.1%} is above {ALERT_ERROR_RATE:.0%}.")
    if p95 is not None and p95 > ALERT_P95_MS:
        alerts.append(f"p95 response time {p95} ms is above {ALERT_P95_MS} ms.")
    for name, value in drift["psi"].items():
        if value is not None and value > ALERT_PSI:
            alerts.append(f"Input drift: PSI of '{name}' is {value} (above {ALERT_PSI}).")
    if stats["agreement"] is not None and stats["decided_with_model"] >= 10 and stats["agreement"] < ALERT_AGREEMENT:
        alerts.append(f"Designers agreed with the model on only {stats['agreement']:.0%} of decided reports.")
    if recent_catch is not None and len(recent_plays) >= 10 and not ALERT_CATCH_LOW <= recent_catch <= ALERT_CATCH_HIGH:
        alerts.append(f"Real catch rate over the last {len(recent_plays)} playtests is {recent_catch:.0%}, outside {ALERT_CATCH_LOW:.0%}-{ALERT_CATCH_HIGH:.0%}.")
    recent_errors = db.all("SELECT at, path, message FROM error_log ORDER BY id DESC LIMIT 20")
    return jsonify({
        "include_simulated": include_simulated,
        "window_days": 7,
        "requests": len(requests_),
        "errors": errors,
        "error_rate": error_rate,
        "p50_ms": _percentile(latencies, 0.5),
        "p95_ms": p95,
        "endpoints": endpoints,
        "days": sorted(days.values(), key=lambda d: d["day"]),
        "inference_count": len(inferences),
        "inference_p50_ms": _percentile([i["latency_ms"] for i in inferences], 0.5),
        "inference_p95_ms": _percentile([i["latency_ms"] for i in inferences], 0.95),
        "drift": drift,
        "recent_catch_rate": recent_catch,
        "recent_playtests": len(recent_plays),
        "agreement": stats["agreement"],
        "decided_with_model": stats["decided_with_model"],
        "expected_catch_rate": {"Novice": 1.0, "Normal": 0.985, "Expert": 0.9775},
        "thresholds": {"error_rate": ALERT_ERROR_RATE, "p95_ms": ALERT_P95_MS, "agreement": ALERT_AGREEMENT, "psi": ALERT_PSI,
                       "catch_low": ALERT_CATCH_LOW, "catch_high": ALERT_CATCH_HIGH},
        "alerts": alerts,
        "recent_errors": recent_errors,
        "simulated_rows": {
            "requests": db.value("SELECT COUNT(*) AS n FROM request_log WHERE simulated = 1"),
            "inferences": db.value("SELECT COUNT(*) AS n FROM inferences WHERE simulated = 1"),
            "playtests": db.value("SELECT COUNT(*) AS n FROM playtests WHERE simulated = 1"),
            "tickets": db.value("SELECT COUNT(*) AS n FROM tickets WHERE simulated = 1"),
        },
    })


def _random_situation(rng):
    rooms = freddy_brain.ROOM_COUNT
    room = rng.randrange(rooms)
    situation = {"freddy_room": room, "noises": {}, "night_progress": round(rng.random(), 2),
                 "objectives": round(rng.random() * 0.8, 2), "sense_ready": rng.random() < 0.6}
    for _ in range(rng.choice((0, 0, 1, 1, 2))):
        situation["noises"][str(rng.randrange(rooms))] = rng.choice((1, 2, 3))
    if rng.random() < 0.4:
        situation["last_seen_room"] = rng.randrange(rooms)
        situation["seconds_since_seen"] = rng.randrange(5, 200)
    situation["rooms_not_checked"] = sorted(rng.sample(range(rooms), rng.randrange(3, rooms)))
    return clean_situation(situation)


@blueprint.post("/monitoring/simulate")
@require("admin")
def simulate():
    """Fills the logs with SIMULATED operation (marked simulated = 1) to test the monitoring.
    Half of the inferences lean on later-night situations so the drift check has something to find."""
    rng = random.Random(int((request.get_json(silent=True) or {}).get("seed", 7)))
    user_id = current_user()["id"]
    stamp = now()
    for i in range(60):
        situation = _random_situation(rng)
        if i >= 30:
            situation["night_progress"] = round(0.75 + rng.random() * 0.25, 2)
            situation["freddy_room"] = rng.choice((9, 10, 11))
            situation = clean_situation(situation)
        decision = freddy_brain.decide(situation)
        legal = [a["index"] for a in decision["actions"]]
        observed = rng.choice(legal) if rng.random() < 0.8 else rng.randrange(freddy_brain.ACTION_COUNT)
        infer(situation, observed, simulated=True, user_id=user_id)
    for i in range(80):
        status = 500 if rng.random() < 0.02 else 200
        db.run("INSERT INTO request_log (at, method, path, status, latency_ms, user_id, simulated) VALUES (?, ?, ?, ?, ?, ?, 1)",
               (stamp - rng.randrange(0, 6 * 86400), rng.choice(("GET", "POST")),
                rng.choice(("/api/studio/tickets", "/api/studio/playtests", "/api/brain/decide", "/api/studio/dashboard")),
                status, round(rng.lognormvariate(3.5, 0.6), 1), user_id))
    for i in range(20):
        night = rng.randrange(1, 8)
        caught = rng.random() < 0.55 + night * 0.04
        db.run("INSERT INTO playtests (user_id, source, played_at, night, mode, outcome, cause, seconds, objectives_done, "
               "objectives_total, simulated, created_at) VALUES (?, 'simulated', ?, ?, 'Story', ?, 'simulated night', ?, ?, 3, 1, ?)",
               (user_id, time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(stamp - i * 3600)), night,
                "Caught" if caught else "Escaped", round(rng.uniform(60, 480), 1), rng.randrange(0, 4), stamp))
    return jsonify({"ok": True, "note": "60 inferences, 80 requests and 20 playtests marked simulated were added."})


@blueprint.delete("/monitoring/simulate")
@require("admin")
def clear_simulated():
    for table in ("inferences", "request_log", "playtests", "tickets"):
        db.run(f"DELETE FROM {table} WHERE simulated = 1")
    return jsonify({"ok": True})
