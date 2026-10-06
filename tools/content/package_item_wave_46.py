#!/usr/bin/env python3
"""Build item-wave-46 (issue #667, the "performers" cluster of the Promotional pass): The Blade of the Balash, The Isha's Yumi - exp, Winter Sled (Promotional-Emperor scans) and Wyrmbone Katana
(Promotional-Celestial scan). Every card is wired from the PRINTED text of its scan. Engine support: src/L5R.Core/PerformersGear.cs and PerformersSled.cs.

The Blade and the Yumi can be discarded from the hand by a Dragon / Mantis Clan player (a Strategy-like Battle action whose source is the card itself); attached they are ordinary Weapons that
attach only to a Dragon (Mantis) Clan or Naga Personality ("Dragon Clan|Naga" lists alternatives). Winter Sled's trait is its Effect; Wyrmbone Katana prints two Battle actions.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-46"
NL = chr(10)
EMPEROR = ("promotional-emperor", "Promotional&ndash;Emperor")
CELESTIAL = ("promotional-celestial", "Promotional&ndash;Celestial")

# Oracle id -> (printing set, abilities, effect, extra definition fields, printed text of the scan).
CARDS: dict[int, tuple[tuple[str, str], list[str], str, dict, str]] = {
    9975: (EMPEROR, ["pf-balash-attack", "pf-balash-attack-hand"], "none", {"AttachRequiresKeyword": "Dragon Clan|Naga"},
           "Will only attach to a Dragon Clan or Naga Personality." + NL
           + "Battle: Bow this card or, if you are a Dragon Clan player, discard it from your hand: Melee 5 Attack."),
    9979: (EMPEROR, ["pf-yumi-attack", "pf-yumi-attack-hand"], "none", {"AttachRequiresKeyword": "Mantis Clan|Naga"},
           "Will only attach to a Mantis Clan or Naga Personality." + NL
           + "Battle: Bow this card or, if you are a Mantis Clan player, discard it from your hand: Ranged 6 Attack."),
    9350: (EMPEROR, ["pf-sled-move"], "pf-winter-sled", {},
           "While you control a Winter card, other players' cards' effects will not negate this Personality's movement." + NL
           + "Reaction: After engaging, if any enemy units are at the current battlefield, bow this card: Move this Personality there."),
    9414: (CELESTIAL, ["pf-wyrmbone-destroy", "pf-wyrmbone-move"], "none", {},
           "Battle: Target an enemy card with lower Force than this Personality and no attachments: Destroy it." + NL
           + "Battle: Move this Personality to the current battlefield if he assigned there."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        number = int(card_id.rsplit("-", 1)[1])
        (slug, set_name), abilities, effect, extra, text = CARDS[number]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        assert source["printing_set"] == [set_name], f"{card_id} provenance is {source['printing_set']}, not {set_name}"
        fields["Text"] = text  # the printed text of the scan
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(CARDS)]
    print(json.dumps(package(ids)))
