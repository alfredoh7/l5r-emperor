#!/usr/bin/env python3
"""Build Holding wave 10 (Emperor Edition Gempukku pass, issue #461): the nine Market Holdings -- Buried Treasures, Purveyor of Questionable Goods, Tattoos & Trinkets,
The Daidoji Marketplace, The Ide Caravan, The Merchant's District, The Mystical Merchant, The Quartermaster's Depot, The Yasuki Peddler.

Every scan was read by eye and its source receipt is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-10"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10211: ["market-draw-mantis"],
    10212: ["market-draw-spider"],
    10213: ["market-draw-dragon"],
    10214: ["market-draw-crane"],
    10215: ["market-draw-unicorn"],
    10216: ["market-draw-scorpion"],
    10217: ["market-draw-phoenix"],
    10218: ["market-draw-lion"],
    10219: ["market-draw-crab"],
}
OVERRIDE: dict[int, dict] = {
    10211: {"GoldProduction": 2},
    10212: {"GoldProduction": 2},
    10213: {"GoldProduction": 2},
    10214: {"GoldProduction": 2},
    10215: {"GoldProduction": 2},
    10216: {"GoldProduction": 2},
    10217: {"GoldProduction": 2},
    10218: {"GoldProduction": 2},
    10219: {"GoldProduction": 2},
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
