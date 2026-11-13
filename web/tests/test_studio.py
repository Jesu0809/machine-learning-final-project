"""End-to-end check of the playtest studio API with Flask's test client and throwaway SQLite files.

Run from the repository root or from web/: python web/tests/test_studio.py
"""
import os
import sys
import tempfile

tmp = tempfile.mkdtemp()
os.environ["SYSTEM_SQLITE"] = os.path.join(tmp, "system.sqlite3")
os.environ["SAVES_SQLITE"] = os.path.join(tmp, "saves.sqlite3")
os.environ["ADMIN_USERNAME"] = "admin"
os.environ["ADMIN_PASSWORD"] = "admin12345"
WEB = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, WEB)
os.chdir(WEB)

import app as webapp  # noqa: E402

failures = []


def check(label, condition, extra=""):
    print(("ok   " if condition else "FAIL ") + label + ("" if condition else f"  -> {extra}"))
    if not condition:
        failures.append(label)


def client():
    return webapp.app.test_client()


admin, designer, tester, other = client(), client(), client(), client()

r = tester.post("/api/auth/register", json={"username": "tess", "password": "playtest1", "role": "admin"})
check("register creates account", r.status_code == 201, r.json)
check("register ignores role=admin", r.json["user"]["role"] == "playtester", r.json)
check("weak password rejected", other.post("/api/auth/register", json={"username": "x1y", "password": "short"}).status_code == 400)
check("duplicate username rejected", other.post("/api/auth/register", json={"username": "TESS", "password": "playtest1"}).status_code == 409)
other.post("/api/auth/register", json={"username": "omar", "password": "playtest2"})
designer.post("/api/auth/register", json={"username": "dina", "password": "design123"})

check("no session -> 401", client().get("/api/studio/dashboard").status_code == 401)
check("form post rejected (415)", tester.post("/api/studio/tickets", data="a=b", content_type="application/x-www-form-urlencoded").status_code == 415)

r = admin.post("/api/auth/login", json={"username": "admin", "password": "admin12345"})
check("bootstrap admin can log in", r.status_code == 200 and r.json["user"]["role"] == "admin", r.json)
users = admin.get("/api/studio/users").json["users"]
dina = next(u for u in users if u["username"] == "dina")
check("playtester cannot list users", tester.get("/api/studio/users").status_code == 403)
check("admin promotes designer", admin.put(f"/api/studio/users/{dina['id']}", json={"role": "designer"}).json["user"]["role"] == "designer")
me = admin.get("/api/auth/me").json["user"]
check("admin cannot change own role", admin.put(f"/api/studio/users/{me['id']}", json={"role": "playtester"}).status_code == 403)
tess_id = next(u for u in users if u["username"] == "tess")["id"]
check("playtester cannot promote self", tester.put(f"/api/studio/users/{tess_id}", json={"role": "admin"}).status_code == 403)

bad = client()
for _ in range(5):
    bad.post("/api/auth/login", json={"username": "omar", "password": "wrong-pass1"})
r = bad.post("/api/auth/login", json={"username": "omar", "password": "playtest2"})
check("5 failures lock the account", r.status_code == 429, r.status_code)

play = {"night": 2, "outcome": "Caught", "seconds": 212.5, "objectives_done": 1, "objectives_total": 3, "room": "Concessions",
        "heard": 4, "seen": 1, "searches": 3, "sense_uses": 1, "sense_hits": 1, "decisions": 60}
r = tester.post("/api/studio/playtests", json=play)
check("create playtest", r.status_code == 201, r.json)
pid = r.json["playtest"]["id"]
check("invalid playtest rejected", tester.post("/api/studio/playtests", json=dict(play, objectives_done=5)).status_code == 400)
check("other playtester cannot read it", other.get(f"/api/studio/playtests/{pid}").status_code in (401, 403))
check("update playtest", tester.put(f"/api/studio/playtests/{pid}", json=dict(play, seconds=230)).json["playtest"]["seconds"] == 230)

user = tester.get("/api/auth/me").json["user"]
save = {"history": [{"date": "2026-11-13T10:00:00Z", "night": 1, "outcome": "Escaped", "how": "You got out", "room": "Ticket Lobby",
                     "seconds": 300, "objectivesDone": 3, "objectivesTotal": 3, "heard": 2, "decisions": 80, "device": "WebGLPlayer"}]}
r = client().put(f"/api/save/{user['player_id']}", json=save)
check("game save imports nights", r.json.get("imported") == 1, r.json)
check("import is idempotent", client().put(f"/api/save/{user['player_id']}", json=save).json.get("imported") == 0)
mine = tester.get("/api/studio/playtests").json["playtests"]
check("playtester sees own 2 playtests", len(mine) == 2, len(mine))
game_row = next(p for p in mine if p["source"] == "game")
r = tester.put(f"/api/studio/playtests/{game_row['id']}", json={"notes": "felt fair", "seconds": 1})
check("game playtest: only notes change", r.json["playtest"]["notes"] == "felt fair" and r.json["playtest"]["seconds"] == 300, r.json)

