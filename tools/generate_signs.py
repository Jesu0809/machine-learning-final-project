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
    label_<item>.png     the objective items: the breaker's warning plate, the staff keycard
                         and the label of the security tape
    breaker_*.png        the breaker panel's dead front with its circuit labels, and the
                         circuit directory inside its door
    cctv_<room>.png      what the office monitors draw over each camera's picture
    drawing_<n>.png      children's crayon drawings taped to the walls
    key_<n>.png          the keys of the tape box's keypad
    lcd_<message>.png    what the card reader and the tape box show on their little screens
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
import zlib
from pathlib import Path

import numpy as np
from PIL import ImageEnhance, Image, ImageChops, ImageDraw, ImageFilter, ImageFont

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
    "crayon": GOOGLE + "apache/schoolbell/Schoolbell-Regular.ttf",
    "lcd": GOOGLE + "ofl/vt323/VT323-Regular.ttf",
    "icons": ICONS + "MaterialSymbolsOutlined%5BFILL,GRAD,opsz,wght%5D.ttf",
}
CODEPOINTS = ICONS + "MaterialSymbolsOutlined%5BFILL,GRAD,opsz,wght%5D.codepoints"

ROOMS = ["Office", "Parts & Service", "Restrooms", "Back Hall", "Storage", "Kitchen",
         "Dining Hall", "Main Hall", "Party Room", "Arcade", "Exit Lobby", "Banquet Room", "Veranda"]
# The power sockets and the color of the cell each takes (yellow, blue, green).
SOCKETS = {"Parts & Service": (250, 196, 40), "Banquet Room": (60, 140, 255), "Party Room": (70, 220, 110)}
STAFF_WING = {"Office", "Parts & Service", "Restrooms", "Back Hall", "Storage", "Kitchen"}
ROOM_ICONS = {
    "Office": "security", "Parts & Service": "build", "Restrooms": "wc", "Back Hall": "meeting_room",
    "Storage": "inventory_2", "Kitchen": "restaurant", "Dining Hall": "local_pizza", "Main Hall": "storefront",
    "Party Room": "celebration", "Arcade": "sports_esports", "Exit Lobby": "door_open",
    "Banquet Room": "table_restaurant", "Veranda": "deck",
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


def seed(text):
    """A stable seed for a sign's grime (Python's hash() changes on every run)."""
    return zlib.crc32(text.encode()) % 1000


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
    return grime(img, 0.35, seed(room))


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
    return grime(img, 0.2, seed(text))


def staff_title(text, plate=(196, 192, 176), ink=(32, 34, 34), width=1280, height=320):
    """A stenciled metal plate (staff areas)."""
    img = Image.new("RGB", (width, height), plate)
    d = ImageDraw.Draw(img)
    d.rectangle([10, 10, width - 11, height - 11], outline=ink, width=8)
    f = fit_font(d, text, "oswald", width - 160, height - 110, weight=700, spacing=8)
    draw_text(d, (width / 2, height / 2 + 4), text, f, ink, spacing=8)
    screws(d, width, height, 34, 9)
    return grime(img, 0.3, seed(text))


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
    hx, hy = p(*here)
    for i, (x, z, rw, rd) in enumerate(rooms):
        name = ROOMS[i]
        fill = (196, 70, 64) if name not in STAFF_WING else (110, 128, 118)
        a, b = p(x, z + rd), p(x + rw, z)
        d.rectangle([a[0], a[1], b[0], b[1]], fill=fill, outline=(40, 30, 30), width=4)
        # The name keeps clear of the YOU ARE HERE pin when the pin is in this room.
        left, right = a[0] + 12, b[0] - 12
        if a[0] <= hx <= b[0] and a[1] <= hy <= b[1]:
            if hx - a[0] < b[0] - hx:
                left = hx + 36
            else:
                right = hx - 36
        f = fit_font(d, name.upper(), "oswald", right - left, 54, weight=650)
        draw_text(d, ((left + right) / 2, (a[1] + b[1]) / 2), name.upper(), f, CREAM)
    f = font("oswald", 40, 700)
    # The exits sit just outside their wall, their labels where nothing else is drawn.
    for (ex, ez), lines, outside in ((front, ["EXIT", "FRONT DOORS"], "below"), (back, ["EXIT", "LOADING", "DOOR"], "left")):
        c = p(ex, ez)
        c = (c[0], c[1] + 26) if outside == "below" else (c[0] - 26, c[1])
        d.rectangle([c[0] - 22, c[1] - 22, c[0] + 22, c[1] + 22], fill=EXIT_GREEN, outline=WHITE, width=4)
        for k, line in enumerate(lines):
            tw, th, _ = text_size(d, line, f)
            if outside == "below":
                d.text((c[0] - 40 - tw, c[1] - 46 + k * 46), line, font=f, fill=EXIT_GREEN)
            else:
                d.text((c[0] - 40 - tw, c[1] - 70 + k * 46), line, font=f, fill=EXIT_GREEN)
    for r, col in ((26, (255, 255, 255)), (19, (220, 20, 20))):
        d.ellipse([hx - r, hy - r, hx + r, hy + r], fill=col)
    f = font("oswald", 40, 700)
    tw, th, box = text_size(d, "YOU ARE HERE", f)
    bx, by = hx - tw / 2 - 16, hy + 38
    d.line([hx, hy + 24, hx, by], fill=(170, 10, 10), width=5)
    d.rectangle([bx, by, bx + tw + 32, by + th + 22], fill=(250, 246, 236), outline=(170, 10, 10), width=4)
    d.text((bx + 16, by + 11 - box[1]), "YOU ARE HERE", font=f, fill=(170, 10, 10))
    # The power sockets, in their colors (from night 2 each cell fits only its own).
    glyph, icon_font = icon("power", 34)
    for i, (x, z, rw, rd) in enumerate(rooms):
        if ROOMS[i] in SOCKETS:
            a, b = p(x, z + rd), p(x + rw, z)
            cx, cy = b[0] - 30, b[1] - 30
            d.ellipse([cx - 24, cy - 24, cx + 24, cy + 24], fill=SOCKETS[ROOMS[i]], outline=(30, 30, 30), width=4)
            draw_text(d, (cx, cy), glyph, icon_font, (25, 25, 25))
    legend = "RED: RESTAURANT    GREY-GREEN: STAFF ONLY    GREEN: WAY OUT    PLUGS: POWER SOCKETS"
    draw_text(d, (w / 2, h - 50), legend, font("oswald", 32, 500), (60, 50, 44))
    return grime(img, 0.45, seed(title))


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


def paper(img, seed, stains=1.0):
    """Old paper: yellowed, stained, folded in four, a torn corner and tape (stains scales the dirt)."""
    w, h = img.size
    rng = np.random.default_rng(seed)
    arr = np.asarray(img, dtype=np.float32) / 255.0
    yellow = np.array([1.0, 0.93, 0.78])
    arr = arr * (0.82 + 0.18 * yellow)
    strength = stains
    blotches = noise((w, h), 90, seed) * 0.7 + noise((w, h), 20, seed + 3) * 0.3
    arr *= (1.0 - 0.22 * strength * np.clip(blotches - 0.5, 0, 1) * 2)[..., None]
    for _ in range(2):
        cx, cy, r = rng.uniform(0, w), rng.uniform(0, h), rng.uniform(40, 140)
        yy, xx = np.mgrid[0:h, 0:w]
        ring = np.exp(-((np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) - r) / 6.0) ** 2)
        arr *= (1.0 - 0.18 * strength * ring)[..., None]
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

