"""Draw the signs and posters of the pizzeria with real typefaces and icons.

The game used to write every sign in a 5x7 pixel font. This tool draws them as textures instead,
with free fonts (Google Fonts: Oswald, Bebas Neue, Bungee, Luckiest Guy, Special Elite, Permanent
Marker) and Material Symbols icons, into Assets/Art/Signs/ of the Unity project:

    door_<room>.png      plate over a doorway: icon and name of the room it leads to, in the
                         colors of that room's wing (public: red and gold, staff: slate and white)
    exit_left/right.png  green EXIT box with the running man and an arrow toward the door
    exit_door.png        red EXIT lightbox over the doors out of the building
    title_<key>.png      the big wall signs (SHOWTIME, PARTY ROOM, SECURITY, ...)
    map_<where>.png      a "YOU ARE HERE" directory of the building, drawn from PizzeriaLayout.cs
    poster_<n>.png       aged posters of the restaurant
    signs.json           width / height of every texture

Fonts are downloaded once into tools/.font-cache (not committed).
Requirements: Python 3 with numpy, pillow and certifi, and internet access.
Usage (from the repository root):
    python tools/generate_signs.py                  # writes into unity/
    python tools/generate_signs.py --project ../ml-game
"""
import argparse
import json
import math
import re
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

from fetch_surfaces import ROOT, fetch

CACHE = Path(__file__).resolve().parent / ".font-cache"
GOOGLE = "https://raw.githubusercontent.com/google/fonts/main/"
ICONS = "https://raw.githubusercontent.com/google/material-design-icons/master/variablefont/"
FONTS = {
    "oswald": GOOGLE + "ofl/oswald/Oswald%5Bwght%5D.ttf",
    "bebas": GOOGLE + "ofl/bebasneue/BebasNeue-Regular.ttf",
    "bungee": GOOGLE + "ofl/bungee/Bungee-Regular.ttf",
    "luckiest": GOOGLE + "apache/luckiestguy/LuckiestGuy-Regular.ttf",
    "typewriter": GOOGLE + "apache/specialelite/SpecialElite-Regular.ttf",
    "marker": GOOGLE + "apache/permanentmarker/PermanentMarker-Regular.ttf",
    "icons": ICONS + "MaterialSymbolsOutlined%5BFILL,GRAD,opsz,wght%5D.ttf",
}
CODEPOINTS = ICONS + "MaterialSymbolsOutlined%5BFILL,GRAD,opsz,wght%5D.codepoints"

ROOMS = ["Office", "Parts & Service", "Restrooms", "Back Hall", "Storage", "Kitchen",
         "Dining Hall", "Main Hall", "Party Room", "Arcade", "Exit Lobby"]
STAFF_WING = {"Office", "Parts & Service", "Restrooms", "Back Hall", "Storage", "Kitchen"}
ROOM_ICONS = {
    "Office": "security", "Parts & Service": "build", "Restrooms": "wc", "Back Hall": "meeting_room",
    "Storage": "inventory_2", "Kitchen": "restaurant", "Dining Hall": "local_pizza", "Main Hall": "storefront",
    "Party Room": "celebration", "Arcade": "sports_esports", "Exit Lobby": "door_open",
}

GOLD = (236, 190, 86)
CREAM = (246, 236, 214)
RED = (128, 14, 18)
SLATE = (30, 42, 40)
WHITE = (238, 240, 236)
EXIT_GREEN = (10, 120, 52)


# ---------------------------------------------------------------- fonts

def font_file(name):
    CACHE.mkdir(exist_ok=True)
    path = CACHE / f"{name}.ttf"
    if not path.exists():
        path.write_bytes(fetch(FONTS[name]))
    return path


def font(name, size, weight=None):
    f = ImageFont.truetype(str(font_file(name)), size)
    if weight is not None:
        try:
            axes = f.get_variation_axes()
            values = []
            for axis in axes:
                label = axis["name"].decode() if isinstance(axis["name"], bytes) else str(axis["name"])
                values.append(weight if label.lower().startswith("weight") else axis["default"])
            f.set_variation_by_axes(values)
        except OSError:
            pass
    return f


_codepoints = None


def icon(name, size, filled=True):
    """The glyph of a Material Symbols icon and a font to draw it with."""
    global _codepoints
    if _codepoints is None:
        path = CACHE / "icons.codepoints"
        if not path.exists():
            CACHE.mkdir(exist_ok=True)
            path.write_bytes(fetch(CODEPOINTS))
        _codepoints = dict(line.split() for line in path.read_text().splitlines() if line.strip())
    f = ImageFont.truetype(str(font_file("icons")), size)
    axes = f.get_variation_axes()
    values = []
    for axis in axes:
        label = (axis["name"].decode() if isinstance(axis["name"], bytes) else str(axis["name"])).lower()
        if label.startswith("fill"):
            values.append(1 if filled else 0)
        elif label.startswith("weight"):
            values.append(600)
        elif label.startswith("optical"):
            values.append(48)
        else:
            values.append(axis["default"])
    f.set_variation_by_axes(values)
    return chr(int(_codepoints[name], 16)), f


# ---------------------------------------------------------------- helpers

