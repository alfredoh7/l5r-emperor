#!/usr/bin/env python3
"""Build Region wave 2 (issue #264, the Second City unwired-card audit): Overgrown Grove, the one
Region found fully buildable with primitives that already existed before this wave.

Overgrown Grove is not a Personality source, so it can never satisfy AtBattle's own Zone check
(Zone.Region isn't one of the Zones AtBattle accepts) -- it needed a bespoke standalone Require
(source.ProvinceIndex == CurrentBattleProvince) instead of reusing AtBattle, the same shape Region
Wave 1's own province-scoped Regions already use elsewhere in the engine.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "region-wave-2"

ABILITIES: dict[int, list[str]] = {
    5841: ["overgrown-grove-move"],  # Overgrown Grove
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = "none"
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
        raise SystemExit("Usage: package_region_wave_2.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
