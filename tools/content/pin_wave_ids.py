#!/usr/bin/env python3
"""Pin the card ids of every content/published wave file in its own manifest,
tests/L5R.Core.Tests/PublishedWaves/<wave>.txt (one sorted id per line).

The published-pool test compares each wave file with its manifest, so deleting
or shrinking a wave (and its loader) by accident fails by name. A new wave adds
a new manifest file and edits no shared line: run this after packaging a wave.

  python3 tools/content/pin_wave_ids.py            write the missing manifests
  python3 tools/content/pin_wave_ids.py --update   rewrite all of them from the files
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PUBLISHED = "content/published"
PINS = "tests/L5R.Core.Tests/PublishedWaves"


def wave_files(root: Path) -> list[Path]:
    return sorted(p for p in (root / PUBLISHED).glob("*.json") if not p.name.endswith("-sources.json"))


def card_ids(path: Path) -> list[str]:
    cards = json.loads(path.read_text(encoding="utf-8"))["Cards"]
    return sorted({card["Id"] for card in cards})


def pin(root: Path = ROOT, update: bool = False) -> list[str]:
    pins = root / PINS
    pins.mkdir(parents=True, exist_ok=True)
    written = []
    for path in wave_files(root):
        target = pins / f"{path.stem}.txt"
        if target.exists() and not update:
            continue
        text = "".join(f"{card}\n" for card in card_ids(path))
        if not target.exists() or target.read_text(encoding="utf-8") != text:
            target.write_text(text, encoding="utf-8", newline="\n")
            written.append(target.name)
    return written


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--update", action="store_true", help="rewrite existing manifests too")
    args = parser.parse_args()
    written = pin(update=args.update)
    print(f"{len(written)} manifest(s) written" + (": " + ", ".join(written) if written else ""))


if __name__ == "__main__":
    main()
