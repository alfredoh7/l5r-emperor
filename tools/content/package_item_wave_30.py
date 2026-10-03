#!/usr/bin/env python3
"""Build Item wave 30 (issue #503, group P4: target/slot model, movement and assignment locks, one-shot watches and shields): Hunger.

The engine side is src/L5R.Core/EmperorP4*.cs. Every scan was read by eye (WebP working copies, sha256-verified against their receipts' working_copy); the published Text is each scan's own wording.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-30"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    3514: ["hunger-negate-move"],  # Hunger
}
OVERRIDE: dict[int, dict] = {
    3514: {"Effect": "hunger-must-attack", "Keywords": ["Weapon", "Unique"]},  # the scan prints "Weapon • Unique" (the catalogue adds Shadowlands); "You must declare an attack each turn and assign this Personality to attack if possible."
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    3514: "You must declare an attack each turn and assign this Personality to attack if possible." + NL
          + "Reaction: After an action targets this Personality: Negate this Personality's movement from the action's effects.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
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
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
