#!/usr/bin/env python3
"""Build Spell wave 26 (issue #503, group P1: src/L5R.Core/EmperorP1.cs): Damning Ceremony, Extending Influence, Mara's Touch, Ochiai's Bravery.

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is
recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-26"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1808: ['damning-ceremony'],  # Damning Ceremony
    2420: ['extending-influence-copy'],  # Extending Influence
    4833: ['maras-touch-cavalry', 'maras-touch-attack'],  # Mara's Touch
    5666: ['ochiai-bravery-duel'],  # Ochiai's Bravery
}
EFFECTS: dict[int, str] = {
    2420: 'extending-influence-air-honor',  # Extending Influence
    4833: 'maras-touch-nonhuman-force',  # Mara's Touch
    5666: 'ochiai-bravery-fire-duelist',  # Ochiai's Bravery
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    1808: 'Limited: Once per game: Give this Personality a permanent Force bonus raising his Force to equal his base Gold Cost. Permanently give him Shadowlands • Nonhuman • Oni. Lose 2 Honor.',  # Damning Ceremony
    2420: "After this card enters play, if this Shugenja is Air: Each other player loses 1 Honor.\nOpen: Destroy this card and target a Personality: The performing Shugenja copies one of the target's abilities which does not copy abilities.",  # Extending Influence
    4833: 'While this Shugenja is Nonhuman, he has +2F.\nOpen: Give this Shugenja –2F: He changes shape. Give him Cavalry.\nBattle: Give this Shugenja –2F: Ranged 6 Attack.',  # Mara's Touch
    5666: "While this Shugenja is Fire, he has Duelist.\nBattle: If this Shugenja is unbowed, bow this card and target an enemy Personality: This Shugenja duels him. Destroy a card without attachments in the unit of the duel's loser.",  # Ochiai's Bravery
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
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
