# Night Shift

A first-person horror game in an abandoned pizzeria, in the style of *Alien: Isolation*. The player
collects three fuses, hides in lockers and escapes; **Freddy**, the NPC that hunts them, has a brain
trained with reinforcement learning using **Unity ML-Agents (PPO)**.

Final project for the Machine Learning course, Universidad de Cundinamarca (Chía).

## How it works

- **Freddy's brain (learned):** a PPO policy that decides where to go, when to stop and listen,
  which hiding spot to search and when to use his "sense" (it finds afraid players), from what
  Freddy heard and saw. It is trained on an abstract version
  of the pizzeria (a graph of 9 rooms) against a simulated survivor, and runs in the game as an
  `.onnx` model.
- **Freddy's body (scripted):** walks to the chosen room, sees with a view cone, chases the player in
  sight and plays the animations.
- **Game:** Unity 6 (C#). Five nights in a pizzeria built in code from the same room graph the brain
  was trained on. Ideas from *Alien: Isolation* (hiding, motion tracker, noisemakers), *Friday the
  13th* (stamina, fear, sense, doors) and the original games (a phone call every night that tells
  the story). A one-minute intro film (the guard drives to the pizzeria in the rain) opens the story.
  Keyboard and mouse or an Xbox controller, also in the browser.
- **The ML is visible:** an AI inspector (Tab) with the network's action probabilities, the route
  and view cone drawn in 3D, and an end-of-night replay of every decision.
- **Evaluation:** the trained brain against a random and a hand-written hunter, against three
  survivor skills, with confidence intervals.
- **Web:** Angular + Flask on Render: play the WebGL build, see which model drives Freddy, how it
  was trained, the metrics, the evidence, and try the trained brain live.

## Versions

Everyone on the team must use exactly these versions. Change them only together, and update this
table, `training/requirements.txt` and `web/requirements.txt` in the same commit.

| Tool | Version | Where it is defined |
|---|---|---|
| Unity Editor | **6000.0.84f1** (Unity 6.0 LTS) | `unity/ProjectSettings/ProjectVersion.txt` |
| Unity render pipeline | URP 17.0.4, Universal Renderer (3D) | `unity/Packages/manifest.json` |
| ML-Agents (Unity package) | **4.1.0** | `unity/Packages/manifest.json` |
| Input System | 1.20.0 | `unity/Packages/manifest.json` |
| Python for training | **3.10.12** (conda env `mlagents`) | `docs/setup.md` |
| ML-Agents (Python) | **mlagents 1.1.0** | `training/requirements.txt` |
| PyTorch | 2.2.2 | `training/requirements.txt` |
| grpcio | 1.48.2 | `training/requirements.txt` |
| TensorBoard | 2.20.0 | `training/requirements.txt` |
| Python for the website | 3.11 (Render: 3.11.9) | `web/render.yaml` |
| Flask / gunicorn | 3.1.3 / 26.2.0 | `web/requirements.txt` |

Unity packages are restored automatically when the project is opened. Python packages are
installed with `pip install -r requirements.txt` inside each folder.

## Repository layout

| Folder | Content |
|---|---|
| `unity/` | Unity project (scripts in `Assets/Scripts/`, Freddy in `Assets/Art/Freddy/`, trained models in `Assets/ML/Models/`) |
| `training/` | PPO config, Python requirements, TensorBoard export and evaluation scripts |
| `web/` | Flask API (`app.py`) + Angular app (`ml-frontend/`) deployed on Render; WebGL build in `web/game/` |
| `docs/` | ML design, setup steps and training log |

## Getting started

Follow [docs/setup.md](docs/setup.md). It covers macOS and Windows step by step:

1. Clone the repository.
2. Install Unity Hub and editor 6000.0.84f1, then open the `unity/` folder.
3. Create the `mlagents` conda environment and install `training/requirements.txt`.
4. Run the official 3DBall example once to confirm Unity and Python connect.
5. Play: **Night Shift → Create Game Scene**, open `Assets/Scenes/Game.unity` and press Play.

## Documentation

- [docs/ml-design.md](docs/ml-design.md): MDP, observations, actions, rules of a hunt, reward,
  survivor bot, baselines and evaluation
- [docs/training-log.md](docs/training-log.md): one entry per training run
- [docs/setup.md](docs/setup.md): installation, local website, WebGL build and deployment

## Credits

- This work is based on "[Freddy Fazbear](https://sketchfab.com/3d-models/freddy-fazbear-e8248c29265544e7b7e686b4c0c09f10)"
  by 2012carlitos17, licensed under CC-BY-4.0. Rigged and animated with Adobe Mixamo.
- The phone call voice was generated with the macOS speech synthesizer (voice "Daniel").
- Font: [VT323](https://fonts.google.com/specimen/VT323) by Peter Hull, SIL Open Font License
  (`unity/Assets/Art/Fonts/OFL.txt`).
- Sounds from Pixabay (Pixabay Content License): "Metal Door Slam" by SoundReality and
  "Jump-Scare_2" by freesound_community. The other sounds were synthesized for this project.

Fan work, non-commercial. Freddy Fazbear © Scott Cawthon. Not affiliated with the original games.

## Authors

Jesús Nuncira · Miguel Moreno
