"""Draw the controls diagrams (keyboard and mouse, Xbox gamepad) as transparent PNGs.

The diagrams are written as SVG and rendered with headless Google Chrome, so the only
requirement is Chrome. The same images are used by the game (Assets/Art/UI) and the website
(web/ml-frontend/public/img).

Usage (from the repository root):
    python tools/generate_controls_diagram.py
"""
import shutil
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUTPUTS = [ROOT / "unity" / "Assets" / "Art" / "UI", ROOT / "web" / "ml-frontend" / "public" / "img"]
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
WIDTH, HEIGHT = 2000, 1000

FONT = "'Avenir Next Condensed', 'Arial Narrow', sans-serif"
LINE = "#e8e8e8"
DIM = "rgba(232,232,232,0.28)"
HOT = "#ff4a3d"

STYLE = f"""
<style>
  text {{ font-family: {FONT}; fill: {LINE}; }}
  .label {{ font-size: 36px; font-weight: 600; letter-spacing: 1px; }}
  .note {{ font-size: 24px; fill: rgba(232,232,232,0.6); letter-spacing: 0.5px; }}
  .cap {{ font-size: 20px; font-weight: 600; fill: rgba(232,232,232,0.45); }}
  .cap.on {{ fill: #ffffff; font-size: 22px; }}
  .lead {{ stroke: {LINE}; stroke-width: 2.5; fill: none; }}
  .dot {{ fill: {LINE}; }}
</style>
"""


def label(x, y, text, note="", anchor="start"):
    out = f'<text class="label" x="{x}" y="{y}" text-anchor="{anchor}">{text}</text>'
    if note:
        out += f'<text class="note" x="{x}" y="{y + 30}" text-anchor="{anchor}">{note}</text>'
    return out


def lead(*points):
    path = " ".join(("M" if i == 0 else "L") + f" {x} {y}" for i, (x, y) in enumerate(points))
    x, y = points[0]
    return f'<path class="lead" d="{path}"/><circle class="dot" cx="{x}" cy="{y}" r="5"/>'


# ---------------------------------------------------------------------- keyboard and mouse

U = 64          # one key unit in pixels
X0, Y0 = 330, 330

ROWS = [
    [("Esc", 1)] + [(c, 1) for c in "1234567890"] + [("-", 1), ("=", 1), ("Back", 2)],
    [("Tab", 1.5)] + [(c, 1) for c in "QWERTYUIOP"] + [("[", 1), ("]", 1), ("\\", 1.5)],
    [("Caps", 1.75)] + [(c, 1) for c in "ASDFGHJKL"] + [(";", 1), ("'", 1), ("Enter", 2.25)],
    [("Shift", 2.25)] + [(c, 1) for c in "ZXCVBNM"] + [(",", 1), (".", 1), ("/", 1), ("Shift ", 2.75)],
    [("Ctrl", 1.25), ("Win", 1.25), ("Alt", 1.25), ("Space", 6.25), ("Alt ", 1.25), ("Win ", 1.25), ("Menu", 1.25), ("Ctrl ", 1.25)],
]
ACTIVE = {"Esc", "Tab", "W", "A", "S", "D", "E", "F", "G", "C", "Shift", "Ctrl", "Space", "Enter"}


