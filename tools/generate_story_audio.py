"""Generate the voice clips of the story: the intro voicemail, what the clown says each night, and his taunts.

The lines are read from unity/Assets/Scripts/Game/StoryData.cs, so the subtitles and the audio
always match. The intro is a voicemail heard in the car (the answering machine, then the man from
the agency, through a telephone filter). Everything else is Pockets over the building's speakers.
To replace the synthetic voice with a real one, record over the same file names.

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
    """Returns the intro, the nights and the taunts of StoryData.cs."""
    intro = re.search(r"\bIntro\s*=\s*\{(.*?)\};", source, re.S).group(1)
    calls = re.search(r"Calls\s*=\s*\{(.*?)\n        \};", source, re.S).group(1)
    strings = lambda block: [s.replace('\\"', '"') for s in re.findall(r'"((?:[^"\\]|\\.)*)"', block)]
    nights = [strings(block) for block in re.findall(r"new\[\]\s*\{(.*?)\}", calls, re.S)]
    taunts = [(key, strings(block)) for key, block in re.findall(r'\("(\w+)",\s*new\[\]\s*\{(.*?)\}\)', source, re.S)]
    agency_line = int(re.search(r"AgencyVoiceLine\s*=\s*(\d+)", source).group(1))
    return strings(intro), nights, taunts, agency_line


class KokoroVoices:
    """Kokoro voices: the man from the agency, the answering machine and the clown."""

    MAN = "am_michael"
    MACHINE = "af_heart"
    CLOWN = "bm_fable"

    def __init__(self, models, espeak):
        from kokoro_onnx import EspeakConfig, Kokoro
        config = EspeakConfig(lib_path=str(Path(espeak) / "lib" / "libespeak-ng.dylib"),
                              data_path=str(Path(espeak) / "share" / "espeak-ng-data")) if espeak else None
        self.kokoro = Kokoro(str(Path(models) / "kokoro-v1.0.onnx"), str(Path(models) / "voices-v1.0.bin"), espeak_config=config)

    def line(self, text, machine=False, speed=1.0, voice=None):
        voice = voice or (self.MACHINE if machine else self.MAN)
        samples, rate = self.kokoro.create(text, voice=voice, speed=min(2.0, max(0.5, speed)), lang="en-gb" if voice.startswith("b") else "en-us")
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


def resample(audio, factor):
    """Plays the audio factor times faster (and that much higher), like a tape."""
    return np.interp(np.arange(0, len(audio), factor), np.arange(len(audio)), audio) if len(audio) else audio


def wobble(audio, rng, depth=0.012, rate=3.5):
    """Tape wow: the pitch wanders, never quite settling."""
    t = np.arange(len(audio)) / RATE
    drift = depth * RATE * (np.sin(2 * np.pi * rate * t + rng.uniform(0, 6)) / (2 * np.pi * rate)
                            + 0.5 * np.sin(2 * np.pi * rate * 2.7 * t + rng.uniform(0, 6)) / (2 * np.pi * rate * 2.7))
    return np.interp(np.arange(len(audio)) + drift, np.arange(len(audio)), audio)


def whisper(audio, rng):
    """Breath instead of voice: noise shaped by the speech's own loudness."""
    size = 256
    envelope = np.interp(np.arange(len(audio)), np.arange(0, len(audio), size),
                         [np.sqrt(np.mean(audio[i:i + size] ** 2)) for i in range(0, len(audio), size)])
    breath = band(rng.normal(0, 1, len(audio)), 1400, 7500)
    breath = breath / (np.max(np.abs(breath)) + 1e-6) * envelope * 5
    return band(audio, 900, 6000) * 0.35 + breath


PHRASE = re.compile(r"\([^)]*\)|[^.!?,;()]+[.!?,;]*")
LAUGH = re.compile(r"\b(hee|ha)(\s+(hee|ha))+\b", re.I)


def phrases(line):
    """The line cut into phrases, each marked: whisper, shout, laugh or plain."""
    parts = []
    for match in PHRASE.finditer(line):
        text = match.group(0).strip()
        if not text.strip(".!?,; "):
            continue
        if text.startswith("("):
            parts.append(("whisper", text.strip("()")))
        elif LAUGH.search(text) and len(LAUGH.sub("", text).strip(" .!?,;")) == 0:
            parts.append(("laugh", text))
        elif sum(c.isupper() for c in text) > 0.7 * max(1, sum(c.isalpha() for c in text)):
            parts.append(("shout", text))
        else:
            parts.append(("plain", text))
    return parts