situation = {"freddy_room": 5, "noises": {"9": 3}, "rooms_not_checked": [0, 1, 2, 3, 4], "night_progress": 0.3}
ticket = {"kind": "behaviour", "title": "He ignored my noise", "description": "I dropped a toy in the Midway and he stayed put.",
          "night": 2, "playtest_id": pid, "situation": situation, "observed_action": 5}
r = tester.post("/api/studio/tickets", json=ticket)
check("create behaviour ticket", r.status_code == 201, r.json)
tid = r.json["ticket"]["id"]
check("playtester cannot review", tester.post(f"/api/studio/tickets/{tid}/review").status_code == 403)
check("decision needs review first", designer.post(f"/api/studio/tickets/{tid}/decision", json={"decision": "bug", "note": "check it"}).status_code == 409)
r = designer.post(f"/api/studio/tickets/{tid}/review")
check("designer runs model review", r.status_code == 200 and r.json["ticket"]["status"] == "reviewed", r.json)
t = r.json["ticket"]
print("     verdict:", t["ai_verdict"], "observed p =", t["inference"]["observed_probability"], "top:", t["inference"]["top_action"])
check("reporter cannot edit after review", tester.put(f"/api/studio/tickets/{tid}", json=ticket).status_code == 409)
r = designer.post(f"/api/studio/tickets/{tid}/decision", json={"decision": "bug", "note": "Body did not follow the brain."})
check("designer decides bug", r.json["ticket"]["status"] == "bug", r.json)
check("resolve needs note", designer.post(f"/api/studio/tickets/{tid}/resolve", json={"note": ""}).status_code == 400)
check("designer resolves", designer.post(f"/api/studio/tickets/{tid}/resolve", json={"note": "Fixed in build 2026-11-13."}).json["ticket"]["status"] == "resolved")

r = tester.post("/api/studio/tickets", json=dict(ticket, observed_action=16, title="Impossible move here"))
t2 = r.json["ticket"]["id"]
illegal = designer.post(f"/api/studio/tickets/{t2}/review").json["ticket"]
print("     second verdict:", illegal["ai_verdict"])
check("reporter deletes open ticket? (reviewed -> no)", tester.delete(f"/api/studio/tickets/{t2}").status_code == 403)
r = tester.post("/api/studio/tickets", json={"kind": "feedback", "title": "Love the clown", "description": "The voice is great, more nights!"})
t3 = r.json["ticket"]["id"]
check("feedback ticket created", r.status_code == 201)
check("reporter deletes own open ticket", tester.delete(f"/api/studio/tickets/{t3}").status_code == 200)

r = designer.post("/api/studio/predict", json={"situation": situation, "observed_action": 9})
check("designer predict", r.status_code == 200 and r.json["actions"], r.json)
check("public demo still works", client().post("/api/brain/decide", json={"freddy_room": 6, "fuses": 3}).status_code == 200)
check("inference history", len(designer.get("/api/studio/inferences").json["inferences"]) >= 4)

d = designer.get("/api/studio/dashboard").json
check("designer dashboard has studio indicators", "studio" in d and d["studio"]["playtests"] == 2, d.get("studio"))
check("playtester dashboard has no studio block", "studio" not in tester.get("/api/studio/dashboard").json)
check("csv report for designer", designer.get("/api/studio/reports/playtests.csv").data.startswith(b"id,username"))
check("csv report denied to playtester", tester.get("/api/studio/reports/playtests.csv").status_code == 403)

check("monitoring denied to designer", designer.get("/api/studio/monitoring").status_code == 403)
check("simulate", admin.post("/api/studio/monitoring/simulate", json={"seed": 7}).status_code == 200)
m = admin.get("/api/studio/monitoring?simulated=1").json
print("     monitoring:", m["requests"], "requests, p95", m["p95_ms"], "ms, psi", m["drift"]["psi"], "alerts", m["alerts"])
check("monitoring with simulated data", m["inference_count"] >= 60 and m["simulated_rows"]["inferences"] == 60)
real = admin.get("/api/studio/monitoring").json
check("real view excludes simulated", real["simulated_rows"]["inferences"] == 60 and real["inference_count"] < 60, real["inference_count"])
check("clear simulated", admin.delete("/api/studio/monitoring/simulate").status_code == 200)
check("logout", tester.post("/api/auth/logout").status_code == 200 and tester.get("/api/studio/dashboard").status_code == 401)

print("\nFAILED:" if failures else "\nALL PASSED", failures)
sys.exit(1 if failures else 0)
