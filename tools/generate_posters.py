"""Make the circus's wall posters from real lithographs of the 1890s and 1900s.

The art comes from public-domain circus posters on Wikimedia Commons (scans from the Library of
Congress and the State Library of New South Wales). Only the pictures are used: each one is cropped
so no real show's name remains, then set under new type for Bellwether's Winter Circus, aged, and
in places defaced by somebody who stayed behind. The same tool draws the scrawls on the walls.

Requirements: Python with numpy, pillow and certifi, and internet access.
Usage (from the repository root):
    python tools/generate_posters.py --project ../ml-game
"""
import argparse
import io
import urllib.parse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

from fetch_surfaces import fetch
from generate_signs import ROOT, draw_text, fit_font, font, noise

WIDTH, HEIGHT = 768, 1024
INK = (34, 22, 16)
CREAM = (232, 214, 170)
RED = (160, 24, 22)
GREASE = (150, 12, 14)

# Name on Commons, width to download, crop as fractions (left, top, right, bottom).
ART = {
    "crest": ("Clown detail, The marvelous foot-ball dogs, poster for Barnum & Bailey, 1900 (cropped).jpg", None, (0, 0, 1, 1)),
    "hush": ("Clown art detail, Barnum & Bailey clowns and geese (cropped).jpg", None, (0, 0.13, 1, 1)),
    "dance": ("Cock fighting and clown detail from Barnum & Bailey poster (cropped).jpg", None, (0, 0, 1, 1)),
    "five": ("5 circus clowns LCCN2002718921.jpg", None, (0.035, 0.07, 0.965, 0.93)),
    "fools": ("Twenty funny felt-crowned fools, poster for Forepaugh & Sells Brothers, 1899.jpg", None, (0.03, 0.19, 0.97, 0.9)),
    "smile": ("The famous Jandaschewsky family of clowns and entertainers (5463615958).jpg", 2400, (0.5, 0.145, 0.93, 0.375)),
    "frown": ("The famous Jandaschewsky family of clowns and entertainers (5463615958).jpg", 2400, (0.52, 0.66, 0.96, 0.88)),
}


def art(name):
    title, width, (left, top, right, bottom) = ART[name]
    url = "https://commons.wikimedia.org/wiki/Special:FilePath/" + urllib.parse.quote(title.replace(" ", "_"))
    if width:
        url += f"?width={width}"
    image = Image.open(io.BytesIO(fetch(url))).convert("RGB")
    w, h = image.size
    return image.crop((int(left * w), int(top * h), int(right * w), int(bottom * h)))


def cover(image, box_w, box_h, focus=(0.5, 0.5)):
    """Scale to fill the box and crop what is left over, keeping the focus point in view."""
    scale = max(box_w / image.width, box_h / image.height)
    image = image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)
    x = int(np.clip(focus[0] * image.width - box_w / 2, 0, image.width - box_w))
    y = int(np.clip(focus[1] * image.height - box_h / 2, 0, image.height - box_h))
    return image.crop((x, y, x + box_w, y + box_h))


def layout(picture, top, title, under, title_font="rye", picture_box=None):
    """A bill: small banner, the picture, the title in big wood type and a line under it."""
    img = Image.new("RGB", (WIDTH, HEIGHT), CREAM)
    d = ImageDraw.Draw(img)
    margin = 26
    d.rectangle([margin, margin, WIDTH - margin, 104], fill=RED)
    draw_text(d, (WIDTH / 2, 66), top, fit_font(d, top, "rye", WIDTH - margin * 2 - 40, 60), CREAM)
    box_h = picture_box or 690
    img.paste(cover(picture, WIDTH - margin * 2, box_h), (margin, 112))
    y = 112 + box_h
    d.rectangle([margin, y, WIDTH - margin, y + 6], fill=INK)
    room = HEIGHT - y - margin - 6
    draw_text(d, (WIDTH / 2, y + 6 + room * 0.40), title, fit_font(d, title, title_font, WIDTH - margin * 2 - 30, room * 0.58), RED)
    draw_text(d, (WIDTH / 2, y + 6 + room * 0.82), under, fit_font(d, under, "rye", WIDTH - margin * 2 - 60, room * 0.24), INK)
    return img


def brush(d, points, width, color, rng, wobble=4.0):
    """A hand-drawn stroke: a thick line that wanders and changes its weight."""
    for (x0, y0), (x1, y1) in zip(points, points[1:]):
        steps = max(2, int(np.hypot(x1 - x0, y1 - y0) / 6))
        for t in np.linspace(0, 1, steps):
            x = x0 + (x1 - x0) * t + rng.normal(0, wobble)
            y = y0 + (y1 - y0) * t + rng.normal(0, wobble)
            r = width * rng.uniform(0.55, 1.05) / 2
            d.ellipse([x - r, y - r, x + r, y + r], fill=color)


def cross_out(img, cx, cy, size, seed, color=GREASE):
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(seed)
    brush(d, [(cx - size, cy - size), (cx + size, cy + size)], 15, color, rng, 2.5)
    brush(d, [(cx + size, cy - size), (cx - size, cy + size)], 15, color, rng, 2.5)


