"""Prepare the Sketchfab models (CC BY, see CREDITS.md) for the game.

The models come as Sketchfab glTF binaries: deep node trees, centimeters or odd scales, the lever's
handle welded into one mesh with its base. This rewrites each one as a clean .glb that Unity imports
with glTFast (com.unity.cloud.gltfast): meters, +Y up, the front facing +Z, standing on y = 0, and
every moving part as its own node whose origin is its pivot:

    car_wagon      the guard's 1984 station wagon. Wheels and steering wheel pivot on their centers.
    car_wreck      a rusted sedan for the back lot (its ground plane is dropped).
    exit_lever     the exit panel's lever, laid flat on the panel face: "Base" and "Handle" (hub axis = x).
    knife_switch   the breaker box's main switch, closed: "Base" and "Blade" (hinge axis = x).
    fp_hands       first-person arms, rigged; copied as they are.

Textures are scaled down (the game is also built for the web) and JPEG-encoded unless they need alpha.

Requirements: Python 3 with numpy and pillow.
Usage (from the repository root):
    python tools/prepare_models.py --source ~/Downloads/nightshift-models --project ../ml-game
"""
import argparse
import io
import json
import shutil
import struct
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
TARGET = "Assets/Art/Sketchfab"
MAX_TEXTURE = 1024

COMPONENTS = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


class Glb:
    """A glTF binary: its JSON and its one buffer."""

    def __init__(self, path):
        data = Path(path).read_bytes()
        json_length = struct.unpack("<I", data[12:16])[0]
        self.json = json.loads(data[20:20 + json_length])
        bin_start = 20 + json_length
        bin_length = struct.unpack("<I", data[bin_start:bin_start + 4])[0]
        self.bin = data[bin_start + 8:bin_start + 8 + bin_length]

    def view_bytes(self, index):
        view = self.json["bufferViews"][index]
        start = view.get("byteOffset", 0)
        return self.bin[start:start + view["byteLength"]]

    def read(self, index):
        accessor = self.json["accessors"][index]
        view = self.json["bufferViews"][accessor["bufferView"]]
        dtype = COMPONENTS[accessor["componentType"]]
        width = WIDTH[accessor["type"]]
        item = np.dtype(dtype).itemsize * width
        stride = view.get("byteStride", 0) or item
        start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        count = accessor["count"]
        raw = np.frombuffer(self.bin, dtype=np.uint8, count=stride * (count - 1) + item, offset=start)
        rows = np.lib.stride_tricks.as_strided(raw, shape=(count, item), strides=(stride, 1))
        return rows.copy().view(dtype).reshape(count, width)

    def local_matrix(self, node):
        if "matrix" in node:
            return np.array(node["matrix"], dtype=np.float64).reshape(4, 4).T
        x, y, z, w = node.get("rotation", [0, 0, 0, 1])
        rotation = np.array([
            [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])
        matrix = np.eye(4)
        matrix[:3, :3] = rotation @ np.diag(node.get("scale", [1, 1, 1]))
        matrix[:3, 3] = node.get("translation", [0, 0, 0])
        return matrix

    def mesh_nodes(self):
        """Every node with a mesh, with its world matrix: [(name, mesh index, matrix)]."""
        found = []

        def walk(index, parent, parent_name):
            node = self.json["nodes"][index]
            world = parent @ self.local_matrix(node)
            name = node.get("name", "")
            # Sketchfab puts the mesh on a child called Object_N; the parent has the real name.
            if not name or name.startswith("Object_"):
                name = parent_name
            if "mesh" in node:
                found.append((name, node["mesh"], world))
            for child in node.get("children", []):
                walk(child, world, name)

        for root in self.json["scenes"][self.json.get("scene", 0)]["nodes"]:
            walk(root, np.eye(4), "mesh")
        return found


class Part:
    """Triangles of one primitive that become one node: vertices already in the model's space."""

    def __init__(self, name, material, attributes, indices):
        self.name = name
        self.material = material
        self.attributes = attributes   # name -> (array, componentType, normalized)
        self.indices = indices
        self.pivot = np.zeros(3)
        self.rotation = np.eye(3)      # columns: the node's axes in model space

    @property
    def positions(self):
        return self.attributes["POSITION"][0]


def parts_of(glb, skip=()):
    """Every primitive of every mesh node, flattened into the model's space (glTF axes, source units)."""
    parts = []
    for name, mesh_index, world in glb.mesh_nodes():
        if any(s in name for s in skip):
            continue
        linear = world[:3, :3]
        normal_matrix = np.linalg.inv(linear).T
        for primitive in glb.json["meshes"][mesh_index]["primitives"]:
            attributes = {}
            for key, accessor_index in primitive["attributes"].items():
                accessor = glb.json["accessors"][accessor_index]
                values = glb.read(accessor_index).astype(np.float64 if accessor["componentType"] == 5126 else COMPONENTS[accessor["componentType"]])
                if key == "POSITION":
                    values = values @ linear.T + world[:3, 3]
                elif key == "NORMAL":
                    values = values @ normal_matrix.T
                    values /= np.linalg.norm(values, axis=1, keepdims=True) + 1e-12
                elif key == "TANGENT":
                    xyz = values[:, :3] @ linear.T
                    xyz /= np.linalg.norm(xyz, axis=1, keepdims=True) + 1e-12
                    values = np.c_[xyz, values[:, 3]]
                attributes[key] = (values, accessor["componentType"], accessor.get("normalized", False))
            indices = glb.read(primitive["indices"]).reshape(-1).astype(np.int64) if "indices" in primitive else np.arange(len(attributes["POSITION"][0]))
            parts.append(Part(clean(name), primitive.get("material"), attributes, indices))
    return parts


def clean(name):
    """Sketchfab node names carry the material: 'Fairheaven_LT80_WheelStock_FL002_RB1c_Tire_1k_0' -> the object."""
    for marker in ("_UCB_", "_Fairheaven_LT80_Bodymat", "_RB1c_", "_Numberplates", "_Carbadges", "_lver", "_Material"):
        if marker in name:
            name = name[:name.index(marker)]
    return name


def transform(parts, matrix):
    """Applies a rotation and uniform scale (3x3) to every part."""
    for part in parts:
        for key in ("POSITION", "NORMAL", "TANGENT"):
            if key not in part.attributes:
                continue
            values, component, normalized = part.attributes[key]
            if key == "POSITION":
                values = values @ matrix.T
            else:
                rotation = matrix / np.cbrt(np.linalg.det(matrix))
                xyz = values[:, :3] @ rotation.T
                values = np.c_[xyz, values[:, 3]] if key == "TANGENT" else xyz
            part.attributes[key] = (values, component, normalized)


def translate(parts, offset):
    for part in parts:
        values, component, normalized = part.attributes["POSITION"]
        part.attributes["POSITION"] = (values + offset, component, normalized)


def bounds(parts):
    points = np.vstack([p.positions for p in parts])
    return points.min(0), points.max(0)


def islands(part):
    """Labels each triangle of a part by connected piece (vertices welded by position)."""
    triangles = part.indices.reshape(-1, 3)
    _, welded = np.unique(np.round(part.positions, 5), axis=0, return_inverse=True)
    welded = welded.reshape(-1)
    parent = np.arange(welded.max() + 1)

    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]
            x = parent[x]
        return x

    for a, b, c in welded[triangles]:
        root = find(a)
        parent[find(b)] = root
        parent[find(c)] = root
    roots = np.array([find(welded[t[0]]) for t in triangles])
    _, labels = np.unique(roots, return_inverse=True)
    return labels


