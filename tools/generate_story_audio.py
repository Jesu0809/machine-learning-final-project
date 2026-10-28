"""Generate the voice clips of the story (intro narration and the five phone calls).

The lines are read from unity/Assets/Scripts/Game/StoryData.cs, so the subtitles and the audio
always match. The intro is a voicemail heard in the car (the answering machine, then the same man
who makes the calls), the calls are his phone calls on each night, all through a telephone filter.

Two voice engines:
  kokoro  Kokoro-82M, a neural text-to-speech model that runs locally (Apache 2.0): natural,
          human-sounding voices. Needs the kokoro-onnx package and its two model files
          (kokoro-v1.0.onnx, voices-v1.0.bin from the kokoro-onnx releases) in --models.
  say     the macOS speech synthesizer (robotic; no download).

Requirements: Python with numpy and soundfile (plus kokoro-onnx and espeak-ng for kokoro; macOS for say).
Usage (from the repository root):
    python tools/generate_story_audio.py --engine kokoro --models ~/kokoro --project ../ml-game
    python tools/generate_story_audio.py --engine say --project ../ml-game
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


class KokoroVoices:
    """Kokoro voices: the man on the phone (calm, a little nervous) and the answering machine."""

    MAN = "am_michael"
    MACHINE = "af_heart"

    def __init__(self, models, espeak):
        from kokoro_onnx import EspeakConfig, Kokoro
        config = EspeakConfig(lib_path=str(Path(espeak) / "lib" / "libespeak-ng.dylib"),
                              data_path=str(Path(espeak) / "share" / "espeak-ng-data")) if espeak else None
        self.kokoro = Kokoro(str(Path(models) / "kokoro-v1.0.onnx"), str(Path(models) / "voices-v1.0.bin"), espeak_config=config)

    def line(self, text, machine=False, speed=1.0):
        samples, rate = self.kokoro.create(text, voice=self.MACHINE if machine else self.MAN, speed=speed, lang="en-us")
        samples = np.asarray(samples, dtype=np.float64)
        # To the clips' rate (linear interpolation is fine for a telephone band).
        positions = np.arange(0, len(samples) * RATE / rate) * rate / RATE
        return np.interp(positions, np.arange(len(samples)), samples)


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


def phone(audio, soft=False):
    """A telephone line: the band of a handset, a little saturation, a faint hiss.
    The soft version keeps more of the voice (for the natural Kokoro voices)."""
    y = band(audio, 260 if soft else 320, 3600 if soft else 3300)
    drive = 1.4 if soft else 2.2
    y = np.tanh(y * drive) / np.tanh(drive)
    y += np.random.default_rng(1).normal(0, 0.004 if soft else 0.006, len(y))
    return y


def save(audio, path):
    audio = audio / np.max(np.abs(audio)) * 0.85
    pad = np.zeros(int(RATE * 0.25))
    sf.write(path, np.concatenate([pad, audio, pad]), RATE, subtype="PCM_16")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--project", default=str(ROOT / "unity"), help="Unity project folder")
    parser.add_argument("--engine", choices=["kokoro", "say"], default="kokoro", help="voice engine")
    parser.add_argument("--models", default=str(Path.home() / "kokoro"), help="folder with the Kokoro model files")
    parser.add_argument("--espeak", default="/opt/homebrew", help="espeak-ng install prefix (brew install espeak-ng); empty for the bundled one")
    args = parser.parse_args()
    story = Path(args.project) / "Assets" / "Scripts" / "Game" / "StoryData.cs"
    output = Path(args.project) / "Assets" / "Audio" / "Story"
    output.mkdir(parents=True, exist_ok=True)
    intro, nights = string_arrays(story.read_text(encoding="utf-8"))
    if args.engine == "kokoro":
        voices = KokoroVoices(args.models, args.espeak)
        for i, line in enumerate(intro, 1):
            # Line 1 is the answering machine; the rest is the man from the calls, in a hurry.
            save(phone(voices.line(line, machine=i == 1, speed=0.95 if i == 1 else 1.05), soft=True), output / f"intro_{i:02d}.wav")
        for night, lines in enumerate(nights, 1):
            for i, line in enumerate(lines, 1):
                save(phone(voices.line(line, speed=1.0), soft=True), output / f"night{night}_{i:02d}.wav")
    else:
        for i, line in enumerate(intro, 1):
            voice, rate = ("Fred", 165) if i == 1 else ("Daniel", 185)
            save(phone(synthesize(line, voice, rate)), output / f"intro_{i:02d}.wav")
        for night, lines in enumerate(nights, 1):
            for i, line in enumerate(lines, 1):
                save(phone(synthesize(line, "Daniel", 175)), output / f"night{night}_{i:02d}.wav")
    print(f"{len(intro)} intro lines, {sum(len(n) for n in nights)} call lines in {output}")


if __name__ == "__main__":
    main()
