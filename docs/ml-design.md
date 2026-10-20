# ML Design

This document defines the learning problem behind Freddy and the exact rules of a hunt. It is the
reference for the code in `unity/Assets/Scripts/Core/` and for the final report. Values marked
*initial* are starting points and may change during training; every change is recorded in
[training-log.md](training-log.md).

## 1. Idea: a learned brain with a scripted body

Freddy is an NPC built with **Unity ML-Agents**. It is split in two layers, the same split used by
the alien in *Alien: Isolation* (a "director" that decides and a body that executes):

| Layer | What it does | How it is made |
|---|---|---|
| **Brain** | Decides *where to go* and *which hiding spot to search*, from what Freddy has heard and seen | PPO policy trained with ML-Agents, run in the game as an `.onnx` model |
| **Body** | Walks to the chosen room, plays the animations, looks and listens in 3D | Unity NavMesh, Animator, raycasts (no learning) |

The brain is trained on an abstract version of the pizzeria: a graph of 11 rooms with hiding spots.
This makes training fast (millions of decisions in minutes) and the decisions readable for the
report. In the 3D game the body turns each decision into movement and reports back what Freddy
perceives.

## 2. The MDP

| Element | Definition |
|---|---|
| Agent | Freddy's brain |
| Environment | Room graph, hiding spots, fuses, exit and the player (a human when playing, `SurvivorBot` during training) |
| State | Freddy's room, noise memory, last sighting, time since each room was visited, fuses, time (section 5) |
| Action | Go to a neighboring room, stay and listen, or search a hiding spot (section 4) |
| Time step | One Freddy decision per tick (`DecisionIntervalSeconds` = 3.5 s in the 3D game) |
| Episode | One night |
| Discount factor γ | 0.99 |
| Algorithm | PPO |

It is a **partially observable** problem: Freddy never observes where the player is. He only gets
noises (sometimes placed in the wrong room) and occasional sightings, so he has to remember and
infer.

### Room graph

One floor in two wings (since `freddy_v5`; v1–v3 used a 3 × 3 grid of 9 rooms, v4 two floors of 14):

```
  STAFF WING                                PUBLIC WING
  Storage ── Kitchen ═════════════════════ DiningHall (stage) ── Arcade
     │          │                              │                   ║
  ══ BackHall ══╪═══════════ door ═════════ MainHall ═══════════════╝
     ║       │       │                         │
  Office ─ Parts  Restrooms                PartyRoom ────────── ExitLobby
```

`═` / `║` mark long connections: walking them takes **two decisions** (`HuntMap.Cost`): Office–Back
Hall, Back Hall–Main Hall, Kitchen–Dining Hall and Main Hall–Arcade. Every other connection takes one.

| Room | Hiding spots | Notes |
|---|---|---|
| Office | 2 | Player start (security office) |
| Parts & Service | 2 | |
| Restrooms | 2 | |
| Back Hall | 1 | Staff hall |
| Storage | 3 | Loading door: one of the two exits |
| Kitchen | 3 | |
| Dining Hall | 3 | Freddy start (on the stage) |
| Main Hall | 1 | Public hall |
| Party Room | 2 | The only way into the Exit Lobby |
| Arcade | 2 | |
| Exit Lobby | 1 | Front doors: one of the two exits |

Spot 0 is always a locker; the others are cabinets or places under tables. The map diameter is 5
moves. **Air ducts** join Office–Storage, Restrooms–Party Room, Kitchen–Arcade and Parts–Dining
Hall: only the player fits (4 actions to crawl through), and nobody sees into them.

**Two exits (`freddy_v6`).** Each night the way out is the front doors in the Exit Lobby or the
loading door in the Storage, chosen at random (`HuntSettings.RandomExit`). The player knows which
(the objective list says it); Freddy does not observe it, so he cannot camp one door and has to
hunt by sound (working an exit panel is the loudest noise in the game).

### Episode termination