def split(part, keep, name):
    """A new part made of the triangles where keep is True (vertices compacted)."""
    triangles = part.indices.reshape(-1, 3)[keep]
    used, remapped = np.unique(triangles.reshape(-1), return_inverse=True)
    attributes = {k: (v[used], c, n) for k, (v, c, n) in part.attributes.items()}
    return Part(name, part.material, attributes, remapped)


def set_pivot(part, pivot, axes=None):
    """Moves the part's origin to pivot (model space); axes (3x3 columns) turn its local frame."""
    part.pivot = np.array(pivot, dtype=np.float64)
    part.rotation = np.eye(3) if axes is None else np.array(axes, dtype=np.float64)
    inverse = part.rotation.T
    for key in ("POSITION", "NORMAL", "TANGENT"):
        if key not in part.attributes:
            continue
        values, component, normalized = part.attributes[key]
        if key == "POSITION":
            values = (values - part.pivot) @ inverse.T
        else:
            xyz = values[:, :3] @ inverse.T
            values = np.c_[xyz, values[:, 3]] if key == "TANGENT" else xyz
        part.attributes[key] = (values, component, normalized)


def yaw(degrees):
    a = np.radians(degrees)
    return np.array([[np.cos(a), 0, np.sin(a)], [0, 1, 0], [-np.sin(a), 0, np.cos(a)]])


