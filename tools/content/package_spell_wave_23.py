#!/usr/bin/env python3
"""Build Spell wave 23 (Emperor Edition remaining-cards pass, issue #492, group E): Capturing the Soul, Cleansing the Path, Consecration, Contemplate the Void, Hanabi,
I Give You My Name, Obscured Pathways, The Kami's Blessing, Thunder's Favor, Unnatural Flood and Warded Paths.

Only the cards this group actually wired are published here (an unwired card is never published with a blank ability list): Chikushudo's Trickery (a battle fought outside the
Attack Phase at a battlefield that belongs to no province) and Summon Swamp Spirits (its Swamp Spirit Follower has no local scan) stay unpublished; see BLOCKERS in
build_card_inventory.py / ee_remE_blockers.json. Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-23"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1216: ["capturing-soul-remove"],  # Capturing the Soul
    1416: ["cleansing-path-attack"],  # Cleansing the Path
    1458: ["consecration-watch"],  # Consecration
    1471: ["contemplate-void-draw"],  # Contemplate the Void
    2954: ["hanabi-honor"],  # Hanabi
    3541: ["i-give-you-my-name-search"],  # I Give You My Name
    5649: ["obscured-pathways-move"],  # Obscured Pathways
    8175: ["kami-blessing-self", "kami-blessing-samurai"],  # The Kami's Blessing
    8473: ["thunders-favor-attack"],  # Thunder's Favor
    8997: ["unnatural-flood-move"],  # Unnatural Flood
    9228: [],  # Warded Paths (two traits, its Effect)
}
# The published Text follows each scan, read by eye, where the catalogue's reworded Oracle text differs from it.
OVERRIDE: dict[int, dict] = {
    1216: dict(Text="Limited: Bow this Shugenja and 0 or more of your other performing Shugenja, target a Personality, and remove this card from the game: If the total Chi of all "
                    "your performing Shugenja is higher than three times the Chi of the Personality, remove him (and his attachments) from the game."),
    8175: dict(Text="Battle/Open: Bow this card: Give this Shugenja a Force bonus equal to his Chi." + NL + "Battle: Bow this card and target one or two Samurai: Give each of them "
                    "a Force bonus equal to this Shugenja's Chi."),
    9228: dict(Effect="warded-paths"),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
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
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
