"""Copy the game's story to the website: the text and the voice clips.

Reads the string arrays of StoryData.cs (intro cards, intro, the calls, the notes, the night
changes) and writes web/ml-frontend/src/app/data/story.ts, then converts the voice clips
(Assets/Audio/Story/*.wav) to AAC in web/ml-frontend/public/audio/*.m4a with macOS afconvert.

Usage (from the repository root):
    python tools/export_story_web.py                      # reads unity/
    python tools/export_story_web.py --project ../ml-game
"""
import argparse
import json
import re
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
STORY_TS = ROOT / "web/ml-frontend/src/app/data/story.ts"
AUDIO_DIR = ROOT / "web/ml-frontend/public/audio"


def strings(block):
    return [s.encode().decode("unicode_escape").encode("latin-1").decode("utf-8")
            for s in re.findall(r'"((?:[^"\\]|\\.)*)"', block)]


def array(source, name):
    match = re.search(r"\b" + name + r"\s*=\s*\{(.*?)\n        \};", source, re.S)
    return match.group(1)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--project", default=str(ROOT / "unity"), help="the Unity project")
    args = parser.parse_args()
    project = Path(args.project)
    source = (project / "Assets/Scripts/Game/StoryData.cs").read_text(encoding="utf-8")

    calls = [strings(block) for block in re.findall(r"new\[\]\s*\{(.*?)\}", array(source, "Calls"), re.S)]
    sections = [
        ("INTRO_CARDS", "string[]", strings(array(source, "IntroCards"))),
        ("INTRO", "string[]", strings(array(source, "Intro"))),
        ("CALLS", "string[][]", calls),
        ("NOTES", "string[]", strings(array(source, "Notes"))),
        ("NIGHT_CHANGES", "string[]", strings(array(source, "NightChanges"))),
    ]
    out = ["// The story of the game, generated from unity/Assets/Scripts/Game/StoryData.cs.",
           "// Audio: public/audio/intro_NN.m4a and nightN_NN.m4a (the same voice clips as the game).", ""]
    for name, kind, value in sections:
        out.append(f"export const {name}: {kind} = " + json.dumps(value, indent=2, ensure_ascii=False) + ";")
        out.append("")
    STORY_TS.write_text("\n".join(out), encoding="utf-8")
    print(f"{STORY_TS.relative_to(ROOT)}: {len(calls)} calls, {sum(len(c) for c in calls)} call lines")

    if shutil.which("afconvert") is None:
        print("afconvert not found (macOS only): voice clips not converted")
        return
    AUDIO_DIR.mkdir(parents=True, exist_ok=True)
    for old in AUDIO_DIR.glob("*.m4a"):
        old.unlink()
    clips = sorted((project / "Assets/Audio/Story").glob("*.wav"))
    for clip in clips:
        target = AUDIO_DIR / (clip.stem + ".m4a")
        subprocess.run(["afconvert", "-f", "m4af", "-d", "aac", "-b", "64000", str(clip), str(target)], check=True)
    print(f"{AUDIO_DIR.relative_to(ROOT)}: {len(clips)} clips")


if __name__ == "__main__":
    main()
