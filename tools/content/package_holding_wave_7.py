#!/usr/bin/env python3
"""Build Holding wave 7 (Seeds of Decay pass, issue #453, batch 4): Temple to Fudo, Hachi's Revenge, Incense Mill, Overlooked Palace and Second City Port.
The catalogue records no production for these Holdings; each prints "Bow this card: Produce 2 Gold" (Hachi's Revenge 3 for a Crane player: crane-gold).

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-7"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10039: [],
    10034: ["hachis-revenge-bow"],
    10035: ["incense-mill-cleanse"],
    10036: ["overlooked-palace-create"],
    10037: ["second-city-port-straighten"],
}
OVERRIDE: dict[int, dict] = {
    10039: {"GoldProduction": 2, "Effect": "temple-to-fudo", "EntryHonorLoss": 3},
    10034: {"GoldProduction": 2, "Effect": "crane-gold"},
    10035: {"GoldProduction": 2},
    10036: {"GoldProduction": 2},
    10037: {"GoldProduction": 2},
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