# ---------------------------------------------------------------- objective items

def label_reactivate():
    """A manila repair tag tied to a spare head."""
    w, h = 384, 192
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(40, 0), (w - 1, 0), (w - 1, h - 1), (40, h - 1), (0, h - 40), (0, 40)], fill=(214, 186, 128, 255))
    d.ellipse([22, h / 2 - 12, 46, h / 2 + 12], fill=(0, 0, 0, 0), outline=(150, 120, 70, 255), width=4)
    draw_text(d, (215, 62), "DO NOT", font("marker", 52), (140, 20, 20, 255))
    draw_text(d, (215, 132), "REACTIVATE", font("marker", 44), (140, 20, 20, 255))
    return img


def label_danger():
    """The warning plate on the breaker box door."""
    w, h = 512, 640
    img = Image.new("RGB", (w, h), (238, 196, 40))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, 150], fill=(20, 20, 20))
    draw_text(d, (w / 2, 78), "DANGER", font("bebas", 140), (238, 196, 40), spacing=10)
    d.polygon([(w / 2, 190), (w / 2 - 150, 450), (w / 2 + 150, 450)], fill=(20, 20, 20))
    d.polygon([(w / 2, 222), (w / 2 - 122, 434), (w / 2 + 122, 434)], fill=(238, 196, 40))
    glyph, f = icon("bolt", 190)
    draw_text(d, (w / 2, 352), glyph, f, (20, 20, 20))
    draw_text(d, (w / 2, 512), "HIGH VOLTAGE", font("oswald", 64, 700), (20, 20, 20), spacing=3)
    draw_text(d, (w / 2, 584), "AUTHORIZED STAFF ONLY", font("oswald", 38, 500), (20, 20, 20), spacing=2)
    screws(d, w, h, 26, 8)
    return grime(img, 0.45, 201)


def label_keycard():
    """The staff keycard in the office lockbox."""
    w, h = 640, 404
    img = Image.new("RGB", (w, h), (232, 234, 236))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, 120], fill=(30, 70, 150))
    draw_text(d, (w / 2 + 58, 62), "FAZBEAR ENTERTAINMENT", font("oswald", 42, 700), WHITE, spacing=2)
    bear(d, 70, 70, 44, color=(150, 100, 60))
    d.rectangle([40, 160, 200, 350], fill=(190, 196, 204), outline=(120, 126, 134), width=3)
    glyph, f = icon("person", 150)
    draw_text(d, (120, 258), glyph, f, (120, 126, 134))
    draw_text(d, (430, 190), "STAFF ACCESS", font("oswald", 54, 700), (30, 70, 150), spacing=2)
    draw_text(d, (430, 252), "LEVEL 2  -  NIGHT", font("oswald", 34, 500), (60, 64, 70), spacing=2)
    d.rectangle([240, 300, 610, 340], fill=(24, 24, 26))
    for i in range(34):
        x = 248 + i * 10.6
        d.rectangle([x, 304, x + (3 if i % 3 else 6), 336], fill=(232, 234, 236))
    d.rectangle([0, h - 30, w, h], fill=(200, 30, 30))
    return grime(img, 0.15, 202)


def label_tape():
    """The handwritten label on the security tape."""
    w, h = 768, 256
    img = Image.new("RGB", (w, h), (236, 228, 200))
    d = ImageDraw.Draw(img)
    for y in range(70, h, 46):
        d.line([20, y, w - 20, y], fill=(170, 180, 205), width=2)
    d.rectangle([0, 0, w, 34], fill=(190, 40, 40))
    draw_text(d, (w / 2, 18), "SECURITY  -  CAM 03", font("oswald", 26, 600), WHITE, spacing=4)
    draw_text(d, (w / 2, 112), "NIGHT OF 11/12/87", font("marker", 64), (30, 30, 80))
    draw_text(d, (w / 2, 190), "DO NOT TAPE OVER", font("marker", 50), (150, 20, 20))
    return grime(img, 0.25, 203)


# ---------------------------------------------------------------- breaker panel

CIRCUITS = ["KITCHEN", "FREEZER", "STAGE LIGHTS", "ARCADE", "OFFICE", "SIGNS"]


def breaker_deadfront():
    """The steel plate around the breakers (0.72 x 0.92 m at 800 px/m), with the circuit names
    written on tape under each breaker. Breakers sit at x = +-0.13 m and y = 0.2, 0, -0.2 m."""
    w, h = 576, 736
    img = Image.new("RGB", (w, h), (92, 96, 98))
    d = ImageDraw.Draw(img)
    d.rectangle([14, 14, w - 15, h - 15], outline=(60, 62, 64), width=4)
    draw_text(d, (w / 2, 52), "PANEL B   120/240 V", font("oswald", 34, 600), (30, 32, 34), spacing=3)
    draw_text(d, (w / 2, 88), "FAZ-ELECTRIC  LOAD CENTER", font("oswald", 20, 500), (40, 42, 44), spacing=2)
    for i in range(6):
        # Odd circuits down the left column, even down the right, as on a real panel.
        number = 2 * i + 1 if i < 3 else 2 * (i - 3) + 2
        name = CIRCUITS[number - 1]
        cx = w / 2 + (-0.13 if i < 3 else 0.13) * 800
        cy = h / 2 - (0.2 - (i % 3) * 0.2) * 800
        # The opening the breaker comes through, and the tape label under it.
        d.rectangle([cx - 76, cy - 52, cx + 76, cy + 52], fill=(20, 20, 22))
        tape = Image.new("RGBA", (150, 38), (232, 222, 190, 255))
        td = ImageDraw.Draw(tape)
        draw_text(td, (75, 20), name, fit_font(td, name, "marker", 138, 26), (30, 30, 70))
        tape = tape.rotate((i * 7 % 5) - 2, expand=True, resample=Image.BICUBIC)
        img.paste(tape, (int(cx - tape.width / 2), int(cy + 58)), tape)
        draw_text(d, (cx - 92 if i < 3 else cx + 92, cy), str(number), font("oswald", 26, 600), (30, 32, 34))
    screws(d, w, h, 34, 9)
    return grime(img, 0.5, 301)


def breaker_module():
    """The face of one breaker: rating, ON / OFF, the maker."""
    w, h = 288, 192
    img = Image.new("RGB", (w, h), (24, 24, 26))
    d = ImageDraw.Draw(img)
    d.rectangle([4, 4, w - 5, h - 5], outline=(48, 48, 52), width=4)
    draw_text(d, (w / 2, 30), "ON", font("oswald", 26, 700), (200, 200, 190), spacing=2)
    draw_text(d, (w / 2, h - 28), "OFF", font("oswald", 26, 700), (200, 200, 190), spacing=2)
    draw_text(d, (46, h / 2), "20", font("oswald", 40, 700), (215, 215, 205))
    draw_text(d, (w - 50, h / 2), "FAZ", font("oswald", 22, 500), (150, 150, 145), spacing=2)
    d.rectangle([w / 2 - 30, 52, w / 2 + 30, h - 52], fill=(10, 10, 12))
    return grime(img, 0.2, 302)


