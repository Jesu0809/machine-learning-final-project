# Training Log

One entry per training run. Copy the template, fill it in right after the run, and never edit an
old entry: if something was wrong, write a new run.

How to train (both systems):

1. In Unity: **Night Shift → Build Hunt Training**. It creates `Assets/Scenes/HuntTraining.unity`
   (32 copies of the hunt, no 3D and no UI) and builds it to `training/builds/` (git-ignored).
2. In a terminal:

```bash
conda activate mlagents
cd training
# macOS
mlagents-learn config/freddy_ppo.yaml --run-id=freddy_vN --env=builds/HuntTraining.app --no-graphics
# Windows
mlagents-learn config/freddy_ppo.yaml --run-id=freddy_vN --env=builds/HuntTraining/HuntTraining.exe --no-graphics
tensorboard --logdir results
```

The arena runs about 1,000–1,400 steps per second on an M-series Mac, so 5M steps take 80–90 minutes.
After a run:

1. Copy `results/freddy_vN/Freddy.onnx` to `unity/Assets/ML/Models/freddy_vN.onnx`, update
   `GameSceneMenu.PreferredModel`, and export its weights for the AI inspector:
   `python scripts/export_policy_weights.py ../unity/Assets/ML/Models/freddy_vN.onnx`.
2. **Night Shift → Build Evaluation**, then run it:
   `builds/Evaluation.app/Contents/MacOS/Night\ Shift -batchmode -nographics -nights 400 -results evaluation/results.json`
   (Windows: `builds\Evaluation\Evaluation.exe` with the same arguments).
3. `python scripts/summarize_eval.py` prints the table and copies it to the website;
   `python scripts/export_tensorboard.py freddy_vN` writes the training curves for the website.

---

## Template

### freddy_vN — YYYY-MM-DD

- **Changed from previous run:**
- **Reward terms:**
- **Config changes (`freddy_ppo.yaml`):**
- **Environment:** instances, SurvivorBot ranges
- **Steps trained / wall time:**
- **Final mean cumulative reward:**
- **Observations:** (curve shape, behaviors seen when watching the agent)
- **Evaluation:**
- **Decision:** keep / discard, and why

---

<!-- Runs go below, newest last. -->

### freddy_v1 — 2026-10-11

- **Changed from previous run:** first run of the hunt.
- **Reward terms:** as in ml-design.md section 6 (+1 catch, −1 escape, −0.5 time up, −0.001 per
  decision, −0.01 empty search, −0.02 repeated search, potential-based distance shaping ×0.1).
- **Config changes (`freddy_ppo.yaml`):** 256 × 2 network, buffer 20,480, time horizon 128,
  3M steps (larger than the defaults because the problem is partially observable and has 43 inputs).
- **Environment:** 32 copies in `HuntTraining`, standalone macOS build, `--no-graphics`;
  SurvivorBot randomized every night; 43 observations, 12 actions with masking.
- **Steps trained / wall time:** 3,000,015 steps in 37 min.
- **Final mean cumulative reward:** +1.02 (started at −0.46).
- **Evaluation (400 nights per cell, catch rate):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 96.5 % | 48.5 % | 29.0 % |
  | Normal | 99.8 % | 42.0 % | 29.0 % |
  | Expert | 100 % | 36.8 % | 24.5 % |

