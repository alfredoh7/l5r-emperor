#!/usr/bin/env python3
"""Spell wave 54 (issue #672, primitive P16): My Ancestor's Steed (Gates of Chaos), read from its scan -- a Home action on a Spell. Engine support: src/L5R.Core/TirelessHome.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from tireless_home_common import ROOT, package_cards

PROFILE = "spell-wave-54"
CARD_TYPE = "Spell"

ENTRIES: dict[int, dict] = {
    10705: dict(Slug="gates-of-chaos", Abilities=["ancestors-steed-move"],  # My Ancestor's Steed (the printed bow icon is written "Bow", as Daigotsu Ryuko's is)
                Text="Home Water Battle, Bow: Move your target Spirit Personality at any location to the current battlefield. (Home actions may be taken from home.)"),
}
CARD_IDS = [f"oracle-l5r-{n}" for n in ENTRIES]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    return package_cards(PROFILE, CARD_TYPE, ENTRIES, card_ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
