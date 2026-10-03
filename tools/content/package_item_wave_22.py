#!/usr/bin/env python3
"""Build Item wave 22 (issue #503, pass group P2): Armor of the Uruwashii, Pillowbook.

Wired in src/L5R.Core/EmperorP2*.cs (issue #503: search and look, Honor and hand-discard, new effects, stat pipelines, tokens, duels). Wave 22 = the highest Item wave at
packaging time + 5 (the P2 offset of the parallel passes). Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each
source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-22"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    453: ["p2-uruwashii-negate-move"],  # Armor of the Uruwashii
    5958: [],  # Pillowbook
}
OVERRIDE: dict[int, dict] = {
    453: {"AttackDefenseKind": "Melee", "AttackDefenseAmount": -2, "AttackDefenseScope": "Unit"},
    5958: {"Effect": "p2-pillowbook"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    453: "Melee Attacks targeting cards in this unit have -2 strength." + NL
          + "Reaction: Before another player's action resolves: Negate this Personality's movement from the action's effects.",
    5958: "Other players' cards' effects will not discard cards from your hand." + NL
          + "Your maximum hand size is increased by 1." + NL
          + "Other players' maximum hand sizes are reduced by 1, to a minimum of 7.",
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
