#!/usr/bin/env python3
"""Build Follower wave 31 (Emperor Edition remaining-cards pass, issue #492, group G): Bandit Gang, Ikiryo, Ikoma House Guard, Kaiu House Guard, Legion of Pain, Moto House Guard,
Tamori House Guard and Tsuruchi House Guard.

Only the cards this group actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye; the published Text is the
scan's own wording (the printing's text, not the catalogue's reworded Oracle text, where they differ), and each source receipt (with the working copy's sha256) is recorded next to
the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-31"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    683: [],  # Bandit Gang (an entry Honor loss and a pay-or-destroy trait)
    3589: ["ikiryo-attack"],  # Ikiryo
    3611: ["ikoma-house-guard-attack"],  # Ikoma House Guard
    4118: ["kaiu-house-guard-destroy"],  # Kaiu House Guard
    4699: [],  # Legion of Pain (an entry Honor loss and a bar on being targeted)
    5329: ["moto-house-guard-attack"],  # Moto House Guard
    7776: ["tamori-house-guard-stone"],  # Tamori House Guard
    8837: ["tsuruchi-house-guard-attack"],  # Tsuruchi House Guard
}
OVERRIDE: dict[int, dict] = {
    683: {"Effect": "bandit-gang", "EntryHonorLoss": 2},
    3611: {"AttachRequiresKeyword": "Lion Clan"},
    4118: {"AttachRequiresKeyword": "Crab Clan"},
    4699: {"Effect": "legion-of-pain-bar", "EntryHonorLoss": 2},
    5329: {"AttachRequiresKeyword": "Unicorn Clan"},
    7776: {"AttachRequiresKeyword": "Dragon Clan"},
    8837: {"AttachRequiresKeyword": "Mantis Clan"},
}
# The published Text is each scan's own wording (read by eye), where the catalogue's Oracle text is reworded.
TEXT: dict[int, str] = {
    683: "After this card enters play: Lose 2 Honor." + NL + "After a battle resolution ends, if this card is at the current battlefield: You may pay 2 Gold. If you do not, destroy this card.",
    3589: "Battle: Destroy this card: Ranged Attack, which may target a Personality with attached Followers, with strength equal to the Chi of the Personality to whom this card was last "
          "attached. Lose Honor equal to that Personality's Chi and dishonor him.",
    3611: "Will only attach to a Lion Clan Personality." + NL + "Battle: Melee Attack with strength equal to this Personality's Personal Honor plus two. If this Melee Attack destroyed a card, gain 1 Honor.",
    4118: "Will only attach to a Crab Clan Personality." + NL + "Battle: Target an enemy card without attachments whose unit has lower total Force than this unit: Destroy the target. Gain 1 Honor.",
    4699: "After this card enters play: Lose 2 Honor." + NL + "Actions performed by Personalities with a Gold Cost of 7 or lower may not target this Personality.",
    5329: "Will only attach to a Unicorn Clan Personality." + NL + "Battle: If any enemy units are at the current battlefield: Move this Personality there. If he is now there, Ranged 4 Attack.",
    7776: "Will only attach to a Dragon Clan Personality." + NL + "Battle: Target an enemy Personality or Follower: It is encased in stone. Give it -4F. It may not perform actions.",
    8837: "Will only attach to a Mantis Clan Personality." + NL + "Battle: Ranged 5 Attack that you may compare against Gold Cost.",
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
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
