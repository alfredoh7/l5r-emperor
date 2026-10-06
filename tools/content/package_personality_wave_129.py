#!/usr/bin/env python3
"""Personality wave 129 (issue #672, primitive P16): the four Personalities that demonstrate Tireless and Home as ability data, each read from its own scan (the printed text is the
published Text): Goju Obayashi and Daigotsu Kanpeki - exp2 (Aftermath), Moshi Sasako - exp and Shinjo Kinto - exp (Gates of Chaos). Engine support: src/L5R.Core/TirelessHome.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from tireless_home_common import NL, ROOT, package_cards

PROFILE = "personality-wave-129"
CARD_TYPE = "Personality"

ENTRIES: dict[int, dict] = {
    10877: dict(Slug="aftermath", Abilities=["obayashi-move-home"], Extra={"EntryHonorLoss": 2},  # Goju Obayashi
                Text="After Obayashi enters play, lose 2 Honor." + NL + "Ninja Tireless Battle: Move Obayashi home. (You may use Tireless abilities even if bowed.)"),
    10643: dict(Slug="gates-of-chaos", Abilities=["sasako-exp-straighten"],  # Moshi Sasako - exp
                Text="Tireless Battle: Straighten Sasako. (Tireless actions may be taken while bowed.)"),
    10674: dict(Slug="gates-of-chaos", Abilities=["kinto-exp-move-home", "kinto-exp-move-battle"],  # Shinjo Kinto - exp
                Text="Home Battle: Move Kinto home or, if he would be opposed, to the current battlefield. (Home actions may be taken from home.)"),
    10876: dict(Slug="aftermath", Abilities=["kanpeki-exp2-favor"],  # Daigotsu Kanpeki - exp2
                Text="Tireless Open: Take the Imperial Favor. (You may use Tireless abilities even if bowed.)"),
}
CARD_IDS = [f"oracle-l5r-{n}" for n in ENTRIES]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    return package_cards(PROFILE, CARD_TYPE, ENTRIES, card_ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