def text_size(draw, text, f, spacing=0):
    if spacing == 0:
        box = draw.textbbox((0, 0), text, font=f)
        return box[2] - box[0], box[3] - box[1], box
    width = sum(draw.textbbox((0, 0), c, font=f)[2] for c in text) + spacing * (len(text) - 1)
    box = draw.textbbox((0, 0), text, font=f)
    return width, box[3] - box[1], box


def draw_text(draw, xy, text, f, fill, spacing=0, anchor_center=True):
    """Draws text centered on xy (with optional letter spacing)."""
    width, height, box = text_size(draw, text, f, spacing)
    x = xy[0] - width / 2 if anchor_center else xy[0]
    y = xy[1] - height / 2 - box[1]
    if spacing == 0:
        draw.text((x, y), text, font=f, fill=fill)
        return
    for c in text:
        draw.text((x, y), c, font=f, fill=fill)
        x += draw.textbbox((0, 0), c, font=f)[2] + spacing


def fit_font(draw, text, name, max_width, max_height, weight=None, spacing=0):
    size = max_height
    while size > 8:
        f = font(name, size, weight)
        w, h, _ = text_size(draw, text, f, spacing)
        if w <= max_width and h <= max_height:
            return f
        size -= 2
    return font(name, 8, weight)


def noise(size, scale, seed):
    rng = np.random.default_rng(seed)
    small = rng.random((max(2, size[1] // scale), max(2, size[0] // scale)))
    return np.asarray(Image.fromarray((small * 255).astype(np.uint8)).resize(size, Image.BICUBIC), dtype=np.float32) / 255.0


def grime(img, amount, seed):
    """Dirt, scratches and uneven fading: signs that have hung in a closed restaurant for years."""
    w, h = img.size
    arr = np.asarray(img.convert("RGB"), dtype=np.float32) / 255.0
    blotch = noise((w, h), 64, seed) * 0.75 + noise((w, h), 16, seed + 1) * 0.25
    dark = 1.0 - amount * np.clip(blotch - 0.45, 0, 1) * 1.2
    edge = np.ones((h, w), np.float32)
    yy, xx = np.mgrid[0:h, 0:w]
    border = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy)) / max(8.0, min(w, h) * 0.12)
    edge = 0.75 + 0.25 * np.clip(border, 0, 1)
    out = arr * dark[..., None] * edge[..., None]
    rng = np.random.default_rng(seed + 7)
    result = Image.fromarray(np.clip(out * 255, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(result)
    for _ in range(int(amount * 40)):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        length = rng.uniform(w * 0.02, w * 0.12)
        angle = rng.uniform(0, math.pi)
        shade = int(rng.uniform(150, 230))
        d.line([x, y, x + math.cos(angle) * length, y + math.sin(angle) * length], fill=(shade, shade, shade), width=1)
    return result


def screws(draw, w, h, inset, radius=7):
    for x, y in ((inset, inset), (w - inset, inset), (inset, h - inset), (w - inset, h - inset)):
        draw.ellipse([x - radius, y - radius, x + radius, y + radius], fill=(150, 150, 145), outline=(60, 60, 60), width=2)
        draw.line([x - radius + 3, y, x + radius - 3, y], fill=(70, 70, 70), width=2)


def glow_layer(img, color, radius):
    """A soft glow of the bright parts (neon and lightbox signs)."""
    alpha = img.convert("L").point(lambda v: 255 if v > 150 else 0)
    blurred = alpha.filter(ImageFilter.GaussianBlur(radius))
    layer = Image.new("RGB", img.size, color)
    return Image.composite(layer, Image.new("RGB", img.size, (0, 0, 0)), blurred)


# ---------------------------------------------------------------- signs

def door_plate(room):
    w, h = 1024, 256
    public = room not in STAFF_WING
    bg, fg = (RED, GOLD) if public else (SLATE, WHITE)
    img = Image.new("RGB", (w, h), bg)
    d = ImageDraw.Draw(img)
    # A subtle vertical gradient and a double border.
    for y in range(h):
        k = 1.0 - 0.25 * abs(y / h - 0.4)
        d.line([0, y, w, y], fill=tuple(int(c * k) for c in bg))
    d.rectangle([10, 10, w - 11, h - 11], outline=fg, width=6)
    d.rectangle([24, 24, w - 25, h - 25], outline=tuple(int(c * 0.6) for c in fg), width=2)
    glyph, ifont = icon(ROOM_ICONS[room], 150)
    d.text((70, h / 2), glyph, font=ifont, fill=fg, anchor="lm")
    name = room.upper()
    f = fit_font(d, name, "oswald", w - 330, 150, weight=650, spacing=6)
    draw_text(d, (240 + (w - 270) / 2, h / 2 + 4), name, f, CREAM if public else WHITE, spacing=6)
    screws(d, w, h, 46, 8)
    return grime(img, 0.35, hash(room) % 1000)


def exit_box(arrow_left):
    w, h = 768, 320
    img = Image.new("RGB", (w, h), EXIT_GREEN)
    d = ImageDraw.Draw(img)
    d.rectangle([14, 14, w - 15, h - 15], outline=(220, 255, 230), width=8)
    run, ifont = icon("directions_run", 200)
    arrow, afont = icon("arrow_back" if arrow_left else "arrow_forward", 170)
    # The word sits between the arrow (170 px) and the running man (200 px).
    f = fit_font(d, "EXIT", "bebas", w - 440, 230, spacing=8)
    if arrow_left:
        d.text((40, h / 2), arrow, font=afont, fill=WHITE, anchor="lm")
        draw_text(d, (40 + 170 + (w - 450) / 2, h / 2 + 8), "EXIT", f, WHITE, spacing=8)
        d.text((w - 30, h / 2), run, font=ifont, fill=WHITE, anchor="rm")
    else:
        d.text((30, h / 2), run, font=ifont, fill=WHITE, anchor="lm")
        draw_text(d, (30 + 200 + (w - 450) / 2, h / 2 + 8), "EXIT", f, WHITE, spacing=8)
        d.text((w - 40, h / 2), arrow, font=afont, fill=WHITE, anchor="rm")
    return img


def exit_door():
    w, h = 768, 256
    img = Image.new("RGB", (w, h), (90, 6, 6))
    d = ImageDraw.Draw(img)
    d.rectangle([12, 12, w - 13, h - 13], outline=(255, 210, 200), width=6)
    run, ifont = icon("directions_run", 170)
    d.text((50, h / 2), run, font=ifont, fill=(255, 235, 230), anchor="lm")
    draw_text(d, (w / 2 + 60, h / 2 + 8), "EXIT", font("bebas", 200), (255, 235, 230), spacing=12)
    return img


def neon_title(text, colors, width=1536, height=384, font_name="bungee"):
    """A lightbox / neon sign: bright letters with a colored glow on a dark backing."""
    img = Image.new("RGB", (width, height), (14, 8, 16))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([8, 8, width - 9, height - 9], radius=28, outline=colors[1], width=6)
    letters = Image.new("RGB", (width, height), (0, 0, 0))
    ld = ImageDraw.Draw(letters)
    f = fit_font(ld, text, font_name, width - 140, height - 120)
    draw_text(ld, (width / 2, height / 2), text, f, colors[0])
    glow = glow_layer(letters, colors[1], 18)
    img = ImageChops.add(img, glow)
    img = ImageChops.add(img, ImageChops.multiply(glow, Image.new("RGB", img.size, (120, 120, 120))))
    img.paste(letters, (0, 0), letters.convert("L").point(lambda v: 255 if v > 20 else 0))
    return grime(img, 0.2, hash(text) % 1000)


def staff_title(text, plate=(196, 192, 176), ink=(32, 34, 34), width=1280, height=320):
    """A stenciled metal plate (staff areas)."""
    img = Image.new("RGB", (width, height), plate)
    d = ImageDraw.Draw(img)
    d.rectangle([10, 10, width - 11, height - 11], outline=ink, width=8)
    f = fit_font(d, text, "oswald", width - 160, height - 110, weight=700, spacing=8)
    draw_text(d, (width / 2, height / 2 + 4), text, f, ink, spacing=8)
    screws(d, width, height, 34, 9)
    return grime(img, 0.3, hash(text) % 1000)


# ---------------------------------------------------------------- directory maps

def read_layout(project):
    source = (Path(project) / "Assets/Scripts/Game/PizzeriaLayout.cs").read_text()
    block = source[source.index("RoomShape[] Rooms"):source.index("Passage[] Passages")]
    rooms = [tuple(float(v) for v in m) for m in re.findall(r"Shape\((-?[\d.]+)f, (-?[\d.]+)f, ([\d.]+)f, ([\d.]+)f", block)]
    links = re.findall(r"Link\(Room\.\w+, Room\.\w+, (-?[\d.]+)f, (-?[\d.]+)f, (-?[\d.]+)f, (-?[\d.]+)f\)", source)
    front = re.search(r"ExitDoor = new Vector3\((-?[\d.]+)f, 0f, (-?[\d.]+)f\)", source)
    back = re.search(r"BackExitDoor = new Vector3\((-?[\d.]+)f, 0f, (-?[\d.]+)f\)", source)
    return rooms, [tuple(float(v) for v in l) for l in links], (float(front[1]), float(front[2])), (float(back[1]), float(back[2]))


def directory(project, here, title):
    rooms, links, front, back = read_layout(project)
    w, h = 1536, 1152
    img = Image.new("RGB", (w, h), (222, 214, 192))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, 150], fill=RED)
    draw_text(d, (w / 2, 76), "FREDDY FAZBEAR'S PIZZA  -  DIRECTORY", font("oswald", 74, 700), CREAM, spacing=4)
    xs = [r[0] for r in rooms] + [r[0] + r[2] for r in rooms]
    zs = [r[1] for r in rooms] + [r[1] + r[3] for r in rooms]
    min_x, max_x, min_z, max_z = min(xs) - 2, max(xs) + 2, min(zs) - 2, max(zs) + 2
    # Room on the left for the loading door's label.
    scale = min((w - 320) / (max_x - min_x), (h - 290) / (max_z - min_z))
    ox = 230 + ((w - 320) - (max_x - min_x) * scale) / 2
    oy = 180

    def p(x, z):
        return ox + (x - min_x) * scale, oy + (max_z - z) * scale

    for x1, z1, x2, z2 in links:
        a, b = p(x1, z1), p(x2, z2)
        half = 1.3 * scale
        if abs(a[0] - b[0]) > abs(a[1] - b[1]):
            d.rectangle([min(a[0], b[0]), a[1] - half, max(a[0], b[0]), a[1] + half], fill=(150, 144, 128))
        else:
            d.rectangle([a[0] - half, min(a[1], b[1]), a[0] + half, max(a[1], b[1])], fill=(150, 144, 128))
    for i, (x, z, rw, rd) in enumerate(rooms):
        name = ROOMS[i]
        fill = (196, 70, 64) if name not in STAFF_WING else (110, 128, 118)
        a, b = p(x, z + rd), p(x + rw, z)
        d.rectangle([a[0], a[1], b[0], b[1]], fill=fill, outline=(40, 30, 30), width=4)
        f = fit_font(d, name.upper(), "oswald", b[0] - a[0] - 14, 54, weight=650)
        draw_text(d, ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2), name.upper(), f, CREAM)
    f = font("oswald", 42, 700)
    for (ex, ez), lines, outside in ((front, ["EXIT  (FRONT DOORS)"], "below"), (back, ["EXIT", "LOADING", "DOOR"], "left")):
        c = p(ex, ez)
        d.rectangle([c[0] - 26, c[1] - 26, c[0] + 26, c[1] + 26], fill=EXIT_GREEN, outline=WHITE, width=4)
        for k, line in enumerate(lines):
            tw, th, _ = text_size(d, line, f)
            if outside == "below":
                d.text((c[0] - tw / 2, c[1] + 34 + k * 48), line, font=f, fill=EXIT_GREEN)
            else:
                d.text((c[0] - 40 - tw, c[1] - 70 + k * 48), line, font=f, fill=EXIT_GREEN)
    hx, hy = p(*here)
    for r, col in ((34, (255, 255, 255)), (26, (220, 20, 20))):
        d.ellipse([hx - r, hy - r, hx + r, hy + r], fill=col)
    f = font("oswald", 44, 700)
    d.text((hx + 40, hy - 70), "YOU ARE HERE", font=f, fill=(170, 10, 10))
    legend = "RED: RESTAURANT     GREY-GREEN: STAFF ONLY     GREEN: WAY OUT (ONE OPENS EACH NIGHT)"
    draw_text(d, (w / 2, h - 50), legend, font("oswald", 34, 500), (60, 50, 44))
    return grime(img, 0.45, hash(title) % 1000)


