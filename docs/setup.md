# Setup

How to get the project running on **macOS (Apple Silicon)** or **Windows**. Every step says what
changes between the two. The exact versions are listed in the README ("Versions") and must not be
changed by one person alone: the Unity package and the Python package belong to the same
ML-Agents release.

| Component | Version |
|---|---|
| Unity Editor | **6000.0.84f1** (Unity 6.0 LTS) |
| `com.unity.ml-agents` | 4.1.0 (installed automatically from `unity/Packages/manifest.json`) |
| Python for training | **3.10.12**, conda environment `mlagents` |
| `mlagents` (Python) | 1.1.0, plus the pins in `training/requirements.txt` |
| Python for the website | 3.11.x (Render uses 3.11.9) |

---

## 1. Get the repository

```bash
git clone https://github.com/Jesu0809/machine-learning-final-project.git
```

Windows: clone into a short path that is **not** inside OneDrive (for example
`C:\dev\machine-learning-final-project`). Unity creates deep folders and OneDrive sync locks files
while Unity is writing them.

The repository has a `.gitattributes` file that fixes line endings, so files look the same on both
systems. No Git configuration is needed.

## 2. Install Unity Hub and the exact editor

1. Download Unity Hub from https://unity.com/download and install it.
2. Open Unity Hub, sign in, and activate a free **Personal** license
   (Settings → Licenses → Add license).
3. Install editor **6000.0.84f1**. It must be this exact version; another version would modify the
   project files for everyone.
   - **Installs → Install Editor → Official releases**. If 6000.0.84f1 is not in the list, open
     https://unity.com/releases/editor/archive, find **6000.0.84**, and click **Install** (it
     opens Unity Hub with that version).
4. Modules to select:

   | Module | macOS | Windows |
   |---|---|---|
   | Web Build Support | ✔ | ✔ |
   | Mac Build Support (IL2CPP) | ✔ (optional) | — |
   | Windows Build Support (Mono) | — | already included |
   | Visual Studio Code / Visual Studio | optional | optional |

   Do not select Android, iOS, Linux or Dedicated Server.

## 3. Open the Unity project

1. In Unity Hub: **Projects → Add → Add project from disk** and select the `unity` folder of the
   repository (the folder itself, not a file inside it).
2. Open it. The first time takes several minutes: Unity builds the `Library` folder and downloads
   the packages listed in `Packages/manifest.json`, ML-Agents 4.1.0 included.
3. Check that the **Console** shows no red errors. Yellow warnings about unused fields are expected
   while some scripts are still stubs.
4. In **Window → Package Manager → In Project** confirm **ML Agents 4.1.0**.
5. Code editor: **Unity → Settings → External Tools** (macOS) or
   **Edit → Preferences → External Tools** (Windows) → External Script Editor → your editor.
   VS Code needs the **Unity** extension by Microsoft.

Do **not** connect the project to Unity Cloud or Unity Version Control, and do not enable
**AI Assistant**. The project is versioned only with Git.

### Project settings already configured (do not change)

- Player → Run In Background: **ON** (training pauses without it).
- Editor → Version Control: **Visible Meta Files**; Asset Serialization: **Force Text**.
- Player → Active Input Handling: **Both** (the keyboard control of the monster works with either).
- Always commit the `.meta` file that Unity creates next to every asset and folder.

## 4. Python training environment

`mlagents==1.1.0` has narrow requirements, which is why everything is pinned:

| Requirement | Why |
|---|---|
| Python 3.10.1 – 3.10.12 | Newer 3.10 patches are rejected by pip; conda provides 3.10.12 |
| `grpcio` exactly 1.48.2 | mlagents needs ≤ 1.48.2 and tensorboard needs ≥ 1.48.2 |
| `torch` 2.2.2 | Newer torch exports `.onnx` with an exporter mlagents 1.1.0 cannot use, so the trained model would fail to save |
| `setuptools` 69.5.1 | mlagents 1.1.0 imports `pkg_resources`, removed in setuptools 70 |

The environment lives inside Miniforge, outside the repository, so nothing of it is committed.

### Windows

1. Download and install **Miniforge3-Windows-x86_64.exe** from
   https://github.com/conda-forge/miniforge/releases/latest (default options, "Just Me").
2. Open **Miniforge Prompt** from the Start menu and run:

   ```bat
   conda create -n mlagents python=3.10.12 -y
   conda activate mlagents
   cd C:\dev\machine-learning-final-project\training
   pip install -r requirements.txt
   pip check
   mlagents-learn --help
   ```

