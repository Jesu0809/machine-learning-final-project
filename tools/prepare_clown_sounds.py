"""Cut the downloaded CC0 recordings into the clown's one-shot sounds for Unity.

Reads the files from a folder (see docs/credits.md for where each one comes from) and writes short,
mono, normalized clips into Assets/Audio/Clown/{Squeak,Pop,Shoes,Bells}, plus the music box.

Requirements: Python with numpy and soundfile.
Usage (from the repository root):
    python tools/prepare_clown_sounds.py ~/Downloads/nightshift-sounds --project ../ml-game
"""
import argparse
from pathlib import Path

import numpy as np
import soundfile as sf

RATE = 44100

# Folder: (start of the source's file name, [(from, to) in seconds; None for the whole recording])
CUTS = {
    "Squeak": [("468443", [None]),
               ("245083", [(0.46, 0.94), (1.8, 2.24), (3.48, 3.92), (5.66, 6.16), (7.92, 8.7)])],
    "Pop": [("527779", [(0.08, 2.3)]),
            ("63980", [(0.05, 1.3), (2.45, 3.75), (9.0, 10.2), (11.05, 12.35)])],
    "Shoes": [("88502", [(1.67, 1.9), (2.05, 2.26), (3.37, 3.6), (4.61, 4.9), (6.97, 7.24), (7.67, 7.9), (8.63, 8.84)])],
    "Bells": [("127553", [(0.85, 1.24), (1.2, 1.78), (1.74, 2.06), (3.25, 3.52)]),
              ("263886", [(0.87, 1.2), (1.2, 1.52), (1.5, 1.9), (4.78, 5.08)])],
}


def load(folder, prefix):
    path = next(folder.glob(prefix + "__*"))
    audio, rate = sf.read(path, always_2d=True)
    mono = audio.mean(axis=1)
    if rate != RATE:
        mono = np.interp(np.arange(0, len(mono), rate / RATE), np.arange(len(mono)), mono)
    return mono


def clip(audio, peak=0.8, fade=0.008):
    audio = audio - audio.mean()
    edge = min(int(RATE * fade), len(audio) // 3)
    envelope = np.ones(len(audio))
    envelope[:edge] = np.linspace(0, 1, edge)
    envelope[-edge * 3:] = np.linspace(1, 0, edge * 3)
    return audio * envelope / max(float(np.abs(audio).max()), 1e-6) * peak


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("source", type=Path, help="folder with the downloaded recordings")
    parser.add_argument("--project", default="../ml-game", help="Unity project folder")
    args = parser.parse_args()
    out = Path(args.project) / "Assets" / "Audio" / "Clown"
    for kind, sources in CUTS.items():
        (out / kind).mkdir(parents=True, exist_ok=True)
        index = 0
        for prefix, cuts in sources:
            audio = load(args.source, prefix)
            for cut in cuts:
                piece = audio if cut is None else audio[int(cut[0] * RATE):int(cut[1] * RATE)]
                sf.write(out / kind / f"{kind.lower()}_{index:02d}.wav", clip(piece), RATE, subtype="PCM_16")
                index += 1
        print(kind, index)
    box = clip(load(args.source, "274379"), 0.75, 0.05)
    sf.write(out / "music_box.ogg", box, RATE, format="OGG", subtype="VORBIS")
    print("music_box", round(len(box) / RATE, 1), "s")


if __name__ == "__main__":
    main()
