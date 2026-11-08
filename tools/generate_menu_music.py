"""Make the home screen's music from a real old recording: a circus march slowed into a nightmare.

The source is Julius Fučík's "Entrance of the Gladiators" (1897) played by the U.S. Marine Band, in
the public domain, from Wikimedia Commons. It is the march everybody knows from the circus. Here it
plays at about 60 % speed on a worn-out gramophone: pitch sagging, narrow and crackling, far away in
an empty hall, with the volume dropping out now and then.

Requirements: Python with numpy and soundfile.
Usage:
    python tools/generate_menu_music.py source.oga ../ml-game/Assets/Audio/menu-music-loop.ogg
"""
import sys

import numpy as np
import soundfile as sf

RATE = 44100
SPEED = 0.6
LOOP_SECONDS = 150
FADE_SECONDS = 6


def band(audio, low, high):
    spectrum = np.fft.rfft(audio)
    freqs = np.fft.rfftfreq(len(audio), 1 / RATE)
    gain = np.clip((freqs - low * 0.7) / (low * 0.3), 0, 1) * np.clip((high * 1.3 - freqs) / (high * 0.3), 0, 1)
    return np.fft.irfft(spectrum * gain, len(audio))


def smooth_noise(rng, length, knots):
    return np.interp(np.arange(length), np.linspace(0, length, knots), rng.normal(0, 1, knots))


def main():
    source, rate = sf.read(sys.argv[1], always_2d=True)
    mono = source.mean(axis=1)
    mono = np.interp(np.arange(0, len(mono), rate / RATE), np.arange(len(mono)), mono)
    rng = np.random.default_rng(1897)
    # The motor is dying: the speed sags and recovers, slowly and unevenly.
    count = int(min(len(mono) / SPEED, RATE * (LOOP_SECONDS + FADE_SECONDS)))
    speed = SPEED * (1 + 0.035 * smooth_noise(rng, count, count // (RATE * 3))
                     + 0.012 * np.sin(2 * np.pi * 0.72 * np.arange(count) / RATE))
    position = np.minimum(np.cumsum(speed), len(mono) - 1)
    audio = np.interp(position, np.arange(len(mono)), mono)
    # A gramophone horn: no bass, no air, a hard edge.
    audio = band(audio, 240, 3400)
    audio = np.tanh(audio / np.max(np.abs(audio)) * 2.2)
    # Now and then it almost stops being there.
    audio *= np.clip(0.8 + 0.35 * smooth_noise(rng, len(audio), len(audio) // (RATE * 5)), 0.15, 1)
    # An empty hall: long, dark echoes.
    wet = np.zeros(len(audio))
    for delay, gain in [(0.11, 0.3), (0.23, 0.24), (0.41, 0.2), (0.67, 0.15), (1.03, 0.11), (1.6, 0.08), (2.3, 0.05)]:
        start = int(RATE * delay)
        wet[start:] += audio[:-start] * gain
    audio = audio * 0.75 + band(wet, 200, 2200) * 0.8
    # The needle: hiss and crackle.
    audio += band(rng.normal(0, 0.012, len(audio)), 500, 6000)
    for at in rng.integers(0, len(audio) - 200, len(audio) // (RATE // 7)):
        audio[at:at + 40] += rng.uniform(-0.25, 0.25) * np.exp(-np.arange(40) / 8)
    # Join the end to the start, so the loop has no seam.
    size = RATE * LOOP_SECONDS
    fade = RATE * FADE_SECONDS
    if len(audio) >= size + fade:
        ramp = np.linspace(0, 1, fade)
        audio[:fade] = audio[:fade] * ramp + audio[size:size + fade] * (1 - ramp)
        audio = audio[:size]
    audio = audio / np.max(np.abs(audio)) * 0.8
    with sf.SoundFile(sys.argv[2], "w", RATE, 1, format="OGG", subtype="VORBIS") as out:
        # In small blocks: the Vorbis encoder fails on one huge write.
        for start in range(0, len(audio), RATE):
            out.write(audio[start:start + RATE])
    print(f"{len(audio) / RATE:.0f} s in {sys.argv[2]}")


if __name__ == "__main__":
    main()