# ---------------------------------------------------------------- posters

def bear(d, cx, cy, s, color=(122, 72, 40)):
    """A cartoon of Freddy for the posters: ears, head, muzzle, eyes, top hat, bow tie."""
    dark = tuple(int(c * 0.6) for c in color)
    light = (214, 170, 120)
    for side in (-1, 1):
        d.ellipse([cx + side * 0.62 * s - 0.24 * s, cy - 0.78 * s, cx + side * 0.62 * s + 0.24 * s, cy - 0.3 * s], fill=color, outline=dark, width=4)
        d.ellipse([cx + side * 0.62 * s - 0.12 * s, cy - 0.66 * s, cx + side * 0.62 * s + 0.12 * s, cy - 0.42 * s], fill=dark)
    d.ellipse([cx - 0.75 * s, cy - 0.65 * s, cx + 0.75 * s, cy + 0.6 * s], fill=color, outline=dark, width=5)
    d.ellipse([cx - 0.42 * s, cy - 0.05 * s, cx + 0.42 * s, cy + 0.5 * s], fill=light)
    d.ellipse([cx - 0.1 * s, cy, cx + 0.1 * s, cy + 0.14 * s], fill=(30, 20, 18))
    d.arc([cx - 0.28 * s, cy + 0.06 * s, cx + 0.28 * s, cy + 0.42 * s], 20, 160, fill=(30, 20, 18), width=6)
    for side in (-1, 1):
        ex = cx + side * 0.3 * s
        d.ellipse([ex - 0.15 * s, cy - 0.33 * s, ex + 0.15 * s, cy - 0.05 * s], fill=(250, 250, 245), outline=dark, width=3)
        d.ellipse([ex - 0.06 * s, cy - 0.24 * s, ex + 0.06 * s, cy - 0.11 * s], fill=(40, 90, 160))
    d.rectangle([cx - 0.28 * s, cy - 1.12 * s, cx + 0.28 * s, cy - 0.62 * s], fill=(20, 18, 20))
    d.rectangle([cx - 0.42 * s, cy - 0.68 * s, cx + 0.42 * s, cy - 0.58 * s], fill=(20, 18, 20))
    d.polygon([(cx, cy + 0.66 * s), (cx - 0.3 * s, cy + 0.52 * s), (cx - 0.3 * s, cy + 0.8 * s)], fill=(20, 18, 20))
    d.polygon([(cx, cy + 0.66 * s), (cx + 0.3 * s, cy + 0.52 * s), (cx + 0.3 * s, cy + 0.8 * s)], fill=(20, 18, 20))


