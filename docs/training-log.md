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

The arena runs about 1,400 steps per second on an M-series Mac, so 3M steps take ~40 minutes.
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
