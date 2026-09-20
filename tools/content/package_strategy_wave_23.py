#!/usr/bin/env python3
"""Build Strategy wave 23 (issue #353): Expensive Achievement (oracle-l5r-10938), Knowledge and Power (oracle-l5r-10942) and Two Steady
Breaths (oracle-l5r-10961) -- three more Aftermath Discipline Strategies, wired once Mendon ruled how a Discipline play is priced.

Verified against each card's own scan (Aftermath printings; the local WebP working copies' sha256 checked against their receipts):

* Expensive Achievement (Focus 3, no Gold Cost box): "Discipline :g2: ... Battle: Give this Province +2 or -2 Strength."
* Knowledge and Power (Focus 2): "Discipline :g1: ... Limited, :g1:: Destroy one or two of a target Personality's tokens. Give him as many
  +1F tokens as the number of tokens this destroyed."
* Two Steady Breaths (Focus 4): "Discipline :g2: (You may pay the Discipline cost to play this card from the discard pile. Remove it from the
  game after the action resolves.) Limited, :g1:: Give your target face-up Personality out of play Destined."

Discipline is a flag on the ability (AbilityDefinition.Discipline). The published GoldCost of a Discipline card holds the DISCIPLINE cost (the
catalogue's Cost box is blank, so build_definition_fields yields 0) -- the same convention as Banzai! (2), Lost in Transit (1) and Return to
Action (2). Played from hand it is not charged; played from a discard pile it is paid FIRST, and for the two "Limited, 1 Gold" cards the
action's own 1 Gold (an explicit Cost on the ability) is paid after it -- one combined quote (GoldQuotes.AbilityGoldCost). Mercantile Conflict
(oracle-l5r-10947) is NOT in this wave: it needs an "increase the next two Honor losses" primitive. See
docs/decisions/2026-09-19-discipline-rulings.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-23"

ABILITIES: dict[int, list[str]] = {
    10938: ["expensive-achievement-raise", "expensive-achievement-lower"],  # Expensive Achievement
    10942: ["knowledge-and-power"],  # Knowledge and Power
    10961: ["two-steady-breaths-destined"],  # Two Steady Breaths
}
EFFECTS: dict[int, str] = {}

# The Discipline icon's amount (see the module docstring); build_definition_fields reads the blank Cost box as 0.
GOLD_COST_OVERRIDES: dict[int, int] = {10938: 2, 10942: 1, 10961: 2}


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
        raise SystemExit("Usage: package_strategy_wave_23.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
