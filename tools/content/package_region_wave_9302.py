#!/usr/bin/env python3
"""Build Region wave 9302 (Promotional-Celestial group C): Deadlands, The Ivory Coast, War-ravaged Plain. Printed text read from each scan; engine side src/L5R.Core/PromoCelestialC.cs."""
from __future__ import annotations

from pathlib import Path

from package_promo_celestial_c_common import NL, ROOT, main, package_profile

PROFILE = "region-wave-9302"
CARDS: dict[int, dict] = {
    1879: dict(slug="promotional-celestial", type="Region", cost=0, keywords=["Shadowlands"], abilities=["pcc-deadlands-refill"], extra=dict(EntryHonorLoss=4),  # Deadlands
               text="After this card enters play: Lose 4 Honor." + NL + "Limited: If there are any cards in this province, discard this card: "
               "Discard a card from the province, refilling it face-up with an Undead Personality in your discard pile."),
    8161: dict(slug="promotional-celestial", type="Region", cost=0, keywords=["Singular"], abilities=["pcc-ivory-coast-home"],  # The Ivory Coast
               text="Battle: Choose your performing Personality and bow this card: Move him home. Straighten his unit if he moved."),
    9224: dict(slug="promotional-celestial", type="Region", cost=0, keywords=[], abilities=["pcc-war-ravaged-destroy"],  # War-ravaged Plain
               text="Limited: Discard this card and target a Region: Destroy it."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    return package_profile(PROFILE, CARDS, card_ids, root)


if __name__ == "__main__":
    main(package, "Usage: package_region_wave_9302.py oracle-l5r-<id> [...]")
