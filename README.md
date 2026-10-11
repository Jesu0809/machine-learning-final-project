# Night Shift ML

A FNAF-style horror game where the monster is a Reinforcement Learning agent trained with
Unity ML-Agents (PPO). The player sits in a security office, watches the cameras and closes the
doors; the monster decides where to move based on what the player is (and is not) looking at.

Final project for the Machine Learning course, Universidad de Cundinamarca (Chía).

## How it works

- **Game:** Unity 6 (C#). The game logic is a pure C# simulation, separated from the UI, so the
  same code runs in the playable scene and in the training arena.
- **Training:** `mlagents-learn` (PPO) against a stochastic `PlayerBot`, many simulations in
  parallel. The trained policy is exported as `MonsterAgent.onnx`.
- **Inference:** inside Unity (CPU), also in the WebGL build. The web server never runs the model.
- **Web:** Flask + Jinja + Bootstrap on Render. It hosts the WebGL build and explains the MDP,
  the training curves and the comparison against two baselines (random and scripted monsters).

## Versions

Everyone on the team must use exactly these versions. Change them only together, and update this
table, `training/requirements.txt` and `web/requirements.txt` in the same commit.

| Tool | Version | Where it is defined |
|---|---|---|
| Unity Editor | **6000.0.84f1** (Unity 6.0 LTS) | `unity/ProjectSettings/ProjectVersion.txt` |
| Unity render pipeline | URP 17.0.4, 2D Renderer | `unity/Packages/manifest.json` |
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
| `unity/` | Unity project (scripts in `Assets/Scripts/`, trained models in `Assets/ML/Models/`) |
| `training/` | PPO config, Python requirements, TensorBoard export and evaluation scripts |
| `web/` | Flask site deployed on Render; the WebGL build goes in `web/static/game/` |
| `docs/` | ML design, setup steps and training log |

## Getting started

Follow [docs/setup.md](docs/setup.md). It covers macOS and Windows step by step:

1. Clone the repository.
2. Install Unity Hub and editor 6000.0.84f1, then open the `unity/` folder.
3. Create the `mlagents` conda environment and install `training/requirements.txt`.
4. Run the official 3DBall example once to confirm Unity and Python connect.

## Documentation

- [docs/ml-design.md](docs/ml-design.md): MDP, observations, actions, rules of a night, reward,
  baselines and evaluation
- [docs/training-log.md](docs/training-log.md): one entry per training run
- [docs/setup.md](docs/setup.md): installation, local website, WebGL build and deployment

## Credits

- Font: [VT323](https://fonts.google.com/specimen/VT323) by Peter Hull, SIL Open Font License
  (`unity/Assets/Art/Fonts/OFL.txt`).
- Sounds from Pixabay (Pixabay Content License): "Metal Door Slam" by SoundReality and
  "Jump-Scare_2" by freesound_community. The other sounds were synthesized for this project.

## Authors

Jesus Nuncira · Miguel Moreno