def keyboard_svg():
    keys = {}
    parts = []
    for row, keys_in_row in enumerate(ROWS):
        x = X0
        y = Y0 + row * U
        for name, width in keys_in_row:
            w = width * U
            on = name in ACTIVE
            parts.append(
                f'<rect x="{x + 4}" y="{y + 4}" width="{w - 8}" height="{U - 8}" rx="9" '
                f'fill="{"rgba(255,74,61,0.16)" if on else "none"}" stroke="{LINE if on else DIM}" stroke-width="{3 if on else 2}"/>'
            )
            parts.append(f'<text class="cap{" on" if on else ""}" x="{x + w / 2}" y="{y + U / 2 + 7}" text-anchor="middle">{name.strip()}</text>')
            keys[name] = (x, y, w)
            x += w
    width = 15 * U
    parts.insert(0, f'<rect x="{X0 - 18}" y="{Y0 - 18}" width="{width + 36}" height="{5 * U + 36}" rx="22" fill="none" stroke="{LINE}" stroke-width="3"/>')

    def center(name):
        x, y, w = keys[name]
        return x + w / 2, y + U / 2

    def left(name):
        x, y, _ = keys[name]
        return x + 4, y + U / 2

    def top(name):
        x, y, w = keys[name]
        return x + w / 2, y + 4

    def bottom(name):
        x, y, w = keys[name]
        return x + w / 2, y + U - 4

    out = []
    # Left side.
    for name, text, note in [("Esc", "PAUSE", ""), ("Tab", "AI INSPECTOR", "watch his brain think"),
                             ("Shift", "SPRINT", "fast, LOUD, ~4 s")]:
        x, y = left(name)
        out.append(lead((x, y), (X0 - 18, y), (300, y)))
        out.append(label(285, y + 12, text, note, "end"))
    cx, cy = left("Ctrl")
    out.append(lead((cx, cy), (310, cy), (310, 680), (300, 680)))
    out.append(label(285, 692, "CROUCH", "hold CTRL or C · silent", "end"))
    # Top.
    wx, wy = top("W")
    out.append(lead((wx, wy), (wx, 250), (500, 250)))
    out.append(label(485, 262, "MOVE", "W A S D", "end"))
    ex, ey = top("E")
    out.append(lead((ex, ey), (ex, 200), (760, 200)))
    out.append(label(775, 212, "USE · HIDE · CLOSE DOORS", "hold E: breaker and exit panel (loud)"))
    # Bottom.
    fx, fy = bottom("F")
    out.append(lead((fx, fy), (fx, 735), (520, 735)))
    out.append(label(505, 747, "FLASHLIGHT", "he sees the light from far away", "end"))
    gx, gy = bottom("G")
    out.append(lead((gx, gy), (gx, 790), (1110, 790)))
    out.append(label(1125, 802, "THROW A PARTY HORN", "his brain goes to the noise"))
    sx, sy = bottom("Space")
    out.append(lead((sx, sy), (sx, 712), (1110, 712)))
    out.append(label(1125, 724, "HOLD YOUR BREATH", "while hidden, when he searches"))
    # Right: Enter.
    nx, ny = keys["Enter"][0] + keys["Enter"][2] - 4, keys["Enter"][1] + U / 2
    out.append(lead((nx, ny), (1345, ny), (1345, 240), (1400, 240)))
    out.append(label(1415, 252, "SKIP THE CALL"))

    # Mouse.
    mx, my = 1520, 470
    mouse = [
        f'<rect x="{mx - 75}" y="{my - 120}" width="150" height="240" rx="75" fill="none" stroke="{LINE}" stroke-width="3"/>',
        f'<line x1="{mx}" y1="{my - 120}" x2="{mx}" y2="{my - 30}" stroke="{LINE}" stroke-width="2.5"/>',
        f'<line x1="{mx - 75}" y1="{my - 30}" x2="{mx + 75}" y2="{my - 30}" stroke="{LINE}" stroke-width="2.5"/>',
        f'<rect x="{mx - 9}" y="{my - 95}" width="18" height="40" rx="9" fill="none" stroke="{LINE}" stroke-width="2.5"/>',
        f'<path d="M {mx - 72} {my - 50} Q {mx - 70} {my - 112} {mx - 3} {my - 117} L {mx - 3} {my - 33} L {mx - 72} {my - 33} Z" fill="rgba(255,74,61,0.16)"/>',
    ]
    out += mouse
    out.append(lead((mx + 75, my + 40), (1650, my + 40)))
    out.append(label(1665, my + 52, "LOOK AROUND"))
    out.append(lead((mx - 40, my - 80), (mx - 40, 300), (1650, 300)))
    out.append(label(1665, 312, "CLICK", "captures the mouse in the browser"))
    return "".join(parts + out)


# ---------------------------------------------------------------------- Xbox gamepad

BODY = (
    "M 640 330 C 700 298 815 296 880 322 L 1120 322 C 1185 296 1300 298 1360 330 "
    "C 1425 365 1452 430 1472 520 C 1495 625 1515 720 1484 782 C 1452 842 1362 836 1320 782 "
    "C 1272 722 1236 684 1162 684 L 838 684 C 764 684 728 722 680 782 "
    "C 638 836 548 842 516 782 C 485 720 505 625 528 520 C 548 430 575 365 640 330 Z"
)


def circle(cx, cy, r, extra=""):
    return f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="{LINE}" stroke-width="3" {extra}/>'


def face(cx, cy, letter, color):
    return (f'<circle cx="{cx}" cy="{cy}" r="31" fill="none" stroke="{LINE}" stroke-width="3"/>'
            f'<text x="{cx}" y="{cy + 13}" text-anchor="middle" style="font-size:38px;font-weight:700;fill:{color}">{letter}</text>')


