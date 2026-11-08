"""Sculpt Pockets from nothing: his head and his mallet, as smooth surfaces with no source model.

Each part is a signed distance field: rounded shapes melted together and carved, the way a digital
sculptor pushes clay. Marching cubes turns the field into a surface, its normals come straight from
the field (so nothing looks faceted), and it is painted from where each point lies: cavities darker,
edges chipped, paint on the porcelain.

The design is a ventriloquist's dummy that was rebuilt as an animatronic: a porcelain face, a hinged
jaw on deep marionette lines, a long painted grin, one eye too wide and one half shut.

Requirements: Python with numpy, scipy, scikit-image and pillow.
Usage: python tools/sculpt_pockets.py out_folder [head mallet]
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage
from skimage import measure

VOXEL = 0.0012


class Field:
    """A distance field on a grid; shapes are added only around where they are, to stay fast."""

    def __init__(self, low, high, voxel=VOXEL):
        self.low = np.array(low, np.float32)
        self.voxel = voxel
        self.shape = tuple(np.ceil((np.array(high) - self.low) / voxel).astype(int) + 1)
        self.d = np.full(self.shape, 1.0, np.float32)

    def window(self, low, high):
        a = np.clip(np.floor((np.array(low) - self.low) / self.voxel).astype(int), 0, np.array(self.shape) - 1)
        b = np.clip(np.ceil((np.array(high) - self.low) / self.voxel).astype(int) + 1, 1, self.shape)
        slices = tuple(slice(i, j) for i, j in zip(a, b))
        axes = [self.low[k] + np.arange(a[k], b[k], dtype=np.float32) * self.voxel for k in range(3)]
        x, y, z = np.meshgrid(*axes, indexing="ij", sparse=True)
        return slices, x, y, z

    def blend(self, slices, shape, k):
        """Melt a shape in (k > 0), weld it hard (k = 0)."""
        here = self.d[slices]
        if k <= 0:
            self.d[slices] = np.minimum(here, shape)
            return
        h = np.clip(0.5 + 0.5 * (shape - here) / k, 0, 1)
        self.d[slices] = shape * (1 - h) + here * h - k * h * (1 - h)

    def carve_shape(self, slices, shape, k):
        here = self.d[slices]
        if k <= 0:
            self.d[slices] = np.maximum(here, -shape)
            return
        h = np.clip(0.5 - 0.5 * (here + shape) / k, 0, 1)
        self.d[slices] = here * (1 - h) - shape * h + k * h * (1 - h)

    def _ellipsoid(self, center, radii, margin, rotate=None):
        center, radii = np.array(center, np.float32), np.array(radii, np.float32)
        m = radii.max() + margin
        slices, x, y, z = self.window(center - m, center + m)
        px, py, pz = x - center[0], y - center[1], z - center[2]
        if rotate is not None:
            c, s = np.cos(rotate), np.sin(rotate)
            px, py = px * c - py * s, px * s + py * c
        k0 = np.sqrt((px / radii[0]) ** 2 + (py / radii[1]) ** 2 + (pz / radii[2]) ** 2)
        k1 = np.sqrt((px / radii[0] ** 2) ** 2 + (py / radii[1] ** 2) ** 2 + (pz / radii[2] ** 2) ** 2)
        return slices, k0 * (k0 - 1) / np.maximum(k1, 1e-5)

    def ball(self, center, radii, k=0.0, rotate=None):
        radii = np.broadcast_to(radii, 3)
        slices, d = self._ellipsoid(center, radii, k + 0.02, rotate)
        self.blend(slices, d, k)

    def cut_ball(self, center, radii, k=0.0, rotate=None):
        radii = np.broadcast_to(radii, 3)
        slices, d = self._ellipsoid(center, radii, k + 0.02, rotate)
        self.carve_shape(slices, d, k)

    def _capsule(self, a, b, ra, rb, margin):
        a, b = np.array(a, np.float32), np.array(b, np.float32)
        low = np.minimum(a, b) - max(ra, rb) - margin
        high = np.maximum(a, b) + max(ra, rb) + margin
        slices, x, y, z = self.window(low, high)
        ab = b - a
        t = ((x - a[0]) * ab[0] + (y - a[1]) * ab[1] + (z - a[2]) * ab[2]) / max(float(ab @ ab), 1e-9)
        t = np.clip(t, 0, 1)
        d = np.sqrt((x - a[0] - ab[0] * t) ** 2 + (y - a[1] - ab[1] * t) ** 2 + (z - a[2] - ab[2] * t) ** 2)
        return slices, d - (ra + (rb - ra) * t)

    def capsule(self, a, b, ra, rb=None, k=0.0):
        slices, d = self._capsule(a, b, ra, ra if rb is None else rb, k + 0.02)
        self.blend(slices, d, k)

    def cut_capsule(self, a, b, ra, rb=None, k=0.0):
        slices, d = self._capsule(a, b, ra, ra if rb is None else rb, k + 0.02)
        self.carve_shape(slices, d, k)

    def path(self, points, radius, k=0.0, carve=False):
        """A curved stroke, as a chain of capsules; radius may change along it."""
        points = np.asarray(points, np.float32)
        radii = np.broadcast_to(radius, len(points))
        for i in range(len(points) - 1):
            if carve:
                self.cut_capsule(points[i], points[i + 1], radii[i], radii[i + 1], k)
            else:
                self.capsule(points[i], points[i + 1], radii[i], radii[i + 1], k)

    def cylinder(self, a, b, radius, bevel=0.0, k=0.0):
        """A round bar with softened edges."""
        a, b = np.array(a, np.float32), np.array(b, np.float32)
        margin = radius + bevel + k + 0.02
        slices, x, y, z = self.window(np.minimum(a, b) - margin, np.maximum(a, b) + margin)
        ab = b - a
        length = float(np.linalg.norm(ab))
        axis = ab / max(length, 1e-9)
        px, py, pz = x - a[0], y - a[1], z - a[2]
        along = px * axis[0] + py * axis[1] + pz * axis[2]
        out = np.sqrt(np.maximum((px - axis[0] * along) ** 2 + (py - axis[1] * along) ** 2 + (pz - axis[2] * along) ** 2, 0))
        dr = out - (radius - bevel)
        dh = np.abs(along - length / 2) - (length / 2 - bevel)
        d = np.minimum(np.maximum(dr, dh), 0) + np.sqrt(np.maximum(dr, 0) ** 2 + np.maximum(dh, 0) ** 2) - bevel
        self.blend(slices, d, k)

    def normal_at(self, points):
        """The field's direction at these points."""
        h = self.voxel
        n = np.stack([self.sample(points + np.array(axis, np.float32) * h) - self.sample(points - np.array(axis, np.float32) * h)
                      for axis in ((1, 0, 0), (0, 1, 0), (0, 0, 1))], 1)
        return n / (np.linalg.norm(n, axis=1, keepdims=True) + 1e-9)

    def surface(self):
        vertices, faces, _, _ = measure.marching_cubes(self.d, 0.0, step_size=1, allow_degenerate=False)
        normals = np.stack(np.gradient(self.d), -1)
        n = np.stack([ndimage.map_coordinates(normals[..., i], vertices.T, order=1) for i in range(3)], 1)
        n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-9
        points = vertices * self.voxel + self.low
        # Marching cubes winds its faces against the field's gradient on this axis order.
        return points.astype(np.float32), faces[:, ::-1].astype(np.int32), n.astype(np.float32)

    def sample(self, points):
        coordinates = ((points - self.low) / self.voxel).T
        return ndimage.map_coordinates(self.d, coordinates, order=1, mode="nearest")


def noise3(p, scale, seed, octaves=4):
    rng = np.random.default_rng(seed)
    value = np.zeros(len(p), np.float32)
    weight = 1.0
    for octave in range(octaves):
        for _ in range(6):
            direction = rng.normal(size=3)
            direction /= np.linalg.norm(direction)
            value += weight * np.sin(p @ direction.astype(np.float32) * scale * 2 ** octave * rng.uniform(0.8, 1.25) + rng.uniform(0, 6.28))
        weight *= 0.55
    # Even across the part: 0.5 is the middle, and 0.85 is as rare on a small part as on a large one.
    return np.clip(0.5 + 0.2 * (value - value.mean()) / max(float(value.std()), 1e-6), 0, 1)


def smooth(low, high, value):
    t = np.clip((value - low) / (high - low), 0, 1)
    return t * t * (3 - 2 * t)


def occlusion(field, p, n):
    """How much the surface around a point closes over it: dark in creases, clean on the form."""
    total = np.zeros(len(p), np.float32)
    weight = 1.0
    for distance in (0.003, 0.007, 0.014, 0.026, 0.045):
        total += weight * np.clip(distance - field.sample(p + n * distance), 0, None) / distance
        weight *= 0.7
    return np.clip(1 - 0.62 * total, 0.08, 1)


# ------------------------------------------------------------------ the head

CROWN = (0.08, 0.1, -0.022)


def grin_curve(t, wide=0.076, rise=0.05, y=-0.068, front=0.092):
    """The mouth line: t from -1 to 1 along a long smile whose corners climb the cheeks."""
    t = np.asarray(t, np.float32)
    x = t * wide
    return np.stack([x, y + rise * np.abs(t) ** 2.0, front - 0.052 * (t * t)], -1)


def mouth_radius(t):
    return 0.0035 + 0.0165 * (1 - np.clip(np.abs(t), 0, 1) ** 1.7)


MOUTH_HALF = 0.07
MOUTH_HIGH = 0.027


def mouth_edges(x):
    """The top and bottom of the open mouth at x: a wide rounded slot whose corners lift."""
    s = np.clip(np.abs(x) / MOUTH_HALF, 0, 1)
    shape = np.sqrt(np.clip(1 - s ** 5, 0, 1))
    middle = -0.06 + 0.022 * s ** 2
    return middle + MOUTH_HIGH * shape, middle - MOUTH_HIGH * shape, middle


def head_solid():
    """A wide round mask of a head, short in the chin, with the sockets and the mouth broken open."""
    f = Field((-0.15, -0.16, -0.14), (0.15, 0.17, 0.15))
    f.ball((0, 0.03, -0.015), (0.106, 0.1, 0.102))
    f.ball((0, -0.05, 0.02), (0.094, 0.068, 0.076), k=0.05)
    f.ball((0, -0.102, 0.034), (0.044, 0.02, 0.032), k=0.04)
    for side in (-1, 1):
        f.ball((side * 0.07, -0.02, 0.062), (0.03, 0.026, 0.03), k=0.035)
    # A flat front, like a mask.
    z = (f.low[2] + np.arange(f.shape[2], dtype=np.float32) * f.voxel)[None, None, :]
    f.d = np.maximum(f.d, z - 0.106)
    # Lips: a thick worn rim around the mouth, built on the real surface.
    xs = np.linspace(-MOUTH_HALF, MOUTH_HALF, 31)
    top, bottom, _ = mouth_edges(xs)
    loop = np.vstack([np.stack([xs, top, np.full(31, 0.09)], 1), np.stack([xs[::-1], bottom[::-1], np.full(31, 0.09)], 1)])
    rim = settle(f, loop)
    MOUTH_RIM["xs"], MOUTH_RIM["z"] = xs, rim[:31, 2]
    closed = np.vstack([rim, rim[:1]])
    wobble = 0.0088 + 0.0016 * np.sin(np.arange(len(closed)) * 1.9) + 0.001 * np.sin(np.arange(len(closed)) * 4.3)
    f.path(closed, wobble, k=0.007)
    # The mouth: a hole through the rim into a dark cavity.
    n = f.shape
    x = (f.low[0] + np.arange(n[0], dtype=np.float32) * f.voxel)[:, None, None]
    y = (f.low[1] + np.arange(n[1], dtype=np.float32) * f.voxel)[None, :, None]
    upper, lower, _ = mouth_edges(x)
    slot = np.maximum(np.maximum(y - upper, lower - y), np.abs(x) - MOUTH_HALF)
    f.d = np.maximum(f.d, -np.maximum(slot, 0.012 - z))
    f.cut_ball((0, -0.056, 0.034), (0.078, 0.044, 0.058), k=0.006)
    # Deep sockets, uneven: the +x eye is larger.
    for side in (-1, 1):
        size = 1.14 if side > 0 else 0.97
        f.cut_ball((side * 0.045, 0.024, 0.104), np.array((0.0345, 0.032, 0.05)) * size, k=0.009)
    # A hole smashed through the crown, with a jagged rim, and the mask's cast seams.
    crown = np.array(CROWN, np.float32)
    reach = np.float32(0.052) + 0.02
    slices, d = f._ellipsoid(crown, (0.052, 0.044, 0.052), 0.02)
    _, jx, jy, jz = f.window(crown - reach, crown + reach)
    jag = np.sin(jx * 330 + jy * 120) * np.sin(jz * 290 + jy * 75) * 0.008 + np.sin(jx * 120 - jz * 170) * 0.005
    f.carve_shape(slices, d + jag, 0.0)
    for path, wide in (([[0.03, 0.12, 0.03], [0.0, 0.1, 0.07], [-0.024, 0.08, 0.09], [-0.012, 0.062, 0.1]], 0.0021),
                       ([[0.02, 0.122, -0.04], [-0.02, 0.122, -0.052], [-0.06, 0.1, -0.07]], 0.002),
                       ([[0.092, 0.05, 0.012], [0.1, 0.012, 0.03], [0.094, -0.022, 0.056]], 0.0019),
                       ([[-0.098, 0.05, 0.0], [-0.103, 0.01, 0.022], [-0.094, -0.03, 0.05]], 0.0014),
                       ([[-0.03, 0.094, 0.08], [-0.066, 0.078, 0.076], [-0.09, 0.06, 0.05]], 0.0012),
                       ([[0.068, -0.086, 0.07], [0.05, -0.1, 0.066], [0.024, -0.118, 0.052]], 0.0014)):
        points = settle(f, path)
        f.path(points, np.linspace(wide, wide * 0.4, len(points)), k=0.001, carve=True)
    f.cut_ball((0, 0.035, -0.02), (0.082, 0.08, 0.078), k=0.0)
    return f


