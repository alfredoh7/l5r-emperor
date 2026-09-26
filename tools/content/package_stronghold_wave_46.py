#!/usr/bin/env python3
"""Build Stronghold wave 46 (issue #503, pass group P5): The Plain of Glass, The New Foundries.

The New Foundries (a zone under the Stronghold: Spells put face-up under it and discarded from there to pay its element-dependent Battle action) and The Plain of Glass (Spider Clan and Fate Honor-loss immunity, a second Conquest in the same battle, Equip of Followers as a Battle action).

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording where the catalogue's Oracle text differs, and each source receipt
(with the working copy's sha256) is recorded next to the wave. The engine side is src/L5R.Core/EmperorP5.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "stronghold-wave-46"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    8260: [],  # The Plain of Glass
    9947: ["p5-foundries-put", "p5-foundries-straighten", "p5-foundries-attack", "p5-foundries-draw"],  # The New Foundries
}
OVERRIDE: dict[int, dict] = {
    8260: {"Effect": "p5-plain-of-glass"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    8260: "You do not lose Honor from Spider Clan and Fate cards you own.\nYou may use the rulebook Conquest ability twice per turn during the same battle.\nEquipping Followers is a Battle/Limited ability for you.",
    9947: "Limited: Put a Spell from your hand face-up underneath this card.\nBattle: Any number of times per turn, discard a Spell from under this card: Either straighten one to three Shugenja at the current battlefield if the Spell is Air or Water, or Ranged 4 Attack if it is Earth or Fire, or draw a card if it is Void.",
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
