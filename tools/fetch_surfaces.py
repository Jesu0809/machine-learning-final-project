"""Download the PBR surfaces of the pizzeria and pack them for Unity's URP Lit shader.

Every surface comes from a CC0 library (Poly Haven or ambientCG: free to use, no attribution
required) and is written as three textures in Assets/Art/Surfaces/<name>/:

    <name>_albedo.jpg   base color (some are recolored or get a printed pattern, see SURFACES)
    <name>_normal.jpg   OpenGL normal map
    <name>_mask.png     URP metallic map: R metallic, G ambient occlusion, A smoothness

plus Assets/Art/Surfaces/surfaces.json with the real size of each texture in meters. In Unity,
Night Shift > Import Surfaces turns them into materials (Assets/Resources/Surfaces).

Requirements: Python 3 with numpy, pillow and certifi, and internet access.
Usage (from the repository root):
    python tools/fetch_surfaces.py                  # writes into unity/
    python tools/fetch_surfaces.py --project ../ml-game
"""
import argparse
import io
import json
import math
import ssl
import urllib.request
import zipfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
RESOLUTION = "2k"
MASK_SIZE = 1024
HEADERS = {"User-Agent": "NightShift-coursework/1.0"}

try:
    import certifi  # the python.org builds for macOS ship without root certificates
    SSL_CONTEXT = ssl.create_default_context(cafile=certifi.where())
except ImportError:
    SSL_CONTEXT = ssl.create_default_context()

# name: (library, asset id, meters covered by one texture, recipe)
SURFACES = {
    # Public wing
    "checker_floor": ("acg", "Tiles012", 1.2, None),
    "checker_band": ("acg", "Tiles016", 1.2, "red_band"),
    "party_wallpaper": ("acg", "Wallpaper002C", 2.0, "wallpaper"),
    "ceiling_tiles": ("acg", "OfficeCeiling006", 4.8, None),
    "arcade_carpet": ("acg", "Carpet012", 2.0, "arcade_carpet"),
    "stage_wood": ("ph", "wood_floor_worn", None, None),
    "velvet": ("ph", "velour_velvet", 1.0, None),
    "tablecloth": ("acg", "Fabric061", 1.0, "tablecloth"),
    # Staff wing
    "staff_floor": ("ph", "linoleum_brown", None, None),
    "staff_wall": ("ph", "concrete_wall_004", None, None),
    "staff_band": ("ph", "painted_concrete", None, None),
    "kitchen_floor": ("ph", "dirty_tiles", None, None),
    "white_tiles": ("ph", "long_white_tiles", None, None),
    "restroom_floor": ("ph", "interior_tiles", None, None),
    "concrete_floor": ("ph", "garage_floor", None, None),
    "concrete_wall": ("ph", "concrete_wall_006", None, None),
    # Props
    "wood_planks": ("ph", "weathered_brown_planks", None, None),
    "metal_red": ("acg", "PaintedMetal004", 1.0, None),
    "metal_green": ("acg", "PaintedMetal006", 1.0, None),
    "metal_hazard": ("acg", "PaintedMetal016", 1.0, None),
    "metal_worn": ("ph", "blue_metal_plate", None, "grey"),
    "metal_rusty": ("ph", "rusty_metal_02", None, None),
    "metal_galvanized": ("ph", "corrugated_iron_02", None, None),
    # Outside
    "bricks": ("ph", "red_brick_03", None, None),
    "asphalt": ("ph", "asphalt_02", None, None),
    "sidewalk": ("ph", "concrete_floor_worn_001", None, None),
}


def fetch(url):
    request = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(request, timeout=120, context=SSL_CONTEXT) as response:
        return response.read()


def image(data, mode):
    return Image.open(io.BytesIO(data)).convert(mode)