- **Observations:** the result was checked for information leaks before trusting it: the
  observations only read what Freddy knows, and the same model run outside Unity (ONNX Runtime on
  the C# simulation) gives the same rates. 99% of its catches are searches of hiding spots. Watching
  traces shows why: the bot hides as soon as Freddy is next to it and then waits while he is close,
  so Freddy learned to patrol the center of the map and search the rooms next to his path, and to
  guard the exit lobby. It also exploited a weakness of the bot: it froze while Freddy was within
  two rooms, so the "expert" (who reads the tracker more often) was caught *more*.
- **Decision:** keep as the first working model (used by the game), but fix the bot: it gets
  impatient after waiting too long (`MaxWaitActions`). Retrain as freddy_v2 against the new bot.

### freddy_v2 — 2026-10-12

- **Changed from previous run:** SurvivorBot gets impatient: after waiting `MaxWaitActions`
  (Novice 3, Normal 6, Expert 8, randomized 3–10) for a path next to Freddy, it risks it.
  Same rewards, config and network as freddy_v1.
- **Environment:** 32 copies, SurvivorBot randomized every night (now including `MaxWaitActions`).
- **Steps trained / wall time:** 3,000,120 steps in about 25 min (the run was interrupted at
  2.68M and resumed from the 2.5M checkpoint with `--resume`).
- **Final mean cumulative reward:** +1.02 (started at −0.41).
- **Evaluation (400 nights per cell, catch rate with 95% Wilson interval):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 92.0 % (88.9–94.3) | 55.8 % (50.8–60.5) | 35.3 % (30.7–40.1) |
  | Normal | 97.5 % (95.5–98.6) | 42.0 % (37.3–46.9) | 27.3 % (23.1–31.8) |
  | Expert | 99.0 % (97.5–99.6) | 36.0 % (31.4–40.8) | 24.0 % (20.1–28.4) |

- **Observations:** the impatient bot escapes the trained Freddy more often (Novice 8% vs 3.5%),
  but the trained policy is still far above both baselines; the confidence intervals do not
  overlap in any cell. In the 3D game (capture tour) it walks toward the player's side of the map,
  searches the Dining Hall spots, and goes straight to the room where it heard a noise.
- **Decision:** keep. Copied to `unity/Assets/ML/Models/freddy_v2.onnx`; used by the game and the
  website. Next: the "learns where you hide" experiment (week 4).

### freddy_v3 — 2026-10-12

- **Changed from previous run:** new learned action **sense** (from *Friday the 13th*): it reveals
  the player's room if their fear is at least 0.5, then recharges for 30 decisions. Fear is computed
  on the room graph (1 − distance/4, +0.3 if seen in the last 4 decisions, −0.25 if hiding two or
  more rooms away). 13 actions, 54 observations (sense readiness, room where sense found the
  player, time since). New reward term: −0.01 for a sense that finds nothing. The scripted
  baseline also uses sense (when it has no other lead) so the comparison stays fair.
- **Config changes:** none (256 × 2 network, 3M steps).
- **Steps trained / wall time:** 3,000,032 steps in about 45 min.
- **Final mean cumulative reward:** +1.03 (started at −0.42).
- **Evaluation (400 nights per cell, catch rate with 95% Wilson interval):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 99.5 % (98.2–99.9) | 66.5 % (61.7–71.0) | 33.0 % (28.6–37.8) |
  | Normal | 98.5 % (96.8–99.3) | 66.2 % (61.5–70.7) | 27.8 % (23.6–32.3) |
  | Expert | 99.2 % (97.8–99.7) | 61.5 % (56.6–66.1) | 26.2 % (22.2–30.8) |

- **How it uses sense (C# simulation, 600 nights per cell):** the trained network uses it about as
  often as the scripted hunter (1.1–1.7 times per night vs 1.0–2.0) but it **finds the player far
  more often: 77–82% of its senses hit, against 62–67%**. It learned *when* sensing pays off (close
  to where the player probably is, when the player is likely afraid), which no rule told it.
- **Checks:** PolicyProbe (inspector probabilities) matches the model's deterministic action in
  7,007 of 7,007 states.
- **Decision:** keep. `unity/Assets/ML/Models/freddy_v3.onnx` is the game's brain.

### freddy_v4 — 2026-10-19

- **Changed from previous run:** a bigger map for the 3D game: 14 rooms on two floors joined by two
  staircases, and four air ducts only the player fits through. SurvivorBot learned to crawl
  (`Crawl` action, 4 actions per duct, `DuctUse` randomized 0.1–0.9). 79 observations (5R + 9),
  18 actions. Same rewards.
- **Config changes:** `max_steps` 3M → 5M for the larger state space.
- **Steps trained / wall time:** 5,000,000 steps in 78 min.
- **Final mean cumulative reward:** +0.97 (started at −0.68).
- **Evaluation (400 nights per cell, catch rate with 95% Wilson interval):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 99.8 % (98.6–100) | 61.8 % (56.9–66.4) | 22.8 % (18.9–27.1) |
  | Normal | 100 % (99.1–100) | 52.5 % (47.6–57.4) | 23.5 % (19.6–27.9) |
  | Expert | 95.5 % (93.0–97.1) | 45.5 % (40.7–50.4) | 16.5 % (13.2–20.4) |

- **Observations:** strong in the simulation, but the playtest showed a **sim-to-game gap**: one
  move on the graph was a 16 m walk in 3D (Freddy walks 2.9 m/s and decides every 3.5 s), so the
  brain planned as if every room were next door. Players also found the two floors hard to read.
- **Decision:** replace: redesign the map and make the simulation count corridor length.

### freddy_v5 — 2026-10-20

- **Changed from previous run:** one floor in two wings (11 rooms): the staff wing (Office, Parts,
  Restrooms, Back Hall, Kitchen, Storage) and the public wing (Dining Hall with the stage, Arcade,
  Main Hall, Party Room, Exit Lobby), joined by a long service corridor and the kitchen pass.
  **Travel time:** a long connection costs two decisions (`HuntMap.Cost`, `HuntSettings.TravelTime`)
  for Freddy and for the survivor, so the simulation runs at the pace of the 3D building. 64
  observations (5R + 9), 15 actions. Same rewards.
- **Config changes:** none (5M steps).
- **Steps trained / wall time:** 5,000,000 steps in 84 min.
- **Final mean cumulative reward:** +0.99 (started at −0.56).
- **Evaluation (400 nights per cell):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 99.2 % (97.8–99.7) | 86.0 % (82.3–89.1) | 46.2 % (41.4–51.1) |
  | Normal | 100 % (99.1–100) | 73.2 % (68.7–77.3) | 30.5 % (26.2–35.2) |
  | Expert | 99.8 % (98.6–100) | 47.5 % (42.6–52.4) | 17.0 % (13.6–21.0) |

- **Observations:** the numbers hide a **degenerate strategy**. Probing the network on the website
  showed it goes straight to the Party Room, the only way into the Exit Lobby, and patrols between
  the two: it ignores noises in the staff wing and searches only 0.1–0.5 spots per night (v4:
  1.8–8.5). Every survivor has to reach the exit, so camping the only door to it wins in the
  simulation, but it is dull to play against: Freddy waits instead of hunting. The v4 playtest had
  also shown him waiting outside a hiding spot instead of opening it.
- **Decision:** discard for the game. Two changes for freddy_v6: the exit is random each night, and
  waiting next to a hidden player is penalized.

### freddy_v6 — 2026-10-20

- **Changed from previous run:** two exits. Each night the way out is the front doors (Exit Lobby)
  or the loading door (Storage), chosen at random (`HuntSettings.RandomExit`); the survivor knows
  which, Freddy does not observe it. No fuse is placed in tonight's exit. New reward term: −0.05 for
  staying or sensing in the room where the player hides instead of searching (`WaitedOutsideHiding`).
  Same observations (64) and actions (15).
- **Config changes:** none (5M steps).
- **Steps trained / wall time:** 5,000,000 steps in 39 min. (A first attempt ran on the previous
  build by mistake: a stray `training/` folder next to the game repository redirected the build
  output. It was stopped at 0.6M steps and discarded; the build path now requires
  `training/config`.)
- **Final mean cumulative reward:** +0.85 (started at −0.52). Lower than v5 on purpose: the easy
  win is gone.
- **Evaluation (400 nights per cell, catch rate with 95% Wilson interval):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 92.2 % (89.2–94.5) | 83.8 % (79.8–87.0) | 46.5 % (41.7–51.4) |
  | Normal | 91.5 % (88.4–93.8) | 71.2 % (66.6–75.5) | 33.5 % (29.0–38.3) |
  | Expert | 81.8 % (77.7–85.2) | 48.0 % (43.1–52.9) | 17.5 % (14.1–21.5) |

- **Observations:** the exit camping is gone and he searches again (1.1–4.9 spots per night against
  0.1–0.5 in v5). The trained policy still beats the scripted hunter in every cell, by 8 to 34
  points, with intervals that do not overlap against Normal and Expert. Probing the network shows a
  new bias: he heads for the staff wing, where the player starts and where most of the night
  begins, and reacts to noises there (a fuse in the Kitchen: 100% go to the Kitchen), but often
  ignores noises in the public wing, even the exit panel in the Exit Lobby. Average night length
  against experts grew from 36 to 77 decisions: the hunt lasts longer.
- **Decision:** keep. `unity/Assets/ML/Models/freddy_v6.onnx` is the game's brain. Possible next
  step: a random player start, so the network cannot rely on where the night begins.

### freddy_v7 — 2026-10-27

- **Changed from previous run:** the new map and the new objectives. Miguel's 13-room layout: the
  staff and public wings are joined only through the north wing (Banquet Room and Veranda). The
  three fuses became three power cells carried one at a time to three sockets (Parts & Service,
  Banquet Room, Party Room), plus the breakers (night 3), the keycard (night 4) and the tape box
  (night 5); the exit opens with a lever (`ExitWorkTicks` 6 → 2). The night is drawn at random
  in training (`RandomNight`). Observations 64 → 74, actions 15 → 17; input 54 is now the share of
  objectives done. Same reward. The training scene is now rebuilt on every build, after the old
  one turned out to keep stale settings (`ExitWorkTicks` 6).
- **Config changes:** none (5M steps).
- **Steps trained / wall time:** 5,000,000 steps in 79 min.
- **Final mean cumulative reward:** +0.98 (started near 0).
- **Evaluation (400 nights per cell, catch rate with 95% Wilson interval):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 100.0 % (99.1–100.0) | 90.0 % (86.7–92.6) | 81.8 % (77.7–85.2) |
  | Normal | 99.8 % (98.6–100.0) | 74.0 % (69.5–78.1) | 75.8 % (71.3–79.7) |
  | Expert | 99.0 % (97.5–99.6) | 50.2 % (45.4–55.1) | 55.8 % (50.8–60.5) |

- **Observations:** the trained policy wins every cell with intervals far from the baselines (+10
  to +49 points over the scripted hunter). It searches 1.6–2.6 spots per night (scripted 5–11) and
  catches faster against Novice and Normal; against experts nights last 52 decisions on average.
  The new map helps the hunter: every trip between the wings goes through the north wing, so even
  the random hunter catches 56–82 % (17–47 % in v6). Part of v7's strength is that bottleneck;
  the survivor bot also has more work per night (up to seven objectives, each one noisy).
- **Decision:** kept at first, replaced by freddy_v8 the next day (see below).

### freddy_v8 — 2026-10-28

- **Changed from previous run:** one new reward term, +0.02 when Freddy walks into a room where he
  has just heard a noise (noise memory at least 0.3, `FreddyRewards.InvestigatedNoise`). Same map,
  objectives, observations (74) and actions (17).
- **Why:** measuring v7 in the simulation (a console harness running the exported weights against
  the survivor bot, 300 nights per preset) showed that it went to a noise in the next room only
  35 % of the time, against 95 % for the scripted hunter. It caught the bot by cutting it off in the
  north wing and predicting its route from the objectives done. That wins against the bot, but for
  a person the stealth and the party horns stop mattering, and the story tells the player that
  Freddy hunts by ear.
- **Config changes:** none (5M steps).
- **Steps trained / wall time:** 5,000,000 steps in 80 min.
- **Final mean cumulative reward:** +0.99 (the bonus adds a little to v7's +0.98).
- **Behavior (harness, 900 nights):** goes to a noise in the next room 93 % of the time (v7: 35 %),
  moves closer to a noise two or more rooms away 87 % (v7: 35 %); 2.2 searches and 1.2 senses per
  night; caught 99.8 %. Already at 2M steps the noise-following was 86 %.
- **Evaluation (400 nights per cell, catch rate with 95% Wilson interval):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 99.8 % (98.6–100.0) | 90.0 % (86.7–92.6) | 81.8 % (77.7–85.2) |
  | Normal | 99.8 % (98.6–100.0) | 74.0 % (69.5–78.1) | 75.8 % (71.3–79.7) |
  | Expert | 99.5 % (98.2–99.9) | 50.2 % (45.4–55.1) | 55.8 % (50.8–60.5) |

- **Observations:** the same strength as v7 with the behavior the game needs: he answers noises,
  so walking, crouching and the party horns matter again. The lesson for the report: a policy can
  be optimal against its training opponent and still wrong for the people who will play against
  it; catch rate alone did not show it, a behavioral measurement did.
- **Decision:** kept until a playtest showed a gap it shares with v7; replaced by freddy_v10.

### freddy_v9 — 2026-10-28

- **Changed from previous run:** two new observation blocks (74 → 100 inputs): for each room next
  to Freddy, whether a step there brings him closer to the loudest noise he remembers, and whether
  it brings him closer to where he last saw or sensed the player. The survivor bot got a
  `Curiosity` setting (randomized 0–0.3 in training): detours to a random room before its next goal.
- **Why:** a playtest of night 2 and night 3. The player made noise in the Dining Hall while Freddy
  went back and forth between the Main Hall and the Arcade, next door, for half a minute, each move
  chosen with 81–99 % confidence. The harness, split by pairs of rooms, showed the cause: of 341
  noises heard in the next room, 286 were "in the Veranda, noise in the Banquet Room"; "in the
  Arcade, noise in the Dining Hall" happened 7 times in 1,000 nights and v8 went there once. The
  Dining Hall has no objective, so the bot almost never enters it, and with one-hot rooms the
  network has to learn every pair of rooms from experience.
- **Config changes:** none (5M planned).
- **Steps trained / wall time:** stopped at 3,212,546 steps (about 1 h 50 min, including a pause
  while the computer slept), once the measurements below showed the inputs alone would not fix it.
- **Final mean cumulative reward:** +0.98.
- **Behavior (harness, 1,000 nights):** at 3M steps it went to a noise in the next room 88 % of
  the time and moved closer to a farther noise 71 %, catching 98.8 %. Asked directly (in the Arcade,
  a loud noise in the Dining Hall, sense not ready) it chose the Main Hall 90 % and the Dining Hall
  9 %; with sense ready, sense 98 %, the Dining Hall 0 %.
- **Observations:** the new inputs made noise-following generalize faster, but the learned habit
  stayed: a room the bot never needs is a room not worth checking, whatever Freddy hears there. The
  more curious bot raised that situation only from 7 to about 8–9 times in 1,000 nights.
- **Decision:** discard as the game's brain; its weights are the starting point of freddy_v10.

### freddy_v10 — 2026-10-28

- **Changed from previous run:** the reward. A step toward the freshest noise Freddy remembers (or
  into its room) earns +0.05 (v8: +0.02, only for walking into the room); a move that does not
  bring him closer to a fresh noise costs −0.03. Searching and sensing are never charged.
  Inputs (100) and the curious bot as in v9.
- **Why:** v9's measurements above: the habit was not a lack of information but what the reward
  made worth doing.
- **Config changes:** fine-tuning from freddy_v9's weights (`--initialize-from=freddy_v9`) with
  `training/config/freddy_ppo_finetune.yaml`, the same setup for 2.5M steps.
- **Steps trained / wall time:** 2,500,000 steps in 43 min (5.7M in total with v9's).
- **Final mean cumulative reward:** +1.02 (the noise bonus adds to v9's +0.98).
- **Behavior (harness, 1,500 nights):** goes to a noise in the next room 96 % of the time (v8: 93 %),
  moves closer to a farther noise 94 % (v8: 87 %); 2.3 searches and 1.4 senses per night; caught
  99.7 %. Asked directly, in the Arcade with a loud noise in the Dining Hall: with sense not ready
  it goes to the Dining Hall 100 % (80 % with a faint noise); with sense ready it senses first (99 %).
  Two rare situations remain weak: in the Banquet Room with a noise in the Kitchen it goes to the
  Veranda (98 %), and in the Back Hall with a noise in the Restrooms it goes there 6–7 %.
- **Evaluation (400 nights per cell, catch rate with 95% Wilson interval):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 100.0 % (99.1–100.0) | 96.0 % (93.6–97.5) | 87.0 % (83.4–90.0) |
  | Normal | 99.8 % (98.6–100.0) | 82.2 % (78.2–85.7) | 80.2 % (76.1–83.9) |
  | Expert | 97.8 % (95.8–98.8) | 55.8 % (50.8–60.5) | 57.2 % (52.4–62.0) |

  The baselines catch more than in v8's table because the evaluation bots are now curious: detours
  cost them time. The trained policy keeps the same lead over both.
- **Observations:** the lesson for the report is the same as v8's, one level deeper. A catch rate of
  99.8 % hid a whole room Freddy would never enter; only a person playing found it, and only
  asking the network about one exact situation explained it. Measuring by pairs of rooms is now
  part of checking a policy.
- **Decision:** kept until the next playtest; replaced by freddy_v13 (below).

### freddy_v11 — 2026-10-29

- **Changed from previous run:** two reward terms for when Freddy has nothing fresh to go on (no
  remembered noise ≥ 0.3, no sighting or sense in the last 20 decisions): +0.04 × how long the room
  he walks into had gone unchecked (0–1), and −0.03 for walking straight back into the room he just
  left. Fine-tuned from freddy_v10.
- **Why:** the next playtest. Freddy paced between the Main Hall and the Arcade again, now hearing
  the player in the Banquet Room. The harness, now also counting where Freddy decides and how often
  he goes straight back, showed v10 spending 86 % of its decisions in three rooms (Arcade 40 %, Main
  Hall 26 %, Veranda 20 %), never entering the staff wing, with 61 % of its moves straight back to
  the room it had left (the scripted hunter: 18 %). It waited on the only way between the wings,
  which the bot has to cross almost every night.
- **Config changes:** fine-tuning (`freddy_ppo_finetune.yaml`, `--initialize-from=freddy_v10`).
- **Steps trained / wall time:** stopped at 1,062,233 steps (18 min).
- **Behavior (harness, 1,000 nights at 1M steps):** backtracking with nothing to go on 54 % → 35 %,
  still 0 % of decisions in the staff wing and 84 % in the same three rooms; caught 99.0 %.
  With the live sockets drawn at random (next run) it still caught 99.5 % from the same rooms.
- **Decision:** stopped. Rewards cannot outweigh a map that makes waiting on one crossing optimal;
  its weights start freddy_v12.

### freddy_v12 — 2026-10-29

- **Changed from previous run:** the map and the objectives, not the reward. A service corridor
  joins the Back Hall and the Main Hall (three decisions long), so there are two ways between the
  wings (map diameter 8 → 5). Eight rooms have a power socket; each night three of them are live,
  at random, and Freddy does not observe which (`HuntSettings.RandomSockets`), as was done for the
  exits in v6. Rewards as in v11. Fine-tuned from freddy_v11.
- **Why:** v11's measurements. Even with random live sockets the bot crossed the north wing almost
  every night (cells, keycard, tape, the front exit). It was the team's idea to break the camping
  the way the two exits once did; the data showed the crossing, not the sockets, had to change.
- **Config changes:** fine-tuning (`--initialize-from=freddy_v11`), 2.5M steps.
- **Steps trained / wall time:** 2,500,008 steps; about 45 min of training (the run sat paused
  for hours while the computer slept).
- **Behavior (harness, 1,500 nights):** straight back to the room he just left 16 % of moves (2 %
  with nothing to go on; scripted 24 % / 19 %); goes to a noise in the next room 95 %; caught 93.0 %
  (scripted 85.9 %). But 50 % of his decisions were in the Main Hall, where both crossings meet,
  standing and listening, and none in the Office, Parts & Service, Restrooms or Storage.
- **Decision:** replaced by freddy_v13 (one more reward term); its weights start v13.

### freddy_v13 — 2026-10-29

- **Changed from previous run:** −0.02 for staying put to listen with nothing fresh to go on, and
  the patrol bonus doubled (0.04 → 0.08 × staleness). Fine-tuned from freddy_v12.
- **Why:** v12 stood listening in the Main Hall half the time.
- **Config changes:** fine-tuning (`--initialize-from=freddy_v12`), 2.5M steps.
- **Steps trained / wall time:** 2,500,107 steps in 43 min.
- **Final mean cumulative reward:** +1.9 (v12: +1.25): most of the rise is patrol bonus.
- **Behavior (harness, 1,000 nights per checkpoint):** at 1M steps it hunted the whole building
  (busiest room 22 %, 2.0 searches and 1.8 senses a night, caught 89.8 %); from there it traded the
  hunt for the bonus: 0.8 searches a night at 1.75M, 0.3 at 2.5M, caught 80 %.
- **Evaluation (final model, 400 nights per cell):** trained 94.5 / 81.5 / 65.0 % against
  Novice / Normal / Expert, scripted 99.8 / 92.5 / 72.0 %, random 78.5 / 54.2 / 39.8 %.
- **Observations:** reward hacking. The patrol bonus was meant to make him search the building and
  became the goal itself; the mean reward kept rising while the behavior the reward stood for got
  worse. The checkpoint to keep had to be chosen by behavior, and only five checkpoints are kept,
  so the good one (1M) was already gone.
- **Decision:** discard; the same run stopped at 1M steps is freddy_v14.

### freddy_v14 — 2026-10-29

- **Changed from previous run:** nothing in the simulation; v13's setup fine-tuned from freddy_v12
  for 1M steps only (early stopping), keeping every checkpoint
  (`training/config/freddy_ppo_finetune_1m.yaml`).
- **Steps trained / wall time:** 1,000,016 steps in 17 min.
- **Final mean cumulative reward:** +1.75.
- **Behavior (harness, 1,000 nights):** caught 88.8 % (scripted 85.9 %); 2.5 searches and 1.8 senses
  a night; goes to a noise in the next room 91 %; 18 % of moves straight back (8 % with nothing to go
  on; v10: 61 % / 54 %). Decisions by room: Main Hall 24, Arcade 18, Back Hall 17, Veranda 13,
  Banquet 10, Kitchen 9, Dining Hall 4, Restrooms 3, Parts 2 %, Office, Storage, Party Room and
  Exit Lobby 0–1 % (v10: 86 % in three rooms, none in the staff wing). Asked directly, with the
  rooms recently checked and sense not ready: in the Arcade with a noise in the Dining Hall it goes
  there (100 %), with a noise in the Banquet Room it goes to the Veranda (100 %), and in the Main
  Hall with a noise in the Kitchen it takes the new corridor (100 %; v10 went to guard the Party
  Room). Still weak: in the Banquet Room with a noise in the Kitchen it goes to the Veranda (70 %),
  in the Back Hall with a noise in the Restrooms to the Kitchen (66 %).
- **Evaluation (400 nights per cell, catch rate with 95% Wilson interval):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 98.5 % (96.8–99.3) | 99.8 % (98.6–100.0) | 78.5 % (74.2–82.2) |
  | Normal | 89.5 % (86.1–92.1) | 92.5 % (89.5–94.7) | 54.2 % (49.4–59.1) |
  | Expert | 77.8 % (73.4–81.5) | 72.0 % (67.4–76.2) | 39.8 % (35.1–44.6) |

- **Observations:** with two ways between the wings, catching the bot is harder for every hunter,
  and the trained policy no longer wins by waiting on a bottleneck: it is level with the scripted
  hunter against novice and normal survivors (overlapping intervals), ahead against experts, and
  far ahead of random. It searches far less than the scripted hunter (0.7–4.6 against 4.5–15 spots a
  night) and catches experts more often, so it finds people by ear and by sense rather than by
  opening every locker. The lesson of v9–v14 for the report: a 99 % catch rate came from a map
  that let the policy wait; a playtest found it, behavioral measurements explained it, and the fix
  was in the environment (a second way across, random sockets), with the reward only finishing it.
- **Decision:** keep until freddy_v17; fine-tuned into freddy_v15.

### freddy_v15 — 2026-10-29

- **Changed from previous run:** the rewards of a hunter instead of a patrol. +0.05 for opening a
  spot in the room a fresh clue points to (a noise, or a sense or sighting in the last 20
  decisions); −0.05 (was −0.03) for walking somewhere that does not bring him closer to a fresh
  noise; the patrol and lingering terms of v13 removed. In the simulation the player waits 14
  actions between ducts (the game: 25 s), and when Freddy hears a grate rattle the duct's exit
  room enters his noise memory at level 2. The survivor bot gets a `Curiosity` setting (detours
  into rooms off its route). Fine-tuned from freddy_v14.
- **Why:** a playtest of v14 found it walked a loop past fresh noises and never opened a hiding
  spot; the player could also chain ducts to stay out of reach.
- **Steps trained / wall time:** 2,500,000 steps in 65 min.
- **Final mean cumulative reward:** +1.14 (rewards changed, not comparable with v14).
- **Behavior (harness, 200 nights per checkpoint):** caught 96.0–98.0 % across checkpoints;
  9–10 % of moves straight back, none of them blind; 1.8–2.0 searches and 1.3–1.4 senses a night;
  goes to a noise in the next room 84–89 %. Decisions by room: Main Hall 27–30, Arcade 17–18,
  Veranda 13–14, Back Hall 12, Banquet 10–11, Kitchen 9–10, Dining Hall 6–7 %; the Office,
  Storage, Parts and Restrooms 0 %.
- **Observations:** the loop is gone and he searches where clues point. Against the bot he
  rarely needs the staff wing, but that bot still had the radar the game had just lost.
- **Decision:** fine-tune further against a bot that hears instead of seeing (freddy_v16).

### freddy_v16 — 2026-10-29

- **Changed from previous run:** the survivor bot's tracker reaches 1 room instead of 2. The game
  no longer has a radar (it is a developer option); the player locates Freddy by ear, about one
  room away. Fine-tuned from freddy_v15.
- **Steps trained / wall time:** 2,500,000 steps in 57 min.
- **Final mean cumulative reward:** +1.10.
- **Behavior (harness, 200 nights per checkpoint, the hearing-only bot):** caught 96.8–98.3 %
  (v15 against the same bot: 95.3 %), 13.7–15.5 decisions a night (v15: 16.5); goes to a noise in
  the next room 95–96 %; 11–13 % of moves straight back. Still 0 % of decisions in the Office,
  Storage and Restrooms. Asked directly: in the Back Hall with a noise in the Office he stays and
  listens (100 %); in the Kitchen with a noise in Storage, stays 60 %.
- **Observations:** a loophole in the reward. Only walking away from a fresh noise was
  penalized; standing still beside it cost nothing. The Office and Restrooms open only onto the
  Back Hall, so waiting at that door catches whoever comes out without going in: camping again,
  in a smaller place.
- **Decision:** close the loophole (freddy_v17).

### freddy_v17 — 2026-10-29

- **Changed from previous run:** staying put while a fresh noise waits in another room counts as
  ignoring it (−0.05), the same as walking away (`HuntState.ApplyFreddyAction`). Fine-tuned from
  freddy_v16 for 1M steps (`freddy_ppo_finetune_1m.yaml`).
- **Steps trained / wall time:** 1,000,027 steps in 23 min.
- **Final mean cumulative reward:** +1.11.
- **Behavior (harness, 300 nights):** caught 98.3 %; goes to a noise in the next room 98 % (519
  times), including the Parts room (6 of 6) and the Restrooms; 16 % of moves straight back, none
  blind; 1.6 searches and 0.8 senses a night. Asked directly, in the Back Hall with a noise in the
  Office he now stays 79 % (v16: 100 %). In the simulation that situation came up 3 times in 300
  nights: the staff wing is quiet when Freddy is next to it.
- **Evaluation (400 nights per cell, catch rate with 95 % Wilson interval; the survivors hear
  Freddy one room away and wait between ducts, so the numbers are not comparable with v14's):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 99.2 % (97.8–99.7) | 99.5 % (98.2–99.9) | 88.8 % (85.3–91.5) |
  | Normal | 99.5 % (98.2–99.9) | 98.2 % (96.4–99.2) | 77.2 % (72.9–81.1) |
  | Expert | 98.0 % (96.1–99.0) | 91.5 % (88.4–93.8) | 73.0 % (68.5–77.1) |

- **Observations:** level with the scripted hunter against novice and normal survivors, ahead
  against experts (intervals do not overlap), with fewer searches (1.0–2.3 against 2.3–4.6 a
  night). Without a radar every hunter catches more, so the comparison that matters is between
  hunters on the same bots, not with earlier runs.
- **Decision:** keep until freddy_v19; fine-tuned into freddy_v18.

### freddy_v18 — 2026-10-29

- **Changed from previous run:** with no fresh noise, a sighting or a sense in the last 20
  decisions counts as a clue the same way (moving away from it or staying put ignores it, −0.05);
  and staying put with the clue in his own room while a spot there is still shut is camping (−0.05).
  `SearchedClue` uses the same test. Fine-tuned from freddy_v17 for 1M steps.
- **Why:** a playtest of v17: it saw the guard from afar and never went to look, and it stood beside
  the Main Hall locker the guard hid in, "stay and listen" four decisions in a row.
- **Steps trained / wall time:** 1,000,037 steps in 19 min.
- **Final mean cumulative reward:** +1.16.
- **Behavior (harness, 300 nights):** caught 98.8 %. After seeing the guard in another room (no
  fresh noise) he moves closer 55 % and stays 1 % (v17: 42 % and 20 %; scripted 44 % and 0 %).
  With the guard hidden in his room, a clue there and a spot shut, he opens a spot 60 % (v17: 50 %)
  and leaves 34 %; he never stays.
- **Observations:** in the simulation neither v17 nor v18 ever stayed twice beside the hidden
  guard, so the playtest's camping came from a state the simulation does not produce: Freddy had
  already opened that locker (the guard held their breath), so for him it was checked, and when the
  guard then gasped he stood by it listening. Opening it again was a repeated search (−0.02).
- **Decision:** fix the state and the reward (freddy_v19).

### freddy_v19 — 2026-10-29

- **Changed from previous run:** staying a second time in a row where any trace of the guard is
  left (a noise not yet forgotten, a sighting or sense in memory) counts as ignoring it, spots
  opened or not. In the simulation and the game, a fresh noise in Freddy's own room makes every
  spot there unopened again (`HuntState.Remember`): the guard may have held their breath. Fine-tuned
  from freddy_v18 for 1M steps; the spot rule was added after training and the model measured with it.
- **Steps trained / wall time:** 1,000,001 steps in 21 min.
- **Final mean cumulative reward:** +1.20.
- **Behavior (harness, 300 nights, with the spot rule):** caught 98.4 %; goes to a noise in the next
  room 93 %; after a sighting moves closer 58 %, stays 1 %; with the guard hidden in his room and a
  clue there he opens a spot 58 %, leaves 38 %, never stays; never stays twice in the room the guard
  hides in (275 cases).
- **Evaluation (400 nights per cell, catch rate with 95 % Wilson interval):**

  | Survivor | Trained | Scripted | Random |
  |---|---|---|---|
  | Novice | 100.0 % (99.1–100.0) | 99.5 % (98.2–99.9) | 88.8 % (85.3–91.5) |
  | Normal | 98.5 % (96.8–99.3) | 98.5 % (96.8–99.3) | 77.2 % (72.9–81.1) |
  | Expert | 97.8 % (95.8–98.8) | 91.5 % (88.4–93.8) | 73.0 % (68.5–77.1) |

- **Observations:** level with the scripted hunter against novice and normal survivors and ahead
  against experts, searching less (1.5–4.0 spots a night against 2.3–4.6). The lesson of v16–v19:
  a camping loophole closed in one place (the next room) reappears in the next (his own room, a
  checked locker); a playtest finds it, the harness measures it, and the reward closes it.
- **Decision:** keep. `unity/Assets/ML/Models/freddy_v19.onnx` is the game's brain.

