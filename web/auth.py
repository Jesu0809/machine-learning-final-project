"""Accounts, sign-in and roles of the playtest studio.

Roles (highest first):
- admin: manages users and roles, sees the monitoring and every record.
- designer: reviews behaviour reports with the trained model and decides what happens to them.
- playtester: plays, records playtest sessions and reports what Pockets did.

Registration always creates a playtester; only an admin can change a role, and never their own,
so nobody can grant themselves administrative rights. Passwords are stored as scrypt hashes.
"""
import functools
import os
import re
import secrets
import time

from flask import Blueprint, g, jsonify, request, session
from werkzeug.security import check_password_hash, generate_password_hash

import db

ROLES = ("playtester", "designer", "admin")
RANK = {role: i for i, role in enumerate(ROLES)}
USERNAME = re.compile(r"^[A-Za-z0-9_.-]{3,32}$")
MAX_FAILED_LOGINS = 5
LOCK_SECONDS = 300
_DUMMY_HASH = generate_password_hash(secrets.token_hex(16))

blueprint = Blueprint("auth", __name__, url_prefix="/api/auth")


def password_problem(password):
    if not isinstance(password, str) or len(password) < 8:
        return "The password needs at least 8 characters."
    if len(password) > 128:
        return "The password is too long."
    if not re.search(r"[A-Za-z]", password) or not re.search(r"\d", password):
        return "The password needs at least one letter and one number."
    return None


def public(user):
    return {
        "id": user["id"],
        "username": user["username"],
        "role": user["role"],
        "active": bool(user["active"]),
        "player_id": user["player_id"],
        "created_at": user["created_at"],
        "last_login": user["last_login"],
    }


def current_user():
    if "user" in g:
        return g.user
    g.user = None
    user_id = session.get("user_id")
    if user_id is not None:
        user = db.one("SELECT * FROM users WHERE id = ?", (user_id,))
        if user and user["active"]:
            g.user = user
        else:
            session.clear()
    return g.user


def require(role="playtester"):
    """The route needs a signed-in, active user with at least this role."""

    def wrap(view):
        @functools.wraps(view)
        def guarded(*args, **kwargs):
            user = current_user()
            if user is None:
                return jsonify({"error": "Please sign in."}), 401
            if RANK[user["role"]] < RANK[role]:
                return jsonify({"error": "Your role cannot do this."}), 403
            return view(*args, **kwargs)

        return guarded

    return wrap


def has_role(role):
    user = current_user()
    return user is not None and RANK[user["role"]] >= RANK[role]


def create_user(username, password, role="playtester"):
    return db.run(
        "INSERT INTO users (username, password_hash, role, player_id, created_at) VALUES (?, ?, ?, ?, ?) RETURNING id",
        (username, generate_password_hash(password), role, secrets.token_hex(16), int(time.time())),
    )


def bootstrap_admin():
    """The first administrator comes from ADMIN_USERNAME and ADMIN_PASSWORD (set on the host, never in git)."""
    username = os.environ.get("ADMIN_USERNAME", "").strip()
    password = os.environ.get("ADMIN_PASSWORD", "")
    if not username or not password or password_problem(password) or not USERNAME.match(username):
        return
    if db.one("SELECT id FROM users WHERE username = ?", (username,)) is None:
        create_user(username, password, "admin")


@blueprint.post("/register")
def register():
    data = request.get_json(silent=True) or {}
    username = str(data.get("username", "")).strip()
    password = data.get("password", "")
    if not USERNAME.match(username):
        return jsonify({"error": "The username needs 3 to 32 letters, numbers, dots, dashes or underscores."}), 400
    problem = password_problem(password)
    if problem:
        return jsonify({"error": problem}), 400
    if db.one("SELECT id FROM users WHERE lower(username) = lower(?)", (username,)):
        return jsonify({"error": "That username is taken."}), 409
    # Any "role" sent by the client is ignored on purpose: new accounts are always playtesters.
    user_id = create_user(username, password)
    session.clear()
    session["user_id"] = user_id
    session.permanent = True
    db.run("UPDATE users SET last_login = ? WHERE id = ?", (int(time.time()), user_id))
    return jsonify({"user": public(db.one("SELECT * FROM users WHERE id = ?", (user_id,)))}), 201


@blueprint.post("/login")
def login():
    data = request.get_json(silent=True) or {}
    username = str(data.get("username", "")).strip()
    password = str(data.get("password", ""))
    user = db.one("SELECT * FROM users WHERE lower(username) = lower(?)", (username,))
    now = int(time.time())
    if user and user["locked_until"] > now:
        minutes = (user["locked_until"] - now) // 60 + 1
        return jsonify({"error": f"Too many failed attempts. Try again in {minutes} min."}), 429
    if user is None:
        # Same work as a real check, so the response time does not reveal which usernames exist.
        check_password_hash(_DUMMY_HASH, password)
    if user is None or not check_password_hash(user["password_hash"], password):
        if user:
            failed = user["failed_logins"] + 1
            locked = now + LOCK_SECONDS if failed >= MAX_FAILED_LOGINS else 0
            db.run("UPDATE users SET failed_logins = ?, locked_until = ? WHERE id = ?",
                   (0 if locked else failed, locked, user["id"]))
        return jsonify({"error": "Wrong username or password."}), 401
    if not user["active"]:
        return jsonify({"error": "This account is disabled. Ask an administrator."}), 403
    db.run("UPDATE users SET failed_logins = 0, locked_until = 0, last_login = ? WHERE id = ?", (now, user["id"]))
    session.clear()
    session["user_id"] = user["id"]
    session.permanent = True
    return jsonify({"user": public(user)})


@blueprint.post("/logout")
def logout():
    session.clear()
    return jsonify({"ok": True})


@blueprint.get("/me")
def me():
    user = current_user()
    return jsonify({"user": public(user) if user else None})


@blueprint.post("/password")
@require()
def change_password():
    data = request.get_json(silent=True) or {}
    user = current_user()
    if not check_password_hash(user["password_hash"], str(data.get("current", ""))):
        return jsonify({"error": "The current password is wrong."}), 400
    problem = password_problem(data.get("new", ""))
    if problem:
        return jsonify({"error": problem}), 400
    db.run("UPDATE users SET password_hash = ? WHERE id = ?", (generate_password_hash(data["new"]), user["id"]))
    return jsonify({"ok": True})
