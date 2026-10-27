"""Generate the voice clips of the story (intro narration and the five phone calls).

The lines are read from unity/Assets/Scripts/Game/StoryData.cs, so the subtitles and the audio
always match. Voices come from the macOS speech synthesizer ("say") with an old telephone
filter: the intro is a voicemail heard in the car (the answering machine, then the same man who
makes the calls), the calls are his phone calls on each night.

Requirements: macOS (say, afconvert) and Python with numpy and soundfile.
Usage (from the repository root):
    python tools/generate_story_audio.py                    # writes into unity/
    python tools/generate_story_audio.py --project ../ml-game
"""
import argparse
import re
import subprocess
import tempfile
from pathlib import Path

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[1]
RATE = 22050


def string_arrays(source):
    """Returns {name: [[lines]]} for the string arrays of StoryData.cs (Intro, Calls)."""
    intro = re.search(r"\bIntro\s*=\s*\{(.*?)\};", source, re.S).group(1)
    calls = re.search(r"Calls\s*=\s*\{(.*?)\n        \};", source, re.S).group(1)
    strings = lambda block: [s.replace('\\"', '"') for s in re.findall(r'"((?:[^"\\]|\\.)*)"', block)]
    nights = [strings(block) for block in re.findall(r"new\[\]\s*\{(.*?)\}", calls, re.S)]
    return strings(intro), nights


def synthesize(text, voice, rate):
    with tempfile.TemporaryDirectory() as tmp:
        aiff = Path(tmp) / "line.aiff"
        wav = Path(tmp) / "line.wav"
        subprocess.run(["say", "-v", voice, "-r", str(rate), "-o", str(aiff), text], check=True)
        subprocess.run(["afconvert", "-f", "WAVE", "-d", f"LEI16@{RATE}", "-c", "1", str(aiff), str(wav)], check=True)
        audio, _ = sf.read(wav)
    return audio


def band(audio, low, high):
    spectrum = np.fft.rfft(audio)
    freqs = np.fft.rfftfreq(len(audio), 1 / RATE)
    return np.fft.irfft(spectrum * ((freqs > low) & (freqs < high)), len(audio))


def phone(audio):
    y = band(audio, 320, 3300)
    y = np.tanh(y * 2.2) / np.tanh(2.2)
    y += np.random.default_rng(1).normal(0, 0.006, len(y))
    return y


def save(audio, path):
    audio = audio / np.max(np.abs(audio)) * 0.85
    pad = np.zeros(int(RATE * 0.25))
    sf.write(path, np.concatenate([pad, audio, pad]), RATE, subtype="PCM_16")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--project", default=str(ROOT / "unity"), help="Unity project folder")
    args = parser.parse_args()
    story = Path(args.project) / "Assets" / "Scripts" / "Game" / "StoryData.cs"
    output = Path(args.project) / "Assets" / "Audio" / "Story"
    output.mkdir(parents=True, exist_ok=True)
    intro, nights = string_arrays(story.read_text(encoding="utf-8"))
    for i, line in enumerate(intro, 1):
        # Line 1 is the answering machine; the rest is the man from the calls, talking a bit faster.
        voice, rate = ("Fred", 165) if i == 1 else ("Daniel", 185)
        save(phone(synthesize(line, voice, rate)), output / f"intro_{i:02d}.wav")
    for night, lines in enumerate(nights, 1):
        for i, line in enumerate(lines, 1):
            save(phone(synthesize(line, "Daniel", 175)), output / f"night{night}_{i:02d}.wav")
    print(f"{len(intro)} intro lines, {sum(len(n) for n in nights)} call lines in {output}")


if __name__ == "__main__":
    main()