MOUTH_RIM = {}
JAW_HINGE = np.array((0.0, -0.022, -0.02), np.float32)
JAW_OPEN_DEGREES = 21.0


def split_jaw(solid):
    """Saw the jaw free through the middle of the mouth and down its corners."""
    n = solid.shape
    x = (solid.low[0] + np.arange(n[0], dtype=np.float32) * solid.voxel)[:, None, None]
    y = (solid.low[1] + np.arange(n[1], dtype=np.float32) * solid.voxel)[None, :, None]
    z = (solid.low[2] + np.arange(n[2], dtype=np.float32) * solid.voxel)[None, None, :]
    _, _, middle = mouth_edges(x)
    wide = np.clip(MOUTH_HALF + 0.17 * (y + 0.05), 0.03, MOUTH_HALF + 0.001)
    cut = np.maximum(np.maximum(np.abs(x) - wide, y - middle), -(z - 0.0))
    upper, jaw = Field.__new__(Field), Field.__new__(Field)
    for field in (upper, jaw):
        field.low, field.voxel, field.shape = solid.low, solid.voxel, solid.shape
    upper.d = np.maximum(solid.d, -(cut - 0.0012))
    jaw.d = np.maximum(solid.d, cut + 0.0012)
    return upper, jaw


def settle(field, points, steps=8):
    """Slide points along the field until they sit on its surface."""
    points = np.array(points, np.float32)
    gradient = np.stack(np.gradient(field.d), -1)
    for _ in range(steps):
        coordinates = ((points - field.low) / field.voxel).T
        d = ndimage.map_coordinates(field.d, coordinates, order=1, mode="nearest")
        n = np.stack([ndimage.map_coordinates(gradient[..., i], coordinates, order=1, mode="nearest") for i in range(3)], 1)
        n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-9
        points -= n * d[:, None] * 0.8
    return points


def sculpt_teeth(lower):
    """Two uneven rows of chipped blades, set behind the lips; some broken, some missing."""
    f = Field((-0.085, -0.12, -0.01), (0.085, 0.0, 0.115))
    rng = np.random.default_rng(66 if lower else 61)
    for k in range(-9, 10):
        if rng.random() < 0.07:
            continue
        x = (k + rng.uniform(-0.28, 0.28)) * 0.0066
        top, bottom, middle = mouth_edges(np.array(x))
        edge = bottom if lower else top
        sign = 1 if lower else -1
        surface = float(np.interp(x, MOUTH_RIM["xs"], MOUTH_RIM["z"]))
        base = np.array((x, edge - sign * 0.0035, surface - 0.0115 + rng.uniform(-0.0012, 0.0012)), np.float32)
        length = rng.uniform(0.0105, 0.021) * (1 - 0.45 * (abs(x) / MOUTH_HALF) ** 2)
        if rng.random() < 0.16:
            length *= 0.45
        wide = rng.uniform(0.0034, 0.0052)
        tip = base + np.array((rng.uniform(-0.0032, 0.0032), sign * length, rng.uniform(-0.0014, 0.001)), np.float32)
        middle_point = base + (tip - base) * 0.45
        f.capsule(base, middle_point, wide, wide * 0.72, k=0.0009)
        f.capsule(middle_point, tip, wide * 0.72, wide * 0.13, k=0.0009)
    return f


def sculpt_gum(lower):
    f = Field((-0.085, -0.12, -0.02), (0.085, 0.0, 0.11))
    xs = np.linspace(-MOUTH_HALF * 0.94, MOUTH_HALF * 0.94, 25)
    top, bottom, _ = mouth_edges(xs)
    edge = bottom if lower else top
    sign = 1 if lower else -1
    z = np.interp(xs, MOUTH_RIM["xs"], MOUTH_RIM["z"]) - 0.016
    f.path(np.stack([xs, edge - sign * 0.0052, z], 1).astype(np.float32), 0.0072, k=0.004)
    return f


def sculpt_mouth_machine():
    """Behind the teeth there is no throat: steel rods and a piston that drive the jaw."""
    f = Field((-0.085, -0.12, -0.07), (0.085, -0.005, 0.07))
    f.ball((0, -0.072, -0.016), (0.074, 0.058, 0.03))
    for x, wide in ((-0.046, 0.0042), (-0.022, 0.0034), (0.002, 0.0048), (0.027, 0.0034), (0.05, 0.0042)):
        f.cylinder((x, -0.112, 0.014), (x * 0.92, -0.012, 0.014), wide, 0.0008)
    f.cylinder((-0.066, -0.054, 0.006), (0.066, -0.05, 0.006), 0.004, 0.0008)
    f.cylinder((-0.066, -0.076, 0.002), (0.066, -0.074, 0.002), 0.0032, 0.0008)
    for x, y in ((-0.046, -0.052), (0.002, -0.051), (0.05, -0.05), (-0.022, -0.075), (0.027, -0.074)):
        f.ball((x, y, 0.009), 0.0058, k=0.001)
    return f


def sculpt_nose():
    f = Field((-0.05, -0.05, 0.08), (0.05, 0.03, 0.15))
    f.ball((0, -0.006, 0.112), (0.0225, 0.0205, 0.0215))
    return f


EYES = ((-0.045, 0.024, 0.0268), (0.045, 0.024, 0.0312))


def sculpt_eyes():
    f = Field((-0.09, -0.02, 0.03), (0.09, 0.07, 0.11))
    for cx, cy, r in EYES:
        f.ball((cx, cy, 0.066), r)
    return f


def sculpt_fittings(face):
    """Heavy bolts at the jaw hinges and a few rivets along the mask's edge."""
    f = Field((-0.13, -0.08, -0.02), (0.13, 0.12, 0.13))
    places = [(0.099, -0.034, 0.05), (-0.099, -0.034, 0.05)]
    big = settle(face, places)
    for at in big:
        f.ball(at, 0.0072, k=0.0008)
    small = [(side * x, y, z) for side in (-1, 1) for x, y, z in ((0.09, 0.052, 0.05), (0.07, 0.078, 0.066), (0.103, 0.004, 0.038))]
    for at in settle(face, small):
        f.ball(at, 0.0031, k=0.0005)
    return f


def sculpt_hatch(face):
    """A riveted service hatch on the back of the head: this mask was opened for repairs."""
    f = Field((-0.08, -0.05, -0.135), (0.08, 0.11, -0.06))
    f.ball((0, 0.03, -0.11), (0.054, 0.061, 0.016))
    z = (np.arange(f.shape[2], dtype=np.float32) * f.voxel + f.low[2])[None, None, :]
    f.d = np.maximum(f.d, -z - 0.12)
    angles = np.linspace(0, 2 * np.pi, 13)[:-1]
    ring = np.stack([np.cos(angles) * 0.047, 0.03 + np.sin(angles) * 0.054, np.full(12, -0.12)], 1)
    for at in settle(face, ring):
        f.ball(at + np.array((0, 0, -0.002), np.float32), 0.0034, k=0.0005)
    return f


def sculpt_hat():
    """A battered little top hat, far too small, tipped over the good side of his head."""
    f = Field((-0.15, 0.06, -0.12), (0.06, 0.27, 0.1))
    tilt = np.array((-0.34, 0.94, 0.05), np.float32)
    tilt /= np.linalg.norm(tilt)
    base = np.array(HAT_AT, np.float32)
    f.cylinder(base - tilt * 0.003, base + tilt * 0.0035, 0.07, 0.0026)
    f.cylinder(base, base + tilt * 0.092, 0.046, 0.0035)
    f.cut_ball(base + tilt * 0.076 + np.array((0.044, 0.0, 0.02), np.float32), (0.02, 0.02, 0.024), k=0.012)
    f.cut_ball(base + tilt * 0.095 + np.array((-0.012, 0.006, -0.01), np.float32), (0.026, 0.009, 0.022), k=0.008)
    return f


HAT_AT = (-0.047, 0.118, -0.012)


def sculpt_machine():
    """Through the broken crown and under the chin: a steel skull, a gear, a neck of rings."""
    f = Field((-0.09, -0.19, -0.11), (0.13, 0.16, 0.07))
    f.ball((0.002, 0.04, -0.024), (0.076, 0.074, 0.072))
    out = np.array((0.62, 0.78, 0.0), np.float32)
    hub = np.array((0.052, 0.094, -0.024), np.float32)
    across = np.array((-out[1], out[0], 0.0), np.float32)
    deep = np.array((0, 0, 1.0), np.float32)
    f.cylinder(hub - out * 0.004, hub + out * 0.006, 0.021, 0.0014, k=0.0)
    for tooth in range(12):
        angle = tooth * np.pi / 6
        spoke = across * np.cos(angle) + deep * np.sin(angle)
        f.capsule(hub + out * 0.001 + spoke * 0.018, hub + out * 0.001 + spoke * 0.0265, 0.0032, 0.0026, k=0.0007)
    f.cylinder(hub + out * 0.005, hub + out * 0.011, 0.005, 0.001)
    f.cylinder((0, -0.19, -0.012), (0, -0.08, -0.012), 0.03, 0.002)
    for y in (-0.175, -0.15, -0.125):
        f.cylinder((0, y - 0.0065, -0.012), (0, y + 0.0065, -0.012), 0.04, 0.0048)
    return f


def sculpt_drool_still(upper, jaw):
    """Spit run from the corners of the mouth and over the lip."""
    f = Field((-0.11, -0.13, 0.0), (0.11, 0.0, 0.12))
    for path, width, solid in (([[0.074, -0.044, 0.07], [0.072, -0.066, 0.07], [0.066, -0.09, 0.065]], 0.0017, jaw),
                               ([[-0.073, -0.048, 0.07], [-0.07, -0.072, 0.07]], 0.0014, jaw),
                               ([[0.018, -0.096, 0.098], [0.017, -0.112, 0.085]], 0.0013, jaw)):
        points = settle(solid, path)
        f.path(points, np.linspace(width * 0.72, width * 1.15, len(points)), k=0.0008)
        f.ball(points[-1], width * 1.55, k=0.0014)
    return f


def drool_strands(angle=0.0):
    """Strings of spit between the rows of teeth."""
    rng = np.random.default_rng(5)
    parts_v, parts_f, parts_n, offset = [], [], [], 0
    for k in range(11):
        x = rng.uniform(-0.05, 0.05)
        top, bottom, _ = mouth_edges(np.array(x))
        surface = float(np.interp(x, MOUTH_RIM["xs"], MOUTH_RIM["z"]))
        start = np.array((x, top - rng.uniform(0.006, 0.014), surface - 0.014), np.float32)
        x2 = x + rng.uniform(-0.006, 0.006)
        end = np.array((x2, bottom + rng.uniform(0.004, 0.012), surface - 0.014), np.float32)
        end = rotate_jaw(end[None], angle)[0]
        steps = np.linspace(0, 1, 9)
        line = start + (end - start) * steps[:, None]
        line[:, 0] += rng.uniform(-0.0026, 0.0026) * np.sin(steps * np.pi)
        line[:, 2] += -0.003 * np.sin(steps * np.pi)
        radius = 0.00028 + 0.0008 * np.abs(steps - 0.5) * 2
        v, f, n = strand(line, radius, 5)
        parts_v.append(v); parts_f.append(f + offset); parts_n.append(n); offset += len(v)
    return np.vstack(parts_v), np.vstack(parts_f), np.vstack(parts_n)


def rotate_jaw(points, angle):
    """Points of the jaw, turned open about the hinge in front of the ears."""
    p = points - JAW_HINGE
    c, s = np.cos(angle), np.sin(angle)
    out = p.copy()
    out[:, 1] = p[:, 1] * c - p[:, 2] * s
    out[:, 2] = p[:, 1] * s + p[:, 2] * c
    return out + JAW_HINGE


def strand(line, radius, sides):
    """A round tapered tube along a line."""
    line = np.asarray(line, np.float32)
    tangent = np.gradient(line, axis=0)
    tangent /= np.linalg.norm(tangent, axis=1, keepdims=True) + 1e-9
    side = np.cross(tangent, np.array([0.3, 0.9, 0.3], np.float32))
    side /= np.linalg.norm(side, axis=1, keepdims=True) + 1e-9
    other = np.cross(tangent, side)
    angles = np.linspace(0, 2 * np.pi, sides, endpoint=False, dtype=np.float32)
    normals = side[:, None] * np.cos(angles)[None, :, None] + other[:, None] * np.sin(angles)[None, :, None]
    vertices = (line[:, None] + normals * np.asarray(radius, np.float32)[:, None, None]).reshape(-1, 3)
    faces = []
    for i in range(len(line) - 1):
        for j in range(sides):
            a, b = i * sides + j, i * sides + (j + 1) % sides
            faces += [[a, b, a + sides], [b, b + sides, a + sides]]
    return vertices, np.array(faces, np.int32), normals.reshape(-1, 3).astype(np.float32)


