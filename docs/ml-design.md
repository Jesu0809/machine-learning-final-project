# ML Design

This document defines the learning problem behind the monster and the exact rules of a night. It
is the reference for the code in `unity/Assets/Scripts/Core/` and `unity/Assets/Scripts/Monster/`
and for the final report. Values marked *initial* are starting points and may change during
training; every change is recorded in [training-log.md](training-log.md).

## 1. Why reinforcement learning

The monster has no labeled data telling it which move is correct. It only learns whether a night
ended well (it reached the office) or badly (the night ended, or a door blocked it), and that
feedback arrives many decisions after the moves that caused it. This is the setting of
reinforcement learning: an agent interacts with an environment, receives rewards, and learns a
policy that maximizes the expected cumulative reward.

We use **PPO (Proximal Policy Optimization)**, the default trainer of Unity ML-Agents. PPO trains a
neural network policy directly and limits how much the policy can change in one update, which
makes training stable with little tuning.

## 2. The MDP

| Element | Definition |
|---|---|
| Agent | The monster |
| Environment | Room graph, two doors, power, night clock and the guard (a human when playing, `PlayerBot` during training) |
| State | Monster room, door states, power, night progress, camera history (section 3) |
| Action | Which room to go to next (section 4) |
| Time step | One monster decision per tick (`DecisionIntervalSeconds` = 4 s) |
| Episode | One night |
| Discount factor γ | 0.99 (ML-Agents default) |
| Algorithm | PPO |

### Room graph

```
Stage(0) ── Hall(1) ── WestCorridor(2) ── [Left door] ── Office(5)
               │
            Kitchen(3) ── EastCorridor(4) ── [Right door] ── Office(5)
```

| Room | Index | Neighbors | Distance to office |
|---|---|---|---|
| Stage | 0 | Hall | 3 |
| Hall | 1 | Stage, WestCorridor, Kitchen | 2 |
| WestCorridor | 2 | Hall, Office (left door) | 1 |
| Kitchen | 3 | Hall, EastCorridor | 2 |
| EastCorridor | 4 | Kitchen, Office (right door) | 1 |
| Office | 5 | terminal | 0 |

The monster starts every night in **Stage**. WestCorridor and EastCorridor are the "at the door"
rooms: only from there can the monster try to enter the office.

### Episode termination

| Condition | Outcome | Terminal reward | ML-Agents call |
|---|---|---|---|
| Monster enters the office through an open door | `MonsterEntered` (agent succeeds) | +1.0 | `EndEpisode()` |
| Night clock reaches 6 AM | `GuardSurvived` (agent fails) | −0.5 | `EndEpisode()` |
| `MaxStepsPerEpisode` decisions reached (safety limit, *initial* 100) | `StepLimitReached` | none | `EpisodeInterrupted()` |

A 300 s night with one decision every 4 s ends at the 75th tick, before the monster acts in that
tick, so the monster gets at most 74 decisions and the safety limit never triggers in normal play.
`EpisodeInterrupted()` tells PPO that the episode was cut, not finished, so the last state is not
treated as terminal.

## 3. Observations

15 vector observations, all in [0, 1] (`Space Size = 15` in Behavior Parameters).

| # | Observation | Size | Encoding |
|---|---|---|---|
| 0–4 | Current room | 5 | One-hot over rooms 0–4 (the office is terminal, never observed) |
| 5 | Left door closed | 1 | 0 or 1 |
| 6 | Right door closed | 1 | 0 or 1 |
| 7 | Power remaining | 1 | `power / maxPower` |
| 8 | Night progress | 1 | `elapsed / nightDuration` |
| 9–13 | Time since each room's camera was last viewed | 5 | `clamp(t / maxWindow, 0, 1)`, *initial* `maxWindow` = 30 s |
| 14 | Guard is watching the monster's room right now | 1 | 0 or 1 |

Observations are read at decision time, before the monster moves. Observations 9–14 are what let
the monster use the guard's blind spots (section 8).

## 4. Actions

### Action space

One discrete branch of size 6. **The action is the index of the target room**:

| Action | Meaning |
|---|---|
| 0 | Go to Stage |
| 1 | Go to Hall |
| 2 | Go to WestCorridor |
| 3 | Go to Kitchen |
| 4 | Go to EastCorridor |
| 5 | Try to enter the Office |

Choosing the room the monster is already in means **stay** (wait, hide). We chose this encoding
over a separate "stay" action because it keeps the branch at size 6 with no empty slot, every
action has exactly one meaning in every room, and masking becomes a plain adjacency check.

### Legal actions per room (action masking)

Before each decision, `WriteDiscreteActionMask` disables every action that is not legal, so the
policy never wastes experience on impossible moves.

