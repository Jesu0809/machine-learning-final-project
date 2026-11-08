"""Join what the game's film mode recorded (FilmMode.cs) into one video.

Requirements: imageio-ffmpeg and numpy.
Usage: python tools/make_film.py recording_folder out.mp4
"""
import subprocess
import sys
from pathlib import Path

import imageio_ffmpeg
import numpy as np

FRAMES_PER_SECOND = 30


def main():
    folder = Path(sys.argv[1])
    rate, channels, rendered = (folder / "format.txt").read_text().split()
    command = [imageio_ffmpeg.get_ffmpeg_exe(), "-y", "-loglevel", "error", "-framerate", str(FRAMES_PER_SECOND),
               "-i", str(folder / "frames" / "%06d.jpg")]
    sound = np.fromfile(folder / "sound.raw", dtype=np.float32)
    if rendered == "True" and len(sound) > 0:
        sound = np.tanh(sound)   # the mix can pass 1 when everything sounds at once
        wave = folder / "sound.f32"
        sound.tofile(wave)
        command += ["-f", "f32le", "-ar", rate, "-ac", channels, "-i", str(wave), "-c:a", "aac", "-b:a", "160k", "-shortest"]
    command += ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "24", "-preset", "veryfast", "-movflags", "+faststart", sys.argv[2]]
    subprocess.run(command, check=True)
    print(sys.argv[2], f"{len(list((folder / 'frames').glob('*.jpg'))) / FRAMES_PER_SECOND:.0f} s, sound: {rendered}")


if __name__ == "__main__":
    main()
