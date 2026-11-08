"""Voice Pockets: a sweet, too-high voice that keeps cracking into rage and laughter.

Regenerates only what the clown says (the nights and his taunts in StoryData.cs). The voicemail of
the intro and the man from the agency stay as generate_story_audio.py made them.

Plain speech and whispers come from Chatterbox (Resemble AI, MIT) with its emotion turned far up,
then go through the "child" chain. Phrases in CAPS, his rages, use a second voice through the
"broken" chain. The laughs are a bank of recordings generated with Dia (Nari Labs, Apache 2.0, see
--laughs); they replace every "hee hee" and "ha ha" of the script and now and then interrupt him.

Requirements: Python 3.11 with chatterbox-tts, librosa and soundfile (setuptools<81 for its watermark).
Usage (from the repository root):
    python tools/generate_clown_voice.py --project ../ml-game --anger-voice anger.wav --laughs laughs/
Only some clips: --only taunt_seen night1_01   Listen first: --demo out_folder
"""
import argparse
import re
import sys
from pathlib import Path

import librosa
import numpy as np
import soundfile as sf
import torch

sys.path.insert(0, str(Path(__file__).resolve().parent))
from generate_story_audio import phrases, string_arrays  # noqa: E402

RATE = 24000
DEMO = ["Hello. Hello hello hello. HELLO! Come closer. Closer. Hee hee hee! (I do not bite. Much.)",
        "Tonight I start with the wardrobes. Just so you know! Ha ha! Oh, you are shaking. I can hear your teeth. (I like your teeth.)",
        "Where did you go? Where did you go, little night watchman? COME OUT AND PLAY WITH ME! Ha ha ha ha! (Shhh. Quiet now.)"]


def norm(audio, peak=0.85):
    return audio / max(float(np.abs(audio).max()), 1e-6) * peak


def band(audio, low, high):
    spectrum = np.fft.rfft(audio)
    freqs = np.fft.rfftfreq(len(audio), 1 / RATE)
    gain = np.clip((freqs - low * 0.7) / (low * 0.3), 0, 1) * np.clip((high * 1.3 - freqs) / (high * 0.3), 0, 1)
    return np.fft.irfft(spectrum * gain, len(audio))


def rasp(audio, rng):
    """Breath and gravel that follow the voice: what a torn throat adds to it."""
    envelope = np.convolve(np.abs(audio), np.ones(480) / 480, "same")
    envelope = envelope / max(float(envelope.max()), 1e-6)
    noise = band(rng.normal(0, 1, len(audio)), 900, 4200)
    flutter = 1 + 0.55 * np.sin(2 * np.pi * 38 * np.arange(len(audio)) / RATE + rng.uniform(0, 6))
    return noise * envelope ** 1.3 * flutter


def shift(audio, steps):
    return librosa.effects.pitch_shift(audio, sr=RATE, n_steps=steps)


def child(audio, rng):
    """Too sweet and too high, with something old and wet underneath: the voice that lures."""
    voice = shift(audio, 2.5)
    under = np.tanh(shift(audio, -10) * 3.5)
    t = np.arange(len(audio)) / RATE
    tremble = 1 + 0.09 * np.sin(2 * np.pi * 5.3 * t) + 0.05 * np.sin(2 * np.pi * 1.7 * t)
    return norm(norm(voice) * tremble + norm(under) * 0.2 + norm(rasp(voice, rng)) * 0.13)


def broken(audio, rng):
    """Low and torn, spoken from the chest, with a growl an octave under it: the rage."""
    voice = shift(audio, -3.5)
    growl = np.tanh(shift(audio, -12) * 5) * (0.5 + 0.5 * np.sin(2 * np.pi * 27 * np.arange(len(audio)) / RATE))
    return norm(norm(voice) + norm(growl) * 0.28 + norm(rasp(voice, rng)) * 0.16)


def whisper(audio, rng):
    """Right at your ear: mostly breath, shaped by the words."""
    voice = shift(audio, 1.0)
    envelope = np.convolve(np.abs(voice), np.ones(240) / 240, "same")
    breath = band(rng.normal(0, 1, len(voice)), 1200, 7000) * envelope
    return norm(norm(voice) * 0.45 + norm(breath) * 0.8)


def speakers(audio):
    """Old public-address horns: a narrow band, saturation and the echoes of an empty hall."""
    narrow = band(audio, 190, 5200)
    narrow = np.tanh(narrow * 2.2) / np.tanh(2.2)
    wet = np.zeros(len(narrow) + int(RATE * 1.3))
    wet[:len(narrow)] = narrow
    for delay, gain in [(0.07, 0.32), (0.13, 0.24), (0.23, 0.17), (0.37, 0.11), (0.59, 0.07), (0.87, 0.04)]:
        start = int(RATE * delay)
        wet[start:start + len(narrow)] += narrow * gain
    return wet


def trim(audio, threshold=0.02):
    loud = np.nonzero(np.abs(audio) > threshold * max(float(np.abs(audio).max()), 1e-6))[0]
    if len(loud) == 0:
        return audio
    return audio[max(0, loud[0] - 240):min(len(audio), loud[-1] + 1200)]