def paper(img, seed):
    """Old paper: yellowed, stained, folded in four, a torn corner and tape."""
    w, h = img.size
    rng = np.random.default_rng(seed)
    arr = np.asarray(img, dtype=np.float32) / 255.0
    yellow = np.array([1.0, 0.93, 0.78])
    arr = arr * (0.82 + 0.18 * yellow)
    stains = noise((w, h), 90, seed) * 0.7 + noise((w, h), 20, seed + 3) * 0.3
    arr *= (1.0 - 0.22 * np.clip(stains - 0.5, 0, 1) * 2)[..., None]
    for _ in range(2):
        cx, cy, r = rng.uniform(0, w), rng.uniform(0, h), rng.uniform(40, 140)
        yy, xx = np.mgrid[0:h, 0:w]
        ring = np.exp(-((np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) - r) / 6.0) ** 2)
        arr *= (1.0 - 0.18 * ring)[..., None]
    arr = np.clip(arr + (rng.random((h, w, 1)) - 0.5) * 0.05, 0, 1)
    out = Image.fromarray((arr * 255).astype(np.uint8))
    d = ImageDraw.Draw(out)
    d.line([w / 2, 0, w / 2, h], fill=(150, 140, 120), width=2)
    d.line([0, h / 2, w, h / 2], fill=(150, 140, 120), width=2)
    for x, y, angle in ((70, 30, -20), (w - 70, 30, 20)):
        tape = Image.new("RGBA", (160, 46), (235, 225, 190, 150))
        tape = tape.rotate(angle, expand=True)
        out.paste(tape, (int(x - tape.width / 2), int(y - tape.height / 2)), tape)
    corner = [(w, h), (w - 120, h), (w, h - 90)]
    d.polygon(corner, fill=(28, 24, 22))
    return out