def circuit_directory():
    """The card inside the breaker door: which breaker feeds what."""
    w, h = 384, 512
    img = Image.new("RGB", (w, h), (238, 232, 212))
    d = ImageDraw.Draw(img)
    draw_text(d, (w / 2, 34), "CIRCUIT DIRECTORY", font("oswald", 32, 700), (30, 30, 30), spacing=2)
    for row in range(12):
        y = 74 + row * 34
        d.line([18, y + 30, w - 18, y + 30], fill=(150, 160, 190), width=2)
        draw_text(d, (34, y + 14), str(row + 1), font("oswald", 22, 500), (40, 40, 40))
        if row < len(CIRCUITS):
            draw_text(d, (200, y + 14), CIRCUITS[row].title(), font("marker", 24), (30, 30, 80))
    draw_text(d, (w / 2 - 30, h - 52), "Never reset FREEZER in a show!", font("marker", 19), (150, 20, 20))
    return grime(paper(img.resize((w, h)), 303), 0.2, 304)


# ---------------------------------------------------------------- office monitors

def cctv_overlay(room, number):
    """Transparent overlay for a security monitor: corner marks, camera number, room, REC."""
    w, h = 512, 384
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for y in range(0, h, 4):
        d.line([0, y, w, y], fill=(0, 0, 0, 46))
    white = (230, 240, 230, 235)
    for x, y, sx, sy in ((20, 20, 1, 1), (w - 20, 20, -1, 1), (20, h - 20, 1, -1), (w - 20, h - 20, -1, -1)):
        d.line([x, y, x + sx * 40, y], fill=white, width=4)
        d.line([x, y, x, y + sy * 40], fill=white, width=4)
    f = font("oswald", 34, 600)
    draw_text(d, (40, 56), f"CAM {number:02d}", f, white, spacing=2, anchor_center=False)
    draw_text(d, (40, 96), room.upper(), font("oswald", 26, 500), white, spacing=2, anchor_center=False)
    d.ellipse([w - 128, 42, w - 106, 64], fill=(230, 30, 30, 240))
    draw_text(d, (w - 96, 54), "REC", font("oswald", 28, 600), white, spacing=2, anchor_center=False)
    draw_text(d, (40, h - 52), "FAZBEAR SECURITY   11-12-87", font("oswald", 22, 500), white, spacing=2, anchor_center=False)
    # Darker corners, like the edge of a picture tube.
    yy, xx = np.mgrid[0:h, 0:w]
    r = np.sqrt(((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2)
    vignette = np.clip((r - 0.75) * 1.6, 0, 1) * 200
    arr = np.asarray(img).copy()
    arr[..., 3] = np.maximum(arr[..., 3], vignette.astype(np.uint8))
    return Image.fromarray(arr, "RGBA")


# ---------------------------------------------------------------- children's drawings

def crayon(layer, points, color, width, seed, closed=False):
    """A crayon stroke: several wobbly, broken passes instead of one clean line."""
    rng = np.random.default_rng(seed)
    d = ImageDraw.Draw(layer)
    pts = list(points) + ([points[0]] if closed else [])
    for _ in range(4):
        wobbly = []
        for x, y in pts:
            wobbly.append((x + rng.normal(0, width * 0.35), y + rng.normal(0, width * 0.35)))
        alpha = int(rng.uniform(140, 220))
        for a, b in zip(wobbly, wobbly[1:]):
            # Waxy gaps along the stroke.
            steps = max(2, int(math.dist(a, b) / 6))
            for k in range(steps):
                if rng.random() < 0.12:
                    continue
                t0, t1 = k / steps, (k + 1) / steps
                p0 = (a[0] + (b[0] - a[0]) * t0, a[1] + (b[1] - a[1]) * t0)
                p1 = (a[0] + (b[0] - a[0]) * t1, a[1] + (b[1] - a[1]) * t1)
                d.line([p0, p1], fill=color + (alpha,), width=max(1, int(width * rng.uniform(0.5, 0.9))))


def circle_points(cx, cy, rx, ry, n=36, start=0.0):
    return [(cx + rx * math.cos(start + 2 * math.pi * i / n), cy + ry * math.sin(start + 2 * math.pi * i / n)) for i in range(n)]


def scribble(layer, cx, cy, rx, ry, color, seed, density=7):
    """Coloring in, the way a child does: back-and-forth strokes that spill over the edge."""
    rng = np.random.default_rng(seed)
    pts = []
    y = cy - ry
    flip = 1
    while y < cy + ry:
        half = rx * math.sqrt(max(0.0, 1 - ((y - cy) / ry) ** 2))
        pts.append((cx - flip * (half + rng.normal(0, 6)), y))
        pts.append((cx + flip * (half + rng.normal(0, 6)), y + density * 0.5))
        y += density
        flip = -flip
    crayon(layer, pts, color, 5, seed)


def kid_bear(layer, cx, cy, s, seed, eyes=(40, 40, 40), body=True):
    brown = (120, 70, 35)
    scribble(layer, cx, cy, s * 0.55, s * 0.5, (150, 95, 50), seed)
    crayon(layer, circle_points(cx, cy, s * 0.55, s * 0.5), brown, 6, seed + 1, True)
    for side in (-1, 1):
        crayon(layer, circle_points(cx + side * s * 0.45, cy - s * 0.48, s * 0.15, s * 0.15), brown, 6, seed + 2 + side, True)
        crayon(layer, circle_points(cx + side * s * 0.2, cy - s * 0.1, s * 0.08, s * 0.08), eyes, 7, seed + 4 + side, True)
    crayon(layer, [(cx - s * 0.2, cy + s * 0.18), (cx, cy + s * 0.28), (cx + s * 0.2, cy + s * 0.18)], (30, 20, 20), 6, seed + 6)
    hat = [(cx - s * 0.18, cy - s * 0.48), (cx - s * 0.18, cy - s * 0.85), (cx + s * 0.18, cy - s * 0.85), (cx + s * 0.18, cy - s * 0.48)]
    scribble(layer, cx, cy - s * 0.66, s * 0.17, s * 0.17, (20, 20, 20), seed + 7, 5)
    crayon(layer, hat, (20, 20, 20), 6, seed + 8)
    crayon(layer, [(cx - s * 0.35, cy - s * 0.48), (cx + s * 0.35, cy - s * 0.48)], (20, 20, 20), 7, seed + 9)
    if body:
        crayon(layer, circle_points(cx, cy + s * 1.15, s * 0.45, s * 0.62), brown, 6, seed + 10, True)
        crayon(layer, [(cx - s * 0.12, cy + s * 0.55), (cx + s * 0.12, cy + s * 0.65), (cx + s * 0.12, cy + s * 0.5), (cx - s * 0.12, cy + s * 0.6), (cx - s * 0.12, cy + s * 0.5)], (20, 20, 20), 6, seed + 11)


def stick_kid(layer, cx, cy, s, color, seed, sad=False):
    crayon(layer, circle_points(cx, cy, s * 0.22, s * 0.24), (40, 40, 40), 5, seed, True)
    crayon(layer, [(cx, cy + s * 0.24), (cx, cy + s * 0.9)], color, 6, seed + 1)
    crayon(layer, [(cx - s * 0.35, cy + s * 0.45), (cx, cy + s * 0.4), (cx + s * 0.35, cy + s * 0.45)], color, 6, seed + 2)
    crayon(layer, [(cx - s * 0.25, cy + s * 1.35), (cx, cy + s * 0.9), (cx + s * 0.25, cy + s * 1.35)], color, 6, seed + 3)
    for side in (-1, 1):
        crayon(layer, [(cx + side * s * 0.08, cy - s * 0.05), (cx + side * s * 0.08 + 1, cy - s * 0.03)], (30, 30, 30), 6, seed + 4 + side)
    mouth = [(cx - s * 0.08, cy + s * 0.14), (cx, cy + s * 0.08), (cx + s * 0.08, cy + s * 0.14)] if sad else \
            [(cx - s * 0.08, cy + s * 0.08), (cx, cy + s * 0.14), (cx + s * 0.08, cy + s * 0.08)]
    crayon(layer, mouth, (30, 30, 30), 4, seed + 7)


def sun(layer, cx, cy, r, seed):
    scribble(layer, cx, cy, r, r, (250, 200, 30), seed)
    for k in range(10):
        a = 2 * math.pi * k / 10
        crayon(layer, [(cx + math.cos(a) * r * 1.2, cy + math.sin(a) * r * 1.2), (cx + math.cos(a) * r * 1.7, cy + math.sin(a) * r * 1.7)], (240, 170, 20), 6, seed + k)


def kid_text(layer, xy, text, color, size, seed, angle=0):
    f = font("crayon", size)
    tmp = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    td = ImageDraw.Draw(tmp)
    draw_text(td, xy, text, f, color + (225,))
    if angle:
        tmp = tmp.rotate(angle, center=xy, resample=Image.BICUBIC)
    layer.alpha_composite(tmp)


def drawing(index):
    w, h = 1024, 768
    seed = 400 + index * 37
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    if index == 0:
        sun(layer, 120, 110, 55, seed)
        kid_bear(layer, 560, 330, 220, seed + 20)
        stick_kid(layer, 250, 380, 190, (230, 60, 60), seed + 40)
        crayon(layer, [(250 + 60, 440), (330, 200)], (60, 60, 60), 3, seed + 50)
        scribble(layer, 335, 160, 45, 55, (230, 40, 60), seed + 51)
        kid_text(layer, (w / 2, 690), "HAPPY BIRTHDAY!!", (40, 90, 200), 84, seed + 60, 2)
    elif index == 1:
        tri = [(300, 150), (760, 260), (380, 640)]
        scribble(layer, 480, 360, 170, 200, (250, 200, 50), seed)
        crayon(layer, tri, (200, 120, 30), 8, seed + 1, True)
        crayon(layer, [(300, 150), (760, 260)], (170, 90, 30), 18, seed + 2)
        for k, (x, y) in enumerate([(420, 300), (540, 330), (450, 450), (600, 280)]):
            scribble(layer, x, y, 32, 30, (200, 30, 30), seed + 10 + k, 5)
        kid_text(layer, (w / 2, 700), "I LOVE PIZZA", (220, 60, 40), 90, seed + 20, -3)
    elif index == 2:
        crayon(layer, [(60, 520), (964, 520)], (120, 60, 30), 10, seed)
        scribble(layer, 512, 600, 450, 70, (140, 80, 40), seed + 1, 9)
        kid_bear(layer, 512, 270, 170, seed + 10)
        crayon(layer, circle_points(250, 300, 80, 95), (130, 70, 200), 6, seed + 20, True)
        scribble(layer, 250, 300, 80, 95, (160, 110, 230), seed + 21)
        crayon(layer, [(215, 215), (200, 90), (235, 210)], (130, 70, 200), 6, seed + 22)
        crayon(layer, [(285, 215), (300, 90), (265, 210)], (130, 70, 200), 6, seed + 23)
        crayon(layer, circle_points(780, 300, 85, 90), (230, 180, 20), 6, seed + 30, True)
        scribble(layer, 780, 300, 85, 90, (250, 215, 60), seed + 31)
        crayon(layer, [(760, 330), (820, 345), (760, 360)], (240, 120, 20), 6, seed + 32, True)
        kid_text(layer, (w / 2, 700), "BEST DAY EVER", (40, 140, 60), 84, seed + 40, 1)
    elif index == 3:
        # The one nobody took down.
        for k in range(14):
            crayon(layer, [(40 + k * 70, 40), (90 + k * 70, 730)], (25, 25, 30), 7, seed + k)
        kid_bear(layer, 640, 300, 200, seed + 30, eyes=(0, 0, 0))
        scribble(layer, 600, 290, 22, 22, (0, 0, 0), seed + 33, 4)
        scribble(layer, 680, 290, 22, 22, (0, 0, 0), seed + 34, 4)
        stick_kid(layer, 230, 380, 170, (70, 90, 200), seed + 40, sad=True)
        for k in range(3):
            crayon(layer, [(215 + k * 4, 395), (210 + k * 4, 430)], (60, 120, 230), 4, seed + 50 + k)
        kid_text(layer, (w / 2, 690), "HE IS STILL HERE", (170, 20, 20), 86, seed + 60, -2)
    elif index == 4:
        crayon(layer, [(150, 470), (870, 470)], (120, 70, 40), 10, seed)
        for x in (210, 810):
            crayon(layer, [(x, 470), (x, 640)], (120, 70, 40), 9, seed + x)
        scribble(layer, 512, 400, 120, 60, (240, 150, 190), seed + 10)
        crayon(layer, [(392, 460), (392, 360), (632, 360), (632, 460)], (200, 80, 120), 6, seed + 11)
        for k in range(5):
            crayon(layer, [(430 + k * 40, 360), (430 + k * 40, 310)], (240, 220, 80), 6, seed + 20 + k)
            scribble(layer, 430 + k * 40, 296, 8, 12, (250, 120, 30), seed + 30 + k, 4)
        for k, x in enumerate((160, 300, 720, 860)):
            stick_kid(layer, x, 230, 130, [(230, 60, 60), (60, 160, 60), (60, 90, 220), (230, 150, 30)][k], seed + 40 + k)
        kid_text(layer, (w / 2, 700), "MY PARTY", (200, 60, 140), 96, seed + 60, 2)
    else:
        sun(layer, w - 120, 110, 50, seed)
        kid_bear(layer, 380, 330, 210, seed + 10)
        stick_kid(layer, 700, 330, 200, (240, 120, 40), seed + 30)
        crayon(layer, [(470, 420), (610, 420)], (230, 60, 60), 6, seed + 40)
        kid_text(layer, (w / 2, 690), "THANK YOU FREDDY", (60, 100, 210), 80, seed + 50, -1)
    # Signed, like they all are.
    names = ["Emma, 6", "Lucas", "Mia 7", "", "Sofia, 5", "Noah"]
    if names[index]:
        kid_text(layer, (w - 150, h - 40), names[index], (90, 90, 90), 42, seed + 90)
    base = Image.new("RGB", (w, h), (250, 248, 240))
    base.paste(layer, (0, 0), layer)
    return paper(base, seed + 99, 0.3)


# ---------------------------------------------------------------- front doors

def door_logo():
    """The gold lettering across the glass of the front doors."""
    w, h = 1024, 198
    img = Image.new("RGB", (w, h), (90, 10, 12))
    d = ImageDraw.Draw(img)
    d.rectangle([6, 6, w - 7, h - 7], outline=GOLD, width=5)
    bear(d, 110, 112, 62, color=(150, 95, 50))
    draw_text(d, (w / 2 + 70, h / 2 + 4), "FREDDY FAZBEAR'S", fit_font(d, "FREDDY FAZBEAR'S", "luckiest", 760, 120), GOLD)
    return grime(img, 0.25, 305)


def closed_sign():
    """The sign hanging inside the front doors."""
    w, h = 1000, 320
    img = Image.new("RGB", (w, h), (236, 230, 214))
    d = ImageDraw.Draw(img)
    d.rectangle([10, 10, w - 11, h - 11], outline=(150, 20, 20), width=10)
    draw_text(d, (w / 2, 92), "SORRY, WE'RE", font("oswald", 70, 600), (40, 40, 40), spacing=4)
    draw_text(d, (w / 2, 210), "CLOSED", font("bebas", 150), (170, 20, 20), spacing=10)
    return grime(img, 0.3, 306)


# ---------------------------------------------------------------- outside

def chain_link():
    """Chain-link fence, tiling: galvanized wire diamonds on transparent."""
    w = h = 512
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    step = 64
    wire = (150, 155, 158, 255)
    for k in range(-w, 2 * w, step):
        d.line([k, 0, k + h, h], fill=wire, width=5)
        d.line([k, h, k + h, 0], fill=wire, width=5)
    # A little rust where the wires cross.
    rng = np.random.default_rng(310)
    for _ in range(40):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=(120, 70, 40, 200))
    return img


# ---------------------------------------------------------------- party things

def pizza_box_top():
    """The lid of a take-away box: white board, red print, a grease stain."""
    w = h = 512
    img = Image.new("RGB", (w, h), (232, 226, 210))
    d = ImageDraw.Draw(img)
    d.rectangle([14, 14, w - 15, h - 15], outline=(170, 30, 30), width=8)
    d.ellipse([w / 2 - 120, 70, w / 2 + 120, 310], outline=(170, 30, 30), width=8)
    bear(d, w / 2, 200, 78, color=(170, 30, 30))
    draw_text(d, (w / 2, 360), "FREDDY FAZBEAR'S", font("luckiest", 54), (170, 30, 30))
    draw_text(d, (w / 2, 420), "HOT  &  FRESH  PIZZA", font("oswald", 36, 600), (60, 60, 60), spacing=3)
    arr = np.asarray(img, dtype=np.float32)
    yy, xx = np.mgrid[0:h, 0:w]
    stain = np.exp(-(((xx - 380) / 70.0) ** 2 + ((yy - 120) / 55.0) ** 2)) * 0.35
    arr *= (1 - stain[..., None] * np.array([0.2, 0.45, 0.8]))
    return grime(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)), 0.25, 320)


