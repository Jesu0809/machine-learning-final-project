"""The players' saves: one JSON document per anonymous player id.

Uses the database in the DATABASE_URL environment variable (Postgres) when there is one, because
the disk of a free web host is erased on every deploy; otherwise a local SQLite file, for development.
"""
import os
import secrets
import sqlite3
import time

DATABASE_URL = os.environ.get("DATABASE_URL", "")
SQLITE_PATH = os.environ.get("SAVES_SQLITE", os.path.join(os.path.dirname(__file__), "data", "saves.sqlite3"))

CODE_LIFETIME_SECONDS = 600
# No I, O, 0 or 1: the code is read on one screen and typed on another.
CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"
CODE_LENGTH = 6

_SCHEMA = (
    "CREATE TABLE IF NOT EXISTS saves (player TEXT PRIMARY KEY, body TEXT NOT NULL, updated_at BIGINT NOT NULL)",
    "CREATE TABLE IF NOT EXISTS link_codes (code TEXT PRIMARY KEY, player TEXT NOT NULL, expires_at BIGINT NOT NULL)",
)


def _connect():
    if DATABASE_URL:
        import psycopg

        return psycopg.connect(DATABASE_URL), "%s"
    return sqlite3.connect(SQLITE_PATH), "?"


def _run(work):
    connection, mark = _connect()
    try:
        cursor = connection.cursor()
        for statement in _SCHEMA:
            cursor.execute(statement)
        result = work(cursor, mark)
        connection.commit()
        return result
    finally:
        connection.close()


def load(player):
    def work(cursor, mark):
        cursor.execute(f"SELECT body FROM saves WHERE player = {mark}", (player,))
        row = cursor.fetchone()
        return row[0] if row else None

    return _run(work)


def store(player, body):
    def work(cursor, mark):
        cursor.execute(
            f"INSERT INTO saves (player, body, updated_at) VALUES ({mark}, {mark}, {mark}) "
            "ON CONFLICT (player) DO UPDATE SET body = excluded.body, updated_at = excluded.updated_at",
            (player, body, int(time.time())),
        )

    _run(work)


def new_code(player):
    """A short code that lets another device become this player, once, for ten minutes."""
    now = int(time.time())
    code = "".join(secrets.choice(CODE_ALPHABET) for _ in range(CODE_LENGTH))

    def work(cursor, mark):
        cursor.execute(f"DELETE FROM link_codes WHERE expires_at < {mark} OR player = {mark}", (now, player))
        cursor.execute(
            f"INSERT INTO link_codes (code, player, expires_at) VALUES ({mark}, {mark}, {mark})",
            (code, player, now + CODE_LIFETIME_SECONDS),
        )

    _run(work)
    return code


def redeem_code(code):
    """The player of this code, or None; the code stops working."""
    now = int(time.time())

    def work(cursor, mark):
        cursor.execute(f"SELECT player FROM link_codes WHERE code = {mark} AND expires_at >= {mark}", (code, now))
        row = cursor.fetchone()
        cursor.execute(f"DELETE FROM link_codes WHERE code = {mark}", (code,))
        return row[0] if row else None

    return _run(work)


def all_saves():
    """Every save, for summarizing how people play."""

    def work(cursor, mark):
        cursor.execute("SELECT body FROM saves")
        return [row[0] for row in cursor.fetchall()]

    return _run(work)
