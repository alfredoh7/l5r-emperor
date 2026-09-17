#!/usr/bin/env python3
"""Build Spell wave 2 (issue #264, the Second City unwired-card audit): three Spells found fully
buildable with primitives that already existed before this wave -- Intervening Spirit, The Earth
Flows, and Winds of Dismissal.

Intervening Spirit and The Earth Flows both reinforce a Personality into the current battlefield
even when its own wielder is not there; Intervening Spirit joins the existing michi-move/rikyou-move/
ring-of-earth-raise/-lower reinforce-from-elsewhere exemption list, while The Earth Flows is
Defender-only (a standalone Require, since no existing reinforce ability is restricted to a side).
The Earth Flows' own passive "gain 2 Honor if this Shugenja is Earth and in the winning army" clause
needed one new shared helper (CheckEarthFlowsBonus in MatchEngine.cs, called from ResolveBattle's own
two outright-win branches only -- never the tie branch, since "the winning army" doesn't exist in a
tie) keyed off a new Effect term ("earth-flows-honor") on this Spell's own definition.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-2"

ABILITIES: dict[int, list[str]] = {
    3788: ["intervening-spirit-move"],  # Intervening Spirit
    8038: ["earth-flows-move"],  # The Earth Flows
    9344: ["winds-of-dismissal-choice"],  # Winds of Dismissal
}
EFFECTS: dict[int, str] = {
    8038: "earth-flows-honor",  # The Earth Flows
}


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
        raise SystemExit("Usage: package_spell_wave_2.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
