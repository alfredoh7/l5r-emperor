#!/usr/bin/env python3
"""Build Strategy wave 18 (issue #331): Return to Action (oracle-l5r-10953), the first card on the general
Discipline and Absent flags.

Return to Action (Aftermath #138, Focus 3, no Gold Cost box): "Discipline :g2: (You may pay 2 Gold to play this
Discipline from your discard pile, then remove it from the game after its action resolves.)\\nAbsent Battle: If you
are the Defender, target your Personality at home and move him to this battlefield. After he moves, straighten him."

Verified against the card's own scan (Aftermath printing; the local WebP working copy's sha256 checked against its
receipt): the printed text above, Focus 3, and no Gold Cost box -- the only 2 on the card is the Discipline icon.

Discipline is a flag on the ability (AbilityDefinition.Discipline), not a Timing: the ability's timing stays the
printed verb, Battle, and Absent (AbilityDefinition.Absent) waives the Defender's need for a unit at the battlefield
(moving one in is the whole point). The move is a negatable effect on the target; he is straightened only if he
really moved. See docs/decisions/2026-09-19-discipline-and-absent-flags.md.

GoldCost: the catalogue's own "cost" field is blank for every Discipline Strategy (the Gold amount lives only inside
the Discipline icon), so build_definition_fields yields 0. The published GoldCost of a Discipline card holds the
DISCIPLINE cost -- the same convention as Banzai! (2) and Lost in Transit (1) in strategy-wave-14 -- because that is the
amount GoldQuotes.ImplicitGoldRule charges when the card is played from a discard pile. Played from hand it is free.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-18"

ABILITIES: dict[int, list[str]] = {
    10953: ["return-to-action-move"],  # Return to Action
}
EFFECTS: dict[int, str] = {}

# The Discipline icon's amount (see the module docstring); build_definition_fields reads the blank Cost box as 0.
GOLD_COST_OVERRIDES: dict[int, int] = {10953: 2}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in GOLD_COST_OVERRIDES:
            fields["GoldCost"] = GOLD_COST_OVERRIDES[oracle_id]
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
        raise SystemExit("Usage: package_strategy_wave_18.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
