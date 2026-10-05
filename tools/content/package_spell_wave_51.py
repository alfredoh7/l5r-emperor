#!/usr/bin/env python3
"""Build Spell wave 51 (issue #653, the Promotional-Emperor pass): the Spells of the Promotional-Emperor set that the engine can play. Every scan was read by eye; the published Text is the scan's own
printed wording (the catalogue's text is only a hint), and each source receipt (with the working copy's sha256) is recorded next to the wave. Engine support: src/L5R.Core/PromoEmperorFate*.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from promo_emperor_common import NL, ROOT, package_cards

PROFILE = "spell-wave-51"

ENTRIES: dict[int, dict] = {
    5046: dict(Abilities=["pe-might-of-the-sun-bow", "pe-might-of-the-sun-free"], Effect="pe-might-of-the-sun",  # Might of the Sun
               Text="This Shugenja has +4F." + NL + "Battle: Bow this Shugenja unless he is Fire or Thunder and target an enemy card without attachments: Destroy it."),
    10414: dict(Abilities=["pe-summon-fushicho-search"],  # Summon Fushicho
                Text="Limited: Bow this Shugenja and destroy this card: Search your Fate deck for an Item, show it, and put it in your hand."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, ENTRIES, ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