def paper_plate():
    """A paper party plate seen from above: scalloped rim, a printed border of balloons."""
    w = h = 256
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    cx = cy = w / 2
    points = []
    for k in range(96):
        a = 2 * math.pi * k / 96
        r = 124 + (3 if k % 2 else 0)
        points.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    d.polygon(points, fill=(246, 244, 236, 255))
    d.ellipse([cx - 104, cy - 104, cx + 104, cy + 104], outline=(225, 222, 210, 255), width=3)
    colors = [(230, 60, 70), (60, 140, 230), (250, 200, 50), (90, 190, 90)]
    for k in range(12):
        a = 2 * math.pi * k / 12
        x, y = cx + 114 * math.cos(a), cy + 114 * math.sin(a)
        d.ellipse([x - 7, y - 9, x + 7, y + 9], fill=colors[k % 4] + (255,))
    return img


def gift_wrap(index):
    """Tiling wrapping paper: stripes, polka dots, stars."""
    w = h = 256
    backs = [(200, 40, 60), (40, 90, 190), (240, 200, 60)]
    fronts = [(250, 240, 230), (250, 220, 80), (200, 50, 120)]
    img = Image.new("RGB", (w, h), backs[index])
    d = ImageDraw.Draw(img)
    if index == 0:
        for k in range(-w, 2 * w, 64):
            d.polygon([(k, 0), (k + 24, 0), (k + 24 + h, h), (k + h, h)], fill=fronts[0])
    elif index == 1:
        for y in range(0, h + 1, 64):
            for x in range(0, w + 1, 64):
                for ox, oy in ((0, 0), (32, 32)):
                    d.ellipse([x + ox - 12, y + oy - 12, x + ox + 12, y + oy + 12], fill=fronts[1])
    else:
        glyph, f = icon("star", 60)
        for y in range(0, h, 64):
            for x in range(0, w, 64):
                draw_text(d, (x + 32 + (16 if (y // 64) % 2 else 0), y + 32), glyph, f, fronts[2])
    return img


def lost_and_found():
    """A torn cardboard label, written in marker."""
    w, h = 512, 256
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.polygon([(8, 20), (w - 14, 6), (w - 4, h - 26), (20, h - 8)], fill=(222, 205, 160, 255))
    draw_text(d, (w / 2, 92), "LOST &", font("marker", 78), (30, 30, 40, 255))
    draw_text(d, (w / 2, 180), "FOUND", font("marker", 78), (30, 30, 40, 255))
    return img


def window_night(index):
    """The view through a window at night: sky, a moon behind clouds, the town's rooftops with a
    few lit windows, and rain on the glass. Seen through the north wing's windows."""
    w, h = 512, 512
    rng = np.random.default_rng(330 + index)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    top = np.array([8, 12, 26], np.float32)
    low = np.array([28, 34, 52], np.float32)
    t = (yy / h)[..., None]
    sky = top * (1 - t) + low * t
    mx, my = (140 + index * 220) % 420 + 50, 110
    glow = np.exp(-(((xx - mx) ** 2 + (yy - my) ** 2) / (2 * 70.0 ** 2)))[..., None]
    sky += glow * np.array([70, 80, 100], np.float32)
    clouds = noise((w, h), 60, 331 + index)
    sky *= (0.75 + 0.35 * clouds)[..., None]
    img = Image.fromarray(np.clip(sky, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    d.ellipse([mx - 20, my - 20, mx + 20, my + 20], fill=(210, 215, 225))
    # Rooftops: a jagged band of dark buildings, some windows still lit.
    x = 0
    while x < w:
        bw = int(rng.uniform(40, 110))
        top_y = int(rng.uniform(260, 380))
        d.rectangle([x, top_y, x + bw, h], fill=(6, 7, 11))
        for wy in range(top_y + 12, h - 10, 22):
            for wx in range(x + 8, x + bw - 10, 18):
                if rng.random() < 0.12:
                    warm = rng.random() < 0.7
                    d.rectangle([wx, wy, wx + 8, wy + 11], fill=(230, 180, 90) if warm else (150, 190, 230))
        x += bw + int(rng.uniform(0, 12))
    # Rain on the glass: beads and the trails they leave.
    drops = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    dd = ImageDraw.Draw(drops)
    for _ in range(160):
        x0, y0 = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(1.5, 4.5)
        dd.ellipse([x0 - r, y0 - r, x0 + r, y0 + r], fill=(200, 210, 230, 70))
        if rng.random() < 0.35:
            dd.line([x0, y0, x0 + rng.uniform(-3, 3), y0 + rng.uniform(20, 90)], fill=(180, 190, 210, 40), width=2)
    img = Image.alpha_composite(img.convert("RGBA"), drops.filter(ImageFilter.GaussianBlur(0.8))).convert("RGB")
    return img


# ---------------------------------------------------------------- minigame screens and keys

LCD_MESSAGES = {
    "reader_swipe": ("SWIPE CARD", (110, 255, 140)),
    "reader_fast": ("TOO FAST. TRY AGAIN.", (255, 90, 70)),
    "reader_slow": ("TOO SLOW. TRY AGAIN.", (255, 90, 70)),
    "reader_bad": ("BAD READ. TRY AGAIN.", (255, 90, 70)),
    "reader_ok": ("ACCEPTED. THANK YOU.", (110, 255, 140)),
    "reader_nocard": ("NO CARD", (255, 190, 70)),
    "keypad_enter": ("ENTER 1 - 9", (110, 255, 140)),
    "keypad_wrong": ("WRONG CODE", (255, 90, 70)),
    "keypad_open": ("OPEN", (110, 255, 140)),
    "vcr_clock": ("12:00", (90, 230, 255)),
    "vcr_play": ("PLAY  0:00:12", (90, 230, 255)),
}


def lcd(text, color):
    """A small backlit LCD: dark glass, faint unlit segments, the message glowing."""
    w, h = 512, 128
    img = Image.new("RGB", (w, h), (8, 16, 10))
    d = ImageDraw.Draw(img)
    for y in range(0, h, 3):
        d.line([0, y, w, y], fill=(12, 22, 14))
    f = fit_font(d, text, "lcd", w - 40, 96)
    glowing = Image.new("RGB", (w, h), (0, 0, 0))
    gd = ImageDraw.Draw(glowing)
    draw_text(gd, (w / 2, h / 2), text, f, color)
    img = ImageChops.add(img, glowing.filter(ImageFilter.GaussianBlur(6)))
    img = ImageChops.add(img, glowing)
    return img


def keypad_key(number):
    """One key of a steel keypad: a raised square with the number stamped in."""
    w = h = 128
    img = Image.new("RGB", (w, h), (52, 54, 56))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([6, 6, w - 7, h - 7], radius=14, fill=(150, 152, 150), outline=(70, 72, 74), width=4)
    d.rounded_rectangle([14, 12, w - 15, h - 22], radius=10, fill=(176, 178, 176))
    draw_text(d, (w / 2, h / 2 - 4), str(number), font("oswald", 70, 700), (28, 28, 30))
    return grime(img, 0.25, 340 + number)


def glass_rain():
    """Rain on a window pane, tiling up and down: beads of water and the trails they leave.
    White with alpha; the game scrolls it down slowly so the water runs."""
    w, h = 512, 1024
    rng = np.random.default_rng(350)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(90):
        x = rng.uniform(0, w)
        y0 = rng.uniform(0, h)
        length = rng.uniform(80, 420)
        width = rng.uniform(1.5, 3.5)
        # A trail wanders a little as it runs down; it is drawn twice when it crosses the bottom.
        for offset in (0, -h):
            px, py = x, y0 + offset
            for k in range(int(length / 6)):
                nx = px + rng.normal(0, 0.9)
                ny = py + 6
                d.line([px, py, nx, ny], fill=(235, 240, 250, int(40 + 30 * rng.random())), width=int(width))
                px, py = nx, ny
            d.ellipse([px - width * 1.6, py - width * 2, px + width * 1.6, py + width * 2], fill=(240, 245, 255, 150))
    for _ in range(700):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(1, 4)
        for offset in (0, -h, h):
            d.ellipse([x - r, y + offset - r, x + r, y + offset + r], fill=(240, 245, 255, int(70 + 80 * rng.random())))
    return img.filter(ImageFilter.GaussianBlur(0.6))


def glass_drops():
    """Beads of rain on a windshield between two strokes of the wipers, tiling: white with alpha,
    each bead lit on its top edge and dark under it, the way a drop bends the light behind it."""
    w = h = 512
    rng = np.random.default_rng(351)
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for _ in range(1400):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(0.8, 2.2) if rng.random() < 0.85 else rng.uniform(3, 6.5)
        squash = rng.uniform(0.8, 1.15)
        for ox in (0, -w, w):
            for oy in (0, -h, h):
                cx, cy = x + ox, y + oy
                d.ellipse([cx - r, cy - r * squash, cx + r, cy + r * squash], fill=(200, 210, 225, int(55 + 50 * rng.random())))
                d.ellipse([cx - r * 0.55, cy - r * squash * 0.8, cx + r * 0.25, cy - r * squash * 0.2], fill=(255, 255, 255, 190))
                d.arc([cx - r, cy - r * squash, cx + r, cy + r * squash], 20, 160, fill=(20, 25, 35, 120), width=max(1, int(r * 0.35)))
    return img.filter(ImageFilter.GaussianBlur(0.5))


def phone_voicemail():
    """The guard's phone on the dashboard, playing a voicemail: a dark screen, the caller and a
    progress bar (the game moves nothing on it; it only glows)."""
    w, h = 512, 1024
    img = Image.new("RGB", (w, h), (10, 12, 16))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w, 64], fill=(18, 20, 26))
    draw_text(d, (60, 32), "11:52", font("lcd", 30), (200, 205, 215))
    draw_text(d, (w - 70, 32), "4G", font("lcd", 26), (200, 205, 215))
    draw_text(d, (w / 2, 190), "Voicemail", font("oswald", 52, 600), (235, 238, 245))
    d.ellipse([w / 2 - 90, 260, w / 2 + 90, 440], fill=(48, 54, 66))
    draw_text(d, (w / 2, 350), "R9", font("oswald", 74, 600), (210, 215, 225))
    draw_text(d, (w / 2, 500), "Route 9 Staffing", font("oswald", 44, 400), (230, 232, 238))
    draw_text(d, (w / 2, 556), "mobile", font("oswald", 32, 400), (140, 146, 158))
    rng = np.random.default_rng(12)
    for i in range(46):
        x = 56 + i * 8.6
        a = 8 + 46 * abs(np.sin(i * 0.5)) * rng.uniform(0.4, 1.0)
        d.rectangle([x, 690 - a, x + 4, 690 + a], fill=(90, 200, 120) if i < 19 else (70, 76, 90))
    d.rectangle([56, 790, w - 56, 796], fill=(60, 64, 76))
    d.rectangle([56, 790, 56 + (w - 112) * 0.41, 796], fill=(90, 200, 120))
    d.ellipse([56 + (w - 112) * 0.41 - 11, 782, 56 + (w - 112) * 0.41 + 11, 804], fill=(235, 238, 245))
    draw_text(d, (90, 830), "0:14", font("lcd", 28), (160, 166, 178))
    draw_text(d, (w - 90, 830), "0:35", font("lcd", 28), (160, 166, 178))
    d.ellipse([w / 2 - 54, 880, w / 2 + 54, 988], fill=(235, 238, 245))
    d.rectangle([w / 2 - 22, 908, w / 2 - 8, 960], fill=(10, 12, 16))
    d.rectangle([w / 2 + 8, 908, w / 2 + 22, 960], fill=(10, 12, 16))
    return img


def night_sky():
    """The sky behind the houses: low clouds lit orange by the town from below, a moon behind them."""
    w, h = 1024, 512
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    t = (yy / h)[..., None]
    sky = np.array([10, 13, 24], np.float32) * (1 - t) + np.array([44, 34, 36], np.float32) * t
    clouds = noise((w, h), 90, 360) * 0.6 + noise((w, h), 30, 361) * 0.4
    sky *= (0.6 + 0.6 * clouds)[..., None]
    mx, my = 300, 120
    moon = np.exp(-(((xx - mx) ** 2 + (yy - my) ** 2) / (2 * 90.0 ** 2)))[..., None]
    sky += moon * np.array([60, 66, 80], np.float32) * (0.5 + 0.5 * clouds[..., None])
    return Image.fromarray(np.clip(sky, 0, 255).astype(np.uint8))


# ---------------------------------------------------------------- wear for plain surfaces

def wear(project):
    """Grime for every plain-colored surface in the game, taken from a photo (the scanned concrete
    wall): near white with soft darker patches, so a colored box is never perfectly clean. Tiles."""
    src = Path(project) / "Assets" / "Art" / "Surfaces" / "concrete_wall" / "concrete_wall_albedo.jpg"
    if not src.exists():
        return None
    img = Image.open(src).convert("L").resize((512, 512), Image.LANCZOS)
    a = np.asarray(img, np.float32) / 255.0
    a = (a - a.mean()) / (a.std() + 1e-6)
    blotch = noise((512, 512), 120, 380) - 0.5
    value = np.clip(0.93 + a * 0.05 + blotch * 0.12, 0.7, 1.0)
    rgb = np.stack([value * 1.0, value * 0.985, value * 0.965], axis=-1)
    return Image.fromarray((rgb * 255).astype(np.uint8))


def socket_face(project):
    """The front plate of a power socket: worn steel from a photo, a round hole through it (alpha),
    an engraved collar around the hole, screws, and the instructions stamped in. 1 px = 1 mm on a
    460 x 560 mm plate; the hole (r 165 mm) is 20 mm above the middle."""
    w, h = 460, 560
    src = Path(project) / "Assets" / "Art" / "Surfaces" / "metal_worn" / "metal_worn_albedo.jpg"
    if src.exists():
        base = Image.open(src).convert("RGB").resize((w, h), Image.LANCZOS)
        base = ImageEnhance.Brightness(base.convert("L").convert("RGB")).enhance(0.9)
    else:
        base = Image.new("RGB", (w, h), (110, 112, 114))
    img = base.convert("RGBA")
    d = ImageDraw.Draw(img)
    cx, cy, r = w / 2, h / 2 - 20, 165
    # Raised border.
    d.rounded_rectangle([4, 4, w - 5, h - 5], radius=18, outline=(40, 42, 44, 255), width=6)
    d.rounded_rectangle([12, 12, w - 13, h - 13], radius=14, outline=(170, 172, 174, 255), width=2)
    # Engraved collar: a dark groove, a bright chamfer.
    for rr, col, width in ((r + 34, (28, 28, 30, 255), 8), (r + 22, (190, 192, 196, 255), 3), (r + 6, (60, 60, 64, 255), 10)):
        d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], outline=col, width=width)
    # Screws in the corners.
    for sx, sy in ((34, 34), (w - 34, 34), (34, h - 34), (w - 34, h - 34)):
        d.ellipse([sx - 10, sy - 10, sx + 10, sy + 10], fill=(150, 152, 156, 255), outline=(50, 50, 54, 255), width=2)
        d.line([sx - 7, sy - 3, sx + 7, sy + 3], fill=(60, 60, 64, 255), width=3)
    f = font("oswald", 24, 600)
    draw_text(d, (cx, 44), "POWER CELL", f, (30, 30, 32, 255), spacing=4)
    draw_text(d, (cx, h - 46), "INSERT  -  PUSH  -  TURN", font("oswald", 20, 500), (30, 30, 32, 255), spacing=3)
    # Turn arrow.
    d.arc([cx - r - 52, cy - r - 52, cx + r + 52, cy + r + 52], 300, 340, fill=(30, 30, 32, 255), width=5)
    img = grime(img.convert("RGB"), 0.25, 390).convert("RGBA")
    # The hole.
    yy, xx = np.mgrid[0:h, 0:w]
    hole = ((xx - cx) ** 2 + (yy - cy) ** 2) < r * r
    a = np.asarray(img).copy()
    a[hole, 3] = 0
    return Image.fromarray(a, "RGBA")


def stencil_tapes():
    """Spray-stenciled words on the tape safe's lid: white paint, overspray, the bridges of the stencil."""
    w, h = 768, 256
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    f = font("bebas", 118)
    draw_text(d, (w / 2, 92), "SECURITY TAPES", f, (232, 228, 216, 235), spacing=10)
    draw_text(d, (w / 2, 196), "DO NOT REMOVE FROM PREMISES", font("oswald", 34, 600), (232, 228, 216, 220), spacing=6)
    # Stencil bridges: thin gaps through the letters.
    for x in range(40, w, 37):
        d.line([x, 30, x, 150], fill=(0, 0, 0, 0), width=4)
    a = np.asarray(img).astype(np.float32)
    spray = np.asarray(img.filter(ImageFilter.GaussianBlur(5))).astype(np.float32)
    a[..., 3] = np.maximum(a[..., 3], spray[..., 3] * 0.35)
    rng = np.random.default_rng(391)
    a[..., 3] *= 0.75 + 0.25 * rng.random(a.shape[:2])
    return Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGBA")


# ---------------------------------------------------------------- the ending: a VCR and its tape

VCR_BLUE = (16, 24, 168)


def vcr_front():
    """The face of an old VHS recorder: brushed dark metal, the cassette door, the clock window,
    a row of piano-key buttons with their names, and the maker's badge."""
    w, h = 1024, 192
    rng = np.random.default_rng(370)
    base = np.full((h, w, 3), 46, np.float32)
    # Brushed: thin horizontal streaks.
    streaks = rng.normal(0, 1, (h, 1)) * 5 + rng.normal(0, 1, (h, w)) * 2.5
    base += streaks[..., None]
    img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8))
    d = ImageDraw.Draw(img)
    d.rectangle([0, 0, w - 1, h - 1], outline=(20, 20, 22), width=4)
    d.line([8, 12, w - 8, 12], fill=(78, 78, 82), width=2)
    # The cassette door (the game's flap covers it), recessed.
    d.rectangle([90, 46, 520, 130], fill=(14, 14, 16), outline=(90, 90, 94), width=3)
    d.line([96, 52, 514, 52], fill=(36, 36, 40), width=2)
    draw_text(d, (305, 152), "VIDEO CASSETTE RECORDER", font("oswald", 18, 500), (150, 150, 155), spacing=5)
    # The clock window, dark glass (the game lights it).
    d.rounded_rectangle([600, 44, 820, 104], radius=6, fill=(6, 10, 12), outline=(100, 100, 104), width=2)
    # Buttons.
    names = ["EJECT", "REW", "PLAY", "FF", "STOP", "REC"]
    for i, name in enumerate(names):
        x = 600 + i * 56
        d.rounded_rectangle([x, 120, x + 48, 146], radius=3, fill=(30, 30, 33), outline=(110, 110, 114), width=2)
        if name == "REC":
            d.ellipse([x + 19, 128, x + 29, 138], fill=(170, 30, 30))
        draw_text(d, (x + 24, 162), name, font("oswald", 14, 500), (160, 160, 165))
    draw_text(d, (930, 74), "MERIDIAN", font("bebas", 40), (190, 190, 196), spacing=3)
    draw_text(d, (930, 108), "HQ  4-HEAD", font("oswald", 16, 500), (140, 140, 146), spacing=3)
    # A power lamp.
    d.ellipse([40, 84, 52, 96], fill=(30, 120, 40))
    return grime(img, 0.05, 371)