def hair_mesh():
    """Hundreds of thin frizzy locks in clumps, instead of one sculpted lump."""
    rng = np.random.default_rng(87)
    vertices, faces, normals, colors, offset = [], [], [], [], 0
    for side in (-1, 1):
        for clump in range(22):
            root = np.array([side * rng.uniform(0.07, 0.092), rng.uniform(-0.02, 0.09), rng.uniform(-0.09, 0.012)], np.float32)
            reach = rng.uniform(0.045, 0.092)
            lift = rng.uniform(-0.02, 0.06)
            back = rng.uniform(-0.06, 0.012)
            shade = rng.uniform(0.55, 1.2)
            for hair in range(11):
                start = root + rng.normal(0, 1, 3).astype(np.float32) * [0.0035, 0.008, 0.008]
                wave, phase = rng.uniform(70, 170), rng.uniform(0, 6.28)
                length = reach * rng.uniform(0.62, 1.12)
                steps = np.linspace(0, 1, 8, dtype=np.float32)
                line = np.stack([start[0] + side * length * steps ** 0.82,
                                 start[1] + (lift + rng.uniform(-0.02, 0.02)) * steps ** 1.7,
                                 start[2] + (back + rng.uniform(-0.02, 0.02)) * steps], 1)
                # Frizz grows toward the tips.
                line[:, 1] += np.sin(steps * wave * length + phase) * 0.0045 * steps
                line[:, 2] += np.cos(steps * wave * length * 0.8 + phase) * 0.0045 * steps
                radius = np.linspace(rng.uniform(0.0017, 0.0028), 0.00035, 8)
                v, f, n = strand(line, radius, 3)
                strand_shade = shade * rng.uniform(0.7, 1.25)
                c = np.array([0.1, 0.018, 0.022], np.float32) * strand_shade * np.repeat(0.3 + 0.8 * steps, 3)[:, None]
                vertices.append(v); faces.append(f + offset); normals.append(n); colors.append(c)
                offset += len(v)
    return {"name": "hair", "v": np.vstack(vertices), "f": np.vstack(faces), "n": np.vstack(normals), "c": np.vstack(colors), "shine": 0.25}


def paint_face(p, n, ao):
    """Old paint on a mask: soot smeared around the eyes, worn red lips, grease and cracks."""
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    grime = noise3(p, 22, 1)
    fine = noise3(p, 150, 2, 3)
    color = np.array([0.4, 0.375, 0.31], np.float32) * (0.66 + 0.56 * grime)[:, None]
    wash = smooth(0.38, 0.66, noise3(p, 13, 3))[:, None]
    color = color * (1 - 0.74 * wash) + np.array([0.07, 0.05, 0.032], np.float32) * 0.74 * wash
    # Broken diamonds of soot over the eyes, the sign on the posters, dragged by a hand.
    for cx, cy, sx, sy in ((-0.045, 0.024, 0.043, 0.069), (0.045, 0.024, 0.05, 0.08)):
        smear = (noise3(p * [1.0, 0.35, 1.0], 26, 7, 3) - 0.5) * 0.46
        diamond = np.abs(x - cx) / sx + np.abs(y - cy) / sy
        edge = (1 - smooth(0.78, 1.04, diamond + smear * 0.75)) * smooth(0.02, 0.05, z)
        color = color * (1 - edge[:, None] * 0.95) + np.array([0.008, 0.008, 0.01], np.float32) * edge[:, None] * 0.95
    for cx, length, width in ((-0.066, 0.05, 0.0024), (-0.03, 0.03, 0.0016), (0.026, 0.038, 0.0018), (0.07, 0.06, 0.0028), (0.052, 0.028, 0.0015)):
        wander = 0.0016 * np.sin(y * 150 + cx * 130)
        run = (np.abs(x - cx - wander) < width * (0.55 + 0.9 * fine)) & (y < -0.006) & (y > -0.006 - length) & (z > 0.03)
        color[run] = np.array([0.012, 0.011, 0.012], np.float32)
    # Worn red on the lips, wider than the rim on one side, as if painted in a hurry.
    top, bottom, _ = mouth_edges(x)
    band = np.minimum(np.abs(y - top), np.abs(y - bottom))
    near_mouth = np.abs(x) < MOUTH_HALF + 0.0045
    width = 0.0115 + 0.0035 * np.sin(x * 70 + 1.0) + 0.006 * (noise3(p, 60, 8, 3) - 0.3)
    lips = (1 - smooth(width - 0.0012, width + 0.0012, band)) * near_mouth * smooth(0.04, 0.07, z)
    red = np.array([0.2, 0.014, 0.017], np.float32) * (0.45 + 0.95 * grime)[:, None]
    bare = smooth(0.62, 0.7, noise3(p, 85, 9, 4))[:, None]
    painted = lips[:, None] * (1 - bare * 0.75)
    color = color * (1 - painted) + red * painted
    # Chips: where paint and glaze broke off.
    chips = smooth(0.84, 0.86, noise3(p, 170, 4, 4))[:, None] * (1 - lips[:, None] * 0.6)
    color = color * (1 - chips) + np.array([0.15, 0.12, 0.085], np.float32) * chips
    # Hairline cracks: the borders of shattered cells.
    rng = np.random.default_rng(1987)
    seeds = (rng.normal(0, 1, (46, 3)) * [0.085, 0.09, 0.08] + [0, 0.02, 0.03]).astype(np.float32)
    distances = np.empty((len(p), 2), np.float32)
    for start in range(0, len(p), 200000):
        chunk = np.linalg.norm(p[start:start + 200000, None, :] - seeds[None], axis=2)
        chunk.partition(1, axis=1)
        distances[start:start + 200000] = chunk[:, :2]
    crack = (1 - smooth(0.0, 0.0009, distances[:, 1] - distances[:, 0]))[:, None]
    gate = smooth(0.42, 0.56, noise3(p, 18, 5, 2))[:, None]
    color = color * (1 - crack * gate * 0.92)
    streak = smooth(0.58, 0.82, noise3(p * [8.0, 0.5, 8.0], 14, 6, 3))[:, None] * smooth(-0.02, 0.09, y)[:, None]
    color = color * (1 - streak * 0.65) + np.array([0.04, 0.024, 0.014], np.float32) * streak * 0.65
    scorch = 1 - smooth(0.035, 0.09, np.linalg.norm(p - np.array(CROWN, np.float32), axis=1))
    color = color * (1 - scorch[:, None] * 0.85)
    # Inside the head and the mouth everything is black.
    inside = (x / 0.086) ** 2 + ((y - 0.035) / 0.084) ** 2 + ((z + 0.02) / 0.082) ** 2 < 1.0
    cavity = (x / 0.08) ** 2 + ((y + 0.056) / 0.046) ** 2 + ((z - 0.034) / 0.06) ** 2 < 1.0
    color[inside | cavity] = np.array([0.012, 0.004, 0.005], np.float32)
    return np.clip(color * ao[:, None] ** 1.5, 0, 1)


def paint_flat(p, ao, base, wear=0.2, scale=60, seed=9):
    grime = noise3(p, scale, seed, 3)
    color = np.tile(np.array(base, np.float32), (len(p), 1)) * (1 - wear + 2 * wear * grime)[:, None]
    return np.clip(color * ao[:, None], 0, 1)


def paint_steel(p, n, ao):
    grime = noise3(p, 60, 11, 4)
    rust = smooth(0.62, 0.86, noise3(p, 28, 12, 4))[:, None]
    color = np.array([0.15, 0.15, 0.155], np.float32) * (0.5 + 0.95 * grime)[:, None]
    color = color * (1 - rust) + np.array([0.24, 0.1, 0.045], np.float32) * rust
    return np.clip(color * ao[:, None], 0, 1)


def part_from(name, field, painter, shine=0.0):
    v, t, n = field.surface()
    ao = occlusion(field, v, n)
    print(f"  {name}: {len(v)} vertices, {len(t)} triangles", flush=True)
    return {"name": name, "v": v, "f": t, "n": n, "c": painter(v, n, ao), "shine": shine, "field": field, "painter": painter}


def head_parts():
    solid = head_solid()
    upper, jaw = split_jaw(solid)

    def skin(p, n, ao):
        """The paint, except on the faces the saw left: inside the head it is black."""
        color = paint_face(p, n, ao)
        color[solid.sample(p) < -0.0022] = np.array([0.012, 0.004, 0.005], np.float32)
        return color

    gum = lambda p, n, ao: paint_flat(p, ao, (0.085, 0.006, 0.01), 0.3, 200)
    teeth = lambda p, n, ao: paint_teeth(p, ao)
    parts = [part_from("face", upper, skin, 0.4)]
    moving = [part_from("jaw", jaw, skin, 0.4), part_from("teeth_low", sculpt_teeth(True), teeth, 0.8), part_from("gum_low", sculpt_gum(True), gum, 1.4)]
    for part in moving:
        part["jaw"] = True
    parts += moving
    parts.append(part_from("teeth_up", sculpt_teeth(False), teeth, 0.8))
    parts.append(part_from("gum_up", sculpt_gum(False), gum, 1.4))
    parts.append(part_from("mouth_machine", sculpt_mouth_machine(), lambda p, n, ao: paint_steel(p, n, ao ** 2) * 0.24, 0.35))
    parts.append(part_from("nose", sculpt_nose(), lambda p, n, ao: paint_flat(p, ao, (0.14, 0.009, 0.012), 0.42, 90), 0.5))
    parts.append(part_from("eyes", sculpt_eyes(), lambda p, n, ao: paint_eyes(p, ao), 0.55))
    parts.append(part_from("fittings", sculpt_fittings(upper), paint_steel, 0.9))
    parts.append(part_from("hatch", sculpt_hatch(upper), paint_steel, 0.8))
    parts.append(part_from("hat", sculpt_hat(), lambda p, n, ao: paint_hat(p, ao), 0.25))
    parts.append(part_from("machine", sculpt_machine(), paint_steel, 0.9))
    parts.append(part_from("drool", sculpt_drool_still(upper, jaw), lambda p, n, ao: paint_flat(p, ao, (0.05, 0.042, 0.03), 0.1, 100), 3.0))
    parts.append(hair_mesh())
    wet = np.array([0.12, 0.11, 0.085], np.float32)
    for name, angle, flag in (("drool_rest", 0.0, "only_rest"), ("drool_open", np.radians(JAW_OPEN_DEGREES), "only_open")):
        v, f, n = drool_strands(angle)
        parts.append({"name": name, "v": v, "f": f, "n": n, "c": np.tile(wet, (len(v), 1)), "shine": 3.0, flag: True})
    return parts


def paint_hat(p, ao):
    worn = noise3(p, 45, 71, 4)
    color = np.array([0.02, 0.018, 0.02], np.float32) * (0.5 + 1.8 * worn)[:, None]
    base = np.array(HAT_AT, np.float32)
    tilt = np.array((-0.34, 0.94, 0.05), np.float32)
    tilt /= np.linalg.norm(tilt)
    height = (p - base) @ tilt
    band = (height > 0.009) & (height < 0.024)
    color[band] = np.array([0.2, 0.02, 0.025], np.float32) * (0.55 + 0.9 * worn[band])[:, None]
    dust = smooth(0.65, 0.85, noise3(p, 28, 72, 3))[:, None]
    color = color * (1 - dust * 0.65) + np.array([0.1, 0.085, 0.06], np.float32) * dust * 0.65
    return np.clip(color * ao[:, None], 0, 1)


def paint_teeth(p, ao):
    """Yellow going to brown at the roots, some of them black."""
    base = noise3(p, 300, 21, 2)
    color = np.array([0.36, 0.28, 0.13], np.float32) * (0.3 + 1.15 * base)[:, None]
    bad = smooth(0.7, 0.75, noise3(p, 110, 23, 2))[:, None]
    color = color * (1 - bad * 0.86)
    return np.clip(color * ao[:, None] ** 1.2, 0, 1)


def paint_eyes(p, ao):
    """Black glass with one tiny lit point in each: a sick green one and a red one."""
    color = np.tile(np.array([0.004, 0.004, 0.005], np.float32), (len(p), 1))
    for (cx, cy, r), tint in zip(EYES, ((0.45, 1.0, 0.36), (1.0, 0.11, 0.05))):
        tint = np.array(tint, np.float32)
        d = np.sqrt((p[:, 0] - cx) ** 2 + (p[:, 1] - cy + 0.002) ** 2)
        front = (p[:, 2] > 0.074)[:, None]
        glow = (1 - smooth(0.0028, r * 0.42, d))[:, None] ** 2 * front
        color = color + tint * glow * 0.24
        point = (1 - smooth(0.0024, 0.0034, d))[:, None] * front
        color = color * (1 - point) + (tint * 0.65 + 0.35) * point
    return np.clip(color, 0, 1)


# ------------------------------------------------------------------ the body

HEAD_AT = np.array((0.0, 1.9, 0.0), np.float32)
HEAD_SIZE = 1.3
HEAD_WIDE = 1.0
SCANS = {}
SURFACES = Path(__file__).resolve().parents[2] / "ml-game" / "Assets" / "Art" / "Surfaces"


def scan(name):
    """A scanned surface as brightness around 1: the weave of real cloth and the wear of real metal."""
    if name not in SCANS:
        image = np.asarray(Image.open(SURFACES / name / f"{name}_albedo.jpg").convert("L").resize((1024, 1024)), np.float32)
        SCANS[name] = image / max(float(image.mean()), 1.0)
    return SCANS[name]


def triplanar(p, n, name, meters):
    """Sample a scan on all three axes, blended by which way the surface faces."""
    image = scan(name)
    size = image.shape[0]
    weights = np.abs(n) ** 4
    weights /= weights.sum(1, keepdims=True) + 1e-9
    out = np.zeros(len(p), np.float32)
    for axis, (a, b) in enumerate(((1, 2), (0, 2), (0, 1))):
        u = ((p[:, a] / meters) % 1 * (size - 1)).astype(np.int32)
        v = ((p[:, b] / meters) % 1 * (size - 1)).astype(np.int32)
        out += image[v, u] * weights[:, axis]
    return out


def wrinkle(f, amount, scale, seed):
    """Folds pushed into the cloth: sharp ridges along the way it hangs."""
    n = f.shape
    x = (f.low[0] + np.arange(n[0], dtype=np.float32) * f.voxel)[:, None, None]
    y = (f.low[1] + np.arange(n[1], dtype=np.float32) * f.voxel)[None, :, None]
    z = (f.low[2] + np.arange(n[2], dtype=np.float32) * f.voxel)[None, None, :]
    rng = np.random.default_rng(seed)
    field = np.zeros(n, np.float32)
    for k in range(9):
        direction = rng.normal(size=3) * [1.2, 0.34, 1.2]
        direction /= np.linalg.norm(direction)
        frequency = scale * rng.uniform(0.7, 1.5)
        wave = np.sin(x * direction[0] * frequency + y * direction[1] * frequency + z * direction[2] * frequency + rng.uniform(0, 6.28))
        field += 1 - np.abs(wave) * 2 ** 0.5
    field /= 9
    f.d += field * amount
    return f


