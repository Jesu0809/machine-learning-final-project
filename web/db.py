"""The information system's database: Postgres when DATABASE_URL is set (Render), otherwise a
local SQLite file for development.

SQL is written once with "?" placeholders and translated for Postgres. Every table is created
here, on first use, so a fresh database needs no manual setup.
"""
import os
import sqlite3
import threading

from flask import g

DATABASE_URL = os.environ.get("DATABASE_URL", "")
SQLITE_PATH = os.environ.get("SYSTEM_SQLITE", os.path.join(os.path.dirname(__file__), "data", "system.sqlite3"))
POSTGRES = bool(DATABASE_URL)

_ID = "SERIAL PRIMARY KEY" if POSTGRES else "INTEGER PRIMARY KEY AUTOINCREMENT"

SCHEMA = [
    f"""CREATE TABLE IF NOT EXISTS users (
        id {_ID},
        username TEXT NOT NULL UNIQUE,
        password_hash TEXT NOT NULL,
        role TEXT NOT NULL DEFAULT 'playtester',
        player_id TEXT UNIQUE,
        active INTEGER NOT NULL DEFAULT 1,
        failed_logins INTEGER NOT NULL DEFAULT 0,
        locked_until BIGINT NOT NULL DEFAULT 0,
        created_at BIGINT NOT NULL,
        last_login BIGINT
    )""",
    f"""CREATE TABLE IF NOT EXISTS playtests (
        id {_ID},
        user_id INTEGER NOT NULL REFERENCES users(id),
        source TEXT NOT NULL,
        fingerprint TEXT UNIQUE,
        played_at TEXT NOT NULL,
        night INTEGER NOT NULL,
        mode TEXT,
        outcome TEXT NOT NULL,
        cause TEXT,
        room TEXT,
        seconds DOUBLE PRECISION NOT NULL DEFAULT 0,
        objectives_done INTEGER NOT NULL DEFAULT 0,
        objectives_total INTEGER NOT NULL DEFAULT 0,
        heard INTEGER NOT NULL DEFAULT 0,
        seen INTEGER NOT NULL DEFAULT 0,
        searches INTEGER NOT NULL DEFAULT 0,
        sense_uses INTEGER NOT NULL DEFAULT 0,
        sense_hits INTEGER NOT NULL DEFAULT 0,
        decisions INTEGER NOT NULL DEFAULT 0,
        threat_before DOUBLE PRECISION,
        threat_after DOUBLE PRECISION,
        device TEXT,
        brain TEXT,
        notes TEXT,
        simulated INTEGER NOT NULL DEFAULT 0,
        created_at BIGINT NOT NULL
    )""",
    f"""CREATE TABLE IF NOT EXISTS tickets (
        id {_ID},
        reporter_id INTEGER NOT NULL REFERENCES users(id),
        playtest_id INTEGER REFERENCES playtests(id) ON DELETE SET NULL,
        kind TEXT NOT NULL,
        title TEXT NOT NULL,
        description TEXT NOT NULL,
        night INTEGER,
        situation TEXT,
        observed_action INTEGER,
        status TEXT NOT NULL DEFAULT 'open',
        inference_id INTEGER,
        ai_verdict TEXT,
        designer_id INTEGER REFERENCES users(id),
        decision TEXT,
        decision_note TEXT,
        resolution_note TEXT,
        simulated INTEGER NOT NULL DEFAULT 0,
        created_at BIGINT NOT NULL,
        updated_at BIGINT NOT NULL
    )""",
    f"""CREATE TABLE IF NOT EXISTS inferences (
        id {_ID},
        user_id INTEGER REFERENCES users(id),
        ticket_id INTEGER,
        model_version TEXT NOT NULL,
        situation TEXT NOT NULL,
        result TEXT NOT NULL,
        top_action TEXT NOT NULL,
        top_probability DOUBLE PRECISION NOT NULL,
        observed_action INTEGER,
        observed_probability DOUBLE PRECISION,
        verdict TEXT,
        latency_ms DOUBLE PRECISION NOT NULL,
        simulated INTEGER NOT NULL DEFAULT 0,
        created_at BIGINT NOT NULL
    )""",
    f"""CREATE TABLE IF NOT EXISTS request_log (
        id {_ID},
        at BIGINT NOT NULL,
        method TEXT NOT NULL,
        path TEXT NOT NULL,
        status INTEGER NOT NULL,
        latency_ms DOUBLE PRECISION NOT NULL,
        user_id INTEGER,
        simulated INTEGER NOT NULL DEFAULT 0
    )""",
    f"""CREATE TABLE IF NOT EXISTS error_log (
        id {_ID},
        at BIGINT NOT NULL,
        path TEXT NOT NULL,
        message TEXT NOT NULL,
        user_id INTEGER
    )""",
    "CREATE INDEX IF NOT EXISTS request_log_at ON request_log (at)",
    "CREATE INDEX IF NOT EXISTS playtests_user ON playtests (user_id)",
    "CREATE INDEX IF NOT EXISTS tickets_status ON tickets (status)",
]

_ready = {"done": False}
_lock = threading.Lock()


def _connect():
    if POSTGRES:
        import psycopg

        return psycopg.connect(DATABASE_URL)
    connection = sqlite3.connect(SQLITE_PATH)
    connection.execute("PRAGMA foreign_keys = ON")
    return connection


def _sql(statement):
    return statement.replace("?", "%s") if POSTGRES else statement


def ensure_schema():
    if _ready["done"]:
        return
    with _lock:
        if _ready["done"]:
            return
        connection = _connect()
        try:
            cursor = connection.cursor()
            for statement in SCHEMA:
                cursor.execute(statement)
            connection.commit()
        finally:
            connection.close()
        _ready["done"] = True


def connection():
    """One connection per request, closed by close_connection at the end of it."""
    if "db" not in g:
        ensure_schema()
        g.db = _connect()
    return g.db


def close_connection(_error=None):
    db = g.pop("db", None)
    if db is not None:
        db.close()


def _rows(cursor):
    if cursor.description is None:
        return []
    names = [column[0] for column in cursor.description]
    return [dict(zip(names, row)) for row in cursor.fetchall()]


def all(statement, params=()):
    cursor = connection().cursor()
    cursor.execute(_sql(statement), params)
    return _rows(cursor)


def one(statement, params=()):
    rows = all(statement, params)
    return rows[0] if rows else None


def run(statement, params=()):
    """INSERT, UPDATE or DELETE, committed at once. An INSERT ... RETURNING id gives the new id."""
    db = connection()
    cursor = db.cursor()
    cursor.execute(_sql(statement), params)
    rows = _rows(cursor) if "RETURNING" in statement.upper() else []
    db.commit()
    return rows[0]["id"] if rows else cursor.rowcount


def value(statement, params=()):
    row = one(statement, params)
    return next(iter(row.values())) if row else None