def poster_celebrate():
    w, h = 768, 1024
    img = Image.new("RGB", (w, h), (30, 70, 150))
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(4)
    for _ in range(160):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        c = [(250, 210, 50), (240, 70, 90), (90, 220, 120), (250, 250, 250)][rng.integers(4)]
        d.rectangle([x, y, x + 10, y + 18], fill=c)
    bear(d, w / 2, h * 0.52, 250)
    draw_text(d, (w / 2, 110), "CELEBRATE!", font("luckiest", 130), (250, 220, 60))
    draw_text(d, (w / 2, h - 90), "FREDDY FAZBEAR'S PIZZA", font("oswald", 62, 700), WHITE, spacing=3)
    return paper(img, 11)


def poster_lets_eat():
    w, h = 768, 1024
    img = Image.new("RGB", (w, h), (196, 40, 32))
    d = ImageDraw.Draw(img)
    cx, cy, r = w / 2, h * 0.5, 250
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(214, 150, 60))
    d.ellipse([cx - r + 26, cy - r + 26, cx + r - 26, cy + r - 26], fill=(236, 190, 70))
    rng = np.random.default_rng(5)
    for _ in range(22):
        a, rr = rng.uniform(0, math.tau), rng.uniform(30, r - 60)
        x, y = cx + math.cos(a) * rr, cy + math.sin(a) * rr
        d.ellipse([x - 22, y - 22, x + 22, y + 22], fill=(170, 30, 30))
    for k in range(4):
        a = k * math.pi / 4
        d.line([cx - math.cos(a) * r, cy - math.sin(a) * r, cx + math.cos(a) * r, cy + math.sin(a) * r], fill=(180, 120, 50), width=5)
    draw_text(d, (w / 2, 120), "LET'S EAT!!!", font("luckiest", 120), (255, 240, 200))
    draw_text(d, (w / 2, h - 90), "FAMILY PIZZA  -  $4.99", font("oswald", 60, 700), (255, 240, 200), spacing=2)
    return paper(img, 12)


def poster_rules():
    w, h = 768, 1024
    img = Image.new("RGB", (w, h), (236, 228, 205))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, 170], fill=(30, 30, 34))
    draw_text(d, (w / 2, 88), "SAFETY RULES", font("oswald", 100, 700), (250, 210, 60), spacing=4)
    rules = ["1. NO RUNNING IN THE", "    DINING AREA.", "2. STAY WITH YOUR PARTY.", "3. DO NOT TOUCH THE", "    PERFORMERS.",
             "4. THE BAND RESTS AFTER", "    MIDNIGHT. LET IT REST.", "5. REPORT ANYTHING THAT", "    MOVES ON ITS OWN."]
    f = font("typewriter", 46)
    y = 220
    for line in rules:
        d.text((60, y), line, font=f, fill=(40, 36, 34))
        y += 68
    d.text((60, h - 120), "- MANAGEMENT", font=font("marker", 52), fill=(160, 20, 20))
    return paper(img, 13)