def jagged_cut(f, center, radii, seed=1):
    """A hole torn through, with an edge that wanders."""
    center = np.array(center, np.float32)
    reach = np.float32(max(radii)) + 0.03
    slices, d = f._ellipsoid(center, radii, 0.03)
    _, x, y, z = f.window(center - reach, center + reach)
    jag = np.sin(x * 87 + y * 53 + seed) * np.sin(z * 71 + y * 61) * 0.024 + np.sin(x * 190 - z * 150 + y * 30) * 0.009
    f.carve_shape(slices, d + jag, 0.0)


def sculpt_torso():
    """A heavy barrel of a body on narrow shoulders: the shirt, with the upper arms."""
    f = Field((-0.76, 0.72, -0.47), (0.76, 1.76, 0.47), 0.003)
    f.ball((0, 1.1, 0.025), (0.262, 0.31, 0.25))
    f.ball((0, 1.43, -0.005), (0.3, 0.26, 0.225), k=0.09)
    f.ball((0, 1.585, -0.01), (0.285, 0.066, 0.145), k=0.08)
    for side in (-1, 1):
        f.path(np.array([[side * 0.285, 1.55, 0.0], [side * 0.395, 1.42, 0.0], [side * 0.49, 1.24, 0.0], [side * 0.545, 1.08, 0.016]] if side < 0 else [[side * 0.285, 1.55, 0.0], [side * 0.395, 1.42, 0.0], [side * 0.45, 1.31, 0.0]], np.float32),
               np.array([0.112, 0.102, 0.09, 0.08] if side < 0 else [0.112, 0.102, 0.09]), k=0.05)
    wrinkle(f, 0.012, 34, 3)
    wrinkle(f, 0.004, 105, 4)
    # The sleeve torn away above the elbow on the machine side.
    jagged_cut(f, (0.52, 1.26, 0.0), (0.11, 0.1, 0.13), 5)
    # The chest torn open onto the machine.
    jagged_cut(f, (0.0, 1.405, 0.215), (0.155, 0.155, 0.125), 8)
    f.cut_ball((0.0, 1.405, 0.0), (0.205, 0.2, 0.18), k=0.0)
    # Room for the neck.
    f.cut_ball((0, 1.65, -0.01), (0.08, 0.1, 0.08), k=0.0)
    return f


def inside_chest(p):
    return ((p[:, 0] / 0.212) ** 2 + ((p[:, 1] - 1.405) / 0.208) ** 2 + ((p[:, 2]) / 0.188) ** 2) < 1


def paint_torso(p, n, ao):
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    weave = triplanar(p, n, "canvas_cream", 0.42)
    weave = 0.42 + 0.58 * np.clip(weave, 0.2, 1.75)
    grime = noise3(p, 9, 81)
    fine = noise3(p, 62, 82, 3)
    # Wide vertical stripes on the body, rings on the sleeves.
    angle = np.arctan2(x, z + 0.02)
    wobble = (noise3(p, 7, 83, 2) - 0.5) * 0.28
    body = ((angle + wobble) / (2 * np.pi) * 14) % 1 < 0.5
    arm = (np.abs(x) - 0.3) * 0.72 + (1.56 - y) * 0.68
    rings = ((arm + wobble * 0.08) * 11.5) % 1 < 0.5
    on_arm = np.abs(x) > 0.36
    band = np.where(on_arm, rings, body)
    scarlet = np.array([0.13, 0.014, 0.018], np.float32)
    cream = np.array([0.23, 0.185, 0.115], np.float32)
    color = np.where(band[:, None], scarlet, cream) * (0.62 + 0.6 * grime)[:, None] * weave[:, None]
    # Dye gone in patches; grease where hands, belly and years rubbed it.
    faded = smooth(0.54, 0.74, noise3(p, 5, 84, 3))[:, None]
    color = color * (1 - 0.3 * faded) + np.array([0.14, 0.105, 0.07], np.float32) * weave[:, None] * 0.3 * faded
    dirt = smooth(0.46, 0.78, grime * 0.55 + fine * 0.45)[:, None]
    color = color * (1 - 0.68 * dirt)
    drip = smooth(0.62, 0.8, noise3(p * [7.0, 0.42, 7.0], 11, 85, 3))[:, None] * (z[:, None] > 0.05)
    color = color * (1 - drip * 0.82) + np.array([0.05, 0.006, 0.006], np.float32) * drip * 0.82
    # Rotted through in places, and scorched around the tear.
    rot = smooth(0.8, 0.83, noise3(p, 30, 86, 3))[:, None]
    color = color * (1 - rot * 0.9)
    burn = 1 - smooth(0.13, 0.26, np.sqrt((x / 1.0) ** 2 + (y - 1.405) ** 2 + np.clip(0.2 - z, 0, 1) ** 2 * 0.2))
    color = color * (1 - burn[:, None] * (z[:, None] > 0.1) * 0.72)
    color[inside_chest(p)] = np.array([0.011, 0.008, 0.008], np.float32)
    return np.clip(color * ao[:, None] ** 1.4, 0, 1)


def sculpt_coat():
    """A ringmaster's frock coat, rotted and too tight across the shoulders, open down the front."""
    f = Field((-0.78, 0.42, -0.5), (0.78, 1.78, 0.46), 0.003)
    f.ball((0, 1.1, 0.025), (0.28, 0.32, 0.27))
    f.ball((0, 1.43, -0.005), (0.3, 0.27, 0.238), k=0.09)
    f.ball((0, 1.59, -0.01), (0.305, 0.07, 0.16), k=0.08)
    for side in (-1, 1):
        f.path(np.array([[side * 0.3, 1.56, 0.0], [side * 0.405, 1.42, 0.0], [side * 0.495, 1.24, 0.0], [side * 0.55, 1.1, 0.016]] if side < 0 else [[side * 0.3, 1.56, 0.0], [side * 0.405, 1.42, 0.0], [side * 0.455, 1.33, 0.0]], np.float32),
               np.array([0.116, 0.108, 0.098, 0.09] if side < 0 else [0.116, 0.108, 0.098]), k=0.05)
        # The tails, hanging down to the knees.
        f.ball((side * 0.125, 0.72, -0.2), (0.135, 0.42, 0.048), k=0.06)
    wrinkle(f, 0.0105, 32, 31)
    wrinkle(f, 0.0032, 110, 32)
    n = f.shape
    x = (f.low[0] + np.arange(n[0], dtype=np.float32) * f.voxel)[:, None, None]
    y = (f.low[1] + np.arange(n[1], dtype=np.float32) * f.voxel)[None, :, None]
    z = (f.low[2] + np.arange(n[2], dtype=np.float32) * f.voxel)[None, None, :]
    # A shell, not a solid.
    f.d = np.maximum(f.d, -(f.d + 0.011))
    # Open down the front, wider toward the waist, and nothing but tails below the waist.
    jag = np.sin(x * 120 + y * 70) * 0.008 + np.sin(y * 210 + x * 90) * 0.004
    opening = np.maximum(np.abs(x) - (0.06 + 0.5 * np.maximum(1.62 - y, 0)) + jag, -z)
    f.d = np.maximum(f.d, -opening)
    below = np.maximum(0.975 - y + jag, z + 0.09)
    f.d = np.maximum(f.d, -below)
    # The vent between the tails, and a rotted hem.
    hem = 0.56 + np.sin(x * 62) * 0.03 + np.sin(x * 171) * 0.014
    f.d = np.maximum(f.d, hem - y)
    f.d = np.maximum(f.d, -np.maximum(np.abs(x) - 0.016 - np.maximum(0.86 - y, 0) * 0.2, y - 0.98))
    jagged_cut(f, (0.52, 1.27, 0.0), (0.11, 0.1, 0.13), 35)
    return f


def paint_coat(p, n, ao):
    weave = 0.5 + 0.5 * np.clip(triplanar(p, n, "velvet", 0.3), 0.2, 1.8)
    grime = noise3(p, 11, 131)
    color = np.array([0.066, 0.008, 0.019], np.float32) * (0.55 + 0.8 * grime)[:, None] * weave[:, None]
    # Velvet goes pale where it has been rubbed.
    rubbed = smooth(0.62, 0.86, noise3(p, 85, 132, 4))[:, None]
    color = color * (1 - rubbed * 0.42) + np.array([0.105, 0.05, 0.05], np.float32) * weave[:, None] * rubbed * 0.42
    dirt = smooth(0.48, 0.76, noise3(p, 60, 133, 3))[:, None]
    color = color * (1 - dirt * 0.62)
    rot = smooth(0.82, 0.85, noise3(p, 30, 134, 3))[:, None]
    color = color * (1 - rot * 0.9)
    return np.clip(color * ao[:, None] ** 1.5, 0, 1)


def surface_line(field, points):
    return settle(field, points, steps=10)


def sculpt_trim(coat):
    """Gold braid down the lapels, tarnished nearly black, frogged across the chest, and on the shoulders."""
    f = Field((-0.5, 0.92, -0.2), (0.5, 1.72, 0.34), 0.0019)
    for side in (-1, 1):
        ys = np.linspace(1.58, 1.14, 15)
        edge = np.stack([side * (0.06 + 0.5 * np.maximum(1.62 - ys, 0) + 0.03), ys, 0.21 - 0.5 * np.maximum(1.5 - ys, 0) * 0.0 + 0.0 * ys], 1)
        f.path(surface_line(coat, edge), 0.0058, k=0.0015)
        # Knots of braid along the front.
        for y in (1.47, 1.38, 1.29, 1.2):
            at = surface_line(coat, [(side * (0.06 + 0.5 * (1.62 - y) + 0.052), y, 0.2)])[0]
            f.ball(at, (0.012, 0.009, 0.008), k=0.001)
        # An epaulette on the shoulder.
        shoulder = surface_line(coat, [(side * 0.355, 1.64, 0.0)])[0]
        f.ball(shoulder + [0, 0.008, 0], (0.066, 0.015, 0.058), k=0.008)
    return f


def sculpt_belt(coat):
    """A wide cracked leather belt under the coat, with a brass buckle that has seen better days."""
    f = Field((-0.36, 0.92, -0.34), (0.36, 1.12, 0.36), 0.0019)
    angles = np.linspace(0, 2 * np.pi, 49)
    for offset in (-0.016, 0.0, 0.016):
        ring = np.stack([np.sin(angles) * 0.283, np.full(49, 1.02 + offset), 0.025 + np.cos(angles) * 0.272], 1).astype(np.float32)
        f.path(ring, 0.0115, k=0.004)
    return f


def sculpt_buckle():
    f = Field((-0.09, 0.93, 0.26), (0.09, 1.11, 0.345), 0.0014)
    for a, b in (((-0.056, 0.98, 0.3), (0.056, 0.98, 0.3)), ((-0.056, 1.06, 0.3), (0.056, 1.06, 0.3)), ((-0.056, 0.98, 0.3), (-0.056, 1.06, 0.3)), ((0.056, 0.98, 0.3), (0.056, 1.06, 0.3))):
        f.capsule(a, b, 0.0078, k=0.0015)
    f.capsule((0, 1.02, 0.3), (0.056, 1.02, 0.307), 0.0052, k=0.001)
    return f


def fray(name, roots, directions, length, tint, seed, count=2, sides=3):
    """Loose threads hanging from a torn edge."""
    rng = np.random.default_rng(seed)
    vertices, faces, normals, colors, offset = [], [], [], [], 0
    for root, direction in zip(roots, directions):
        for _ in range(count):
            start = np.array(root, np.float32) + rng.normal(0, 0.006, 3).astype(np.float32)
            reach = length * rng.uniform(0.4, 1.2)
            steps = np.linspace(0, 1, 7, dtype=np.float32)
            bend = rng.normal(0, 0.012, 3).astype(np.float32)
            line = start + (np.array(direction, np.float32) * reach)[None] * steps[:, None] + bend[None] * (steps ** 2)[:, None] + np.array([0, -0.5, 0], np.float32)[None] * (reach * steps ** 2)[:, None]
            v, f, n = strand(line, np.linspace(0.0014, 0.0003, 7), sides)
            vertices.append(v); faces.append(f + offset); normals.append(n)
            colors.append(np.array(tint, np.float32)[None] * rng.uniform(0.55, 1.3) * np.ones((len(v), 1), np.float32))
            offset += len(v)
    return {"name": name, "v": np.vstack(vertices), "f": np.vstack(faces), "n": np.vstack(normals), "c": np.vstack(colors), "shine": 0.1}


def body_frays():
    rng = np.random.default_rng(141)
    out = []
    # The cuff of the coat and the torn sleeve above the machine arm.
    angles = rng.uniform(0, 6.28, 54)
    roots = [(-0.553 + np.cos(a) * 0.095, 1.105, 0.016 + np.sin(a) * 0.09) for a in angles]
    dirs = [(np.cos(a) * 0.35, -1, np.sin(a) * 0.35) for a in angles]
    out.append(fray("fray_cuff", roots, dirs, 0.085, (0.13, 0.03, 0.03), 142))
    angles = rng.uniform(0, 6.28, 54)
    roots = [(0.47 + np.cos(a) * 0.1, 1.31 + np.sin(a) * 0.04, np.sin(a) * 0.1) for a in angles]
    dirs = [(np.cos(a) * 0.3 + 0.2, -1, np.sin(a) * 0.3) for a in angles]
    out.append(fray("fray_arm", roots, dirs, 0.1, (0.13, 0.03, 0.03), 143))
    # The rotted hem of the tails.
    xs = rng.uniform(-0.25, 0.25, 70)
    roots = [(x, 0.57 + np.sin(x * 62) * 0.03, -0.205) for x in xs]
    dirs = [(0, -1, -0.1)] * len(xs)
    out.append(fray("fray_hem", roots, dirs, 0.11, (0.1, 0.022, 0.025), 144))
    # Threads off the ruff and the epaulettes: gold fringe.
    for side in (-1, 1):
        fringe = rng.uniform(0, 6.28, 30)
        roots = [(side * 0.355 + np.cos(a) * 0.062, 1.625, np.sin(a) * 0.054) for a in fringe]
        dirs = [(np.cos(a) * 0.3, -1, np.sin(a) * 0.3) for a in fringe]
        out.append(fray(f"fringe_{side}", roots, dirs, 0.075, (0.17, 0.115, 0.03), 145 + side, count=2))
    return out