def play_mark(d, x, y, size, fill):
    """The play triangle, drawn (the VCR font has none)."""
    d.polygon([(x, y), (x, y + size), (x + size * 0.85, y + size / 2)], fill=fill)


def osd_text(d, xy, text, size, anchor="la"):
    """White on-screen text with the hard black shadow of a VCR's character generator."""
    f = font("lcd", size)
    x, y = xy
    d.text((x + 4, y + 4), text, font=f, fill=(0, 0, 0, 230), anchor=anchor)
    d.text((x, y), text, font=f, fill=(245, 245, 245, 255), anchor=anchor)


def vhs_osd(top_left, top_right, bottom_left, bottom_right, play=False, rec=False):
    """A tape's on-screen display, transparent around the text: laid over the picture."""
    w, h = 1024, 768
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    x = 60
    if play:
        play_mark(d, 62, 52, 46, (0, 0, 0, 230))
        play_mark(d, 58, 48, 46, (245, 245, 245, 255))
        x = 128
    if rec:
        d.ellipse([62, 52, 104, 94], fill=(220, 30, 30, 255))
        x = 128
    osd_text(d, (x, 40), top_left, 64)
    osd_text(d, (w - 60, 40), top_right, 64, "ra")
    osd_text(d, (60, h - 110), bottom_left, 58)
    osd_text(d, (w - 60, h - 110), bottom_right, 58, "ra")
    return img


