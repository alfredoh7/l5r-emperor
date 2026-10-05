#!/usr/bin/env python3
"""Build Spell wave 9402 (group D, Promotional-Celestial): All Within the World, Channeling the Fallen, Hunger of the Fire. Wired in src/L5R.Core/PromoCelestialD.cs."""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-9402"
NL = chr(10)
REVIEWED_SET = "promotional-celestial"

ABILITIES: dict[int, list[str]] = {
    326: ["promo-d-all-within-ring", "promo-d-all-within-self"],  # All Within the World
    1296: ["promo-d-channeling-attack"],  # Channeling the Fallen
    3517: ["promo-d-hunger-fire-destroy"],  # Hunger of the Fire
}
OVERRIDE: dict[int, dict] = {
    1296: {"Effect": "promo-d-channeling-force"},
}
# The published Text is each scan's own wording (read by eye against the Promotional-Celestial scans).
TEXT: dict[int, str] = {
    326: "Battle/Open: If this Shugenja shares an element keyword with a Ring you control: Either straighten the Ring or this Shugenja.",
    1296: "This Shugenja has +5F." + NL + "Battle: Bow this card: Ranged 5 Attack, or Ranged 6 Attack if this Shugenja is Fire.",
    3517: "Battle: Destroy this card, bow this Shugenja unless he is Fire, and target an enemy card without attachments in a unit whose Personality has Force lower than or equal to your Shugenja's Force or Chi: Destroy the enemy card.",
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