def sculpt_ruff():
    """A wide pleated collar, gone limp and gray."""
    f = Field((-0.4, 1.47, -0.38), (0.4, 1.76, 0.37), 0.0024)
    for k in range(26):
        angle = k * 2 * np.pi / 26
        spread = 0.172 + 0.014 * (k % 2)
        center = (np.sin(angle) * spread, 1.625 - 0.012 * (k % 3) - 0.018 * abs(np.cos(angle)), -0.01 + np.cos(angle) * spread * 0.9)
        f.ball(center, (0.062, 0.05, 0.062), k=0.008)
    f.cut_ball((0, 1.655, -0.01), (0.1, 0.1, 0.1), k=0.004)
    wrinkle(f, 0.004, 95, 7)
    return f


def paint_cloth(p, n, ao, base, seed, scan_name="canvas_cream", size=0.42):
    weave = 0.42 + 0.58 * np.clip(triplanar(p, n, scan_name, size), 0.2, 1.75)
    grime = noise3(p, 12, seed)
    dirt = smooth(0.48, 0.78, grime * 0.5 + noise3(p, 58, seed + 1, 3) * 0.5)[:, None]
    color = np.array(base, np.float32) * (0.62 + 0.6 * grime)[:, None] * weave[:, None] * (1 - 0.66 * dirt)
    return np.clip(color * ao[:, None] ** 1.4, 0, 1)


def sculpt_buttons():
    """Three fat pompoms down the belly, and little bells on the collar, as his shoes already sound."""
    f = Field((-0.34, 0.86, -0.32), (0.34, 1.62, 0.42), 0.0016)
    for y, z, r in ((1.235, 0.283, 0.032), (1.1, 0.3, 0.036), (0.965, 0.285, 0.032)):
        f.ball((0, y, z), r)
    return f


def sculpt_bells():
    f = Field((-0.32, 1.48, -0.34), (0.32, 1.66, 0.34), 0.0014)
    for k in range(7):
        angle = (k - 3) * 0.52
        center = np.array((np.sin(angle) * 0.225, 1.568, np.cos(angle) * 0.2 - 0.01), np.float32)
        f.ball(center, 0.0205)
        f.cut_capsule(center + [-0.024, -0.009, 0], center + [0.024, -0.009, 0], 0.0026)
        f.ball(center + [0, 0.022, 0], 0.0048, k=0.002)
    return f


def sculpt_trousers():
    """Huge baggy trousers, gathered at the ankles."""
    f = Field((-0.5, 0.16, -0.38), (0.5, 0.94, 0.38), 0.003)
    f.ball((0, 0.84, 0.01), (0.262, 0.13, 0.215))
    for side in (-1, 1):
        f.path(np.array([[side * 0.13, 0.85, 0.0], [side * 0.165, 0.55, 0.012], [side * 0.16, 0.33, 0.01], [side * 0.155, 0.2, 0.005]], np.float32),
               np.array([0.158, 0.145, 0.122, 0.085]), k=0.05)
    wrinkle(f, 0.012, 30, 11)
    wrinkle(f, 0.0038, 100, 12)
    # The shin torn open on the -x side.
    jagged_cut(f, (-0.16, 0.39, 0.125), (0.1, 0.13, 0.08), 13)
    f.path(np.array([[-0.16, 0.6, 0.012], [-0.16, 0.2, 0.01]], np.float32), np.array([0.102, 0.068]), k=0.0, carve=True)
    return f


def paint_trousers(p, n, ao):
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    weave = 0.42 + 0.58 * np.clip(triplanar(p, n, "velvet", 0.36), 0.2, 1.75)
    grime = noise3(p, 10, 91)
    color = np.array([0.018, 0.019, 0.027], np.float32) * (0.58 + 0.75 * grime)[:, None] * weave[:, None]
    # Big faded polka dots.
    cell = 0.17
    q = (p + np.array([0.03, 0.02, 0.05], np.float32)) / cell
    near = np.linalg.norm(q - np.round(q), axis=1) * cell
    dots = (1 - smooth(0.04, 0.046, near + (noise3(p, 90, 92, 2) - 0.5) * 0.006))[:, None]
    color = color * (1 - dots * 0.8) + np.array([0.075, 0.016, 0.02], np.float32) * weave[:, None] * (0.55 + 0.6 * grime)[:, None] * dots * 0.8
    # A square patch sewn over the +x knee.
    patch = (np.abs(x - 0.17) < 0.08) & (np.abs(y - 0.5) < 0.08) & (z > 0.05)
    color[patch] = np.array([0.1, 0.075, 0.045], np.float32) * (0.55 + 0.7 * grime[patch])[:, None] * weave[patch][:, None]
    stitch = (np.abs(np.maximum(np.abs(x - 0.17) / 0.08, np.abs(y - 0.5) / 0.08) - 0.98) < 0.02) & (np.sin((x + y) * 310) > 0) & (z > 0.05)
    color[stitch] = np.array([0.32, 0.27, 0.18], np.float32)
    dirt = smooth(0.46, 0.78, grime * 0.5 + noise3(p, 60, 93, 3) * 0.5)[:, None]
    low = (1 - smooth(0.16, 0.5, y))[:, None]
    color = color * (1 - np.clip(0.56 * dirt + 0.3 * low, 0, 0.9)) + np.array([0.05, 0.035, 0.018], np.float32) * low * 0.25
    return np.clip(color * ao[:, None] ** 1.4, 0, 1)


def sculpt_frill(center, radius, count, lobes):
    """A ruffle around a wrist or an ankle."""
    center = np.array(center, np.float32)
    m = radius + lobes + 0.05
    f = Field(center - m, center + m, 0.0024)
    for k in range(count):
        angle = k * 2 * np.pi / count
        at = center + np.array((np.cos(angle) * radius, (k % 2) * 0.012 - 0.006, np.sin(angle) * radius), np.float32)
        f.ball(at, (lobes, lobes * 0.72, lobes), k=0.005)
    return f


def sculpt_shoes():
    """Enormous shoes, split along the welts, curling up at the toes."""
    f = Field((-0.44, -0.01, -0.22), (0.44, 0.26, 0.5), 0.0025)
    for side in (-1, 1):
        f.ball((side * 0.165, 0.084, 0.235), (0.112, 0.09, 0.19))
        f.ball((side * 0.165, 0.074, -0.04), (0.085, 0.072, 0.1), k=0.08)
        f.capsule((side * 0.16, 0.1, -0.015), (side * 0.155, 0.24, 0.0), 0.08, 0.072, k=0.03)
        f.ball((side * 0.165, 0.135, 0.34), (0.078, 0.05, 0.088), k=0.05)
        # Creases across the toe.
        for k, z in enumerate((0.12, 0.17, 0.22)):
            f.path(np.array([[side * 0.075, 0.13 + 0.02 * k, z], [side * 0.165, 0.17, z + 0.012], [side * 0.255, 0.13 + 0.02 * k, z]], np.float32), 0.0052, k=0.012, carve=True)
    # Flat on the floor, with a welt line.
    y = (f.low[1] + np.arange(f.shape[1], dtype=np.float32) * f.voxel)[None, :, None]
    f.d = np.maximum(f.d, 0.004 - y)
    return f


def paint_shoes(p, n, ao):
    grime = noise3(p, 16, 101)
    leather = 0.65 + 0.35 * np.clip(triplanar(p, n, "dirty_carpet", 0.3), 0.3, 1.5)
    color = np.array([0.05, 0.01, 0.012], np.float32) * (0.5 + 0.85 * grime)[:, None] * leather[:, None]
    scuff = smooth(0.66, 0.78, noise3(p, 62, 102, 3))[:, None]
    color = color * (1 - scuff * 0.45) + np.array([0.09, 0.065, 0.04], np.float32) * scuff * 0.45
    sole = p[:, 1] < 0.03
    color[sole] = np.array([0.02, 0.016, 0.014], np.float32) * (0.7 + 0.7 * grime[sole])[:, None]
    return np.clip(color * ao[:, None] ** 1.4, 0, 1)


def sculpt_glove():
    """A fat four-fingered hand in a stained glove."""
    wrist = np.array((-0.56, 0.975, 0.03), np.float32)
    f = Field(wrist + [-0.15, -0.28, -0.12], wrist + [0.15, 0.08, 0.15], 0.0018)
    f.ball(wrist + [0, -0.075, 0.004], (0.062, 0.073, 0.036))
    f.capsule(wrist + [0, 0.06, 0], wrist + [0, -0.02, 0], 0.062, 0.052, k=0.02)
    for k, (dx, length, dz) in enumerate(((-0.044, 0.1, 0.008), (-0.014, 0.115, 0.012), (0.018, 0.112, 0.012), (0.048, 0.088, 0.006))):
        root = wrist + [dx, -0.135, 0.0065]
        tip = root + [dx * 0.42, -length, dz + 0.016]
        middle = (root + tip) / 2 + np.array([0, 0, 0.009], np.float32)
        f.path(np.array([root, middle, tip], np.float32), np.array([0.0205, 0.0185, 0.0165]), k=0.008)
    f.path(np.array([wrist + [-0.045, -0.06, 0.032], wrist + [-0.09, -0.098, 0.052], wrist + [-0.1, -0.15, 0.056]], np.float32), np.array([0.024, 0.02, 0.017]), k=0.012)
    wrinkle(f, 0.0026, 150, 21)
    return f


def sculpt_robot_arm():
    """The whole lower arm on +x is bare machine: bones, pistons, and a hand of steel fingers."""
    f = Field((0.35, 0.62, -0.17), (0.73, 1.38, 0.22), 0.0015)
    elbow = np.array((0.45, 1.29, 0.0), np.float32)
    wrist = np.array((0.55, 0.97, 0.03), np.float32)
    f.ball(elbow, 0.05)
    f.cylinder(elbow, wrist, 0.026, 0.002)
    for t in (0.14, 0.86):
        at = elbow + (wrist - elbow) * t
        axis = (wrist - elbow) / np.linalg.norm(wrist - elbow)
        f.cylinder(at - axis * 0.012, at + axis * 0.012, 0.05, 0.0035)
    half = (elbow + wrist) / 2
    side = np.array((0.048, 0.015, 0.026), np.float32)
    f.cylinder(elbow + side, half + side, 0.0185, 0.002)
    f.cylinder(half + side - (wrist - elbow) * 0.03, wrist + side, 0.0095, 0.0012)
    f.cylinder(half + side - (wrist - elbow) * 0.012, half + side + (wrist - elbow) * 0.012, 0.023, 0.002)
    other = np.array((-0.046, -0.012, -0.022), np.float32)
    f.cylinder(elbow + other, wrist + other, 0.0092, 0.0012)
    f.ball(wrist, 0.03)
    down = np.array((0.14, -0.99, 0.02), np.float32)
    down /= np.linalg.norm(down)
    right = np.array((0.99, 0.14, 0.0), np.float32)
    palm = wrist + down * 0.068
    f.cylinder(palm + right * -0.044, palm + right * 0.044, 0.042, 0.009)
    for offset in (-0.03, 0.0, 0.03):
        f.capsule(wrist + right * offset * 0.4, palm + right * offset + down * 0.03 + np.array((0, 0, 0.018), np.float32), 0.0058, k=0.002)
    for k, (dx, length) in enumerate(((-0.036, 0.051), (-0.012, 0.06), (0.012, 0.058), (0.035, 0.047))):
        joint = palm + right * dx + down * 0.034
        for segment in range(3):
            following = joint + (down + right * dx * 3.2 + np.array((0, 0, 0.14 * segment), np.float32)) * length * (0.88 ** segment)
            f.ball(joint, 0.0088, k=0.001)
            f.capsule(joint, following, 0.0061, 0.0052, k=0.001)
            joint = following
        f.capsule(joint, joint + (down + np.array((0, 0, 0.45), np.float32)) * 0.034, 0.0048, 0.0006, k=0.001)
    thumb = palm - right * 0.05 + np.array((0, 0, 0.02), np.float32)
    f.ball(thumb, 0.01)
    f.path(np.array([thumb, thumb + [-0.034, -0.03, 0.022], thumb + [-0.046, -0.07, 0.04], thumb + [-0.05, -0.1, 0.05]], np.float32), np.array([0.0065, 0.0058, 0.005, 0.0008]), k=0.001)
    return f


