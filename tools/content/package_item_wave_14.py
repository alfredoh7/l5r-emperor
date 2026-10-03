#!/usr/bin/env python3
"""Build Item wave 14 (The Shadow's Embrace pass, issue #476, batch 5): Blood of the Preserver, Celestial Sword of the Crab, Celestial Sword of the Crane,
Celestial Sword of the Phoenix, The String of the Colonies, The Victor and Vanquisher's Armor.

Only the cards this batch actually wired are published here (an unwired card is never published with a blank ability list): Vanquisher's Blades creates
Readied Blade, whose scan is not available locally, and stays unpublished (see BLOCKERS in build_card_inventory.py). Every scan was read by eye and its
source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-14"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    9875: ["blood-preserver-remove"],  # Blood of the Preserver
    9876: ["celestial-sword-crab-honor", "celestial-sword-crab-straighten"],  # Celestial Sword of the Crab
    9877: ["celestial-sword-crane-honor", "celestial-sword-crane-weaken", "celestial-sword-crane-favor"],  # Celestial Sword of the Crane
    9878: ["celestial-sword-phoenix-honor", "celestial-sword-phoenix-return"],  # Celestial Sword of the Phoenix
    9880: ["string-colonies-attack"],  # The String of the Colonies
    9881: [],  # The Victor ("This Personality has Conqueror.")
    9882: [],  # Vanquisher's Armor (an Attack-strength trait)
}
# The published Text follows each scan (the Emperor-era The Shadow's Embrace printing), read by eye, where the catalogue's current Oracle text differs from it.
OVERRIDE: dict[int, dict] = {
    9876: dict(Effect="celestial-sword-force-crab", AttachRequiresKeyword="Crab Clan"),
    9877: dict(Effect="celestial-sword-force-crane", AttachRequiresKeyword="Crane Clan",
               Text="Will only attach to a Crane Clan Personality.\nThis card has +1F for each Crane Clan Personality you control.\nAfter this card enters play: "
                    "Gain 2 Honor.\nPolitical Open: Target a Personality or Follower and you may discard the Imperial Favor: Give the target -4F. If you "
                    "discarded the Imperial Favor, gain 1 Honor."),
    9878: dict(Effect="celestial-sword-force-phoenix", AttachRequiresKeyword="Phoenix Clan"),
    9881: dict(AttachGrantsKeyword="Conqueror"),
    9882: dict(AttackDefenseKind="Both", AttackDefenseAmount=-5, AttackDefenseScope="Wielder"),
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
