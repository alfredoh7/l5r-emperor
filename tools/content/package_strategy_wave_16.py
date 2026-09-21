#!/usr/bin/env python3
"""Build Strategy wave 16 (issue #304): Brawl (oracle-l5r-1127).

Its BLOCKERS note ("Needs Regions (not built)") was stale: Regions (region-wave-1/2/3) and Terrains
were both already built. Brawl is two alternative effects of one Strategy, wired as a paired-ability
set sharing one UsageKey: "Battle: Target an enemy card without attachments: Bow it" reuses the
existing enemy-card-without-attachments Target verbatim, and "Battle/Open: Target a Region (at any
province) or a Terrain: Destroy it" needed one new Target category (region-or-terrain) plus a small
DestroyRegion helper (Terrain reuses DiscardTerrain).

Verified against the card's own scan (Second City printing, sha256-checked against its pinned
receipt): Cost 0, Focus 1, and the printed text matches deck-pool.json/the catalogue exactly -- the
"(at any province)" parenthetical is an italic clarification attached to "Region".
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-16"

ABILITIES: dict[int, list[str]] = {
    1127: ["brawl-bow", "brawl-destroy"],  # Brawl
}
EFFECTS: dict[int, str] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    import sys
    ids = sys.argv[1:]
    if not ids:
        raise SystemExit("Usage: package_strategy_wave_16.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
