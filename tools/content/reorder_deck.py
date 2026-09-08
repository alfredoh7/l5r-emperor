#!/usr/bin/env python3
"""Reorder a plain-text deck list into the canonical presentation order:
Stronghold, then Border Keep, then Bamboo Harvesters, then the rest of the
Dynasty deck, then the Fate deck -- each group keeping its original relative
order. Classifies each entry by name against content/deck-pool.json (#23).
Cards that don't resolve in the pool are left in place at the end of the
Dynasty group and reported, rather than silently dropped or misplaced.
"""
from __future__ import annotations

import argparse
import re
from pathlib import Path

from build_deck_pool import ROOT

POOL = ROOT / "content/deck-pool.json"
EXPERIENCE = re.compile(r"\s+-\s+exp\d*\s*$", re.IGNORECASE)


def load_pool(path: Path = POOL) -> dict:
    import json
    return json.loads(path.read_text(encoding="utf-8"))


def base_name(card_name: str) -> str:
    return EXPERIENCE.sub("", card_name).strip()


def index_by_name(pool: dict) -> dict:
    by_name = {}
    for card in pool["cards"]:
        by_name.setdefault(card["name"].casefold(), card)
    return by_name


def parse(text: str) -> tuple[str, list[tuple[int, str]]]:
    legality = None
    entries = []
    for raw in text.splitlines():
        line = raw.strip()
        if not line or line.startswith("#") or line.startswith("//"):
            continue
        if line.lower().startswith("legality:"):
            legality = line.split(":", 1)[1].strip()
            continue
        match = re.match(r"^(\d+)\s+(.+)$", line)
        entries.append((int(match.group(1)), match.group(2).strip()) if match else (1, line))
    if legality is None:
        raise ValueError("Deck must include a Legality: header line.")
    return legality, entries


def group(name: str, by_name: dict) -> tuple[int, bool]:
    """Returns (sort key, resolved) -- resolved is False when the name wasn't found in the pool.
    Some catalogue entries carry their "- exp" suffix as a permanent part of the title (e.g. a
    card only ever printed Experienced), so an exact match is tried before falling back to the
    suffix-stripped base name, never the other way -- stripping first would silently match the
    wrong printing whenever the exact title happens to include the suffix."""
    if name.casefold() == "border keep":
        return 1, True
    if name.casefold() == "bamboo harvesters":
        return 2, True
    card = by_name.get(name.casefold()) or by_name.get(base_name(name).casefold())
    if card is None:
        return 3, False
    types = card.get("types", [])
    if "Stronghold" in types:
        return 0, True
    return (4 if "Fate" in card.get("deckTypes", []) else 3), True


def reorder(text: str, pool: dict | None = None) -> tuple[str, list[str]]:
    pool = pool or load_pool()
    by_name = index_by_name(pool)
    legality, entries = parse(text)
    unresolved = []
    keyed = []
    for position, (count, name) in enumerate(entries):
        key, resolved = group(name, by_name)
        if not resolved:
            unresolved.append(name)
        keyed.append((key, position, count, name))
    keyed.sort(key=lambda item: (item[0], item[1]))
    lines = [f"Legality: {legality}", ""]
    lines += [name if count == 1 else f"{count} {name}" for _, _, count, name in keyed]
    return "\n".join(lines) + "\n", unresolved


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("deck", type=Path, help="Path to a legacy deck .txt file")
    parser.add_argument("--out", type=Path, help="Write the reordered deck here instead of stdout")
    args = parser.parse_args()
    reordered, unresolved = reorder(args.deck.read_text(encoding="utf-8-sig"))
    if args.out:
        args.out.write_text(reordered, encoding="utf-8")
        print(f"Wrote {args.out}")
    else:
        print(reordered, end="")
    if unresolved:
        print(f"-- {len(unresolved)} unresolved name(s), left in place: {', '.join(unresolved)}")


if __name__ == "__main__":
    main()