def aged(img, seed, dirt=1.0):
    """Forty years on a damp wall: faded, yellowed, folded, scratched, darker toward the edges."""
    w, h = img.size
    rng = np.random.default_rng(seed)
    a = np.asarray(img, dtype=np.float32) / 255.0
    grey = a.mean(axis=2, keepdims=True)
    a = a + (grey - a) * 0.22
    a = a * np.array([1.0, 0.9, 0.72]) * 0.82
    # Damp in soft clouds, thin enough that the picture still shows through.
    blotches = np.asarray(Image.fromarray((noise((w, h), 110, seed) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(26)), dtype=np.float32) / 255.0
    blotches = (blotches - blotches.min()) / max(float(blotches.max() - blotches.min()), 0.01)
    a *= (1.0 - 0.42 * dirt * np.clip(blotches * 1.5 - 0.55, 0, 1))[..., None]
    # Water ran down the wall behind it: faint vertical streaks.
    streaks = np.interp(np.arange(w), np.linspace(0, w, 60), rng.random(60)).astype(np.float32)
    a *= (1.0 - 0.2 * dirt * np.clip(streaks * 2.0 - 1.0, 0, 1))[None, :, None]
    yy, xx = np.mgrid[0:h, 0:w]
    edge = np.minimum.reduce([xx, w - 1 - xx, yy, h - 1 - yy]).astype(np.float32)
    a *= (0.52 + 0.48 * np.clip(edge / 90.0, 0, 1))[..., None]
    a = np.clip(a + (rng.random((h, w, 1)) - 0.5) * 0.055, 0, 1)
    out = Image.fromarray((a * 255).astype(np.uint8))
    d = ImageDraw.Draw(out, "RGBA")
    for x in (w // 2 + rng.integers(-5, 5),):
        d.line([x, 0, x, h], fill=(20, 14, 10, 90), width=2)
        d.line([x + 2, 0, x + 2, h], fill=(255, 240, 210, 46), width=1)
    y = h // 2 + rng.integers(-5, 5)
    d.line([0, y, w, y], fill=(20, 14, 10, 90), width=2)
    d.line([0, y + 2, w, y + 2], fill=(255, 240, 210, 46), width=1)
    for _ in range(46):
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        length, angle = rng.uniform(10, 120), rng.uniform(0, np.pi)
        d.line([x, y, x + length * np.cos(angle), y + length * np.sin(angle)], fill=(235, 220, 190, int(rng.uniform(16, 60))), width=1)
    # The top corner torn away, with the wall behind it.
    tear = [(w, 0), (w - rng.uniform(60, 110), 0)]
    x, y = tear[1]
    for _ in range(6):
        x, y = x + rng.uniform(0, 22), y + rng.uniform(2, 15)
        tear.append((x, y))
    tear.append((w, y))
    d.polygon(tear, fill=(20, 15, 12, 255))
    d.line(tear[1:-1], fill=(214, 200, 168, 255), width=3)
    return out


def posters():
    five = layout(art("five"), "BELLWETHER'S WINTER CIRCUS", "FIVE MERRY FOOLS", "FOUR OF THEM HAVE RETIRED", picture_box=566)
    # The faces in the picture, from left to right; the second one is still working.
    picture_w, picture_h = WIDTH - 52, 566
    for index, (fx, fy, size) in enumerate([(0.12, 0.33, 34), (0.48, 0.47, 42), (0.72, 0.31, 34), (0.86, 0.33, 40)]):
        cross_out(five, 26 + fx * picture_w, 112 + fy * picture_h, size, 20 + index)

    smile = layout(art("smile"), "BELLWETHER'S WINTER CIRCUS", "SMILE", "HE CAN TELL WHEN YOU DON'T")
    rng = np.random.default_rng(31)
    d = ImageDraw.Draw(smile)
    mouth = [(330, 545), (390, 610), (480, 630), (570, 600), (640, 520)]
    brush(d, mouth, 13, GREASE, rng, 1.8)

    frown = layout(art("frown"), "THE MANAGEMENT REGRETS", "NO REFUNDS", "AND NO EXITS BEFORE THE FINALE")
    return {
        "poster_0": aged(layout(art("crest"), "BELLWETHER'S WINTER CIRCUS", "POCKETS", "THE CLOWN WHO NEVER WENT HOME", picture_box=700), 41, 0.8),
        "poster_1": aged(five, 42),
        "poster_3": aged(smile, 43),
        "poster_4": aged(layout(art("hush"), "BELLWETHER'S WINTER CIRCUS", "HUSH", "THE SHOW HAS ALREADY STARTED"), 44),
        "poster_5": aged(frown, 45),
        "poster_6": aged(layout(art("dance"), "EVERY NIGHT AT TWELVE", "HE DANCES", "UNTIL YOU DO"), 46),
        "poster_7": aged(layout(art("fools"), "THE LAST SHOW OF THE SEASON", "GRAND FINALE", "EVERYONE PERFORMS", picture_box=566), 47),
    }


def scrawl(text, seed, size=(1024, 384), color=GREASE, name="marker"):
    """Words smeared on a wall with greasepaint, with runs under the letters."""
    img = Image.new("L", size, 0)
    d = ImageDraw.Draw(img)
    f = fit_font(d, text, name, size[0] - 80, size[1] - 150)
    draw_text(d, (size[0] / 2, size[1] * 0.42), text, f, 255)
    rng = np.random.default_rng(seed)
    letters = np.asarray(img)
    columns = np.nonzero(letters.max(axis=0))[0]
    for x in rng.choice(columns, min(11, len(columns)), replace=False):
        rows = np.nonzero(letters[:, x])[0]
        length = rng.uniform(30, 130)
        d.line([x, rows.max() - 4, x + rng.uniform(-2, 2), rows.max() + length], fill=210, width=int(rng.integers(3, 7)))
    mask = np.asarray(img.filter(ImageFilter.GaussianBlur(1.1)), dtype=np.float32) / 255.0
    grit = noise(size, 5, seed) * 0.55 + 0.6
    alpha = np.clip(mask * grit * 1.25, 0, 1)
    rgba = np.zeros((size[1], size[0], 4), dtype=np.uint8)
    rgba[..., :3] = color
    rgba[..., 3] = (alpha * 235).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def grin(seed):
    """A smile as wide as a wall, drawn with one arm in greasepaint."""
    w, h = 1536, 640
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(seed)
    upper = [(70 + (w - 140) * t, 190 + 150 * np.sin(np.pi * t) ** 0.9) for t in np.linspace(0, 1, 40)]
    lower = [(70 + (w - 140) * t, 190 + 360 * np.sin(np.pi * t) ** 0.8) for t in np.linspace(1, 0, 40)]
    d.polygon(upper + lower, fill=255)
    for k in range(1, 12):
        t = k / 12
        x = 70 + (w - 140) * t
        top = 190 + 150 * np.sin(np.pi * t) ** 0.9
        bottom = 190 + 360 * np.sin(np.pi * t) ** 0.8
        d.polygon([(x - 38, top - 4), (x + 38, top - 4), (x + rng.uniform(-8, 8), top + (bottom - top) * 0.62)], fill=0)
        d.polygon([(x - 34, bottom + 4), (x + 34, bottom + 4), (x + rng.uniform(-8, 8), bottom - (bottom - top) * 0.42)], fill=0)
    for _ in range(26):
        t = rng.uniform(0.03, 0.97)
        x = 70 + (w - 140) * t
        y = 190 + 360 * np.sin(np.pi * t) ** 0.8
        d.line([x, y - 6, x + rng.uniform(-3, 3), y + rng.uniform(30, 120)], fill=225, width=int(rng.integers(3, 9)))
    mask = np.asarray(img.filter(ImageFilter.GaussianBlur(2.2)), dtype=np.float32) / 255.0
    grit = noise((w, h), 7, seed) * 0.6 + 0.55
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., :3] = GREASE
    rgba[..., 3] = (np.clip(mask * grit * 1.3, 0, 1) * 235).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def shoe_prints(seed):
    """A line of huge greasepaint footprints, walking up the texture."""
    w, h = 256, 1024
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)
    rng = np.random.default_rng(seed)
    for i in range(5):
        cx = 84 if i % 2 else 172
        cy = h - 110 - i * 200
        d.ellipse([cx - 42, cy - 88, cx + 42, cy + 24], fill=255)
        d.ellipse([cx - 25, cy + 34, cx + 25, cy + 92], fill=255)
    mask = np.asarray(img.filter(ImageFilter.GaussianBlur(1.6)), dtype=np.float32) / 255.0
    grit = np.clip(noise((w, h), 6, seed) * 1.8 - 0.25, 0, 1)
    fade = np.linspace(0.45, 1.0, h)[:, None] * np.ones((h, w))
    rgba = np.zeros((h, w, 4), dtype=np.uint8)
    rgba[..., :3] = (110, 12, 14)
    rgba[..., 3] = (np.clip(mask * grit * fade, 0, 1) * 215).astype(np.uint8)
    return Image.fromarray(rgba, "RGBA")


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--project", default=str(ROOT / "unity"), help="Unity project folder")
    args = parser.parse_args()
    out = Path(args.project) / "Assets" / "Art" / "Signs"
    for name, image in posters().items():
        image.convert("RGB").save(out / f"{name}.png", optimize=True)
        print(name)
    words = {"decal_scrawl_0": "HA HA HA HA", "decal_scrawl_1": "SMILE", "decal_scrawl_2": "LOOK UP",
             "decal_scrawl_3": "HE KNOWS", "decal_scrawl_4": "NOT THIS ONE", "decal_scrawl_5": "STAY FOR THE FINALE"}
    for index, (name, text) in enumerate(words.items()):
        scrawl(text, 200 + index).save(out / f"{name}.png", optimize=True)
        print(name)
    grin(220).save(out / "decal_grin.png", optimize=True)
    shoe_prints(221).save(out / "decal_shoeprints.png", optimize=True)


if __name__ == "__main__":
    main()