def quaternion(matrix):
    """(x, y, z, w) of a rotation matrix."""
    m = matrix
    w = np.sqrt(max(0.0, 1 + m[0, 0] + m[1, 1] + m[2, 2])) / 2
    x = np.copysign(np.sqrt(max(0.0, 1 + m[0, 0] - m[1, 1] - m[2, 2])) / 2, m[2, 1] - m[1, 2])
    y = np.copysign(np.sqrt(max(0.0, 1 - m[0, 0] + m[1, 1] - m[2, 2])) / 2, m[0, 2] - m[2, 0])
    z = np.copysign(np.sqrt(max(0.0, 1 - m[0, 0] - m[1, 1] + m[2, 2])) / 2, m[1, 0] - m[0, 1])
    return [float(x), float(y), float(z), float(w)]


class Writer:
    """Builds a new glTF binary from parts, keeping the source's materials and (shrunk) textures."""

    def __init__(self, source, max_texture):
        self.source = source
        self.max_texture = max_texture
        self.bin = bytearray()
        self.json = {"asset": {"version": "2.0", "generator": "Night Shift prepare_models.py"},
                     "scene": 0, "scenes": [{"nodes": [0]}], "nodes": [], "meshes": [], "accessors": [], "bufferViews": []}

    def view(self, data, target=None):
        while len(self.bin) % 4:
            self.bin.append(0)
        view = {"buffer": 0, "byteOffset": len(self.bin), "byteLength": len(data)}
        if target:
            view["target"] = target
        self.bin.extend(data)
        self.json["bufferViews"].append(view)
        return len(self.json["bufferViews"]) - 1

    def accessor(self, values, component, normalized=False, target=34962, kind=None):
        values = np.ascontiguousarray(values.astype(COMPONENTS[component]))
        width = 1 if values.ndim == 1 else values.shape[1]
        accessor = {"bufferView": self.view(values.tobytes(), target), "componentType": component,
                    "count": len(values), "type": kind or {1: "SCALAR", 2: "VEC2", 3: "VEC3", 4: "VEC4"}[width]}
        if normalized:
            accessor["normalized"] = True
        if component == 5126 and width == 3:
            accessor["min"] = values.min(0).tolist()
            accessor["max"] = values.max(0).tolist()
        self.json["accessors"].append(accessor)
        return len(self.json["accessors"]) - 1

    def add(self, parts, root_name):
        """One node per part under a root node (node 0)."""
        self.json["nodes"].append({"name": root_name, "children": list(range(1, len(parts) + 1))})
        for part in parts:
            attributes = {key: self.accessor(values, component, normalized) for key, (values, component, normalized) in part.attributes.items()}
            component = 5123 if part.indices.max() < 65536 else 5125
            primitive = {"attributes": attributes, "indices": self.accessor(part.indices, component, target=34963, kind="SCALAR"), "mode": 4}
            if part.material is not None:
                primitive["material"] = part.material
            self.json["meshes"].append({"name": part.name, "primitives": [primitive]})
            node = {"name": part.name, "mesh": len(self.json["meshes"]) - 1}
            if np.any(part.pivot):
                node["translation"] = part.pivot.tolist()
            if not np.allclose(part.rotation, np.eye(3)):
                node["rotation"] = quaternion(part.rotation)
            self.json["nodes"].append(node)

    def copy_materials(self):
        source = self.source.json
        for key in ("materials", "textures", "samplers"):
            if key in source:
                self.json[key] = json.loads(json.dumps(source[key]))
        if "extensionsUsed" in source:
            self.json["extensionsUsed"] = list(source["extensionsUsed"])
        images = []
        for image in source.get("images", []):
            data = self.source.view_bytes(image["bufferView"])
            picture = Image.open(io.BytesIO(data))
            picture.load()
            if max(picture.size) > self.max_texture:
                scale = self.max_texture / max(picture.size)
                picture = picture.resize((max(1, round(picture.width * scale)), max(1, round(picture.height * scale))), Image.LANCZOS)
            out = io.BytesIO()
            has_alpha = picture.mode in ("RGBA", "LA") and np.asarray(picture.getchannel("A")).min() < 250
            if has_alpha:
                picture.save(out, "PNG", optimize=True)
                mime = "image/png"
            else:
                picture.convert("RGB").save(out, "JPEG", quality=88)
                mime = "image/jpeg"
            images.append({"bufferView": self.view(out.getvalue()), "mimeType": mime})
        if images:
            self.json["images"] = images

    def save(self, path):
        while len(self.bin) % 4:
            self.bin.append(0)
        self.json["buffers"] = [{"byteLength": len(self.bin)}]
        text = json.dumps(self.json, separators=(",", ":")).encode()
        text += b" " * (-len(text) % 4)
        with open(path, "wb") as f:
            f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(text) + 8 + len(self.bin)))
            f.write(struct.pack("<II", len(text), 0x4E4F534A) + text)
            f.write(struct.pack("<II", len(self.bin), 0x004E4942) + bytes(self.bin))


