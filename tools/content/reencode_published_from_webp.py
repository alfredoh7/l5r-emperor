#!/usr/bin/env python3
"""One-time migration (issue #200): re-encode every already-shipped
src/L5R.Client/Content/Published/<card>.jpg from its receipt's WebP working copy
(tools/content/sync_webp_working_copies.py), instead of the original Oracle-of-the-Void master,
wherever a working copy exists -- trading a little more generation loss for a large storage win on
assets that are actually committed to Git, not just a local dev cache.

Walks every content/published/*-sources.json manifest, finds each entry's own receipt by its
recorded top-level image_path, and -- only when that receipt has a working_copy -- re-encodes it
to src/L5R.Client/Content/Published/<card_id>.jpg and updates the manifest's own image_path/
image_sha256 to match. A card whose receipt has no working copy (no WebP counterpart in the
release) is left completely untouched, still pointing at its original master.

Usage: python3 tools/content/reencode_published_from_webp.py [--apply]
       (without --apply, only reports what WOULD change)
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def receipt_path_for(image_path: str, root: Path = ROOT) -> Path | None:
    """None for a legacy image with no receipts/ sibling at all (a handful of the pinned
    lion-crane-v1 deck's own images sit directly under their set folder, predating the
    images/receipts split every other set uses -- nothing to migrate them from).

    A manifest's own image_path may already point into `images/` (an original, not yet migrated)
    or `webp/` (already migrated by an earlier run of this script) -- either names the same receipt
    by its shared filename stem, so this is idempotent to re-run once new working copies appear."""
    parts = Path(image_path).parts
    for parent in ("images", "webp"):
        if parent in parts:
            i = parts.index(parent)
            stem = Path(image_path).stem
            return root / Path(*parts[:i], "receipts", f"{stem}.json")
    return None


def migrate(root: Path = ROOT, apply: bool = False) -> dict:
    image_dest = root / "src/L5R.Client/Content/Published"
    replaced, skipped, missing_receipt = 0, 0, 0
    skipped_ids: list[str] = []
    for manifest_path in sorted((root / "content/published").glob("*-sources.json")):
        doc = json.loads(manifest_path.read_text(encoding="utf-8"))
        changed = False
        for entry in doc:
            card_id = entry["card_id"]
            receipt_path = receipt_path_for(entry["image_path"], root)
            if receipt_path is None or not receipt_path.exists():
                missing_receipt += 1
                continue
            receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
            working_copy = receipt.get("working_copy")
            if working_copy is None:
                skipped += 1
                skipped_ids.append(card_id)
                continue

            working_image = root / working_copy["path"]
            if sha256(working_image.read_bytes()) != working_copy["sha256"]:
                raise ValueError(f"Working-copy scan changed since it was synced: {card_id}")
            if apply:
                destination = image_dest / f"{card_id}.jpg"
                with Image.open(working_image) as img:
                    img.convert("RGB").save(destination, "JPEG", quality=95)
                entry["image_path"] = working_copy["path"]
                entry["image_sha256"] = working_copy["sha256"]
                changed = True
            replaced += 1
        if changed:
            manifest_path.write_text(json.dumps(doc, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    return {
        "replaced": replaced,
        "skipped_no_working_copy": skipped,
        "missing_receipt": missing_receipt,
        "skipped_ids": skipped_ids,
        "apply": apply,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apply", action="store_true", help="Actually rewrite the JPEGs/manifests; otherwise dry-run.")
    args = parser.parse_args()
    print(json.dumps(migrate(apply=args.apply), indent=2))


if __name__ == "__main__":
    main()
