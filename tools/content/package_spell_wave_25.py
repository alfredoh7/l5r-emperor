#!/usr/bin/env python3
"""Build Spell wave 25 (Emperor Edition rulings pass R1, issue #492): Chikushudo's Trickery, blocked until Mendon ruled on a battle without a province (a battlefield may have no province;
the engine's additional battlefield slot and a battle fought inside the Action Phase, EmperorRulingR1.cs).

Its scan was read by eye and its source receipt (with the working copy's sha256) is recorded next
to the wave. The published Text follows the scan (the Emperor Edition printing), which differs from the Oracle's reworded text; the scan's "Earth • Forest" line is the two keywords.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-25"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1323: ["chikushudo-trickery-battle"],  # Chikushudo's Trickery
}
TEXT: dict[int, str] = {
    1323: "Open: Choose one or two of your performing unbowed Nonhuman Spirit Personalities, target another player's unbowed Personality, bow this Shugenja, and destroy this card: "
          "Create a battlefield. Negate all movement to it. Assign the target to attack there; he will not be destroyed during the battle's Combat Segment. Assign the performing "
          "Spirits to defend there. Fight a battle there.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="emperor-edition")
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
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
