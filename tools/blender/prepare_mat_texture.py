#!/usr/bin/env python3
"""Derive the approved runtime texture from the user's untouched Topaz master."""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops, __version__ as pillow_version

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "src/L5R.Client/Art/navy-mat-topaz-4x.webp"
REPORT = ROOT / "assets/table/mat-fidelity/texture-provenance.json"


def sha256(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("master", type=Path)
    args = parser.parse_args()
    Image.MAX_IMAGE_PIXELS = 150_000_000
    with Image.open(args.master) as master:
        if master.size != (13376, 7528) or master.mode != "RGB":
            raise ValueError("Expected the approved 13376 × 7528 RGB Topaz master.")
        reduced = master.resize((6688, 3764), Image.Resampling.LANCZOS)
    reduced.save(OUTPUT, "WEBP", lossless=True, method=4)
    with Image.open(OUTPUT) as decoded:
        if decoded.size != reduced.size or ImageChops.difference(reduced, decoded.convert("RGB")).getbbox():
            raise ValueError("WebP verification failed: decoded pixels differ from the resized source.")
    report = {
        "master": {"filename": args.master.name, "dimensions": [13376, 7528],
                   "bytes": args.master.stat().st_size, "sha256": sha256(args.master),
                   "provenance": "User supplied Topaz 8x upscale; master preserved outside Git."},
        "runtime": {"path": str(OUTPUT.relative_to(ROOT)), "dimensions": list(reduced.size),
                    "bytes": OUTPUT.stat().st_size, "sha256": sha256(OUTPUT),
                    "pixel_sha256": hashlib.sha256(reduced.tobytes()).hexdigest(),
                    "encoding": "Lossless WebP, method 4", "resampling": "Lanczos",
                    "pillow": pillow_version, "decoded_pixel_match": True},
        "godot": {"compression": "lossless", "mipmaps": True, "size_limit": 0,
                  "extra_procedural_weave": "off by default"}
    }
    REPORT.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