def poly_haven(asset):
    """Albedo, normal, roughness, AO, metal (PIL images) and the real width in meters."""
    files = json.loads(fetch(f"https://api.polyhaven.com/files/{asset}"))
    info = json.loads(fetch(f"https://api.polyhaven.com/info/{asset}"))

    def get(key):
        return fetch(files[key][RESOLUTION]["jpg"]["url"])

    arm = np.asarray(image(get("arm"), "RGB"), dtype=np.float32) / 255.0   # R AO, G rough, B metal
    meters = (info.get("dimensions") or [2000, 2000])[0] / 1000.0
    return {
        "albedo": image(get("Diffuse"), "RGB"),
        "normal": image(get("nor_gl"), "RGB"),
        "rough": arm[..., 1],
        "ao": arm[..., 0],
        "metal": arm[..., 2],
        "meters": meters,
    }


def ambient_cg(asset):
    archive = zipfile.ZipFile(io.BytesIO(fetch(f"https://ambientcg.com/get?file={asset}_2K-JPG.zip")))

    def find(suffix):
        for name in archive.namelist():
            if name.endswith(suffix):
                return archive.read(name)
        return None

    albedo = image(find("_Color.jpg"), "RGB")
    size = albedo.size

    def gray(suffix, default):
        data = find(suffix)
        if data is None:
            return np.full((size[1], size[0]), default, dtype=np.float32)
        return np.asarray(image(data, "L").resize(size), dtype=np.float32) / 255.0

    return {
        "albedo": albedo,
        "normal": image(find("_NormalGL.jpg"), "RGB"),
        "rough": gray("_Roughness.jpg", 0.7),
        "ao": gray("_AmbientOcclusion.jpg", 1.0),
        "metal": gray("_Metalness.jpg", 0.0),
        "meters": None,
    }


# ---------------------------------------------------------------- recipes (baked into the albedo)

def luminance(img):
    return np.asarray(img.convert("L"), dtype=np.float32) / 255.0


def to_image(rgb):
    return Image.fromarray(np.clip(rgb * 255.0, 0, 255).astype(np.uint8), "RGB")


def red_band(surface):
    """Small black and white checker turned into the red and black band of the dining room walls."""
    lum = luminance(surface["albedo"])[..., None]
    dark = np.array([0.05, 0.04, 0.045])
    red = np.array([0.55, 0.06, 0.07])
    surface["albedo"] = to_image(dark + (red - dark) * np.clip((lum - 0.15) / 0.7, 0, 1))


def wallpaper(surface):
    """Stained plaster with a faded vertical stripe and diamond print, like 80s party wallpaper."""
    w, h = surface["albedo"].size
    grime = luminance(surface["albedo"])[..., None]
    x = np.arange(w)[None, :, None] / w
    y = np.arange(h)[:, None, None] / h
    stripes = 8
    phase = (x * stripes) % 1.0
    base = np.array([0.62, 0.55, 0.42])
    stripe = np.array([0.52, 0.16, 0.13])
    pattern = np.where((phase > 0.36) & (phase < 0.64), 1.0, 0.0)
    # Little diamonds along the middle of the wide stripes.
    cx = (phase - 0.5) * 2.0
    cy = ((y * stripes * 2.0) % 1.0 - 0.5) * 2.0
    diamond = np.where((np.abs(cx) + np.abs(cy) < 0.25) & (pattern == 0), 1.0, 0.0)
    color = base * (1 - pattern) + stripe * pattern
    color = color * (1 - diamond * 0.35)
    # Grime from the scanned wallpaper: darker stains and water marks.
    stained = color * (0.35 + 0.75 * grime)
    surface["albedo"] = to_image(np.clip(stained, 0, 1))