| Current room | 0 Stage | 1 Hall | 2 West | 3 Kitchen | 4 East | 5 Office |
|---|---|---|---|---|---|---|
| Stage | stay | ✔ | ✖ | ✖ | ✖ | ✖ |
| Hall | ✔ | stay | ✔ | ✔ | ✖ | ✖ |
| WestCorridor | ✖ | ✔ | stay | ✖ | ✖ | ✔ enter |
| Kitchen | ✖ | ✔ | ✖ | stay | ✔ | ✖ |
| EastCorridor | ✖ | ✖ | ✖ | ✔ | stay | ✔ enter |

The number of legal actions is 2 in Stage, 4 in Hall and 3 in every other room.

**The door state is not masked.** Trying to enter through a closed door is legal but penalized.
The door state is in the observations, so learning not to hit a closed door is part of the task.
Masking it would hide exactly the decision we want the agent to learn.

### What each action does

| Situation | Result (`MoveResult`) | Monster ends in |
|---|---|---|
| Target = current room | `Stayed` | same room |
| Target is a neighbor closer to the office | `MovedCloser` | target |
| Target is a neighbor farther from the office | `MovedAway` | target |
| Target is a neighbor at the same distance (Hall ↔ Kitchen) | `MovedSideways` | target |
| Target = Office, that corridor's door is open | `EnteredOffice`, night ends | Office |
| Target = Office, that corridor's door is closed | `BlockedByDoor` | Hall (pushed back) |
| Illegal target (only possible without masking) | counted in `IllegalActions`, treated as `Stayed` | same room |

The monster is **seen while moving** when it changes room (any result except `Stayed`) and the
guard is watching the room it leaves or the room it ends in at that moment. Entering the office is
seen only if the guard is watching the corridor it leaves (the office has no camera).

### Keyboard control (Heuristic)

With Behavior Type = Heuristic Only, keys `0`–`5` (top row or numpad) choose the target room for
the next decision. No key, or an illegal key, means stay. This lets us test the environment and the
rewards by hand before training.

## 5. Rules of a night

### Guard actions

The guard (human or `PlayerBot`) can, at any time:

| Action | Effect | Not allowed when |
|---|---|---|
| View camera of room 0–4 | Opens the panel on that room (or switches to it). That room's "time since viewed" stays at 0 while it is on screen | Power is out |
| Close cameras | Back to the office view | — |
| Toggle left / right door | Closes or opens that door | Power is out (doors stay open) |

There is no camera in the office. Only one room can be watched at a time.

### Power

`drain per second = 0.12 + 0.25 × closed doors + 0.10 × (camera open)` (*initial*), starting from
100. With both doors closed and the camera open the whole night, power lasts about 139 s of the
300 s night. When power reaches 0:

1. Both doors open and stay locked open for the rest of the night.
2. The camera panel closes and cannot be opened again.
3. The monster can now enter from either corridor.

### Order inside one tick

Every tick (4 s in play mode, one fixed step in training) runs in this order:

1. **Time:** the clock, power drain and camera counters advance by the tick length.
2. **Power outage:** if power reached 0, apply the outage rules above.
3. **6 AM:** if the clock reached the end of the night, the guard survives and nothing else
   happens in this tick.
4. **Guard:** the bot acts once (a human may have acted at any moment since the last tick).
5. **Monster:** observes, chooses an action (with masking) and the action is applied.
6. **Entry:** if the monster entered, the night ends immediately.

Because the guard acts before the monster within a tick, a door closed in step 4 already blocks an
entry attempt in step 5.

## 6. Reward function (*initial*, `monster_v1`)

| Event | Reward |
|---|---|
| Every decision (time pressure) | −0.005 |
| `MovedCloser` | +0.05 |
| `MovedAway` | −0.05 |
| `BlockedByDoor` (pushed back to Hall) | −0.3 |
| Seen while moving | −0.05 |
| `EnteredOffice` | +1.0, episode ends |
| Night ends without entering | −0.5, episode ends |

`Stayed` and `MovedSideways` only receive the time penalty.

The terminal rewards (+1.0 / −0.5) define the goal. The small shaping terms only guide early
exploration so the agent finds the office before the night ends.

**Why "moved away" is −0.05 and not smaller.** The distance terms are *potential-based shaping*
(Ng, Harada & Russell, 1999): the bonus for getting one step closer is exactly cancelled by the
penalty for stepping back, so the sum of shaping along any route only depends on where it starts
and ends. With an asymmetric pair such as +0.05 / −0.02, going Hall → WestCorridor → Hall would
earn +0.03 every two steps, and the agent could learn to walk back and forth instead of entering.
With the symmetric pair, every go-and-come-back loop has a negative total (−0.01 from the time
penalty), so no loop can be farmed. Being pushed back by a closed door uses only the −0.3
penalty, which is already larger than any distance term.

**Rule for iterations:** if training does not improve after a few runs, *remove* terms instead of
adding new ones. Simpler rewards are harder to exploit.

## 7. Training opponent: PlayerBot

