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
- **Decision:** keep. `unity/Assets/ML/Models/freddy_v8.onnx` is the game's brain.

