#!/usr/bin/env python3
"""Build Spell wave 22 (The Shadow's Embrace pass, issue #476, batch 5): Beseech Sakkaku, Channel the Moon, Purify the Soul, Set the Fields Aflame,
Thunder's Stamina, Touch the Emptiness, Ward of Air and Your Heart's Enemy.

Only the cards this batch actually wired are published here (an unwired card is never published with a blank ability list): Our Ancestor's Call and
Yari of Air create cards no local scan prints and stay unpublished (see BLOCKERS in build_card_inventory.py). Every scan was read by eye and its source
receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-22"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    9884: ["beseech-sakkaku-honor", "beseech-sakkaku-destroy"],  # Beseech Sakkaku
    9885: ["channel-moon-grant"],  # Channel the Moon
    9887: ["purify-soul-negate"],  # Purify the Soul
    9888: ["set-fields-terrain", "set-fields-region", "set-fields-holding"],  # Set the Fields Aflame
    9891: [],  # Thunder's Stamina (two traits, its Effect)
    9892: ["touch-emptiness-weaken"],  # Touch the Emptiness
    9893: ["ward-of-air-negate", "ward-of-air-move-home"],  # Ward of Air
    9895: ["hearts-enemy-weaken"],  # Your Heart's Enemy
}
# The published Text follows each scan (the Emperor-era The Shadow's Embrace printing), read by eye, where the catalogue's current Oracle text differs from it.
OVERRIDE: dict[int, dict] = {
    9884: dict(Text="Limited: Bow this Shugenja, you may destroy this card if he is Earth, and target your Spirit Personality: If you destroyed this card, gain "
                    "Honor equal to the Spirit's Personal Honor; if you did not destroy it, gain 1 Honor."),
    9885: dict(Effect="channel-the-moon",
               Text="While this Shugenja is Shadowlands, your opposed Nonhumans have +1F.\nOpen: Bow this card and choose your performing Nonhuman: Give him "
                    "the ability, \"Battle: Melee Attack with strength equal to this card's Force.\" Lose 3 Honor."),
    9887: dict(Effect="purify-the-soul"),
    9888: dict(Effect="set-fields-aflame"),
    9891: dict(Effect="thunders-stamina"),
    9892: dict(Text="Battle: Bow this card and target an enemy Personality or Follower: Give it -2F. If this Shugenja is Void, negate the effects of the next "
                    "action the target performs."),
    9893: dict(Text="Reaction: After another player's Battle action targets this Shugenja, or any Personality you control if this Shugenja is Air, destroy this "
                    "card: Negate the target's destruction from the action's effects.\nBattle: Move this unit home."),
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