During training the human is replaced by `PlayerBot`. If the bot were deterministic, the monster
would learn to exploit one fixed pattern, so the bot is stochastic and its parameters are
randomized at the start of every episode (**domain randomization**).

| Parameter | Training range (*initial*) |
|---|---|
| Reaction probability (closes the door when it sees the monster at it) | 0.4 – 0.9 |
| Reaction delay | 0 – 2 ticks |
| Camera check frequency | one check every 1 – 4 ticks |
| Preferred rooms | random weights, biased toward WestCorridor and EastCorridor |

Fixed presets for evaluation:

| Preset | Reaction probability | Reaction delay | Check every |
|---|---|---|---|
| Easy | 0.4 | 2 ticks | 4 ticks |
| Normal | 0.65 | 1 tick | 2 ticks |
| Hard | 0.9 | 0 ticks | 1 tick |

**Balance warning.** The route to the office is only 3 moves long and a night has 74 decisions. In
a first simulation against a crude guard (random camera, closes a door 70 % of the times it sees
the monster at it) the random monster won 98.7 % of nights and the scripted one 100 %. If the bot
is that weak, there is nothing left for the trained agent to improve. The bot must defend well
enough that the scripted monster fails often (target: well under 50 % on Hard), so that
outsmarting the cameras actually matters. Tune the bot and the power numbers before training.

## 8. "Learns where you don't look": what it really means

ML-Agents trains **offline**. The exported model (`MonsterAgent.onnx`) is frozen inside the game
and **does not learn during a match**.

The adaptive behavior comes from the **observations**. The policy receives how long ago each camera
was viewed and whether the guard is watching its room right now. During training against many
different bots it learns the general strategy "move through the rooms the guard neglects". When a
human plays, the same frozen policy reacts to *that* player's camera habits in real time.

The optional `PlayerHabitTracker` (week 4) stores neglect statistics across nights with
`PlayerPrefs` and biases the monster's starting route. It is a **heuristic, not reinforcement
learning**, and is labeled that way everywhere.

## 9. Baselines and evaluation

Two non-learning monsters implement the same `IMonsterPolicy` interface as the trained agent:

| Policy | Behavior |
|---|---|
| `RandomMonsterPolicy` | Uniform random legal action each tick ("stay" included) |
| `ScriptedMonsterPolicy` | Always moves along the shortest path to the office and tries to enter as soon as it reaches a corridor; ignores cameras and doors |
| `MonsterAgent` (trained) | PPO policy loaded from `MonsterAgent.onnx` |

`EvaluationRunner` plays **200 nights** for each policy against each bot preset (3 policies × 3
presets = 1,800 nights) and records:

| Metric | Definition |
|---|---|
| Success rate | Nights where the monster reached the office / nights played |
| Average time to office | Mean simulated seconds until entering, over successful nights only |
| Door blocks | Mean number of times per night the monster tried to enter through a closed door |
| Power remaining | Mean guard power (0–100) at the end of the night |

### `results.json` format

Written to `training/evaluation/results.json` and copied to `web/static/data/results.json`:

```json
{
  "generated_at": "2026-11-01T18:00:00Z",
  "model": "monster_v3",
  "nights_per_run": 200,
  "runs": [
    {
      "policy": "trained",
      "bot_preset": "Normal",
      "nights": 200,
      "success_rate": 0.0,
      "avg_time_to_office_s": 0.0,
      "avg_door_blocks": 0.0,
      "avg_power_remaining": 0.0
    }
  ]
}
```

`policy` is one of `trained`, `random`, `scripted`; `bot_preset` is one of `Easy`, `Normal`, `Hard`.

### Training curves

`training/scripts/export_tensorboard.py` reads the TensorBoard events of a run and writes
`training_curves.csv` with the columns `step`, `cumulative_reward`, `episode_length`,
`policy_loss`. The `/how-it-works` page plots these.

## 10. Training setup (*initial*)

- Scene `TrainingArena` with 16–32 `EnvironmentInstance` copies, no UI.
- Each instance advances the simulation with a fixed tick instead of real time.
- Config: `training/config/monster_ppo.yaml`; run IDs `monster_v1`, `monster_v2`, …
- Output: `MonsterAgent.onnx` → `unity/Assets/ML/Models/`.

## 11. Limitations (for the report)

- The policy is frozen after training; there is no online learning during a match.
- The bot is an approximation of a human player; a policy that beats the bot may still feel
  different against real players.
- Small, discrete world (6 rooms, 2 doors). The scope is a playable alpha, not a full game.

## References

Ng, A. Y., Harada, D., & Russell, S. (1999). Policy invariance under reward transformations:
Theory and application to reward shaping. *Proceedings of the Sixteenth International Conference
on Machine Learning*, 278–287.

Schulman, J., Wolski, F., Dhariwal, P., Radford, A., & Klimov, O. (2017). Proximal policy
optimization algorithms. *arXiv:1707.06347*.
