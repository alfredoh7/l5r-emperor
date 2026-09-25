#!/usr/bin/env python3
"""Build Strategy wave 81 (Emperor Edition mechanics group E, issue #501): Greasing the Wheels, The Heir's Wrath, The Host's Advantage.

Mendon's items 22-25 (EmperorMechE.cs): Greasing the Wheels' Focus Effect (its winner's Gold Cost lowered to 0) and its optional payment with an "otherwise" branch; The Heir's Wrath's Favor discard or
bow of a 5-Force Personality, its target limited to units the player can pay for, and its unnegatable effects; The Host's Advantage's mandatory and alternative Favor discards.

Every scan was read by eye (The Heir's Wrath's is the scan Mendon supplied); the published Text and Keywords are the scans' own (the catalogue lists "Favor" among the keywords of The Heir's Wrath and
The Host's Advantage and prints Favor/Political/Winter designators on the latter's actions; the scans do not), and each source receipt is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-81"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    2896: ["greasing-wheels-focus", "greasing-wheels-battle"],  # Greasing the Wheels
    8127: ["heirs-wrath-favor", "heirs-wrath-bow"],  # The Heir's Wrath
    8141: ["hosts-advantage-destroy", "hosts-advantage-bow"],  # The Host's Advantage
}
OVERRIDE: dict[int, dict] = {
    8127: {"Keywords": ["Imperial", "Political", "Unique"]},
    8141: {"Keywords": ["Political", "Winter"]},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    2896: "As a Focus Effect: After this duel ends, lower the Gold Cost of its winner to 0." + NL
          + "Battle: Choose your performing unbowed Courtier or Magistrate and target a Personality: You may pay Gold equal to his unit's total Gold Cost minus 6. If you did, move him home. Otherwise, straighten his unit.",
    8127: "Favor Imperial Political Limited, :gstar:: Discard the Imperial Favor or bow a Personality with 5 or more Force to lose 2 Honor and destroy another player's target Personality whose unit has a total Gold Cost equal to or less than what you paid for this action; these effects will not be negated.",
    8141: "Limited: Discard the Imperial Favor and target an attachment: Destroy it." + NL
          + "Battle: Choose your performing Courtier at any location, bow him or discard the Imperial Favor, and target an enemy Personality: Bow him. His controller loses 2 Honor.",
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
