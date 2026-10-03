#!/usr/bin/env python3
"""Build Strategy wave 52 (Embers of War pass, batch 3, issue #460): the ten Strategies wired this batch.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-52"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    99: ["weak-poison-force-zero"],
    637: ["accusation-negate", "accusation-honor-loss"],
    1880: ["deadly-discipline-tokens", "deadly-discipline-bow"],
    2413: ["exploited-advantage-move"],
    3055: ["help-shadows-move"],
    5423: ["moving-unmoving-approach", "moving-unmoving-retreat"],
    5941: ["phantom-visage-bow"],
    6047: ["precision-boost", "precision-reduce", "precision-attack"],
    6077: ["pronouncement-guilt-move"],
    6209: ["reckless-duel"],
}
OVERRIDE: dict[int, dict] = {}


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