| Outcome | When |
|---|---|
| `Caught` | Freddy finds the player (section 3) |
| `Escaped` | The player finishes the work on tonight's exit panel with all 3 fuses |
| `TimeUp` | `MaxTicks` = 240 decisions (about 10 minutes) pass with the player still inside |

## 3. Rules of a hunt

### Time

One tick = **2 player actions, then 1 Freddy action** (`PlayerActionsPerTick` = 2). Freddy moves
one room per tick, the same speed as a walking player; a long connection takes him two ticks
(**travel time**, `HuntSettings.TravelTime`, since `freddy_v5`), and a walking or running player
twice as many actions. This keeps the simulation at the pace of the 3D building, where a long
corridor is a long walk. A running player is twice as fast but loud. Freddy stays on the stage for
the first 6 ticks (`FreddyWakeTicks`), so the player can get going.

### Player actions

| Action | Effect | Noise |
|---|---|---|
| Wait | Nothing | 0 (silent) |
| Walk | Moves to a neighboring room in 2 actions | 1 |
| Run | Moves to a neighboring room in 1 action | 2 |
| Hide | Enters a hiding spot of the current room | 0 |
| Pick fuse | Takes the fuse in the room | 2 |
| Work exit | One of 6 actions on tonight's exit panel (progress is kept if the player leaves) | 3 |
| Crawl | Moves through an air duct to the room at its other end in 4 actions | 0 |

Three fuses are placed in random rooms with a fuse shelf at the start of each night, never in the
Office, the Exit Lobby or tonight's exit.

### What Freddy perceives

| Sense | Rule |
|---|---|
| Hearing | Noise level 1 is heard in the same room, 2 up to 1 room away, 3 up to 2 rooms away. With 25% chance a noise from another room is placed in a neighbor of the real room |
| Sight (same room) | A player who is not hidden in Freddy's room is caught with 75% chance per check (rooms have corners) |
| Sight (next room) | A player who is not hidden one room away is seen with 20% chance per player action |

### How the player is caught

1. Freddy enters (or stays in) the player's room and spots them (75%).
2. The player walks into Freddy's room and is spotted (75%).
3. Freddy searches the exact spot where the player hides: found with 80% chance, otherwise the
   player held their breath (`SearchFindChance`, as in *Alien: Isolation*).

## 4. Actions

One discrete branch with R + 4 actions, R = number of rooms: 15 since `freddy_v5` (13 in v3, 18 in
v4; "sense" was added in `freddy_v3`):

