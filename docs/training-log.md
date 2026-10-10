# Training Log

One entry per training run. Copy the template, fill it in right after the run, and never edit an
old entry: if something was wrong, write a new run.

Commands used for every run:

```bash
conda activate mlagents
cd training
mlagents-learn config/monster_ppo.yaml --run-id=monster_vN
# Standalone build instead of the Editor:
# mlagents-learn config/monster_ppo.yaml --run-id=monster_vN --env=builds/TrainingArena --no-graphics --num-envs=4
tensorboard --logdir results
```

---

## Template

### monster_vN — YYYY-MM-DD

- **Changed from previous run:**
- **Reward terms:**
- **Config changes (`monster_ppo.yaml`):**
- **Environment:** instances per scene, num-envs, PlayerBot ranges
- **Steps trained / wall time:**
- **Final mean cumulative reward:**
- **Observations:** (curve shape, behaviors seen when watching the agent)
- **Decision:** keep / discard, and why

---

<!-- Runs go below, newest last. -->