def poster_help_wanted():
    w, h = 768, 1024
    img = Image.new("RGB", (w, h), (240, 232, 210))
    d = ImageDraw.Draw(img)
    draw_text(d, (w / 2, 120), "HELP WANTED", font("bebas", 170), (150, 16, 18), spacing=4)
    lines = ["NIGHT SECURITY GUARD", "12 AM - 6 AM", "", "$120 / WEEK", "", "MUST BE CALM.", "MUST STAY ALERT.",
             "MUST NOT PANIC."]
    y = 280
    for line in lines:
        if line:
            draw_text(d, (w / 2, y), line, font("oswald", 54, 600), (40, 36, 34), spacing=2)
        y += 70
    draw_text(d, (w / 2, h - 110), "Management is not responsible", font("typewriter", 34), (90, 80, 70))
    draw_text(d, (w / 2, h - 70), "for injury or dismemberment.", font("typewriter", 34), (90, 80, 70))
    return paper(img, 14)


def poster_employee():
    w, h = 768, 1024
    img = Image.new("RGB", (w, h), (24, 60, 110))
    d = ImageDraw.Draw(img)
    draw_text(d, (w / 2, 110), "EMPLOYEE", font("bungee", 104), (250, 220, 70))
    draw_text(d, (w / 2, 220), "OF THE MONTH", font("bungee", 70), (250, 220, 70))
    d.rectangle([164, 300, w - 164, 760], fill=(220, 214, 200), outline=(250, 220, 70), width=10)
    bear(d, w / 2, 540, 150, color=(90, 60, 40))
    d.line([190, 330, w - 190, 730], fill=(20, 20, 20), width=10)
    d.line([w - 190, 330, 190, 730], fill=(20, 20, 20), width=10)
    draw_text(d, (w / 2, 850), "JEREMY F.", font("marker", 80), WHITE)
    return paper(img, 15)


# ---------------------------------------------------------------- arcade cabinets

GAMES = [
    ("FAZ-MAN", (255, 220, 40), (30, 60, 220)),
    ("STAR BLASTER", (80, 255, 240), (200, 40, 255)),
    ("PIZZA RUN", (255, 120, 40), (240, 30, 60)),
    ("GHOST MAZE", (120, 255, 120), (30, 180, 90)),
]


def marquee(index):
    title, fg, bg = GAMES[index]
    w, h = 1024, 256
    img = Image.new("RGB", (w, h), tuple(int(c * 0.25) for c in bg))
    d = ImageDraw.Draw(img)
    for i in range(0, w, 32):
        d.line([i, 0, i - 120, h], fill=tuple(int(c * 0.45) for c in bg), width=10)
    letters = Image.new("RGB", (w, h), (0, 0, 0))
    ld = ImageDraw.Draw(letters)
    f = fit_font(ld, title, "bungee", w - 120, h - 70)
    draw_text(ld, (w / 2 + 6, h / 2 + 6), title, f, tuple(int(c * 0.3) for c in fg))
    draw_text(ld, (w / 2, h / 2), title, f, fg)
    img = ImageChops.add(img, glow_layer(letters, bg, 14))
    img.paste(letters, (0, 0), letters.convert("L").point(lambda v: 255 if v > 30 else 0))
    return img


def screen(index):
    """The attract-mode picture of each game, in chunky pixels."""
    w, h = 160, 120
    img = Image.new("RGB", (w, h), (4, 4, 10))
    d = ImageDraw.Draw(img)
    title, fg, bg = GAMES[index]
    rng = np.random.default_rng(index + 30)
    if index == 0:
        for y in range(10, h - 10, 20):
            d.rectangle([8, y, w - 8, y + 3], fill=(30, 60, 220))
        for x in range(16, w - 10, 12):
            for y in range(20, h - 10, 20):
                d.rectangle([x, y, x + 1, y + 1], fill=(250, 220, 200))
        d.pieslice([60, 52, 76, 68], 30, 330, fill=(255, 220, 40))
        for k, col in enumerate([(255, 40, 40), (255, 160, 220), (60, 230, 255)]):
            x = 100 + k * 16
            d.rectangle([x, 54, x + 12, 66], fill=col)
    elif index == 1:
        for _ in range(60):
            x, y = rng.integers(0, w), rng.integers(0, h)
            d.point((x, y), fill=(200, 200, 255))
        for row in range(3):
            for col in range(8):
                x, y = 18 + col * 16, 14 + row * 14
                d.rectangle([x, y, x + 9, y + 6], fill=[(80, 255, 240), (200, 40, 255), (255, 220, 40)][row])
        d.polygon([(80, 100), (72, 112), (88, 112)], fill=(240, 240, 255))
        d.line([80, 96, 80, 70], fill=(255, 80, 80), width=1)
    elif index == 2:
        d.rectangle([0, 60, w, h], fill=(40, 120, 40))
        d.polygon([(70, 60), (90, 60), (150, h), (10, h)], fill=(70, 70, 76))
        for y in range(64, h, 10):
            d.rectangle([79, y, 81, y + 4], fill=(240, 240, 200))
        d.ellipse([70, 90, 90, 110], fill=(236, 190, 70))
        d.rectangle([0, 0, w, 60], fill=(255, 120, 60))
        d.ellipse([110, 18, 140, 48], fill=(255, 230, 120))
    else:
        for x in range(0, w, 16):
            for y in range(0, h, 16):
                if rng.random() < 0.45:
                    d.rectangle([x, y, x + 15, y + 15], fill=(20, 90, 50))
        for k in range(3):
            x, y = rng.integers(10, w - 20), rng.integers(10, h - 20)
            d.ellipse([x, y, x + 12, y + 14], fill=(230, 230, 255))
            d.rectangle([x + 3, y + 4, x + 5, y + 6], fill=(10, 10, 10))
            d.rectangle([x + 7, y + 4, x + 9, y + 6], fill=(10, 10, 10))
    d.text((4, 2), "1UP 004230", fill=(255, 255, 255))
    d.text((w - 70, h - 12), "INSERT COIN", fill=fg)
    img = img.resize((640, 480), Image.NEAREST)
    arr = np.asarray(img, dtype=np.float32)
    arr[::4] *= 0.55   # scanlines
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))