def gamepad_svg():
    p = [f'<path d="{BODY}" fill="none" stroke="{LINE}" stroke-width="3.5"/>']
    # Bumpers and triggers.
    p.append(f'<path d="M 652 318 C 700 282 810 280 872 306" fill="none" stroke="{LINE}" stroke-width="3"/>')
    p.append(f'<path d="M 1348 318 C 1300 282 1190 280 1128 306" fill="none" stroke="{HOT}" stroke-width="5"/>')
    p.append(f'<rect x="700" y="210" width="80" height="78" rx="26" fill="none" stroke="{DIM}" stroke-width="3"/>')
    p.append(f'<rect x="1220" y="210" width="80" height="78" rx="26" fill="none" stroke="{DIM}" stroke-width="3"/>')
    # Sticks.
    p.append(circle(690, 470, 78) + circle(690, 470, 52))
    p.append(circle(1170, 600, 78) + circle(1170, 600, 52))
    # D-pad.
    p.append(f'<path d="M 805 555 h 34 v 34 h 34 v 34 h -34 v 34 h -34 v -34 h -34 v -34 h 34 Z" fill="none" stroke="{LINE}" stroke-width="3"/>')
    p.append('<path d="M 808 627 h 28 v 26 h -28 Z" fill="rgba(255,74,61,0.35)"/>')
    # View, Xbox, Menu.
    p.append(circle(925, 470, 20) + circle(1000, 410, 34) + circle(1075, 470, 20))
    p.append(f'<rect x="914" y="463" width="14" height="10" rx="2" fill="none" stroke="{LINE}" stroke-width="2"/>')
    p.append(f'<path d="M 1066 465 h 18 M 1066 470 h 18 M 1066 475 h 18" stroke="{LINE}" stroke-width="2"/>')
    # Face buttons.
    p.append(face(1310, 400, "Y", "#ffd23c") + face(1250, 465, "X", "#3c9bff") + face(1370, 465, "B", "#ff4a3d") + face(1310, 530, "A", "#4cd964"))

    out = []
    # Left: stick and D-pad.
    out.append(lead((612, 470), (420, 470)))
    out.append(label(405, 470, "MOVE", "", "end"))
    out.append(label(405, 510, "CLICK  L3:  SPRINT", "", "end").replace('class="label"', 'class="label" style="font-size:28px"'))
    out.append(lead((822, 655), (822, 830), (420, 830)))
    out.append(label(405, 842, "SKIP THE CALL", "D-pad down", "end"))
    # Top: View and Menu.
    out.append(lead((925, 450), (925, 150), (870, 150)))
    out.append(label(855, 162, "AI INSPECTOR", "View", "end"))
    out.append(lead((1075, 450), (1075, 150), (1130, 150)))
    out.append(label(1145, 162, "PAUSE", "Menu"))
    # Right: RB.
    out.append(lead((1330, 300), (1560, 300)))
    out.append(label(1575, 312, "THROW A PARTY HORN", "RB"))
    # Right: face buttons.
    out.append(lead((1404, 465), (1530, 465)))
    out.append(f'<path class="lead" d="M 1530 360 L 1545 360 L 1545 590 L 1530 590"/>')
    rows = [("Y", "#ffd23c", "FLASHLIGHT"), ("X", "#3c9bff", "HOLD YOUR BREATH"), ("B", "#ff4a3d", "CROUCH (toggle)"), ("A", "#4cd964", "USE · HIDE · DOORS")]
    for i, (letter, color, text) in enumerate(rows):
        y = 385 + i * 62
        out.append(f'<circle cx="1595" cy="{y}" r="21" fill="none" stroke="{color}" stroke-width="3"/>'
                   f'<text x="1595" y="{y + 9}" text-anchor="middle" style="font-size:26px;font-weight:700;fill:{color}">{letter}</text>')
        out.append(label(1630, y + 12, text))
    out.append(label(1630, 640, "", "hold A: breaker and exit panel"))
    # Bottom right: right stick.
    out.append(lead((1170, 678), (1170, 830), (1560, 830)))
    out.append(label(1575, 842, "LOOK AROUND", "right stick"))
    return "".join(p + out)


def render(name, body):
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="{WIDTH}" height="{HEIGHT}" viewBox="0 0 {WIDTH} {HEIGHT}">{STYLE}{body}</svg>'
    html = f'<!doctype html><html><body style="margin:0;background:transparent">{svg}</body></html>'
    with tempfile.TemporaryDirectory() as folder:
        page = Path(folder) / "diagram.html"
        page.write_text(html, encoding="utf-8")
        png = Path(folder) / "diagram.png"
        subprocess.run([CHROME, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--default-background-color=00000000",
                        f"--window-size={WIDTH},{HEIGHT}", f"--screenshot={png}", page.as_uri()],
                       check=True, capture_output=True)
        for output in OUTPUTS:
            output.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(png, output / name)
    print("wrote", name)


def main():
    render("controls-keyboard.png", keyboard_svg())
    render("controls-gamepad.png", gamepad_svg())


if __name__ == "__main__":
    main()
