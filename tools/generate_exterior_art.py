"""Draw the neon signs of the circus front (seen in the intro cinematic) as PNGs.

Written as HTML with CSS glows and rendered with headless Google Chrome. The dark parts of each
image are the sign board; the bright parts become the emission (neon) in the game.

Usage (from the repository root):
    python tools/generate_exterior_art.py [unity project folder]
"""
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = Path(sys.argv[1] if len(sys.argv) > 1 else ROOT / "unity") / "Assets" / "Art" / "Exterior"
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"

MAIN_SIGN = """
<div class="board">
  <div class="script">Bellwether's</div>
  <div class="block">CIR<span class="dead">C</span>US</div>
</div>
<style>
  body { margin: 0; background: #07060a; }
  .board { width: 2048px; height: 640px; box-sizing: border-box; border: 10px solid #1d1a22;
           background: radial-gradient(ellipse at center, #120d14 0%, #07060a 75%);
           display: flex; flex-direction: column; align-items: center; justify-content: center; }
  .script { font-family: 'Brush Script MT', 'Snell Roundhand', cursive; font-size: 250px; line-height: 0.9;
            color: #ffd9c8; text-shadow: 0 0 6px #fff, 0 0 18px #ff3b2a, 0 0 42px #ff2a1a, 0 0 90px #c4100a; }
  .block { font-family: 'Futura', 'DIN Condensed', sans-serif; font-weight: 800; font-size: 170px; letter-spacing: 70px;
           margin-left: 70px; color: #fff3c4; text-shadow: 0 0 6px #fff, 0 0 18px #ffc22a, 0 0 46px #ffae00, 0 0 90px #b86a00; }
  .dead { color: #2b2418; text-shadow: none; }
</style>
"""

POLE_SIGN = """
<div class="board">
  <div class="name">BELLWETHER'S</div>
  <svg width="460" height="400" viewBox="0 0 460 400">
    <defs><filter id="glow" x="-50%" y="-50%" width="200%" height="200%">
      <feGaussianBlur stdDeviation="9" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
    </filter></defs>
    <g fill="none" stroke="#fff1d6" stroke-width="12" filter="url(#glow)"><ellipse cx="230" cy="240" rx="120" ry="130"/></g>
    <g fill="none" stroke="#3ca8ff" stroke-width="16" stroke-linecap="round" filter="url(#glow)">
      <path d="M 112 170 q -60 -10 -62 50 q -2 50 40 58"/><path d="M 348 170 q 60 -10 62 50 q 2 50 -40 58"/>
    </g>
    <g fill="none" stroke="#ff4a3c" stroke-width="12" filter="url(#glow)">
      <path d="M 170 118 L 230 8 L 290 118 Z"/><circle cx="230" cy="248" r="24"/><path d="M 140 285 Q 230 365 320 285" stroke-linecap="round"/>
    </g>
    <g fill="#fff1d6" filter="url(#glow)">
      <path d="M 180 180 l 16 32 l -16 32 l -16 -32 Z"/><path d="M 280 180 l 16 32 l -16 32 l -16 -32 Z"/>
    </g>
  </svg>
  <div class="line">WINTER CIRCUS</div>
  <div class="closed">CLOSED</div>
</div>
<style>
  body { margin: 0; background: #07060a; }
  .board { width: 1024px; height: 1024px; box-sizing: border-box; border: 12px solid #1d1a22; background: #0a080d;
           display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px; }
  .name { font-family: 'Futura', sans-serif; font-weight: 800; font-size: 96px; color: #ffe9b0;
          text-shadow: 0 0 8px #fff, 0 0 24px #ffb000, 0 0 60px #c87800; }
  .line { font-family: 'DIN Condensed', 'Futura', sans-serif; font-size: 96px; letter-spacing: 8px; color: #bfe8ff;
          text-shadow: 0 0 8px #fff, 0 0 24px #2aa8ff, 0 0 60px #0060c0; }
  .closed { font-family: 'Futura', sans-serif; font-weight: 800; font-size: 110px; letter-spacing: 20px; color: #ff6a5a;
            text-shadow: 0 0 8px #fff, 0 0 30px #ff2a1a, 0 0 70px #c4100a; }
</style>
"""


def render(name, body, width, height):
    with tempfile.TemporaryDirectory() as folder:
        page = Path(folder) / "sign.html"
        page.write_text(f"<!doctype html><html><head><meta charset='utf-8'></head><body>{body}</body></html>", encoding="utf-8")
        OUTPUT.mkdir(parents=True, exist_ok=True)
        subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", f"--window-size={width},{height}",
                        f"--screenshot={OUTPUT / name}", page.as_uri()], check=True, capture_output=True)
    print("wrote", name)


def main():
    render("sign-main.png", MAIN_SIGN, 2048, 640)
    render("sign-pole.png", POLE_SIGN, 1024, 1024)


if __name__ == "__main__":
    main()
