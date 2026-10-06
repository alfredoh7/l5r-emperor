#!/usr/bin/env python3
"""Item wave 47 (issue #672, primitive P16): Koan's Scroll Satchel (Coils of Madness), read from its scan -- it gives Home to Spells in its unit. Engine support: src/L5R.Core/TirelessHome.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from tireless_home_common import ROOT, package_cards

PROFILE = "item-wave-47"
CARD_TYPE = "Item"

ENTRIES: dict[int, dict] = {
    10526: dict(Slug="coils-of-madness", Abilities=["koan-satchel-home"],  # Koan's Scroll Satchel (the printed bow icon is written "Bow")
                Text="Open, Bow: Give Home to one or more Spells in this unit, up to the number of Koan cards in this unit. (Home actions may be used from home.)"),
}
CARD_IDS = [f"oracle-l5r-{n}" for n in ENTRIES]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    return package_cards(PROFILE, CARD_TYPE, ENTRIES, card_ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