def vhs_blue(lines, play=True):
    """The recorder's blue screen, with lines of text in its blocky font."""
    w, h = 1024, 768
    img = Image.new("RGB", (w, h), VCR_BLUE)
    d = ImageDraw.Draw(img)
    if play:
        play_mark(d, 60, 48, 46, (245, 245, 245))
        d.text((128, 40), "PLAY", font=font("lcd", 64), fill=(245, 245, 245))
    y = 220
    for line in lines:
        if line.startswith("#"):
            # A progress bar: "#87" fills 87 % of it.
            done = int(line[1:])
            d.rectangle([150, y + 6, 874, y + 50], outline=(245, 245, 245), width=4)
            d.rectangle([160, y + 16, 160 + int(704 * done / 100), y + 40], fill=(245, 245, 245))
            y += 80
            continue
        d.text((w / 2, y), line, font=font("lcd", 60), fill=(245, 245, 245), anchor="ma")
        y += 70
    return img


def vhs_scanlines():
    """Scanlines and a dark rounded vignette, for the picture of an old TV; tiles vertically."""
    w, h = 512, 512
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    lines = ((yy % 4) < 2).astype(np.float32) * 70
    edge = np.clip(((np.abs(xx / w - 0.5) * 2) ** 6 + (np.abs(yy / h - 0.5) * 2) ** 6) * 255, 0, 255)
    alpha = np.clip(lines + edge, 0, 255)
    rgba = np.zeros((h, w, 4), np.uint8)
    rgba[..., 3] = alpha.astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def vhs_static():
    """TV snow: grey noise in fine horizontal grains; the game jumps around it every frame."""
    w, h = 512, 512
    rng = np.random.default_rng(372)
    grain = rng.random((h, w // 2))
    grain = np.repeat(grain, 2, axis=1)
    rows = rng.random((h, 1)) * 0.35
    value = np.clip(grain * 0.85 + rows, 0, 1) * 235
    return Image.fromarray(value.astype(np.uint8)).convert("RGB")


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
              "arcade": ("ARCADE", ((255, 170, 255), (190, 40, 255))),
              "tickets": ("TICKETS", ((255, 235, 150), (255, 140, 30))),
              "banquet_room": ("BANQUET ROOM", ((255, 230, 170), (230, 120, 40))),
              "veranda": ("VERANDA", ((190, 255, 210), (40, 200, 120)))}
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
    save("label_danger", label_danger())
    save("label_keycard", label_keycard())
    save("label_tape", label_tape())
    save_rgba("decal_tag_reactivate", label_reactivate())
    save_rgba("fence_chainlink", chain_link())
    save_rgba("glass_rain", glass_rain())
    save("sky_night", night_sky())
    for i in range(3):
        save(f"window_night_{i}", window_night(i))
    save("pizza_box_top", pizza_box_top())
    save_rgba("decal_paper_plate", paper_plate())
    save_rgba("decal_lost_found", lost_and_found())
    for i in range(3):
        save(f"wrap_{i}", gift_wrap(i))
    for name, (text, color) in LCD_MESSAGES.items():
        save("lcd_" + name, lcd(text, color))
    for n in range(1, 10):
        save(f"key_{n}", keypad_key(n))
    save("door_logo", door_logo())
    save("closed_sign", closed_sign())
    save("breaker_deadfront", breaker_deadfront())
    save("breaker_module", breaker_module())
    save("breaker_directory", circuit_directory())
    for i, room in enumerate(ROOMS):
        save_rgba("cctv_" + re.sub(r"[^a-z]+", "_", room.lower()).strip("_"), cctv_overlay(room, i + 1))
    for i in range(6):
        save(f"drawing_{i}", drawing(i))
    save_rgba("decal_stencil_tapes", stencil_tapes())
    face = socket_face(args.project)
    save_rgba("decal_socket_face", face)
    grime_map = wear(args.project)
    if grime_map is not None:
        save("wrap_wear", grime_map)
    # The ending: the guard plays the tape at home.
    save("vcr_front", vcr_front())
    save_rgba("vhs_osd_party", vhs_osd("TOMMY 8TH", "SP", "AUG 08 1987", "8:14 PM", play=True))
    save_rgba("glass_drops", glass_drops())
    save("screen_voicemail", phone_voicemail())
    save_rgba("vhs_osd_1987", vhs_osd("GRAND OPENING", "SP", "JUN 12 1987", "7:42 PM", play=True))
    save_rgba("vhs_osd_sim", vhs_osd("CAM 06  DINING HALL", "SIM 0001", "NIGHTSHIFT LABS", "REWARD -0.46", play=True))
    save_rgba("vhs_osd_guard4", vhs_osd("CAM 01  OFFICE", "NIGHT 5", "GUARD #4", "03:12:44 AM", rec=True))
    save_rgba("vhs_osd_guard5", vhs_osd("LIVE", "", "GUARD #5", "--:--", rec=True))
    save("vhs_blue_play", vhs_blue([]))
    save("vhs_blue_end", vhs_blue(["GUARD #5", "NIGHTS RECORDED: 5", "ADDED TO TRAINING SET", "", "FREDDY_V8  TRAINING", "#87"], play=False))
    save_rgba("glass_vhs_scanlines", vhs_scanlines())
    save("vhs_static", vhs_static())
    (out / "signs.json").write_text(json.dumps(made, indent=2, sort_keys=True) + "\n")


if __name__ == "__main__":
    main()
