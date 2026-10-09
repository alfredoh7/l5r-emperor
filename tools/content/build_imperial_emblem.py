#!/usr/bin/env python3
"""Build src/L5R.Client/Art/clan-imperial.png, the Imperial Clan emblem (issue #771).

The input is Mendon's supplied Imperial mon (tools/content/art/imperial-clan-mon.png: a flat green silhouette cut from the Journey's End Keep scan, with stray coloured speckle along its edge).
Only its SHAPE is used: the alpha is thresholded, the speckle (every connected piece far smaller than the mon) is dropped and the edge is re-antialiased, so the mon is the one supplied, not a redraw.
It is then set as gold relief on green enamel inside a dark bronze bezel, the construction of the other clan emblems (clan-mantis.png, clan-spider.png): 1254 x 1254, RGBA, round, transparent outside.
The same file serves the clan placard and the battle placard (ClanPlaque.Emblem).

Needs numpy and scipy besides Pillow (publisher-side only; the game never runs this).
Usage: python tools/content/build_imperial_emblem.py [--mon PATH] [--output PATH] [--seed N]
"""
from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
import scipy.ndimage as ndi
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_MON = ROOT / "tools/content/art/imperial-clan-mon.png"
DEFAULT_OUTPUT = ROOT / "src/L5R.Client/Art/clan-imperial.png"
SIZE = 1254
CENTER = (SIZE - 1) / 2
BEZEL_OUTER, BEZEL_INNER = 606.0, 566.0  # radii in pixels
MON_DIAMETER = 1118  # the mon's own extent, scaled so its ring sits just inside the bezel
MIN_PIECE = 3000  # square pixels: a speckle of the cutout, not part of the mon


def smooth(distance: np.ndarray, edge: float, softness: float = 1.6) -> np.ndarray:
    """1 inside, 0 outside, anti-aliased across `softness` pixels."""
    return np.clip((edge - distance) / softness + 0.5, 0, 1)


def noise(rng: np.random.Generator, scale: float, size: int = SIZE) -> np.ndarray:
    field = ndi.gaussian_filter(rng.standard_normal((size, size)), scale)
    return (field - field.mean()) / (field.std() + 1e-9)


def clean_mon(path: Path) -> np.ndarray:
    """The mon's alpha as a 0..1 float array: thresholded, de-speckled, re-antialiased, cropped square and scaled to MON_DIAMETER."""
    source = np.array(Image.open(path).convert("RGBA"))
    solid = source[..., 3] >= 128
    labels, count = ndi.label(solid)
    sizes = ndi.sum(solid, labels, range(1, count + 1))
    keep = np.isin(labels, [i + 1 for i, size in enumerate(sizes) if size >= MIN_PIECE])
    keep = ndi.binary_opening(keep, iterations=1)
    mask = ndi.gaussian_filter(keep.astype(float), 1.2)
    ys, xs = np.nonzero(mask > 0.5)
    side = max(ys.max() - ys.min(), xs.max() - xs.min()) + 1
    cy, cx = (ys.max() + ys.min()) / 2, (xs.max() + xs.min()) / 2
    image = Image.fromarray((mask * 255).astype(np.uint8))
    box = (int(cx - side / 2), int(cy - side / 2), int(cx + side / 2) + 1, int(cy + side / 2) + 1)
    return np.array(image.crop(box).resize((MON_DIAMETER, MON_DIAMETER), Image.LANCZOS), dtype=float) / 255


def light(height: np.ndarray, strength: float) -> np.ndarray:
    """Relief shading, light from the upper left: 0 dark .. 1 bright around 0.5 flat."""
    gy, gx = np.gradient(ndi.gaussian_filter(height, 2.2))
    shade = (-gx - gy) * strength
    return np.clip(0.5 + shade, 0, 1)


def build(mon_path: Path = DEFAULT_MON, seed: int = 771) -> Image.Image:
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:SIZE, 0:SIZE].astype(float)
    radius = np.hypot(xx - CENTER, yy - CENTER)
    wobble = noise(rng, 40) * 2.2  # cast-metal irregularity of the outer edge
    disc = smooth(radius + wobble, BEZEL_OUTER)
    field = smooth(radius + wobble * 0.5, BEZEL_INNER)

    mon = np.zeros((SIZE, SIZE))
    offset = (SIZE - MON_DIAMETER) // 2
    mon[offset:offset + MON_DIAMETER, offset:offset + MON_DIAMETER] = clean_mon(mon_path)
    mon *= field

    # Height: bezel a little raised, enamel recessed, gold mon raised highest.
    height = 0.35 * disc * (1 - field) + 0.0 * field + 1.0 * mon
    shade = light(height, 8.0)
    grain = noise(rng, 1.3) * 0.5 + noise(rng, 6) * 0.5

    # Dark bronze bezel: smooth cast metal, a worn highlight along its inner chamfer.
    fine = noise(rng, 0.9)
    bezel_value = 0.50 + 0.07 * noise(rng, 7) + 0.05 * fine + 0.60 * (shade - 0.5)
    bezel = np.stack([np.clip(bezel_value * c, 0, 255) for c in (86, 74, 58)], -1)

    # Deep green enamel, finely mottled, darker where the mon's shadow falls and toward the bezel.
    cast = ndi.shift(ndi.gaussian_filter(mon, 8), (12, 9), order=1)
    vignette = np.clip((radius - 380) / 190, 0, 1)
    enamel_value = 0.66 + 0.10 * noise(rng, 11) + 0.05 * fine - 0.50 * np.clip(cast, 0, 1) - 0.28 * vignette + 0.30 * (shade - 0.5)
    enamel = np.stack([np.clip(enamel_value * c, 0, 255) for c in (22, 84, 46)], -1)

    # Gold: a hammered-leaf gradient lit from the upper left, lightly mottled, shaded hard by the relief so the edges read as cast.
    sweep = np.clip(0.55 - (xx + yy - 2 * CENTER) / (2.6 * SIZE), 0, 1)
    wear = np.clip(0.18 + 0.50 * sweep + 0.16 * noise(rng, 5) + 0.08 * fine, 0, 1)
    gold_low, gold_high = np.array([128, 88, 20]), np.array([240, 200, 96])
    gold = gold_low + (gold_high - gold_low) * wear[..., None]
    gold = np.clip(gold * (0.35 + 1.30 * shade[..., None]), 0, 255)
    patina = np.clip(noise(rng, 16) - 1.35, 0, 1)[..., None]  # a little green verdigris, as the Mantis emblem has
    gold = gold * (1 - 0.30 * patina) + np.array([64, 112, 66]) * 0.30 * patina

    colour = bezel * (1 - field[..., None]) + enamel * field[..., None]
    colour = colour * (1 - mon[..., None]) + gold * mon[..., None]
    colour = colour * (disc[..., None] > 0.004)  # no stray colour under the transparent outside, as the other clan emblems
    out = np.dstack([np.clip(colour, 0, 255), disc * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--mon", type=Path, default=DEFAULT_MON)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--seed", type=int, default=771)
    arguments = parser.parse_args()
    emblem = build(arguments.mon, arguments.seed)
    arguments.output.parent.mkdir(parents=True, exist_ok=True)
    emblem.save(arguments.output, optimize=True)
    print(f"wrote {arguments.output} {emblem.size}")