class Laughs:
    """Dia's laughs: sweet ones high up for the child voice, the mad ones low for the rage."""

    def __init__(self, folder):
        self.sweet, self.mad = [], []
        for path in sorted(Path(folder).glob("*.wav")):
            audio, rate = sf.read(path, always_2d=True)
            mono = librosa.resample(audio.mean(axis=1), orig_sr=rate, target_sr=RATE)
            mono = trim(mono)
            if 0.5 < len(mono) / RATE < 7:
                (self.mad if path.stem.startswith("mad") else self.sweet).append(mono)
        if not self.sweet or not self.mad:
            raise SystemExit(f"no usable laughs in {folder}")

    def laugh(self, kind, rng):
        bank = self.mad if kind == "mad" else self.sweet
        audio = bank[rng.integers(len(bank))]
        return broken(audio, rng) if kind == "mad" else child(shift(audio, float(rng.uniform(-1.5, 1))), rng)


class Voice:
    def __init__(self, anger_voice):
        from chatterbox.tts import ChatterboxTTS
        self.model = ChatterboxTTS.from_pretrained(device="mps")
        self.anger = anger_voice

    def say(self, text, emotion, weight, prompt=None, seed=7):
        """Synthesized speech; a take that is far too long or short for its text is tried again."""
        for attempt in range(3):
            torch.manual_seed(seed + attempt)
            wave = self.model.generate(text, audio_prompt_path=prompt, exaggeration=emotion, cfg_weight=weight, temperature=1.0)
            audio = wave.squeeze().cpu().numpy()
            seconds = len(audio) / self.model.sr
            if 0.03 * len(text) < seconds < 0.2 * len(text) + 1.5:
                break
        audio = librosa.resample(audio, orig_sr=self.model.sr, target_sr=RATE)
        return trim(audio)


def merged(line):
    """The phrases of a line with the plain ones joined, so he speaks in whole sentences."""
    out = []
    for kind, text in phrases(line):
        if out and out[-1][0] == kind and kind in ("plain", "whisper", "shout") and len(out[-1][1]) + len(text) < 190:
            out[-1] = (kind, out[-1][1] + " " + text)
        else:
            out.append((kind, text))
    return out


def clown(voice, laughs, line, seed):
    rng = np.random.default_rng(seed)
    pieces = []
    mood = "sweet"

    def pause(low, high):
        pieces.append(np.zeros(int(RATE * rng.uniform(low, high))))

    for kind, text in merged(line):
        if kind == "laugh":
            pieces.append(laughs.laugh("mad" if mood == "rage" or text.isupper() else "sweet", rng))
        elif kind == "whisper":
            mood = "sweet"
            pieces.append(whisper(voice.say(text, 0.7, 0.45, seed=seed), rng) * 0.8)
        elif kind == "shout":
            mood = "rage"
            spoken = re.sub(r"[A-Z]{2,}", lambda m: m.group(0).capitalize(), text)
            pieces.append(broken(voice.say(spoken, 1.8, 0.2, voice.anger, seed), rng))
        else:
            mood = "sweet"
            pieces.append(child(voice.say(text, 1.8, 0.2, seed=seed), rng))
            # Now and then he cannot hold it in, even where the script does not say so.
            if rng.random() < 0.22:
                pause(0.04, 0.16)
                pieces.append(laughs.laugh("sweet", rng) * 0.75)
        pause(0.08, 0.3)
    return speakers(np.concatenate(pieces))


def save(audio, path):
    audio = norm(audio)
    pad = np.zeros(int(RATE * 0.2))
    sf.write(path, np.concatenate([pad, audio, pad]), RATE, subtype="PCM_16")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--project", default="../ml-game", help="Unity project folder")
    parser.add_argument("--anger-voice", required=True, help="a recording whose voice the rages take")
    parser.add_argument("--laughs", required=True, help="folder with Dia's laughs (mad_*.wav for the rage)")
    parser.add_argument("--demo", help="only write three test lines into this folder")
    parser.add_argument("--only", nargs="*", help="clip names to generate")
    args = parser.parse_args()
    laughs = Laughs(args.laughs)
    voice = Voice(args.anger_voice)
    if args.demo:
        Path(args.demo).mkdir(parents=True, exist_ok=True)
        for i, line in enumerate(DEMO, 1):
            save(clown(voice, laughs, line, i), Path(args.demo) / f"prueba_{i}.wav")
            print("demo", i, flush=True)
        return
    story = Path(args.project) / "Assets" / "Scripts" / "Game" / "StoryData.cs"
    output = Path(args.project) / "Assets" / "Audio" / "Story"
    _, nights, taunts, agency_line = string_arrays(story.read_text(encoding="utf-8"))
    clips = []
    for night, lines in enumerate(nights, 1):
        for i, line in enumerate(lines, 1):
            # Night 6: he does the voice of the agency, which keeps its clip.
            if not (night == 6 and i - 1 == agency_line):
                clips.append((f"night{night}_{i:02d}", line))
    for key, lines in taunts:
        for i, line in enumerate(lines, 1):
            clips.append((f"taunt_{key}_{i:02d}", line))
    for index, (name, line) in enumerate(clips):
        if args.only and not any(name.startswith(wanted) for wanted in args.only):
            continue
        save(clown(voice, laughs, line, 100 + index), output / f"{name}.wav")
        print(f"{index + 1}/{len(clips)} {name}", flush=True)


if __name__ == "__main__":
    main()