def sculpt_chest_machine():
    """Spine, ribs and gears inside the torn chest. The core is separate, for it glows."""
    f = Field((-0.22, 1.17, -0.13), (0.22, 1.65, 0.24), 0.0019)
    f.cylinder((0, 1.2, 0.035), (0, 1.62, 0.035), 0.027, 0.002)
    for y in np.arange(1.23, 1.62, 0.052):
        f.cylinder((0, y - 0.013, 0.035), (0, y + 0.013, 0.035), 0.041, 0.0045)
    for y in (1.28, 1.335, 1.39, 1.445, 1.5, 1.555):
        for side in (-1, 1):
            arc = [(side * (0.03 + 0.135 * np.sin(a)), y - 0.018 * np.sin(a), 0.035 + 0.088 * np.sin(a * 1.45)) for a in np.linspace(0, 1.62, 11)]
            f.path(np.array(arc, np.float32), np.linspace(0.0125, 0.009, 11), k=0.0015)
    for center, r in (((-0.085, 1.49, 0.06), 0.04), ((0.09, 1.31, 0.06), 0.032)):
        c = np.array(center, np.float32)
        axis = np.array((0, 0, 1), np.float32)
        f.cylinder(c - axis * 0.008, c + axis * 0.008, r, 0.0016)
        for tooth in range(12):
            a = tooth * np.pi / 6
            spoke = np.array((np.cos(a), np.sin(a), 0), np.float32)
            f.capsule(c + spoke * (r - 0.002), c + spoke * (r + 0.009), 0.0055, 0.0042, k=0.001)
        f.cylinder(c + axis * 0.008, c + axis * 0.018, r * 0.25, 0.001)
    for path in ([[-0.02, 1.54, 0.065], [-0.085, 1.45, 0.12], [-0.11, 1.33, 0.1], [-0.05, 1.27, 0.065]],
                 [[0.03, 1.5, 0.065], [0.08, 1.44, 0.125], [0.115, 1.4, 0.1], [0.07, 1.25, 0.065]]):
        f.path(np.array(path, np.float32), 0.0085, k=0.004)
    return f


def sculpt_core():
    f = Field((-0.1, 1.3, 0.03), (0.1, 1.5, 0.2), 0.0017)
    f.ball((0, 1.4, 0.112), 0.042)
    return f


def sculpt_cage():
    f = Field((-0.1, 1.3, 0.03), (0.1, 1.5, 0.2), 0.0015)
    for a in np.linspace(0, 2 * np.pi, 8, endpoint=False):
        at = np.array((np.cos(a), 1.4 / 1.0, np.sin(a)), np.float32)
        ring = np.array((np.cos(a) * 0.054, 1.4 + np.sin(a) * 0.054, 0.088), np.float32)
        out = ring + np.array((np.cos(a) * 0.012, np.sin(a) * 0.012, 0.038), np.float32)
        f.capsule(ring, out, 0.0044, k=0.001)
    angles = np.linspace(0, 2 * np.pi, 33)
    f.path(np.stack([np.cos(angles) * 0.062, 1.4 + np.sin(angles) * 0.062, np.full(33, 0.086)], 1).astype(np.float32), 0.0058, k=0.001)
    return f


def sculpt_shin_machine():
    f = Field((-0.3, 0.14, -0.09), (-0.02, 0.62, 0.17), 0.0017)
    knee = np.array((-0.16, 0.54, 0.035), np.float32)
    ankle = np.array((-0.16, 0.2, 0.03), np.float32)
    f.ball(knee, 0.055)
    f.ball(ankle, 0.043)
    f.cylinder(knee, ankle, 0.029, 0.002)
    for t in (0.2, 0.5, 0.8):
        at = knee + (ankle - knee) * t
        f.cylinder(at + [0, 0.011, 0], at - [0, 0.011, 0], 0.04, 0.0035)
    side = np.array((0.058, 0, 0.034), np.float32)
    half = (knee + ankle) / 2
    f.cylinder(knee + side, half + side, 0.021, 0.002)
    f.cylinder(half + side + [0, 0.02, 0], ankle + side, 0.011, 0.0012)
    f.cylinder(half + side + [0, 0.012, 0], half + side - [0, 0.012, 0], 0.026, 0.0022)
    f.path(np.array([knee + [-0.042, -0.03, 0.016], half + [-0.066, 0, 0.018], ankle + [-0.044, 0.03, 0.018]], np.float32), 0.0078, k=0.002)
    return f


def sculpt_key():
    """The winding key in his back, a brass butterfly as wide as his head. It turns when he walks."""
    f = Field((-0.2, 1.22, -0.48), (0.2, 1.56, -0.15), 0.0018)
    y = 1.39
    f.cylinder((0, y, -0.16), (0, y, -0.36), 0.021, 0.002)
    f.cylinder((0, y, -0.24), (0, y, -0.26), 0.04, 0.003)
    f.cylinder((0, y, -0.36), (0, y, -0.39), 0.032, 0.003)
    for side in (-1, 1):
        for a in np.linspace(0, 2 * np.pi, 25)[:-1]:
            at = np.array((side * (0.095 + np.cos(a) * 0.068), y + np.sin(a) * 0.105, -0.385), np.float32)
            b = a + 2 * np.pi / 24
            next_at = np.array((side * (0.095 + np.cos(b) * 0.068), y + np.sin(b) * 0.105, -0.385), np.float32)
            f.capsule(at, next_at, 0.0125, k=0.001)
        f.capsule((side * 0.02, y, -0.385), (side * 0.04, y, -0.385), 0.017, k=0.004)
    return f


def paint_metal(p, n, ao, base=(0.082, 0.082, 0.087), rust_at=0.6):
    wear = np.clip(triplanar(p, n, "metal_rusty", 0.5), 0.35, 1.7)
    grime = noise3(p, 45, 111, 4)
    rust = smooth(rust_at, rust_at + 0.2, noise3(p, 24, 112, 4))[:, None]
    color = np.array(base, np.float32) * (0.55 + 0.75 * grime)[:, None] * wear[:, None]
    color = color * (1 - rust) + np.array([0.09, 0.036, 0.016], np.float32) * wear[:, None] * rust
    return np.clip(color * ao[:, None] ** 1.3, 0, 1)


def body_parts():
    parts = []

    def add(name, field, painter, shine=0.15):
        parts.append(part_from(name, field, painter, shine))

    coat = sculpt_coat()
    add("torso", sculpt_torso(), paint_torso, 0.08)
    add("coat", coat, paint_coat, 0.22)
    add("trim", sculpt_trim(coat), lambda p, n, ao: paint_metal(p, n, ao, (0.2, 0.135, 0.04), 0.66), 0.9)
    add("belt", sculpt_belt(coat), lambda p, n, ao: paint_cloth(p, n, ao, (0.026, 0.018, 0.015), 147, "dirty_carpet", 0.22), 2.6)
    add("buckle", sculpt_buckle(), lambda p, n, ao: paint_metal(p, n, ao, (0.24, 0.16, 0.05), 0.7), 1.6)
    parts.extend(body_frays())
    add("trousers", sculpt_trousers(), paint_trousers, 0.08)
    add("ruff", sculpt_ruff(), lambda p, n, ao: paint_cloth(p, n, ao, (0.15, 0.125, 0.085), 121), 0.05)
    add("buttons", sculpt_buttons(), lambda p, n, ao: paint_cloth(p, n, ao, (0.13, 0.014, 0.018), 123, "velvet", 0.1), 0.05)
    add("bells", sculpt_bells(), lambda p, n, ao: paint_metal(p, n, ao, (0.24, 0.16, 0.05), 0.66), 1.2)
    add("shoes", sculpt_shoes(), paint_shoes, 2.2)
    add("frill_l", sculpt_frill((-0.155, 0.2, 0.005), 0.088, 18, 0.04), lambda p, n, ao: paint_cloth(p, n, ao, (0.13, 0.105, 0.07), 125), 0.05)
    add("frill_r", sculpt_frill((0.155, 0.2, 0.005), 0.088, 18, 0.04), lambda p, n, ao: paint_cloth(p, n, ao, (0.13, 0.105, 0.07), 126), 0.05)
    add("frill_w", sculpt_frill((-0.556, 1.0, 0.03), 0.072, 16, 0.036), lambda p, n, ao: paint_cloth(p, n, ao, (0.13, 0.105, 0.07), 127), 0.05)
    add("glove", sculpt_glove(), lambda p, n, ao: paint_cloth(p, n, ao, (0.17, 0.15, 0.105), 128, "canvas_cream", 0.2), 0.12)
    # Hands too big for him.
    wrist = np.array((-0.56, 1.0, 0.03), np.float32)
    parts[-1]["v"] = wrist + (parts[-1]["v"] - wrist) * 1.32
    add("robot_arm", sculpt_robot_arm(), paint_metal, 1.3)
    add("chest_machine", sculpt_chest_machine(), paint_metal, 1.3)
    add("cage", sculpt_cage(), lambda p, n, ao: paint_metal(p, n, ao, (0.24, 0.16, 0.05), 0.68), 1.2)
    add("core", sculpt_core(), lambda p, n, ao: np.tile(np.array([1.0, 0.16, 0.05], np.float32), (len(p), 1)) * (0.52 + 0.75 * n[:, 2:3].clip(0, 1)), 1.4)
    add("shin_machine", sculpt_shin_machine(), paint_metal, 1.3)
    add("key", sculpt_key(), lambda p, n, ao: paint_metal(p, n, ao, (0.24, 0.16, 0.05), 0.68), 1.2)
    return parts


def splat(parts, yaw, width, height, center, span, key=(0.55, -0.1, 0.72), rim=(-0.75, 0.5, -0.45), radius=1):
    """A fast full-figure picture: every vertex drawn as a small dot, nearest ones on top."""
    a = np.radians(yaw)
    key = np.array(key, np.float32) / np.linalg.norm(key)
    rim = np.array(rim, np.float32) / np.linalg.norm(rim)
    half = (key + np.array([0, 0, 1], np.float32))
    half /= np.linalg.norm(half)
    image = np.full((height, width, 3), 0.0016, np.float32)
    depth = np.full((height, width), -1e9, np.float32)
    scale = height / span
    for part in parts:
        v = part["v"] - np.array(center, np.float32)
        n = part["n"]
        x = v[:, 0] * np.cos(a) + v[:, 2] * np.sin(a)
        z = -v[:, 0] * np.sin(a) + v[:, 2] * np.cos(a)
        nx = n[:, 0] * np.cos(a) + n[:, 2] * np.sin(a)
        nz = -n[:, 0] * np.sin(a) + n[:, 2] * np.cos(a)
        normal = np.stack([nx, n[:, 1], nz], 1)
        keep = nz > -0.05
        diffuse = np.clip(normal @ key, 0, 1)[:, None] * np.array([1.25, 1.02, 0.82], np.float32)
        back = np.clip(normal @ rim, 0, 1)[:, None] * np.array([0.2, 0.3, 0.55], np.float32)
        specular = np.clip(normal @ half, 0, 1)[:, None] ** 42 * 0.26 * part.get("shine", 0.3)
        lit = part["c"] * (diffuse + back + 0.09) + specular * (part["c"].mean(1, keepdims=True) * 0.8 + 0.15)
        order = np.argsort(z[keep])
        sx = np.round(width / 2 + x[keep][order] * scale).astype(np.int32)
        sy = np.round(height / 2 - v[keep, 1][order] * scale).astype(np.int32)
        zz = z[keep][order]
        ll = lit[keep][order]
        for dx in range(-radius, radius + 1):
            for dy in range(-radius, radius + 1):
                px, py = sx + dx, sy + dy
                inside = (px >= 0) & (px < width) & (py >= 0) & (py < height)
                ix, iy = px[inside], py[inside]
                bias = zz[inside] - 0.0014 * (abs(dx) + abs(dy))
                near = bias > depth[iy, ix]
                image[iy[near], ix[near]] = ll[inside][near]
                depth[iy[near], ix[near]] = bias[near]
    return Image.fromarray((np.clip(image, 0, 1) ** (1 / 2.2) * 255).astype(np.uint8))


def moved(parts, offset=None, flip=False, size=1.0, wide=1.0):
    out = []
    for part in parts:
        turned = dict(part)
        v = part["v"].copy()
        n = part["n"].copy()
        if flip:
            v[:, 1] = -v[:, 1]
            v[:, 0] = -v[:, 0]
            n[:, 1] = -n[:, 1]
            n[:, 0] = -n[:, 0]
        turned["v"] = v * size * np.array((wide, 1.0, 1.0), np.float32) + (0 if offset is None else np.array(offset, np.float32))
        turned["n"] = n
        out.append(turned)
    return out


# ------------------------------------------------------------------ the mallet

def sculpt_mallet_head():
    """A fairground high-striker's mallet: an iron-bound barrel of oak, dented with years of hitting."""
    f = Field((-0.25, -0.16, -0.16), (0.25, 0.16, 0.16), 0.0018)
    f.cylinder((-0.19, 0, 0), (0.19, 0, 0), 0.118, 0.011)
    # The barrel swells in the middle.
    f.ball((0, 0, 0), (0.17, 0.126, 0.126), k=0.02)
    # Dents in the faces.
    for at, r in (((0.2, 0.035, 0.03), 0.03), ((0.204, -0.05, -0.012), 0.022), ((-0.204, 0.02, -0.045), 0.028), ((-0.206, -0.04, 0.05), 0.02)):
        f.cut_ball(at, r, k=0.008)
    return f


def sculpt_mallet_bands():
    f = Field((-0.25, -0.16, -0.16), (0.25, 0.16, 0.16), 0.0018)
    for x in (-0.178, -0.092, 0.092, 0.178):
        wide = 0.0135
        radius = 0.1215 if abs(x) > 0.12 else 0.1295
        f.cylinder((x - wide, 0, 0), (x + wide, 0, 0), radius, 0.003)
        for k in range(10):
            angle = k * np.pi / 5
            f.ball((x, np.cos(angle) * (radius + 0.001), np.sin(angle) * (radius + 0.001)), 0.0058, k=0.002)
    return f


def sculpt_handle():
    f = Field((-0.05, -1.05, -0.05), (0.05, 0.13, 0.05), 0.0018)
    f.capsule((0, 0.12, 0), (0, -1.0, 0), 0.0215, 0.0185)
    f.ball((0, -1.02, 0), (0.028, 0.02, 0.028), k=0.01)
    # A slight warp.
    for k in range(34):
        y = -0.72 - k * 0.0072
        f.path(np.array([[np.cos(k * 1.1) * 0.0212, y, np.sin(k * 1.1) * 0.0212], [np.cos(k * 1.1 + 1.3) * 0.0212, y - 0.0065, np.sin(k * 1.1 + 1.3) * 0.0212]], np.float32), 0.0048, k=0.0012)
    return f