def side_art(index):
    title, fg, bg = GAMES[index]
    w, h = 512, 1024
    img = Image.new("RGB", (w, h), (10, 8, 14))
    d = ImageDraw.Draw(img)
    for k, col in enumerate([bg, fg, (250, 250, 250)]):
        off = k * 50
        d.polygon([(0, 640 + off), (w, 300 + off), (w, 330 + off), (0, 670 + off)], fill=col)
    rng = np.random.default_rng(index + 50)
    for _ in range(40):
        x, y, r = rng.uniform(0, w), rng.uniform(0, 520), rng.uniform(2, 6)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(240, 240, 255))
    letters = Image.new("RGB", (h, w), (0, 0, 0))
    ld = ImageDraw.Draw(letters)
    f = fit_font(ld, title, "bungee", h - 200, 150)
    draw_text(ld, (h / 2, w / 2), title, f, fg)
    letters = letters.rotate(90, expand=True)
    img.paste(letters, (0, 0), letters.convert("L").point(lambda v: 255 if v > 30 else 0))
    return grime(img, 0.35, index + 60)


# ---------------------------------------------------------------- decals (with transparency)

def leak(seed):
    """Water stains running down a wall from the ceiling: brown streaks that fade downward."""
    w, h = 512, 1024
    rng = np.random.default_rng(seed)
    alpha = np.zeros((h, w), np.float32)
    yy = np.arange(h)[:, None] / h
    for _ in range(rng.integers(3, 7)):
        cx = rng.uniform(0.2, 0.8) * w
        width = rng.uniform(12, 60)
        length = rng.uniform(0.45, 0.95)
        wobble = noise((1, h), 64, int(rng.integers(1000))).reshape(h)[:, None] * 30 - 15
        xx = np.arange(w)[None, :]
        streak = np.exp(-((xx - cx - wobble) / width) ** 2) * np.clip(1 - yy / length, 0, 1) ** 0.6
        alpha = np.maximum(alpha, streak * rng.uniform(0.5, 0.9))
    top = np.exp(-(yy / 0.12) ** 2) * (0.5 + 0.5 * noise((w, h), 40, seed))
    alpha = np.clip(np.maximum(alpha, top) * (0.6 + 0.4 * noise((w, h), 8, seed + 1)), 0, 1)
    color = np.zeros((h, w, 3), np.float32) + np.array([0.28, 0.2, 0.12])
    color *= (0.7 + 0.3 * noise((w, h), 16, seed + 2))[..., None]
    rgba = np.dstack([color, alpha * 0.85])
    return Image.fromarray((rgba * 255).astype(np.uint8), "RGBA")


def mold(seed):
    """Dark green-black mold creeping up from the floor."""
    w, h = 1024, 512
    yy = np.arange(h)[:, None] / h
    n = noise((w, h), 30, seed) * 0.6 + noise((w, h), 7, seed + 1) * 0.4
    reach = (1 - yy) * 1.4   # stronger at the bottom of the texture
    alpha = np.clip((n * reach - 0.35) * 2.2, 0, 1)
    color = np.dstack([np.full((h, w), 0.08), np.full((h, w), 0.11), np.full((h, w), 0.07)])
    rgba = np.dstack([color, alpha * 0.9])
    return Image.fromarray((rgba[::-1] * 255).astype(np.uint8), "RGBA")


def graffiti(text, seed, color=(170, 20, 20), size=(1024, 512), font_name="marker"):
    w, h = size
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    f = fit_font(d, text, font_name, w - 80, h - 80)
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    draw_text(ld, (w / 2, h / 2), text, f, color + (235,))
    # Drips under the letters.
    rng = np.random.default_rng(seed)
    alpha = np.asarray(layer)[..., 3]
    cols = np.where(alpha.max(axis=0) > 0)[0]
    for _ in range(14):
        if len(cols) == 0:
            break
        x = int(rng.choice(cols))
        rows = np.where(alpha[:, x] > 0)[0]
        y0 = int(rows.max())
        length = int(rng.uniform(20, 120))
        ld.line([x, y0, x, min(h - 1, y0 + length)], fill=color + (200,), width=int(rng.uniform(3, 7)))
    worn = (noise((w, h), 6, seed) * 0.5 + 0.5)
    arr = np.asarray(layer).astype(np.float32)
    arr[..., 3] *= worn
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


