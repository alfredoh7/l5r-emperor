#!/usr/bin/env python3
"""Build Strategy wave 112 (issue #653, the Promotional-Emperor pass): the Strategies of the Promotional-Emperor set that the engine can play. Every scan was read by eye; the published Text is the
scan's own printed wording (the catalogue's text is only a hint), and each source receipt (with the working copy's sha256) is recorded next to the wave. Engine support: src/L5R.Core/PromoEmperorFate*.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from promo_emperor_common import NL, ROOT, package_cards

PROFILE = "strategy-wave-112"

# Oracle id -> the curated entry (Text is the scan's wording; Abilities / Effect are the engine's catalogue ids).
ENTRIES: dict[int, dict] = {
    9991: dict(Abilities=["pe-legendary-rivalry-bow"],  # Legendary Rivalry
               Text="Battle/Open: Target a Unique Personality: Bow him."),
    7788: dict(Abilities=["pe-tamori-potion-attack"],  # Tamori Potion
               Text="Battle: Bow your performing Personality: Ranged 4 Attack with +1 strength if he is an Alchemist and +1 strength for each different element keyword in his unit."),
    696: dict(Abilities=["simple-yari-focus", "pe-bar-fight-attack"],  # Bar Fight
              Text="As a Focus Effect: After this duel ends, bow its winner." + NL + "Battle: Choose your performing unbowed Personality: Melee Attack with strength equal to 4 plus the number of Sake tokens he has."),
    10597: dict(Abilities=["pe-glimpse-of-fate-draw"],  # A Glimpse of Fate
                Text="Battle/Open: You may discard a card. Draw cards until you have exactly two cards in your hand."),
    10600: dict(Abilities=["pe-elemental-adroitness-straighten"],  # Elemental Adroitness
                Text="Battle: Straighten a target Ring. Before this phase ends, put this Strategy into your hand."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, ENTRIES, ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