def paint_mallet_head(p, ao):
    x, y, z = p[:, 0], p[:, 1], p[:, 2]
    angle = np.arctan2(z, y)
    # Oak staves, painted in the circus's colors.
    stave = np.abs(((angle / (2 * np.pi) * 18) % 1) - 0.5)
    grain = noise3(p * [0.16, 1, 1], 110, 41, 4)
    wood = np.array([0.075, 0.04, 0.02], np.float32) * (0.5 + 0.95 * grain)[:, None]
    red = np.array([0.12, 0.011, 0.014], np.float32) * (0.68 + 0.5 * grain)[:, None]
    cream = np.array([0.2, 0.15, 0.085], np.float32) * (0.68 + 0.5 * grain)[:, None]
    band = np.where(np.floor((angle / (2 * np.pi) + 0.5) * 8).astype(int) % 2 == 0, 1, 0)[:, None]
    color = red * band + cream * (1 - band)
    # Paint worn off the faces and the raised middle.
    worn = smooth(0.46, 0.66, noise3(p, 18, 42, 4) + 0.5 * smooth(0.13, 0.2, np.abs(x)) - 0.12)[:, None]
    color = color * (1 - worn) + wood * worn
    seam = (1 - smooth(0.0, 0.03, stave))[:, None]
    color = color * (1 - seam * 0.8)
    # What it has been used for.
    splash = smooth(0.66, 0.74, noise3(p, 30, 43, 4))[:, None] * smooth(0.13, 0.2, x)[:, None]
    color = color * (1 - splash * 0.9) + np.array([0.075, 0.006, 0.006], np.float32) * splash * 0.9
    # Years of grime over all of it.
    color = color * (0.42 + 0.34 * noise3(p, 24, 44, 4))[:, None]
    # The striking faces are bare end grain, rings of the old oak, black from the blows.
    rings = 0.5 + 0.5 * np.sin(np.hypot(y + 0.03, z - 0.02) * 260 + noise3(p, 40, 45, 3) * 7)
    end = np.array([0.03, 0.017, 0.01], np.float32) * (0.45 + 0.75 * rings)[:, None] * (0.6 + 0.8 * noise3(p, 90, 46, 3))[:, None]
    face = smooth(0.188, 0.196, np.abs(x))[:, None]
    color = color * (1 - face) + end * face
    return np.clip(color * ao[:, None], 0, 1)


def mallet_parts():
    parts = []

    def add(name, field, painter):
        parts.append(part_from(name, field, painter, 0.5))

    add("mallet_head", sculpt_mallet_head(), lambda p, n, ao: paint_mallet_head(p, ao))
    add("mallet_bands", sculpt_mallet_bands(), lambda p, n, ao: paint_metal(p, n, ao, (0.11, 0.11, 0.115), 0.58))
    add("mallet_handle", sculpt_handle(), lambda p, n, ao: paint_handle(p, ao))
    return parts


def paint_handle(p, ao):
    grain = noise3(p * [1, 0.12, 1], 150, 51, 4)
    color = np.array([0.04, 0.022, 0.012], np.float32) * (0.5 + 1.0 * grain)[:, None]
    rope = (p[:, 1] < -0.715) & (p[:, 1] > -0.97)
    color[rope] = np.array([0.16, 0.12, 0.07], np.float32) * (0.5 + noise3(p[rope], 300, 52, 2))[:, None]
    return np.clip(color * ao[:, None], 0, 1)


# ------------------------------------------------------------------ pictures

def posed(parts, open_jaw):
    """The parts, with the jaw shut or fallen open."""
    out = []
    angle = np.radians(JAW_OPEN_DEGREES)
    for part in parts:
        if (part.get("only_open") and not open_jaw) or (part.get("only_rest") and open_jaw):
            continue
        if part.get("jaw") and open_jaw:
            turned = dict(part)
            turned["v"] = rotate_jaw(part["v"], angle)
            n = part["n"].copy()
            c, s = np.cos(angle), np.sin(angle)
            n[:, 1] = part["n"][:, 1] * c - part["n"][:, 2] * s
            n[:, 2] = part["n"][:, 1] * s + part["n"][:, 2] * c
            turned["n"] = n
            out.append(turned)
        else:
            out.append(part)
    return out


def render(parts, yaw, width, height, center, span, pitch=0.0, key=(0.55, -0.12, 0.72), rim=(-0.75, 0.5, -0.45)):
    """A portrait with smooth shading: a warm key, a cold rim, a hint of specular."""
    a, b = np.radians(yaw), np.radians(pitch)
    image = np.full((height, width, 3), 0.0016, np.float32)
    depth = np.full((height, width), -1e9, np.float32)
    scale = height / span
    key = np.array(key, np.float32) / np.linalg.norm(key)
    rim = np.array(rim, np.float32) / np.linalg.norm(rim)
    view = np.array([0, 0, 1], np.float32)
    half = (key + view) / np.linalg.norm(key + view)
    for part in parts:
        v = part["v"] - np.array(center, np.float32)
        n = part["n"]
        for vector, name in ((v, "p"), (n, "n")):
            x = vector[:, 0] * np.cos(a) + vector[:, 2] * np.sin(a)
            z = -vector[:, 0] * np.sin(a) + vector[:, 2] * np.cos(a)
            y = vector[:, 1] * np.cos(b) - z * np.sin(b)
            z = vector[:, 1] * np.sin(b) + z * np.cos(b)
            if name == "p":
                px, py, pz = x, y, z
            else:
                nx, ny, nz = x, y, z
        sx = width / 2 + px * scale
        sy = height / 2 - py * scale
        faces = part["f"]
        for i0, i1, i2 in faces:
            x0, x1, x2, y0, y1, y2 = sx[i0], sx[i1], sx[i2], sy[i0], sy[i1], sy[i2]
            area = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
            if area >= 0 and False:
                continue
            if abs(area) < 1e-6:
                continue
            left, right = max(int(min(x0, x1, x2)), 0), min(int(max(x0, x1, x2)) + 1, width - 1)
            top, bottom = max(int(min(y0, y1, y2)), 0), min(int(max(y0, y1, y2)) + 1, height - 1)
            if left > right or top > bottom:
                continue
            gx, gy = np.meshgrid(np.arange(left, right + 1), np.arange(top, bottom + 1))
            l0 = ((y1 - y2) * (gx + 0.5 - x2) + (x2 - x1) * (gy + 0.5 - y2)) / area
            l1 = ((y2 - y0) * (gx + 0.5 - x2) + (x0 - x2) * (gy + 0.5 - y2)) / area
            l2 = 1 - l0 - l1
            inside = (l0 >= 0) & (l1 >= 0) & (l2 >= 0)
            if not inside.any():
                continue
            l0, l1, l2 = l0[inside], l1[inside], l2[inside]
            ix, iy = gx[inside], gy[inside]
            z = l0 * pz[i0] + l1 * pz[i1] + l2 * pz[i2]
            near = z > depth[iy, ix]
            if not near.any():
                continue
            normal = np.stack([l0 * nx[i0] + l1 * nx[i1] + l2 * nx[i2], l0 * ny[i0] + l1 * ny[i1] + l2 * ny[i2], l0 * nz[i0] + l1 * nz[i1] + l2 * nz[i2]], -1)
            normal /= np.linalg.norm(normal, axis=1, keepdims=True) + 1e-6
            if (normal[near, 2] < -0.25).all():
                continue
            color = l0[:, None] * part["c"][i0] + l1[:, None] * part["c"][i1] + l2[:, None] * part["c"][i2]
            diffuse = np.clip(normal @ key, 0, 1)[:, None] * np.array([1.25, 1.02, 0.82], np.float32)
            back = np.clip(normal @ rim, 0, 1)[:, None] * np.array([0.2, 0.3, 0.55], np.float32)
            specular = np.clip(normal @ half, 0, 1)[:, None] ** 42 * 0.26 * part.get("shine", 0.3)
            lit = color * (diffuse + back + 0.09) + specular * (color.mean(1, keepdims=True) * 0.8 + 0.15)
            image[iy[near], ix[near]] = lit[near]
            depth[iy[near], ix[near]] = z[near]
    return Image.fromarray((np.clip(image, 0, 1) ** (1 / 2.2) * 255).astype(np.uint8))


# ------------------------------------------------------------------ the game model

BUDGET = {"torso": 9000, "coat": 16000, "trousers": 8000, "ruff": 3500, "trim": 2500, "belt": 1500, "buckle": 500, "buttons": 500,
          "bells": 900, "shoes": 4500, "frill_l": 1600, "frill_r": 1600, "frill_w": 1400, "glove": 3000, "robot_arm": 9000,
          "chest_machine": 8000, "cage": 1200, "core": 500, "shin_machine": 3500, "key": 1800, "face": 10000, "jaw": 3000,
          "teeth_low": 900, "teeth_up": 900, "gum_low": 300, "gum_up": 300, "mouth_machine": 1800, "nose": 500, "eyes": 800,
          "fittings": 500, "hatch": 800, "hat": 2500, "machine": 3500, "drool": 200, "mallet_head": 5000, "mallet_bands": 4000,
          "mallet_handle": 2200}
METAL = {"robot_arm": 0.85, "chest_machine": 0.85, "cage": 0.9, "shin_machine": 0.85, "key": 0.9, "bells": 0.9, "buckle": 0.9, "trim": 0.75,
         "fittings": 0.8, "hatch": 0.8, "machine": 0.8, "mouth_machine": 0.6, "mallet_bands": 0.8}
STRIP = 64


def tangents(v, f, uv, n):
    """Per-vertex tangents from the UVs, to turn the field's normals into a normal map."""
    a, b, c = f[:, 0], f[:, 1], f[:, 2]
    e1, e2 = v[b] - v[a], v[c] - v[a]
    d1, d2 = uv[b] - uv[a], uv[c] - uv[a]
    det = d1[:, 0] * d2[:, 1] - d2[:, 0] * d1[:, 1]
    r = np.where(np.abs(det) > 1e-12, 1 / np.where(np.abs(det) > 1e-12, det, 1), 0)[:, None]
    t = (e1 * d2[:, 1:2] - e2 * d1[:, 1:2]) * r
    b_ = (e2 * d1[:, 0:1] - e1 * d2[:, 0:1]) * r
    T, B = np.zeros_like(v), np.zeros_like(v)
    for i in (a, b, c):
        np.add.at(T, i, t)
        np.add.at(B, i, b_)
    T -= n * (T * n).sum(1, keepdims=True)
    T /= np.linalg.norm(T, axis=1, keepdims=True) + 1e-9
    sign = np.sign((np.cross(n, T) * B).sum(1, keepdims=True))
    sign[sign == 0] = 1
    return T, np.cross(n, T) * sign