def write(source, parts, root_name, path, max_texture=MAX_TEXTURE):
    writer = Writer(source, max_texture)
    writer.copy_materials()
    writer.add(parts, root_name)
    writer.save(path)
    low, high = bounds_after(parts)
    print(f"{path.name}: {len(parts)} parts, {path.stat().st_size / 1e6:.1f} MB, size {np.round(high - low, 2)} m")


def bounds_after(parts):
    points = np.vstack([p.positions @ p.rotation.T + p.pivot for p in parts])
    return points.min(0), points.max(0)


def ground(parts, center_on=None):
    """Stands the model on y = 0, centered in x and z (on center_on's bounds if given)."""
    low, high = bounds(parts)
    reference = bounds(center_on) if center_on else (low, high)
    middle = (reference[0] + reference[1]) / 2
    translate(parts, np.array([-middle[0], -low[1], -middle[2]]))


def car_wagon(source):
    glb = Glb(source)
    parts = parts_of(glb)
    # Turn it so the front (the headlights) faces +z.
    lights = [p for p in parts if "Headlights" in p.name]
    low, high = bounds(parts)
    front = np.mean(np.vstack([p.positions for p in lights]), 0) - (low + high) / 2
    angle = np.degrees(np.arctan2(front[0], front[2]))
    transform(parts, yaw(-angle))
    wheels = [p for p in parts if "Wheel" in p.name and "Steering" not in p.name]
    ground(parts, center_on=wheels)
    for wheel in wheels:
        low, high = bounds([wheel])
        set_pivot(wheel, (low + high) / 2)
    steering = next(p for p in parts if "Steering" in p.name)
    points = steering.positions
    center = points.mean(0)
    # The column's axis: the direction the rim varies least along.
    _, _, axes = np.linalg.svd(points - center, full_matrices=False)
    column = axes[2] if axes[2][2] < 0 else -axes[2]          # pointing back, at the driver
    side = np.cross([0.0, 1.0, 0.0], column)
    side /= np.linalg.norm(side)
    up = np.cross(column, side)
    set_pivot(steering, center, np.c_[side, up, column])
    seat_side = "left (Unity -x)" if center[0] > 0 else "right (Unity +x)"
    print(f"  wagon: turned {angle:.0f} degrees; steering wheel at {np.round(center, 2)} (glTF), driver on the {seat_side}")
    return glb, parts


def car_wreck(source):
    glb = Glb(source)
    parts = parts_of(glb, skip=("Plane001",))
    low, high = bounds(parts)
    length = (high - low)[[0, 2]].max()
    transform(parts, np.eye(3) * (4.9 / length))
    ground(parts)
    return glb, parts


def exit_lever(source):
    glb = Glb(source)
    (part,) = parts_of(glb, skip=("Lamp", "Camera"))
    labels = islands(part)
    # The base is the biggest piece (the plate); the handle has more triangles (its round knob).
    sizes = [np.prod(np.ptp(part.positions[part.indices.reshape(-1, 3)[labels == label].reshape(-1)], 0) + 1e-6) for label in range(labels.max() + 1)]
    plate = int(np.argmax(sizes))
    base = split(part, labels == plate, "Base")
    handle = split(part, labels != plate, "Handle")
    parts = [base, handle]
    # Lying flat in the source (plate in x-z, its long side along z): stand the plate up on the
    # panel face, long side vertical, front toward +z.
    stand = np.array([[1, 0, 0], [0, 0, -1], [0, 1, 0]], dtype=np.float64)   # y -> z, z -> -y
    transform(parts, stand)
    low, high = bounds([base])
    transform(parts, np.eye(3) * (0.42 / (high - low)[1]))
    low, high = bounds([base])
    translate(parts, -np.array([(low[0] + high[0]) / 2, (low[1] + high[1]) / 2, low[2]]))
    # The knob is the far end from the plate; the hub, a short axle across the handle's foot, is the
    # far end from the knob: its pivot is the middle of that end's box (the axle's axis is x).
    points = handle.positions
    knob_cap = points[points[:, 2] > np.percentile(points[:, 2], 90)]
    knob = (knob_cap.min(0) + knob_cap.max(0)) / 2
    distance = np.linalg.norm(points - knob, axis=1)
    foot = points[distance > np.percentile(distance, 85)]
    pivot = np.array([np.median(points[:, 0]), (foot[:, 1].min() + foot[:, 1].max()) / 2, (foot[:, 2].min() + foot[:, 2].max()) / 2])
    set_pivot(handle, pivot)
    tip = knob - pivot
    print(f"  lever: hub at {np.round(pivot, 3)}, handle points {np.round(tip / np.linalg.norm(tip), 2)} (glTF)")
    return glb, parts


