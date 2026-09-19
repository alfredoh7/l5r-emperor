#!/usr/bin/env python3
"""Build Item wave 4 (issue #335): Blade of Champions (oracle-l5r-1004), a Unique Weapon whose Reaction redirects a Battle
action aimed at a card in its unit to another of its controller's cards ("Reaction: When a Battle action would target a card
in this unit, bow this card: The action targets another of your cards instead, if legal."). It is the blade-of-champions-
redirect ability of the targeted-by-action Reaction family (TargetedActionReactions.cs), shared with Mirumoto Ezuno.

The scan (Second City printing 1) was read by eye and its working copy sha256-checked against the receipt. Force +3, Chi +1,
Cost 5, Focus 3, Unique Weapon, no attach restriction.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the card's own scanned image,
never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-4"

ABILITIES: dict[int, list[str]] = {
    1004: ["blade-of-champions-redirect"],  # Blade of Champions
}
EFFECTS: dict[int, str] = {}


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
        raise SystemExit("Usage: package_item_wave_4.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
