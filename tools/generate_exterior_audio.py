"""Synthesize the sounds of the intro cinematic: rain, thunder, the car engine and the voicemail beep.

Everything is generated from noise and sine waves, so there is nothing to license.
Requirements: Python with numpy and soundfile.
Usage (from the repository root):
    python tools/generate_exterior_audio.py
"""
from pathlib import Path

import numpy as np
import soundfile as sf

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "unity" / "Assets" / "Audio"
RATE = 44100
rng = np.random.default_rng(1987)


def lowpass(signal, cutoff):
    spectrum = np.fft.rfft(signal)
    freqs = np.fft.rfftfreq(len(signal), 1 / RATE)
    return np.fft.irfft(spectrum / (1 + (freqs / cutoff) ** 4), len(signal))


def highpass(signal, cutoff):
    spectrum = np.fft.rfft(signal)
    freqs = np.fft.rfftfreq(len(signal), 1 / RATE)
    return np.fft.irfft(spectrum * (1 - 1 / (1 + (freqs / cutoff) ** 4)), len(signal))


def loopable(signal, fade_seconds):
    """Crossfades the end into the start so the clip loops without a click."""
    fade = int(RATE * fade_seconds)
    ramp = np.linspace(0, 1, fade)
    out = signal[fade:].copy()
    out[-fade:] = out[-fade:] * (1 - ramp) + signal[:fade] * ramp
    return out


def save(name, signal, peak=0.8):
    signal = signal / np.max(np.abs(signal)) * peak
    stereo = np.stack([signal, signal], axis=1) if signal.ndim == 1 else signal
    sf.write(OUTPUT / name, stereo, RATE, format="OGG", subtype="VORBIS")
    print("wrote", name)


def rain(seconds=10.0):
    n = int(RATE * seconds)
    hiss = highpass(lowpass(rng.normal(0, 1, n), 7000), 900)
    body = lowpass(rng.normal(0, 1, n), 600) * 0.5
    # Individual drops on the roof and the puddles.
    drops = np.zeros(n)
    for start in rng.integers(0, n - 2000, 2600):
        length = int(rng.integers(150, 900))
        drops[start:start + length] += rng.normal(0, 1, length) * np.exp(-np.linspace(0, 8, length)) * rng.uniform(0.2, 1)
    drops = highpass(drops, 1500)
    left = hiss + body + drops * 0.8
    right = np.roll(hiss, 5000) + body + np.roll(drops, 9000) * 0.8
    return np.stack([loopable(left, 1.0), loopable(right, 1.0)], axis=1)


def thunder(seconds=7.0):
    n = int(RATE * seconds)
    t = np.linspace(0, seconds, n)
    crack = highpass(rng.normal(0, 1, n), 1200) * np.exp(-t * 14) * 0.6
    rumble = lowpass(rng.normal(0, 1, n), 140) * (1 - np.exp(-t * 6)) * np.exp(-t * 0.55)
    rolls = 1 + 0.6 * np.sin(t * 2.1) * np.sin(t * 5.3)
    return crack + rumble * rolls * 4


def engine(seconds=4.0):
    n = int(RATE * seconds)
    t = np.arange(n) / RATE
    base = 38.0  # idle firing frequency
    wobble = 1 + 0.015 * np.sin(2 * np.pi * 0.5 * t)  # whole cycles in 4 s, so it loops
    phase = 2 * np.pi * base * np.cumsum(wobble) / RATE
    tone = sum(np.sin(phase * k) / k for k in range(1, 8))
    rough = lowpass(rng.normal(0, 1, n), 400) * 0.25
    return loopable(lowpass(tone + rough, 900), 0.5)


def beep():
    t = np.arange(int(RATE * 0.45)) / RATE
    tone = np.sin(2 * np.pi * 1000 * t) * np.minimum(1, np.minimum(t, 0.45 - t) * 80)
    return np.concatenate([np.zeros(int(RATE * 0.2)), tone, np.zeros(int(RATE * 0.3))])


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    save("rain-loop.ogg", rain(), 0.6)
    save("thunder.ogg", thunder(), 0.9)
    save("car-engine-loop.ogg", engine(), 0.7)
    save("voicemail-beep.ogg", beep(), 0.5)


if __name__ == "__main__":
    main()
