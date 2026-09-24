#!/usr/bin/env python3
"""Build Holding wave 10 (Forgotten Legacy pass, issue #467, batch 5): Ageless Shrine (a Nonhuman conditional production and a straighten),
Bamboo Harvesters - experienced (a third-turn straighten hold and a draw), Border Keep - experienced (a once-per-game hand cycle) and
Colonial Harbor (Tireless for its controller's Stronghold).

Only the cards this batch actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye and its source receipt (with the
working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-10"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    166: ["ageless-shrine-straighten"],  # Ageless Shrine
    680: ["bamboo-exp-draw"],  # Bamboo Harvesters - experienced
    1107: ["border-keep-exp-cycle"],  # Border Keep - experienced
    1431: [],  # Colonial Harbor
}
OVERRIDE: dict[int, dict] = {
    166: {"Effect": "ageless-shrine-gold", "GoldProduction": 2},
    680: {"Effect": "bamboo-harvesters-exp", "GoldProduction": 2},
    1107: {"GoldProduction": 2},
    1431: {"Effect": "colonial-harbor-tireless", "GoldProduction": 5},
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
