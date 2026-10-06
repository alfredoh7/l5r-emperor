#!/usr/bin/env python3
"""Personality wave 124 (issue #667, Promotional-Emperor blocked-card wave, cluster "duel-sleep"): Fusako and Gaijin Sorcerer, each from its Promotional-Emperor scan.
The scan's printed wording is the Text (Fusako prints "Iaijustu Limited"; Gaijin Sorcerer's trait is "While your ritual is resolving, Gaijin Sorcerer counts as two Shugenja.").
Mendon's confirmed rulings: Fusako's challenged Personality may refuse only while bowed (a straightened one may bow to gain the right); the Sorcerer's doubling counts only toward the
number of Shugenja and never doubles his Chi contribution. Engine support: src/L5R.Core/DuelSleepFusako.cs and DuelSleepSorcerer.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from promo_emperor_common import ROOT, package_cards

PROFILE = "personality-wave-124"

ENTRIES: dict[int, dict] = {
    10232: dict(Abilities=["fusako-duel"], Effect="none",  # Fusako
                Text="Iaijustu Limited: Target another player's Personality: Fusako challenges the target. His controller may bow him; if he is now bowed, he may refuse the duel. Destroy the duel's loser."),
    10585: dict(Abilities=[], Effect="gaijin-sorcerer-two-shugenja",  # Gaijin Sorcerer
                Text="While your ritual is resolving, Gaijin Sorcerer counts as two Shugenja."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, ENTRIES, ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
