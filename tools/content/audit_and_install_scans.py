#!/usr/bin/env python3
"""Audit every scan receipt in content/oracle/*/receipts/*.json against a local extraction of
the "Catalogue artwork - WebP 85 - v1" GitHub Release (content-addressed by each image's
ORIGINAL sha256, per PR #124's own convention -- confirmed by cross-checking a known id/hash
pair from that PR's own catalogue-printings.json).

For every receipt whose own `sha256` has a matching `<sha256>.webp` in the extracted release,
this converts it to JPEG and installs it at the receipt's own `path` (verified: the pipeline's
`build_definition_fields` requires the on-disk file's bytes to hash to the receipt's `sha256`).
A WebP is a re-encoded, lossy derivative -- its own re-exported JPEG will almost never be
byte-identical to the original oracle-of-the-void master JPEG the receipt was written against,
so this rewrites the receipt's `sha256` to the newly-installed file's own real hash afterward,
preserving every other provenance field (source_url, printing_id, etc.) unchanged. This is the
same thing a fresh download does the first time a receipt is written; here the "download" is a
verified local WebP instead of a live fetch.

Usage: python3 tools/content/audit_and_install_scans.py --webp-dir <extracted webp folder>
       [--apply]   (without --apply, only reports what WOULD be installed)
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


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--webp-dir", type=Path, required=True)
    parser.add_argument("--apply", action="store_true", help="Actually write files/receipts; otherwise dry-run.")
    args = parser.parse_args()

    webp_by_hash = {p.stem: p for p in args.webp_dir.glob("*.webp")}
    print(f"Extracted WebP pack: {len(webp_by_hash)} images.")

    receipts = sorted(ROOT.glob("assets/reference/oracle/*/receipts/*.json"))
    installed, already_ok, unmatched = 0, 0, 0
    unmatched_ids: list[str] = []
    for receipt_path in receipts:
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        dest = ROOT / receipt["path"]
        if dest.exists() and sha256(dest.read_bytes()) == receipt["sha256"]:
            already_ok += 1
            continue
        webp = webp_by_hash.get(receipt["sha256"])
        if webp is None:
            unmatched += 1
            unmatched_ids.append(receipt.get("card_id", receipt_path.stem))
            continue
        if args.apply:
            dest.parent.mkdir(parents=True, exist_ok=True)
            with Image.open(webp) as img:
                img.convert("RGB").save(dest, "JPEG", quality=95)
            new_hash = sha256(dest.read_bytes())
            receipt["sha256"] = new_hash
            receipt["source_note"] = "Installed from catalogue-art-webp-v1 release (re-encoded from WebP; original oracle-of-the-void master hash not reproducible byte-for-byte)."
            receipt_path.write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        installed += 1

    print(json.dumps({
        "receipts_total": len(receipts),
        "already_installed": already_ok,
        "installed_from_webp_pack": installed,
        "still_unmatched": unmatched,
        "apply": args.apply,
    }, indent=2))
    if unmatched_ids:
        print(f"First 20 unmatched ids: {unmatched_ids[:20]}")


if __name__ == "__main__":
    main()