def arcade_carpet(surface):
    """The classic arcade carpet: dark purple with neon confetti, worn by the scanned carpet fibers."""
    w, h = surface["albedo"].size
    fibers = luminance(surface["albedo"])
    canvas = Image.new("RGB", (w, h), (24, 14, 42))
    draw = ImageDraw.Draw(canvas)
    rng = np.random.default_rng(1987)
    colors = [(255, 40, 160), (40, 220, 255), (255, 220, 40), (90, 255, 90), (255, 120, 30)]
    for _ in range(int(w * h / 9000)):
        cx, cy = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(w * 0.008, w * 0.022)
        color = colors[rng.integers(len(colors))]
        kind = rng.integers(3)
        for dx in (-w, 0, w):
            for dy in (-h, 0, h):
                x, y = cx + dx, cy + dy
                if kind == 0:
                    draw.ellipse([x - r, y - r, x + r, y + r], fill=color)
                elif kind == 1:
                    angle = rng.uniform(0, math.tau)
                    points = [(x + r * 1.3 * math.cos(angle + k * math.tau / 3), y + r * 1.3 * math.sin(angle + k * math.tau / 3)) for k in range(3)]
                    draw.polygon(points, fill=color)
                else:
                    draw.line([x - r * 1.5, y - r * 0.5, x - r * 0.5, y + r * 0.5, x + r * 0.5, y - r * 0.5, x + r * 1.5, y + r * 0.5],
                              fill=color, width=max(2, int(r * 0.45)))
    pattern = np.asarray(canvas.filter(ImageFilter.GaussianBlur(1.2)), dtype=np.float32) / 255.0
    surface["albedo"] = to_image(pattern * (0.45 + 0.75 * fibers[..., None]))


def tablecloth(surface):
    """White cloth with thin red checks, a little dirty."""
    w, h = surface["albedo"].size
    weave = luminance(surface["albedo"])[..., None]
    x = np.arange(w)[None, :] / w
    y = np.arange(h)[:, None] / h
    checks = 6
    band_x = ((x * checks) % 1.0) < 0.5
    band_y = ((y * checks) % 1.0) < 0.5
    red = (band_x.astype(np.float32) + band_y.astype(np.float32))[..., None] * 0.5
    white = np.array([0.88, 0.86, 0.8])
    tint = np.array([0.72, 0.12, 0.13])
    color = white * (1 - red) + tint * red
    surface["albedo"] = to_image(color * (0.6 + 0.45 * weave))


def grey(surface):
    """Painted steel without its blue: a neutral grey that the game tints per prop."""
    lum = luminance(surface["albedo"])[..., None]
    surface["albedo"] = to_image(np.repeat(np.clip(lum * 1.1, 0, 1), 3, axis=2))


RECIPES = {"red_band": red_band, "wallpaper": wallpaper, "arcade_carpet": arcade_carpet,
           "tablecloth": tablecloth, "grey": grey}


# ---------------------------------------------------------------- output

def write(surface, name, folder):
    folder.mkdir(parents=True, exist_ok=True)
    surface["albedo"].save(folder / f"{name}_albedo.jpg", quality=88)
    surface["normal"].save(folder / f"{name}_normal.jpg", quality=92)
    smooth = 1.0 - surface["rough"]
    mask = np.stack([surface["metal"], surface["ao"], np.zeros_like(smooth), smooth], axis=-1)
    Image.fromarray(np.clip(mask * 255.0, 0, 255).astype(np.uint8), "RGBA").resize((MASK_SIZE, MASK_SIZE), Image.LANCZOS) \
        .save(folder / f"{name}_mask.png", optimize=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--project", default=str(ROOT / "unity"), help="Unity project folder")
    parser.add_argument("--only", nargs="*", help="surface names to (re)download")
    args = parser.parse_args()
    out = Path(args.project) / "Assets" / "Art" / "Surfaces"
    sizes_file = out / "surfaces.json"
    sizes = json.loads(sizes_file.read_text()) if sizes_file.exists() else {}

    for name, (library, asset, meters, recipe) in SURFACES.items():
        if args.only and name not in args.only:
            continue
        surface = poly_haven(asset) if library == "ph" else ambient_cg(asset)
        if recipe:
            RECIPES[recipe](surface)
        write(surface, name, out / name)
        sizes[name] = {"meters": round(meters or surface["meters"] or 2.0, 3), "source": f"{library}:{asset}"}
        print(f"{name:18s} {library}:{asset:24s} {sizes[name]['meters']} m")

    out.mkdir(parents=True, exist_ok=True)
    sizes_file.write_text(json.dumps(sizes, indent=2, sort_keys=True) + "\n")


if __name__ == "__main__":
    main()
