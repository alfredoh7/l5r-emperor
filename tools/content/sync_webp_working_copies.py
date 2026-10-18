#!/usr/bin/env python3
"""Populate every receipt's `working_copy` field (issue #200) from the "Catalogue artwork -
WebP 85 - v1" GitHub Release, and materialize the matching local WebP file.

Two independent identities live side by side in each receipt from here on:
  - The receipt's own top-level `path`/`sha256`/`source_url` -- untouched, forever -- name the
    true Oracle-of-the-Void master. Restoring it is just re-running download_oracle_printings.py /
    download_expansion_printings.py, which already fetch by `source_url`.
  - A new sibling `working_copy` block names a much smaller WebP artifact instead, matched against
    the release by the ORIGINAL master's own sha256 (the release's own content-addressing scheme,
    confirmed against PR #124's catalogue-printings.json). This is what the packaging pipeline
    (`build_definition_fields` in package_vanilla_wave.py) prefers when present.

This is the non-destructive successor to issue #198's own audit_and_install_scans.py: that tool
re-encoded WebP to JPEG and overwrote each receipt's OWN sha256 with the re-encoded file's hash,
permanently losing the tie back to the true master -- which is exactly why those rewrites were
kept local-only rather than committed. Storing the WebP bytes as-is (no re-encode) in a separate
field fixes that: nothing about the original identity is ever touched, so this IS safe to commit.

The release zip itself is fetched automatically (via `gh release download`) into a local, gitignored
cache the first time it's needed -- no more manually locating/downloading it by hand each session.
Its own zip hash is pinned below and verified before extracting, the same way tools/bootstrap.py
pins the SDKs it installs.

Usage: python3 tools/content/sync_webp_working_copies.py [--apply] [--webp-zip PATH] [--cache-dir PATH]
       (without --apply, only reports what WOULD be written)
"""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
import zipfile
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CACHE_DIR = ROOT / ".cache/webp-art"
RELEASE = "catalogue-art-webp-v1"
RELEASE_ASSET = "L5R-Catalogue-WebP85.zip"
RELEASE_ASSET_SHA256 = "18b23719d646c68f13c1f7a0a164d1c8e28e99fa443ec822c8695a914bfd8df4"
RELEASE_REPO = "l5r-emperor-dev/l5r-emperor"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def ensure_extracted(cache_dir: Path, webp_zip: Path | None) -> Path:
    """Return a directory of extracted `<original-sha256>.webp` files, downloading/extracting the
    pinned release into `cache_dir` first if neither it nor an already-extracted copy exists."""
    extracted = cache_dir / "extracted"
    if extracted.is_dir() and any(extracted.glob("*.webp")):
        return extracted

    if webp_zip is None:
        cache_dir.mkdir(parents=True, exist_ok=True)
        webp_zip = cache_dir / RELEASE_ASSET
        if not webp_zip.exists():
            subprocess.run(
                ["gh", "release", "download", RELEASE, "-R", RELEASE_REPO, "-p", RELEASE_ASSET,
                 "-D", str(cache_dir), "--clobber"],
                check=True,
            )

    actual = sha256(webp_zip.read_bytes())
    if actual != RELEASE_ASSET_SHA256:
        raise ValueError(
            f"{webp_zip} sha256 {actual} does not match the pinned {RELEASE_ASSET_SHA256} -- "
            "the release asset changed; update RELEASE_ASSET_SHA256 only after verifying why."
        )
    extracted.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(webp_zip) as zf:
        zf.extractall(extracted)
    return extracted


def sync(cache_dir: Path = DEFAULT_CACHE_DIR, webp_zip: Path | None = None, apply: bool = False) -> dict:
    extracted = ensure_extracted(cache_dir, webp_zip)
    webp_by_original_hash = {p.stem: p for p in extracted.glob("*.webp")}

    receipts = sorted(ROOT.glob("assets/reference/oracle/*/receipts/*.json"))
    synced, already_synced, unmatched = 0, 0, 0
    unmatched_ids: list[str] = []
    for receipt_path in receipts:
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        existing = receipt.get("working_copy")
        webp_source = webp_by_original_hash.get(receipt["sha256"])
        if webp_source is None:
            if existing is None:
                unmatched += 1
                unmatched_ids.append(receipt.get("card_id", receipt_path.stem))
            continue

        slug = receipt_path.parents[1].name
        dest = ROOT / "assets/reference/oracle" / slug / "webp" / f"{receipt_path.stem}.webp"
        webp_bytes = webp_source.read_bytes()
        webp_hash = sha256(webp_bytes)
        if existing is not None and existing.get("sha256") == webp_hash and dest.exists():
            already_synced += 1
            continue

        if apply:
            dest.parent.mkdir(parents=True, exist_ok=True)
            dest.write_bytes(webp_bytes)
            with Image.open(dest) as img:
                width, height = img.size
            receipt["working_copy"] = {
                "format": "WEBP",
                "path": dest.relative_to(ROOT).as_posix(),
                "sha256": webp_hash,
                "bytes": len(webp_bytes),
                "width": width,
                "height": height,
                "source": {"release": RELEASE, "asset": RELEASE_ASSET, "matched_by": "original_sha256"},
            }
            receipt_path.write_text(json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        synced += 1

    return {
        "receipts_total": len(receipts),
        "already_synced": already_synced,
        "synced": synced,
        "unmatched": unmatched,
        "unmatched_ids_sample": unmatched_ids[:20],
        "apply": apply,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apply", action="store_true", help="Actually write the WebP files/receipts; otherwise dry-run.")
    parser.add_argument("--webp-zip", type=Path, default=None, help="Use this local zip instead of downloading the release.")
    parser.add_argument("--cache-dir", type=Path, default=DEFAULT_CACHE_DIR, help="Local cache directory for the extracted release.")
    args = parser.parse_args()
    print(json.dumps(sync(cache_dir=args.cache_dir, webp_zip=args.webp_zip, apply=args.apply), indent=2))


if __name__ == "__main__":
    main()