def knife_switch(source):
    glb = Glb(source)
    parts = parts_of(glb)
    closed = next(p for p in parts if "Type C Standard" in p.name or "Standard" in p.name)
    opened = next(p for p in parts if "Open" in p.name)
    # Islands that sit elsewhere in the open switch are the blade (handle, blades, crossbar).
    closed_labels = islands(closed)
    opened_labels = islands(opened)
    # The variants stand side by side: compare them on the same spot (their plates, the biggest piece).
    plate = np.bincount(closed_labels).argmax()
    offset = opened.positions[opened.indices.reshape(-1, 3)[opened_labels == plate].reshape(-1)].mean(0) \
        - closed.positions[closed.indices.reshape(-1, 3)[closed_labels == plate].reshape(-1)].mean(0)
    opened.attributes["POSITION"] = (opened.positions - offset,) + opened.attributes["POSITION"][1:]
    moving = np.zeros(len(closed_labels), bool)
    for label in range(closed_labels.max() + 1):
        triangles = closed.indices.reshape(-1, 3)[closed_labels == label]
        same = opened.indices.reshape(-1, 3)[opened_labels == label] if label <= opened_labels.max() else None
        a = closed.positions[triangles.reshape(-1)]
        b = opened.positions[same.reshape(-1)] if same is not None and len(same) == len(triangles) else None
        if b is None or np.abs(a - b).max() > 0.002:
            moving[closed_labels == label] = True
    base = split(closed, ~moving, "Base")
    blade = split(closed, moving, "Blade")
    # The hinge: where the blade's vertices barely move between closed and open.
    blade_tris = closed.indices.reshape(-1, 3)[moving].reshape(-1)
    shift = np.linalg.norm(closed.positions[blade_tris] - opened.positions[opened.indices.reshape(-1, 3)[moving].reshape(-1)], axis=1) \
        if len(opened.indices) == len(closed.indices) else None
    parts = [base, blade]
    transform(parts, np.eye(3) * 1.15)
    low, high = bounds([base])
    translate(parts, -np.array([(low[0] + high[0]) / 2, low[1], low[2]]))
    points = blade.positions
    if shift is not None:
        still = closed.positions[blade_tris][shift < np.percentile(shift, 3)] * 1.15 - np.array([(low[0] + high[0]) / 2, low[1], low[2]])
        hinge = np.array([0.0, still[:, 1].mean(), still[:, 2].mean()])
    else:
        hinge = np.array([0.0, points[:, 1].min() + 0.01, points[:, 2].mean()])
    set_pivot(blade, hinge)
    print(f"  switch: {moving.sum()} of {len(moving)} triangles move; hinge at {np.round(hinge, 3)}")
    return glb, parts


SPECS = {
    "car_wagon": ("fairheaven_sw_84_-_low_poly_model.glb", car_wagon, 2048),
    "car_wreck": ("old_rusty_car.glb", car_wreck, 1024),
    "exit_lever": ("industrial_electricitys_lever.glb", exit_lever, 1024),
    "knife_switch": ("early_1900s_knife_switches_type_c.glb", knife_switch, 1024),
}
HANDS = ("first_person_hands_rigged.glb", "fp_hands")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--source", default=str(Path.home() / "Downloads/nightshift-models"), help="folder with the downloaded .glb files")
    parser.add_argument("--project", default=str(ROOT / "unity"), help="the Unity project")
    args = parser.parse_args()
    source = Path(args.source).expanduser()
    out = Path(args.project) / TARGET
    out.mkdir(parents=True, exist_ok=True)
    for name, (file, prepare, max_texture) in SPECS.items():
        glb, parts = prepare(source / file)
        write(glb, parts, name, out / f"{name}.glb", max_texture)
    shutil.copyfile(source / HANDS[0], out / f"{HANDS[1]}.glb")
    print(f"{HANDS[1]}.glb: copied")


if __name__ == "__main__":
    main()
