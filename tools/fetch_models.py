"""Download the 3D props of the pizzeria (CC0 models from Poly Haven) for Unity's URP Lit shader.

Each model is written to Assets/Art/Models/<name>/ as the FBX geometry plus three textures packed
like the surfaces (see fetch_surfaces.py): <name>_albedo.jpg, <name>_normal.jpg and <name>_mask.png
(R metallic, G ambient occlusion, A smoothness). In Unity, Night Shift > Import Models makes a
material for each one and lists the models in GameAssets, so the level can place them by name.

Requirements: Python 3 with numpy, pillow and certifi, and internet access.
Usage (from the repository root):
    python tools/fetch_models.py                    # writes into unity/
    python tools/fetch_models.py --project ../ml-game
"""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

from fetch_surfaces import ROOT, fetch, image

RESOLUTION = "1k"
MASK_SIZE = 512

# The props, by where they go.
MODELS = [
    # Security office
    "metal_office_desk", "television_02", "SchoolChair_01", "vintage_wooden_drawer_01", "wall_clock",
    # Everywhere: watching the player
    "security_camera_01",
    # Parts & service, storage
    "tool_cart", "old_drill_press", "metal_tool_chest", "steel_frame_shelves_01", "cardboard_box_01",
    "plastic_crate_02", "Barrel_02", "metal_toolbox",
    # Kitchen
    "electric_stove", "industrial_storage_cart", "pot_enamel_01",
    # Restrooms, halls
    "WetFloorSign_01", "plunger", "multi_cleaner_5_litre", "plastic_broom",
    "metal_trash_can",
    # Public wing
    "plastic_monobloc_chair_01", "dining_table", "strawberry_chocolate_cake", "painted_wooden_bench",
    "boombox",
    # Power
    "power_box_01",
]


def model(asset, folder):
    files = json.loads(fetch(f"https://api.polyhaven.com/files/{asset}"))
    if "Diffuse" not in files:
        raise ValueError(f"{asset} has several materials; only single-material models are supported")
    folder.mkdir(parents=True, exist_ok=True)
    fbx = files["fbx"][RESOLUTION]["fbx"]["url"]
    (folder / f"{asset}.fbx").write_bytes(fetch(fbx))

    def jpg(key):
        return fetch(files[key][RESOLUTION]["jpg"]["url"])

    image(jpg("Diffuse"), "RGB").save(folder / f"{asset}_albedo.jpg", quality=88)
    image(jpg("nor_gl"), "RGB").save(folder / f"{asset}_normal.jpg", quality=92)
    arm = np.asarray(image(jpg("arm"), "RGB"), dtype=np.float32) / 255.0   # R AO, G rough, B metal
    mask = np.stack([arm[..., 2], arm[..., 0], np.zeros_like(arm[..., 0]), 1.0 - arm[..., 1]], axis=-1)
    Image.fromarray(np.clip(mask * 255.0, 0, 255).astype(np.uint8), "RGBA").resize((MASK_SIZE, MASK_SIZE), Image.LANCZOS) \
        .save(folder / f"{asset}_mask.png", optimize=True)

    info = json.loads(fetch(f"https://api.polyhaven.com/info/{asset}"))
    return [round(v / 1000.0, 3) for v in info.get("dimensions", [0, 0, 0])]


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--project", default=str(ROOT / "unity"), help="Unity project folder")
    parser.add_argument("--only", nargs="*", help="model names to (re)download")
    args = parser.parse_args()
    out = Path(args.project) / "Assets" / "Art" / "Models"
    sizes_file = out / "models.json"
    sizes = json.loads(sizes_file.read_text()) if sizes_file.exists() else {}
    for asset in MODELS:
        if args.only and asset not in args.only:
            continue
        sizes[asset] = {"meters": model(asset, out / asset), "source": f"polyhaven:{asset}"}
        print(f"{asset:28s} {sizes[asset]['meters']}")
        sizes_file.write_text(json.dumps(sizes, indent=2, sort_keys=True) + "\n")


if __name__ == "__main__":
    main()