`grpcio` 1.48.2 has a prebuilt Windows package, so nothing is compiled. Training runs on the CPU,
which is the right choice for a network this small.

### macOS (Apple Silicon)

There is no prebuilt Apple Silicon package of `grpcio` 1.48.2, so it is compiled once (about
5 minutes).

```bash
brew install --cask miniforge
conda init zsh
```

**Close the terminal and open a new one** (otherwise `conda activate` fails), then:

```bash
conda create -n mlagents python=3.10.12 -y
conda activate mlagents
pip install "setuptools==69.5.1" wheel

GRPC_PYTHON_BUILD_SYSTEM_OPENSSL=1 GRPC_PYTHON_BUILD_SYSTEM_ZLIB=1 \
CFLAGS="-I$CONDA_PREFIX/include -Wno-error" \
CXXFLAGS="-I$CONDA_PREFIX/include -Wno-error -std=c++14" \
LDFLAGS="-L$CONDA_PREFIX/lib -Wl,-rpath,$CONDA_PREFIX/lib -framework CoreFoundation -framework Security" \
pip install --no-build-isolation --no-cache-dir "grpcio==1.48.2"

cd path/to/machine-learning-final-project/training
pip install -r requirements.txt
pip check
mlagents-learn --help
```

### Expected result (both systems)

- `pip check` prints `No broken requirements found.`
- `mlagents-learn --help` prints the list of options.

From now on, run `conda activate mlagents` in every new terminal before training.

## 5. Verify with an official ML-Agents example

This proves that Unity and Python can talk to each other, so later problems are ours and not the
installation. Do it once per computer.

1. Download the official repository **outside** our project:

   ```bash
   git clone --branch release_23_tag --depth 1 https://github.com/Unity-Technologies/ml-agents.git
   ```

2. In Unity Hub: **Add → Add project from disk → `ml-agents/Project`**, open it with 6000.0.84f1.
3. Open the scene `Assets/ML-Agents/Examples/3DBall/Scenes/3DBall`.
4. In a terminal (Miniforge Prompt on Windows):

   ```bash
   conda activate mlagents
   cd ml-agents
   mlagents-learn config/ppo/3DBall.yaml --run-id=3dball_test
   ```

5. When the terminal says *"Listening on port 5004. Start training by pressing the Play button in
   the Unity Editor"*, press **Play** in Unity.
6. After a few minutes the *Mean Reward* climbs toward 100. Stop with `Ctrl+C`; the model is saved
   to `results/3dball_test/3DBall.onnx`.

Windows: the first time, Windows Firewall may ask about Python; allow it on private networks
(Unity and Python talk through port 5004 on your own computer).

Afterwards the `ml-agents` folder can be deleted; nothing from it goes into our repository.

## 6. Run the website locally

macOS:

```bash
cd web
python3 -m venv .venv
source .venv/bin/activate
pip install -r requirements.txt
flask --app app run --debug
```

Windows (PowerShell or Command Prompt):

```bat
cd web
py -3.11 -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
flask --app app run --debug
```

Open http://127.0.0.1:5000. Until there is a WebGL build in `web/static/game/Build/`, the home
page shows a placeholder instead of the game. `gunicorn` is only used on Render; it is installed
on Windows but not used there.

## 7. WebGL build (week 5)

1. **File → Build Profiles → Web → Switch Platform.**
2. **Player Settings → Publishing Settings:**
   - Compression Format: **Gzip**
   - Decompression Fallback: **ON** (the game then loads on any server, including Render,
     without special headers)
3. Build into a temporary folder, then copy the `Build/` folder into `web/static/game/`.
4. The build name must match `GAME_BUILD_NAME` in `web/app.py` (default `game`), so the files
   are `web/static/game/Build/game.loader.js`, `game.data.unityweb`, and so on.
5. Keep the build small: GitHub rejects files larger than 100 MB. If a file gets close, use
   Git LFS.

## 8. Deploy on Render

Render cannot run Unity, so the WebGL build is committed and Render only serves files.

1. Push the repository to GitHub (`main` branch).
2. On https://dashboard.render.com: **New → Web Service** and connect the repository.
3. Settings:
   - Root Directory: `web`
   - Runtime: Python 3
   - Build Command: `pip install -r requirements.txt`
   - Start Command: `gunicorn app:app`
   - Instance type: Free
   - Environment variable: `PYTHON_VERSION` = `3.11.9`
4. Alternatively use **New → Blueprint** and set the Blueprint path to `web/render.yaml`.
5. The free tier sleeps after inactivity, and the first request can take about a minute. Open the
   site a few minutes before any demo.
