#!/usr/bin/env python3
"""Build Item wave 9401 (group D, Promotional-Celestial): Inexpensive Armor and Colonist's Mark. Scans read by eye; wired in src/L5R.Core/PromoCelestialD.cs."""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-9401"
NL = chr(10)
REVIEWED_SET = "promotional-celestial"

ABILITIES: dict[int, list[str]] = {
    3742: [],  # Inexpensive Armor
    1432: ["promo-d-colonists-mark-negate-move"],  # Colonist's Mark
}
OVERRIDE: dict[int, dict] = {
    3742: {"AttackDefenseKind": "Ranged", "AttackDefenseAmount": -2, "AttackDefenseScope": "Wielder"},
}
# The published Text is each scan's own wording (read by eye against the Promotional-Celestial scans).
TEXT: dict[int, str] = {
    3742: "Ranged Attacks targeting this Personality have -2 strength.",
    1432: "Reaction: After an action targets this Personality, discard a card: Negate this Personality's movement from the action's effects.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=REVIEWED_SET)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        assert "Promotional" in str(source["printing_set"]) and "Celestial" in str(source["printing_set"]), f"{card_id}: wrong printing {source['printing_set']!r}"
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
