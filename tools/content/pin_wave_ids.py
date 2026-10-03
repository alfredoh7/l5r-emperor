#!/usr/bin/env python3
"""Pin the card ids of every content/published wave file in its own manifest,
tests/L5R.Core.Tests/PublishedWaves/<wave>.txt (one sorted id per line).

The published-pool test compares each wave file with its manifest, so deleting
or shrinking a wave by accident fails by name. A new wave adds
a new manifest file and edits no shared line: run this after packaging a wave.

  python3 tools/content/pin_wave_ids.py            write the missing manifests
  python3 tools/content/pin_wave_ids.py --wave item-wave-42  pin just this packaged wave
  python3 tools/content/pin_wave_ids.py --update   rewrite all of them from the files
"""
from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PUBLISHED = "content/published"
PINS = "tests/L5R.Core.Tests/PublishedWaves"


def wave_files(root: Path) -> list[Path]:
    return sorted(p for p in (root / PUBLISHED).glob("*.json") if not p.name.endswith("-sources.json"))


def card_ids(path: Path) -> list[str]:
    cards = json.loads(path.read_text(encoding="utf-8"))["Cards"]
    return sorted({card["Id"] for card in cards})


def pin(root: Path = ROOT, update: bool = False, waves: list[str] | None = None) -> list[str]:
    files = wave_files(root)
    if waves is not None:
        requested = set(waves)
        invalid = sorted(name for name in requested if not re.fullmatch(r"[a-z]+-wave-[1-9][0-9]*", name))
        missing = sorted(requested - {path.stem for path in files})
        if invalid or missing:
            raise ValueError(f"Invalid or missing wave profiles: {sorted(set(invalid + missing))}")
        files = [path for path in files if path.stem in requested]
    pins = root / PINS
    pins.mkdir(parents=True, exist_ok=True)
    written = []
    for path in files:
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
    parser.add_argument("--wave", action="append", help="pin only this <type>-wave-<N> profile (repeatable)")
    args = parser.parse_args()
    try:
        written = pin(update=args.update, waves=args.wave)
    except ValueError as error:
        parser.error(str(error))
    print(f"{len(written)} manifest(s) written" + (": " + ", ".join(written) if written else ""))


if __name__ == "__main__":
    main()