def bake_model(parts, strands, out, size=4096, prefix="pockets", share=None, dead=False):
    """Reduce every part, unwrap them on one atlas, and paint the atlas from the sculpt itself.
    share(name) scales a part's triangles; dead paints the eyes and the core with the power off."""
    import fast_simplification
    import xatlas

    atlas = xatlas.Atlas()
    lows = []
    for part in parts:
        target = max(int(BUDGET.get(part["name"], 1500) * (share(part["name"]) if share else 1)) // 2, 24)
        v, f = fast_simplification.simplify(part["v"].astype(np.float32), part["f"].astype(np.int32), target_count=target)
        # Props must stay light: remesh coarser the parts the reduction gets stuck on. (Never on the
        # rigged model: its mesh has to stay the one Mixamo rigged.)
        for coarser in (3, 6, 12) if share else ():
            if len(f) <= target * 2.5:
                break
            field = part["field"]
            v, f, _, _ = measure.marching_cubes(field.d[::coarser, ::coarser, ::coarser], 0.0, spacing=(field.voxel * coarser,) * 3)
            v = v.astype(np.float32) + np.asarray(field.low, np.float32)
            v, f = fast_simplification.simplify(v, f[:, ::-1].astype(np.int32), target_count=target)
        v = v.astype(np.float32)
        lows.append({"part": part, "v": v, "f": f.astype(np.uint32), "n": part["field"].normal_at(v)})
        atlas.add_mesh(lows[-1]["v"], lows[-1]["f"], lows[-1]["n"])
        print(f"  {part['name']}: {len(f)} triangles", flush=True)
    charts, pack = xatlas.ChartOptions(), xatlas.PackOptions()
    pack.resolution = size - STRIP
    pack.padding = 5
    pack.bilinear = True
    atlas.generate(chart_options=charts, pack_options=pack)
    print("  atlas", atlas.width, atlas.height, "charts", atlas.chart_count, flush=True)
    width, height = atlas.width + STRIP, atlas.height
    albedo = np.zeros((height, width, 3), np.float32)
    normal = np.zeros((height, width, 3), np.float32)
    normal[..., 2] = 1
    mask = np.zeros((height, width, 4), np.float32)
    emission = np.zeros((height, width, 3), np.float32)
    covered = np.zeros((height, width), bool)
    for index, low in enumerate(lows):
        mapping, faces, uvs = atlas[index]
        part = low["part"]
        v, n = low["v"][mapping], low["n"][mapping]
        uv = uvs.astype(np.float32)
        low.update(v=v, n=n, f=faces.astype(np.int64), uv=uv * np.array((atlas.width / width, 1.0), np.float32))
        T, B = tangents(v, low["f"], low["uv"], n)
        px = uv * np.array((atlas.width - 1, atlas.height - 1), np.float32)
        rows, cols, positions, frames = [], [], [], []
        for i0, i1, i2 in low["f"]:
            x0, y0, x1, y1, x2, y2 = px[i0, 0], px[i0, 1], px[i1, 0], px[i1, 1], px[i2, 0], px[i2, 1]
            area = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2)
            if abs(area) < 1e-6:
                continue
            gx, gy = np.meshgrid(np.arange(int(min(x0, x1, x2)), int(max(x0, x1, x2)) + 2), np.arange(int(min(y0, y1, y2)), int(max(y0, y1, y2)) + 2))
            l0 = ((y1 - y2) * (gx - x2) + (x2 - x1) * (gy - y2)) / area
            l1 = ((y2 - y0) * (gx - x2) + (x0 - x2) * (gy - y2)) / area
            l2 = 1 - l0 - l1
            inside = (l0 >= -0.02) & (l1 >= -0.02) & (l2 >= -0.02)
            if not inside.any():
                continue
            w = np.stack([l0[inside], l1[inside], l2[inside]], 1)[:, :, None].astype(np.float32)
            rows.append(gy[inside]); cols.append(gx[inside])
            positions.append((w * v[[i0, i1, i2]]).sum(1))
            frames.append(np.stack([(w * n[[i0, i1, i2]]).sum(1), (w * T[[i0, i1, i2]]).sum(1), (w * B[[i0, i1, i2]]).sum(1)], 1))
        rows, cols = np.concatenate(rows), np.concatenate(cols)
        keep = (rows >= 0) & (rows < height) & (cols >= 0) & (cols < atlas.width)
        rows, cols = rows[keep], cols[keep]
        p = np.concatenate(positions)[keep]
        frame = np.concatenate(frames)[keep]
        field = part["field"]
        # The sculpt's own surface and direction under each texel of the reduced mesh.
        fine = field.normal_at(p)
        p = p - fine * np.clip(field.sample(p), -0.01, 0.01)[:, None]
        fine = field.normal_at(p)
        ao = occlusion(field, p, fine)
        color = part["painter"](p, fine, ao)
        if dead and part["name"] in ("core", "eyes"):
            color = color * (0.1 if part["name"] == "core" else 0.45)
        albedo[rows, cols] = color
        N = frame[:, 0] / (np.linalg.norm(frame[:, 0], axis=1, keepdims=True) + 1e-9)
        Tt = frame[:, 1] / (np.linalg.norm(frame[:, 1], axis=1, keepdims=True) + 1e-9)
        Bt = frame[:, 2] / (np.linalg.norm(frame[:, 2], axis=1, keepdims=True) + 1e-9)
        normal[rows, cols] = np.stack([(fine * Tt).sum(1), (fine * Bt).sum(1), np.clip((fine * N).sum(1), 0.05, 1)], 1)
        mask[rows, cols, 0] = METAL.get(part["name"], 0.0)
        mask[rows, cols, 1] = 1.0
        smooth_value = np.clip(0.14 + 0.2 * part.get("shine", 0.1), 0.05, 0.86)
        mask[rows, cols, 3] = smooth_value
        if part["name"] == "core":
            emission[rows, cols] = color * 2.2
        elif part["name"] == "eyes":
            emission[rows, cols] = np.where(color.max(1, keepdims=True) > 0.05, color, 0) * 3
        covered[rows, cols] = True
        print(f"  painted {part['name']}: {len(rows)} texels", flush=True)
    # Bleed outward, so the seams of the atlas never show.
    index = ndimage.distance_transform_edt(~covered[:, :atlas.width], return_distances=False, return_indices=True)
    for image in (albedo, normal, mask, emission):
        image[:, :atlas.width] = image[:, :atlas.width][index[0], index[1]]
    # The strip of plain colors for hair and threads: one tint per tile, dark at the top.
    tints = [s["tint"] for s in strands]
    tile = height // max(len(tints), 1)
    for k, tint in enumerate(tints):
        ramp = np.linspace(0, 1, tile, dtype=np.float32)[:, None, None]
        albedo[k * tile:(k + 1) * tile, atlas.width:] = np.array(tint, np.float32) * ramp
        mask[k * tile:(k + 1) * tile, atlas.width:, 1] = 1
        mask[k * tile:(k + 1) * tile, atlas.width:, 3] = 0.2
    for k, s_ in enumerate(strands):
        level = np.clip(s_["c"].max(1) / max(max(s_["tint"]), 1e-4), 0, 1)
        s_["uv"] = np.stack([np.full(len(level), (atlas.width + STRIP / 2) / width), (k + 0.02 + level * 0.96) * tile / height], 1).astype(np.float32)
    out.mkdir(parents=True, exist_ok=True)

    def save(array, name, kind="JPEG"):
        image = Image.fromarray((np.clip(array, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB" if array.shape[2] == 3 else "RGBA")
        if kind == "JPEG":
            image.save(out / name, quality=92)
        else:
            image.save(out / name, optimize=True)

    # Painted colors are display values already, as the renderer expects of a base map: gamma encode them.
    save(np.clip(albedo, 0, 1) ** (1 / 2.2), f"{prefix}_albedo.jpg")
    save(normal * 0.5 + 0.5, f"{prefix}_normal.jpg")
    save(mask, f"{prefix}_mask.png", "PNG")
    if not dead:
        save(emission, f"{prefix}_emission.jpg")
    return lows


def write_obj(path, pieces):
    """One OBJ from pieces of (vertices, normals, uvs, faces)."""
    with open(path, "w") as f:
        f.write(f"mtllib pockets.mtl\no {path.stem}\nusemtl pockets\n")
        offset = 0
        body = []
        for v, n, uv, faces in pieces:
            # Every face turned the way the sculpt's surface looks, or the engine draws the inside.
            normal = np.cross(v[faces[:, 1]] - v[faces[:, 0]], v[faces[:, 2]] - v[faces[:, 0]])
            backwards = (normal * n[faces].mean(1)).sum(1) < 0
            faces = faces.copy()
            faces[backwards] = faces[backwards][:, ::-1]
            f.writelines(f"v {a:.5f} {b:.5f} {c:.5f}\n" for a, b, c in v)
            f.writelines(f"vt {a:.5f} {1 - b:.5f}\n" for a, b in uv)
            f.writelines(f"vn {a:.4f} {b:.4f} {c:.4f}\n" for a, b, c in n)
            for a, b, c in faces + offset + 1:
                body.append(f"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}\n")
            offset += len(v)
        f.writelines(body)


def export_model(out, project):
    head = head_parts()
    body = body_parts()
    mallet = mallet_parts()
    sculpted, strands = [], []
    def group_of(name, default):
        if name in ("jaw", "teeth_low", "gum_low"):
            return "jaw"
        return "key" if name == "key" else default
    for part, offset, size, flip, group in [(p, HEAD_AT, HEAD_SIZE, False, "body") for p in head] + [(p, None, 1.0, False, "body") for p in body] + [(p, (0.585, 0.135, 0.05), 1.0, True, "mallet") for p in mallet]:
        if part.get("only_open"):
            continue
        part = dict(part, offset=offset, size=size, flip=flip, group=group_of(part["name"], group))
        if "field" in part:
            sculpted.append(part)
        else:
            part["tint"] = tuple(np.percentile(part["c"], 99, axis=0).tolist())
            strands.append(part)
    lows = bake_model(sculpted, strands, out)

    def placed(part, v, n):
        v, n = v.copy(), n.copy()
        if part["flip"]:
            v[:, :2] *= -1
            n[:, :2] *= -1
        v = v * part["size"] + (0 if part["offset"] is None else np.array(part["offset"], np.float32))
        return v, n

    hinge = HEAD_AT + JAW_HINGE * HEAD_SIZE
    pivots = {"body": np.zeros(3), "jaw": np.array(hinge), "key": np.array((0.0, 1.39, -0.16)), "mallet": np.array((0.585, 0.985, 0.05))}
    pieces = {name: [] for name in pivots}
    for low in lows:
        part = low["part"]
        v, n = placed(part, low["v"], low["n"])
        pieces[part["group"]].append((v - pivots[part["group"]], n, low["uv"], low["f"]))
    for part in strands:
        v, n = placed(part, part["v"], part["n"])
        pieces["body"].append((v, n, part["uv"], part["f"]))
    (out / "pockets.mtl").write_text("newmtl pockets\nKd 1 1 1\nmap_Kd pockets_albedo.jpg\n")
    names = {"body": "pockets.obj", "jaw": "pockets_jaw.obj", "key": "pockets_key.obj", "mallet": "pockets_mallet.obj"}
    for group, name in names.items():
        write_obj(out / name, pieces[group])
        print(name, sum(len(p[3]) for p in pieces[group]), "triangles", flush=True)
    (out / "pivots.txt").write_text("".join(f"{k} {v[0]:.4f} {v[1]:.4f} {v[2]:.4f}\n" for k, v in pivots.items()))
    import zipfile
    with zipfile.ZipFile(out / "pockets_para_mixamo.zip", "w", zipfile.ZIP_DEFLATED) as archive:
        for name in ("pockets.obj", "pockets.mtl", "pockets_albedo.jpg"):
            archive.write(out / name, name)


def thin_strands(part, every):
    """Keeps one strand in every few: a prop does not need all his hair."""
    from scipy.sparse import coo_matrix
    from scipy.sparse.csgraph import connected_components
    f = part["f"]
    n = len(part["v"])
    edges = np.concatenate([f[:, :2], f[:, 1:]])
    graph = coo_matrix((np.ones(len(edges)), (edges[:, 0], edges[:, 1])), shape=(n, n))
    labels = connected_components(graph, directed=False)[1]
    kept = labels % every == 0
    faces = f[kept[f[:, 0]]]
    used, faces = np.unique(faces, return_inverse=True)
    return dict(part, v=part["v"][used], n=part["n"][used], c=part["c"][used], f=faces.reshape(-1, 3))


def export_props(out):
    """The dummies and spare heads standing around the circus: the same sculpt, light and with the power off."""
    head = [dict(p, offset=HEAD_AT, size=HEAD_SIZE, flip=False, group="head") for p in head_parts() if not p.get("only_open")]
    body = [dict(p, offset=None, size=1.0, flip=False, group="dummy") for p in body_parts() if p["name"] != "key"]
    sculpted, strands = [], []
    for part in head + body:
        if "field" in part:
            sculpted.append(part)
        else:
            part = thin_strands(part, 6 if part["name"] == "hair" else 3)
            part["tint"] = tuple(np.percentile(part["c"], 99, axis=0).tolist())
            strands.append(part)
    head_names = {p["name"] for p in head}
    lows = bake_model(sculpted, strands, out, size=2048, prefix="prop", share=lambda name: 0.2 if name in head_names else 0.09, dead=True)
    pieces = {"head": [], "dummy": []}

    def placed(part, v):
        v = v * part["size"]
        return v if part["group"] == "head" or part["offset"] is None else v + np.array(part["offset"], np.float32)

    for low in lows:
        part = low["part"]
        pieces[part["group"]].append((placed(part, low["v"]), low["n"], low["uv"], low["f"]))
    for part in strands:
        pieces[part["group"]].append((placed(part, part["v"]), part["n"], part["uv"], part["f"]))
    for group, name in (("head", "prop_head.obj"), ("dummy", "prop_dummy.obj")):
        write_obj(out / name, pieces[group])
        print(name, sum(len(p[3]) for p in pieces[group]), "triangles", flush=True)


def main():
    out = Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    wanted = sys.argv[2:] or ["all"]
    if "export" in wanted:
        export_model(out, None)
        return
    if "props" in wanted:
        export_props(out)
        return
    if "all" in wanted:
        head = head_parts()
        sheet = Image.new("RGB", (800 * 4, 1000))
        for i, (yaw, opened) in enumerate(((0, False), (30, False), (0, True), (-36, False))):
            sheet.paste(render(posed(head, opened), yaw, 800, 1000, (0, 0.03, 0.015), 0.5), (i * 800, 0))
        sheet.save(out / "cabeza.jpg", quality=92)
        mallet = mallet_parts()
        # He holds it by the grip in the machine hand, and its head drags on the floor.
        carried = moved(mallet, (0.585, 0.135, 0.05), flip=True)
        everyone = body_parts() + moved(posed(head, False), HEAD_AT, size=HEAD_SIZE, wide=HEAD_WIDE) + carried
        sheet = Image.new("RGB", (900 * 4, 1400))
        for i, yaw in enumerate((0, 35, 90, 180)):
            sheet.paste(splat(everyone, yaw, 900, 1400, (0.0, 1.13, 0.0), 2.4, radius=2), (i * 900, 0))
        sheet.save(out / "pockets_cuerpo.jpg", quality=92)
        sheet = Image.new("RGB", (1000 * 3, 1000))
        sheet.paste(splat(everyone, 0, 1000, 1000, (0.0, 1.62, 0.0), 1.08, radius=2), (0, 0))
        sheet.paste(splat(everyone, -28, 1000, 1000, (0.38, 1.02, 0.0), 1.1, radius=2), (1000, 0))
        sheet.paste(splat(everyone, 18, 1000, 1000, (0.0, 0.42, 0.1), 0.98, radius=2), (2000, 0))
        sheet.save(out / "pockets_detalle2.jpg", quality=92)
        return
    if "head" in wanted:
        parts = head_parts()
        sheet = Image.new("RGB", (800 * 4, 1000))
        for i, (yaw, opened) in enumerate(((0, False), (30, False), (0, True), (-36, False))):
            sheet.paste(render(posed(parts, opened), yaw, 800, 1000, (0, 0.03, 0.015), 0.5), (i * 800, 0))
        sheet.save(out / "cabeza.jpg", quality=92)
    if "mallet" in wanted:
        parts = mallet_parts()
        sheet = Image.new("RGB", (1000 * 2, 1000))
        sheet.paste(render(parts, 35, 1000, 1000, (0, -0.42, 0), 1.45, pitch=12), (0, 0))
        sheet.paste(render(parts, 62, 1000, 1000, (0, 0, 0), 0.62, pitch=16), (1000, 0))
        sheet.save(out / "mazo.jpg", quality=92)


if __name__ == "__main__":
    main()