def puddle(seed):
    """A shallow puddle: an irregular soft-edged blob (the wet look comes from the material)."""
    w = h = 512
    yy, xx = np.mgrid[0:h, 0:w]
    r = np.sqrt(((xx - w / 2) / (w * 0.45)) ** 2 + ((yy - h / 2) / (h * 0.32)) ** 2)
    wobble = noise((w, h), 60, seed) * 0.5
    alpha = np.clip((1.0 - r - wobble * 0.6) * 4.0, 0, 1)
    color = np.zeros((h, w, 3), np.float32) + 0.04
    rgba = np.dstack([color, alpha * 0.75])
    return Image.fromarray((rgba * 255).astype(np.uint8), "RGBA")


def handprints(seed):
    """Small painted handprints: a party craft gone wrong."""
    w, h = 1024, 512
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(seed)
    colors = [(200, 30, 30), (40, 90, 200), (230, 190, 40), (40, 160, 70)]
    for k in range(7):
        cx, cy = 90 + k * 135 + rng.uniform(-20, 20), h / 2 + rng.uniform(-120, 120)
        c = colors[k % 4] + (220,)
        d.ellipse([cx - 34, cy - 30, cx + 34, cy + 38], fill=c)
        for f in range(5):
            angle = math.radians(-70 + f * 32)
            fx, fy = cx + math.sin(angle) * 52, cy - math.cos(angle) * 52
            d.ellipse([fx - 11, fy - 22, fx + 11, fy + 22], fill=c)
    return img


# ---------------------------------------------------------------- main

def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--project", default=str(ROOT / "unity"), help="Unity project folder")
    args = parser.parse_args()
    out = Path(args.project) / "Assets" / "Art" / "Signs"
    out.mkdir(parents=True, exist_ok=True)
    made = {}

    def save_rgba(name, img):
        img.save(out / f"{name}.png", optimize=True)
        made[name] = {"width": img.width, "height": img.height}
        print(f"{name:28s} {img.width}x{img.height} rgba")

    def save(name, img):
        img.convert("RGB").save(out / f"{name}.png", optimize=True)
        made[name] = {"width": img.width, "height": img.height}
        print(f"{name:28s} {img.width}x{img.height}")

    for room in ROOMS:
        save("door_" + re.sub(r"[^a-z]+", "_", room.lower()).strip("_"), door_plate(room))
    save("exit_left", exit_box(True))
    save("exit_right", exit_box(False))
    save("exit_door", exit_door())
    public = {"showtime": ("SHOWTIME", ((255, 230, 120), (255, 120, 40))),
              "lets_party": ("LET'S PARTY!", ((255, 220, 90), (240, 40, 90))),
              "welcome": ("WELCOME", ((255, 230, 140), (255, 90, 40))),
              "party_room": ("PARTY ROOM", ((255, 220, 90), (240, 40, 140))),
              "prizes": ("PRIZES", ((170, 255, 255), (40, 200, 255))),
              "happy_birthday": ("HAPPY BIRTHDAY", ((180, 240, 255), (60, 120, 255))),
              "arcade": ("ARCADE", ((255, 170, 255), (190, 40, 255)))}
    for key, (text, colors) in public.items():
        save("title_" + key, neon_title(text, colors))
    staff = {"security": "SECURITY", "parts_service": "PARTS & SERVICE", "storage": "STORAGE",
             "staff_only": "STAFF ONLY", "out_of_order": "OUT OF ORDER"}
    for key, text in staff.items():
        plate = (210, 60, 50) if key == "out_of_order" else (196, 192, 176)
        ink = (250, 240, 230) if key == "out_of_order" else (32, 34, 34)
        save("title_" + key, staff_title(text, plate, ink))
    save("map_back_hall", directory(args.project, (-17.0, -0.5), "back"))
    save("map_main_hall", directory(args.project, (9.0, -0.5), "main"))
    for i in range(3):
        save_rgba(f"decal_leak_{i}", leak(80 + i))
    for i in range(2):
        save_rgba(f"decal_mold_{i}", mold(90 + i))
    save_rgba("decal_graffiti_0", graffiti("IT'S ME", 101))
    save_rgba("decal_graffiti_1", graffiti("SAVE THEM", 102, (30, 30, 30)))
    save_rgba("decal_graffiti_2", graffiti("HELP", 103, (150, 10, 10), font_name="typewriter"))
    save_rgba("decal_handprints", handprints(104))
    save_rgba("decal_puddle", puddle(105))
    save("title_prize_claw", neon_title("PRIZE CLAW", ((255, 200, 250), (255, 40, 180)), 1024, 256))
    for i in range(len(GAMES)):
        save(f"arcade_marquee_{i}", marquee(i))
        save(f"arcade_screen_{i}", screen(i))
        save(f"arcade_side_{i}", side_art(i))
    for i, make in enumerate([poster_celebrate, poster_lets_eat, poster_rules, poster_help_wanted, poster_employee]):
        save(f"poster_{i}", make())
    (out / "signs.json").write_text(json.dumps(made, indent=2, sort_keys=True) + "\n")


if __name__ == "__main__":
    main()