| Index | Action |
|---|---|
| 0–10 | Go to room *i* (if *i* is Freddy's room: stay and listen) |
| 11, 12, 13 | Search hiding spot 0, 1 or 2 of the current room |
| 14 | **Sense**: if the player's fear is at least 0.5, reveals their room. Then it recharges for 30 decisions (about 1 min 45 s) |

### Sense and fear (from *Friday the 13th*)

The player's fear is computed on the room graph, the same way in training and in the 3D game:
`fear = 1 − distance/4` (distance in rooms to Freddy), +0.3 if Freddy saw them in the last 4
decisions, −0.25 if they are hidden two or more rooms away (calm in a hiding spot), clamped to
[0, 1]. So "sense" finds a player within about two rooms, unless they hide calmly. Using it costs
the whole decision and a long recharge, and a sense that finds nothing costs −0.01: **when** to use
it is something the network has to learn.

### Action masking

Moves to rooms that are not connected and searches of spots that do not exist are masked, so the
policy only chooses among legal actions (Huang & Ontañón, 2022). For example, in the Main Hall the
legal actions are: stay, go to Back Hall, Party Room or Arcade, search spot 0, and sense if it has
recharged.

## 5. Observations

5R + 9 values (64 since `freddy_v5`; 43 in v1–v2, 54 in v3, 79 in v4), all in [0, 1], built only
from what Freddy knows (`FreddyObservations.cs`):

| Index | Observation |
|---|---|
| 0–10 | Freddy's room, one-hot |
| 11–21 | Noise memory per room (heard noise level / 3, fades ×0.8 per tick) |
| 22–32 | Room where the player was last seen, one-hot (zeros if never seen) |
| 33 | Ticks since the player was seen / 60 |
| 34–44 | Ticks since Freddy visited each room / 60 |
| 45 | Player seen this tick |
| 46 | Fuses collected / 3 (the generators light up the map, so Freddy knows) |
| 47 | Night progress (tick / 240) |
| 48–50 | Hiding spots of the current room already searched since arriving |
| 51 | Sense readiness (1 = ready, 0 right after using it) |
| 52–62 | Room where sense last found the player, one-hot |
| 63 | Decisions since that / 60 |

Tonight's exit is **not** an input: Freddy has to find it out from the noise of the panel.

## 6. Reward function (`freddy_v1`, with the terms added later marked)

| Event | Reward |
|---|---|
| Player caught | +1 |
| Player escaped | −1 |
| Time up | −0.5 |
| Every step | −0.001 |
| Search of an empty spot | −0.01 |
| Search of a spot already searched since arriving | −0.02 extra |
| Sense that finds nothing (`freddy_v3`) | −0.01 |
| Staying or sensing in the room where the player hides, instead of searching (`freddy_v6`) | −0.05 |
| Distance shaping | 0.1 · (γ·Φ(s′) − Φ(s)), with Φ(s) = −distance(Freddy, player) / diameter |

The `freddy_v6` term exists because a policy can win by **waiting**: next to a hidden player he
only has to stand still until they come out. That is a valid strategy in the simulation and a dull
one to play against, so waiting there now costs more than opening a spot (−0.01 if it is empty).
It does not stop him from searching: the catch (+1) still comes from opening the right spot.

The shaping term is **potential-based** (Ng et al., 1999), so it speeds up learning without
changing the optimal policy. It uses the player's real room, which Freddy never observes:
privileged information is used only in the reward, never in the inputs, so the trained policy
works in the game where that information does not exist.

## 7. Training opponent: SurvivorBot

During training the player is `SurvivorBot`, a scripted survivor with a motion tracker, like the
player in the 3D game:

1. It reads the tracker (chance `TrackerUse` per action), which shows Freddy's room up to 2 rooms
   away, and assumes Freddy may have come one room closer per tick since the last reading.
2. If Freddy is next to it: it hides (chance `HideChance`), choosing the locker with chance
   `Spot0Preference`. After a loud action or in a room with a single spot it runs away first.
3. If Freddy is 2 rooms away it waits in silence for a while (`Patience`).
4. Otherwise it walks (or runs, chance `RunChance`) to the nearest fuse and then to the exit,
   never stepping into Freddy's last known room, and not next to it until it has waited
   `MaxWaitActions` actions (then it risks it: people do not freeze forever).

**Domain randomization:** every training night uses random bot settings (tracker 0.5–0.97, hide
0.4–0.8, run 0.05–0.45, patience 2–5, max wait 3–10, locker preference 0.1–0.9) so the policy does
not overfit one play style. Evaluation uses three fixed presets:

| Preset | Tracker use | Hide | Run | Patience | Max wait |
|---|---|---|---|---|---|
| Novice | 0.55 | 0.75 | 0.40 | 2 | 3 |
| Normal | 0.80 | 0.60 | 0.20 | 3 | 6 |
| Expert | 0.95 | 0.45 | 0.08 | 3 | 8 |

### Balance (simulation before training)

400 simulated nights per cell on the two-wing map with random exits, run outside Unity on the
same C# code (catch rate):

| Freddy | vs Novice | vs Normal | vs Expert |
|---|---|---|---|
| Random | 46.0% | 31.3% | 19.8% |
| Scripted | 89.3% | 71.5% | 48.5% |

The map was tuned with these numbers before training: a second door into the Exit Lobby raised the
scripted hunter against experts from 48.5% to 67.8%, so the lobby keeps a single door.

Difficulty is monotonic (better survivors are caught less) and the scripted hunter is clearly
better than random but far from perfect, which leaves room for the trained policy.

## 8. From the graph to the 3D game

`HuntDirector` connects the 3D world to the same `HuntState` the brain was trained on
(`ResolveCatches = false`, because in 3D the body catches the player physically):

1. Every `DecisionIntervalSeconds / 2` it measures the real player: the room under their feet,
   whether they hide and in which spot, and the loudest noise since the last measurement
   (crouching 0, walking 1, running, picking up a fuse or gasping 2, the exit panel 3). This goes
   into `HuntState.ObservePlayer`, so Freddy hears with exactly the training rules.
2. When the body has finished its action, it puts the brain in the body's real room and asks the
   policy for the next action (the `.onnx` model through ML-Agents with `BehaviorType.InferenceOnly`).
3. The body carries the action out. If its eyes (a 120° raycast view cone, longer when the
   flashlight is on, blocked by walls) see the player, it reports the sighting to the brain and
   chases until it loses sight for 3 s; then the brain takes over again. If it saw the player get
   into a spot, it goes straight to that spot.
4. A search opens the spot; the player inside is caught unless they hold their breath.

### The building is not a grid

The 3D pizzeria keeps exactly the connections of the training graph, but the rooms have different
sizes and heights and are joined by corridors whose length matches their cost on the graph
(`PizzeriaLayout.cs`): two 3 m wide halls, a Dining Hall with the show stage on its north wall, and
two long corridors between the wings. The wings look different (grey-green staff rooms, red and
checkered public rooms) and green EXIT signs beside the doors mark the way to tonight's exit. Inside
a room Freddy's body walks straight lines between the room's hub, its doorways and its hiding
spots, and props are only placed where they leave those lines free. An automated audit
(`CaptureMode -audit`) walks every connection both ways and crawls every duct with the real
player collider.

### Survivor and body rules that are not learned

Inspired by *Friday the 13th: The Game*, the 3D player has **stamina** (a full sprint lasts about
4.5 s; empty, they are winded and pant) and **fear** (rises when Freddy is near or sees them; it
drains stamina faster, makes the flashlight sputter and the breathing audible in the room). Freddy's
body gets a **rage** bonus as the player progresses: +10% walking and +6% chasing speed per fuse,
and once more when the exit panel starts working. When the brain chooses to stay in a room, or
sends him to a new one, the body walks to one of the room's hiding spots or its middle at random,
so he also covers the corners. None of this changes what the brain observes or decides; it changes
how its decisions play out in 3D, and the panting feeds its hearing like any other noise.

Emergent strategies: on the 3 × 3 grid (v1–v3) Freddy learned to favor the middle of the map, at
most two moves from every room, so he heard more and reached any room quickly. On the two-wing map
with a single exit (v5) he learned to guard the only door to the exit instead (see the training
log), which led to the random exits. None of these were programmed.

### Things the player can do to the brain

- **Party horns** (from *Alien: Isolation*): thrown with G, they blow where they land, and the
  brain hears a loud noise in that room (`HuntState.HearNoiseAt`, same hearing rules as the
  player's noises). The network cannot tell it from the player, so it goes to investigate: the
  player can watch it take the bait in the AI inspector.
- **Doors** (from *Friday the 13th*): closing a door blocks the way and the view; Freddy's body has
  to break it down (three loud blows). The brain does not know about doors.
- **Crouching in the dark**: crouched with the flashlight off, Freddy's eyes only reach 5 m.

### Nights and the phone call

Night 1 opens with a phone call (as in the original games, voiced with a synthetic voice): while
it plays Freddy sleeps and the building is free to explore, and the voice explains the rules and
that Freddy is a trained neural network. Nights 2–5 use the same trained brain at a faster pace:
one decision every 3.5 s on night 1 down to 2.3 s on night 5, the body 7% faster per night, fewer
party horns.

### Making the ML visible

- **AI inspector (Tab):** a live map of what the brain knows (noise memory per room, where it last
  saw the player, rooms it has not checked), the **probability the network gives to each action**
  at the last decision, and its recent decisions.
- **AI vision in 3D (Tab):** Freddy's planned route drawn on the floor, his view cone, and a ripple
  in every room where his brain hears a noise.
- **End-of-night replay:** Freddy's route and the player's on the map, every decision of the
  network with the probability it gave to the chosen action, and how the catch happened.

The probabilities come from `PolicyProbe.cs`, which recomputes the policy's forward pass (two
dense layers with Swish, the output layer, action masking and softmax) from the weights exported by
`training/scripts/export_policy_weights.py`. It was checked against the `.onnx` model: its most
likely action equals the model's deterministic action in 13,060 of 13,060 states. The game still
acts with the `.onnx` model through ML-Agents; the probe is only for display.

## 9. "Learns where you hide": what it really means

ML-Agents trains **offline**. The exported model is frozen inside the game and **does not learn
during a match**. What it can learn during training is a general strategy against many survivors:
for example to go where it heard a noise and search the right spots, to check the rooms near fuses
and the exit late in the night, and to favor the locker when that is where survivors usually hide.

The week 4 experiment trains a second model against survivors that **always** choose the locker
(`Spot0Preference` = 1) and compares which spot each model searches first. This is reinforcement
learning adapting to a population of players, not online learning.

## 10. Baselines and evaluation

All three Freddys implement the same `IFreddyPolicy` interface:

| Policy | Behavior |
|---|---|
| `RandomFreddyPolicy` | Uniform random legal action each tick |
| `ScriptedFreddyPolicy` | Chases the last sighting, investigates the loudest noise and searches every spot there, otherwise patrols the room it has not visited for the longest time |
| `FreddyAgent` (trained) | PPO policy loaded from the `.onnx` model |

Evaluation plays a fixed number of nights for each policy against each preset and records the
catch rate (with a 95% Wilson confidence interval), escape rate, time-up rate, average night
length and searches per night.

## 11. Training setup (*initial*)

- Scene `HuntTraining` with 32 `HuntEnvironment` copies, no 3D and no UI.
- Each copy advances with a simulated tick instead of real time.
- Config: `training/config/freddy_ppo.yaml` (256×2 network, buffer 20,480; 3M steps for v1–v3,
  5M since v4); run IDs `freddy_v1`, `freddy_v2`, …

## 12. Limitations (for the report)

- The policy is frozen after training; there is no online learning during a match.
- The bot approximates a human player; a policy that beats the bot may feel different against
  people.
- The brain decides on a graph of rooms; positions inside a room are handled by the scripted body.
- A policy can exploit weaknesses of the training bot (freddy_v1) or of the map (freddy_v5, which
  camped the only door to the exit). High catch rates alone do not show good behavior: the network
  has to be probed and watched, and the bot, the map or the reward fixed when that happens.

## References

Huang, S., & Ontañón, S. (2022). A closer look at invalid action masking in policy gradient
algorithms. *The International FLAIRS Conference Proceedings, 35*.

Ng, A. Y., Harada, D., & Russell, S. (1999). Policy invariance under reward transformations:
Theory and application to reward shaping. *Proceedings of the Sixteenth International Conference
on Machine Learning*, 278–287.

Schulman, J., Wolski, F., Dhariwal, P., Radford, A., & Klimov, O. (2017). Proximal policy
optimization algorithms. *arXiv:1707.06347*.

Tobin, J., Fong, R., Ray, A., Schneider, J., Zaremba, W., & Abbeel, P. (2017). Domain
randomization for transferring deep neural networks from simulation to the real world. *2017
IEEE/RSJ International Conference on Intelligent Robots and Systems*, 23–30.