def giggle(voices, text, rng):
    """His laugh: each syllable on its own, high and unsteady, rising and falling out of control."""
    pieces = []
    words = text.lower().replace("!", " ").replace(".", " ").split()
    for i, word in enumerate(words):
        sound = voices.line(word + "!", voice=KokoroVoices.CLOWN, speed=1.25)
        pitch = rng.uniform(1.35, 1.95) + 0.25 * np.sin(i * 1.3)
        sound = resample(sound, pitch)[:int(RATE * 0.2)]
        sound = sound * np.hanning(len(sound)) ** 0.35 * rng.uniform(0.7, 1.1)
        pieces.append(sound)
        pieces.append(np.zeros(int(RATE * rng.uniform(0.01, 0.07))))
    return wobble(np.concatenate(pieces), rng, depth=0.03, rate=7)


def mad_clown(voices, line, seed):
    """
    Pockets: never two phrases alike. He lurches between a low purr and a shrill sing-song, shouts
    and breaks, drops to a whisper at your ear, laughs where nothing is funny, and the tape under
    him wobbles. A ghost of his own voice mutters behind him.
    """
    rng = np.random.default_rng(seed)
    out = []
    moods = [0.82, 0.9, 1.04, 1.16, 1.3]
    mood = rng.integers(len(moods))
    for kind, text in phrases(line):
        if kind == "laugh":
            piece = giggle(voices, text, rng) * 0.95
        elif kind == "whisper":
            piece = whisper(wobble(resample(voices.line(text, voice=KokoroVoices.CLOWN, speed=0.82), 0.92), rng), rng) * 1.1
        elif kind == "shout":
            raw = voices.line(text.capitalize(), voice=KokoroVoices.CLOWN, speed=1.08)
            piece = resample(raw, rng.uniform(1.12, 1.28))
            piece = np.tanh(piece * 6) * 0.95
        else:
            mood = (mood + rng.integers(1, 4) * rng.choice([-1, 1])) % len(moods)
            raw = voices.line(text, voice=KokoroVoices.CLOWN, speed=rng.uniform(0.86, 1.2))
            piece = wobble(resample(raw, moods[mood] * rng.uniform(0.97, 1.03)), rng)
            piece = np.tanh(piece * 2.2)
        out.append(piece)
        out.append(np.zeros(int(RATE * rng.uniform(0.03, 0.2))))
    audio = np.concatenate(out)
    ghost = np.zeros(len(audio))
    delay = int(RATE * 0.13)
    low = resample(audio, 0.62)[: len(audio)]
    ghost[delay:delay + len(low)] = low[: max(0, len(audio) - delay)]
    return audio + ghost * 0.12


def speakers(audio):
    """Old public-address horns: a narrow band, saturation and the hall's echoes."""
    y = band(audio, 170, 5600)
    y = np.tanh(y * 1.6) / np.tanh(1.6)
    wet = np.zeros(len(y) + int(RATE * 1.2))
    wet[:len(y)] = y
    for delay, gain in [(0.09, 0.3), (0.17, 0.2), (0.31, 0.13), (0.52, 0.07), (0.83, 0.04)]:
        start = int(RATE * delay)
        wet[start:start + len(y)] += y * gain
    return wet + np.random.default_rng(2).normal(0, 0.003, len(wet))


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
    intro, nights, taunts, agency_line = string_arrays(story.read_text(encoding="utf-8"))
    for old in output.glob("*.wav"):
        old.unlink()
    if args.engine == "kokoro":
        voices = KokoroVoices(args.models, args.espeak)
        counter = [0]

        def clown(text):
            counter[0] += 1
            return speakers(mad_clown(voices, text, counter[0]))

        for i, line in enumerate(intro, 1):
            # Line 1 is the answering machine; the rest is the man from the agency, in a hurry.
            save(phone(voices.line(line, machine=i == 1, speed=0.95 if i == 1 else 1.05), soft=True), output / f"intro_{i:02d}.wav")
        for night, lines in enumerate(nights, 1):
            for i, line in enumerate(lines, 1):
                # Night 6: he does the voice of the agency, which was him all along.
                agency = night == 6 and i - 1 == agency_line
                audio = phone(voices.line(line, speed=1.05), soft=True) if agency else clown(line)
                save(audio, output / f"night{night}_{i:02d}.wav")
        for key, lines in taunts:
            for i, line in enumerate(lines, 1):
                save(clown(line), output / f"taunt_{key}_{i:02d}.wav")
    else:
        for i, line in enumerate(intro, 1):
            voice, rate = ("Fred", 165) if i == 1 else ("Daniel", 185)
            save(phone(synthesize(line, voice, rate)), output / f"intro_{i:02d}.wav")
        for night, lines in enumerate(nights, 1):
            for i, line in enumerate(lines, 1):
                save(speakers(synthesize(line, "Daniel", 175)), output / f"night{night}_{i:02d}.wav")
        for key, lines in taunts:
            for i, line in enumerate(lines, 1):
                save(speakers(synthesize(line, "Daniel", 175)), output / f"taunt_{key}_{i:02d}.wav")
    print(f"{len(intro)} intro lines, {sum(len(n) for n in nights)} night lines, {sum(len(t[1]) for t in taunts)} taunts in {output}")


if __name__ == "__main__":
    main()
